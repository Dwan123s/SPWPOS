param([switch]$SkipCertificateTrust, [switch]$UseMailpit, [switch]$RotateSecurityKeys)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$startupLease = $null
Push-Location -LiteralPath $taskRoot
try {
    # Include older framework builds from this checkout so an upgrade cannot
    # migrate its database while the previous API is still serving requests.
    $ownedApiBin = [IO.Path]::GetFullPath((Join-Path $taskRoot 'src\SPWPOS.Api\bin')) + '\'
    $runningApi = Get-CimInstance Win32_Process -Filter "Name = 'SPWPOS.Api.exe'" |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($ownedApiBin, [StringComparison]::OrdinalIgnoreCase) }
    if ($runningApi) {
        Write-Host 'SPWPOS is already running: https://localhost:7043'
        Write-Host 'Stop the existing API before restarting after configuration changes.'
        return
    }
    & (Join-Path $PSScriptRoot 'Build-Frontend.ps1') -Install
    $artifactDirectory = Join-Path $taskRoot 'artifacts'
    $null = New-Item -ItemType Directory -Path $artifactDirectory -Force
    try {
        $startupLease = [IO.File]::Open((Join-Path $artifactDirectory '.development-startup.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    } catch [IO.IOException] {
        throw 'Another SPWPOS development startup is in progress. Wait for that command to finish instead of running startup twice.'
    }
    . (Join-Path $PSScriptRoot 'Initialize-Development.ps1') -StartContainers -UseMailpit:$UseMailpit -RotateSecurityKeys:$RotateSecurityKeys
    & $SpwposDotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed. Startup stopped.' }
    & $SpwposDotnet restore --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed. Startup stopped.' }
    if (-not $SkipCertificateTrust) {
        & $SpwposDotnet dev-certs https --trust
        if ($LASTEXITCODE -ne 0) { throw 'HTTPS certificate setup failed. Startup stopped.' }
    }
    & $SpwposDotnet ef database update --project src/SPWPOS.Infrastructure
    if ($LASTEXITCODE -ne 0) { throw 'Database migration failed. API was not started.' }
    $startupLease.Dispose()
    $startupLease = $null
    Write-Host 'SPWPOS UI: https://localhost:7043'
    Write-Host 'Swagger: https://localhost:7043/swagger'
    & $SpwposDotnet run --project src/SPWPOS.Api --launch-profile https -p:SkipFrontendBuild=true
    if ($LASTEXITCODE -ne 0) { throw 'API stopped with an error.' }
} finally {
    if ($startupLease) { $startupLease.Dispose() }
    Pop-Location
}
