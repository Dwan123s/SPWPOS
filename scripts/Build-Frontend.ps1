param([switch]$Install, [switch]$Test, [switch]$Watch)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Node.ps1')
$frontendRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'frontend'
Push-Location -LiteralPath $frontendRoot
try {
    if ($Install -or -not (Test-Path -LiteralPath 'node_modules')) {
        & $SpwposNode $SpwposNpm ci --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency restore failed.' }
    }
    if ($Test) {
        & $SpwposNode $SpwposNpm test
        if ($LASTEXITCODE -ne 0) { throw 'React frontend tests failed.' }
    }
    $command = if ($Watch) { 'dev' } else { 'build' }
    & $SpwposNode $SpwposNpm run $command
    if ($LASTEXITCODE -ne 0) { throw 'React frontend build failed.' }
} finally { Pop-Location }
