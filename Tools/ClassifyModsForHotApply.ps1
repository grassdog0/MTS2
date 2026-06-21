param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2"
)

$roots = @(
    (Join-Path $GameDir "mods"),
    (Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) "workshop\content\2868840"),
    (Join-Path (Get-Location) "TestMods")
) | Where-Object { Test-Path $_ }

$mods = @()
foreach ($root in $roots) {
    Get-ChildItem $root -Recurse -Filter *.json | ForEach-Object {
        try {
            $json = Get-Content $_.FullName -Raw | ConvertFrom-Json
            if (-not $json.id) { return }
            $dir = Split-Path $_.FullName -Parent
            $deps = @()
            foreach ($dep in @($json.dependencies)) {
                if ($dep -is [string]) { $deps += $dep }
                elseif ($dep.id) { $deps += $dep.id }
                elseif ($dep.mod_id) { $deps += $dep.mod_id }
            }
            $hasDll = [bool]$json.has_dll -or [bool](Get-ChildItem $dir -Filter *.dll -ErrorAction SilentlyContinue)
            $hasPck = [bool]$json.has_pck -or [bool](Get-ChildItem $dir -Filter *.pck -ErrorAction SilentlyContinue)
            $affectsGameplay = if ($null -eq $json.affects_gameplay) { $true } else { [bool]$json.affects_gameplay }
            $declaresHot = [bool]$json.hot_apply -or [bool]$json.hot_reload
            $isSelf = $json.id -eq "ModTheSpire2"
            $hot = (-not $isSelf) -and ($declaresHot -or ((-not $affectsGameplay) -and (-not $hasDll) -and (-not $hasPck)))
            $mods += [pscustomobject]@{
                Id = [string]$json.id
                Name = if ($json.name) { [string]$json.name } else { [string]$json.id }
                Hot = $hot
                Deps = @($deps)
                Reason = if ($hot) { if ($declaresHot) { "manifest declares hot-apply" } else { "utility/config candidate" } } else { if ($isSelf) { "launcher/UI patch requires restart" } elseif ($hasDll -or $hasPck) { "DLL/PCK or unknown startup behavior" } else { "gameplay or unknown behavior" } }
            }
        } catch {
        }
    }
}

$unique = $mods | Group-Object Id | ForEach-Object { $_.Group[0] }
$byId = @{}
foreach ($mod in $unique) { $byId[$mod.Id.ToLowerInvariant()] = $mod }
$changed = $true
while ($changed) {
    $changed = $false
    foreach ($mod in $unique) {
        if ($mod.Hot) {
            foreach ($dep in $mod.Deps) {
                $key = $dep.ToLowerInvariant()
                if (-not $byId.ContainsKey($key)) {
                    $mod.Hot = $false
                    $mod.Reason = "missing dependency: $dep"
                    $changed = $true
                } elseif (-not $byId[$key].Hot) {
                    $mod.Hot = $false
                    $mod.Reason = "depends on restart-required mod: $dep"
                    $changed = $true
                }
            }
        }
    }
}

"Hot-Apply candidates:"
$unique | Where-Object Hot | Sort-Object Name | ForEach-Object { "  - $($_.Name) [$($_.Id)] :: $($_.Reason)" }
""
"Restart Required:"
$unique | Where-Object { -not $_.Hot } | Sort-Object Name | ForEach-Object { "  - $($_.Name) [$($_.Id)] :: $($_.Reason)" }
