param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$ExpectedVersion = "0.4.0",
    [switch]$SkipLive
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$clean = Join-Path $root "dist\WorkshopUpload\ModTheSpire2Content-Clean"
$uploader = Join-Path $root "ModUploader-win-x64\ModTheSpire2Workspace\content"
$live = Join-Path $GameDir "mods\ModTheSpire2"
$launcher = Join-Path $clean "ModTheSpire2Launcher.exe"
$gameExe = Join-Path $GameDir "SlayTheSpire2.exe"
$quotedGameExe = '"' + $gameExe + '"'
$releaseInfo = Join-Path $GameDir "release_info.json"

function Fail([string]$message) {
    throw "VERIFY FAILED: $message"
}

function Assert-Exists([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing path: $path"
    }
}

function Get-Sha([string]$path) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
}

Assert-Exists $clean
Assert-Exists $uploader
Assert-Exists $launcher
Assert-Exists $gameExe
$currentGameVersion = ""
if (Test-Path -LiteralPath $releaseInfo) {
    try {
        $releaseJson = Get-Content -LiteralPath $releaseInfo -Raw | ConvertFrom-Json -ErrorAction Stop
        $currentGameVersion = if ($releaseJson.version) { [string]$releaseJson.version } elseif ($releaseJson.branch) { [string]$releaseJson.branch } else { "" }
    } catch {
        $currentGameVersion = ""
    }
}
if (-not $SkipLive) {
    Assert-Exists $live
}

$stateSignalCheck = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\CheckSts2StateSignals.ps1") -GameDir $GameDir
if ($LASTEXITCODE -ne 0) {
    Fail "STS2 state signal check failed"
}
if (-not ($stateSignalCheck -match "HasRunSave") -or -not ($stateSignalCheck -match "HasMultiplayerRunSave") -or -not ($stateSignalCheck -match "ContinueRunInfo")) {
    Fail "STS2 state signal check did not verify unfinished-run signals"
}

$manifestAnalysis = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\AnalyzeModManifests.ps1") -GameDir $GameDir
if ($LASTEXITCODE -ne 0) {
    Fail "AnalyzeModManifests.ps1 failed"
}
$manifestAnalysisText = ($manifestAnalysis | Out-String)
if (-not ($manifestAnalysisText -match "QuickRestart" -and $manifestAnalysisText -match "BaseLib min_version=3.3.0")) {
    Fail "Manifest analysis did not capture QuickRestart dependency version constraint"
}
if (-not ($manifestAnalysisText -match "wuwancients" -and $manifestAnalysisText -match "BaseLib min_version=v3.2.0")) {
    Fail "Manifest analysis did not capture wuwancients dependency version constraint"
}
if (-not ($manifestAnalysisText -match "ActsFromThePast" -and $manifestAnalysisText -match "BaseLib min_version=v[0-9]+\.[0-9]+\.[0-9]+")) {
    Fail "Manifest analysis did not capture ActsFromThePast dependency version constraint"
}
if (-not ($manifestAnalysisText -match "STS2-RitsuLib" -and $manifestAnalysisText -match "0.107.1")) {
    Fail "Manifest analysis did not capture RitsuLib minimum game version"
}
if (-not ($manifestAnalysisText -match "wuwancients" -and $manifestAnalysisText -match "0.107.0")) {
    Fail "Manifest analysis did not capture wuwancients minimum game version"
}

$companionSource = Get-Content (Join-Path $root "ModTheSpire2Companion\ModTheSpire2Entry.HotManage.cs") -Raw
$classifierSource = Get-Content (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -Raw
$launcherSource = Get-Content (Join-Path $root "NativeLauncher\ModTheSpire2Launcher.c") -Raw
if (-not ($companionSource.Contains("KnownMainMenuContentMods") -and $companionSource.Contains("not proof that toggling the loaded mod itself is safe"))) {
    Fail "Companion source no longer documents why wuwancients is not an entire-mod Hot-Apply allow-list entry"
}
if (-not ($classifierSource.Contains('$knownMainMenuContentMods') -and $classifierSource.Contains("toggling the loaded DLL/PCK mod itself is still restart-required"))) {
    Fail "Classifier source no longer documents why wuwancients is not an entire-mod Hot-Apply allow-list entry"
}
if (-not ($companionSource.Contains("TryReadInvalidManifestFallback") -and $companionSource.Contains('"invalid manifest; restart required"'))) {
    Fail "Companion source no longer has the conservative invalid-manifest fallback"
}
if (-not ($companionSource.Contains("Whole-mod changes require closing the game and reopening the launcher.") -and $companionSource.Contains("BuildRestartWorkflowSummary"))) {
    Fail "Companion source no longer explains the restart-based whole-mod workflow"
}
if (-not ($companionSource.Contains("requires STS2 >= ") -and $companionSource.Contains("MinGameVersion"))) {
    Fail "Companion source no longer shows minimum game version details in mod rows"
}
if (-not ($companionSource.Contains("RestartDialogMetrics") -and $companionSource.Contains("var scroll = new ScrollContainer") -and $companionSource.Contains("scroll.CustomMinimumSize = new Vector2(updated.ContentWidth, updated.ScrollHeight)") -and $companionSource.Contains("Math.Max(90, panelHeight"))) {
    Fail "Companion source no longer constrains management/restart dialogs to a scrollable viewport-safe layout"
}
if (-not ($companionSource.Contains("CreateIconButton") -and $companionSource.Contains("Close ModTheSpire2 management and return to the game.") -and $companionSource.Contains("Close the game and open the launcher to change restart-required mods.") -and $companionSource.Contains("Enabled For This Launch") -and $companionSource.Contains("Available But Disabled"))) {
    Fail "Companion source no longer exposes the clean restart management controls"
}
if (-not ($companionSource.Contains("BuildRestartLauncherArguments") -and $companionSource.Contains("System.Environment.GetCommandLineArgs()") -and $companionSource.Contains('args += " -- " + QuoteArg(current[0])') -and $companionSource.Contains("renderer flags such as --rendering-driver opengl3 are preserved"))) {
    Fail "Companion source no longer forwards the current game command line to the launcher restart flow"
}
if (-not ($companionSource.Contains("MultiplayerMismatchErrorPatch") -and $companionSource.Contains("NetErrorInfo") -and $companionSource.Contains("GetErrorString") -and $companionSource.Contains("missingModsOnLocal") -and $companionSource.Contains("missingModsOnHost") -and $companionSource.Contains("multiplayer-mismatch-last.txt"))) {
    Fail "Companion source no longer exposes the read-only multiplayer mismatch helper"
}
if (-not ($companionSource.Contains("MismatchModResolver") -and $companionSource.Contains("public static bool SelfTest()") -and $companionSource.Contains("https://steamcommunity.com/sharedfiles/filedetails/?id=") -and $companionSource.Contains("Known local/subscribed mod index"))) {
    Fail "Companion source no longer maps multiplayer mismatch entries to Workshop links"
}
if (-not ($companionSource.Contains("MultiplayerMismatchActions") -and $companionSource.Contains("Open Missing Mod Links") -and $companionSource.Contains("MaxOpenLinks") -and $companionSource.Contains("ExtractWorkshopLinks") -and $companionSource.Contains("OpenMismatchWorkshopLinksFromConfig"))) {
    Fail "Companion source no longer exposes a safe way to open mismatch Workshop links"
}
if (-not ($companionSource.Contains("RunStartupSelfTests") -and $companionSource.Contains("MultiplayerMismatchActions.SelfTest()") -and $companionSource.Contains("MismatchModResolver.SelfTest()") -and $companionSource.Contains("Startup self-tests: mismatchLinks="))) {
    Fail "Companion source no longer runs lightweight mismatch helper startup self-tests"
}
if (-not ($companionSource.Contains("Copy Mismatch Report") -and $companionSource.Contains("CopyLastReport") -and $companionSource.Contains("DisplayServer.ClipboardSet(report)") -and $companionSource.Contains("CopyMismatchReportFromConfig"))) {
    Fail "Companion source no longer exposes a safe way to copy mismatch reports"
}
if (-not ($companionSource.Contains("A full report is saved to ModTheSpire2Data\\multiplayer-mismatch-last.txt") -and $companionSource.Contains("Open ModTheSpire2 Management to use Open Missing Mod Links or Copy Mismatch Report"))) {
    Fail "Multiplayer mismatch helper no longer tells players where to find/report mismatch details"
}
if (-not ($companionSource.Contains("CreateMismatchReportPanel") -and $companionSource.Contains("Latest multiplayer mismatch report") -and $companionSource.Contains("Workshop links found") -and $companionSource.Contains("GetLastReportStatus"))) {
    Fail "Management dialog no longer shows multiplayer mismatch report status"
}
if (-not ($companionSource.Contains('openMismatchLinks.Pressed += MultiplayerMismatchActions.OpenLastWorkshopLinks') -and $companionSource.Contains('copyMismatchReport.Pressed += MultiplayerMismatchActions.CopyLastReport'))) {
    Fail "Management dialog no longer exposes multiplayer mismatch report actions"
}
if ($companionSource.Contains("Apply Hot Changes") -or $companionSource.Contains("Apply selected Runtime Hot-Apply and Apply at Main Menu changes.")) {
    Fail "Companion source still exposes player-facing Hot-Apply controls in the clean restart UI"
}
if ($companionSource.Contains("ForceJoin") -or $companionSource.Contains("BypassModMismatch") -or $companionSource.Contains("SubscribeItem")) {
    Fail "Companion source appears to expose force-join, mismatch-bypass, or auto-subscribe behavior"
}
if (-not ($companionSource.Contains("Whole-mod changes require closing the game and reopening the launcher.") -and $companionSource.Contains("Close and Open Launcher") -and $companionSource.Contains("BuildRestartLauncherArguments"))) {
    Fail "Companion source no longer preserves the clean restart-manager workflow"
}
if (-not ($classifierSource.Contains("invalid manifest; restart required") -and $classifierSource.Contains("GetFileNameWithoutExtension") -and $classifierSource.Contains("Test-PayloadFileInModDir"))) {
    Fail "Classifier source no longer has the conservative invalid-manifest fallback"
}
if (-not ($launcherSource.Contains("ApplyBetterModMenuGroupsFromJson") -and $launcherSource.Contains("mod_data\\BetterModMenu\\mod_profiles.json") -and $launcherSource.Contains("ApplyBetterModMenuGroupsFromCsvExports") -and $launcherSource.Contains("ApplyFallbackGroup") -and $launcherSource.Contains("group=%ls"))) {
    Fail "Launcher source no longer preserves optional Better Mod Menu grouping with fallback diagnostics"
}
if (-not ($launcherSource.Contains("swprintf(path, _countof(path), L`"%ls\\mod_data\\BetterModMenu\\mod_profiles.json`", userRoot);") -and $launcherSource.Contains("char* json = ReadFileBytes(path, &size);"))) {
    Fail "Launcher source no longer reads Better Mod Menu profile JSON through the read-only file path"
}
if ($launcherSource -match "WriteFileBytes\([^)]*BetterModMenu|CreateFileW\([^)]*BetterModMenu|DeleteFileW\([^)]*BetterModMenu|MoveFileW\([^)]*BetterModMenu|CopyFileW\([^)]*BetterModMenu") {
    Fail "Launcher source appears to write, delete, move, or copy real Better Mod Menu files; grouping import must remain read-only"
}
if (-not ($launcherSource.Contains("JoinPath(csvPath, _countof(csvPath), dataDir, L`"better-mod-menu-group-self-test.csv`");") -and $launcherSource.Contains("DeleteFileW(csvPath);"))) {
    Fail "Launcher grouping self-test no longer confines temporary CSV writes to ModTheSpire2Data"
}

$expectedFiles = @(
    "ModTheSpire2.dll",
    "ModTheSpire2.json",
    "ModTheSpire2.pck",
    "ModTheSpire2Launcher.exe",
    "ModTheSpire2Launcher.sh",
    "ModTheSpire2Launcher.command",
    "README.md"
)

$actualFiles = Get-ChildItem -LiteralPath $clean -File | Select-Object -ExpandProperty Name | Sort-Object
$expectedSorted = $expectedFiles | Sort-Object
if (@($actualFiles).Count -ne @($expectedSorted).Count) {
    Fail "Clean package file count mismatch. Found: $($actualFiles -join ', ')"
}
for ($i = 0; $i -lt $expectedSorted.Count; $i++) {
    if ($actualFiles[$i] -ne $expectedSorted[$i]) {
        Fail "Clean package files mismatch. Found: $($actualFiles -join ', ')"
    }
}

$manifest = Get-Content (Join-Path $clean "ModTheSpire2.json") -Raw | ConvertFrom-Json
if ($manifest.id -ne "ModTheSpire2") {
    Fail "Unexpected manifest id: $($manifest.id)"
}
if ($manifest.version -ne $ExpectedVersion) {
    Fail "Unexpected manifest version: $($manifest.version), expected $ExpectedVersion"
}

foreach ($file in $expectedFiles) {
    $cleanFile = Join-Path $clean $file
    $uploaderFile = Join-Path $uploader $file
    Assert-Exists $cleanFile
    Assert-Exists $uploaderFile
    $cleanHash = Get-Sha $cleanFile
    $uploaderHash = Get-Sha $uploaderFile
    if ($cleanHash -ne $uploaderHash) {
        Fail "$file differs between clean package and uploader content"
    }
    if (-not $SkipLive) {
        $liveFile = Join-Path $live $file
        Assert-Exists $liveFile
        $liveHash = Get-Sha $liveFile
        if ($cleanHash -ne $liveHash) {
            Fail "$file differs between clean package and live test folder"
        }
    }
}

function Assert-LfScript([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    for ($i = 0; $i -lt $bytes.Length - 1; $i++) {
        if ($bytes[$i] -eq 13 -and $bytes[$i + 1] -eq 10) {
            Fail "Script uses CRLF line endings instead of LF: $path"
        }
    }
}

$linuxScript = Join-Path $clean "ModTheSpire2Launcher.sh"
$macScript = Join-Path $clean "ModTheSpire2Launcher.command"
Assert-LfScript $linuxScript
Assert-LfScript $macScript
$linuxScriptText = Get-Content -LiteralPath $linuxScript -Raw
$macScriptText = Get-Content -LiteralPath $macScript -Raw
if (-not $linuxScriptText.StartsWith("#!/usr/bin/env bash")) {
    Fail "Linux launcher script is missing the bash shebang"
}
if (-not $macScriptText.StartsWith("#!/bin/sh")) {
    Fail "macOS command wrapper is missing the sh shebang"
}
if (-not ($macScriptText.Contains("ModTheSpire2Launcher.sh") -and $macScriptText.Contains('exec /usr/bin/env bash'))) {
    Fail "macOS command wrapper no longer delegates to the shared shell launcher"
}
foreach ($needle in @(
    'settings-backups',
    'cp -p "$settings_file" "$backup"',
    'Launch Vanilla once',
    'Launch saved enabled mods',
    'Diagnostics',
    'Configure Steam Launch Options to call this script with -- %command%',
    'steam://rungameid/$APP_ID',
    'SELECTED_IDS_BLOB',
    'DISCOVERED_MODS_BLOB'
)) {
    if (-not $linuxScriptText.Contains($needle)) {
        Fail "Linux/macOS shared script is missing expected behavior marker: $needle"
    }
}

$runtimeGhostDir = Join-Path $root "TestMods\ModTheSpire2Data\scan-ignored"
New-Item -ItemType Directory -Force -Path $runtimeGhostDir | Out-Null
Set-Content -LiteralPath (Join-Path $runtimeGhostDir "GhostRuntimeDataMod.json") -Value '{"id":"GhostRuntimeDataMod","name":"Ghost Runtime Data Mod","version":"0.0.0","hot_apply":true}' -Encoding UTF8

$classify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 failed"
}
if ($classify -match "GhostRuntimeDataMod") {
    Fail "Classifier scanned ModTheSpire2Data runtime files as mods"
}
if (Test-Path -LiteralPath (Join-Path $root "TestMods\ModTheSpire2Data")) {
    $resolvedRuntimeGhost = (Resolve-Path -LiteralPath (Join-Path $root "TestMods\ModTheSpire2Data")).Path
    $resolvedTestMods = (Resolve-Path -LiteralPath (Join-Path $root "TestMods")).Path
    if (-not $resolvedRuntimeGhost.StartsWith($resolvedTestMods, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean runtime ghost outside TestMods: $resolvedRuntimeGhost"
    }
    Remove-Item -LiteralPath $resolvedRuntimeGhost -Recurse -Force
}
if (-not ($classify -match "ModTheSpire2 \[ModTheSpire2\] :: launcher/UI patch requires restart")) {
    Fail "Classifier output did not keep ModTheSpire2 restart-required"
}
if (-not ($classify -match "ModTheSpire2 Hot Config Test \[ModTheSpire2HotConfigTest\]")) {
    Fail "Classifier output did not include controlled config-only hot candidate"
}
if (-not ($classify -match "ModTheSpire2 Runtime Scope Config Test \[ModTheSpire2RuntimeScopeConfigTest\] :: manifest declares runtime-safe hot-apply")) {
    Fail "Classifier output did not allow explicit runtime/config scope for config-only mods"
}
if (-not ($classify -match "ModTheSpire2 Run-Safe Runtime Config Test \[ModTheSpire2RunSafeRuntimeConfigTest\] :: manifest declares runtime-safe hot-apply")) {
    Fail "Classifier output did not allow explicit run-safe runtime/config scope for config-only mods"
}
if (-not ($classify -match "ModTheSpire2 Runtime Scope DLL Guard Test \[ModTheSpire2RuntimeScopeDllGuardTest\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Classifier output incorrectly allowed explicit runtime/config scope for DLL/PCK mods"
}
if (-not ($classify -match "ModTheSpire2 Nested Payload Guard Test \[ModTheSpire2NestedPayloadGuardTest\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Classifier output incorrectly allowed hot-apply for a mod with a nested DLL/PCK payload"
}
if (-not ($classify -match "ModTheSpire2InvalidManifestFallbackTest \[ModTheSpire2InvalidManifestFallbackTest\] :: invalid manifest; restart required")) {
    Fail "Classifier output did not include controlled invalid-manifest fallback"
}
if (-not ($classify -match "ModTheSpire2 Settings Json Manifest Test \[ModTheSpire2SettingsJsonManifestTest\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Classifier output did not keep payload-backed settings.json manifest compatibility"
}
if ($classify -match "ModTheSpire2JsonFalsePositiveGuardTest") {
    Fail "Classifier incorrectly discovered a plain config.json id as a mod"
}
if (-not ($classify -match "Quick Restart \[QuickRestart\] :: DLL/PCK")) {
    Fail "Classifier output did not keep QuickRestart restart-required"
}
if (-not ($classify -match "Act 4 Heart \[Act4Heart\] :: DLL/PCK")) {
    Fail "Classifier output did not include Act4Heart from mod_manifest.json"
}
if (-not ($classify -match "RitsuLib \[STS2-RitsuLib\] :: framework DLL; restart required")) {
    Fail "Classifier output did not keep RitsuLib restart-required"
}
if (-not ($classify -match "ModTheSpire2 Tagged Framework Test \[ModTheSpire2TaggedFrameworkTest\] :: framework DLL; restart required")) {
    Fail "Classifier output did not treat framework/library tags as restart-required"
}
if (-not ($classify -match "ModTheSpire2 Tagged UI Patch Test \[ModTheSpire2TaggedUiPatchTest\] :: startup/core/UI patch; restart required")) {
    Fail "Classifier output did not let UI/core patch tags override hot_apply"
}
if (-not ($classify -match "ModTheSpire2 Tagged Save Serializer Test \[ModTheSpire2TaggedSaveSerializerTest\] :: save serializer; restart required")) {
    Fail "Classifier output did not keep save serializer tags restart-required"
}
if (-not ($classify -match "ModTheSpire2 Tagged Active Run Patch Test \[ModTheSpire2TaggedActiveRunPatchTest\] :: active-run patch; restart required")) {
    Fail "Classifier output did not keep active-run patch tags restart-required"
}
if (-not ($classify -match "ModTheSpire2 Tagged Save Affecting Patch Test \[ModTheSpire2TaggedSaveAffectingPatchTest\] :: save-affecting patch; restart required")) {
    Fail "Classifier output did not keep save-affecting patch tags restart-required"
}
if (-not ($classify -match "wuwancients\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Classifier output incorrectly allowed wuwancients as an entire-mod Hot-Apply candidate"
}
if (-not ($classify -match "ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\] :: main menu only; safe menu state not confirmed")) {
    Fail "Classifier output did not include controlled main-menu-only content test"
}
if (-not ($classify -match "ModTheSpire2 Tagged Future Content Test \[ModTheSpire2TaggedFutureContentTest\] :: main menu only; safe menu state not confirmed")) {
    Fail "Classifier output did not classify content_type/content_tags as a main-menu-only content test"
}
if (-not ($classify -match "ModTheSpire2 Tagged Framework Dependent Content Test \[ModTheSpire2TaggedFrameworkDependentContentTest\] :: main menu only; safe menu state not confirmed")) {
    Fail "Classifier output did not classify framework-dependent future content as a main-menu-only content test"
}
if (-not ($classify -match "ModTheSpire2 Hot Depends Restart \[ModTheSpire2HotDependsRestart\] :: depends on restart-required mod: BaseLib")) {
    Fail "Classifier output did not downgrade hot-declared mod with restart-required dependency"
}
if (-not ($classify -match "ModTheSpire2 Hot Depends Chain \[ModTheSpire2HotDependsChain\] :: depends on restart-required mod: ModTheSpire2HotDependsRestart")) {
    Fail "Classifier output did not downgrade transitive hot dependency chain"
}
if (-not ($classify -match "ModTheSpire2 Requires Alias Test \[ModTheSpire2RequiresAliasTest\] :: depends on restart-required mod: BaseLib")) {
    Fail "Classifier output did not parse requires dependency alias"
}
if (-not ($classify -match "ModTheSpire2 RequiredMods Alias Test \[ModTheSpire2RequiredModsAliasTest\] :: depends on restart-required mod: BaseLib")) {
    Fail "Classifier output did not parse requiredMods/modId dependency alias"
}
if (-not ($classify -match "ModTheSpire2 Single String Dependency Test \[ModTheSpire2SingleStringDependencyTest\] :: depends on restart-required mod: BaseLib")) {
    Fail "Classifier output did not parse single-string dependency fields"
}
if (-not ($classify -match "ModTheSpire2 Optional Dependency Test \[ModTheSpire2OptionalDependencyTest\] :: manifest declares runtime-safe hot-apply")) {
    Fail "Classifier output incorrectly treated optional dependency objects as missing hard dependencies"
}
if (-not ($classify -match "ModTheSpire2 Sidecar Manifest Id Test \[ModTheSpire2SidecarManifestIdTest\] :: manifest declares runtime-safe hot-apply")) {
    Fail "Classifier output did not discover explicit-id .manifest mod files"
}
if ($classify -match "IgnoredVariant") {
    Fail "Classifier incorrectly discovered id-less sidecar variants manifest as a mod"
}
if (-not ($classify -match "ModTheSpire2 Name Alias Dependent \[ModTheSpire2NameAliasDependent\] :: depends on restart-required mod: ModTheSpire2NameAliasBase")) {
    Fail "Classifier output did not resolve unique dependency display-name alias"
}
if (-not ($classify -match "ModTheSpire2 Ambiguous Name Alias Dependent \[ModTheSpire2AmbiguousNameAliasDependent\] :: missing dependency: ModTheSpire2 Ambiguous Name Alias")) {
    Fail "Classifier incorrectly resolved or ignored ambiguous dependency display-name alias"
}
if (-not ($classify -match "ModTheSpire2 Missing Dependency Test \[ModTheSpire2MissingDependencyTest\] :: missing dependency: ModTheSpire2NotInstalledDependency")) {
    Fail "Classifier output did not prioritize missing dependency reason for restart-required mods"
}
if (-not ($classify -match "ModTheSpire2 Versioned Framework Old Test \[ModTheSpire2VersionedFrameworkOldTest\] :: framework DLL; restart required")) {
    Fail "Classifier output did not keep versioned framework fixture restart-required"
}
if (-not ($classify -match "ModTheSpire2 Versioned Dependency Too Low Test \[ModTheSpire2VersionedDependencyTooLowTest\] :: dependency version too low: ModTheSpire2VersionedFrameworkOldTest requires v1.3.0, found v1.2.0")) {
    Fail "Classifier output did not downgrade dependency with a too-low installed version"
}
if (-not ($classify -match "ModTheSpire2 Future Game Version Test \[ModTheSpire2FutureGameVersionTest\] :: game version too low: requires v999.0.0, found $([regex]::Escape($currentGameVersion))")) {
    Fail "Classifier output did not downgrade mod requiring a newer game version"
}
if (-not ($classify -match "ModTheSpire2 Pck Name Fallback Test \[ModTheSpire2PckNameFallbackTest\] :: gameplay or unknown behavior")) {
    Fail "Classifier output did not discover pck_name-only manifest fallback"
}
if (-not ($classify -match "ModTheSpire2 Pck Name Duplicate Guard Real \[ModTheSpire2PckNameDuplicateGuardReal\] :: utility/config candidate")) {
    Fail "Classifier output did not include pck_name duplicate guard real manifest"
}
if ($classify -match "\[ModTheSpire2PckNameDuplicateGuardTest\]") {
    Fail "Classifier incorrectly created duplicate mod from pck_name sidecar beside id manifest"
}

$safeMenuClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeSafeMainMenuNoRun
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeSafeMainMenuNoRun failed"
}
if (-not ($safeMenuClassify -match "wuwancients\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Safe-menu classifier incorrectly allowed wuwancients entire-mod toggle"
}
if (-not ($safeMenuClassify -match "ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\] :: main menu only; mod is not loaded, start through launcher to enable")) {
    Fail "Safe-menu classifier incorrectly allowed unloaded controlled future-run content test"
}
if (-not ($safeMenuClassify -match "ModTheSpire2 Tagged Future Content Test \[ModTheSpire2TaggedFutureContentTest\] :: main menu only; mod is not loaded, start through launcher to enable")) {
    Fail "Safe-menu classifier incorrectly allowed unloaded tagged future-run content test"
}
if (-not ($safeMenuClassify -match "ModTheSpire2 Tagged Framework Dependent Content Test \[ModTheSpire2TaggedFrameworkDependentContentTest\] :: main menu only; mod is not loaded, start through launcher to enable")) {
    Fail "Safe-menu classifier incorrectly allowed unloaded framework-dependent future content test"
}

$enabledButUnloadedClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeSafeMainMenuNoRun -AssumeEnabledIds ModTheSpire2FutureRunContentTest -ShowState
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeEnabledIds failed"
}
if (-not ($enabledButUnloadedClassify -match "ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\] :: main menu only; mod is not loaded, start through launcher to enable enabled=True loaded=False")) {
    Fail "Enabled-but-unloaded classifier incorrectly treated settings-enabled state as loaded"
}

$unfinishedRunClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeMainMenuWithUnfinishedRun -AssumeLoadedIds wuwancients,BaseLib,ModTheSpire2FutureRunContentTest,ModTheSpire2TaggedFutureContentTest
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeMainMenuWithUnfinishedRun failed"
}
if (-not ($unfinishedRunClassify -match "wuwancients\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Unfinished-run classifier incorrectly allowed wuwancients entire-mod toggle"
}
if (-not ($unfinishedRunClassify -match "ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\] :: main menu only; unfinished run present")) {
    Fail "Unfinished-run classifier incorrectly allowed controlled future-run content test"
}

$activeRunClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeActiveRun -AssumeLoadedIds wuwancients,BaseLib,ModTheSpire2FutureRunContentTest,ModTheSpire2TaggedFutureContentTest
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeActiveRun failed"
}
if (-not ($activeRunClassify -match "wuwancients\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Active-run classifier incorrectly allowed wuwancients entire-mod toggle"
}
if (-not ($activeRunClassify -match "ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\] :: main menu only; active run may reference this content")) {
    Fail "Active-run classifier incorrectly allowed controlled future-run content test"
}
if (-not ($activeRunClassify -match "ModTheSpire2 Runtime Scope Config Test \[ModTheSpire2RuntimeScopeConfigTest\] :: runtime/config apply blocked during active run")) {
    Fail "Active-run classifier incorrectly allowed runtime/config mod without run-safe tag"
}
if (-not ($activeRunClassify -match "ModTheSpire2 Run-Safe Runtime Config Test \[ModTheSpire2RunSafeRuntimeConfigTest\] :: manifest declares runtime-safe hot-apply")) {
    Fail "Active-run classifier incorrectly blocked explicit run-safe runtime/config mod"
}

$loadedSafeMenuClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeSafeMainMenuNoRun -AssumeLoadedIds wuwancients,BaseLib,ModTheSpire2FutureRunContentTest,ModTheSpire2TaggedFutureContentTest,ModTheSpire2TaggedFrameworkDependentContentTest -AssumeEnabledIds wuwancients,BaseLib,ModTheSpire2FutureRunContentTest,ModTheSpire2TaggedFutureContentTest,ModTheSpire2TaggedFrameworkDependentContentTest
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeSafeMainMenuNoRun -AssumeLoadedIds -AssumeEnabledIds failed"
}
if (-not ($loadedSafeMenuClassify -match "wuwancients\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Loaded safe-menu classifier incorrectly promoted wuwancients entire-mod toggle"
}
if (-not ($loadedSafeMenuClassify -match "ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\] :: future-run content toggle; main menu safe")) {
    Fail "Loaded safe-menu classifier did not promote controlled future-run content test"
}
if (-not ($loadedSafeMenuClassify -match "ModTheSpire2 Tagged Future Content Test \[ModTheSpire2TaggedFutureContentTest\] :: future-run content toggle; main menu safe")) {
    Fail "Loaded safe-menu classifier did not promote tagged future-run content test"
}
if (-not ($loadedSafeMenuClassify -match "ModTheSpire2 Tagged Framework Dependent Content Test \[ModTheSpire2TaggedFrameworkDependentContentTest\] :: depends on restart-required mod: ModTheSpire2TaggedFrameworkTest")) {
    Fail "Loaded safe-menu classifier incorrectly allowed content whose tagged framework dependency is not loaded"
}

$partialSignalsClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeSafeMainMenuNoRun -AssumePartialSignals -AssumeLoadedIds wuwancients,BaseLib,ModTheSpire2FutureRunContentTest,ModTheSpire2TaggedFutureContentTest
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeSafeMainMenuNoRun -AssumePartialSignals failed"
}
if (-not ($partialSignalsClassify -match "wuwancients\] :: DLL/PCK or unknown startup behavior")) {
    Fail "Partial-signal classifier incorrectly allowed wuwancients entire-mod toggle"
}
if (-not ($partialSignalsClassify -match "ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\] :: main menu only; safe menu state not confirmed")) {
    Fail "Partial-signal classifier incorrectly allowed controlled future-run content test"
}
if (-not ($partialSignalsClassify -match "ModTheSpire2 Tagged Future Content Test \[ModTheSpire2TaggedFutureContentTest\] :: main menu only; safe menu state not confirmed")) {
    Fail "Partial-signal classifier incorrectly allowed tagged future-run content test"
}

$loadedTaggedFrameworkClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeSafeMainMenuNoRun -AssumeLoadedIds ModTheSpire2TaggedFrameworkTest,ModTheSpire2TaggedFrameworkDependentContentTest
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeSafeMainMenuNoRun -AssumeLoadedIds tagged framework failed"
}
if (-not ($loadedTaggedFrameworkClassify -match "ModTheSpire2 Tagged Framework Test \[ModTheSpire2TaggedFrameworkTest\] :: framework DLL; restart required")) {
    Fail "Tagged framework root was not kept restart-required"
}
if (-not ($loadedTaggedFrameworkClassify -match "ModTheSpire2 Tagged Framework Dependent Content Test \[ModTheSpire2TaggedFrameworkDependentContentTest\] :: depends on restart-required mod: ModTheSpire2TaggedFrameworkTest")) {
    Fail "Loaded but disabled tagged framework root incorrectly allowed dependent future content candidate"
}

$enabledLoadedTaggedFrameworkClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeSafeMainMenuNoRun -AssumeLoadedIds ModTheSpire2TaggedFrameworkTest,ModTheSpire2TaggedFrameworkDependentContentTest -AssumeEnabledIds ModTheSpire2TaggedFrameworkTest,ModTheSpire2TaggedFrameworkDependentContentTest
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -AssumeSafeMainMenuNoRun -AssumeLoadedIds -AssumeEnabledIds tagged framework failed"
}
if (-not ($enabledLoadedTaggedFrameworkClassify -match "ModTheSpire2 Tagged Framework Dependent Content Test \[ModTheSpire2TaggedFrameworkDependentContentTest\] :: future-run content toggle; main menu safe")) {
    Fail "Enabled and loaded tagged framework root did not allow dependent future content candidate"
}

$applyStateGateSelfTest = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir -AssumeSafeMainMenuNoRun -AssumeLoadedIds ModTheSpire2FutureRunContentTest -AssumeEnabledIds ModTheSpire2FutureRunContentTest,ModTheSpire2RuntimeScopeConfigTest -SelfTestApplyStateGate
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 -SelfTestApplyStateGate failed"
}
if (-not ($applyStateGateSelfTest -match "PASS: Cannot disable ModTheSpire2 Future Run Content Test \[ModTheSpire2FutureRunContentTest\]: an unfinished run may reference this content")) {
    Fail "Apply state gate self-test did not block disabling main-menu content when an unfinished run appears"
}
if (-not ($applyStateGateSelfTest -match "PASS: unchanged main-menu content does not block")) {
    Fail "Apply state gate self-test incorrectly blocked unchanged main-menu content"
}
if (-not ($applyStateGateSelfTest -match "PASS: Cannot change ModTheSpire2 Runtime Scope Config Test \[ModTheSpire2RuntimeScopeConfigTest\]: runtime/config changes require a run-safe manifest token while a run is active")) {
    Fail "Apply state gate self-test did not block runtime/config changes when an active run appears"
}

$verifyFixturesRoot = Join-Path $root "dist\verify-fixtures"
New-Item -ItemType Directory -Force -Path $verifyFixturesRoot | Out-Null
$workshopAliasFixture = Join-Path $verifyFixturesRoot "classifier-workshop-alias-fixture"
$workshopAliasGameDir = Join-Path $workshopAliasFixture "steamapps\common\Slay the Spire 2"
$workshopAliasModsDir = Join-Path $workshopAliasGameDir "mods"
$workshopAliasContentDir = Join-Path $workshopAliasFixture "steamapps\workshop\content\2868840"
New-Item -ItemType Directory -Force -Path $workshopAliasModsDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workshopAliasContentDir "1234567890\WorkshopAliasBase") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workshopAliasModsDir "WorkshopAliasDependent") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workshopAliasModsDir "WorkshopObjectAliasDependent") | Out-Null
Set-Content -LiteralPath (Join-Path $workshopAliasContentDir "1234567890\WorkshopAliasBase\WorkshopAliasBase.json") -Value '{"id":"ClassifierWorkshopAliasBase","name":"Classifier Workshop Alias Base","version":"0.0.0","has_dll":true,"mod_type":"framework"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $workshopAliasModsDir "WorkshopAliasDependent\WorkshopAliasDependent.json") -Value '{"id":"ClassifierWorkshopAliasDependent","name":"Classifier Workshop Alias Dependent","version":"0.0.0","has_dll":false,"has_pck":false,"affects_gameplay":false,"hot_apply":true,"dependencies":["1234567890"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $workshopAliasModsDir "WorkshopObjectAliasDependent\WorkshopObjectAliasDependent.json") -Value '{"id":"ClassifierWorkshopObjectAliasDependent","name":"Classifier Workshop Object Alias Dependent","version":"0.0.0","has_dll":false,"has_pck":false,"affects_gameplay":false,"hot_apply":true,"dependencies":[{"publishedFileId":"1234567890"}]}' -Encoding UTF8
$workshopAliasClassify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $workshopAliasGameDir
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 workshop alias fixture failed"
}
if (-not ($workshopAliasClassify -match "Classifier Workshop Alias Dependent \[ClassifierWorkshopAliasDependent\] :: depends on restart-required mod: ClassifierWorkshopAliasBase")) {
    Fail "Classifier did not resolve unique workshop folder dependency alias"
}
if (-not ($workshopAliasClassify -match "Classifier Workshop Object Alias Dependent \[ClassifierWorkshopObjectAliasDependent\] :: depends on restart-required mod: ClassifierWorkshopAliasBase")) {
    Fail "Classifier did not resolve workshop object dependency alias"
}
if (Test-Path -LiteralPath $workshopAliasFixture) {
    $resolvedFixture = (Resolve-Path -LiteralPath $workshopAliasFixture).Path
    $resolvedVerifyFixtures = (Resolve-Path -LiteralPath (Join-Path $root "dist\verify-fixtures")).Path
    if (-not $resolvedFixture.StartsWith($resolvedVerifyFixtures, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean classifier workshop alias fixture outside verify fixtures: $resolvedFixture"
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}

$launcherLog = Join-Path $clean "ModTheSpire2Data\launcher.log"
$launcherData = Join-Path $clean "ModTheSpire2Data"
$loadOrder = Join-Path $launcherData "load-order.txt"
if (Test-Path -LiteralPath $launcherData) {
    $resolvedRuntime = (Resolve-Path -LiteralPath $launcherData).Path
    $resolvedClean = (Resolve-Path -LiteralPath $clean).Path
    if (-not $resolvedRuntime.StartsWith($resolvedClean, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean runtime data outside clean package: $resolvedRuntime"
    }
    Remove-Item -LiteralPath $resolvedRuntime -Recurse -Force
}
$beforeLogLength = if (Test-Path -LiteralPath $launcherLog) { (Get-Item -LiteralPath $launcherLog).Length } else { 0 }
$process = Start-Process -FilePath $launcher -ArgumentList @("--diagnose", "--", $quotedGameExe) -WorkingDirectory $clean -PassThru
$diagnosticComplete = $false
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 1
    if (Test-Path -LiteralPath $launcherLog) {
        $log = Get-Content -LiteralPath $launcherLog -Raw
        if ($log.Length -gt $beforeLogLength -and $log.Contains("Diagnostic scan end")) {
            $diagnosticComplete = $true
            break
        }
    }
    if ($process.HasExited) {
        break
    }
}
if (-not $diagnosticComplete) {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    Fail "Launcher diagnostics did not finish within timeout"
}
if (-not $process.HasExited) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

$process = Start-Process -FilePath $launcher -ArgumentList @("--self-test-order", "--", $quotedGameExe) -WorkingDirectory $clean -PassThru -Wait
if ($process.ExitCode -ne 0) {
    $selfTestLog = if (Test-Path -LiteralPath $launcherLog) { Get-Content -LiteralPath $launcherLog -Tail 120 } else { "" }
    Fail "Launcher order self-test failed with exit code $($process.ExitCode). Last log lines: $($selfTestLog -join ' | ')"
}
$selfTestLogRaw = Get-Content -LiteralPath $launcherLog -Raw
if (-not $selfTestLogRaw.Contains("Order self-test passed")) {
    Fail "Launcher order self-test log did not report success"
}
if (-not $selfTestLogRaw.Contains("Data-file backup self-test passed")) {
    Fail "Launcher order self-test did not verify data-file backups"
}
if (-not $selfTestLogRaw.Contains("Order self-test numeric dependency repair passed")) {
    Fail "Launcher order self-test did not verify numeric order dependency repair"
}

$process = Start-Process -FilePath $launcher -ArgumentList @("--self-test-settings", "--", $quotedGameExe) -WorkingDirectory $clean -PassThru -Wait
if ($process.ExitCode -ne 0) {
    $settingsSelfTestLog = if (Test-Path -LiteralPath $launcherLog) { Get-Content -LiteralPath $launcherLog -Tail 120 } else { "" }
    Fail "Launcher settings self-test failed with exit code $($process.ExitCode). Last log lines: $($settingsSelfTestLog -join ' | ')"
}
$settingsSelfTestLogRaw = Get-Content -LiteralPath $launcherLog -Raw
if (-not $settingsSelfTestLogRaw.Contains("Settings self-test passed")) {
    Fail "Launcher settings self-test log did not report success"
}

$diagnosticLog = Get-Content -LiteralPath $launcherLog -Raw
if (-not $diagnosticLog.Contains("Diagnostic total mods=")) {
    Fail "Launcher diagnostics log did not include total mod count"
}
if (-not ($diagnosticLog.Contains("id=BaseLib") -and $diagnosticLog.Contains("id=QuickRestart"))) {
    Fail "Launcher diagnostics log did not include BaseLib and QuickRestart"
}

$resolvedClean = (Resolve-Path -LiteralPath $clean).Path
if (Test-Path -LiteralPath $launcherData) {
    $resolvedRuntime = (Resolve-Path -LiteralPath $launcherData).Path
    if (-not $resolvedRuntime.StartsWith($resolvedClean, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean runtime data outside clean package: $resolvedRuntime"
    }
    Remove-Item -LiteralPath $resolvedRuntime -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $launcherData | Out-Null
Set-Content -LiteralPath $loadOrder -Value @("ModTheSpire2", "BaseLib", "QuickRestart") -Encoding UTF8
$process = Start-Process -FilePath $launcher -ArgumentList @("--diagnose", "--", $quotedGameExe) -WorkingDirectory $clean -PassThru
$customOrderComplete = $false
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 1
    if (Test-Path -LiteralPath $launcherLog) {
        $log = Get-Content -LiteralPath $launcherLog -Raw
        if ($log.Contains("Loaded custom order entries=3") -and $log.Contains("Diagnostic mod[0]=ModTheSpire2 id=ModTheSpire2")) {
            $customOrderComplete = $true
            break
        }
    }
    if ($process.HasExited) {
        break
    }
}
if (-not $customOrderComplete) {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    Fail "Launcher diagnostics did not apply custom load-order.txt"
}
if (-not $process.HasExited) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

if (Test-Path -LiteralPath $launcherData) {
    $resolvedRuntime = (Resolve-Path -LiteralPath $launcherData).Path
    $resolvedClean = (Resolve-Path -LiteralPath $clean).Path
    if (-not $resolvedRuntime.StartsWith($resolvedClean, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean runtime data outside clean package: $resolvedRuntime"
    }
    Remove-Item -LiteralPath $resolvedRuntime -Recurse -Force
}

$aliasFixture = Join-Path ([System.IO.Path]::GetTempPath()) ("ModTheSpire2LauncherAliasFixture-" + [System.Guid]::NewGuid().ToString("N"))
$fakeGameDir = Join-Path $aliasFixture "steamapps\common\Slay the Spire 2"
$fakeModsDir = Join-Path $fakeGameDir "mods"
$fakeWorkshopDir = Join-Path $aliasFixture "steamapps\workshop\content\2868840"
$fakeGameExe = Join-Path $fakeGameDir "SlayTheSpire2.exe"
New-Item -ItemType Directory -Force -Path $fakeModsDir | Out-Null
Set-Content -LiteralPath $fakeGameExe -Value "" -Encoding ASCII
Set-Content -LiteralPath (Join-Path $fakeGameDir "release_info.json") -Value '{"version":"v0.107.1","branch":"v0.107.1"}' -Encoding UTF8
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "BaseLib") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "RequiresAlias") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "RequiredModsAlias") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "SingleObjectDependency") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "OptionalDependency") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "OptionalSingleObjectDependency") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "LoadAfterOnly") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "LoadBeforeOnly") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "InvalidManifestFallback") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "SettingsJsonManifest") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "JsonFalsePositiveGuard") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "MissingDependency") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "PckNameOnly") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "PckNameDuplicateGuard") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "NameAliasBase") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "NameAliasDependent") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "AmbiguousNameAliasOne") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "AmbiguousNameAliasTwo") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "AmbiguousNameAliasDependent") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeWorkshopDir "2345678901\WorkshopAliasBase") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "WorkshopAliasDependent") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "WorkshopObjectAliasDependent") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "SidecarManifestId") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "SidecarVariantsIgnored") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "VersionedFrameworkOld") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "VersionedDependencyTooLow") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "FutureGameVersion") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fakeModsDir "ModTheSpire2\ModTheSpire2Data") | Out-Null
Set-Content -LiteralPath (Join-Path $fakeModsDir "BaseLib\BaseLib.json") -Value '{"id":"BaseLib","name":"BaseLib","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "RequiresAlias\RequiresAlias.json") -Value '{"id":"LauncherRequiresAlias","name":"Launcher Requires Alias","version":"0.0.0","requires":["BaseLib"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "RequiredModsAlias\RequiredModsAlias.json") -Value '{"id":"LauncherRequiredModsAlias","name":"Launcher RequiredMods Alias","version":"0.0.0","requiredMods":[{"modId":"BaseLib"}]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "SingleObjectDependency\SingleObjectDependency.json") -Value '{"id":"LauncherSingleObjectDependency","name":"Launcher Single Object Dependency","version":"0.0.0","requires":{"modId":"BaseLib"}}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "OptionalDependency\OptionalDependency.json") -Value '{"id":"LauncherOptionalDependency","name":"Launcher Optional Dependency","version":"0.0.0","dependencies":[{"id":"LauncherOptionalMissingDependency","optional":true}]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "OptionalSingleObjectDependency\OptionalSingleObjectDependency.json") -Value '{"id":"LauncherOptionalSingleObjectDependency","name":"Launcher Optional Single Object Dependency","version":"0.0.0","requires":{"id":"LauncherOptionalSingleMissingDependency","required":false}}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "LoadAfterOnly\LoadAfterOnly.json") -Value '{"id":"LauncherLoadAfterOnly","name":"Launcher Load After Only","version":"0.0.0","load_after":["BaseLib"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "LoadBeforeOnly\LoadBeforeOnly.json") -Value '{"id":"LauncherLoadBeforeOnly","name":"Launcher Load Before Only","version":"0.0.0","load_before":["LauncherLoadAfterOnly"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "InvalidManifestFallback\LauncherInvalidManifestFallback.json") -Value @'
{
  "id": "LauncherInvalidManifestFallback",
  "name": "Launcher Invalid Manifest Fallback",
  "version": "0.0.0",
  "description": "broken string,
  "has_dll": true
}
'@ -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "InvalidManifestFallback\LauncherInvalidManifestFallback.dll") -Value "placeholder" -Encoding ASCII
Set-Content -LiteralPath (Join-Path $fakeModsDir "SettingsJsonManifest\settings.json") -Value '{"id":"LauncherSettingsJsonManifest","name":"Launcher Settings Json Manifest","version":"0.0.0","has_dll":true}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "SettingsJsonManifest\LauncherSettingsJsonManifest.dll") -Value "placeholder" -Encoding ASCII
Set-Content -LiteralPath (Join-Path $fakeModsDir "JsonFalsePositiveGuard\config.json") -Value '{"id":"LauncherJsonFalsePositiveGuard","name":"Launcher Json False Positive Guard","version":"0.0.0","affects_gameplay":false}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "MissingDependency\MissingDependency.json") -Value '{"id":"LauncherMissingDependency","name":"Launcher Missing Dependency","version":"0.0.0","dependencies":["MissingFramework"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "PckNameOnly\mod_manifest.json") -Value '{"pck_name":"LauncherPckNameOnly","name":"Launcher PckName Only","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "PckNameDuplicateGuard\mod_manifest.json") -Value '{"pck_name":"LauncherPckNameDuplicateSidecar","name":"Launcher PckName Duplicate Sidecar","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "PckNameDuplicateGuard\Real.json") -Value '{"id":"LauncherPckNameDuplicateReal","name":"Launcher PckName Duplicate Real","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "NameAliasBase\NameAliasBase.json") -Value '{"id":"LauncherNameAliasBase","name":"Launcher Name Alias Base","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "NameAliasDependent\NameAliasDependent.json") -Value '{"id":"LauncherNameAliasDependent","name":"Launcher Name Alias Dependent","version":"0.0.0","dependencies":["Launcher Name Alias Base"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "AmbiguousNameAliasOne\AmbiguousNameAliasOne.json") -Value '{"id":"LauncherAmbiguousNameAliasOne","name":"Launcher Ambiguous Name Alias","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "AmbiguousNameAliasTwo\AmbiguousNameAliasTwo.json") -Value '{"id":"LauncherAmbiguousNameAliasTwo","name":"Launcher Ambiguous Name Alias","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "AmbiguousNameAliasDependent\AmbiguousNameAliasDependent.json") -Value '{"id":"LauncherAmbiguousNameAliasDependent","name":"Launcher Ambiguous Name Alias Dependent","version":"0.0.0","dependencies":["Launcher Ambiguous Name Alias"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeWorkshopDir "2345678901\WorkshopAliasBase\WorkshopAliasBase.json") -Value '{"id":"LauncherWorkshopAliasBase","name":"Launcher Workshop Alias Base","version":"0.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "WorkshopAliasDependent\WorkshopAliasDependent.json") -Value '{"id":"LauncherWorkshopAliasDependent","name":"Launcher Workshop Alias Dependent","version":"0.0.0","dependencies":["2345678901"]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "WorkshopObjectAliasDependent\WorkshopObjectAliasDependent.json") -Value '{"id":"LauncherWorkshopObjectAliasDependent","name":"Launcher Workshop Object Alias Dependent","version":"0.0.0","dependencies":[{"workshopId":"2345678901"}]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "SidecarManifestId\SidecarManifestId.manifest") -Value '{"id":"LauncherSidecarManifestId","name":"Launcher Sidecar Manifest Id","version":"0.0.0","affects_gameplay":false}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "SidecarVariantsIgnored\variants.manifest") -Value '{"schema":1,"variants":[{"compatTarget":"0.107.1","directory":"lib/0.107.1","assembly":"IgnoredVariant.dll"}]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "VersionedFrameworkOld\VersionedFrameworkOld.json") -Value '{"id":"LauncherVersionedFrameworkOld","name":"Launcher Versioned Framework Old","version":"v1.2.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "VersionedDependencyTooLow\VersionedDependencyTooLow.json") -Value '{"id":"LauncherVersionedDependencyTooLow","name":"Launcher Versioned Dependency Too Low","version":"0.0.0","dependencies":[{"id":"LauncherVersionedFrameworkOld","min_version":"v1.3.0"}]}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "FutureGameVersion\FutureGameVersion.json") -Value '{"id":"LauncherFutureGameVersion","name":"Launcher Future Game Version","version":"0.0.0","min_game_version":"v999.0.0"}' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fakeModsDir "ModTheSpire2\ModTheSpire2Data\GhostLauncherRuntimeDataMod.json") -Value '{"id":"GhostLauncherRuntimeDataMod","name":"Ghost Launcher Runtime Data Mod","version":"0.0.0"}' -Encoding UTF8

$beforeAliasLogLength = if (Test-Path -LiteralPath $launcherLog) { (Get-Item -LiteralPath $launcherLog).Length } else { 0 }
$quotedFakeGameExe = '"' + $fakeGameExe + '"'
$process = Start-Process -FilePath $launcher -ArgumentList @("--diagnose", "--", $quotedFakeGameExe) -WorkingDirectory $clean -PassThru
$aliasDiagnosticComplete = $false
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 1
    if (Test-Path -LiteralPath $launcherLog) {
        $log = Get-Content -LiteralPath $launcherLog -Raw
        if ($log.Length -gt $beforeAliasLogLength -and $log.Contains("Diagnostic scan end")) {
            $aliasDiagnosticComplete = $true
            break
        }
    }
    if ($process.HasExited) {
        break
    }
}
if (-not $aliasDiagnosticComplete) {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    Fail "Launcher alias diagnostics did not finish within timeout"
}
if (-not $process.HasExited) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}
$aliasLog = Get-Content -LiteralPath $launcherLog -Raw
if (-not ($aliasLog.Contains("id=LauncherRequiresAlias") -and $aliasLog.Contains("deps=BaseLib"))) {
    Fail "Launcher diagnostics did not parse requires dependency alias"
}
if (-not ($aliasLog.Contains("id=LauncherRequiredModsAlias") -and $aliasLog.Contains("deps=BaseLib"))) {
    Fail "Launcher diagnostics did not parse requiredMods/modId dependency alias"
}
if (-not ($aliasLog.Contains("id=LauncherSingleObjectDependency") -and $aliasLog.Contains("deps=BaseLib"))) {
    Fail "Launcher diagnostics did not parse single-object dependency fields"
}
if (-not $aliasLog.Contains("id=LauncherOptionalDependency")) {
    Fail "Launcher diagnostics did not discover optional-dependency fixture"
}
if ($aliasLog.Contains("id=LauncherOptionalDependency") -and $aliasLog.Contains("LauncherOptionalMissingDependency")) {
    Fail "Launcher diagnostics incorrectly treated optional dependency array objects as hard dependencies"
}
if (-not $aliasLog.Contains("id=LauncherOptionalSingleObjectDependency")) {
    Fail "Launcher diagnostics did not discover optional single-object dependency fixture"
}
if ($aliasLog.Contains("id=LauncherOptionalSingleObjectDependency") -and $aliasLog.Contains("LauncherOptionalSingleMissingDependency")) {
    Fail "Launcher diagnostics incorrectly treated optional single-object dependency as a hard dependency"
}
if (-not ($aliasLog.Contains("id=LauncherLoadAfterOnly") -and $aliasLog.Contains("deps=loads after: BaseLib"))) {
    Fail "Launcher diagnostics did not expose load_after as an order-only relation"
}
if ($aliasLog.Contains("id=LauncherLoadAfterOnly") -and $aliasLog.Contains("Missing dependency: BaseLib")) {
    Fail "Launcher diagnostics incorrectly treated load_after as a missing hard dependency"
}
if (-not ($aliasLog.Contains("id=LauncherLoadBeforeOnly") -and $aliasLog.Contains("deps=loads before: LauncherLoadAfterOnly"))) {
    Fail "Launcher diagnostics did not expose load_before as an order-only relation"
}
if ($aliasLog.Contains("id=LauncherLoadBeforeOnly") -and $aliasLog.Contains("Missing dependency: LauncherLoadAfterOnly")) {
    Fail "Launcher diagnostics incorrectly treated load_before as a missing hard dependency"
}
if (-not $aliasLog.Contains("id=LauncherInvalidManifestFallback")) {
    Fail "Launcher diagnostics did not discover malformed JSON manifest with a valid early id/name"
}
if (-not $aliasLog.Contains("id=LauncherSettingsJsonManifest")) {
    Fail "Launcher diagnostics did not keep payload-backed settings.json manifest compatibility"
}
if ($aliasLog.Contains("LauncherJsonFalsePositiveGuard")) {
    Fail "Launcher diagnostics incorrectly discovered a plain config.json id as a mod"
}
if (-not ($aliasLog.Contains("id=LauncherMissingDependency") -and $aliasLog.Contains("deps=missing: MissingFramework"))) {
    Fail "Launcher diagnostics did not mark missing dependencies visibly"
}
if (-not ($aliasLog.Contains("id=LauncherMissingDependency") -and $aliasLog.Contains("status=Missing dependency: MissingFramework"))) {
    Fail "Launcher diagnostics did not expose missing dependency status"
}
if (-not $aliasLog.Contains("id=LauncherPckNameOnly")) {
    Fail "Launcher diagnostics did not discover pck_name-only manifest fallback"
}
if (-not $aliasLog.Contains("id=LauncherPckNameDuplicateReal")) {
    Fail "Launcher diagnostics did not include real manifest beside pck_name sidecar"
}
if ($aliasLog.Contains("id=LauncherPckNameDuplicateSidecar")) {
    Fail "Launcher diagnostics incorrectly created duplicate mod from pck_name sidecar beside id manifest"
}
if (-not ($aliasLog.Contains("id=LauncherNameAliasDependent") -and $aliasLog.Contains("deps=LauncherNameAliasBase"))) {
    Fail "Launcher diagnostics did not resolve unique dependency display-name alias"
}
if (-not ($aliasLog.Contains("id=LauncherAmbiguousNameAliasDependent") -and $aliasLog.Contains("deps=missing: Launcher Ambiguous Name Alias"))) {
    Fail "Launcher diagnostics incorrectly resolved or ignored ambiguous dependency display-name alias"
}
if (-not ($aliasLog.Contains("id=LauncherWorkshopAliasDependent") -and $aliasLog.Contains("deps=LauncherWorkshopAliasBase"))) {
    Fail "Launcher diagnostics did not resolve unique workshop folder dependency alias"
}
if (-not ($aliasLog.Contains("id=LauncherWorkshopObjectAliasDependent") -and $aliasLog.Contains("deps=LauncherWorkshopAliasBase"))) {
    Fail "Launcher diagnostics did not resolve workshop object dependency alias"
}
if (-not ($aliasLog.Contains("id=LauncherVersionedDependencyTooLow") -and $aliasLog.Contains("deps=LauncherVersionedFrameworkOld >= v1.3.0"))) {
    Fail "Launcher diagnostics did not display dependency minimum-version requirement"
}
if (-not ($aliasLog.Contains("id=LauncherVersionedDependencyTooLow") -and $aliasLog.Contains("status=Dependency too old: LauncherVersionedFrameworkOld needs v1.3.0, found v1.2.0"))) {
    Fail "Launcher diagnostics did not expose too-low dependency version status"
}
if (-not ($aliasLog.Contains("id=LauncherFutureGameVersion") -and $aliasLog.Contains("minGame=v999.0.0"))) {
    Fail "Launcher diagnostics did not expose minimum game version"
}
if (-not ($aliasLog.Contains("id=LauncherFutureGameVersion") -and $aliasLog.Contains("deps=requires STS2 >= v999.0.0"))) {
    Fail "Launcher diagnostics did not show minimum game version in dependency/details text"
}
if (-not ($aliasLog.Contains("id=LauncherFutureGameVersion") -and $aliasLog.Contains("status=Game too old: needs v999.0.0, found v0.107.1"))) {
    Fail "Launcher diagnostics did not expose too-low game version status"
}
if (-not $aliasLog.Contains("id=LauncherSidecarManifestId")) {
    Fail "Launcher diagnostics did not discover explicit-id .manifest mod files"
}
if ($aliasLog.Contains("IgnoredVariant")) {
    Fail "Launcher diagnostics incorrectly discovered id-less sidecar variants manifest as a mod"
}
if ($aliasLog.Contains("GhostLauncherRuntimeDataMod")) {
    Fail "Launcher diagnostics scanned ModTheSpire2Data runtime files as mods"
}

$process = Start-Process -FilePath $launcher -ArgumentList @("--self-test-order", "--", $quotedFakeGameExe) -WorkingDirectory $clean -PassThru -Wait
if ($process.ExitCode -ne 0) {
    $aliasSelfTestLog = if (Test-Path -LiteralPath $launcherLog) { Get-Content -LiteralPath $launcherLog -Tail 120 } else { "" }
    Fail "Launcher alias/missing dependency self-test failed with exit code $($process.ExitCode). Last log lines: $($aliasSelfTestLog -join ' | ')"
}
$aliasSelfTestLogRaw = Get-Content -LiteralPath $launcherLog -Raw
if (-not $aliasSelfTestLogRaw.Contains("Order self-test pruned missing dependency selection")) {
    Fail "Launcher self-test did not prune saved/profile selection with missing dependency"
}
if (-not $aliasSelfTestLogRaw.Contains("Order self-test load_after repair passed")) {
    Fail "Launcher self-test did not verify load_after order repair"
}
if (-not $aliasSelfTestLogRaw.Contains("Order self-test load_before repair passed")) {
    Fail "Launcher self-test did not verify load_before order repair"
}

if (Test-Path -LiteralPath $aliasFixture) {
    $resolvedFixture = (Resolve-Path -LiteralPath $aliasFixture).Path
    $resolvedTemp = (Resolve-Path -LiteralPath ([System.IO.Path]::GetTempPath())).Path
    $fixtureName = Split-Path -Leaf $resolvedFixture
    if (-not $resolvedFixture.StartsWith($resolvedTemp, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $fixtureName.StartsWith("ModTheSpire2LauncherAliasFixture-", [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean unexpected alias fixture path: $resolvedFixture"
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}

if (Test-Path -LiteralPath $launcherData) {
    $resolvedRuntime = (Resolve-Path -LiteralPath $launcherData).Path
    $resolvedClean = (Resolve-Path -LiteralPath $clean).Path
    if (-not $resolvedRuntime.StartsWith($resolvedClean, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean runtime data outside clean package after alias diagnostics: $resolvedRuntime"
    }
    Remove-Item -LiteralPath $resolvedRuntime -Recurse -Force
}

$cleanSize = (Get-ChildItem -LiteralPath $clean -File | Measure-Object Length -Sum).Sum
[pscustomobject]@{
    Status = "OK"
    Version = $manifest.version
    CleanPackageKB = [math]::Round($cleanSize / 1KB, 2)
    DllSha256 = Get-Sha (Join-Path $clean "ModTheSpire2.dll")
    Files = ($expectedSorted -join ", ")
}
