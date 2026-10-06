$ErrorActionPreference = 'Stop'
$nodeVersion = '24.21.0'
$nodeMinimum = [Version]'24.15.0'
$nodeCommand = Get-Command node.exe -ErrorAction SilentlyContinue
$SpwposNode = $null
if ($nodeCommand) {
    $installedVersion = [Version]((& $nodeCommand.Source --version).TrimStart('v'))
    if ($installedVersion -ge $nodeMinimum) { $SpwposNode = $nodeCommand.Source }
}
if (-not $SpwposNode) {
    $nodeRoot = Join-Path $env:LOCALAPPDATA 'SPWPOS\node24'
    $distributionName = "node-v$nodeVersion-win-x64"
    $SpwposNode = Join-Path $nodeRoot "$distributionName\node.exe"
    if (-not (Test-Path -LiteralPath $SpwposNode)) {
        if (-not [Environment]::Is64BitOperatingSystem) { throw 'PLANORA frontend requires 64-bit Node.js 24.15 or newer.' }
        $null = New-Item -ItemType Directory -Path $nodeRoot -Force
        $archiveName = "$distributionName.zip"
        $archivePath = Join-Path $nodeRoot $archiveName
        $downloadBase = "https://nodejs.org/dist/v$nodeVersion"
        Write-Host "Installing portable Node.js $nodeVersion for SPWPOS..."
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $checksums = (Invoke-WebRequest -UseBasicParsing -Uri "$downloadBase/SHASUMS256.txt").Content
        $checksumLine = @($checksums -split "`n" | Where-Object { $_.Trim().EndsWith(" $archiveName") })
        if ($checksumLine.Count -ne 1) { throw 'Official Node.js checksum is missing or ambiguous.' }
        $expectedHash = ($checksumLine[0] -split '\s+')[0]
        Invoke-WebRequest -UseBasicParsing -Uri "$downloadBase/$archiveName" -OutFile $archivePath
        if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Node.js checksum verification failed.' }
        Expand-Archive -LiteralPath $archivePath -DestinationPath $nodeRoot -Force
        Remove-Item -LiteralPath $archivePath
    }
}
$nodeBin = Split-Path -Parent $SpwposNode
$SpwposNpm = Join-Path $nodeBin 'node_modules\npm\bin\npm-cli.js'
if (-not (Test-Path -LiteralPath $SpwposNpm)) { throw 'npm is missing from the selected Node.js installation.' }
$env:PATH = "$nodeBin;$env:PATH"
