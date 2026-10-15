param()
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskSdkVersion = (Get-Content -LiteralPath (Join-Path $taskRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
if ($taskSdkVersion -notmatch '^8\.0\.\d+$') { throw 'The project does not select a stable .NET 8 SDK.' }
$taskSdkRoot = Join-Path $taskRoot 'artifacts\dotnet8'
$taskSdkExe = Join-Path $taskSdkRoot 'dotnet.exe'
if (Test-Path -LiteralPath (Join-Path $taskSdkRoot ('sdk\' + $taskSdkVersion))) { Write-Host "SDK $taskSdkVersion is already installed for this project."; return }
$taskMetadata = Invoke-RestMethod -Uri 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json' -TimeoutSec 30
$taskSdk = @($taskMetadata.releases | ForEach-Object { $_.sdk; $_.sdks } | Where-Object { $_.version -eq $taskSdkVersion }) | Select-Object -First 1
$taskDownload = $taskSdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $taskDownload -or $taskDownload.url -notmatch '^https://(builds\.dotnet\.microsoft\.com|download\.visualstudio\.microsoft\.com)/') { throw 'The selected SDK was not found on the official Microsoft feed.' }
$taskDownloadDir = Join-Path $taskRoot 'artifacts\runtime-downloads'
[IO.Directory]::CreateDirectory($taskDownloadDir) | Out-Null
$taskArchive = Join-Path $taskDownloadDir ('dotnet-sdk-' + $taskSdkVersion + '-win-x64.zip')
Invoke-WebRequest -Uri $taskDownload.url -OutFile $taskArchive -TimeoutSec 300
if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA512).Hash -ne $taskDownload.hash) { throw 'The SDK SHA-512 checksum did not match the Microsoft release metadata.' }
Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskSdkRoot -Force
Write-Host "Installed Microsoft .NET SDK $taskSdkVersion for this project (SHA-512 verified)."
