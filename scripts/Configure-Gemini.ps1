param(
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]{0,99}$')]
    [string]$Model = 'gemini-3.5-flash-lite'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskGeminiSecret = Read-Host 'Nhap Gemini API key tu Google AI Studio (an ky tu)' -AsSecureString
$taskGeminiPointer = [IntPtr]::Zero
try {
    $taskGeminiPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskGeminiSecret)
    $taskGeminiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskGeminiPointer).Trim()
    if ([string]::IsNullOrWhiteSpace($taskGeminiKey) -or $taskGeminiKey -match '\s') {
        throw 'Gemini API key khong duoc de trong hoac chua khoang trang.'
    }
    $taskGeminiPayload = @{
        'AiSettings:Enabled' = 'true'
        'AiSettings:Provider' = 'Gemini'
        'AiSettings:Model' = $Model
        'AiSettings:GeminiApiKey' = $taskGeminiKey
    } | ConvertTo-Json -Compress
    # Standard input avoids placing the API key in command arguments or shell history.
    $taskGeminiPayload | & $SpwposDotnet user-secrets set --project (Join-Path $PSScriptRoot '../src/SPWPOS.Api')
    if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc cau hinh Gemini.' }
    Write-Host 'Da cau hinh Gemini. Khoi dong lai API, mo Ghi chu & agent. Phan tich note luon su dung AI.'
} finally {
    if ($taskGeminiPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskGeminiPointer) }
    $taskGeminiKey = $null
    $taskGeminiPayload = $null
    $taskGeminiSecret.Dispose()
}
