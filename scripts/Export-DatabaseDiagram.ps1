param(
    [string]$Container = 'spwpos-sqlserver-1',
    [string]$Database = 'SPWPOS',
    [string]$OutputDirectory,
    [string]$NodePath = 'node',
    [string]$RendererModule
)
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRepository 'docs\database\current' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskQuery = Join-Path $PSScriptRoot 'database-diagram\export-schema.sql'
$taskRenderer = Join-Path $PSScriptRoot 'database-diagram\render.mjs'

# The password stays inside the existing container environment. Neither shell
# arguments, the saved snapshot nor command output contain it.
$taskShell = 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd "$@"'
$taskJsonLines = Get-Content -LiteralPath $taskQuery -Raw | docker exec -i $Container sh -c $taskShell sh `
    -S localhost -U sa -C -d $Database -b -r 1 -y 0 -w 65535 -f 65001 -l 15 -t 60
if ($LASTEXITCODE -ne 0) { throw 'Could not read the SQL Server schema. Check Docker access, container name and database.' }
$taskJson = ($taskJsonLines -join '').Trim()
$taskSchema = $taskJson | ConvertFrom-Json
if (-not $taskSchema.tables -or -not $taskSchema.capturedAt) { throw 'The database returned an empty or invalid schema snapshot.' }
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskSnapshot = Join-Path $taskOutput 'schema.json'
[IO.File]::WriteAllText($taskSnapshot, $taskJson, (New-Object Text.UTF8Encoding($false)))
$taskArguments = @($taskRenderer, '--schema', $taskSnapshot, '--output', $taskOutput)
if ($RendererModule) { $taskArguments += @('--renderer', $RendererModule) }
& $NodePath @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Schema was captured, but rendering failed. See scripts/database-diagram/README.md for renderer setup.' }
Write-Host "Generated live database documentation: $taskOutput\index.html"
