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

function Get-RepoAppProcesses {
    param(
        [string]$RepoRoot
    )

    return @(Get-Process ShowMeReels.App -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)
    })
}

function Stop-RepoAppProcesses {
    param(
        [System.Diagnostics.Process[]]$Processes
    )

    foreach ($process in $Processes) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        }
        catch {
            & taskkill /PID $process.Id /F /T > $null 2>&1
        }
    }
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Resolve-DotNet
$projectPath = Join-Path $root "ShowMeReels.App\ShowMeReels.App.csproj"
$exePath = Join-Path $root "ShowMeReels.App\bin\Release\net8.0-windows\win-x64\ShowMeReels.App.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Release app not found. Building it now..."
    & $dotnet build $projectPath -c Release -r win-x64
}

if (-not (Test-Path $exePath)) {
    throw "Unable to find ShowMeReels.App.exe at $exePath"
}

$repoProcesses = @(Get-RepoAppProcesses -RepoRoot $root)

if ($repoProcesses.Count -gt 0) {
    Stop-RepoAppProcesses -Processes $repoProcesses
    Start-Sleep -Milliseconds 500
}

Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath) -WindowStyle Normal
