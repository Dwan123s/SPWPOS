param([Parameter(Mandatory=$true)][string]$PublicBaseUrl)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-Dotnet.ps1')
$taskPayOsUrl = $null
if (-not [Uri]::TryCreate($PublicBaseUrl, [UriKind]::Absolute, [ref]$taskPayOsUrl) -or
    $taskPayOsUrl.Scheme -ne 'https' -or $taskPayOsUrl.IsLoopback -or $taskPayOsUrl.UserInfo -or
    $taskPayOsUrl.Query -or $taskPayOsUrl.Fragment -or $taskPayOsUrl.AbsolutePath -ne '/') {
    throw 'Use the public HTTPS origin only, for example https://planora.example/ (not localhost).'
}
$taskPayOsSecrets = @{}
try {
    foreach ($taskPayOsName in @('ClientId','ApiKey','ChecksumKey')) {
        $taskPayOsSecure = Read-Host "Nhap payOS $taskPayOsName (an ky tu)" -AsSecureString
        $taskPayOsPointer = [IntPtr]::Zero
        try {
            $taskPayOsPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskPayOsSecure)
            $taskPayOsValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPayOsPointer).Trim()
            if ([string]::IsNullOrWhiteSpace($taskPayOsValue) -or $taskPayOsValue -match '\s') { throw 'payOS credentials cannot be empty or contain whitespace.' }
            $taskPayOsSecrets["PayOs:$taskPayOsName"] = $taskPayOsValue
        } finally {
            if ($taskPayOsPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPayOsPointer) }
            $taskPayOsSecure.Dispose(); $taskPayOsValue = $null
        }
    }
    $taskPayOsSecrets['PayOs:Enabled'] = 'true'
    $taskPayOsSecrets['PayOs:PublicBaseUrl'] = $taskPayOsUrl.AbsoluteUri.TrimEnd('/') + '/'
    # Extend host filtering for this specific public origin, retaining existing hosts.
    $taskPayOsHosts = @('localhost', $taskPayOsUrl.Host)
    $taskPayOsSettings = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../src/SPWPOS.Api/appsettings.json') -Raw | ConvertFrom-Json
    if ($taskPayOsSettings.AllowedHosts) { $taskPayOsHosts += $taskPayOsSettings.AllowedHosts -split ';' }
    $taskPayOsStore = Join-Path $env:APPDATA 'Microsoft/UserSecrets/SPWPOS-Authentication-Development/secrets.json'
    if (Test-Path -LiteralPath $taskPayOsStore) {
        $taskPayOsExisting = Get-Content -LiteralPath $taskPayOsStore -Raw | ConvertFrom-Json
        if ($taskPayOsExisting.AllowedHosts) { $taskPayOsHosts += $taskPayOsExisting.AllowedHosts -split ';' }
    }
    $taskPayOsSecrets['AllowedHosts'] = ($taskPayOsHosts | Where-Object { $_ } | Select-Object -Unique) -join ';'
    $taskPayOsPayload = $taskPayOsSecrets | ConvertTo-Json -Compress
    $taskPayOsPayload | & $SpwposDotnet user-secrets set --project (Join-Path $PSScriptRoot '../src/SPWPOS.Api')
    if ($LASTEXITCODE -ne 0) { throw 'Could not save payOS configuration.' }
    Write-Host 'Da luu cau hinh payOS vao User Secrets. Khoi dong lai API.'
    Write-Host ('Webhook URL: ' + $taskPayOsUrl.AbsoluteUri.TrimEnd('/') + '/api/commerce/payos/webhook')
    Write-Host 'Dang ky URL nay trong kenh payOS, sau khi dia chi HTTPS cong khai da truy cap duoc.'
} finally {
    $taskPayOsPayload = $null
    $taskPayOsSecrets.Clear()
    $taskPayOsExisting = $null
}
