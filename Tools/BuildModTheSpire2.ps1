param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$OutputDir = "dist\build-temp\refactor-services",
    [switch]$SkipCompanion,
    [switch]$SkipLauncher
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$output = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $root $OutputDir }
$managed = Join-Path $GameDir "data_sts2_windows_x86_64"

function Fail([string]$message) {
    throw "BUILD FAILED: $message"
}

if (-not (Test-Path -LiteralPath $managed)) {
    Fail "Managed game directory was not found: $managed"
}

New-Item -ItemType Directory -Force -Path $output | Out-Null
$built = @()

if (-not $SkipCompanion) {
    $csc = Get-ChildItem -Path (Join-Path $env:ProgramFiles "dotnet\sdk") -Filter csc.dll -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]Roslyn[\\/]bincore[\\/]csc\.dll$' } |
        Sort-Object { try { [version]$_.Directory.Parent.Parent.Name } catch { [version]"0.0" } } -Descending |
        Select-Object -First 1
    if (-not $csc) {
        Fail "Roslyn csc.dll was not found under Program Files\dotnet\sdk."
    }

    $references = Get-ChildItem -LiteralPath $managed -Filter "*.dll" | Where-Object {
        try {
            [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
            $true
        } catch {
            $false
        }
    } | ForEach-Object { "/reference:" + $_.FullName }

    $sources = @(
        Join-Path $root "ModTheSpire2Companion\CompanionServices.cs"
        Join-Path $root "ModTheSpire2Companion\ModTheSpire2Entry.HotManage.cs"
    )
    foreach ($source in $sources) {
        if (-not (Test-Path -LiteralPath $source)) {
            Fail "Companion source was not found: $source"
        }
    }

    $companionOutput = Join-Path $output "ModTheSpire2.dll"
    $compilerArguments = @(
        $csc.FullName,
        "/noconfig",
        "/nostdlib+",
        "/target:library",
        "/langversion:preview",
        "/nullable:enable",
        "/optimize+",
        "/debug-",
        "/out:$companionOutput"
    ) + $references + $sources
    & dotnet @compilerArguments
    if ($LASTEXITCODE -ne 0) {
        Fail "Companion compilation failed with exit code $LASTEXITCODE."
    }
    $built += $companionOutput
}

if (-not $SkipLauncher) {
    $compiler = Get-Command x86_64-w64-mingw32-gcc, gcc -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $compiler) {
        Fail "A MinGW GCC compiler was not found on PATH."
    }

    $launcherSource = Join-Path $root "NativeLauncher\ModTheSpire2Launcher.c"
    $launcherOutput = Join-Path $output "ModTheSpire2Launcher.exe"
    & $compiler.Source $launcherSource -municode -mwindows -O2 -Wall -Wextra -o $launcherOutput -lcomctl32 -lshell32 -lole32 -luuid -luxtheme
    if ($LASTEXITCODE -ne 0) {
        Fail "Launcher compilation failed with exit code $LASTEXITCODE."
    }
    $built += $launcherOutput
}

$built | ForEach-Object {
    $item = Get-Item -LiteralPath $_
    [pscustomobject]@{
        File = $item.FullName
        Size = $item.Length
        SHA256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
    }
}
