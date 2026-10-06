$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$fixtureRoot = Join-Path $repositoryRoot ('artifacts\smtp-fixture-' + [guid]::NewGuid().ToString('N'))
$expectedParent = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not ([IO.Path]::GetFullPath($fixtureRoot)).StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Fixture path is outside the intended artifact directory.'
}
$savedEnvironment = @{}
foreach ($key in @('SPWPOS_SQL_PORT', 'SPWPOS_SQL_PASSWORD', 'ConnectionStrings__DefaultConnection', 'ASPNETCORE_ENVIRONMENT')) {
    $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}
$global:SpwposSmtpFixtureLines = @()
$global:SpwposSmtpFixturePayload = $null
$global:SpwposSmtpFixturePassword = [guid]::NewGuid().ToString('N').Substring(0, 16)
function Invoke-FixtureDotnet {
    [CmdletBinding(PositionalBinding = $false)]
    param(
        [Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments,
        [Parameter(ValueFromPipeline = $true)][string]$InputObject
    )
    begin { $stdinText = '' }
    process { $stdinText += $InputObject }
    end {
        $global:LASTEXITCODE = 0
        if ($Arguments[1] -eq 'list') { return $global:SpwposSmtpFixtureLines }
        if ($Arguments[1] -eq 'set') {
            $global:SpwposSmtpFixturePayload = $stdinText | ConvertFrom-Json
            foreach ($entry in $global:SpwposSmtpFixturePayload.PSObject.Properties) {
                $entryPrefix = $entry.Name + ' = '
                $global:SpwposSmtpFixtureLines = @($global:SpwposSmtpFixtureLines | Where-Object { -not $_.StartsWith($entryPrefix) }) + ($entryPrefix + $entry.Value)
            }
            return
        }
        throw 'Unexpected SDK command in fixture.'
    }
}
function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if (-not $AsSecureString) { throw 'Unexpected unmasked prompt in fixture.' }
    return ConvertTo-SecureString $global:SpwposSmtpFixturePassword -AsPlainText -Force
}
function Get-CimInstance {
    param([string]$ClassName, [string]$Filter)
    # No actual process inspection or termination in script fixtures.
    return @()
}
function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
try {
    $null = New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'scripts') -Force
    foreach ($name in @('Initialize-Development.ps1', 'Configure-Gmail.ps1', 'Start-Development.ps1')) {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot "scripts\$name") -Destination (Join-Path $fixtureRoot "scripts\$name")
    }
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'scripts\Initialize-Dotnet.ps1') -Value '$SpwposDotnet = ''Invoke-FixtureDotnet''' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'scripts\Build-Frontend.ps1') -Value 'param([switch]$Install)' -Encoding UTF8
    $env:SPWPOS_SQL_PORT = $null
    $env:SPWPOS_SQL_PASSWORD = [guid]::NewGuid().ToString('N')

    $global:SpwposSmtpFixtureLines = @('EmailSettings:Host = smtp.gmail.com')
    & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') | Out-Null
    Assert-Condition (-not ($global:SpwposSmtpFixturePayload.PSObject.Properties.Name -like 'EmailSettings:*')) 'Existing external SMTP settings were overwritten.'

    $global:SpwposSmtpFixtureLines = @()
    & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') | Out-Null
    Assert-Condition ($global:SpwposSmtpFixturePayload.'EmailSettings:Host' -eq 'localhost') 'Missing SMTP should default to Mailpit.'

    $global:SpwposSmtpFixtureLines = @('EmailSettings:Host = smtp.gmail.com')
    & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') -UseMailpit | Out-Null
    Assert-Condition ($global:SpwposSmtpFixturePayload.'EmailSettings:Host' -eq 'localhost' -and $global:SpwposSmtpFixturePayload.'EmailSettings:Password' -eq '') 'Explicit Mailpit switch did not clear SMTP credentials.'

    & (Join-Path $fixtureRoot 'scripts\Configure-Gmail.ps1') -GmailAddress 'sender@example.test' | Out-Null
    Assert-Condition ($global:SpwposSmtpFixturePayload.'EmailSettings:Host' -eq 'smtp.gmail.com' -and $global:SpwposSmtpFixturePayload.'EmailSettings:Port' -eq '587' -and $global:SpwposSmtpFixturePayload.'EmailSettings:EnableSsl' -eq 'true') 'Gmail TLS configuration is incorrect.'
    Assert-Condition ($global:SpwposSmtpFixturePayload.'EmailSettings:Username' -eq 'sender@example.test' -and $global:SpwposSmtpFixturePayload.'EmailSettings:SenderEmail' -eq 'sender@example.test' -and $global:SpwposSmtpFixturePayload.'EmailSettings:Password' -eq $global:SpwposSmtpFixturePassword) 'Gmail identity/credential was not saved via stdin.'

    $global:SpwposSmtpFixturePayload = $null
    $global:SpwposSmtpFixturePassword = 'short'
    $wasRejected = $false
    try { & (Join-Path $fixtureRoot 'scripts\Configure-Gmail.ps1') -GmailAddress 'sender@example.test' | Out-Null }
    catch { $wasRejected = $true }
    Assert-Condition ($wasRejected -and $null -eq $global:SpwposSmtpFixturePayload) 'Invalid App Password changed settings.'

    $wasRejected = $false
    try { & (Join-Path $fixtureRoot 'scripts\Configure-Gmail.ps1') -GmailAddress 'not-an-email' | Out-Null }
    catch { $wasRejected = $true }
    Assert-Condition ($wasRejected -and $null -eq $global:SpwposSmtpFixturePayload) 'Invalid sender changed settings.'
    $fixtureDatabasePassword = [guid]::NewGuid().ToString('N')
    $global:SpwposSmtpFixtureLines = @("ConnectionStrings:DefaultConnection = Server=localhost;Database=SPWPOS;User Id=sa;Password=$fixtureDatabasePassword", 'EmailSettings:Host = smtp.gmail.com')
    $env:SPWPOS_SQL_PASSWORD = [guid]::NewGuid().ToString('N')
    & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') | Out-Null
    $fixtureConnection = New-Object System.Data.Common.DbConnectionStringBuilder
    $fixtureConnection.set_ConnectionString($global:SpwposSmtpFixturePayload.'ConnectionStrings:DefaultConnection')
    Assert-Condition ($fixtureConnection.get_Item('Password') -ceq $fixtureDatabasePassword -and $env:SPWPOS_SQL_PASSWORD -ceq $fixtureDatabasePassword) 'Stale SQL environment override replaced the saved database credential.'
    $fixtureSigningKey = $global:SpwposSmtpFixturePayload.'JwtSettings:SigningKey'
    $fixtureOtpKey = $global:SpwposSmtpFixturePayload.'OtpSettings:SecretKey'
    & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') | Out-Null
    Assert-Condition ($global:SpwposSmtpFixturePayload.'JwtSettings:SigningKey' -ceq $fixtureSigningKey -and $global:SpwposSmtpFixturePayload.'OtpSettings:SecretKey' -ceq $fixtureOtpKey) 'Ordinary restart rotated signing or OTP keys.'
    & (Join-Path $fixtureRoot 'scripts\Initialize-Development.ps1') -RotateSecurityKeys | Out-Null
    Assert-Condition ($global:SpwposSmtpFixturePayload.'JwtSettings:SigningKey' -cne $fixtureSigningKey -and $global:SpwposSmtpFixturePayload.'OtpSettings:SecretKey' -cne $fixtureOtpKey) 'Explicit security key rotation did not replace both keys.'
    $fixtureArtifactDirectory = Join-Path $fixtureRoot 'artifacts'
    $null = New-Item -ItemType Directory -Path $fixtureArtifactDirectory -Force
    $fixtureLease = [IO.File]::Open((Join-Path $fixtureArtifactDirectory '.development-startup.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $wasRejected = $false
    try {
        try { & (Join-Path $fixtureRoot 'scripts\Start-Development.ps1') -SkipCertificateTrust | Out-Null }
        catch { $wasRejected = $_.Exception.Message -like '*startup is in progress*' }
    } finally { $fixtureLease.Dispose() }
    Assert-Condition $wasRejected 'Concurrent startup did not fail before changing configuration or starting containers.'
    Write-Output 'SMTP/startup script checks: 10 cases passed; no real secrets, network, or emails used.'
} finally {
    foreach ($key in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key], 'Process') }
    # The resolved absolute path was checked against the artifact directory above.
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
    $global:SpwposSmtpFixturePayload = $null
    $global:SpwposSmtpFixturePassword = $null
}
