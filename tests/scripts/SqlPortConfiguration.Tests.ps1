$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$fixtureRoot = Join-Path $repositoryRoot ('artifacts\sql-port-fixture-' + [guid]::NewGuid().ToString('N'))
$expectedParent = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not ([IO.Path]::GetFullPath($fixtureRoot)).StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Fixture path is outside the intended artifact directory.'
}
$savedEnvironment = @{}
foreach ($key in @('SPWPOS_SQL_PORT', 'SPWPOS_SQL_PASSWORD', 'ConnectionStrings__DefaultConnection', 'ASPNETCORE_ENVIRONMENT')) {
    $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}
$global:SpwposSqlPortFixturePayload = $null
$global:SpwposSqlPortFixturePassword = [guid]::NewGuid().ToString('N') + 'aA9!'
function Invoke-SqlPortFixtureDotnet {
    [CmdletBinding(PositionalBinding = $false)]
    param(
        [Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments,
        [Parameter(ValueFromPipeline = $true)][string]$InputObject
    )
    begin { $stdinText = '' }
    process { $stdinText += $InputObject }
    end {
        $global:LASTEXITCODE = 0
        if ($Arguments[1] -eq 'list') {
            return "ConnectionStrings:DefaultConnection = Server=tcp:127.0.0.1,1433;Database=SPWPOS;User Id=sa;Password=$global:SpwposSqlPortFixturePassword"
        }
        if ($Arguments[1] -eq 'set') {
            $global:SpwposSqlPortFixturePayload = $stdinText | ConvertFrom-Json
            return
        }
        throw 'Unexpected SDK command in fixture.'
    }
}
function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
try {
    $null = New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'scripts') -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Initialize-Development.ps1') -Destination (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1')
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'scripts\Initialize-Dotnet.ps1') -Value '$SpwposDotnet = ''Invoke-SqlPortFixtureDotnet''' -Encoding UTF8
    foreach ($port in @('', '15433', '1433')) {
        $env:SPWPOS_SQL_PORT = $port
        & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') | Out-Null
        $expectedPort = if ($port) { $port } else { '11433' }
        $connection = New-Object System.Data.Common.DbConnectionStringBuilder
        $connection.set_ConnectionString($global:SpwposSqlPortFixturePayload.'ConnectionStrings:DefaultConnection')
        Assert-Condition ($connection.get_Item('Server') -eq "tcp:127.0.0.1,$expectedPort") "Wrong SQL endpoint for port '$port'."
        Assert-Condition ($env:SPWPOS_SQL_PORT -eq $expectedPort) 'Compose port differs from connection string.'
        Assert-Condition ($env:ConnectionStrings__DefaultConnection -eq $global:SpwposSqlPortFixturePayload.'ConnectionStrings:DefaultConnection') 'API environment differs from saved connection string.'
        Assert-Condition ($connection.get_Item('Password') -ceq $global:SpwposSqlPortFixturePassword) 'Port change replaced the existing SQL credential.'
    }
    foreach ($port in @('0', '-1', '65536', 'abc', '11433:1433')) {
        $env:SPWPOS_SQL_PORT = $port
        $global:SpwposSqlPortFixturePayload = $null
        $wasRejected = $false
        try { & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') | Out-Null }
        catch { $wasRejected = $_.Exception.Message -like '*SPWPOS_SQL_PORT*' }
        Assert-Condition ($wasRejected -and $null -eq $global:SpwposSqlPortFixturePayload) "Invalid port '$port' was not rejected before saving secrets."
    }
    $composeText = Get-Content -LiteralPath (Join-Path $repositoryRoot 'compose.yaml') -Raw
    Assert-Condition ($composeText.Contains('127.0.0.1:${SPWPOS_SQL_PORT:-11433}:1433')) 'Compose does not use the same configurable SQL host port.'
    Write-Output 'SQL port script checks: 9 cases passed; no real secrets or containers changed.'
} finally {
    foreach ($key in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key], 'Process') }
    # The resolved absolute path was checked against the artifact directory above.
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
    $global:SpwposSqlPortFixturePayload = $null
    $global:SpwposSqlPortFixturePassword = $null
}
