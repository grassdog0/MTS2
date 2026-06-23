param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$OutPath = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = Join-Path $root "dist\analysis-workshop-manifests-current.tsv"
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

function Get-WorkshopIdFromPath([string]$Path) {
    $parts = $Path -split '[\\/]'
    for ($i = 0; $i -lt $parts.Count - 1; $i++) {
        if ($parts[$i] -eq "2868840" -and $parts[$i + 1] -match '^\d+$') {
            return $parts[$i + 1]
        }
    }
    return ""
}

function Get-CandidateKind([string]$Path) {
    $name = [System.IO.Path]::GetFileName($Path)
    if ($name -ieq "mod_manifest.json") { return "mod_manifest" }
    if ($name -ieq "mod_mainfest.json") { return "typo_mod_mainfest" }
    if ($name -ieq "settings.json") { return "settings_json" }
    if ($name -ieq "variants.manifest" -or $name -ilike "*-variants.manifest") { return "variants_manifest" }
    if ([System.IO.Path]::GetExtension($Path) -ieq ".manifest") { return "sidecar_manifest" }
    return "json"
}

function Test-PayloadFileInModDir([string]$Dir, [string]$Filter) {
    if ([string]::IsNullOrWhiteSpace($Dir) -or -not (Test-Path -LiteralPath $Dir)) {
        return $false
    }
    return [bool](Get-ChildItem -LiteralPath $Dir -Recurse -File -Filter $Filter -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-RuntimeDataPath $_.FullName) } |
        Select-Object -First 1)
}

function Convert-DependencyValueToText($Value) {
    if ($null -eq $Value) { return "" }
    if ($Value -is [string]) { return [string]$Value }
    foreach ($field in @("id", "mod_id", "modId", "workshop_id", "workshopId", "steam_id", "steamId", "published_file_id", "publishedFileId")) {
        if ($null -ne $Value.$field) { return [string]$Value.$field }
    }
    try {
        return ($Value | ConvertTo-Json -Compress -Depth 8)
    } catch {
        return [string]$Value
    }
}

function Convert-DependencyConstraintToText($Value) {
    if ($null -eq $Value -or $Value -is [string]) { return "" }
    $id = Convert-DependencyValueToText $Value
    $constraints = @()
    foreach ($field in @("min_version", "minVersion", "minimum_version", "minimumVersion", "version", "max_version", "maxVersion")) {
        if ($null -ne $Value.$field) {
            $constraints += "$field=$($Value.$field)"
        }
    }
    if ($constraints.Count -eq 0) { return "" }
    if ([string]::IsNullOrWhiteSpace($id)) {
        return ($constraints -join ",")
    }
    return "$id $($constraints -join ',')"
}

function Get-FirstJsonString($Json, [string[]]$Fields) {
    foreach ($field in $Fields) {
        if ($null -ne $Json.$field) {
            $value = [string]$Json.$field
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                return $value
            }
        }
    }
    return ""
}

function Get-DependencyValues($Json, [string[]]$Fields) {
    $values = @()
    foreach ($field in $Fields) {
        if ($null -eq $Json.$field) { continue }
        foreach ($value in @($Json.$field)) {
            $text = Convert-DependencyValueToText $value
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                $values += "$field=$text"
            }
        }
    }
    return @($values)
}

function Get-DependencyConstraints($Json, [string[]]$Fields) {
    $values = @()
    foreach ($field in $Fields) {
        if ($null -eq $Json.$field) { continue }
        foreach ($value in @($Json.$field)) {
            $text = Convert-DependencyConstraintToText $value
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                $values += "$field=$text"
            }
        }
    }
    return @($values)
}

$steamApps = Split-Path (Split-Path $GameDir -Parent) -Parent
$roots = @(
    (Join-Path $GameDir "mods"),
    (Join-Path $steamApps "workshop\content\2868840"),
    (Join-Path $root "TestMods")
) | Where-Object { Test-Path -LiteralPath $_ }

$dependencyFields = @("dependencies", "requires", "required_mods", "requiredMods", "load_after", "loadAfter", "load_before", "loadBefore")
$tagFields = @("hot_apply_scope", "hot_reload_scope", "content_type", "content_tags", "mod_type", "tags", "affects_gameplay", "has_dll", "has_pck", "hot_apply", "hot_reload")
$gameVersionFields = @("min_game_version", "minGameVersion", "minimum_game_version", "minimumGameVersion", "game_version_min", "gameVersionMin", "required_game_version", "requiredGameVersion")
$rows = @()

foreach ($scanRoot in $roots) {
    Get-ChildItem -LiteralPath $scanRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -ieq ".json" -or $_.Extension -ieq ".manifest" } |
        Where-Object { -not (Test-RuntimeDataPath $_.FullName) } |
        ForEach-Object {
            $file = $_.FullName
            $dir = Split-Path $file -Parent
            $hasDllPayload = Test-PayloadFileInModDir $dir "*.dll"
            $hasPckPayload = Test-PayloadFileInModDir $dir "*.pck"
            $candidateKind = Get-CandidateKind $file
            try {
                $json = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json -ErrorAction Stop
                $id = [string]$json.id
                $identitySource = "id"
                if ([string]::IsNullOrWhiteSpace($id) -and $json.pck_name) {
                    $id = [string]$json.pck_name
                    $identitySource = "pck_name"
                } elseif ([string]::IsNullOrWhiteSpace($id)) {
                    $identitySource = "none"
                }

                $deps = @()
                foreach ($field in $dependencyFields) {
                    if ($null -ne $json.$field) { $deps += $field }
                }
                $tags = @()
                foreach ($field in $tagFields) {
                    if ($null -ne $json.$field) { $tags += $field }
                }
                if ([string]::IsNullOrWhiteSpace($id) -and $deps.Count -eq 0 -and $tags.Count -eq 0 -and -not $hasDllPayload -and -not $hasPckPayload) {
                    return
                }

                $rows += [pscustomobject]@{
                    SourceRoot = $scanRoot
                    WorkshopId = Get-WorkshopIdFromPath $file
                    CandidateKind = $candidateKind
                    ParseStatus = "ok"
                    IdentitySource = $identitySource
                    Id = $id
                    Name = [string]$json.name
                    HasDllPayload = $hasDllPayload
                    HasPckPayload = $hasPckPayload
                    DependencyFields = ($deps -join ",")
                    DependencyValues = ((Get-DependencyValues $json $dependencyFields) -join "; ")
                    DependencyConstraints = ((Get-DependencyConstraints $json $dependencyFields) -join "; ")
                    MinGameVersion = Get-FirstJsonString $json $gameVersionFields
                    TagFields = ($tags -join ",")
                    File = $file
                }
            } catch {
                $fallbackId = [System.IO.Path]::GetFileNameWithoutExtension($file)
                $canFallback =
                    -not [string]::IsNullOrWhiteSpace($fallbackId) -and
                    $fallbackId -ine "mod_manifest" -and
                    $fallbackId -ine "mod_mainfest" -and
                    $fallbackId -ine "settings" -and
                    $fallbackId -ine "variants" -and
                    ($hasDllPayload -or $hasPckPayload)

                $rows += [pscustomobject]@{
                    SourceRoot = $scanRoot
                    WorkshopId = Get-WorkshopIdFromPath $file
                    CandidateKind = $candidateKind
                    ParseStatus = if ($canFallback) { "invalid_json_payload_fallback" } else { "invalid_json" }
                    IdentitySource = if ($canFallback) { "filename_fallback" } else { "none" }
                    Id = if ($canFallback) { $fallbackId } else { "" }
                    Name = if ($canFallback) { $fallbackId } else { "" }
                    HasDllPayload = $hasDllPayload
                    HasPckPayload = $hasPckPayload
                    DependencyFields = ""
                    DependencyValues = ""
                    DependencyConstraints = ""
                    MinGameVersion = ""
                    TagFields = "parse_error"
                    File = $file
                }
            }
        }
}

New-Item -ItemType Directory -Force -Path (Split-Path $OutPath -Parent) | Out-Null
$sortedRows = $rows | Sort-Object WorkshopId, Id, File
$sortedRows | Export-Csv -NoTypeInformation -Delimiter "`t" -LiteralPath $OutPath -Encoding UTF8
$sortedRows
