$ErrorActionPreference = "Stop"

$port = 18777
$url = "http://127.0.0.1:$port/"
$candidatePaths = @(
    "adb",
    "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
    "C:\platform-tools\adb.exe",
    "C:\Android\platform-tools\adb.exe",
    "C:\Program Files\e2eSoft\iVCam\adb\adb.exe",
    "C:\Program Files (x86)\iMobie\DroidKit\adb.exe",
    "C:\Program Files (x86)\IriunVR\adb.exe"
)

$adb = $null
foreach ($candidatePath in $candidatePaths) {
    try {
        $command = Get-Command $candidatePath -ErrorAction Stop
        $adb = $command.Source
        break
    } catch {
    }
}

if (-not $adb) {
    throw "adb.exe was not found. Install Android platform-tools or add adb.exe to PATH."
}

$devicesOutput = & $adb devices
$deviceLine = $devicesOutput | Where-Object { $_ -match "\tdevice$" } | Select-Object -First 1
if (-not $deviceLine) {
    throw "No authorized Android device found. Check USB debugging and approve the prompt on the phone."
}

& $adb reverse "tcp:$port" "tcp:$port" | Out-Null
& $adb shell am start -a android.intent.action.VIEW -d $url | Out-Null

Write-Host "Opened ShowMeReels remote on Android: $url"
