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
$sdkCandidates = @(
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\dotnet8\dotnet.exe'),
    (Join-Path $env:LOCALAPPDATA 'SPWPOS\dotnet8\dotnet.exe'),
    (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe')
)
$systemDotnet = @(Get-Command dotnet -All -CommandType Application -ErrorAction SilentlyContinue)
$sdkCandidates += @($systemDotnet | ForEach-Object { $_.Source })
if ($env:DOTNET_ROOT) { $sdkCandidates += Join-Path $env:DOTNET_ROOT 'dotnet.exe' }
if ($env:DOTNET_ROOT_X64) { $sdkCandidates += Join-Path $env:DOTNET_ROOT_X64 'dotnet.exe' }
if ($env:ProgramFiles) { $sdkCandidates += Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
$originalTaskPath = $env:PATH
$originalTaskDotnetRoot = $env:DOTNET_ROOT
$originalTaskDotnetRootX64 = $env:DOTNET_ROOT_X64
$SpwposDotnet = $null
Push-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
try {
    foreach ($sdkCandidate in ($sdkCandidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $sdkCandidate -PathType Leaf)) { continue }
        $candidateRoot = Split-Path -Parent $sdkCandidate
        # Child processes must use the same SDK root as this process.
        $env:DOTNET_ROOT = $candidateRoot
        $env:DOTNET_ROOT_X64 = $candidateRoot
        $env:PATH = "$candidateRoot;$originalTaskPath"
        try {
            # Windows PowerShell 5.1 can turn native stderr into a terminating
            # error. Capture it so an incompatible installation can be skipped.
            $sdkOutput = @(& $sdkCandidate --version 2>&1)
            if ($LASTEXITCODE -eq 0 -and $sdkOutput.Count -eq 1 -and
                "$($sdkOutput[0])" -match '^8\.\d+\.\d+$') {
                $sdkVersion = "$($sdkOutput[0])"
                $SpwposDotnet = $sdkCandidate
                Write-Host "Using .NET SDK $sdkVersion ($SpwposDotnet)"
                break
            }
        } catch { continue }
    }
} finally {
    Pop-Location
    if (-not $SpwposDotnet) {
        $env:PATH = $originalTaskPath
        $env:DOTNET_ROOT = $originalTaskDotnetRoot
        $env:DOTNET_ROOT_X64 = $originalTaskDotnetRootX64
    }
}
if (-not $SpwposDotnet) {
    $minimumSdkVersion = (Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'global.json') -Raw | ConvertFrom-Json).sdk.version
    throw "No compatible .NET 8 SDK found. Run scripts/Install-Dotnet8.ps1 or install a stable .NET 8 SDK ($minimumSdkVersion or later in the 8.0 series), then open a new terminal. The SDK is required."
}
