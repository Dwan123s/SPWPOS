param(
    [switch]$PrepareOnly,
    [ValidateSet('http2','auto','quic')][string]$Protocol = 'http2'
)
$ErrorActionPreference = 'Stop'
$taskPayOsTunnelRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskPayOsTunnelDirectory = Join-Path $taskPayOsTunnelRoot 'artifacts/payos-tunnel'
$null = New-Item -ItemType Directory -Path $taskPayOsTunnelDirectory -Force
$taskPayOsTunnelCertificatePath = Join-Path $taskPayOsTunnelDirectory 'localhost-public-certificate.pem'
$taskPayOsTunnelSocket = [Net.Sockets.TcpClient]::new()
$taskPayOsTunnelTls = $null
$taskPayOsTunnelCertificate = $null
try {
    if (-not $taskPayOsTunnelSocket.ConnectAsync('localhost',7043).Wait(5000)) { throw 'Local API connection timed out.' }
    $taskPayOsTunnelSocket.ReceiveTimeout = 10000
    $taskPayOsTunnelSocket.SendTimeout = 10000
    # Use normal OS certificate validation. Export only the public certificate;
    # no private key, credentials, or certificate-validation bypass is needed.
    $taskPayOsTunnelTls = [Net.Security.SslStream]::new($taskPayOsTunnelSocket.GetStream(),$false)
    $taskPayOsTunnelTls.AuthenticateAsClient('localhost')
    $taskPayOsTunnelCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($taskPayOsTunnelTls.RemoteCertificate)
    $taskPayOsTunnelCertificateBytes = $taskPayOsTunnelCertificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $taskPayOsTunnelPem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($taskPayOsTunnelCertificateBytes,[Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----`n"
    [IO.File]::WriteAllText($taskPayOsTunnelCertificatePath,$taskPayOsTunnelPem,[Text.Encoding]::ASCII)
} finally {
    if ($null -ne $taskPayOsTunnelCertificate) { $taskPayOsTunnelCertificate.Dispose() }
    if ($null -ne $taskPayOsTunnelTls) { $taskPayOsTunnelTls.Dispose() }
    $taskPayOsTunnelSocket.Dispose()
}
Write-Host 'Local HTTPS certificate verified. Public certificate prepared for cloudflared.'
if ($PrepareOnly) { return }
$taskPayOsTunnelCommand = Get-Command cloudflared.exe -CommandType Application -ErrorAction SilentlyContinue
if (-not $taskPayOsTunnelCommand) { throw 'Install cloudflared from https://developers.cloudflare.com/tunnel/downloads/ then reopen PowerShell and run this script again.' }
Write-Host 'Starting a temporary public HTTPS route to PLANORA. Keep this terminal open; Ctrl+C stops the route.'
Write-Host ("Cloudflare transport: $Protocol over IPv4 (HTTPS certificate validation remains enabled).")
Write-Host 'Copy the https://...trycloudflare.com URL, then run Configure-PayOs.ps1 in another terminal.'
& $taskPayOsTunnelCommand.Source tunnel --protocol $Protocol --edge-ip-version 4 --url https://localhost:7043 --origin-ca-pool $taskPayOsTunnelCertificatePath
if ($LASTEXITCODE -ne 0) { throw 'cloudflared stopped with an error. Review its output above.' }
