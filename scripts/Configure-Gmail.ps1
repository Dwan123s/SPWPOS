param([string]$GmailAddress, [switch]$RestartApi)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $taskRoot 'src\SPWPOS.Api\SPWPOS.Api.csproj'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')

if ([string]::IsNullOrWhiteSpace($GmailAddress)) {
    $GmailAddress = Read-Host 'Gmail / Google Workspace address used to SEND SPWPOS emails'
}
$GmailAddress = $GmailAddress.Trim()
try { $mailAddress = New-Object System.Net.Mail.MailAddress($GmailAddress) }
catch { throw 'Enter a valid sender email address.' }
if ($mailAddress.Address -ne $GmailAddress) { throw 'Enter only the sender email address, without a display name.' }

Write-Host 'Enable Google 2-Step Verification, then create an App Password named SPWPOS:'
Write-Host 'https://myaccount.google.com/apppasswords'
Write-Host 'Enter the 16-character APP password below, not your normal Gmail password.'
$secureAppPassword = Read-Host 'Google App Password (hidden input)' -AsSecureString
$passwordPointer = [IntPtr]::Zero
$plainAppPassword = $null
$smtpSecrets = $null
try {
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureAppPassword)
    $plainAppPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer) -replace '\s', ''
    if ($plainAppPassword -notmatch '^[A-Za-z0-9]{16}$') {
        throw 'The Google App Password must contain 16 characters. No settings were changed.'
    }
    $smtpSecrets = @{
        'EmailSettings:Host' = 'smtp.gmail.com'
        'EmailSettings:Port' = '587'
        'EmailSettings:EnableSsl' = 'true'
        'EmailSettings:Username' = $GmailAddress
        'EmailSettings:Password' = $plainAppPassword
        'EmailSettings:SenderEmail' = $GmailAddress
        'EmailSettings:SenderName' = 'SPWPOS'
    }
    # JSON stdin keeps the password out of process arguments and command history.
    $smtpSecrets | ConvertTo-Json -Compress | & $SpwposDotnet user-secrets set --project $apiProject | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not save Gmail SMTP settings. Restart configuration.' }
    $savedSmtpSecrets = & $SpwposDotnet user-secrets list --project $apiProject
    if ($LASTEXITCODE -ne 0 -or
        $savedSmtpSecrets -cnotcontains 'EmailSettings:Host = smtp.gmail.com' -or
        $savedSmtpSecrets -cnotcontains "EmailSettings:Username = $GmailAddress" -or
        $savedSmtpSecrets -cnotcontains "EmailSettings:SenderEmail = $GmailAddress" -or
        $savedSmtpSecrets -cnotcontains "EmailSettings:Password = $plainAppPassword") {
        throw 'Gmail settings could not be read back from User Secrets. The API was not restarted.'
    }
} finally {
    if ($passwordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer) }
    if ($secureAppPassword) { $secureAppPassword.Dispose() }
    if ($smtpSecrets) { $smtpSecrets.Clear() }
    $plainAppPassword = $null
    $savedSmtpSecrets = $null
}

$smtpOverrides = Get-ChildItem Env: | Where-Object { $_.Name -like 'EmailSettings__*' }
if ($smtpOverrides) {
    Write-Warning 'EmailSettings__* environment variables override User Secrets. Check/remove conflicting overrides before starting the API.'
}
Write-Host 'Gmail SMTP settings saved in development User Secrets. Password was not printed.'
Write-Host 'Restart the API, then request a new OTP to verify delivery. Delivery has not been tested yet.'
Write-Host 'Start: .\scripts\Start-Development.ps1 -SkipCertificateTrust'
if ($RestartApi) {
    # Stop only API apphosts built from this checkout, never other projects.
    $ownedApiPaths = @(
        (Join-Path $taskRoot 'src\SPWPOS.Api\bin\Debug\net10.0\SPWPOS.Api.exe'),
        (Join-Path $taskRoot 'src\SPWPOS.Api\bin\Release\net10.0\SPWPOS.Api.exe')
    )
    Get-CimInstance Win32_Process -Filter "Name = 'SPWPOS.Api.exe'" |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath -in $ownedApiPaths } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
    & (Join-Path $PSScriptRoot 'Start-Development.ps1') -SkipCertificateTrust
}
