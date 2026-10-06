param([switch]$RequireSqlServer)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
if ($RequireSqlServer -and -not $env:SPWPOS_TEST_SQLSERVER) { throw 'SPWPOS_TEST_SQLSERVER is required. SQL Server tests must not be skipped before merge.' }
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
dotnet restore --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet build -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
dotnet test -c Release --no-build --logger trx --results-directory TestResults
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
node --experimental-default-type=module --test tests/browser/session-refresh.test.mjs tests/browser/analysis-state.test.mjs tests/browser/calendar-view.test.mjs
if ($LASTEXITCODE -ne 0) { throw 'Browser module regression tests failed. Node.js 20.18+ is required for this check.' }
dotnet ef migrations has-pending-model-changes --project src/SPWPOS.Infrastructure
if ($LASTEXITCODE -ne 0) { throw 'EF model differs from migration snapshot.' }
New-Item -ItemType Directory -Force -Path TestResults | Out-Null
$auditJson = dotnet list package --vulnerable --include-transitive --format json --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Package audit failed.' }
$auditJson | Set-Content -LiteralPath 'TestResults/package-audit.json'
$audit = $auditJson | ConvertFrom-Json
$vulnerable = @($audit.projects | ForEach-Object { $_.frameworks } | ForEach-Object { $_.topLevelPackages; $_.transitivePackages } | Where-Object { $_.vulnerabilities.Count -gt 0 })
if ($vulnerable.Count -gt 0) { throw 'Known vulnerable packages detected. See TestResults/package-audit.json.' }
Write-Host 'Checks passed. See TestResults for test and vulnerability reports.'
