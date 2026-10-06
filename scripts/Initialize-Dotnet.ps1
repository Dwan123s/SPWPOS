$ErrorActionPreference = 'Stop'
# Packaged desktop apps may virtualize APPDATA into their own LocalCache.
# Use an accessible loopback filesystem path for this process so CLI and API
# see the actual Windows store, including on first initialization.
# This does not create shares or change permissions; inaccessible shares are skipped.
if ($env:APPDATA -match '^[A-Za-z]:\\') {
    $taskSharedAppData = '\\localhost\' + $env:APPDATA.Substring(0, 1) + '$' + $env:APPDATA.Substring(2)
    try {
        if (Test-Path -LiteralPath $taskSharedAppData -PathType Container) {
            $env:APPDATA = $taskSharedAppData
            Write-Host 'Using the shared Windows development User Secrets store (app cache bypassed).'
        }
    } catch {
        Write-Warning 'Could not check the shared User Secrets location; using the current process location.'
    }
}
$sdkCandidates = @((Join-Path $env:LOCALAPPDATA 'SPWPOS\dotnet\dotnet.exe'))
$systemDotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($systemDotnet) { $sdkCandidates += $systemDotnet.Source }
if ($env:DOTNET_ROOT) { $sdkCandidates += Join-Path $env:DOTNET_ROOT 'dotnet.exe' }
$originalTaskPath = $env:PATH
$SpwposDotnet = $null
foreach ($sdkCandidate in ($sdkCandidates | Select-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $sdkCandidate)) { continue }
    $candidateRoot = Split-Path -Parent $sdkCandidate
    # Set these BEFORE invoking SDK tools: their child processes also resolve dotnet.
    $env:DOTNET_ROOT = $candidateRoot
    $env:DOTNET_ROOT_X64 = $candidateRoot
    $env:PATH = "$candidateRoot;$originalTaskPath"
    try {
        $sdkVersion = & $sdkCandidate --version 2>$null
        if ($LASTEXITCODE -eq 0 -and $sdkVersion -match '^10\.\d+\.\d+$') {
            $SpwposDotnet = $sdkCandidate
            Write-Host "Using .NET SDK $sdkVersion"
            break
        }
    } catch { continue }
}
if (-not $SpwposDotnet) {
    $env:PATH = $originalTaskPath
    throw 'No compatible .NET 10 SDK found. Install the .NET 10 SDK (not only the runtime), then open a new terminal.'
}
