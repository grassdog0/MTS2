param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [switch]$AssumeSafeMainMenuNoRun,
    [switch]$AssumeMainMenuWithUnfinishedRun,
    [switch]$AssumeActiveRun,
    [switch]$SelfTestApplyStateGate,
    [switch]$AssumePartialSignals,
    [string[]]$AssumeEnabledIds = @(),
    [string[]]$AssumeLoadedIds = @(),
    [switch]$ShowState
)

$enabledIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($id in ($AssumeEnabledIds | ForEach-Object { [string]$_ -split "," })) {
    if (-not [string]::IsNullOrWhiteSpace($id)) {
        [void]$enabledIds.Add($id.Trim())
    }
}

$loadedIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($id in ($AssumeLoadedIds | ForEach-Object { [string]$_ -split "," })) {
    if (-not [string]::IsNullOrWhiteSpace($id)) {
        [void]$loadedIds.Add($id.Trim())
    }
}

$simulatedMainMenu = [bool]$AssumeSafeMainMenuNoRun -or [bool]$AssumeMainMenuWithUnfinishedRun
$simulatedActiveRun = [bool]$AssumeActiveRun
$simulatedUnfinishedRun = [bool]$AssumeMainMenuWithUnfinishedRun -or [bool]$AssumeActiveRun
$simulatedSignalsReliable = -not [bool]$AssumePartialSignals
$simulatedSafeMainMenuNoRun = $simulatedSignalsReliable -and $simulatedMainMenu -and (-not $simulatedActiveRun) -and (-not $simulatedUnfinishedRun)

$knownMainMenuContentMods = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
# WuWa Ancients has a main-menu runtime setting, but toggling the loaded DLL/PCK mod itself is still restart-required.

function Get-CurrentGameVersion([string]$GameDir) {
    $releaseInfo = Join-Path $GameDir "release_info.json"
    if (-not (Test-Path -LiteralPath $releaseInfo)) { return "" }
    try {
        $json = Get-Content -LiteralPath $releaseInfo -Raw | ConvertFrom-Json -ErrorAction Stop
        if ($json.version) { return [string]$json.version }
        if ($json.branch) { return [string]$json.branch }
    } catch {
    }
    return ""
}

function Test-ApplyStateSafetyGate($Candidates, [System.Collections.Generic.HashSet[string]]$DesiredEnabledIds, [bool]$LatestSafeMainMenuNoRun, [bool]$LatestActiveRun, [bool]$LatestUnfinishedRun) {
    if ($LatestActiveRun) {
        foreach ($mod in ($Candidates | Where-Object { $_.Scope -eq "runtime" -and -not $_.RunSafeRuntime })) {
            $currentEnabled = [bool]$mod.Enabled
            $desiredEnabled = $DesiredEnabledIds.Contains($mod.Id)
            if ($currentEnabled -ne $desiredEnabled) {
                return "Cannot change $($mod.Name) [$($mod.Id)]: runtime/config changes require a run-safe manifest token while a run is active."
            }
        }
    }

    foreach ($mod in ($Candidates | Where-Object { $_.Scope -eq "main_menu_no_run" })) {
        $currentEnabled = [bool]$mod.Enabled
        $desiredEnabled = $DesiredEnabledIds.Contains($mod.Id)
        if ($currentEnabled -eq $desiredEnabled) { continue }
        if ($LatestSafeMainMenuNoRun) { continue }

        if ($currentEnabled -and (-not $desiredEnabled)) {
            if ($LatestActiveRun) {
                return "Cannot disable $($mod.Name) [$($mod.Id)]: an active run may reference this content."
            }
            if ($LatestUnfinishedRun) {
                return "Cannot disable $($mod.Name) [$($mod.Id)]: an unfinished run may reference this content."
            }
            return "Cannot disable $($mod.Name) [$($mod.Id)]: the current screen is not a confirmed safe main menu."
        }

        return "Cannot change $($mod.Name) [$($mod.Id)]: main-menu content changes require a confirmed safe main menu with no unfinished run."
    }
    return ""
}

function Test-MainMenuOnlyToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return @(
        "main-menu-no-run",
        "future-run",
        "future-run-content",
        "future-run-generation",
        "new-run-content",
        "run-generation",
        "event",
        "events",
        "character",
        "characters",
        "relic",
        "relics",
        "card",
        "cards",
        "encounter",
        "encounters",
        "ancient",
        "ancients",
        "ancient-choices"
    ) -contains $normalized.ToLowerInvariant()
}

function Test-RuntimeSafeToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return @(
        "runtime",
        "runtime-safe",
        "settings",
        "config",
        "configuration",
        "utility"
    ) -contains $normalized.ToLowerInvariant()
}

function Test-RunSafeRuntimeToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return @(
        "run-safe",
        "during-run",
        "active-run-safe",
        "combat-safe"
    ) -contains $normalized.ToLowerInvariant()
}

function Test-RestartRequiredToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return @(
        "framework",
        "library",
        "shared-library",
        "dependency-root",
        "api",
        "core-patch",
        "ui-patch",
        "startup-patch",
        "harmony-patch",
        "active-run-patch",
        "run-patch",
        "combat-patch",
        "save-patch",
        "save-affecting",
        "save-data",
        "serializer",
        "save-serializer",
        "runtime-service"
    ) -contains $normalized.ToLowerInvariant()
}

function Test-FrameworkRootToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return @(
        "framework",
        "library",
        "shared-library",
        "dependency-root",
        "api",
        "runtime-service"
    ) -contains $normalized.ToLowerInvariant()
}

function Test-ManifestToken($Json, [scriptblock]$Predicate) {
    foreach ($field in @(
        "hot_apply_content",
        "hot_reload_content",
        "content_type",
        "content_types",
        "content_tags",
        "mod_type",
        "mod_types",
        "tags",
        "categories"
    )) {
        foreach ($value in @($Json.$field)) {
            if ($null -ne $value -and (& $Predicate ([string]$value))) {
                return $true
            }
        }
    }
    return $false
}

function Test-MainMenuOnlyManifest($Json, [string]$Scope) {
    if (Test-MainMenuOnlyToken $Scope) { return $true }
    return Test-ManifestToken $Json ${function:Test-MainMenuOnlyToken}
}

function Test-RuntimeSafeManifest($Json, [string]$Scope) {
    if (Test-RuntimeSafeToken $Scope) { return $true }
    return Test-ManifestToken $Json ${function:Test-RuntimeSafeToken}
}

function Test-RunSafeRuntimeManifest($Json, [string]$Scope) {
    if (Test-RunSafeRuntimeToken $Scope) { return $true }
    return Test-ManifestToken $Json ${function:Test-RunSafeRuntimeToken}
}

function Test-RestartRequiredManifest($Json) {
    return Test-ManifestToken $Json ${function:Test-RestartRequiredToken}
}

function Test-SaveSerializerToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return $normalized.ToLowerInvariant() -eq "save-serializer"
}

function Test-ActiveRunPatchToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return @(
        "active-run-patch",
        "run-patch",
        "combat-patch"
    ) -contains $normalized.ToLowerInvariant()
}

function Test-SaveAffectingToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = $Value.Trim().Replace("_", "-").Replace(" ", "-")
    return @(
        "save-patch",
        "save-affecting",
        "save-data",
        "serializer"
    ) -contains $normalized.ToLowerInvariant()
}

function Get-RestartRequiredManifestReason($Json) {
    if (Test-ManifestToken $Json ${function:Test-SaveSerializerToken}) {
        return "save serializer; restart required"
    }
    if (Test-ManifestToken $Json ${function:Test-SaveAffectingToken}) {
        return "save-affecting patch; restart required"
    }
    if (Test-ManifestToken $Json ${function:Test-ActiveRunPatchToken}) {
        return "active-run patch; restart required"
    }
    if (Test-RestartRequiredManifest $Json) {
        return "startup/core/UI patch; restart required"
    }
    return ""
}

function Test-FrameworkRootManifest($Json) {
    return Test-ManifestToken $Json ${function:Test-FrameworkRootToken}
}

function Get-PckNameFallbackId($Json, [string]$Path) {
    if (-not $Json.pck_name) { return "" }
    $dir = Split-Path $Path -Parent
    foreach ($other in Get-ChildItem -LiteralPath $dir -File -ErrorAction SilentlyContinue | Where-Object { Test-ManifestCandidatePath $_.FullName }) {
        if ($other.FullName -eq $Path) { continue }
        try {
            $otherJson = Get-Content -LiteralPath $other.FullName -Raw | ConvertFrom-Json -ErrorAction Stop
            if ($otherJson.id) { return "" }
        } catch {
            return ""
        }
    }
    return [string]$Json.pck_name
}

function Test-ManifestCandidatePath([string]$Path) {
    $ext = [System.IO.Path]::GetExtension($Path)
    return $ext -ieq ".json" -or $ext -ieq ".manifest"
}

function Test-PlausibleManifestCandidate($Json, [string]$Path, [string]$ModId, [string]$Dir) {
    $ext = [System.IO.Path]::GetExtension($Path)
    if ($ext -ieq ".manifest") { return $true }

    $fileName = [System.IO.Path]::GetFileName($Path)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $parentName = Split-Path $Dir -Leaf
    if ($fileName -ieq "mod_manifest.json" -or $fileName -ieq "mod_mainfest.json") { return $true }
    if ($stem -ieq $parentName) { return $true }
    if ($stem -ieq $ModId) { return $true }
    if ($Json.pck_name -and $stem -ieq ([string]$Json.pck_name)) { return $true }
    if ([bool]$Json.has_dll -or [bool]$Json.has_pck) { return $true }
    if (Test-DirectoryHasSidecarManifest $Path $Dir) { return $true }
    if ((Test-PayloadFileInModDir $Dir "*.dll") -or (Test-PayloadFileInModDir $Dir "*.pck")) { return $true }
    return $false
}

function Test-DirectoryHasSidecarManifest([string]$Path, [string]$Dir) {
    if ([string]::IsNullOrWhiteSpace($Dir) -or -not (Test-Path -LiteralPath $Dir)) { return $false }
    foreach ($other in Get-ChildItem -LiteralPath $Dir -File -ErrorAction SilentlyContinue) {
        if ($other.FullName -eq $Path) { continue }
        if ($other.Name -ieq "mod_manifest.json" -or $other.Name -ieq "mod_mainfest.json" -or $other.Extension -ieq ".manifest") {
            return $true
        }
    }
    return $false
}

function Test-PayloadFileInModDir([string]$Dir, [string]$Filter) {
    if ([string]::IsNullOrWhiteSpace($Dir) -or -not (Test-Path -LiteralPath $Dir)) {
        return $false
    }
    return [bool](Get-ChildItem -LiteralPath $Dir -Recurse -File -Filter $Filter -ErrorAction SilentlyContinue | Where-Object { -not (Test-RuntimeDataPath $_.FullName) } | Select-Object -First 1)
}

function Test-OptionalDependencyObject($Dependency) {
    if ($Dependency -is [string] -or $null -eq $Dependency) {
        return $false
    }
    if ($Dependency.optional -eq $true -or $Dependency.is_optional -eq $true -or $Dependency.isOptional -eq $true) {
        return $true
    }
    return $Dependency.required -eq $false
}

function Get-DependencyId($Dependency) {
    if ($Dependency -is [string]) { return [string]$Dependency }
    if ($null -eq $Dependency) { return "" }
    foreach ($field in @(
        "id",
        "mod_id",
        "modId",
        "workshop_id",
        "workshopId",
        "steam_id",
        "steamId",
        "published_file_id",
        "publishedFileId"
    )) {
        if ($Dependency.$field) {
            return [string]$Dependency.$field
        }
    }
    return ""
}

function Get-DependencyMinVersion($Dependency) {
    if ($Dependency -is [string] -or $null -eq $Dependency) { return "" }
    foreach ($field in @(
        "min_version",
        "minVersion",
        "minimum_version",
        "minimumVersion",
        "version_min",
        "versionMin",
        "required_version",
        "requiredVersion"
    )) {
        if ($Dependency.$field) {
            return [string]$Dependency.$field
        }
    }
    return ""
}

function Get-MinGameVersion($Json) {
    foreach ($field in @(
        "min_game_version",
        "minGameVersion",
        "minimum_game_version",
        "minimumGameVersion",
        "game_version_min",
        "gameVersionMin",
        "required_game_version",
        "requiredGameVersion"
    )) {
        if ($Json.$field) {
            return [string]$Json.$field
        }
    }
    return ""
}

function Get-VersionSegments([string]$Version) {
    if ([string]::IsNullOrWhiteSpace($Version)) { return @() }
    $text = $Version.Trim()
    if ($text.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $text = $text.Substring(1)
    }

    $segments = @()
    foreach ($part in ($text -split '[^\d]+')) {
        if ([string]::IsNullOrWhiteSpace($part)) { continue }
        $value = 0L
        if ([Int64]::TryParse($part, [ref]$value)) {
            $segments += $value
        }
    }
    return @($segments)
}

function Compare-ModVersion([string]$ActualVersion, [string]$RequiredVersion) {
    $actual = @(Get-VersionSegments $ActualVersion)
    $required = @(Get-VersionSegments $RequiredVersion)
    if ($actual.Count -eq 0 -or $required.Count -eq 0) { return $null }

    $count = [Math]::Max($actual.Count, $required.Count)
    for ($i = 0; $i -lt $count; $i++) {
        $a = if ($i -lt $actual.Count) { $actual[$i] } else { 0L }
        $r = if ($i -lt $required.Count) { $required[$i] } else { 0L }
        if ($a -lt $r) { return -1 }
        if ($a -gt $r) { return 1 }
    }
    return 0
}

function Resolve-DependencyAlias([string]$DependencyId, $ById, $ByName, $ByWorkshopId) {
    if ([string]::IsNullOrWhiteSpace($DependencyId)) { return "" }
    $key = $DependencyId.ToLowerInvariant()
    if ($ById.ContainsKey($key)) {
        return [string]$ById[$key].Id
    }
    if ($ByName.ContainsKey($key) -and @($ByName[$key]).Count -eq 1) {
        return [string]$ByName[$key][0].Id
    }
    if ($ByWorkshopId.ContainsKey($key) -and @($ByWorkshopId[$key]).Count -eq 1) {
        return [string]$ByWorkshopId[$key][0].Id
    }
    return $DependencyId
}

function Get-WorkshopIdFromPath([string]$Path) {
    $parts = $Path -split '[\\/]'
    for ($i = 0; $i -lt $parts.Count - 1; $i++) {
        if ($parts[$i] -eq '2868840' -and $parts[$i + 1] -match '^\d+$') {
            return $parts[$i + 1]
        }
    }
    return ""
}

function Test-RuntimeDataPath([string]$Path) {
    $current = Split-Path -Parent $Path
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if ((Split-Path -Leaf $current) -ieq "ModTheSpire2Data") {
            return $true
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
        $current = $parent
    }
    return $false
}

$roots = @(
    (Join-Path $GameDir "mods"),
    (Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) "workshop\content\2868840"),
    (Join-Path (Get-Location) "TestMods")
) | Where-Object { Test-Path $_ }

$currentGameVersion = Get-CurrentGameVersion $GameDir

$mods = @()
foreach ($root in $roots) {
    Get-ChildItem $root -Recurse -File | Where-Object { (Test-ManifestCandidatePath $_.FullName) -and -not (Test-RuntimeDataPath $_.FullName) } | ForEach-Object {
        $manifestPath = $_.FullName
        try {
            $json = Get-Content $manifestPath -Raw | ConvertFrom-Json
            $modId = [string]$json.id
            if ([string]::IsNullOrWhiteSpace($modId)) {
                if ([System.IO.Path]::GetExtension($manifestPath) -ieq ".json") {
                    $modId = Get-PckNameFallbackId $json $manifestPath
                }
            }
            if ([string]::IsNullOrWhiteSpace($modId)) { return }
            $dir = Split-Path $manifestPath -Parent
            if (-not (Test-PlausibleManifestCandidate $json $manifestPath $modId $dir)) { return }
            $deps = @()
            $depVersionRequirements = @()
            foreach ($field in @("dependencies", "requires", "required_mods", "requiredMods")) {
                foreach ($dep in @($json.$field)) {
                    if (Test-OptionalDependencyObject $dep) { continue }
                    $depId = Get-DependencyId $dep
                    if (-not [string]::IsNullOrWhiteSpace($depId)) {
                        $deps += $depId
                        $minVersion = Get-DependencyMinVersion $dep
                        if (-not [string]::IsNullOrWhiteSpace($minVersion)) {
                            $depVersionRequirements += [pscustomobject]@{
                                Id = $depId
                                MinVersion = $minVersion
                            }
                        }
                    }
                }
            }
            $hasDll = [bool]$json.has_dll -or (Test-PayloadFileInModDir $dir "*.dll")
            $hasPck = [bool]$json.has_pck -or (Test-PayloadFileInModDir $dir "*.pck")
            $affectsGameplay = if ($null -eq $json.affects_gameplay) { $true } else { [bool]$json.affects_gameplay }
            $declaresHot = [bool]$json.hot_apply -or [bool]$json.hot_reload
            $scope = if ($json.hot_apply_scope) { [string]$json.hot_apply_scope } elseif ($json.hot_reload_scope) { [string]$json.hot_reload_scope } else { "" }
            $isSelf = $modId -eq "ModTheSpire2"
            $isFrameworkRoot = (@("BaseLib", "RitsuLib", "STS2-RitsuLib") -contains $modId) -or (Test-FrameworkRootManifest $json)
            $restartRequiredTagReason = Get-RestartRequiredManifestReason $json
            $isRestartRequiredTagged = $isFrameworkRoot -or (-not [string]::IsNullOrWhiteSpace($restartRequiredTagReason))
            $isMainMenuOnly = $knownMainMenuContentMods.Contains($modId) -or (Test-MainMenuOnlyManifest $json $scope)
            $isRuntimeSafeDeclared = Test-RuntimeSafeManifest $json $scope
            $isRunSafeRuntime = Test-RunSafeRuntimeManifest $json $scope
            $hot = $false
            $reason = ""
            $hotScope = "restart"
            if ($isSelf) {
                $reason = "launcher/UI patch requires restart"
            } elseif ($isRestartRequiredTagged) {
                $reason = if ($isFrameworkRoot) { "framework DLL; restart required" } else { $restartRequiredTagReason }
            } elseif ($isMainMenuOnly) {
                $hotScope = "main_menu_no_run"
                if ($simulatedSafeMainMenuNoRun -and $loadedIds.Contains($modId)) {
                    $hot = $true
                    $reason = "future-run content toggle; main menu safe"
                } elseif ($simulatedSafeMainMenuNoRun) {
                    $reason = "main menu only; mod is not loaded, start through launcher to enable"
                } elseif ($simulatedActiveRun) {
                    $reason = "main menu only; active run may reference this content"
                } elseif ($simulatedUnfinishedRun) {
                    $reason = "main menu only; unfinished run present"
                } else {
                    $reason = "main menu only; safe menu state not confirmed"
                }
            } elseif (($declaresHot -or $isRuntimeSafeDeclared -or (-not $affectsGameplay)) -and (-not $hasDll) -and (-not $hasPck) -and (-not $affectsGameplay)) {
                $hot = $true
                $hotScope = "runtime"
                $reason = if ($isRuntimeSafeDeclared -or $declaresHot) { "manifest declares runtime-safe hot-apply" } else { "utility/config candidate" }
                if ($simulatedActiveRun -and (-not $isRunSafeRuntime)) {
                    $hot = $false
                    $reason = "runtime/config apply blocked during active run"
                }
            } elseif ($hasDll -or $hasPck) {
                $reason = "DLL/PCK or unknown startup behavior"
            } else {
                $reason = "gameplay or unknown behavior"
            }
            $mods += [pscustomobject]@{
                Id = $modId
                Name = if ($json.name) { [string]$json.name } else { $modId }
                Hot = $hot
                Scope = $hotScope
                Enabled = $enabledIds.Contains($modId)
                Loaded = $loadedIds.Contains($modId)
                Deps = @($deps)
                Version = if ($json.version) { [string]$json.version } else { "" }
                MinGameVersion = Get-MinGameVersion $json
                DepVersionRequirements = @($depVersionRequirements)
                FrameworkRoot = $isFrameworkRoot
                RunSafeRuntime = $isRunSafeRuntime
                WorkshopId = Get-WorkshopIdFromPath $manifestPath
                Reason = $reason
            }
        } catch {
            $ext = [System.IO.Path]::GetExtension($manifestPath)
            if ($ext -ine ".json" -and $ext -ine ".manifest") { return }

            $modId = [System.IO.Path]::GetFileNameWithoutExtension($manifestPath)
            if ([string]::IsNullOrWhiteSpace($modId) -or $modId -ieq "mod_manifest" -or $modId -ieq "variants") {
                return
            }

            $dir = Split-Path $manifestPath -Parent
            if (-not ((Test-PayloadFileInModDir $dir "*.dll") -or (Test-PayloadFileInModDir $dir "*.pck"))) {
                return
            }

            $mods += [pscustomobject]@{
                Id = $modId
                Name = $modId
                Hot = $false
                Scope = "restart"
                Enabled = $enabledIds.Contains($modId)
                Loaded = $loadedIds.Contains($modId)
                Deps = @()
                Version = ""
                MinGameVersion = ""
                DepVersionRequirements = @()
                FrameworkRoot = $false
                RunSafeRuntime = $false
                WorkshopId = Get-WorkshopIdFromPath $manifestPath
                Reason = "invalid manifest; restart required"
            }
        }
    }
}

$unique = $mods | Group-Object Id | ForEach-Object { $_.Group[0] }
$byId = @{}
foreach ($mod in $unique) { $byId[$mod.Id.ToLowerInvariant()] = $mod }
$byName = @{}
foreach ($group in ($unique | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Name) } | Group-Object Name)) {
    $byName[$group.Name.ToLowerInvariant()] = @($group.Group)
}
$byWorkshopId = @{}
foreach ($group in ($unique | Where-Object { -not [string]::IsNullOrWhiteSpace($_.WorkshopId) } | Group-Object WorkshopId)) {
    $byWorkshopId[$group.Name.ToLowerInvariant()] = @($group.Group)
}
foreach ($mod in $unique) {
    $canonicalDeps = @()
    foreach ($dep in $mod.Deps) {
        $canonicalDeps += Resolve-DependencyAlias ([string]$dep) $byId $byName $byWorkshopId
    }
    $mod.Deps = @($canonicalDeps | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

    $canonicalVersionRequirements = @()
    foreach ($requirement in @($mod.DepVersionRequirements)) {
        $depId = Resolve-DependencyAlias ([string]$requirement.Id) $byId $byName $byWorkshopId
        if ([string]::IsNullOrWhiteSpace($depId) -or [string]::IsNullOrWhiteSpace([string]$requirement.MinVersion)) {
            continue
        }
        $canonicalVersionRequirements += [pscustomobject]@{
            Id = $depId
            MinVersion = [string]$requirement.MinVersion
        }
    }
    $mod.DepVersionRequirements = @($canonicalVersionRequirements)
}
$changed = $true
while ($changed) {
    $changed = $false
    foreach ($mod in $unique) {
        $missingDep = $null
        foreach ($dep in $mod.Deps) {
            $key = $dep.ToLowerInvariant()
            if (-not $byId.ContainsKey($key)) {
                $missingDep = $dep
                break
            }
        }
        if ($null -ne $missingDep) {
            if (-not ([string]$mod.Reason).StartsWith("missing dependency:", [System.StringComparison]::OrdinalIgnoreCase)) {
                $mod.Hot = $false
                $mod.Reason = "missing dependency: $missingDep"
                $changed = $true
            }
            continue
        }
        $tooLowRequirement = $null
        foreach ($requirement in @($mod.DepVersionRequirements)) {
            $key = ([string]$requirement.Id).ToLowerInvariant()
            if (-not $byId.ContainsKey($key)) { continue }
            $installedVersion = [string]$byId[$key].Version
            $comparison = Compare-ModVersion $installedVersion ([string]$requirement.MinVersion)
            if ($null -ne $comparison -and $comparison -lt 0) {
                $tooLowRequirement = [pscustomobject]@{
                    Id = [string]$requirement.Id
                    MinVersion = [string]$requirement.MinVersion
                    InstalledVersion = $installedVersion
                }
                break
            }
        }
        if ($null -ne $tooLowRequirement) {
            $versionReason = "dependency version too low: $($tooLowRequirement.Id) requires $($tooLowRequirement.MinVersion), found $($tooLowRequirement.InstalledVersion)"
            if ($mod.Reason -ne $versionReason) {
                $mod.Hot = $false
                $mod.Reason = $versionReason
                $changed = $true
            }
            continue
        }
        $gameVersionComparison = if ([string]::IsNullOrWhiteSpace([string]$mod.MinGameVersion)) { $null } else { Compare-ModVersion $currentGameVersion ([string]$mod.MinGameVersion) }
        if ($null -ne $gameVersionComparison -and $gameVersionComparison -lt 0) {
            $gameVersionReason = "game version too low: requires $($mod.MinGameVersion), found $currentGameVersion"
            if ($mod.Reason -ne $gameVersionReason) {
                $mod.Hot = $false
                $mod.Reason = $gameVersionReason
                $changed = $true
            }
            continue
        }
        if ($mod.Hot) {
            foreach ($dep in $mod.Deps) {
                $key = $dep.ToLowerInvariant()
                if ((-not $byId[$key].Hot) -and -not ($mod.Scope -eq "main_menu_no_run" -and $simulatedSafeMainMenuNoRun -and $loadedIds.Contains($mod.Id) -and $byId[$key].Loaded -and $byId[$key].Enabled -and $byId[$key].FrameworkRoot)) {
                    $mod.Hot = $false
                    $mod.Reason = "depends on restart-required mod: $dep"
                    $changed = $true
                }
            }
        }
    }
}

"Hot-Apply candidates:"
$unique | Where-Object Hot | Sort-Object Name | ForEach-Object {
    $state = if ($ShowState) { " enabled=$($_.Enabled) loaded=$($_.Loaded)" } else { "" }
    "  - $($_.Name) [$($_.Id)] :: $($_.Reason)$state"
}
""
"Restart Required:"
$unique | Where-Object { -not $_.Hot } | Sort-Object Name | ForEach-Object {
    $state = if ($ShowState) { " enabled=$($_.Enabled) loaded=$($_.Loaded)" } else { "" }
    "  - $($_.Name) [$($_.Id)] :: $($_.Reason)$state"
}

if ($SelfTestApplyStateGate) {
    ""
    "Apply State Gate Self-Test:"
    $desiredNone = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $blockUnfinished = Test-ApplyStateSafetyGate $unique $desiredNone $false $false $true
    if ([string]::IsNullOrWhiteSpace($blockUnfinished)) {
        "  - FAIL: unfinished-run main-menu content disable was not blocked"
    } else {
        "  - PASS: $blockUnfinished"
    }

    $desiredAllCurrent = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($mod in ($unique | Where-Object { $_.Enabled })) {
        [void]$desiredAllCurrent.Add($mod.Id)
    }
    $noChangeBlock = Test-ApplyStateSafetyGate $unique $desiredAllCurrent $false $false $true
    if ([string]::IsNullOrWhiteSpace($noChangeBlock)) {
        "  - PASS: unchanged main-menu content does not block"
    } else {
        "  - FAIL: unchanged main-menu content blocked unexpectedly: $noChangeBlock"
    }

    $desiredRuntimeOff = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $blockRuntimeActiveRun = Test-ApplyStateSafetyGate $unique $desiredRuntimeOff $false $true $true
    if ($blockRuntimeActiveRun -match "runtime/config changes require a run-safe manifest token") {
        "  - PASS: $blockRuntimeActiveRun"
    } else {
        "  - FAIL: active-run runtime/config change was not blocked"
    }
}
