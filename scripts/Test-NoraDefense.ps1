param(
    [switch]$Live,
    [ValidateRange(1, 100)][int]$MaxCalls = 12,
    [string[]]$CaseId = @('G31-01','G31-02','G31-06','G31-08','G31-13','G31-20','G31-21','G31-23'),
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$taskDefenseRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (-not $OutputDirectory) {
    $taskDefenseStamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $OutputDirectory = Join-Path $taskDefenseRoot "artifacts\nora-defense\$taskDefenseStamp"
} elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $taskDefenseRoot $OutputDirectory }
$taskDefenseOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $taskDefenseOutput.StartsWith($taskDefenseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose a report directory inside this repository.'
}
if (Test-Path -LiteralPath $taskDefenseOutput) { throw 'Choose a new report directory to preserve earlier evidence.' }
[IO.Directory]::CreateDirectory($taskDefenseOutput) | Out-Null
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskDefenseResults = @()
Push-Location -LiteralPath $taskDefenseRoot
try {
    $taskDefenseProject = Join-Path $taskDefenseRoot 'tests\SPWPOS.UnitTests\SPWPOS.UnitTests.csproj'
    $taskDefenseBuild = Join-Path $taskDefenseOutput 'build'
    & $SpwposDotnet test $taskDefenseProject -c Release --no-restore -m:1 -p:SkipFrontendBuild=true -p:UseSharedCompilation=false "-p:OutputPath=$taskDefenseBuild" `
        --filter 'Category=Defense|FullyQualifiedName~Hardening_' --logger 'trx;LogFileName=defense.trx' --results-directory $taskDefenseOutput
    $taskDefenseResults += @{ layer='deterministic'; exitCode=$LASTEXITCODE; meaning='Independent bounded feasibility, saved-state oracle and catalog contracts.' }
    $taskDefenseFrontend = Join-Path $taskDefenseRoot 'frontend\node_modules\.bin\vitest.cmd'
    if (Test-Path -LiteralPath $taskDefenseFrontend) {
        Push-Location -LiteralPath (Join-Path $taskDefenseRoot 'frontend')
        try {
            & $taskDefenseFrontend run tests/defense-schedule-contract.test.jsx tests/nora-schedule-alternatives.test.jsx --reporter=default --reporter=json "--outputFile.json=$taskDefenseOutput\frontend.json"
            $taskDefenseResults += @{ layer='frontend-contract'; exitCode=$LASTEXITCODE; meaning='Captured backend conflict output through real frontend parser.' }
        } finally { Pop-Location }
    } else { $taskDefenseResults += @{ layer='frontend-contract'; exitCode=1; meaning='NOT_RUN: restore frontend dependencies first.' } }
    try {
        & (Join-Path $PSScriptRoot 'Test-NoraSandbox.ps1') -Catalog Conversations -Mode Offline -GroupId G31 -Suite All -OutputDirectory (Join-Path $taskDefenseOutput 'fixtures')
        $taskDefenseResults += @{ layer='fixtures'; exitCode=0; meaning='Fixture validation only; not AI or scheduling passes.' }
    } catch { $taskDefenseResults += @{ layer='fixtures'; exitCode=1; meaning='Validation/build failed; inspect saved output.' } }
    if ($Live) {
        try {
            & (Join-Path $PSScriptRoot 'Test-NoraSandbox.ps1') -Catalog Conversations -Mode Live -CaseId $CaseId -MaxCalls $MaxCalls -OutputDirectory (Join-Path $taskDefenseOutput 'live')
            $taskDefenseResults += @{ layer='live'; exitCode=0; meaning='Inspect results: REVIEW_REQUIRED and NOT_RUN are not passes.' }
        } catch { $taskDefenseResults += @{ layer='live'; exitCode=1; meaning='Live checks/build failed; inspect retained evidence.' } }
    }
    @{ generatedAtUtc=[DateTimeOffset]::UtcNow.ToString('O'); layers=$taskDefenseResults; liveRequested=[bool]$Live } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskDefenseOutput 'summary.json') -Encoding UTF8
    Write-Host "Defense evidence: $taskDefenseOutput"
} finally { Pop-Location }
if (@($taskDefenseResults | Where-Object { $_.exitCode -ne 0 }).Count -gt 0) {
    throw 'Defense checks found failures or missing prerequisites. Read the reports; no failure was hidden or skipped.'
}
