param([switch]$Live, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$agentTestRoot = Split-Path -Parent $PSScriptRoot
$agentTestBin = Join-Path $agentTestRoot 'artifacts\nora-agent-build'
Push-Location -LiteralPath $agentTestRoot
$agentPriorLive = $env:PLANORA_LIVE_AGENT_TESTS
$agentPriorRoot = $env:PLANORA_REPOSITORY_ROOT
try {
    if (-not $SkipBuild) {
        & $SpwposDotnet build tests/SPWPOS.UnitTests/SPWPOS.UnitTests.csproj -c Release --no-restore -m:1 -p:SkipFrontendBuild=true "-p:OutDir=$agentTestBin/"
        if ($LASTEXITCODE -ne 0) { throw 'NORA test build failed.' }
    }
    $agentArguments = @('-class', 'SPWPOS.UnitTests.NoraAgentRunnerTests', '-class', 'SPWPOS.UnitTests.NoraAgentRuntimeTests', '-class', 'SPWPOS.UnitTests.GeminiAgentModelTests', '-maxThreads', '2', '-xml', (Join-Path $agentTestRoot 'artifacts\nora-agent-tests.xml'))
    & $SpwposDotnet (Join-Path $agentTestBin 'SPWPOS.UnitTests.dll') @agentArguments
    if ($LASTEXITCODE -ne 0) { throw 'NORA deterministic checks failed.' }
    if ($Live) {
        # Explicit opt-in sends fictional schedule data to the configured model.
        # TestSupport uses an in-memory database, never the account's SQL database.
        $env:PLANORA_LIVE_AGENT_TESTS = '1'; $env:PLANORA_REPOSITORY_ROOT = $agentTestRoot
        & $SpwposDotnet (Join-Path $agentTestBin 'SPWPOS.UnitTests.dll') -class SPWPOS.UnitTests.NoraLiveAgentTests -maxThreads 1 -xml (Join-Path $agentTestRoot 'artifacts\nora-agent-live.xml')
        if ($LASTEXITCODE -ne 0) { throw 'NORA live provider check failed. Inspect the sanitized test report.' }
    }
} finally {
    $env:PLANORA_LIVE_AGENT_TESTS = $agentPriorLive; $env:PLANORA_REPOSITORY_ROOT = $agentPriorRoot
    Pop-Location
}
