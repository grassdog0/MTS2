param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$clean = Join-Path $root "dist\WorkshopUpload\ModTheSpire2Content-Clean"
$modDir = Join-Path $GameDir "mods\ModTheSpire2"
$dataDir = Join-Path $modDir "ModTheSpire2Data"
$backupDir = Join-Path $dataDir "manual-test-log-backups"

function Fail([string]$message) {
    throw "PREPARE MANUAL TEST FAILED: $message"
}

if (Get-Process | Where-Object { $_.ProcessName -match 'Slay|Spire|ModTheSpire2Launcher' }) {
    Fail "Slay the Spire 2 or ModTheSpire2Launcher appears to be running. Fully exit it before preparing a clean manual-test log."
}

foreach ($path in @($clean, $modDir)) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing path: $path"
    }
}

$expectedFiles = @(
    "ModTheSpire2.dll",
    "ModTheSpire2.json",
    "ModTheSpire2.pck",
    "ModTheSpire2Launcher.exe",
    "README.md"
)

foreach ($file in $expectedFiles) {
    $cleanFile = Join-Path $clean $file
    $liveFile = Join-Path $modDir $file
    if (-not (Test-Path -LiteralPath $cleanFile)) {
        Fail "Missing clean package file: $cleanFile"
    }
    if (-not (Test-Path -LiteralPath $liveFile)) {
        Fail "Missing live package file: $liveFile"
    }
    $cleanHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $cleanFile).Hash
    $liveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $liveFile).Hash
    if ($cleanHash -ne $liveHash) {
        Fail "$file differs between clean package and live folder"
    }
}

New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backedUp = @()
foreach ($logName in @("companion.log", "launcher.log")) {
    $logPath = Join-Path $dataDir $logName
    if (Test-Path -LiteralPath $logPath) {
        $backupPath = Join-Path $backupDir ($logName + "." + $stamp + ".bak")
        Move-Item -LiteralPath $logPath -Destination $backupPath -Force
        $backedUp += $backupPath
    }
}

$marker = Join-Path $dataDir "manual-test-ready.txt"
Set-Content -LiteralPath $marker -Encoding UTF8 -Value @"
Prepared at: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Expected companion marker: Initialize ModTheSpire2 0.4.0-clean-restart-ui

Next steps:
1. Start Slay the Spire 2 from Steam or through the configured Steam launch option.
2. Open Settings / General / Mod Settings.
3. Open ModTheSpire2 Launcher management.
4. Test top-right X and/or Close and Open Launcher.
5. Run Tools\CheckCleanRestartManualEvidence.ps1.
"@

[pscustomobject]@{
    Status = "OK"
    LiveModDir = $modDir
    BackedUpLogs = ($backedUp -join " | ")
    Marker = $marker
    NextAction = "Start the game, open ModTheSpire2 management, then run Tools\CheckCleanRestartManualEvidence.ps1."
}
