param(
    [string]$Configuration = "Debug"
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

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Resolve-DotNet

& $dotnet test "$repoRoot\ShowMeReels.sln" -c $Configuration
