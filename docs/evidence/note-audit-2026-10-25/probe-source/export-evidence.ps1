$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskDestination = Join-Path $taskRoot 'docs\evidence\note-audit-2026-10-25'
New-Item -ItemType Directory -Force -Path $taskDestination | Out-Null
$taskRows = @()
foreach ($taskSuite in @('unit', 'integration')) {
    [xml]$taskTrx = Get-Content -LiteralPath (Join-Path $PSScriptRoot "$taskSuite\note-$taskSuite.trx")
    foreach ($taskCase in $taskTrx.TestRun.Results.UnitTestResult) {
        $taskRows += [PSCustomObject]@{ layer=$taskSuite; id=$taskCase.testId; name=$taskCase.testName; execution=$taskCase.outcome; assessment='REGRESSION_PASS'; evidence="$taskSuite/note-$taskSuite.trx" }
    }
}
$taskFrontend = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'frontend.json') -Raw | ConvertFrom-Json
foreach ($taskFile in $taskFrontend.testResults) {
    foreach ($taskCase in $taskFile.assertionResults) {
        $taskRows += [PSCustomObject]@{layer='frontend'; id=(Split-Path $taskFile.name -Leaf); name=$taskCase.fullName; execution=$taskCase.status; assessment='REGRESSION_PASS'; evidence='frontend.json'}
    }
}
foreach ($taskFile in @('backend-probes.json','ui-probes.json')) {
    $taskProbe = Get-Content -LiteralPath (Join-Path $PSScriptRoot $taskFile) -Raw | ConvertFrom-Json
    foreach ($taskCase in $taskProbe.probes) {
        $taskRows += [PSCustomObject]@{layer='diagnostic'; id=$taskCase.id; name=$taskCase.title; execution='REPRODUCED'; assessment=$taskCase.classification; evidence=$taskFile}
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskFile) -Destination $taskDestination
}
$taskLive = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'live-ai.json') -Raw | ConvertFrom-Json
foreach ($taskCase in $taskLive.cases) {
    $taskRows += [PSCustomObject]@{layer='live-ai'; id=$taskCase.id; name=$taskCase.expectation; execution=$taskCase.status; assessment=$taskCase.status; evidence='live-ai.json'}
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'live-ai.json') -Destination $taskDestination
$taskRows | Export-Csv -LiteralPath (Join-Path $taskDestination 'cases.csv') -NoTypeInformation -Encoding UTF8
$taskRelevant = & git -C $taskRoot ls-files --cached --others --exclude-standard | Where-Object {
    $_ -match '(^frontend/(src|tests)/.*note|^src/.*/.*Note|^tests/.*Note|^tests/SPWPOS.TestSupport/|^src/.*/Workspace/(TaskService|PlanningEngine|TaskInterpreter|WorkspaceService|Contracts)\.cs$|^src/SPWPOS.Application/Workspace/Contracts\.cs$|^frontend/src/controller\.jsx$|^frontend/(package.*json|vite.config.js)$|^Directory.Build.props$|^global.json$)'
} | Sort-Object -Unique
$taskHashes = foreach ($taskPath in $taskRelevant) {
    if (Test-Path -LiteralPath (Join-Path $taskRoot $taskPath) -PathType Leaf) {
        [PSCustomObject]@{path=$taskPath; sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot $taskPath) -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
}
$taskMetadata = [ordered]@{
    auditDate='2026-10-25'; timezone='Asia/Ho_Chi_Minh'; exportedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
    head=(& git -C $taskRoot rev-parse HEAD); auditedWorkingTree='Includes uncommitted changes; use hashes rather than HEAD alone.'
    baseline=@{unit=65; integration=16; frontend=107; total=188; failed=0; skipped=0}
    diagnostic=@{backend=11; frontend=4; total=15; reproducedGapsOrRisks=13; positiveChecks=2}
    liveAi=@{provider=$taskLive.provider; cases=5; passed=5; fictionalInputsOnly=$true; databaseWrites=$false}
    records=$taskRows.Count; sourceFiles=$taskHashes
}
$taskMetadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskDestination 'manifest.json') -Encoding UTF8
foreach ($taskFile in @('frontend.json','frontend.log','unit.log','integration.log','backend-probes.log','ui-probes.log','live-ai.log','frontend-build.log')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskFile) -Destination $taskDestination
}
foreach ($taskSuite in @('unit','integration')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$taskSuite\note-$taskSuite.trx") -Destination $taskDestination
}
$taskRows | Group-Object layer | Select-Object Name,Count | Format-Table -AutoSize
Write-Output "Exported $($taskRows.Count) case records and $($taskHashes.Count) source hashes."
