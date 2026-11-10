# NEX ORDER DESK - install the newest version from PowerShell (published with every release as install.ps1).
#
#   irm https://github.com/__REPO__/releases/latest/download/install.ps1 | iex
#
# It reads the release information, downloads the setup program, checks its SHA256 checksum and starts it.
# Files fetched this way are not marked as "downloaded from the internet", so Windows SmartScreen does not
# show its blue warning - Windows still asks for permission (UAC) before installing, as usual.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$repo = '__REPO__'
$base = "https://github.com/$repo/releases/latest/download"

Write-Host 'NEX ORDER DESK - looking for the newest version...' -ForegroundColor Cyan
$wc = New-Object Net.WebClient
$wc.Encoding = [Text.Encoding]::UTF8
$wc.Headers['User-Agent'] = 'NexOrderDesk-Install'
$m = $wc.DownloadString("$base/update.json") | ConvertFrom-Json
$s = $m.setup
if (-not $s -or -not $s.downloadUrl -or -not $s.downloadUrl.StartsWith("https://github.com/$repo/releases/download/")) {
  throw 'This release has no setup program.'
}

$dir = Join-Path $env:TEMP 'NexOrderDesk_Setup'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$exe = Join-Path $dir 'NexOrderDesk_Setup.exe'
Write-Host "Downloading NEX ORDER DESK $($m.version) setup..."
$wc.Headers['User-Agent'] = 'NexOrderDesk-Install'
$wc.DownloadFile($s.downloadUrl, $exe)

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
if ($hash -ne ([string]$s.sha256).ToUpperInvariant()) {
  Remove-Item $exe -Force
  throw 'The download failed the SHA256 security check and was deleted.'
}
Write-Host 'Starting the setup - allow the Windows permission prompt.' -ForegroundColor Green
Start-Process -FilePath $exe
