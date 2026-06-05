param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-DotNet {
    $candidates = @(
        "$env:USERPROFILE\.dotnet\dotnet.exe"
    )

    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $candidates += $dotnetCommand.Source
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    throw ".NET SDK not found. Install .NET 8 or add dotnet to PATH."
}

function Test-WebView2RuntimeInstalled {
    $registryPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    )

    foreach ($registryPath in $registryPaths) {
        if (Test-Path $registryPath) {
            return $true
        }
    }

    return $false
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Resolve-DotNet

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\publish"
}

if (-not (Test-WebView2RuntimeInstalled)) {
    Write-Warning "Microsoft Edge WebView2 Runtime was not detected. Install it on target machines before running ShowMeReels."
}

$outputPath = Join-Path $OutputRoot $Runtime

& $dotnet publish "$repoRoot\ShowMeReels.App\ShowMeReels.App.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $outputPath
