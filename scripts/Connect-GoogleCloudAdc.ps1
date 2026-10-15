param(
    [ValidatePattern('^[a-z][a-z0-9-]{4,28}[a-z0-9]$')]
    [string]$ProjectId = 'planora-511614'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskAdcRoot = Split-Path -Parent $PSScriptRoot
$taskAdcCli = Join-Path $taskAdcRoot 'artifacts\google-cloud-sdk\bin\gcloud.cmd'
$taskAdcStatusFile = Join-Path $taskAdcRoot 'artifacts\cloud-adc-setup.status.json'
$taskAdcLoginLog = Join-Path $taskAdcRoot 'artifacts\cloud-adc-login.log'
$taskAdcToken = $null
$taskAdcStage = 'starting'
function Set-AdcStatus([string]$State) {
    @{ state = $State; project = $ProjectId; stage = $taskAdcStage; time = [DateTimeOffset]::UtcNow.ToString('O') } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath $taskAdcStatusFile -Encoding UTF8
}
function Invoke-AdcCli([string[]]$CliArguments) {
    $taskAdcErrorPreference = $ErrorActionPreference
    try {
        # Windows PowerShell 5.1 turns normal native stderr into ErrorRecords.
        $ErrorActionPreference = 'Continue'
        & $taskAdcCli @CliArguments >> $taskAdcLoginLog 2>&1
        return $LASTEXITCODE
    } finally { $ErrorActionPreference = $taskAdcErrorPreference }
}
try {
    if (-not (Test-Path -LiteralPath $taskAdcCli -PathType Leaf)) { throw 'Google Cloud CLI is not prepared.' }
    $env:CLOUDSDK_CORE_DISABLE_USAGE_REPORTING = 'true'
    $env:CLOUDSDK_CORE_PROJECT = $ProjectId
    $taskAdcStage = 'google_sign_in'
    Set-AdcStatus 'awaiting_google_login'
    # Official Google login opens the user's browser. Never ask for a password or token in chat.
    if ((Invoke-AdcCli @('auth', 'application-default', 'login', "--project=$ProjectId", '--quiet')) -ne 0) {
        throw 'Google login did not complete.'
    }
    $taskAdcStage = 'quota_project'
    Set-AdcStatus 'configuring'
    if ((Invoke-AdcCli @('auth', 'application-default', 'set-quota-project', $ProjectId, '--quiet')) -ne 0) {
        throw 'Cannot set the quota project. Check project access.'
    }
    # Capture the access token in process memory only; never log the command output.
    $taskAdcErrorPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $taskAdcTokenOutput = @(& $taskAdcCli auth application-default print-access-token --quiet 2>$null)
    } finally { $ErrorActionPreference = $taskAdcErrorPreference }
    if ($LASTEXITCODE -ne 0 -or $taskAdcTokenOutput.Count -ne 1) { throw 'Cannot obtain ADC access token.' }
    $taskAdcToken = "$($taskAdcTokenOutput[0])".Trim()
    $taskAdcTokenOutput = $null
    $taskAdcHeaders = @{ Authorization = 'Bearer ' + $taskAdcToken; 'x-goog-user-project' = $ProjectId }
    $taskAdcStage = 'billing_check'
    $taskAdcBilling = $null
    try {
        $taskAdcBilling = Invoke-RestMethod -Uri "https://cloudbilling.googleapis.com/v1/projects/$ProjectId/billingInfo" -Headers $taskAdcHeaders -TimeoutSec 15
    } catch {
        # Billing inspection can require a separate API/role. The real model call
        # remains mandatory; do not grant broader billing permissions to inspect it.
    }
    if ($taskAdcBilling -and -not $taskAdcBilling.billingEnabled) { throw 'Billing is not enabled on the requested project.' }
    $taskAdcStage = 'agent_api'
    $taskAdcServiceUri = "https://serviceusage.googleapis.com/v1/projects/$ProjectId/services/aiplatform.googleapis.com"
    $taskAdcService = Invoke-RestMethod -Uri $taskAdcServiceUri -Headers $taskAdcHeaders -TimeoutSec 30
    if ($taskAdcService.state -ne 'ENABLED') {
        # Enabling this exact API is part of the user's requested PLANORA Cloud setup.
        $taskAdcOperation = Invoke-RestMethod -Method Post -Uri ($taskAdcServiceUri + ':enable') -Headers $taskAdcHeaders -ContentType 'application/json' -Body '{}' -TimeoutSec 30
        if ($taskAdcOperation.error) { throw 'Agent Platform API could not be enabled.' }
        for ($taskAdcAttempt = 0; $taskAdcAttempt -lt 30; $taskAdcAttempt++) {
            $taskAdcService = Invoke-RestMethod -Uri $taskAdcServiceUri -Headers $taskAdcHeaders -TimeoutSec 15
            if ($taskAdcService.state -eq 'ENABLED') { break }
            Start-Sleep -Seconds 2
        }
        if ($taskAdcService.state -ne 'ENABLED') { throw 'Agent Platform API is not ready.' }
    }
    $taskAdcToken = $null; $taskAdcHeaders = $null
    $taskAdcStage = 'live_ai_validation'
    Set-AdcStatus 'validating'
    & (Join-Path $PSScriptRoot 'Configure-VertexAI.ps1') -UseAdc -ProjectId $ProjectId -SkipBuild
    if ($LASTEXITCODE -ne 0) { throw 'Cloud AI validation did not pass.' }
    $taskAdcStage = 'complete'
    Set-AdcStatus 'activated'
    Write-Host 'PLANORA now uses Google Cloud ADC. No API key is required.'
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show('Đã đăng nhập và kiểm tra Google Cloud thành công. PLANORA đã chuyển sang Cloud.', 'PLANORA') | Out-Null
} catch {
    Set-AdcStatus 'failed'
    # Raw OAuth/HTTP exceptions and credential contents must not be shown or logged here.
    Write-Warning "Google Cloud ADC setup stopped at stage: $taskAdcStage. Check sign-in, project access, billing and API status."
    exit 1
} finally {
    $taskAdcToken = $null; $taskAdcTokenOutput = $null; $taskAdcHeaders = $null
}
