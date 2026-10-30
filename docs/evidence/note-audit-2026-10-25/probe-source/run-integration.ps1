$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\..\scripts\Initialize-Dotnet.ps1')
$taskSecrets=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft\UserSecrets\SPWPOS-Authentication-Development\secrets.json') -Raw | ConvertFrom-Json
$taskConnection=$taskSecrets.'ConnectionStrings:DefaultConnection'
if (-not $taskConnection) { $taskConnection=$taskSecrets.ConnectionStrings.DefaultConnection }
if (-not $taskConnection) { throw 'Local connection not configured.' }
$taskBuilder=New-Object System.Data.Common.DbConnectionStringBuilder
$taskBuilder.set_ConnectionString($taskConnection)
$taskServer=[string]$taskBuilder['Server']
if (-not $taskServer) { $taskServer=[string]$taskBuilder['Data Source'] }
$taskHost=(($taskServer -replace '^tcp:', '') -split '[,\\]')[0]
if ($taskHost -notin @('localhost','127.0.0.1','.','(local)','(localdb)',$env:COMPUTERNAME)) { throw 'Only local test SQL Server is allowed.' }
$taskPreviousTestConnection=$env:SPWPOS_TEST_SQLSERVER
try {
  # ApiFactory always replaces InitialCatalog with SPWPOS_Tests_<random GUID>.
  # Its cleanup refuses every other database name.
  $env:SPWPOS_TEST_SQLSERVER=$taskConnection
  & $SpwposDotnet test tests/SPWPOS.IntegrationTests -c Release --no-restore -m:1 -nodeReuse:false -p:SkipFrontendBuild=true -p:UseSharedCompilation=false "-p:OutputPath=$PSScriptRoot\build-integration\" --filter 'FullyQualifiedName~NotesApiTests|FullyQualifiedName~NoteImpactTests|FullyQualifiedName~TaskVersionTests' --logger 'trx;LogFileName=note-integration.trx' --results-directory "$PSScriptRoot\integration" *> "$PSScriptRoot\integration.log"
  $taskExit=$LASTEXITCODE
  Get-Content "$PSScriptRoot\integration.log" -Tail 20
  exit $taskExit
} finally { $env:SPWPOS_TEST_SQLSERVER=$taskPreviousTestConnection }
