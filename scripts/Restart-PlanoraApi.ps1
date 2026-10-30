param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskApiRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskApiDirectory = Join-Path $taskApiRoot 'src\SPWPOS.Api'
$taskApiExe = Join-Path $taskApiDirectory 'bin\Release\net8.0\SPWPOS.Api.exe'
if (-not (Test-Path -LiteralPath $taskApiExe -PathType Leaf)) { throw 'Build the Release API first.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
Push-Location -LiteralPath $taskApiDirectory
try {
    & $SpwposDotnet (Join-Path $taskApiDirectory 'bin\Release\net8.0\SPWPOS.Api.dll') --check-schema
    if ($LASTEXITCODE -ne 0) { throw 'Schema check did not pass. Review migrations before restart; the existing API was left running.' }
} finally { Pop-Location }
$taskApiOwnedProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'SPWPOS.Api.exe'" | Where-Object {
    $_.ExecutablePath -and $_.ExecutablePath.StartsWith($taskApiDirectory + '\', [StringComparison]::OrdinalIgnoreCase)
})
foreach ($taskApiProcess in $taskApiOwnedProcesses) {
    Stop-Process -Id $taskApiProcess.ProcessId -Force
    Wait-Process -Id $taskApiProcess.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
}
$taskApiLogs = Join-Path $taskApiRoot 'artifacts'
[IO.Directory]::CreateDirectory($taskApiLogs) | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$taskApiArguments = @('--urls', 'https://localhost:7043', '--contentRoot', ('"' + $taskApiDirectory + '"'))
$taskApiNewProcess = Start-Process -FilePath $taskApiExe -ArgumentList $taskApiArguments `
    -WorkingDirectory $taskApiDirectory -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $taskApiLogs 'live-ai-api.stdout.log') `
    -RedirectStandardError (Join-Path $taskApiLogs 'live-ai-api.stderr.log')
$taskApiReady = $false
for ($taskApiAttempt = 0; $taskApiAttempt -lt 30; $taskApiAttempt++) {
    $taskApiNewProcess.Refresh()
    if ($taskApiNewProcess.HasExited) { throw 'PLANORA API exited during startup; inspect the local API log.' }
    $taskApiSocket = New-Object Net.Sockets.TcpClient
    try {
        if ($taskApiSocket.ConnectAsync('localhost', 7043).Wait(300) -and $taskApiSocket.Connected) {
            $taskApiListeners = @(Get-NetTCPConnection -State Listen -LocalPort 7043 -ErrorAction SilentlyContinue)
            if ($taskApiListeners | Where-Object { $_.OwningProcess -eq $taskApiNewProcess.Id }) {
                $taskApiReady = $true; break
            }
        }
    } catch { } finally { $taskApiSocket.Dispose() }
    Start-Sleep -Milliseconds 200
}
if (-not $taskApiReady) { throw 'PLANORA API did not listen on port 7043.' }
Write-Host "PLANORA API started (PID $($taskApiNewProcess.Id)) at https://localhost:7043."
$taskApiAiStatus = $null
for ($taskAiStatusAttempt = 0; $taskAiStatusAttempt -lt 10; $taskAiStatusAttempt++) {
    $taskApiAiStatus = Get-Content -LiteralPath (Join-Path $taskApiLogs 'live-ai-api.stdout.log') -ErrorAction SilentlyContinue |
        Where-Object { $_ -match '^PLANORA AI: (ready|not configured); provider=[A-Za-z]*; model=[A-Za-z0-9._/-]+\.$' } | Select-Object -Last 1
    if ($taskApiAiStatus) { break }
    Start-Sleep -Milliseconds 200
}
if ($taskApiAiStatus) { Write-Host $taskApiAiStatus }
