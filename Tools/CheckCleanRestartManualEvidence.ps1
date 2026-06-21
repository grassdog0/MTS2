param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$ExpectedMarker = "Initialize ModTheSpire2 0.4.0-clean-restart-ui"
)

$ErrorActionPreference = "Stop"
$modDir = Join-Path $GameDir "mods\ModTheSpire2"
$dataDir = Join-Path $modDir "ModTheSpire2Data"
$companionLog = Join-Path $dataDir "companion.log"
$launcherLog = Join-Path $dataDir "launcher.log"

function Read-TextOrEmpty([string]$path) {
    if (Test-Path -LiteralPath $path) {
        return Get-Content -LiteralPath $path -Raw
    }
    return ""
}

$companion = Read-TextOrEmpty $companionLog
$launcher = Read-TextOrEmpty $launcherLog
$dllPath = Join-Path $modDir "ModTheSpire2.dll"
$expectedCleanDllSha256 = "A60A15BE68ED2486439D07A0BCF727F3B1F7811EEB3422AE8A4015B020922D6D"
$dllHash = if (Test-Path -LiteralPath $dllPath) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $dllPath).Hash
} else {
    ""
}

$markers = @()
if ($companion) {
    $markers = [regex]::Matches($companion, "Initialize ModTheSpire2 [^\r\n]+") |
        ForEach-Object { $_.Value }
}
$newestMarker = if ($markers.Count -gt 0) { $markers[-1] } else { "" }

$checks = [ordered]@{
    CompanionLogExists = [bool](Test-Path -LiteralPath $companionLog)
    LauncherLogExists = [bool](Test-Path -LiteralPath $launcherLog)
    CleanBuildLoaded = $companion.Contains($ExpectedMarker)
    OldHotOrderMarkerOnly = (-not $companion.Contains($ExpectedMarker)) -and $companion.Contains("0.4.0-hot-order-ui")
    ModSettingsButtonSeen = $companion.Contains("Visible fixed modding screen button added") -or $companion.Contains("BaseLib submenu hot-manage button added")
    ManagementDialogShown = $companion.Contains("Management dialog shown.")
    TopRightCloseUsed = $companion.Contains("Management dialog closed: top-right")
    RestartDialogShown = $companion.Contains("Restart dialog shown")
    RestartConfirmed = $companion.Contains("Restart dialog closed: confirm")
    LauncherStartedByCompanion = $companion.Contains("Started launcher for restart")
    LauncherWaitForPidUsed = $launcher.Contains("waitForPid=") -and ($launcher -match "waitForPid=[1-9][0-9]*")
    LauncherDetectedGame = $launcher.Contains("Detected gameDir=") -and $launcher.Contains("Detected settings=")
    LauncherScannedMods = $launcher.Contains("Refresh total mods=")
    LauncherLaunchedSelected = $launcher.Contains("Launch selected any=1")
}

$missing = New-Object System.Collections.Generic.List[string]
foreach ($entry in $checks.GetEnumerator()) {
    if (-not $entry.Value -and $entry.Key -ne "OldHotOrderMarkerOnly") {
        $missing.Add($entry.Key)
    }
}

$status = if ($missing.Count -eq 0) { "OK" } else { "INCOMPLETE" }
$nextAction = if ($status -eq "OK") {
    "Manual log evidence is complete. Still confirm the overlay visually before final release."
} elseif (-not $checks.CleanBuildLoaded -and $dllHash -eq $expectedCleanDllSha256) {
    "Live DLL hash matches the clean build, but companion.log has not loaded it yet. Fully exit Slay the Spire 2, start it again, open ModTheSpire2 management, then rerun this script."
} elseif (-not $checks.CleanBuildLoaded) {
    "Clean build marker is missing. Sync the clean package to the live mod folder, fully exit the game, start it again, open ModTheSpire2 management, then rerun this script."
} else {
    "Run the missing manual actions, then rerun this script."
}

[pscustomobject]@{
    Status = $status
    CompanionLog = $companionLog
    LauncherLog = $launcherLog
    Missing = ($missing -join ", ")
    NextAction = $nextAction
    LiveDllSha256 = $dllHash
    ExpectedCleanDllSha256 = $expectedCleanDllSha256
    NewestInitializeMarker = $newestMarker
    CleanBuildLoaded = $checks.CleanBuildLoaded
    OldHotOrderMarkerOnly = $checks.OldHotOrderMarkerOnly
    ModSettingsButtonSeen = $checks.ModSettingsButtonSeen
    ManagementDialogShown = $checks.ManagementDialogShown
    TopRightCloseUsed = $checks.TopRightCloseUsed
    RestartDialogShown = $checks.RestartDialogShown
    RestartConfirmed = $checks.RestartConfirmed
    LauncherStartedByCompanion = $checks.LauncherStartedByCompanion
    LauncherWaitForPidUsed = $checks.LauncherWaitForPidUsed
    LauncherScannedMods = $checks.LauncherScannedMods
    LauncherLaunchedSelected = $checks.LauncherLaunchedSelected
}
