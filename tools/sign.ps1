<#
  NEX ORDER DESK - optional code signing (called by build-release.ps1 for every exe and msi it makes).

  Does NOTHING unless one of these is set (locally as environment variables, on GitHub as repository secrets):
    SIGN_PFX_BASE64 + SIGN_PFX_PASSWORD   a code-signing certificate exported as .pfx, base64-encoded
                                          -> signed with signtool (SHA256 + RFC3161 timestamp)
    SIGN_COMMAND                          any signing command line; the file path is added at the end
                                          (e.g. a cloud signing tool such as Azure Artifact Signing or SignPath)
  A signed setup and MSI show "Verified publisher" in Windows and stop the SmartScreen warning once the
  certificate has built up reputation. See RELEASING.md, section "Windows SmartScreen".
#>
param([Parameter(Mandatory = $true)][string]$File)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $File)) { throw "Cannot sign - file not found: $File" }

if ($env:SIGN_COMMAND) {
  Write-Host "  signing $([IO.Path]::GetFileName($File)) (SIGN_COMMAND)"
  & cmd.exe /c "$($env:SIGN_COMMAND) `"$File`""
  if ($LASTEXITCODE -ne 0) { throw "Signing failed for $File" }
  return
}

if ($env:SIGN_PFX_BASE64) {
  $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
  $signtool = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
  if (-not $signtool) { throw 'signtool.exe not found - install the Windows SDK to sign.' }
  $pfx = Join-Path $env:TEMP ("nex_sign_" + [guid]::NewGuid().ToString('N') + '.pfx')
  [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:SIGN_PFX_BASE64))
  try {
    Write-Host "  signing $([IO.Path]::GetFileName($File)) (certificate)"
    & $signtool.FullName sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /d "NEX ORDER DESK" `
      /f $pfx /p $env:SIGN_PFX_PASSWORD $File
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $File" }
  } finally { Remove-Item $pfx -Force -ErrorAction SilentlyContinue }
}
