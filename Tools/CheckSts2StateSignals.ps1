param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "STATE SIGNAL CHECK FAILED: $Message"
}

function Assert-TextContains([string]$Text, [string]$Needle, [string]$SourceName) {
    if (-not $Text.Contains($Needle)) {
        Fail "Missing '$Needle' in $SourceName"
    }
    "OK $SourceName contains $Needle"
}

$libDir = Join-Path $GameDir "data_sts2_windows_x86_64"
$sts2Dll = Join-Path $libDir "sts2.dll"
$sts2Xml = Join-Path $libDir "sts2.xml"
if (-not (Test-Path -LiteralPath $sts2Dll)) {
    Fail "Missing sts2.dll at $sts2Dll"
}
if (-not (Test-Path -LiteralPath $sts2Xml)) {
    Fail "Missing sts2.xml at $sts2Xml"
}

$xmlText = Get-Content -LiteralPath $sts2Xml -Raw
$dllText = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($sts2Dll))

$xmlSignals = @(
    "MegaCrit.Sts2.Core.Nodes.NGame.PropertyName.MainMenu",
    "MegaCrit.Sts2.Core.Nodes.NGame.PropertyName.CurrentRunNode",
    "MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMainMenu.PropertyName.ContinueRunInfo",
    "MegaCrit.Sts2.Core.Runs.RunManager.IsInProgress",
    "MegaCrit.Sts2.Core.Saves.SaveManager.CurrentRunSaveTask"
)

foreach ($signal in $xmlSignals) {
    Assert-TextContains $xmlText $signal "sts2.xml"
}

$metadataSignals = @(
    "NGame",
    "MainMenu",
    "CurrentRunNode",
    "NMainMenu",
    "ContinueRunInfo",
    "RunManager",
    "IsInProgress",
    "IsGameOver",
    "IsAbandoned",
    "SaveManager",
    "HasRunSave",
    "HasMultiplayerRunSave",
    "CurrentRunSaveTask"
)

foreach ($signal in $metadataSignals) {
    Assert-TextContains $dllText $signal "sts2.dll metadata"
}
