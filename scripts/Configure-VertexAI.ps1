param(
    [ValidatePattern('^[a-z][a-z0-9-]{4,28}[a-z0-9]$')]
    [string]$ProjectId = 'planora-511614',
    [ValidatePattern('^(global|[a-z]+(-[a-z]+)+[0-9])$')]
    [string]$Location = 'global',
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]{0,99}$')]
    [string]$Model = 'gemini-3.5-flash-lite',
    [switch]$UseWindow,
    [switch]$UseAdc,
    [switch]$ExpressMode,
    [switch]$SkipBuild,
    [switch]$NoRestart
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskCloudRoot = Split-Path -Parent $PSScriptRoot
$taskCloudApi = Join-Path $taskCloudRoot 'src\SPWPOS.Api'
$taskCloudStatus = Join-Path $taskCloudRoot 'artifacts\cloud-ai-setup.status.json'
[IO.Directory]::CreateDirectory((Split-Path -Parent $taskCloudStatus)) | Out-Null
function Set-CloudSetupStatus([string]$State) {
    @{ state = $State; project = $ProjectId; model = $Model; time = [DateTimeOffset]::UtcNow.ToString('O') } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath $taskCloudStatus -Encoding UTF8
}
if ($ExpressMode -and $UseAdc) { throw 'Express mode requires its Cloud API key; use standard mode for ADC.' }
if ($ExpressMode -and $Location -ne 'global') { throw 'Express mode uses the global endpoint.' }
if (-not $UseAdc -and $Location -ne 'global') { throw 'Cloud API-key authentication uses the global publisher endpoint.' }
if (-not $UseAdc) { $ExpressMode = $true; $Location = 'global' }
$taskCloudSecureKey = $null
$taskCloudPointer = [IntPtr]::Zero
$taskCloudKey = $null
$taskCloudStage = 'credential_input'
$taskCloudPreviousEnvironment = @{}
try {
    Set-CloudSetupStatus 'awaiting_credentials'
    if (-not $UseAdc) {
        if ($UseWindow) {
            Add-Type -AssemblyName System.Windows.Forms
            Add-Type -AssemblyName System.Drawing
            $taskCloudForm = New-Object Windows.Forms.Form
            $taskCloudForm.Text = 'PLANORA - Kết nối Google Cloud'
            $taskCloudForm.ClientSize = New-Object Drawing.Size(550, 235)
            $taskCloudForm.StartPosition = 'CenterScreen'
            $taskCloudForm.FormBorderStyle = 'FixedDialog'
            $taskCloudForm.MaximizeBox = $false
            $taskCloudForm.MinimizeBox = $false
            $taskCloudForm.TopMost = $true
            $taskCloudLabel = New-Object Windows.Forms.Label
            $taskCloudLabel.Text = "Project: $ProjectId`r`nDán API key Google Cloud vào ô bên dưới.`r`nKey chỉ lưu trên máy, không gửi vào chat."
            $taskCloudLabel.SetBounds(20, 20, 510, 65)
            $taskCloudBox = New-Object Windows.Forms.TextBox
            $taskCloudBox.UseSystemPasswordChar = $true
            $taskCloudBox.SetBounds(20, 92, 510, 30)
            $taskCloudNote = New-Object Windows.Forms.Label
            $taskCloudNote.Text = 'Sẽ thử gọi AI trước khi lưu và khởi động lại PLANORA.'
            $taskCloudNote.SetBounds(20, 135, 510, 25)
            $taskCloudApply = New-Object Windows.Forms.Button
            $taskCloudApply.Text = 'Kết nối'
            $taskCloudApply.DialogResult = [Windows.Forms.DialogResult]::OK
            $taskCloudApply.SetBounds(325, 180, 95, 32)
            $taskCloudCancel = New-Object Windows.Forms.Button
            $taskCloudCancel.Text = 'Hủy'
            $taskCloudCancel.DialogResult = [Windows.Forms.DialogResult]::Cancel
            $taskCloudCancel.SetBounds(435, 180, 95, 32)
            $taskCloudForm.Controls.AddRange(@($taskCloudLabel, $taskCloudBox, $taskCloudNote, $taskCloudApply, $taskCloudCancel))
            $taskCloudForm.AcceptButton = $taskCloudApply
            $taskCloudForm.CancelButton = $taskCloudCancel
            try {
                if ($taskCloudForm.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) {
                    Set-CloudSetupStatus 'cancelled'; return
                }
                $taskCloudSecureKey = ConvertTo-SecureString -String $taskCloudBox.Text -AsPlainText -Force
                $taskCloudBox.Clear()
            } finally { $taskCloudForm.Dispose() }
        } else {
            $taskCloudSecureKey = Read-Host 'Nhap API key Google Cloud (an ky tu, khong phai key AI Studio)' -AsSecureString
        }
        $taskCloudPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskCloudSecureKey)
        $taskCloudKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskCloudPointer).Trim()
        if ([string]::IsNullOrWhiteSpace($taskCloudKey) -or $taskCloudKey -match '\s') { throw 'A Cloud API key is required.' }
    }
    $taskCloudStage = 'environment_configuration'
    $taskCloudSettings = @{
        'AiSettings:Enabled' = 'true'
        'AiSettings:Provider' = 'VertexAI'
        'AiSettings:Model' = $Model
        'AiSettings:VertexProjectId' = $ProjectId
        'AiSettings:VertexLocation' = $Location
        'AiSettings:VertexAuthentication' = $(if ($UseAdc) { 'ADC' } else { 'ApiKey' })
        'AiSettings:VertexExpressMode' = $ExpressMode.ToString().ToLowerInvariant()
    }
    if (-not $UseAdc) { $taskCloudSettings['AiSettings:VertexApiKey'] = $taskCloudKey }
    foreach ($taskCloudSetting in $taskCloudSettings.GetEnumerator()) {
        $taskCloudEnvName = $taskCloudSetting.Key.Replace(':', '__')
        $taskCloudPreviousEnvironment[$taskCloudEnvName] = [Environment]::GetEnvironmentVariable($taskCloudEnvName, 'Process')
        [Environment]::SetEnvironmentVariable($taskCloudEnvName, $taskCloudSetting.Value, 'Process')
    }
    Set-CloudSetupStatus 'validating'
    $taskCloudStage = 'live_ai_check'
    # Key reaches the child process through its environment, never command-line arguments.
    # The HTTP adapter uses query + header; its default HTTP logging is disabled.
    & (Join-Path $PSScriptRoot 'Test-LiveAi.ps1') -SkipBuild:$SkipBuild
    if ($LASTEXITCODE -ne 0) { throw 'Google Cloud verification failed.' }
    # Save only after the real Cloud request and planner checks succeed.
    $taskCloudStage = 'saving_configuration'
    $taskCloudPayload = $taskCloudSettings | ConvertTo-Json -Compress
    $taskCloudPayload | & $SpwposDotnet user-secrets set --project $taskCloudApi
    if ($LASTEXITCODE -ne 0) { throw 'Could not save Cloud configuration.' }
    Set-CloudSetupStatus 'saved'
    foreach ($taskCloudEnvName in $taskCloudPreviousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskCloudEnvName, $taskCloudPreviousEnvironment[$taskCloudEnvName], 'Process')
    }
    if (-not $NoRestart) {
        $taskCloudStage = 'restarting_api'
        & (Join-Path $PSScriptRoot 'Restart-PlanoraApi.ps1')
        Set-CloudSetupStatus 'activated'
    }
    Write-Host "Google Cloud configured for $ProjectId using $Model."
    if ($UseWindow) {
        [Windows.Forms.MessageBox]::Show('Đã kiểm tra AI và lưu cấu hình Google Cloud thành công.', 'PLANORA') | Out-Null
    }
} catch {
    $taskCloudFailureCode = 'SETUP_FAILED'
    $taskCloudProviderReason = $null
    $taskCloudHttpStatus = $null
    if ($taskCloudStage -eq 'live_ai_check' -and (Test-Path (Join-Path $taskCloudRoot 'artifacts\cloud-ai-check.result.json'))) {
        $taskCloudCheckResult = Get-Content -LiteralPath (Join-Path $taskCloudRoot 'artifacts\cloud-ai-check.result.json') -Raw | ConvertFrom-Json
        if ($taskCloudCheckResult.code -match '^[A-Z][A-Z0-9_]{0,79}$') { $taskCloudFailureCode = $taskCloudCheckResult.code }
        $taskCloudProviderReason = $taskCloudCheckResult.providerReason
        $taskCloudHttpStatus = $taskCloudCheckResult.httpStatus
    }
    @{ state = 'failed'; stage = $taskCloudStage; code = $taskCloudFailureCode;
        exceptionType = $_.Exception.GetType().Name; scriptLine = $_.InvocationInfo.ScriptLineNumber;
        providerReason = $taskCloudProviderReason; httpStatus = $taskCloudHttpStatus;
        project = $ProjectId; model = $Model; time = [DateTimeOffset]::UtcNow.ToString('O') } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath $taskCloudStatus -Encoding UTF8
    # Do not print raw exceptions from native processes or credential input.
    Write-Warning "Cloud setup stopped at $taskCloudStage ($taskCloudFailureCode). No raw credential details are logged."
    $taskCloudFailureHint = switch ($taskCloudFailureCode) {
        'AGENT_AI_AUTH_FAILED' { 'Google Cloud từ chối xác thực hoặc quyền truy cập. Kiểm tra key gắn đúng service account và account có quyền Agent Platform User trong project PLANORA.' }
        'AGENT_AI_QUOTA_EXCEEDED' { 'Google Cloud báo hết hạn mức hoặc thiếu năng lực phục vụ. Kiểm tra quota và billing của project.' }
        'AGENT_AI_MODEL_UNAVAILABLE' { 'Model đang cấu hình chưa khả dụng cho project. Cần kiểm tra model và khu vực kết nối.' }
        'AGENT_AI_REQUEST_INVALID' { 'Google Cloud từ chối định dạng yêu cầu. Cần kiểm tra phần tích hợp API.' }
        'AGENT_AI_TIMEOUT' { 'Google Cloud phản hồi quá lâu. Thử lại sau.' }
        default { 'Chưa hoàn tất cấu hình. Xem mã lỗi và giai đoạn trong artifacts/cloud-ai-setup.status.json.' }
    }
    $taskCloudFailureHint = switch ($taskCloudProviderReason) {
        'SERVICE_DISABLED' { 'Agent Platform API chưa được bật cho project của key. Bật API trên đúng project PLANORA.' }
        'BILLING_DISABLED' { 'Billing của project đang bị tắt. Kiểm tra liên kết billing account có credit.' }
        'API_KEY_INVALID' { 'Google báo API key không hợp lệ. Dùng chuỗi key được sao chép từ cửa sổ tạo key, không dùng tên hoặc ID của key.' }
        'API_KEY_NOT_FOUND' { 'Google không tìm thấy API key này. Kiểm tra key còn tồn tại và được bật.' }
        'API_KEY_SERVICE_BLOCKED' { 'Key chưa được cho phép gọi Agent Platform API. Kiểm tra API restrictions của chính key.' }
        'API_KEY_IP_ADDRESS_BLOCKED' { 'Key đang giới hạn IP và không cho phép địa chỉ của máy này.' }
        'API_KEY_HTTP_REFERRER_BLOCKED' { 'Key đang giới hạn website; backend PLANORA không gửi website referrer.' }
        'IAM_PERMISSION_DENIED' { 'Tài khoản dịch vụ gắn với key thiếu quyền trên project PLANORA. Cấp Agent Platform User cho tài khoản dịch vụ trong IAM của project.' }
        'ACCOUNT_STATE_INVALID' { 'Tài khoản dịch vụ gắn với key đang bị vô hiệu hóa hoặc đã xóa.' }
        'UNAUTHENTICATED' { 'Google chưa xác thực được API key. Sao chép lại chuỗi key từ Show key của key gắn với planora-ai. Nếu vẫn lỗi, cần kiểm tra trạng thái key và tài khoản dịch vụ; mã này chưa chứng minh thiếu quyền IAM.' }
        default { $taskCloudFailureHint }
    }
    if ($UseWindow) {
        [Windows.Forms.MessageBox]::Show("$taskCloudFailureHint`r`n`r`nMã lỗi: $taskCloudFailureCode`r`nHTTP: $taskCloudHttpStatus`r`nGoogle reason: $taskCloudProviderReason`r`nGiai đoạn: $taskCloudStage", 'PLANORA') | Out-Null
    }
    exit 1
} finally {
    foreach ($taskCloudEnvName in $taskCloudPreviousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskCloudEnvName, $taskCloudPreviousEnvironment[$taskCloudEnvName], 'Process')
    }
    if ($taskCloudPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskCloudPointer) }
    if ($taskCloudSecureKey) { $taskCloudSecureKey.Dispose() }
    $taskCloudKey = $null; $taskCloudPayload = $null; $taskCloudSettings = $null
}
