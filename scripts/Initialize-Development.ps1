param([switch]$StartContainers, [switch]$UseMailpit, [switch]$RotateSecurityKeys)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $taskRoot 'src\SPWPOS.Api\SPWPOS.Api.csproj'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskDotnet = $SpwposDotnet
function New-Secret {
    # Instance GetBytes works in Windows PowerShell 5.1 and PowerShell 7.
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes); return [Convert]::ToBase64String($bytes) }
    finally { $rng.Dispose() }
}
if ($StartContainers) {
    $dockerCommand = Get-Command docker -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dockerCommand) { throw 'Docker CLI not found. Install/open Docker Desktop before starting containers.' }
    $dockerCheck = New-Object System.Diagnostics.ProcessStartInfo
    $dockerCheck.FileName = $dockerCommand.Source
    $dockerCheck.Arguments = 'info --format {{.ServerVersion}}'
    $dockerCheck.UseShellExecute = $false
    $dockerCheck.CreateNoWindow = $true
    $dockerCheck.RedirectStandardOutput = $true
    $dockerCheck.RedirectStandardError = $true
    $checkProcess = [System.Diagnostics.Process]::Start($dockerCheck)
    try {
        if (-not $checkProcess.WaitForExit(20000)) {
            $checkProcess.Kill()
            throw 'Docker engine did not respond within 20 seconds. Open Docker Desktop, wait for engine Running, then retry. Setup stopped.'
        }
        if ($checkProcess.ExitCode -ne 0) {
            throw 'Docker engine is unavailable. Open Docker Desktop, wait for engine Running, then retry. Setup stopped.'
        }
    } finally { $checkProcess.Dispose() }
}
# Keep the SQL container credential stable after the first initialization.
$secretText = & $taskDotnet user-secrets list --project $apiProject
if ($LASTEXITCODE -ne 0) { throw 'Could not read development User Secrets. Setup stopped.' }
$savedConnection = $secretText | Where-Object { $_ -and $_.StartsWith('ConnectionStrings:DefaultConnection = ') } | Select-Object -First 1
if ($savedConnection) {
        $parsedConnection = New-Object System.Data.Common.DbConnectionStringBuilder
        # PowerShell's dictionary adapter treats property assignment/indexing as
        # dictionary access. Call the CLR accessors explicitly in both PS 5.1/7.
        $parsedConnection.set_ConnectionString($savedConnection.Substring('ConnectionStrings:DefaultConnection = '.Length))
        $savedSqlPassword = $parsedConnection.get_Item('Password')
        if (-not [string]::IsNullOrWhiteSpace($savedSqlPassword)) {
            if ($env:SPWPOS_SQL_PASSWORD -and $env:SPWPOS_SQL_PASSWORD -cne $savedSqlPassword) {
                Write-Warning 'Ignoring a stale SPWPOS_SQL_PASSWORD override; using the existing database credential from User Secrets.'
            }
            $env:SPWPOS_SQL_PASSWORD = $savedSqlPassword
        }
}
if (-not $env:SPWPOS_SQL_PASSWORD) { $env:SPWPOS_SQL_PASSWORD = (New-Secret) + 'aA9!' }
# Keep signing/HMAC keys stable across ordinary development restarts.
$securityKeys = @{}
foreach ($keyName in @('JwtSettings:SigningKey', 'OtpSettings:SecretKey')) {
    $keyPrefix = $keyName + ' = '
    $savedKeyLine = $secretText | Where-Object { $_ -and $_.StartsWith($keyPrefix) } | Select-Object -First 1
    $savedKey = if ($savedKeyLine) { $savedKeyLine.Substring($keyPrefix.Length) } else { '' }
    if ($RotateSecurityKeys -or [string]::IsNullOrWhiteSpace($savedKey)) {
        $securityKeys[$keyName] = New-Secret
    } else {
        try { $keyBytes = [Convert]::FromBase64String($savedKey) }
        catch { throw 'A stored security key is invalid. Use -RotateSecurityKeys to replace development signing/OTP keys explicitly.' }
        if ($keyBytes.Length -lt 32) { throw 'A stored security key is too short. Use -RotateSecurityKeys explicitly.' }
        $securityKeys[$keyName] = $savedKey
        $keyBytes = $null
    }
    $savedKey = $null
}
$env:ConnectionStrings__DefaultConnection = "Server=tcp:127.0.0.1,1433;Database=SPWPOS;User Id=sa;Password=$env:SPWPOS_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True"
$developmentSecrets = @{
    'JwtSettings:SigningKey' = $securityKeys['JwtSettings:SigningKey']
    'OtpSettings:SecretKey' = $securityKeys['OtpSettings:SecretKey']
    'ConnectionStrings:DefaultConnection' = $env:ConnectionStrings__DefaultConnection
}
# Keep an explicitly configured SMTP provider across development restarts.
$savedSmtpHost = $secretText | Where-Object { $_ -and $_.StartsWith('EmailSettings:Host = ') } | Select-Object -First 1
$smtpHost = if ($savedSmtpHost) { $savedSmtpHost.Substring('EmailSettings:Host = '.Length) } else { '' }
if ($UseMailpit -or [string]::IsNullOrWhiteSpace($smtpHost)) {
    $smtpHost = 'localhost'
    $developmentSecrets['EmailSettings:Host'] = $smtpHost
    $developmentSecrets['EmailSettings:Port'] = '1025'
    $developmentSecrets['EmailSettings:SenderEmail'] = 'no-reply@spwpos.example'
    $developmentSecrets['EmailSettings:SenderName'] = 'SPWPOS'
    $developmentSecrets['EmailSettings:EnableSsl'] = 'false'
    $developmentSecrets['EmailSettings:Username'] = ''
    $developmentSecrets['EmailSettings:Password'] = ''
}
# JSON via stdin preserves empty values in PowerShell 5.1 and keeps secrets out of process arguments.
$developmentSecrets | ConvertTo-Json -Compress | & $taskDotnet user-secrets set --project $apiProject | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not save development User Secrets. Setup stopped.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
if ($StartContainers) {
    $sqlVolumeName = if ($env:SPWPOS_SQL_VOLUME_NAME) { $env:SPWPOS_SQL_VOLUME_NAME } else { 'spwpos-sql-data' }
    $existingVolume = & docker volume ls -q --filter "name=^$sqlVolumeName$"
    if (-not $existingVolume) {
        & docker volume create $sqlVolumeName | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the development SQL data volume.' }
    }
    & docker compose --project-directory $taskRoot up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Containers did not start. Check Docker Desktop.' }
}
Write-Host 'Development secrets saved; no secret values printed. Existing SMTP configuration is preserved.'
if ($env:EmailSettings__Host) { $smtpHost = $env:EmailSettings__Host }
if ($smtpHost -in @('localhost', '127.0.0.1', '::1')) {
    Write-Host 'SMTP uses local Mailpit. OTP inbox: http://localhost:8025'
} else {
    Write-Host 'SMTP uses your configured external provider. OTP messages go to recipient email inboxes.'
}
Write-Host 'Next: .\scripts\Start-Development.ps1'
