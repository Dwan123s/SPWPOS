$ErrorActionPreference = 'Stop'
$restartTestRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$restartFixture = Join-Path $restartTestRoot ('artifacts/restart-script-tests/' + [Guid]::NewGuid().ToString('N'))
foreach ($restartFolder in @('scripts', 'artifacts', 'src/SPWPOS.Api/bin/Release/net8.0')) {
    [IO.Directory]::CreateDirectory((Join-Path $restartFixture $restartFolder)) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $restartTestRoot 'scripts/Restart-PlanoraApi.ps1') -Destination (Join-Path $restartFixture 'scripts/Restart-PlanoraApi.ps1')
'' | Set-Content -LiteralPath (Join-Path $restartFixture 'src/SPWPOS.Api/bin/Release/net8.0/SPWPOS.Api.exe')
'$SpwposDotnet = Join-Path $PSScriptRoot ''fake-dotnet.cmd''' | Set-Content -LiteralPath (Join-Path $restartFixture 'scripts/Initialize-Dotnet.ps1')
@'
@echo off
if "%RESTART_FAKE_FAIL_SCHEMA%"=="true" exit /b 1
echo Database schema verified by offline fixture.
exit /b 0
'@ | Set-Content -LiteralPath (Join-Path $restartFixture 'scripts/fake-dotnet.cmd') -Encoding ASCII
@'
$ErrorActionPreference = 'Stop'
$fixtureRoot = $PSScriptRoot
$global:RestartFixtureStarted = $null
$global:RestartFixtureStopped = $false
function Get-CimInstance {
    param($Filter)
    [pscustomobject]@{ ProcessId=12345; ExecutablePath=(Join-Path $fixtureRoot 'src/SPWPOS.Api/bin/Release/net8.0/SPWPOS.Api.exe') }
}
function Stop-Process { param($Id, [switch]$Force) $global:RestartFixtureStopped = $true }
function Wait-Process { param($Id, $Timeout, $ErrorAction) }
function Get-NetTCPConnection { param($State, $LocalPort, $ErrorAction) [pscustomobject]@{ OwningProcess=54321 } }
function Start-Process {
    param($FilePath, $ArgumentList, $WorkingDirectory, $WindowStyle, [switch]$PassThru, $RedirectStandardOutput, $RedirectStandardError)
    $global:RestartFixtureStarted = @{ Arguments=$ArgumentList; Directory=$WorkingDirectory; Window=$WindowStyle; Dotnet=$env:DOTNET_ENVIRONMENT; Aspnet=$env:ASPNETCORE_ENVIRONMENT }
    'PLANORA AI: ready; provider=VertexAI; model=gemini-3.8-flash.' | Set-Content -LiteralPath $RedirectStandardOutput
    $process = [pscustomobject]@{ Id=54321; HasExited=$false }
    $process | Add-Member ScriptMethod Refresh { }
    return $process
}
function New-Object {
    param($TypeName)
    if ($TypeName -ne 'Net.Sockets.TcpClient') { throw 'Unexpected fixture object.' }
    $socket = [pscustomobject]@{ Connected=$true }
    $socket | Add-Member ScriptMethod ConnectAsync { param($Address,$Port) return [System.Threading.Tasks.Task]::FromResult($true) }
    $socket | Add-Member ScriptMethod Dispose { }
    return $socket
}
$env:RESTART_FAKE_FAIL_SCHEMA = 'true'
$failed = $false
try { & (Join-Path $fixtureRoot 'scripts/Restart-PlanoraApi.ps1') } catch { $failed = $true }
if (-not $failed -or $global:RestartFixtureStopped -or $global:RestartFixtureStarted) { throw 'Failed schema validation must leave the existing API running.' }
$env:RESTART_FAKE_FAIL_SCHEMA = 'false'
$env:DOTNET_ENVIRONMENT = 'Production'
$output = @(& (Join-Path $fixtureRoot 'scripts/Restart-PlanoraApi.ps1') 6>&1)
if ($global:RestartFixtureStarted.Dotnet -ne 'Development' -or $global:RestartFixtureStarted.Aspnet -ne 'Development') { throw 'Restart did not override the inherited environment for development secrets.' }
$rootIndex = [Array]::IndexOf($global:RestartFixtureStarted.Arguments, '--contentRoot')
if ($rootIndex -lt 0 -or $global:RestartFixtureStarted.Arguments[$rootIndex+1].Trim('"') -ne (Join-Path $fixtureRoot 'src/SPWPOS.Api')) { throw 'Restart used a different content root.' }
if ($global:RestartFixtureStarted.Window -ne 'Hidden' -or -not $global:RestartFixtureStopped) { throw 'Restart did not use the verified workspace process and hidden window.' }
if ("$output" -notmatch 'PLANORA AI: ready; provider=VertexAI; model=gemini-3.8-flash') { throw 'Restart omitted actual AI startup status.' }
Write-Output 'Restart regression checks passed: protected preflight, development configuration, content root and actual AI status. No server was stopped.'
'@ | Set-Content -LiteralPath (Join-Path $restartFixture 'verify.ps1') -Encoding UTF8
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $restartFixture 'verify.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Restart script regression tests failed.' }
