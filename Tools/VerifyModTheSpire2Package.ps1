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
if (-not $SkipLive) {
    Assert-Exists $live
}

$expectedFiles = @(
    "ModTheSpire2.dll",
    "ModTheSpire2.json",
    "ModTheSpire2.pck",
    "ModTheSpire2Launcher.exe",
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

$classify = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "Tools\ClassifyModsForHotApply.ps1") -GameDir $GameDir
if ($LASTEXITCODE -ne 0) {
    Fail "ClassifyModsForHotApply.ps1 failed"
}
if (-not ($classify -match "ModTheSpire2 \[ModTheSpire2\] :: launcher/UI patch requires restart")) {
    Fail "Classifier output did not keep ModTheSpire2 restart-required"
}
if (-not ($classify -match "ModTheSpire2 Hot Config Test \[ModTheSpire2HotConfigTest\]")) {
    Fail "Classifier output did not include controlled config-only hot candidate"
}
if (-not ($classify -match "Quick Restart \[QuickRestart\] :: DLL/PCK")) {
    Fail "Classifier output did not keep QuickRestart restart-required"
}
if (-not ($classify -match "Act 4 Heart \[Act4Heart\] :: DLL/PCK")) {
    Fail "Classifier output did not include Act4Heart from mod_manifest.json"
}
if (-not ($classify -match "ModTheSpire2 Hot Depends Restart \[ModTheSpire2HotDependsRestart\] :: depends on restart-required mod: BaseLib")) {
    Fail "Classifier output did not downgrade hot-declared mod with restart-required dependency"
}
if (-not ($classify -match "ModTheSpire2 Hot Depends Chain \[ModTheSpire2HotDependsChain\] :: depends on restart-required mod: ModTheSpire2HotDependsRestart")) {
    Fail "Classifier output did not downgrade transitive hot dependency chain"
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

$cleanSize = (Get-ChildItem -LiteralPath $clean -File | Measure-Object Length -Sum).Sum
[pscustomobject]@{
    Status = "OK"
    Version = $manifest.version
    CleanPackageKB = [math]::Round($cleanSize / 1KB, 2)
    DllSha256 = Get-Sha (Join-Path $clean "ModTheSpire2.dll")
    Files = ($expectedSorted -join ", ")
}
