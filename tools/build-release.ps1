<#
  NEX ORDER DESK - release builder (used by build_release.bat, build_msi.bat and the GitHub Actions workflow)

  Produces in .\release\ :
    NexOrderDesk_<ver>_Setup.msi        light installer (needs .NET 8 Desktop Runtime x64)
    NexOrderDesk_<ver>_Setup_Full.msi   full installer  (self-contained, nothing else needed)
    NexOrderDesk_Setup.exe              one-click setup (like Chrome): downloads the right MSI from GitHub and installs it
    NexOrderDesk_Setup_Offline.exe      one-click setup with the full installer inside (no internet needed)
    install.ps1                         PowerShell installer:  irm <release>/install.ps1 | iex
    update.json                         update manifest read by the installed app
    SHA256SUMS.txt                      checksums of the installers
    RELEASE_NOTES.md                    the notes used for the GitHub release

  Version, repository and minimum supported version default to Directory.Build.props.
  Every exe / msi is passed to tools\sign.ps1 (does nothing unless a signing certificate is configured).
  Works with Windows PowerShell 5.1 and PowerShell 7.
#>
param(
  [string]$Version,                     # e.g. 2.7.1   (default: Directory.Build.props)
  [string]$Repository,                  # owner/repo  (default: Directory.Build.props UpdateRepository)
  [string]$MinimumVersion,              # default: Directory.Build.props MinimumSupportedVersion
  [string]$NotesFile,                   # default: release-notes\v<ver>.md
  [string[]]$Variants = @('light', 'full'),
  [string]$ReleaseDate,                 # yyyy-MM-dd (default: today, UTC)
  [switch]$SkipSetup                    # only the MSI files (build_msi.bat)
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$proj = 'src\NexOrderDesk\NexOrderDesk.csproj'
$wix = 'installer\NexOrderDesk.Installer.wixproj'

function Prop([string]$name) {
  $v = & dotnet msbuild $proj -nologo "-getProperty:$name"
  if ($LASTEXITCODE -ne 0) { throw "Could not read $name from the project." }
  return ($v | Out-String).Trim()
}
function Step([string]$t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Cyan }
function Sign([string]$file) { & (Join-Path $PSScriptRoot 'sign.ps1') -File $file | Out-Host }
function FileEntry([string]$path, [string]$url) {
  $f = Get-Item $path
  return [ordered]@{
    fileName    = $f.Name
    downloadUrl = $url
    sha256      = (Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    size        = $f.Length
  }
}

if (-not $Version) { $Version = Prop 'Version' }
$Version = $Version.Trim().TrimStart('v', 'V')
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 2.7.1 (got '$Version')." }
$parts = $Version.Split('.') | ForEach-Object { [int]$_ }
if ($parts[0] -gt 255 -or $parts[1] -gt 255 -or $parts[2] -gt 65535) { throw "MSI versions are limited to 255.255.65535." }
if (-not $Repository) { $Repository = Prop 'UpdateRepository' }
if (-not $MinimumVersion) { $MinimumVersion = Prop 'MinimumSupportedVersion' }
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $Repository -eq 'OWNER/REPO') {
  Write-Warning "UpdateRepository is '$Repository' - this build will NOT be able to check for online updates."
}
if (-not $ReleaseDate) { $ReleaseDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd') }
$tag = "v$Version"

Write-Host "NEX ORDER DESK release $Version  (repo: $Repository, minimum supported: $MinimumVersion, variants: $($Variants -join ', '))"

$out = Join-Path $root 'release'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null

function Get-MsiProperty([string]$path, [string]$prop) {
  try {
    $wi = New-Object -ComObject WindowsInstaller.Installer
    $db = $wi.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $wi, @($path, 0))
    $view = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, @("SELECT Value FROM Property WHERE Property='$prop'"))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
    $rec = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
    $val = $null
    if ($rec) { $val = $rec.GetType().InvokeMember('StringData', 'GetProperty', $null, $rec, 1) }
    $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($view)
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($db)
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($wi)
    return $val
  } catch { Write-Warning "Could not read $prop from the MSI: $($_.Exception.Message)"; return $null }
}

$installers = [ordered]@{}
foreach ($variant in $Variants) {
  Step "Publishing NexOrderDesk.exe ($variant)"
  if (Test-Path 'installer\payload') { Remove-Item 'installer\payload' -Recurse -Force }
  $selfContained = if ($variant -eq 'full') { 'true' } else { 'false' }
  $pub = @('publish', $proj, '-c', 'Release', '-r', 'win-x64', '--self-contained', $selfContained, '-nologo',
           '-p:PublishSingleFile=true', '-p:DebugType=none', "-p:Version=$Version",
           "-p:UpdateRepository=$Repository", "-p:MinimumSupportedVersion=$MinimumVersion", "-p:Distribution=$variant",
           '-o', 'installer\payload')
  if ($variant -eq 'full') { $pub += @('-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true') }
  & dotnet @pub
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($variant)." }
  $exe = 'installer\payload\NexOrderDesk.exe'
  if (-not (Test-Path $exe)) { throw "NexOrderDesk.exe was not produced ($variant)." }
  $exeVer = (Get-Item $exe).VersionInfo.ProductVersion
  if (-not $exeVer.StartsWith($Version)) { throw "Version check failed: exe says '$exeVer', expected '$Version'." }
  Sign $exe

  Step "Building the MSI ($variant)"
  foreach ($d in 'installer\bin', 'installer\obj') { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
  & dotnet build $wix -c Release -nologo "-p:Version=$Version"
  if ($LASTEXITCODE -ne 0) { throw "MSI build failed ($variant)." }
  $msi = Get-ChildItem 'installer\bin' -Recurse -Filter '*.msi' | Select-Object -First 1
  if (-not $msi) { throw "No MSI produced ($variant)." }

  $name = if ($variant -eq 'full') { "NexOrderDesk_${Version}_Setup_Full.msi" } else { "NexOrderDesk_${Version}_Setup.msi" }
  $dest = Join-Path $out $name
  Copy-Item $msi.FullName $dest
  Sign $dest

  Step "Validating $name"
  $msiVer = Get-MsiProperty $dest 'ProductVersion'
  if ($msiVer -and $msiVer -ne $Version) { throw "MSI ProductVersion is '$msiVer', expected '$Version'." }
  $code = Get-MsiProperty $dest 'UpgradeCode'
  if ($code -and $code -ne '{7F3C2A51-9B64-4E2D-8C1A-5D0E3B7A9C42}') { throw "MSI UpgradeCode changed ($code) - updates would install side by side." }
  $size = (Get-Item $dest).Length
  if ($size -lt 100000) { throw "MSI is suspiciously small ($size bytes)." }
  $hash = (Get-FileHash $dest -Algorithm SHA256).Hash.ToUpperInvariant()
  Write-Host "  ProductVersion $msiVer, $([math]::Round($size / 1MB, 2)) MB, SHA256 $hash"

  $installers[$variant] = [ordered]@{
    fileName    = $name
    downloadUrl = "https://github.com/$Repository/releases/download/$tag/$name"
    sha256      = $hash
    size        = $size
  }
}
foreach ($d in 'installer\payload', 'installer\bin', 'installer\obj') { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }

# ---------------------------------------------------------------- one-click setup programs
$setupProj = 'src\NexOrderDesk.Setup\NexOrderDesk.Setup.csproj'
function Build-Setup([string]$offlineMsi, [string]$destName) {
  foreach ($d in 'src\NexOrderDesk.Setup\bin', 'src\NexOrderDesk.Setup\obj', 'installer\setupout') { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
  $b = @('build', $setupProj, '-c', 'Release', '-nologo', "-p:Version=$Version", "-p:UpdateRepository=$Repository", '-o', 'installer\setupout')
  if ($offlineMsi) { $b += "-p:OfflineMsi=$((Resolve-Path $offlineMsi).Path)" }
  & dotnet @b | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "Setup build failed ($destName)." }
  $built = 'installer\setupout\NexOrderDesk_Setup.exe'
  if (-not (Test-Path $built)) { throw "NexOrderDesk_Setup.exe was not produced ($destName)." }
  $v = (Get-Item $built).VersionInfo.ProductVersion
  if (-not $v.StartsWith($Version)) { throw "Setup version check failed: '$v', expected '$Version'." }
  $dest = Join-Path $out $destName
  Copy-Item $built $dest
  Sign $dest
  foreach ($d in 'src\NexOrderDesk.Setup\bin', 'src\NexOrderDesk.Setup\obj', 'installer\setupout') { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
  Write-Host ("  {0}  {1:N1} MB" -f $destName, ((Get-Item $dest).Length / 1MB))
  return $dest
}

$setups = [ordered]@{}
if (-not $SkipSetup) {
  Step 'Building the one-click setup (online)'
  $p = Build-Setup $null 'NexOrderDesk_Setup.exe'
  $setups['setup'] = FileEntry $p "https://github.com/$Repository/releases/download/$tag/NexOrderDesk_Setup.exe"
  if ($installers.Contains('full')) {
    Step 'Building the one-click setup (offline, full installer inside)'
    $p = Build-Setup (Join-Path $out $installers['full'].fileName) 'NexOrderDesk_Setup_Offline.exe'
    $setups['offlineSetup'] = FileEntry $p "https://github.com/$Repository/releases/download/$tag/NexOrderDesk_Setup_Offline.exe"
  }
  $ps = (Get-Content (Join-Path $PSScriptRoot 'install.ps1') -Raw).Replace('__REPO__', $Repository)
  [IO.File]::WriteAllText((Join-Path $out 'install.ps1'), $ps, (New-Object System.Text.UTF8Encoding($false)))
}

Step 'Release notes'
if (-not $NotesFile) { $NotesFile = "release-notes\$tag.md" }
if (Test-Path $NotesFile) { $notes = (Get-Content $NotesFile -Raw).Trim() }
else {
  Write-Warning "$NotesFile not found - using a short default text."
  $notes = "NEX ORDER DESK $Version`n`n- Improvements and fixes."
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $out 'RELEASE_NOTES.md'), $notes, $utf8)

Step 'Checksums and update manifest'
$sums = (@($installers.Values) + @($setups.Values) | ForEach-Object { "$($_.sha256.ToLowerInvariant())  $($_.fileName)" }) -join "`n"
[IO.File]::WriteAllText((Join-Path $out 'SHA256SUMS.txt'), $sums + "`n", $utf8)

$main = if ($installers.Contains('light')) { $installers['light'] } else { $installers[@($installers.Keys)[0]] }
$manifest = [ordered]@{
  schema                  = 1
  app                     = 'NexOrderDesk'
  version                 = $Version
  tag                     = $tag
  releaseDate             = $ReleaseDate
  minimumSupportedVersion = $MinimumVersion
  installerFileName       = $main.fileName
  downloadUrl             = $main.downloadUrl
  sha256                  = $main.sha256
  size                    = $main.size
  releaseNotes            = $notes
  releasePage             = "https://github.com/$Repository/releases/tag/$tag"
  installers              = $installers
}
foreach ($k in $setups.Keys) { $manifest[$k] = $setups[$k] }
[IO.File]::WriteAllText((Join-Path $out 'update.json'), ($manifest | ConvertTo-Json -Depth 6), $utf8)

Step 'Done'
Get-ChildItem $out | ForEach-Object { Write-Host ("  {0,-42} {1,10:N0} bytes" -f $_.Name, $_.Length) }
Write-Host ""
Write-Host "Release files are in: $out" -ForegroundColor Green
