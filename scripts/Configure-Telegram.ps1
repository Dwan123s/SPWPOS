param([switch]$NoRestart)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskTelegramRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskTelegramProject = Join-Path $taskTelegramRoot 'src\SPWPOS.Api'
Write-Host 'Copy only the bot token from BotFather. Paste it at the next prompt, then press Enter.'
$taskTelegramSecure = Read-Host 'Telegram bot token (hidden)' -AsSecureString
$taskTelegramPlain = $null
$taskTelegramJson = $null
try {
    $taskTelegramPlain = ([System.Net.NetworkCredential]::new('', $taskTelegramSecure)).Password.Trim()
    if ($taskTelegramPlain -notmatch '^\d+:[A-Za-z0-9_-]{20,}$') {
        throw 'Token is empty or has an invalid format. No configuration was changed. Run this script again and paste only the token from BotFather.'
    }
    $taskTelegramJson = @{
        'Telegram:Enabled' = 'true'
        'Telegram:BotToken' = $taskTelegramPlain
        'Telegram:PollSeconds' = '3'
    } | ConvertTo-Json -Compress
    # JSON through stdin keeps the token out of command arguments and shell history.
    $taskTelegramJson | & $SpwposDotnet user-secrets set --project $taskTelegramProject
    if ($LASTEXITCODE -ne 0) { throw 'Could not save Telegram configuration. PLANORA was not restarted.' }
    Write-Host 'Telegram configuration saved. The token was not displayed.'
} finally {
    $taskTelegramPlain = $null
    $taskTelegramJson = $null
    if ($taskTelegramSecure) { $taskTelegramSecure.Dispose() }
    Remove-Variable taskTelegramPlain, taskTelegramJson, taskTelegramSecure -ErrorAction SilentlyContinue
}
if (-not $NoRestart) {
    & (Join-Path $PSScriptRoot 'Restart-PlanoraApi.ps1') -Configuration Debug
    if (-not $?) { throw 'Telegram was saved, but PLANORA could not restart. Review the local API log.' }
}
Write-Host 'Next: open PLANORA > Settings > Notifications > Connect Telegram, then press Start in the bot.'
