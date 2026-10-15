$ErrorActionPreference = 'Stop'
$taskCloudTestRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskCloudFixture = Join-Path $taskCloudTestRoot ('artifacts\cloud-ai-script-tests\' + [Guid]::NewGuid().ToString('N'))
$taskCloudFixtureScripts = Join-Path $taskCloudFixture 'scripts'
[IO.Directory]::CreateDirectory($taskCloudFixtureScripts) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $taskCloudFixture 'artifacts')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $taskCloudFixture 'src\SPWPOS.Api')) | Out-Null
foreach ($taskCloudTestScript in @('Configure-VertexAI.ps1','Test-LiveAi.ps1')) {
    Copy-Item -LiteralPath (Join-Path $taskCloudTestRoot ('scripts\' + $taskCloudTestScript)) -Destination (Join-Path $taskCloudFixtureScripts $taskCloudTestScript)
}
'$SpwposDotnet = Join-Path $PSScriptRoot ''fake-dotnet.cmd''' | Set-Content -LiteralPath (Join-Path $taskCloudFixtureScripts 'Initialize-Dotnet.ps1') -Encoding UTF8
@'
@echo off
if "%1"=="user-secrets" (
  echo attempted>"%~dp0..\artifacts\secret-save.marker"
  exit /b 0
)
echo auth=%AiSettings__VertexAuthentication%;express=%AiSettings__VertexExpressMode%;location=%AiSettings__VertexLocation%>"%~dp0..\artifacts\connection-settings.marker"
if "%CLOUD_AI_FAKE_MODE%"=="success" (
  echo LIVE AI: VertexAI structured output and planner checks passed. No calendar data was saved.
  exit /b 0
)
if "%CLOUD_AI_FAKE_MODE%"=="startup" (
  echo unsafe-provider-output-with-private-token 1>&2
  exit /b 1
)
echo unsafe-provider-output-with-private-token 1>&2
echo LIVE AI check failed: AGENT_AI_AUTH_FAILED. Fake authorization denied. 1>&2
echo LIVE AI provider detail: HTTP=403; REASON=SERVICE_DISABLED. 1>&2
exit /b 1
'@ | Set-Content -LiteralPath (Join-Path $taskCloudFixtureScripts 'fake-dotnet.cmd') -Encoding ASCII

$taskCloudOriginalMode = $env:CLOUD_AI_FAKE_MODE
try {
    foreach ($taskCloudMode in @('auth','startup','success')) {
        $env:CLOUD_AI_FAKE_MODE = $taskCloudMode
        $taskCloudSaveMarker = Join-Path $taskCloudFixture 'artifacts\secret-save.marker'
        if (Test-Path $taskCloudSaveMarker) { throw 'Fixture unexpectedly reused a saved configuration.' }
        $taskCloudOldErrorPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $taskCloudFixtureOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $taskCloudFixtureScripts 'Configure-VertexAI.ps1') -UseAdc -SkipBuild -NoRestart 2>&1)
            $taskCloudFixtureExit = $LASTEXITCODE
        } finally { $ErrorActionPreference = $taskCloudOldErrorPreference }
        $taskCloudResult = Get-Content -LiteralPath (Join-Path $taskCloudFixture 'artifacts\cloud-ai-check.result.json') -Raw | ConvertFrom-Json
        $taskCloudCheckLog = Get-Content -LiteralPath (Join-Path $taskCloudFixture 'artifacts\cloud-ai-check.log') -Raw
        if ($taskCloudCheckLog -match 'unsafe-provider-output-with-private-token' -or ("$taskCloudFixtureOutput" -match 'unsafe-provider-output-with-private-token')) {
            throw 'Raw provider data leaked from the diagnostic capture.'
        }
        if ($taskCloudMode -eq 'success') {
            if ($taskCloudFixtureExit -ne 0 -or -not $taskCloudResult.success -or $taskCloudResult.code -ne 'OK' -or -not (Test-Path $taskCloudSaveMarker)) {
                throw 'A verified Cloud configuration was not saved.'
            }
        } else {
            $taskCloudExpectedCode = if ($taskCloudMode -eq 'auth') { 'AGENT_AI_AUTH_FAILED' } else { 'AI_STARTUP_FAILED' }
            $taskCloudState = Get-Content -LiteralPath (Join-Path $taskCloudFixture 'artifacts\cloud-ai-setup.status.json') -Raw | ConvertFrom-Json
            if ($taskCloudFixtureExit -eq 0 -or $taskCloudResult.success -or $taskCloudState.code -ne $taskCloudExpectedCode -or (Test-Path $taskCloudSaveMarker)) {
                throw 'Failed validation did not preserve configuration or record the safe failure code.'
            }
            if ($taskCloudMode -eq 'auth' -and ($taskCloudResult.httpStatus -ne 403 -or $taskCloudResult.providerReason -ne 'SERVICE_DISABLED' -or $taskCloudState.providerReason -ne 'SERVICE_DISABLED')) {
                throw 'Safe Google failure details did not reach the setup diagnostics.'
            }
        }
    }
    Remove-Item -LiteralPath $taskCloudSaveMarker
    $taskCloudKeyWrapper = Join-Path $taskCloudFixtureScripts 'test-key-input.ps1'
    @'
function global:Read-Host { param([string]$Prompt, [switch]$AsSecureString); ConvertTo-SecureString 'fictional-offline-key' -AsPlainText -Force }
& (Join-Path $PSScriptRoot 'Configure-VertexAI.ps1') -SkipBuild -NoRestart
'@ | Set-Content -LiteralPath $taskCloudKeyWrapper -Encoding UTF8
    $env:CLOUD_AI_FAKE_MODE = 'success'
    $taskCloudKeyOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $taskCloudKeyWrapper 2>&1)
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $taskCloudSaveMarker)) { throw 'Offline API-key setup failed.' }
    $taskCloudKeySettings = Get-Content -LiteralPath (Join-Path $taskCloudFixture 'artifacts\connection-settings.marker') -Raw
    if ($taskCloudKeySettings.Trim() -ne 'auth=ApiKey;express=true;location=global') { throw 'API-key setup did not enforce express mode and global location.' }
    if ("$taskCloudKeyOutput" -match 'fictional-offline-key') { throw 'Offline API-key input leaked into setup output.' }
    Write-Host 'Cloud setup scripts: Windows PowerShell 5.1 failure codes, secret preservation and verified saving passed. No live API called.'
} finally { $env:CLOUD_AI_FAKE_MODE = $taskCloudOriginalMode }
