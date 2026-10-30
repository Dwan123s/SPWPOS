param(
    [switch]$Follow,
    [string]$RunId,
    [string]$Date = [DateTime]::UtcNow.ToString('yyyy-MM-dd'),
    [ValidateRange(1, 10000)][int]$Tail = 100
)
$ErrorActionPreference = 'Stop'
if ($Date -notmatch '^\d{4}-\d{2}-\d{2}$') { throw 'Date must use yyyy-MM-dd (UTC).' }
if ($RunId) {
    $parsedNoraRun = [Guid]::Empty
    if (-not [Guid]::TryParse($RunId, [ref]$parsedNoraRun)) { throw 'RunId must be a GUID.' }
}
$noraLogRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\SPWPOS.Api\logs\nora-agent'
$noraLogFile = Join-Path $noraLogRoot "nora-agent-$Date.jsonl"
Write-Host "NORA log: $noraLogFile"
if (-not (Test-Path -LiteralPath $noraLogFile -PathType Leaf)) {
    Write-Host 'No log for this UTC date yet. Start/restart the updated API in Development first.'
    return
}
if ($Follow) { Write-Host 'Live view; Ctrl+C to stop. Reopen for a new UTC date.' }
function Show-NoraLogLine([string]$line) {
    try { $entry = $line | ConvertFrom-Json } catch { Write-Warning 'Skipped an incomplete or invalid JSON log line.'; return }
    if ($RunId -and $entry.runId -ne $parsedNoraRun.ToString()) { return }
    $noraTime = ([DateTimeOffset]::Parse($entry.timestamp)).ToLocalTime().ToString('HH:mm:ss.fff')
    $noraId = if ($entry.runId) { ([string]$entry.runId).Substring(0, 8) } else { '-' }
    $noraDetails = $entry.details | ConvertTo-Json -Compress -Depth 5
    Write-Output "$noraTime [$noraId] $($entry.eventName) $noraDetails"
}
if ($Follow) {
    Get-Content -LiteralPath $noraLogFile -Encoding UTF8 -Tail $Tail -Wait | ForEach-Object { Show-NoraLogLine $_ }
} else {
    Get-Content -LiteralPath $noraLogFile -Encoding UTF8 -Tail $Tail | ForEach-Object { Show-NoraLogLine $_ }
}
