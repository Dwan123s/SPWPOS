param(
    [ValidateSet('Offline', 'Live')][string]$Mode = 'Offline',
    [ValidateSet('Smoke', 'All')][string]$Suite = 'Smoke',
    [ValidateSet('Legacy', 'Conversations')][string]$Catalog = 'Legacy',
    [string[]]$GroupId = @(),
    [string[]]$CaseId = @(),
    [ValidateRange(1, 5)][int]$Repeats = 1,
    [ValidateRange(1, 500)][int]$MaxCalls = 30,
    [ValidateRange(5, 120)][int]$TimeoutSeconds = 60,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$taskSandboxRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (-not $OutputDirectory) {
    $taskSandboxStamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $OutputDirectory = Join-Path $taskSandboxRoot "artifacts\nora-sandbox\$Mode-$taskSandboxStamp"
} elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $taskSandboxRoot $OutputDirectory }
$taskSandboxOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $taskSandboxOutput.StartsWith($taskSandboxRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose an output directory inside this repository.'
}
if (Test-Path -LiteralPath (Join-Path $taskSandboxOutput 'results.json')) { throw 'Results already exist there. Choose a new output directory.' }
$taskSandboxCases = @($CaseId | ForEach-Object { $_.Split(',') } | ForEach-Object { $_.Trim().ToUpperInvariant() } | Where-Object { $_ })
$taskSandboxGroups = @($GroupId | ForEach-Object { $_.Split(',') } | ForEach-Object { $_.Trim().ToUpperInvariant() } | Where-Object { $_ })
if ($Catalog -eq 'Legacy' -and $taskSandboxGroups.Count -gt 0) { throw 'GroupId requires -Catalog Conversations.' }
[IO.Directory]::CreateDirectory($taskSandboxOutput) | Out-Null
$taskSandboxOptions = Join-Path $taskSandboxOutput 'options.json'
@{ repositoryRoot=$taskSandboxRoot; outputDirectory=$taskSandboxOutput; mode=$Mode; suite=$Suite; caseIds=$taskSandboxCases; catalog=$Catalog; groupIds=$taskSandboxGroups;
    repeats=$Repeats; maxCalls=$MaxCalls; timeoutSeconds=$TimeoutSeconds } | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $taskSandboxOptions -Encoding UTF8
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskSandboxPreviousOptions = $env:SPWPOS_NORA_SANDBOX_OPTIONS
$taskSandboxExitCode = 1
try {
    $env:SPWPOS_NORA_SANDBOX_OPTIONS = $taskSandboxOptions
    $taskSandboxBuild = Join-Path $taskSandboxOutput 'build\net8.0'
    $taskSandboxProject = Join-Path $taskSandboxRoot 'tests\SPWPOS.UnitTests\SPWPOS.UnitTests.csproj'
    Write-Host "NORA sandbox: $Catalog / $Mode / $Suite; interpreter attempt cap $MaxCalls; results: $taskSandboxOutput"
    if ($Catalog -eq 'Conversations' -and $Mode -eq 'Offline') { Write-Host 'Conversation Offline mode validates catalog and isolated database fixtures; it does not interpret prompts or run model checks.' }
    if ($Mode -eq 'Live') { Write-Host 'Live mode uses the saved AI provider and synthetic calendar data; provider quota may be consumed.' }
    # Separate output keeps the currently serving API and ordinary test builds untouched.
    & $SpwposDotnet test $taskSandboxProject -c Release --no-restore -m:1 -p:SkipFrontendBuild=true -p:UseSharedCompilation=false "-p:OutputPath=$taskSandboxBuild" `
        --filter 'FullyQualifiedName=SPWPOS.UnitTests.NoraSandboxRun.Execute_selected_scenarios_and_export_results' --logger 'console;verbosity=normal'
    $taskSandboxExitCode = $LASTEXITCODE
} finally {
    $env:SPWPOS_NORA_SANDBOX_OPTIONS = $taskSandboxPreviousOptions
    $taskSandboxResults = Join-Path $taskSandboxOutput 'results.json'
    if (Test-Path -LiteralPath $taskSandboxResults) {
        $taskSandboxReport = Get-Content -LiteralPath $taskSandboxResults -Encoding UTF8 -Raw | ConvertFrom-Json
        $taskSandboxReport.cases | Group-Object status | ForEach-Object { Write-Host "$($_.Name): $($_.Count)" }
        Write-Host "Readable report: $(Join-Path $taskSandboxOutput 'report.md')"
        Write-Host "Full evidence: $taskSandboxResults"
        Write-Host "CSV: $(Join-Path $taskSandboxOutput 'turns.csv')"
    } else { Write-Warning 'The runner did not produce a report. Inspect the build/test error above.' }
}
if ($taskSandboxExitCode -ne 0) { throw "Sandbox checks did not all pass (exit $taskSandboxExitCode). Exported results are preserved." }
