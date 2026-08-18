param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$OutputDir = "dist\build-temp\refactor-services"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$output = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $root $OutputDir }
$gameExe = Join-Path $GameDir "SlayTheSpire2.exe"

function Fail([string]$message) {
    throw "REFACTOR VERIFY FAILED: $message"
}

$required = @(
    "ModTheSpire2Companion\CompanionServices.cs",
    "ModTheSpire2Companion\ModTheSpire2Entry.HotManage.cs",
    "NativeLauncher\LauncherServices.h",
    "NativeLauncher\ModTheSpire2Launcher.c",
    "Tools\BuildModTheSpire2.ps1",
    "REFACTOR_ARCHITECTURE.md"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative))) {
        Fail "Missing refactor file: $relative"
    }
}

$companionContracts = Get-Content -LiteralPath (Join-Path $root "ModTheSpire2Companion\CompanionServices.cs") -Raw
foreach ($contract in @(
    "ICompanionPaths",
    "ICompanionLogSink",
    "ILauncherGateway",
    "IGameSessionProbe",
    "IModCatalog",
    "ILoadOrderService",
    "IRuntimeModStateProvider",
    "IGameCompatibilityInspector",
    "AssemblyInfo.ModMap"
)) {
    if (-not $companionContracts.Contains($contract)) {
        Fail "Missing companion contract or official compatibility adapter: $contract"
    }
}

$launcherContracts = Get-Content -LiteralPath (Join-Path $root "NativeLauncher\LauncherServices.h") -Raw
foreach ($contract in @(
    "Mts2ModDiscoveryService",
    "Mts2LoadOrderService",
    "Mts2GroupingService",
    "Mts2SettingsService",
    "Mts2LaunchService"
)) {
    if (-not $launcherContracts.Contains($contract)) {
        Fail "Missing native launcher service contract: $contract"
    }
}

$launcherSource = Get-Content -LiteralPath (Join-Path $root "NativeLauncher\ModTheSpire2Launcher.c") -Raw
foreach ($behavior in @(
    "ShouldReplaceDiscoveredMod",
    "Workshop mod supersedes older local mod",
    "affectsGameplayKnown",
    "Discovery compatibility self-test passed",
    "g_services.settings.write",
    "g_services.launch.startGame"
)) {
    if (-not $launcherSource.Contains($behavior)) {
        Fail "Missing refactored launcher behavior: $behavior"
    }
}

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\BuildModTheSpire2.ps1") -GameDir $GameDir -OutputDir $output
if ($LASTEXITCODE -ne 0) {
    Fail "Build script failed with exit code $LASTEXITCODE"
}

$launcher = Join-Path $output "ModTheSpire2Launcher.exe"
$companion = Join-Path $output "ModTheSpire2.dll"
if (-not (Test-Path -LiteralPath $launcher) -or -not (Test-Path -LiteralPath $companion)) {
    Fail "Build products are incomplete"
}
if (-not (Test-Path -LiteralPath $gameExe)) {
    Fail "Game executable was not found: $gameExe"
}

$quotedGameExe = '"' + $gameExe + '"'
foreach ($test in @("--self-test-settings", "--self-test-order")) {
    $process = Start-Process -FilePath $launcher -ArgumentList @($test, "--", $quotedGameExe) -WorkingDirectory $output -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Fail "$test failed with exit code $($process.ExitCode)"
    }
}

$log = Join-Path $output "ModTheSpire2Data\launcher.log"
if (-not (Test-Path -LiteralPath $log)) {
    Fail "Launcher self-test log was not created"
}
$logText = Get-Content -LiteralPath $log -Raw
foreach ($marker in @(
    "Settings self-test passed",
    "Discovery compatibility self-test passed",
    "Order self-test passed"
)) {
    if (-not $logText.Contains($marker)) {
        Fail "Launcher self-test marker is missing: $marker"
    }
}

[pscustomobject]@{
    Status = "OK"
    GameVersion = if (Test-Path -LiteralPath (Join-Path $GameDir "release_info.json")) {
        (Get-Content -LiteralPath (Join-Path $GameDir "release_info.json") -Raw | ConvertFrom-Json).version
    } else {
        "unknown"
    }
    CompanionSha256 = (Get-FileHash -LiteralPath $companion -Algorithm SHA256).Hash
    LauncherSha256 = (Get-FileHash -LiteralPath $launcher -Algorithm SHA256).Hash
    OutputDir = $output
}
