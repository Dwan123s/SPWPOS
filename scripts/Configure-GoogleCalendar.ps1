param(
    [string]$OAuthClientJson,
    [string]$ClientId,
    [string]$RedirectUri = 'https://localhost:7043/api/integrations/google-calendar/callback',
    [string]$WebhookUrl = '',
    [string]$KeyRingPath,
    [int]$PollSeconds = 60
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $taskRoot 'src\SPWPOS.Api\SPWPOS.Api.csproj'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
if ($PollSeconds -lt 30 -or $PollSeconds -gt 3600) { throw 'PollSeconds must be between 30 and 3600.' }
$redirectAddress = $null
if (-not [Uri]::TryCreate($RedirectUri, [UriKind]::Absolute, [ref]$redirectAddress) -or $redirectAddress.Scheme -ne 'https' -or $redirectAddress.Query) {
    throw 'RedirectUri must be an absolute HTTPS callback address without a query.'
}
if ($WebhookUrl) {
    $webhookAddress = $null
    if (-not [Uri]::TryCreate($WebhookUrl, [UriKind]::Absolute, [ref]$webhookAddress) -or $webhookAddress.Scheme -ne 'https' -or $webhookAddress.IsLoopback) {
        throw 'WebhookUrl must be a public HTTPS URL; leave it empty for localhost polling.'
    }
}
if (-not $KeyRingPath) { $KeyRingPath = Join-Path $env:LOCALAPPDATA 'PLANORA\CalendarKeys' }
$KeyRingPath = [IO.Path]::GetFullPath($KeyRingPath)
$secureSecret = $null; $secretPointer = [IntPtr]::Zero; $plainSecret = $null; $calendarSecrets = $null; $download = $null
try {
    if ($OAuthClientJson) {
        $download = Get-Content -LiteralPath $OAuthClientJson -Raw | ConvertFrom-Json
        if (-not $download.web) { throw 'Download an OAuth client of type Web application, not Desktop app or a service account.' }
        $ClientId = [string]$download.web.client_id
        $plainSecret = [string]$download.web.client_secret
        if ($download.web.redirect_uris -notcontains $RedirectUri) {
            throw 'This callback URL is not registered in the downloaded client. Add it in Google Auth Platform, then download the JSON again.'
        }
    } else {
        if (-not $ClientId) { $ClientId = Read-Host 'Google OAuth Client ID (Web application)' }
        $secureSecret = Read-Host 'Google OAuth Client Secret (hidden input)' -AsSecureString
        $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureSecret)
        $plainSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
    }
    $ClientId = $ClientId.Trim()
    if (-not $ClientId.EndsWith('.apps.googleusercontent.com') -or [string]::IsNullOrWhiteSpace($plainSecret)) {
        throw 'A valid Google OAuth Web Client ID and Client Secret are required.'
    }
    $calendarSecrets = @{
        'GoogleCalendar:Enabled' = 'true'
        'GoogleCalendar:ClientId' = $ClientId
        'GoogleCalendar:ClientSecret' = $plainSecret
        'GoogleCalendar:RedirectUri' = $RedirectUri
        'GoogleCalendar:WebhookUrl' = $WebhookUrl
        'GoogleCalendar:KeyRingPath' = $KeyRingPath
        'GoogleCalendar:PollSeconds' = [string]$PollSeconds
    }
    # JSON stdin keeps the client secret out of process arguments and history.
    $calendarSecrets | ConvertTo-Json -Compress | & $SpwposDotnet user-secrets set --project $apiProject | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not save Google Calendar configuration.' }
} finally {
    if ($secretPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
    if ($secureSecret) { $secureSecret.Dispose() }
    if ($calendarSecrets) { $calendarSecrets.Clear() }
    $plainSecret = $null; $download = $null
}
Write-Host 'Google Calendar configuration saved in development User Secrets. No secret was printed.'
Write-Host ('Callback: ' + $RedirectUri)
if ($WebhookUrl) { Write-Host 'Webhook enabled; ensure this HTTPS endpoint is publicly reachable.' }
else { Write-Host 'Localhost mode: incoming changes are checked periodically; a public webhook is optional.' }
Write-Host 'Apply the Calendar migration, restart the API, then open Settings > Connections > Google Calendar.'
Write-Host 'This script does not connect an account, send calendar data, or restart the running app.'
