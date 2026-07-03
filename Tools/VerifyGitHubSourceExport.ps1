param(
    [string]$SourceExport = "dist\Release\ModTheSpire2-0.4.0-CleanRestart-GitHubSource"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$source = if ([System.IO.Path]::IsPathRooted($SourceExport)) {
    $SourceExport
} else {
    Join-Path $root $SourceExport
}

function Fail([string]$message) {
    throw "SOURCE EXPORT VERIFY FAILED: $message"
}

if (-not (Test-Path -LiteralPath $source)) {
    Fail "Missing source export: $source"
}

$sourceFull = [System.IO.Path]::GetFullPath($source).TrimEnd('\')

$forbiddenNames = @(
    "bin",
    "obj",
    "ModTheSpire2Data",
    "build-temp",
    "snapshots",
    "TestPackages"
)

$forbidden = Get-ChildItem -LiteralPath $source -Recurse -Force | Where-Object {
    $full = [System.IO.Path]::GetFullPath($_.FullName).TrimEnd('\')
    $isAllowedRootGit = $_.PSIsContainer -and $_.Name -eq ".git" -and $full -eq (Join-Path $sourceFull ".git")
    $forbiddenNames -contains $_.Name -or
    ($_.Name -eq ".git" -and -not $isAllowedRootGit) -or
    $_.Name -match '\.(obj|bak|log|tmp)$' -or
    $_.Name -match '^analysis-.*\.(tsv|txt|json)$'
}

if ($forbidden) {
    Fail ("Forbidden generated/runtime entries found: " + (($forbidden | Select-Object -First 20 -ExpandProperty FullName) -join " | "))
}

$required = @(
    "README.md",
    "GITHUB_README.md",
    "GOAL_CLEAN_RESTART_MANAGER.md",
    "MANUAL_TEST_0.4.0.md",
    "RELEASE_STATUS_CLEAN_RESTART.md",
    "ModTheSpire2Companion\ModTheSpire2Entry.HotManage.cs",
    "NativeLauncher\ModTheSpire2Launcher.c",
    "Tools\CheckCleanRestartManualEvidence.ps1",
    "Tools\PrepareCleanRestartManualTest.ps1",
    "Tools\VerifyModTheSpire2Package.ps1",
    "Tools\VerifyGitHubSourceExport.ps1",
    "dist\WorkshopUpload\ModTheSpire2Content-Clean\ModTheSpire2.dll",
    "dist\WorkshopUpload\ModTheSpire2Content-Clean\ModTheSpire2.json",
    "dist\WorkshopUpload\ModTheSpire2Content-Clean\ModTheSpire2.pck",
    "dist\WorkshopUpload\ModTheSpire2Content-Clean\ModTheSpire2Launcher.exe",
    "dist\WorkshopUpload\ModTheSpire2Content-Clean\ModTheSpire2Launcher.sh",
    "dist\WorkshopUpload\ModTheSpire2Content-Clean\ModTheSpire2Launcher.command",
    "dist\WorkshopUpload\ModTheSpire2Content-Clean\README.md"
)

foreach ($rel in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $rel))) {
        Fail "Missing required source export file: $rel"
    }
}

$uploadDir = Join-Path $source "dist\WorkshopUpload\ModTheSpire2Content-Clean"
$expectedUploadFiles = @(
    "ModTheSpire2.dll",
    "ModTheSpire2.json",
    "ModTheSpire2.pck",
    "ModTheSpire2Launcher.exe",
    "ModTheSpire2Launcher.sh",
    "ModTheSpire2Launcher.command",
    "README.md"
) | Sort-Object
$actualUploadFiles = Get-ChildItem -LiteralPath $uploadDir -File | Select-Object -ExpandProperty Name | Sort-Object
if (@($actualUploadFiles).Count -ne @($expectedUploadFiles).Count) {
    Fail "Workshop upload content file count mismatch in source export: $($actualUploadFiles -join ', ')"
}
for ($i = 0; $i -lt $expectedUploadFiles.Count; $i++) {
    if ($actualUploadFiles[$i] -ne $expectedUploadFiles[$i]) {
        Fail "Workshop upload content files mismatch in source export: $($actualUploadFiles -join ', ')"
    }
}

$readme = Get-Content -LiteralPath (Join-Path $source "README.md") -Raw
if (-not $readme.Contains("Clean in-game restart helper")) {
    Fail "README does not describe the clean restart workflow"
}
if ($readme.Contains("Conservative Hot-Apply") -or $readme.Contains("State-aware Hot-Apply")) {
    Fail "README still advertises Hot-Apply as a current player-facing workflow"
}

$manualTest = Get-Content -LiteralPath (Join-Path $source "MANUAL_TEST_0.4.0.md") -Raw
if (-not ($manualTest.Contains("Current settings.save") -and $manualTest.Contains("Click ``Save``") -and $manualTest.Contains("Startup self-tests: mismatchLinks=True resolver=True"))) {
    Fail "Manual test checklist no longer uses current launcher labels or startup self-test marker"
}
if ($manualTest.Contains("Current Game Settings") -or $manualTest.Contains("Save Order") -or $manualTest.Contains("Save Profile")) {
    Fail "Manual test checklist still contains old launcher labels"
}

$sourceFiles = Get-ChildItem -LiteralPath $source -Recurse -File -Force | Where-Object {
    [System.IO.Path]::GetFullPath($_.FullName) -notlike ((Join-Path $sourceFull ".git") + "\*")
}
$fileCount = ($sourceFiles | Measure-Object).Count
$size = ($sourceFiles | Measure-Object Length -Sum).Sum

[pscustomobject]@{
    Status = "OK"
    SourceExport = $source
    FileCount = $fileCount
    SizeMB = [math]::Round($size / 1MB, 2)
}
