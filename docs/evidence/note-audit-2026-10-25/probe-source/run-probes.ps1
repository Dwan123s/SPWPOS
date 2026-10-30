$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\..\scripts\Initialize-Dotnet.ps1')
$taskSecrets=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft\UserSecrets\SPWPOS-Authentication-Development\secrets.json') -Raw | ConvertFrom-Json
$taskConnection=$taskSecrets.'ConnectionStrings:DefaultConnection'
if (-not $taskConnection) { $taskConnection=$taskSecrets.ConnectionStrings.DefaultConnection }
$taskBuilder=New-Object System.Data.Common.DbConnectionStringBuilder
$taskBuilder.set_ConnectionString($taskConnection)
$taskServer=[string]$taskBuilder['Server']
if (-not $taskServer) { $taskServer=[string]$taskBuilder['Data Source'] }
$taskHost=(($taskServer -replace '^tcp:', '') -split '[,\\]')[0]
if ($taskHost -notin @('localhost','127.0.0.1','.','(local)','(localdb)',$env:COMPUTERNAME)) { throw 'Only local test SQL Server is allowed.' }
$taskOld=$env:SPWPOS_TEST_SQLSERVER
try {
 $env:SPWPOS_TEST_SQLSERVER=$taskConnection
 & $SpwposDotnet restore "$PSScriptRoot\NoteAudit.Probes.csproj" --ignore-failed-sources -p:NuGetAudit=false -m:1 *> "$PSScriptRoot\probes-restore.log"
 if ($LASTEXITCODE -ne 0) { Get-Content "$PSScriptRoot\probes-restore.log" -Tail 10; exit $LASTEXITCODE }
 & $SpwposDotnet build "$PSScriptRoot\NoteAudit.Probes.csproj" -c Release --no-restore -m:1 -nodeReuse:false -p:SkipFrontendBuild=true -p:UseSharedCompilation=false "-p:OutputPath=$PSScriptRoot\build-probes\" *> "$PSScriptRoot\probes-build.log"
 if ($LASTEXITCODE -ne 0) { Get-Content "$PSScriptRoot\probes-build.log" -Tail 15; exit $LASTEXITCODE }
 & $SpwposDotnet "$PSScriptRoot\build-probes\NoteAudit.Probes.dll" "$PSScriptRoot\backend-probes.json" --sql *> "$PSScriptRoot\backend-probes.log"
 $taskExit=$LASTEXITCODE
 Get-Content "$PSScriptRoot\backend-probes.log" -Tail 18
 exit $taskExit
} finally { $env:SPWPOS_TEST_SQLSERVER=$taskOld }
