param([switch]$SkipCertificateTrust, [switch]$UseMailpit, [switch]$RotateSecurityKeys)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$startupLease = $null
function Assert-DevelopmentPortAvailable {
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort 7043 -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        $owners = ($listeners.OwningProcess | Sort-Object -Unique) -join ', '
        throw "HTTPS port 7043 is already in use (PID: $owners). Stop that server in its original terminal before starting SPWPOS again."
    }
}
Push-Location -LiteralPath $taskRoot
try {
    $artifactDirectory = Join-Path $taskRoot 'artifacts'
    $null = New-Item -ItemType Directory -Path $artifactDirectory -Force
    try {
        # Keep the lease until dotnet run exits, including the gap before Kestrel binds.
        $startupLease = [IO.File]::Open((Join-Path $artifactDirectory '.development-startup.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    } catch [IO.IOException] {
        throw 'Another SPWPOS development startup is in progress or its server is still running. Stop it in its original terminal before starting again.'
    }
    # Include older framework builds from this checkout so an upgrade cannot
    # migrate its database while the previous API is still serving requests.
    $ownedApiBin = [IO.Path]::GetFullPath((Join-Path $taskRoot 'src\SPWPOS.Api\bin')) + '\'
    $ownedApiRuntime = [IO.Path]::GetFullPath((Join-Path $artifactDirectory 'api-runtime')) + '\'
    $runningApi = Get-CimInstance Win32_Process -Filter "Name = 'SPWPOS.Api.exe'" |
        Where-Object { $_.ExecutablePath -and ($_.ExecutablePath.StartsWith($ownedApiBin, [StringComparison]::OrdinalIgnoreCase) -or
            $_.ExecutablePath.StartsWith($ownedApiRuntime, [StringComparison]::OrdinalIgnoreCase)) }
    if ($runningApi) {
        Write-Host 'SPWPOS is already running: https://localhost:7043'
        Write-Host 'Stop the existing API before restarting after configuration changes.'
        return
    }
    Assert-DevelopmentPortAvailable
    & (Join-Path $PSScriptRoot 'Build-Frontend.ps1') -Install
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
    Assert-DevelopmentPortAvailable
    Write-Host 'SPWPOS UI: https://localhost:7043'
    Write-Host 'Swagger: https://localhost:7043/swagger'
    & $SpwposDotnet run --project src/SPWPOS.Api --launch-profile https -p:SkipFrontendBuild=true
    if ($LASTEXITCODE -ne 0) { throw 'API stopped with an error.' }
} finally {
    if ($startupLease) { $startupLease.Dispose() }
    Pop-Location
}
