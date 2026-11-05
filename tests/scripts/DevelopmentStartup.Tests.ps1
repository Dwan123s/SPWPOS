$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/startup-tests/' + [Guid]::NewGuid().ToString('N'))
$expectedParent = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $fixtureRoot.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid fixture directory.' }
function Assert-Condition([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Get-CimInstance {
    param($ClassName, $Filter)
    if ($global:StartupFixtureApiPath) { return [pscustomobject]@{ ExecutablePath=$global:StartupFixtureApiPath; ProcessId=12345 } }
}
function Get-NetTCPConnection {
    param($State, $LocalPort, $ErrorAction)
    $global:StartupFixturePortChecks++
    if ($global:StartupFixtureBusy -or ($global:StartupFixtureLateBusy -and $global:StartupFixturePortChecks -gt 1)) {
        return [pscustomobject]@{ OwningProcess=23456 }
    }
}
function Invoke-StartupFixtureDotnet {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
    $global:LASTEXITCODE = 0
    $global:StartupFixtureCommands += $Arguments[0]
    if ($Arguments[0] -eq 'run') {
        $locked = $false
        try {
            $lease = [IO.File]::Open((Join-Path $fixtureRoot 'artifacts/.development-startup.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
            $lease.Dispose()
        } catch [IO.IOException] { $locked = $true }
        Assert-Condition $locked 'Startup lease was released before the API exited.'
        if ($global:StartupFixtureFailRun) { $global:LASTEXITCODE = 1 }
    }
}
function Reset-Fixture {
    $global:StartupFixtureApiPath = $null
    $global:StartupFixtureBusy = $false
    $global:StartupFixtureLateBusy = $false
    $global:StartupFixtureFailRun = $false
    $global:StartupFixturePortChecks = 0
    $global:StartupFixtureCommands = @()
    $global:StartupFixtureBuilt = $false
    $global:StartupFixtureInitialized = $false
}
try {
    [IO.Directory]::CreateDirectory((Join-Path $fixtureRoot 'scripts')) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts/Start-Development.ps1') -Destination (Join-Path $fixtureRoot 'scripts/Start-Development.ps1')
    'param([switch]$Install); $global:StartupFixtureBuilt = $true' | Set-Content -LiteralPath (Join-Path $fixtureRoot 'scripts/Build-Frontend.ps1')
    'param([switch]$StartContainers,[switch]$UseMailpit,[switch]$RotateSecurityKeys); $global:StartupFixtureInitialized = $true; $SpwposDotnet = ''Invoke-StartupFixtureDotnet''' | Set-Content -LiteralPath (Join-Path $fixtureRoot 'scripts/Initialize-Development.ps1')
    $script = Join-Path $fixtureRoot 'scripts/Start-Development.ps1'
    foreach ($runtime in @('src/SPWPOS.Api/bin/Debug/net8.0', 'artifacts/api-runtime/previous')) {
        Reset-Fixture
        $global:StartupFixtureApiPath = Join-Path $fixtureRoot ($runtime + '/SPWPOS.Api.exe')
        & $script -SkipCertificateTrust
        Assert-Condition (-not $global:StartupFixtureBuilt -and -not $global:StartupFixtureInitialized) 'Running API was not detected before setup.'
    }
    Reset-Fixture
    $global:StartupFixtureBusy = $true
    $rejected = $false
    try { & $script -SkipCertificateTrust } catch { $rejected = $_.Exception.Message -like '*7043*23456*' }
    Assert-Condition ($rejected -and -not $global:StartupFixtureBuilt -and -not $global:StartupFixtureInitialized) 'Occupied port did not stop setup with its owning PID.'
    Reset-Fixture
    $global:StartupFixtureLateBusy = $true
    $rejected = $false
    try { & $script -SkipCertificateTrust } catch { $rejected = $_.Exception.Message -like '*7043*23456*' }
    Assert-Condition ($rejected -and 'run' -notin $global:StartupFixtureCommands) 'Port was not checked again before launch.'
    Reset-Fixture
    $fixtureLease = [IO.File]::Open((Join-Path $fixtureRoot 'artifacts/.development-startup.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    $rejected = $false
    try {
        try { & $script -SkipCertificateTrust } catch { $rejected = $_.Exception.Message -like '*startup is in progress*' }
    } finally { $fixtureLease.Dispose() }
    Assert-Condition ($rejected -and -not $global:StartupFixtureBuilt) 'Concurrent startup reached frontend build.'
    Reset-Fixture
    & $script -SkipCertificateTrust
    Assert-Condition ('run' -in $global:StartupFixtureCommands) 'Available server did not launch.'
    Reset-Fixture
    $global:StartupFixtureFailRun = $true
    $rejected = $false
    try { & $script -SkipCertificateTrust } catch { $rejected = $_.Exception.Message -eq 'API stopped with an error.' }
    Assert-Condition $rejected 'API failure was hidden.'
    # A new run proves the failed process released its lease.
    Reset-Fixture
    & $script -SkipCertificateTrust
    Write-Output 'Development startup: 8 regression cases passed; no real API, containers, or configuration changed.'
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
