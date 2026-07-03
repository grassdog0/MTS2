using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AssemblyBuilder = System.Reflection.Emit.AssemblyBuilder;
using AssemblyBuilderAccess = System.Reflection.Emit.AssemblyBuilderAccess;
using CustomAttributeBuilder = System.Reflection.Emit.CustomAttributeBuilder;
using OpCodes = System.Reflection.Emit.OpCodes;
using TypeBuilder = System.Reflection.Emit.TypeBuilder;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace ModTheSpire2;

[ModInitializer(nameof(Initialize))]
public static class ModTheSpire2Entry
{
    private const string HarmonyId = "HZDH.ModTheSpire2";
    private const string BuildMarker = "0.4.0-clean-restart-ui";

    public static void Initialize()
    {
        try
        {
            CompanionLog.Write("Initialize ModTheSpire2 " + BuildMarker);
            new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
            ModConfigIntegration.TryRegister();
            CompanionLog.Write("PatchAll complete");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Initialize failed: " + ex);
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuReadyPatch
{
    private const string ButtonNodeName = "ModTheSpire2Button";

    public static void Postfix(NMainMenu __instance)
    {
        try
        {
            ModConfigIntegration.TryRegister();
            CompanionLog.Write("NMainMenu._Ready");
            if (__instance.FindChild(ButtonNodeName, recursive: true, owned: false) is not null)
            {
                return;
            }

            var template = __instance.FindChildren("*", nameof(NMainMenuTextButton), recursive: true, owned: false)
                .OfType<NMainMenuTextButton>()
                .FirstOrDefault();
            if (template?.GetParent() is not Control parent)
            {
                return;
            }

            var button = new NMainMenuTextButton
            {
                Name = ButtonNodeName,
                CustomMinimumSize = template.CustomMinimumSize,
                SizeFlagsHorizontal = template.SizeFlagsHorizontal,
                SizeFlagsVertical = template.SizeFlagsVertical
            };

            parent.AddChild(button);
            parent.MoveChild(button, template.GetIndex() + 1);
            button.SetLocalization("ModTheSpire2");
            button.Released += OpenLauncher;
            CompanionLog.Write("Main menu button added under " + parent.GetPath());
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Main menu button failed: " + ex);
        }
    }

    private static void OpenLauncher(NClickableControl _)
    {
        if (Engine.GetMainLoop() is SceneTree tree && tree.Root is Node root)
        {
            RestartToLauncher.ShowConfirm(root);
        }
    }
}

[HarmonyPatch(typeof(NModdingScreen), nameof(NModdingScreen._Ready))]
internal static class ModdingScreenReadyPatch
{
    public static void Postfix(NModdingScreen __instance) => ModdingScreenButton.TryAdd(__instance, "NModdingScreen._Ready");
}

[HarmonyPatch(typeof(NModdingScreen), nameof(NModdingScreen.OnSubmenuOpened))]
internal static class ModdingScreenOpenedPatch
{
    public static void Postfix(NModdingScreen __instance) => ModdingScreenButton.TryAdd(__instance, "NModdingScreen.OnSubmenuOpened");
}

internal static class ModdingScreenButton
{
    private const string RestartButtonNodeName = "ModTheSpire2RestartButton";

    public static void TryAdd(NModdingScreen screen, string source)
    {
        try
        {
            CompanionLog.Write(source);
            if (screen.FindChild(RestartButtonNodeName, recursive: true, owned: false) is Button existing)
            {
                KeepButtonInteractive(existing);
                CompanionLog.Write("Visible modding screen button refreshed from " + source + " under " + existing.GetParent()?.GetPath());
                return;
            }

            var modsBorder = screen.FindChild("ModsBorder", recursive: true, owned: false) as Control;
            var template = modsBorder?.FindChild("GetModsButton", recursive: true, owned: false) as Control
                ?? screen.FindChild("GetModsButton", recursive: true, owned: false) as Control;
            var buttonSize = template?.Size ?? new Vector2(260, 48);
            if (buttonSize.X <= 0 || buttonSize.Y <= 0)
            {
                buttonSize = template?.CustomMinimumSize ?? new Vector2(260, 56);
            }
            var parent = screen;

            var restart = new Button
            {
                Name = RestartButtonNodeName,
                Text = "ModTheSpire2 Launcher",
                Position = FindButtonPosition(parent),
                AnchorsPreset = (int)Control.LayoutPreset.TopLeft,
                CustomMinimumSize = buttonSize,
                Size = buttonSize,
                TooltipText = "Open ModTheSpire2 management.",
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = true,
                TopLevel = false,
                ZIndex = 5000
            };
            UiStyle.ApplyButton(restart);
            restart.Pressed += () =>
            {
                CompanionLog.Write("Modding screen launcher clicked");
                RestartToLauncher.ShowConfirm(screen);
            };
            parent.AddChild(restart);
            KeepButtonInteractive(restart);
            ScheduleRefresh(screen, source);
            CompanionLog.Write("Visible modding screen button added from " + source + " under " + parent.GetPath());
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Modding screen button failed from " + source + ": " + ex);
        }
    }

    private static void KeepButtonInteractive(Button button)
    {
        button.Visible = true;
        button.Disabled = false;
        button.MouseFilter = Control.MouseFilterEnum.Stop;
        button.ZIndex = 5000;
        button.Show();
        if (button.GetParent() is Node parent)
        {
            parent.MoveChild(button, parent.GetChildCount() - 1);
        }
    }

    private static void ScheduleRefresh(NModdingScreen screen, string source)
    {
        try
        {
            RefreshAfterDelay(screen, source, 0.05);
            RefreshAfterDelay(screen, source, 0.15);
            RefreshAfterDelay(screen, source, 0.35);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Modding screen button delayed refresh scheduling failed: " + ex.Message);
        }
    }

    private static async void RefreshAfterDelay(NModdingScreen screen, string source, double seconds)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                return;
            }
            await tree.CreateTimer(seconds).ToSignal(tree, SceneTreeTimer.SignalName.Timeout);
            if (!GodotObject.IsInstanceValid(screen))
            {
                return;
            }
            if (screen.FindChild(RestartButtonNodeName, recursive: true, owned: false) is Button button)
            {
                KeepButtonInteractive(button);
                CompanionLog.Write($"Delayed modding screen button refresh after {seconds:0.00}s from {source}");
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Modding screen button delayed refresh failed: " + ex.Message);
        }
    }

    private static Vector2 FindButtonPosition(Node parent)
    {
        try
        {
            if (parent is Control parentControl &&
                parent.FindChild("InstalledModsTitle", recursive: true, owned: false) is Control title)
            {
                var localTitlePosition = parentControl.GetGlobalTransform().AffineInverse() * title.GlobalPosition;
                var x = localTitlePosition.X + title.Size.X + 28;
                if (x < 260)
                {
                    x = 260;
                }
                return new Vector2(x, localTitlePosition.Y - 8);
            }
        }
        catch
        {
        }

        return new Vector2(330, 16);
    }
}

[HarmonyPatch]
internal static class MultiplayerMismatchErrorPatch
{
    public static bool Prepare()
    {
        return TargetMethod() is not null;
    }

    public static MethodBase? TargetMethod()
    {
        return AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Multiplayer.NetErrorInfo")
            ?.GetMethod("GetErrorString", BindingFlags.Public | BindingFlags.Instance);
    }

    public static void Postfix(object __instance, ref string __result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(__result) || __result.Contains("ModTheSpire2 help", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (!MultiplayerMismatchInfo.TryBuild(__instance, out var helpText, out var reportText))
            {
                return;
            }

            __result = __result.TrimEnd() + "\n\n" + helpText;
            MultiplayerMismatchInfo.SaveLastReport(reportText);
            CompanionLog.Write("Multiplayer ModMismatch help appended");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Multiplayer mismatch helper failed: " + ex.Message);
        }
    }
}

internal static class MultiplayerMismatchInfo
{
    private const int MaxItems = 24;

    public static bool TryBuild(object info, out string helpText, out string reportText)
    {
        helpText = "";
        reportText = "";
        if (!LooksLikeModMismatch(info))
        {
            return false;
        }

        var localMissing = new System.Collections.Generic.List<string>();
        var hostMissing = new System.Collections.Generic.List<string>();
        CollectMissingMods(info, localMissing, hostMissing, new System.Collections.Generic.HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        var resolver = MismatchModResolver.Create();

        var lines = new System.Collections.Generic.List<string>
        {
            "ModTheSpire2 help:",
            "This multiplayer join failed because the host and local gameplay mod lists do not match."
        };
        if (localMissing.Count > 0)
        {
            lines.Add("Mods the host has but you are missing:");
            lines.AddRange(localMissing.Take(MaxItems).Select(name => "- " + resolver.Describe(name)));
            if (localMissing.Count > MaxItems)
            {
                lines.Add($"- ...and {localMissing.Count - MaxItems} more");
            }
        }
        if (hostMissing.Count > 0)
        {
            lines.Add("Mods you have but the host is missing:");
            lines.AddRange(hostMissing.Take(MaxItems).Select(name => "- " + resolver.Describe(name)));
            if (hostMissing.Count > MaxItems)
            {
                lines.Add($"- ...and {hostMissing.Count - MaxItems} more");
            }
        }
        if (localMissing.Count == 0 && hostMissing.Count == 0)
        {
            lines.Add("The game did not expose the exact missing mod names to ModTheSpire2.");
        }
        lines.Add("Use the ModTheSpire2 launcher to switch profiles or restart with a matching mod set. If a missing mod has no Workshop link here, search its name in the Workshop.");

        helpText = string.Join("\n", lines);
        reportText = "ModTheSpire2 multiplayer mismatch report\n"
            + "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n"
            + helpText
            + "\n\nKnown local/subscribed mod index:\n"
            + resolver.BuildIndexReport()
            + "\n\nRaw NetErrorInfo:\n" + SafeToString(info);
        return true;
    }

    public static void SaveLastReport(string report)
    {
        try
        {
            var dir = Path.Combine(LauncherActions.GetModDir(), "ModTheSpire2Data");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "multiplayer-mismatch-last.txt"), report);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Save multiplayer mismatch report failed: " + ex.Message);
        }
    }

    private static bool LooksLikeModMismatch(object? info)
    {
        if (info is null)
        {
            return false;
        }
        try
        {
            var reason = info.GetType().GetMethod("GetReason", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(info, null);
            if (StringLikeModMismatch(reason))
            {
                return true;
            }
        }
        catch
        {
        }

        return ObjectGraphContainsModMismatch(info, new System.Collections.Generic.HashSet<object>(ReferenceEqualityComparer.Instance), 0);
    }

    private static bool ObjectGraphContainsModMismatch(object? value, System.Collections.Generic.HashSet<object> seen, int depth)
    {
        if (value is null || depth > 3)
        {
            return false;
        }
        if (StringLikeModMismatch(value))
        {
            return true;
        }
        var type = value.GetType();
        if (IsSimple(type) || !seen.Add(value))
        {
            return false;
        }
        foreach (var memberValue in EnumerateMemberValues(value, type))
        {
            if (ObjectGraphContainsModMismatch(memberValue, seen, depth + 1))
            {
                return true;
            }
        }
        return false;
    }

    private static bool StringLikeModMismatch(object? value)
    {
        if (value is null)
        {
            return false;
        }
        var text = value.ToString();
        return text?.IndexOf("ModMismatch", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void CollectMissingMods(
        object? value,
        System.Collections.Generic.List<string> localMissing,
        System.Collections.Generic.List<string> hostMissing,
        System.Collections.Generic.HashSet<object> seen,
        int depth)
    {
        if (value is null || depth > 5)
        {
            return;
        }
        var type = value.GetType();
        if (IsSimple(type) || !seen.Add(value))
        {
            return;
        }

        foreach (var field in SafeFields(type))
        {
            object? fieldValue;
            try
            {
                fieldValue = field.GetValue(value);
            }
            catch
            {
                continue;
            }
            AddIfMissingList(field.Name, fieldValue, localMissing, hostMissing);
            CollectMissingMods(fieldValue, localMissing, hostMissing, seen, depth + 1);
        }

        foreach (var prop in SafeProperties(type))
        {
            if (prop.GetIndexParameters().Length != 0)
            {
                continue;
            }
            object? propValue;
            try
            {
                propValue = prop.GetValue(value);
            }
            catch
            {
                continue;
            }
            AddIfMissingList(prop.Name, propValue, localMissing, hostMissing);
            CollectMissingMods(propValue, localMissing, hostMissing, seen, depth + 1);
        }
    }

    private static void AddIfMissingList(string memberName, object? value, System.Collections.Generic.List<string> localMissing, System.Collections.Generic.List<string> hostMissing)
    {
        var target = memberName.IndexOf("missingModsOnLocal", StringComparison.OrdinalIgnoreCase) >= 0
            ? localMissing
            : memberName.IndexOf("missingModsOnHost", StringComparison.OrdinalIgnoreCase) >= 0
                ? hostMissing
                : null;
        if (target is null)
        {
            return;
        }
        foreach (var item in FlattenStrings(value))
        {
            if (!target.Contains(item, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(item);
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<string> FlattenStrings(object? value)
    {
        if (value is null)
        {
            yield break;
        }
        if (value is string text)
        {
            foreach (var item in SplitPossibleList(text))
            {
                yield return item;
            }
            yield break;
        }
        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                foreach (var textItem in FlattenStrings(item))
                {
                    yield return textItem;
                }
            }
            yield break;
        }

        var rendered = SafeToString(value);
        foreach (var item in SplitPossibleList(rendered))
        {
            yield return item;
        }
    }

    private static System.Collections.Generic.IEnumerable<string> SplitPossibleList(string text)
    {
        foreach (var item in text.Split(['\n', '\r', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = item.Trim().Trim('[', ']', '"');
            if (trimmed.Length > 0 && !trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                yield return trimmed;
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<object?> EnumerateMemberValues(object value, Type type)
    {
        foreach (var field in SafeFields(type))
        {
            object? fieldValue = null;
            try
            {
                fieldValue = field.GetValue(value);
            }
            catch
            {
            }
            if (fieldValue is not null)
            {
                yield return fieldValue;
            }
        }
        foreach (var prop in SafeProperties(type))
        {
            if (prop.GetIndexParameters().Length != 0)
            {
                continue;
            }
            object? propValue = null;
            try
            {
                propValue = prop.GetValue(value);
            }
            catch
            {
            }
            if (propValue is not null)
            {
                yield return propValue;
            }
        }
    }

    private static FieldInfo[] SafeFields(Type type)
    {
        try
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch
        {
            return [];
        }
    }

    private static PropertyInfo[] SafeProperties(Type type)
    {
        try
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch
        {
            return [];
        }
    }

    private static bool IsSimple(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime);
    }

    private static string SafeToString(object? value)
    {
        try
        {
            return value?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private sealed class ReferenceEqualityComparer : System.Collections.Generic.IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

internal sealed class MismatchModResolver
{
    private readonly System.Collections.Generic.Dictionary<string, ModScanner.ModSummary> byId;
    private readonly System.Collections.Generic.Dictionary<string, ModScanner.ModSummary> byName;
    private readonly System.Collections.Generic.Dictionary<string, ModScanner.ModSummary> byWorkshopId;
    private readonly ModScanner.ModSummary[] mods;

    internal MismatchModResolver(ModScanner.ModSummary[] mods)
    {
        this.mods = mods;
        byId = BuildUniqueMap(mods, mod => mod.Id);
        byName = BuildUniqueMap(mods, mod => mod.Name);
        byWorkshopId = BuildUniqueMap(mods.Where(mod => !string.IsNullOrWhiteSpace(mod.WorkshopId)).ToArray(), mod => mod.WorkshopId);
    }

    public static MismatchModResolver Create()
    {
        try
        {
            return new MismatchModResolver(ModScanner.Discover());
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Build mismatch mod resolver failed: " + ex.Message);
            return new MismatchModResolver([]);
        }
    }

    public static bool SelfTest()
    {
        var mods = new[]
        {
            new ModScanner.ModSummary(
                "BaseLib",
                "BaseLib",
                ModSource.SteamWorkshop,
                "3746969593",
                "3.3.0",
                "",
                false,
                true,
                true,
                true,
                false,
                "self-test",
                [],
                [],
                [],
                [],
                ModScanner.HotApplyScope.RestartRequired),
            new ModScanner.ModSummary(
                "Act4Heart",
                "Act 4 Heart",
                ModSource.SteamWorkshop,
                "3747537811",
                "1.0.0",
                "",
                false,
                true,
                false,
                false,
                false,
                "self-test",
                ["BaseLib"],
                [],
                [],
                [],
                ModScanner.HotApplyScope.RestartRequired)
        };
        var resolver = new MismatchModResolver(mods);
        var byId = resolver.Describe("Act4Heart");
        var byName = resolver.Describe("Act 4 Heart");
        var byUrl = resolver.Describe("https://steamcommunity.com/sharedfiles/filedetails/?id=3747537811");
        var unknownUrl = resolver.Describe("https://steamcommunity.com/sharedfiles/filedetails/?id=1234567890");
        return byId.Contains("3747537811", StringComparison.Ordinal)
            && byName.Contains("Act4Heart", StringComparison.Ordinal)
            && byUrl.Contains("Act 4 Heart", StringComparison.Ordinal)
            && unknownUrl.Contains("1234567890", StringComparison.Ordinal);
    }

    public string Describe(string raw)
    {
        var token = CleanToken(raw);
        if (token.Length == 0)
        {
            return raw;
        }
        var workshopId = ExtractWorkshopId(token);
        var mod = Resolve(token, workshopId);
        if (mod is not null)
        {
            return FormatKnown(token, mod);
        }
        if (!string.IsNullOrWhiteSpace(workshopId))
        {
            return token + " [" + WorkshopUrl(workshopId) + "]";
        }
        return token;
    }

    public string BuildIndexReport()
    {
        if (mods.Length == 0)
        {
            return "<no local/subscribed mods discovered>";
        }
        return string.Join("\n", mods
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .Select(mod =>
            {
                var link = string.IsNullOrWhiteSpace(mod.WorkshopId) ? "" : " " + WorkshopUrl(mod.WorkshopId);
                return "- " + mod.Name + " [" + mod.Id + "] source=" + mod.Source + " version=" + mod.Version + link;
            }));
    }

    private ModScanner.ModSummary? Resolve(string token, string workshopId)
    {
        if (!string.IsNullOrWhiteSpace(workshopId) && byWorkshopId.TryGetValue(workshopId, out var byWorkshop))
        {
            return byWorkshop;
        }
        if (byId.TryGetValue(token, out var byExactId))
        {
            return byExactId;
        }
        if (byName.TryGetValue(token, out var byExactName))
        {
            return byExactName;
        }

        var normalized = Normalize(token);
        foreach (var mod in mods)
        {
            if (Normalize(mod.Id) == normalized || Normalize(mod.Name) == normalized)
            {
                return mod;
            }
        }
        return null;
    }

    private static string FormatKnown(string original, ModScanner.ModSummary mod)
    {
        var label = string.Equals(original, mod.Name, StringComparison.OrdinalIgnoreCase)
            ? mod.Name
            : original + " -> " + mod.Name;
        var parts = new System.Collections.Generic.List<string> { label + " [" + mod.Id + "]" };
        if (!string.IsNullOrWhiteSpace(mod.Version))
        {
            parts.Add("version " + mod.Version);
        }
        if (!string.IsNullOrWhiteSpace(mod.WorkshopId))
        {
            parts.Add(WorkshopUrl(mod.WorkshopId));
        }
        return string.Join(" | ", parts);
    }

    private static System.Collections.Generic.Dictionary<string, ModScanner.ModSummary> BuildUniqueMap(ModScanner.ModSummary[] mods, Func<ModScanner.ModSummary, string> keySelector)
    {
        var groups = mods
            .Select(mod => (Key: CleanToken(keySelector(mod)), Mod: mod))
            .Where(item => item.Key.Length > 0)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var map = new System.Collections.Generic.Dictionary<string, ModScanner.ModSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var items = group.ToArray();
            if (items.Length == 1)
            {
                map[group.Key] = items[0].Mod;
            }
        }
        return map;
    }

    private static string ExtractWorkshopId(string text)
    {
        const string idMarker = "id=";
        var idIndex = text.IndexOf(idMarker, StringComparison.OrdinalIgnoreCase);
        if (idIndex >= 0)
        {
            return ReadDigits(text, idIndex + idMarker.Length);
        }
        var digits = ReadDigits(text, 0);
        return digits.Length >= 8 ? digits : "";
    }

    private static string ReadDigits(string text, int start)
    {
        while (start < text.Length && !char.IsDigit(text[start]))
        {
            start++;
        }
        var end = start;
        while (end < text.Length && char.IsDigit(text[end]))
        {
            end++;
        }
        return end > start ? text[start..end] : "";
    }

    private static string CleanToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }
        return raw.Trim().Trim('[', ']', '"', '\'');
    }

    private static string Normalize(string text)
    {
        var cleaned = CleanToken(text);
        return new string(cleaned.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string WorkshopUrl(string workshopId) => "https://steamcommunity.com/sharedfiles/filedetails/?id=" + workshopId;
}

internal static class MultiplayerMismatchActions
{
    private const int MaxOpenLinks = 12;

    public static void OpenLastWorkshopLinks()
    {
        try
        {
            var links = ReadLastWorkshopLinks().Take(MaxOpenLinks + 1).ToArray();
            if (links.Length == 0)
            {
                NativeMessageBox.Show(
                    "No multiplayer mismatch Workshop links were found yet.\n\nTry joining the host once, then open this again after ModTheSpire2 records the mismatch report.",
                    "ModTheSpire2");
                return;
            }
            var toOpen = links.Take(MaxOpenLinks).ToArray();
            foreach (var link in toOpen)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = link,
                    UseShellExecute = true
                });
            }
            var extra = links.Length > MaxOpenLinks
                ? $"\n\nOnly the first {MaxOpenLinks} links were opened to avoid flooding Steam/browser windows."
                : "";
            NativeMessageBox.Show(
                $"Opened {toOpen.Length} Workshop link(s) from the latest multiplayer mismatch report." + extra,
                "ModTheSpire2");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Open mismatch Workshop links failed: " + ex);
            NativeMessageBox.Show("Could not open mismatch Workshop links:\n" + ex.Message, "ModTheSpire2");
        }
    }

    public static void CopyLastReport()
    {
        try
        {
            var path = GetReportPath();
            if (!File.Exists(path))
            {
                NativeMessageBox.Show(
                    "No multiplayer mismatch report was found yet.\n\nTry joining the host once, then copy the report after ModTheSpire2 records the mismatch.",
                    "ModTheSpire2");
                return;
            }

            var report = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(report))
            {
                NativeMessageBox.Show("The latest multiplayer mismatch report is empty.", "ModTheSpire2");
                return;
            }

            DisplayServer.ClipboardSet(report);
            NativeMessageBox.Show("Copied the latest multiplayer mismatch report to the clipboard.", "ModTheSpire2");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Copy mismatch report failed: " + ex);
            NativeMessageBox.Show("Could not copy the multiplayer mismatch report:\n" + ex.Message, "ModTheSpire2");
        }
    }

    public static string GetReportPath() => Path.Combine(LauncherActions.GetModDir(), "ModTheSpire2Data", "multiplayer-mismatch-last.txt");

    public static string[] ReadLastWorkshopLinks()
    {
        var path = GetReportPath();
        if (!File.Exists(path))
        {
            return [];
        }
        var text = File.ReadAllText(path);
        return ExtractWorkshopLinks(text).ToArray();
    }

    internal static System.Collections.Generic.IEnumerable<string> ExtractWorkshopLinks(string text)
    {
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     text,
                     @"https?://steamcommunity\.com/sharedfiles/filedetails/\?id=(\d+)",
                     System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var id = match.Groups[1].Value;
            if (id.Length == 0 || !seen.Add(id))
            {
                continue;
            }
            yield return "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id;
        }
    }

    public static bool SelfTest()
    {
        var links = ExtractWorkshopLinks(
            "one https://steamcommunity.com/sharedfiles/filedetails/?id=1111111111 " +
            "dup https://steamcommunity.com/sharedfiles/filedetails/?id=1111111111 " +
            "two https://steamcommunity.com/sharedfiles/filedetails/?id=2222222222").ToArray();
        return links.Length == 2
            && links[0].EndsWith("1111111111", StringComparison.Ordinal)
            && links[1].EndsWith("2222222222", StringComparison.Ordinal);
    }
}

internal static class UiLayoutStore
{
    private static readonly object Gate = new();
    private static System.Collections.Generic.Dictionary<string, StoredPosition>? cache;

    public static Vector2? Load(string key)
    {
        try
        {
            var all = LoadAll();
            return all.TryGetValue(key, out var stored) ? new Vector2(stored.X, stored.Y) : null;
        }
        catch (Exception ex)
        {
            CompanionLog.Write("UI layout load failed: " + ex.Message);
            return null;
        }
    }

    public static void Save(string key, Vector2 position)
    {
        try
        {
            lock (Gate)
            {
                var all = LoadAll();
                all[key] = new StoredPosition(position.X, position.Y);
                var path = GetPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var json = JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                CompanionLog.Write("UI layout saved " + key + "=" + position);
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("UI layout save failed: " + ex.Message);
        }
    }

    private static System.Collections.Generic.Dictionary<string, StoredPosition> LoadAll()
    {
        lock (Gate)
        {
            if (cache is not null)
            {
                return cache;
            }

            var path = GetPath();
            if (!File.Exists(path))
            {
                cache = new System.Collections.Generic.Dictionary<string, StoredPosition>(StringComparer.OrdinalIgnoreCase);
                return cache;
            }

            var json = File.ReadAllText(path);
            cache = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, StoredPosition>>(json)
                ?? new System.Collections.Generic.Dictionary<string, StoredPosition>(StringComparer.OrdinalIgnoreCase);
            return cache;
        }
    }

    private static string GetPath() => Path.Combine(LauncherActions.GetModDir(), "ModTheSpire2Data", "ui-layout.json");

    private sealed class StoredPosition
    {
        public StoredPosition()
        {
        }

        public StoredPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; set; }
        public float Y { get; set; }
    }
}

internal static class DraggableUi
{
    private const double LongPressSeconds = 0.35;

    public static void AttachLongPressDrag(Control surface, Control target, Control boundsParent, string layoutKey)
    {
        var state = new DragState();
        surface.MouseDefaultCursorShape = Control.CursorShape.Move;
        surface.AddChild(new DragReleaseWatcher(state, target, boundsParent, layoutKey));
        surface.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
            {
                if (mouse.Pressed)
                {
                    state.Pressed = true;
                    state.Dragging = false;
                    state.PressTicks = DateTime.UtcNow.Ticks;
                    state.PressGlobal = mouse.GlobalPosition;
                    state.GrabOffset = mouse.GlobalPosition - target.GlobalPosition;
                    surface.AcceptEvent();
                    return;
                }

                if (!state.Pressed)
                {
                    return;
                }

                var wasDragging = state.Dragging;
                state.Pressed = false;
                state.Dragging = false;
                if (wasDragging)
                {
                    SaveClamped(target, boundsParent, layoutKey);
                    surface.AcceptEvent();
                    return;
                }

                return;
            }

            if (input is InputEventMouseMotion motion && state.Pressed)
            {
                var held = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - state.PressTicks).TotalSeconds;
                if (!state.Dragging && held >= LongPressSeconds)
                {
                    state.Dragging = true;
                    CompanionLog.Write("UI drag started: " + layoutKey);
                }
                if (state.Dragging)
                {
                    MoveControl(target, boundsParent, motion.GlobalPosition - state.GrabOffset);
                    surface.AcceptEvent();
                }
            }
        };
    }

    public static void AttachDragSurface(Control surface, Control target, Control boundsParent, string layoutKey)
    {
        var state = new DragState();
        surface.MouseDefaultCursorShape = Control.CursorShape.Move;
        surface.TooltipText = "Drag to move. Position is remembered.";
        surface.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
            {
                if (mouse.Pressed)
                {
                    state.Pressed = true;
                    state.Dragging = true;
                    state.GrabOffset = mouse.GlobalPosition - target.GlobalPosition;
                    surface.AcceptEvent();
                    return;
                }

                if (state.Pressed)
                {
                    state.Pressed = false;
                    state.Dragging = false;
                    SaveClamped(target, boundsParent, layoutKey);
                    surface.AcceptEvent();
                }
                return;
            }

            if (input is InputEventMouseMotion motion && state.Pressed)
            {
                MoveControl(target, boundsParent, motion.GlobalPosition - state.GrabOffset);
                surface.AcceptEvent();
            }
        };
    }

    public static Vector2 ClampToParent(Vector2 position, Vector2 size, Vector2 parentSize)
    {
        var maxX = Math.Max(8, parentSize.X - size.X - 8);
        var maxY = Math.Max(8, parentSize.Y - size.Y - 8);
        return new Vector2(
            Math.Clamp(position.X, 8, maxX),
            Math.Clamp(position.Y, 8, maxY));
    }

    public static void MoveControl(Control target, Control boundsParent, Vector2 globalTopLeft)
    {
        var local = boundsParent.GetGlobalTransform().AffineInverse() * globalTopLeft;
        var parentSize = boundsParent.GetViewportRect().Size;
        target.Position = ClampToParent(local, target.Size, parentSize);
    }

    public static void SaveClamped(Control target, Control boundsParent, string layoutKey)
    {
        var parentSize = boundsParent.GetViewportRect().Size;
        target.Position = ClampToParent(target.Position, target.Size, parentSize);
        UiLayoutStore.Save(layoutKey, target.Position);
    }

    private static void FinishPress(DragState state, Control target, Control boundsParent, string layoutKey)
    {
        if (!state.Pressed)
        {
            return;
        }

        var wasDragging = state.Dragging;
        state.Pressed = false;
        state.Dragging = false;

        if (wasDragging)
        {
            SaveClamped(target, boundsParent, layoutKey);
        }
    }

    private sealed class DragState
    {
        public bool Pressed;
        public bool Dragging;
        public long PressTicks;
        public Vector2 PressGlobal;
        public Vector2 GrabOffset;
    }

    private sealed partial class DragReleaseWatcher : Node
    {
        private readonly DragState state;
        private readonly Control target;
        private readonly Control boundsParent;
        private readonly string layoutKey;

        public DragReleaseWatcher(DragState state, Control target, Control boundsParent, string layoutKey)
        {
            this.state = state;
            this.target = target;
            this.boundsParent = boundsParent;
            this.layoutKey = layoutKey;
        }

        public override void _Process(double delta)
        {
            if (!state.Pressed || Input.IsMouseButtonPressed(MouseButton.Left))
            {
                return;
            }

            FinishPress(state, target, boundsParent, layoutKey);
            CompanionLog.Write("UI press ended by process: " + layoutKey);
        }
    }
}

internal static class RestartToLauncher
{
    private static readonly Color BackstopColor = new(0.035f, 0.026f, 0.02f, 0.74f);
    private static readonly Color PanelColor = new(0.135f, 0.095f, 0.062f, 0.985f);

    public static void ShowConfirm(Node owner)
    {
        var modDir = LauncherActions.GetModDir();
        var launcher = LauncherActions.GetLauncherPath();
        var launchOption = LauncherActions.GetLaunchOption();
        var message =
            "Close the current game and open the ModTheSpire2 launcher?\n\n" +
            "The launcher will appear after the game has fully exited. From there, you can launch vanilla or choose which mods to enable for this session.\n\n" +
            "To show ModTheSpire2 every time you press Play in Steam, set this Steam launch option. Keep %command% exactly as written:\n\n" +
            launchOption + "\n\n" +
            "If the game needs extra launch arguments, add them after %command%. Example:\n\n" +
            launchOption + " --rendering-driver opengl3\n\n" +
            "Steam Workshop cannot change launch options automatically.";

        try
        {
            if (owner.FindChild("ModTheSpire2RestartDialog", recursive: true, owned: false) is Control existing)
            {
                existing.Show();
                existing.MoveToFront();
                existing.CallDeferred(Control.MethodName.GrabFocus);
                CompanionLog.Write("Restart dialog already open; focused existing overlay");
                return;
            }

            var dialog = new Control
            {
                Name = "ModTheSpire2RestartDialog",
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 420
            };
            void CloseDialog(string reason)
            {
                CompanionLog.Write("Restart dialog closed: " + reason);
                dialog.QueueFree();
            }

            owner.AddChild(dialog);
            var backstop = new ColorRect
            {
                Name = "Backstop",
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                Color = BackstopColor,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backstop.GuiInput += input =>
            {
                if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
                {
                    CloseDialog("backstop");
                }
            };
            dialog.AddChild(backstop);
            dialog.GuiInput += input =>
            {
                if (input is InputEventKey { Pressed: false, Keycode: Key.Escape })
                {
                    CloseDialog("escape");
                }
            };

            var restartMetrics = RestartDialogMetrics.From(dialog.GetViewportRect().Size);
            var panel = new Panel
            {
                Name = "Panel",
                AnchorsPreset = (int)Control.LayoutPreset.Center,
                CustomMinimumSize = restartMetrics.PanelSize,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ClipContents = true
            };
            panel.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(PanelColor, UiStyle.PanelBorderColor, 3, 8));
            dialog.AddChild(panel);

            var root = new VBoxContainer
            {
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                OffsetLeft = 22,
                OffsetTop = 22,
                OffsetRight = -22,
                OffsetBottom = -22,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            panel.AddChild(root);

            var title = new Label
            {
                Text = "ModTheSpire2 Launcher",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(restartMetrics.ContentWidth, 34)
            };
            UiStyle.ApplyTitle(title);
            root.AddChild(title);

            var scroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(restartMetrics.ContentWidth, restartMetrics.ScrollHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            root.AddChild(scroll);

            var content = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            scroll.AddChild(content);

            var body = new Label
            {
                Text = "Close the current game and open the ModTheSpire2 launcher?\n\nThe launcher will appear after the game has fully exited. From there, you can launch vanilla or choose which mods to enable for this session. Current game launch arguments are forwarded to the launcher so renderer flags such as --rendering-driver opengl3 are preserved.\n\nTo show ModTheSpire2 every time you press Play in Steam, set this Steam launch option. Keep %command% exactly as written:",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(restartMetrics.ContentWidth, 130),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            UiStyle.ApplyMutedLabel(body);
            content.AddChild(body);

            var optionBox = new PanelContainer
            {
                CustomMinimumSize = new Vector2(restartMetrics.ContentWidth, 72),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            optionBox.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(new Color(0.09f, 0.065f, 0.045f, 0.96f), new Color(0.36f, 0.245f, 0.13f, 0.9f), 1, 4));
            var optionLabel = new Label
            {
                Text = launchOption,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            UiStyle.ApplyBaseText(optionLabel);
            optionBox.AddChild(optionLabel);
            content.AddChild(optionBox);

            var note = new Label
            {
                Text = "Do not replace or remove %command%. If the game needs extra launch arguments, add them after %command%, for example: --rendering-driver opengl3. Steam Workshop cannot change launch options automatically.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(restartMetrics.ContentWidth, 58),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            UiStyle.ApplyMutedLabel(note);
            content.AddChild(note);

            var buttons = new FlowContainer
            {
                CustomMinimumSize = new Vector2(restartMetrics.ContentWidth, restartMetrics.ButtonAreaHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            root.AddChild(buttons);

            var copy = UiStyle.CreateButton("Copy Launch Option", 184, 44);
            copy.Pressed += () =>
            {
                DisplayServer.ClipboardSet(launchOption);
                copy.Text = "Copied";
            };
            buttons.AddChild(copy);

            var confirm = UiStyle.CreateButton("Close and Open Launcher", 220, 44);
            confirm.Pressed += () =>
            {
                CloseDialog("confirm");
                Restart(launcher, modDir);
            };
            buttons.AddChild(confirm);

            var cancel = UiStyle.CreateButton("Cancel", 132, 44);
            cancel.Pressed += () => CloseDialog("cancel");
            buttons.AddChild(cancel);

            CenterRestartDialog(dialog, panel, scroll, buttons, title, body, optionBox, note);
            dialog.Resized += () => CenterRestartDialog(dialog, panel, scroll, buttons, title, body, optionBox, note);
            dialog.CallDeferred(Control.MethodName.GrabFocus);
            CompanionLog.Write("Restart dialog shown");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Styled restart dialog failed: " + ex);
            if (NativeMessageBox.Confirm(message, "ModTheSpire2 Launcher"))
            {
                Restart(launcher, modDir);
            }
        }
    }

    private static void CenterRestartDialog(Control root, Control panel, Control scroll, Control buttons, Control title, Control body, Control optionBox, Control note)
    {
        var size = root.GetViewportRect().Size;
        var metrics = RestartDialogMetrics.From(size);
        panel.CustomMinimumSize = metrics.PanelSize;
        scroll.CustomMinimumSize = new Vector2(metrics.ContentWidth, metrics.ScrollHeight);
        buttons.CustomMinimumSize = new Vector2(metrics.ContentWidth, metrics.ButtonAreaHeight);
        title.CustomMinimumSize = new Vector2(metrics.ContentWidth, 34);
        body.CustomMinimumSize = new Vector2(metrics.ContentWidth, 130);
        optionBox.CustomMinimumSize = new Vector2(metrics.ContentWidth, 72);
        note.CustomMinimumSize = new Vector2(metrics.ContentWidth, 34);
        panel.Position = new Vector2(
            Math.Max(8, (size.X - metrics.PanelSize.X) * 0.5f),
            Math.Max(8, (size.Y - metrics.PanelSize.Y) * 0.5f));
        panel.Size = metrics.PanelSize;
    }

    private readonly record struct RestartDialogMetrics(Vector2 PanelSize, float ContentWidth, float ScrollHeight, float ButtonAreaHeight)
    {
        public static RestartDialogMetrics From(Vector2 viewport)
        {
            var availableWidth = Math.Max(300, viewport.X - 24);
            var availableHeight = Math.Max(260, viewport.Y - 24);
            var panelWidth = Math.Min(760, availableWidth);
            var panelHeight = Math.Min(430, availableHeight);
            var contentWidth = Math.Max(240, panelWidth - 74);
            var buttonAreaHeight = panelWidth < 650 ? 108 : 56;
            var scrollHeight = Math.Max(90, panelHeight - 146 - buttonAreaHeight);
            return new RestartDialogMetrics(
                new Vector2(panelWidth, panelHeight),
                contentWidth,
                scrollHeight,
                buttonAreaHeight);
        }
    }

    public static void Restart(string launcher, string modDir)
    {
        if (!File.Exists(launcher))
        {
            NativeMessageBox.Show("ModTheSpire2Launcher.exe was not found. Please check that the mod files are complete.", "ModTheSpire2");
            return;
        }

        try
        {
            var pid = Process.GetCurrentProcess().Id;
            var restartArgs = BuildRestartLauncherArguments(pid);
            Process.Start(new ProcessStartInfo
            {
                FileName = launcher,
                Arguments = restartArgs,
                WorkingDirectory = modDir,
                UseShellExecute = true
            });
            CompanionLog.Write("Started launcher for restart, args=" + restartArgs);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Start launcher failed: " + ex);
            NativeMessageBox.Show("Failed to start ModTheSpire2Launcher.exe:\n" + ex.Message, "ModTheSpire2");
            return;
        }

        try
        {
            NGame.Instance?.Quit();
        }
        catch (Exception ex)
        {
            CompanionLog.Write("NGame.Quit failed: " + ex);
            try
            {
                if (Engine.GetMainLoop() is SceneTree tree)
                {
                    tree.Quit(0);
                }
            }
            catch (Exception treeEx)
            {
                CompanionLog.Write("SceneTree.Quit failed: " + treeEx);
            }
        }
    }

    private static string BuildRestartLauncherArguments(int pid)
    {
        var current = System.Environment.GetCommandLineArgs();
        var args = "--wait-for-pid " + pid;
        if (current.Length == 0 || string.IsNullOrWhiteSpace(current[0]))
        {
            return args;
        }

        args += " -- " + QuoteArg(current[0]);
        for (var i = 1; i < current.Length; i++)
        {
            args += " " + QuoteArg(current[i]);
        }

        return args;
    }

    private static string QuoteArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        var needsQuotes = arg.Any(ch => char.IsWhiteSpace(ch) || ch == '"');
        if (!needsQuotes)
        {
            return arg;
        }

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}

internal static class ModManagementDialog
{
    private static readonly Color BackstopColor = new(0.035f, 0.026f, 0.02f, 0.74f);
    private static readonly Color PanelColor = new(0.135f, 0.095f, 0.062f, 0.985f);
    private static readonly Color SectionColor = new(0.23f, 0.155f, 0.085f, 0.96f);
    private static readonly Color RowColor = new(0.18f, 0.125f, 0.078f, 0.96f);
    private static readonly Color RowAltColor = new(0.155f, 0.105f, 0.066f, 0.96f);

    public static void Show(Node owner)
    {
        try
        {
            if (owner.FindChild("ModTheSpire2ManagementDialog", recursive: true, owned: false) is Control existing)
            {
                existing.Show();
                existing.MoveToFront();
                existing.CallDeferred(Control.MethodName.GrabFocus);
                CompanionLog.Write("Management dialog already open; focused existing overlay");
                return;
            }

            var gameState = GameSessionState.Detect();
            var mods = ModScanner.Discover(gameState);
            var enabledMods = mods
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var loadedMods = mods
                .Where(m => m.IsLoaded)
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var availableMods = mods
                .Where(m => !m.IsEnabled)
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var dialog = new Control
            {
                Name = "ModTheSpire2ManagementDialog",
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 5000
            };
            void CloseDialog(string reason)
            {
                CompanionLog.Write("Management dialog closed: " + reason);
                dialog.QueueFree();
            }

            owner.AddChild(dialog);
            var metrics = UiMetrics.From(dialog.GetViewportRect().Size);

            var backstop = new ColorRect
            {
                Name = "Backstop",
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                Color = BackstopColor,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backstop.GuiInput += input =>
            {
                if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
                {
                    CloseDialog("backstop");
                }
            };
            dialog.AddChild(backstop);
            dialog.GuiInput += input =>
            {
                if (input is InputEventKey { Pressed: false, Keycode: Key.Escape })
                {
                    CloseDialog("escape");
                }
            };

            var panel = new Panel
            {
                Name = "Panel",
                AnchorsPreset = (int)Control.LayoutPreset.Center,
                CustomMinimumSize = metrics.PanelSize,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ClipContents = true
            };
            panel.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(PanelColor, UiStyle.PanelBorderColor, 3, 8));
            dialog.AddChild(panel);

            var root = new VBoxContainer
            {
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                OffsetLeft = 22,
                OffsetTop = 22,
                OffsetRight = -22,
                OffsetBottom = -22,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            panel.AddChild(root);

            var header = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(metrics.ContentWidth, 42),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            DraggableUi.AttachDragSurface(header, panel, dialog, "management_panel");
            root.AddChild(header);

            var title = new Label
            {
                Text = "ModTheSpire2 Management",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(Math.Max(180, metrics.ContentWidth - 244), 34),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            UiStyle.ApplyTitle(title);
            header.AddChild(title);

            var topRestart = UiStyle.CreateButton("Close and Open Launcher", 196, 38);
            topRestart.TooltipText = "Close the game and open the launcher to change restart-required mods.";
            topRestart.Pressed += () => RestartToLauncher.ShowConfirm(dialog);
            header.AddChild(topRestart);

            var topClose = UiStyle.CreateIconButton("X", "Close ModTheSpire2 management and return to the game.");
            topClose.Pressed += () => CloseDialog("top-right");
            header.AddChild(topClose);

            var intro = new Label
            {
                Text = "Whole-mod enablement is managed before startup. To change enabled mods, close this game and reopen the ModTheSpire2 launcher, then start the game again with the selected profile.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(metrics.ContentWidth, 42)
            };
            UiStyle.ApplyMutedLabel(intro);
            root.AddChild(intro);

            root.AddChild(CreateStateSummaryPanel(gameState, mods.Length, enabledMods.Length, loadedMods.Length, metrics.ContentWidth));

            var scroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(metrics.ContentWidth, metrics.ScrollHeight),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            scroll.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(new Color(0.09f, 0.065f, 0.045f, 0.55f), new Color(0.31f, 0.21f, 0.12f, 0.9f), 1, 6));
            root.AddChild(scroll);

            var lists = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Theme = new Theme()
            };
            scroll.AddChild(lists);

            AddSectionHeader(lists, "Enabled For This Launch (" + enabledMods.Length + ")");
            if (enabledMods.Length == 0)
            {
                lists.AddChild(CreateInfoLabel("No enabled mods were found in the current settings."));
            }
            foreach (var mod in enabledMods.Take(60))
            {
                lists.AddChild(CreateCurrentModRow(mod));
            }
            if (enabledMods.Length > 60)
            {
                lists.AddChild(CreateInfoLabel("...and " + (enabledMods.Length - 60) + " more."));
            }

            AddSectionHeader(lists, "Available But Disabled (" + availableMods.Length + ")");
            if (availableMods.Length == 0)
            {
                lists.AddChild(CreateInfoLabel("No disabled mods were detected."));
            }
            foreach (var mod in availableMods.Take(60))
            {
                lists.AddChild(CreateCurrentModRow(mod));
            }
            if (availableMods.Length > 60)
            {
                lists.AddChild(CreateInfoLabel("...and " + (availableMods.Length - 60) + " more."));
            }

            AddLoadOrderSection(lists, mods);

            var buttons = new FlowContainer
            {
                CustomMinimumSize = new Vector2(metrics.ContentWidth, metrics.ButtonAreaHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            root.AddChild(buttons);

            var restartButton = CreateActionButton("Close and Open Launcher");
            restartButton.Pressed += () => RestartToLauncher.ShowConfirm(dialog);
            buttons.AddChild(restartButton);

            var close = CreateActionButton("Close");
            close.Pressed += () => CloseDialog("button");
            buttons.AddChild(close);

            dialog.Resized += () =>
            {
                var updated = UiMetrics.From(dialog.GetViewportRect().Size);
                panel.CustomMinimumSize = updated.PanelSize;
                panel.Size = updated.PanelSize;
                scroll.CustomMinimumSize = new Vector2(updated.ContentWidth, updated.ScrollHeight);
                buttons.CustomMinimumSize = new Vector2(updated.ContentWidth, updated.ButtonAreaHeight);
                header.CustomMinimumSize = new Vector2(updated.ContentWidth, 42);
                title.CustomMinimumSize = new Vector2(Math.Max(180, updated.ContentWidth - 244), 34);
                intro.CustomMinimumSize = new Vector2(updated.ContentWidth, 42);
                PositionPanel(dialog, panel);
            };
            PositionPanel(dialog, panel);
            dialog.CallDeferred(Control.MethodName.GrabFocus);
            CompanionLog.Write("Management dialog shown. detected=" + mods.Length + " enabled=" + enabledMods.Length + " loaded=" + loadedMods.Length);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Management dialog failed: " + ex);
            RestartToLauncher.ShowConfirm(owner);
        }
    }

    private static void AddSectionHeader(VBoxContainer parent, string text)
    {
        var container = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        container.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(SectionColor, UiStyle.AccentColor, 1, 5));
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 34),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyHeaderLabel(label);
        container.AddChild(label);
        parent.AddChild(container);
    }

    private static Button CreateActionButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(172, 44),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        UiStyle.ApplyButton(button);
        return button;
    }

    private static string BuildRestartWorkflowSummary(int detectedCount, int enabledCount, int loadedCount, GameSessionState gameState)
    {
        var runNote = gameState.IsActiveRun || gameState.HasUnfinishedRun
            ? "An active or unfinished run may still reference loaded mod content, so whole-mod changes should be made through a clean restart."
            : "Use the launcher to change enabled mods, load order, or profiles before the next game process starts.";

        return "Detected " + detectedCount + " mod(s), with " + enabledCount + " enabled in settings and " + loadedCount + " loaded this session. " + runNote;
    }

    private static Control CreateStateSummaryPanel(GameSessionState gameState, int detectedCount, int enabledCount, int loadedCount, float contentWidth)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(contentWidth, 92),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var border = gameState.IsSafeMainMenuWithoutRun
            ? UiStyle.SuccessColor
            : gameState.IsActiveRun || gameState.HasUnfinishedRun
                ? UiStyle.WarningColor
                : UiStyle.AccentColor;
        panel.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(new Color(0.105f, 0.074f, 0.049f, 0.96f), border, 1, 6));

        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        panel.AddChild(box);

        var headline = new Label
        {
            Text = BuildGameStateHeadline(gameState),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyHeaderLabel(headline);
        headline.AddThemeColorOverride("font_color", border);
        box.AddChild(headline);

        var signals = new Label
        {
            Text = BuildGameStateSignals(gameState),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyMutedLabel(signals);
        box.AddChild(signals);

        var summary = new Label
        {
            Text = BuildRestartWorkflowSummary(detectedCount, enabledCount, loadedCount, gameState),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyBaseText(summary);
        summary.AddThemeColorOverride("font_color", UiStyle.MutedTextColor);
        box.AddChild(summary);

        return panel;
    }

    private static string BuildGameStateHeadline(GameSessionState gameState)
    {
        if (gameState.IsSafeMainMenuWithoutRun)
        {
            return "Game state: Safe main menu";
        }
        if (gameState.IsActiveRun)
        {
            return "Game state: Active run";
        }
        if (gameState.HasUnfinishedRun)
        {
            return "Game state: Unfinished run available";
        }
        if (gameState.IsMainMenu)
        {
            return "Game state: Main menu, safety not fully confirmed";
        }
        return "Game state: Not a confirmed safe menu";
    }

    private static string BuildGameStateSignals(GameSessionState gameState) =>
        "Signals: main menu=" + GameSessionState.BoolText(gameState.IsMainMenu) +
        " (inside tree=" + GameSessionState.BoolText(gameState.IsMainMenuInsideTree) +
        ", visible=" + GameSessionState.BoolText(gameState.IsMainMenuVisible) + ")" +
        ", active run=" + GameSessionState.BoolText(gameState.IsActiveRun) +
        ", run save=" + GameSessionState.BoolText(gameState.HasRunSave) +
        ", multiplayer save=" + GameSessionState.BoolText(gameState.HasMultiplayerRunSave) +
        ", continue panel=" + GameSessionState.BoolText(gameState.HasContinueRunInfo) +
        ", saving=" + GameSessionState.BoolText(gameState.IsRunSaveTaskInProgress) +
        ", scene=" + (string.IsNullOrWhiteSpace(gameState.SceneName) ? "unknown" : gameState.SceneName) +
        ", root=" + (string.IsNullOrWhiteSpace(gameState.RootChildNames) ? "unknown" : gameState.RootChildNames) +
        ", reliability=" + (gameState.SignalsReliable ? "ok" : "partial");

    private static void TryRefreshNativeModdingScreen(Node context)
    {
        try
        {
            var screen = FindAncestor<NModdingScreen>(context);
            if (screen is null)
            {
                CompanionLog.Write("Native modding screen refresh skipped: screen not found");
                return;
            }

            var names = new[] { "Refresh", "Reload", "Rebuild", "OnSubmenuOpened", "_Ready" };
            foreach (var name in names)
            {
                var method = screen.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
                if (method is null)
                {
                    continue;
                }

                method.Invoke(screen, null);
                CompanionLog.Write("Native modding screen refresh invoked: " + name);
                ModdingScreenButton.TryAdd(screen, "hot-apply-refresh");
                return;
            }

            CompanionLog.Write("Native modding screen refresh unavailable: no known method");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Native modding screen refresh failed: " + ex.Message);
        }
    }

    private static T? FindAncestor<T>(Node node) where T : Node
    {
        for (var current = node; current is not null; current = current.GetParent())
        {
            if (current is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static Control CreateCurrentModRow(ModScanner.ModSummary mod)
    {
        var color = mod.IsLoaded ? UiStyle.SuccessColor : mod.IsEnabled ? UiStyle.AccentColor : UiStyle.MutedTextColor;
        var badge = mod.IsLoaded ? "Loaded" : mod.IsEnabled ? "Enabled" : "Disabled";
        return CreateModInfoRow(mod, badge, color, alternate: !mod.IsEnabled);
    }

    private static Control CreateModInfoRow(ModScanner.ModSummary mod, string badgeText, Color reasonColor, bool alternate = false)
    {
        var box = new VBoxContainer
        {
            TooltipText = DependencyTooltip(mod),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        box.AddChild(header);

        var title = new Label
        {
            Text = mod.Name + " [" + mod.Id + "]",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyBaseText(title);
        header.AddChild(title);

        header.AddChild(CreateStatusBadge(badgeText, reasonColor));

        var detail = new Label
        {
            Text = BuildModDetailText(mod),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyMutedLabel(detail);
        detail.AddThemeColorOverride("font_color", reasonColor);
        box.AddChild(detail);

        return WrapRow(box, alternate);
    }

    private static Control CreateStatusBadge(string text, Color color)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(96, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
        };
        panel.AddThemeStyleboxOverride("panel", UiStyle.CreateCompactStyle(new Color(color.R * 0.22f, color.G * 0.22f, color.B * 0.22f, 0.96f), color, 1, 4));

        var badge = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(78, 20),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        badge.AddThemeColorOverride("font_color", UiStyle.TextColor);
        panel.AddChild(badge);
        return panel;
    }

    private static string BuildModDetailText(ModScanner.ModSummary mod)
    {
        var parts = new System.Collections.Generic.List<string>
        {
            mod.Reason,
            "enabled=" + GameSessionState.BoolText(mod.IsEnabled),
            "loaded=" + GameSessionState.BoolText(mod.IsLoaded)
        };
        if (mod.Dependencies.Length > 0)
        {
            parts.Add("depends on " + string.Join(", ", mod.Dependencies));
        }
        if (!string.IsNullOrWhiteSpace(mod.MinGameVersion))
        {
            parts.Add("requires STS2 >= " + mod.MinGameVersion);
        }
        if (mod.LoadAfter.Length > 0)
        {
            parts.Add("loads after " + string.Join(", ", mod.LoadAfter));
        }
        if (mod.LoadBefore.Length > 0)
        {
            parts.Add("loads before " + string.Join(", ", mod.LoadBefore));
        }
        return string.Join("  |  ", parts);
    }

    private static Control CreateInfoLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyMutedLabel(label);
        return label;
    }

    private static PanelContainer WrapRow(Control child, bool alternate = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 42),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(alternate ? RowAltColor : RowColor, new Color(0.36f, 0.245f, 0.13f, 0.8f), 1, 4));
        child.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.AddChild(child);
        return panel;
    }

    private static void AddLoadOrderSection(VBoxContainer parent, ModScanner.ModSummary[] mods)
    {
        try
        {
            AddLoadOrderSectionCore(parent, mods);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Load order section failed: " + ex);
            AddSectionHeader(parent, "Load Order");
            parent.AddChild(new Label
            {
                Text = "Load-order controls are unavailable in this session. Use the launcher to change order.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 44),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            });
        }
    }

    private static void AddLoadOrderSectionCore(VBoxContainer parent, ModScanner.ModSummary[] mods)
    {
        AddSectionHeader(parent, "Load Order");
        var order = LoadOrderManager.CreateInitialOrder(mods);
        var list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        UiStyle.ApplyItemList(list);
        parent.AddChild(list);

        var status = new Label
        {
            Text = order.Count == 0 ? "No enabled mods to order." : "Enabled mods are shown in the order ModTheSpire2 will save for the launcher.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 34),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyMutedLabel(status);
        parent.AddChild(status);

        void RefreshOrderList(int selected = -1)
        {
            list.Clear();
            for (var i = 0; i < order.Count; i++)
            {
                var mod = order[i];
                var prefix = mod.Dependencies.Length == 0 ? "" : "    ";
                list.AddItem(prefix + mod.Name + " [" + mod.Id + "]");
            }
            if (selected >= 0 && selected < order.Count)
            {
                list.Select(selected);
                list.EnsureCurrentIsVisible();
            }
        }

        int SelectedIndex()
        {
            var selected = list.GetSelectedItems();
            return selected.Length == 0 ? -1 : selected[0];
        }

        var row = new FlowContainer
        {
            CustomMinimumSize = new Vector2(0, 96),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        parent.AddChild(row);

        var up = CreateActionButton("Move Up");
        up.Pressed += () =>
        {
            var index = SelectedIndex();
            if (LoadOrderManager.TryMove(order, index, -1, out var message))
            {
                RefreshOrderList(index - 1);
            }
            status.Text = message;
        };
        row.AddChild(up);

        var down = CreateActionButton("Move Down");
        down.Pressed += () =>
        {
            var index = SelectedIndex();
            if (LoadOrderManager.TryMove(order, index, 1, out var message))
            {
                RefreshOrderList(index + 1);
            }
            status.Text = message;
        };
        row.AddChild(down);

        var save = CreateActionButton("Save Order");
        save.Pressed += () =>
        {
            status.Text = LoadOrderManager.Save(order);
        };
        row.AddChild(save);

        var reset = CreateActionButton("Reset Order");
        reset.Pressed += () =>
        {
            status.Text = LoadOrderManager.Reset();
            order.Clear();
            order.AddRange(LoadOrderManager.CreateDefaultOrder(mods));
            RefreshOrderList(order.Count > 0 ? 0 : -1);
        };
        row.AddChild(reset);

        RefreshOrderList(order.Count > 0 ? 0 : -1);
        CompanionLog.Write("Load order section shown entries=" + order.Count);
    }

    private static void PositionPanel(Control root, Control panel)
    {
        var size = root.GetViewportRect().Size;
        var panelSize = UiMetrics.From(size).PanelSize;
        panel.CustomMinimumSize = panelSize;
        panel.Size = panelSize;
        var saved = UiLayoutStore.Load("management_panel");
        panel.Position = saved is Vector2 position
            ? DraggableUi.ClampToParent(position, panelSize, size)
            : new Vector2(
                Math.Max(8, (size.X - panelSize.X) * 0.5f),
                Math.Max(8, (size.Y - panelSize.Y) * 0.5f));
        panel.Size = panelSize;
    }

    private readonly record struct UiMetrics(Vector2 PanelSize, float ContentWidth, float ScrollHeight, float ButtonAreaHeight)
    {
        public static UiMetrics From(Vector2 viewport)
        {
            var availableWidth = Math.Max(300, viewport.X - 24);
            var availableHeight = Math.Max(280, viewport.Y - 24);
            var panelWidth = Math.Min(960, availableWidth);
            var panelHeight = Math.Min(690, availableHeight);
            var contentWidth = Math.Max(240, panelWidth - 74);
            var buttonAreaHeight = panelWidth < 880 ? 104 : 56;
            var scrollHeight = Math.Max(90, panelHeight - 218 - buttonAreaHeight);
            return new UiMetrics(
                new Vector2(panelWidth, panelHeight),
                contentWidth,
                scrollHeight,
                buttonAreaHeight);
        }
    }

    private static bool DependenciesSatisfied(ModScanner.ModSummary mod, System.Collections.Generic.Dictionary<string, CheckBox> selectedHot)
    {
        foreach (var dependency in mod.Dependencies)
        {
            if (selectedHot.TryGetValue(dependency, out var checkBox) && !checkBox.ButtonPressed)
            {
                return false;
            }
        }
        return true;
    }

    private static string DependencyTooltip(ModScanner.ModSummary mod) =>
        mod.Reason +
        "\nWhole-mod changes require closing the game and reopening the launcher." +
        "\nEnabled in settings: " + GameSessionState.BoolText(mod.IsEnabled) +
        "\nLoaded this session: " + GameSessionState.BoolText(mod.IsLoaded) +
        (mod.Dependencies.Length == 0 ? "" : "\nDepends on: " + string.Join(", ", mod.Dependencies)) +
        (mod.LoadAfter.Length == 0 ? "" : "\nLoads after: " + string.Join(", ", mod.LoadAfter)) +
        (mod.LoadBefore.Length == 0 ? "" : "\nLoads before: " + string.Join(", ", mod.LoadBefore));
}

internal static class UiStyle
{
    public static readonly Color PanelBorderColor = new(0.68f, 0.47f, 0.23f, 1f);
    public static readonly Color TextColor = new(0.93f, 0.86f, 0.72f, 1f);
    public static readonly Color MutedTextColor = new(0.76f, 0.67f, 0.52f, 1f);
    public static readonly Color AccentColor = new(0.84f, 0.58f, 0.27f, 1f);
    public static readonly Color WarningColor = new(0.96f, 0.72f, 0.32f, 1f);
    public static readonly Color SuccessColor = new(0.63f, 0.82f, 0.49f, 1f);
    private static readonly Color ButtonColor = new(0.31f, 0.21f, 0.115f, 1f);
    private static readonly Color ButtonHoverColor = new(0.42f, 0.28f, 0.14f, 1f);
    private static readonly Color ButtonPressedColor = new(0.22f, 0.145f, 0.082f, 1f);

    public static StyleBoxFlat CreatePanelStyle(Color background, Color border, int borderWidth, int radius) =>
        new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 18,
            ContentMarginTop = 18,
            ContentMarginRight = 18,
            ContentMarginBottom = 18
        };

    public static StyleBoxFlat CreateCompactStyle(Color background, Color border, int borderWidth, int radius) =>
        new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 8,
            ContentMarginTop = 3,
            ContentMarginRight = 8,
            ContentMarginBottom = 3
        };

    public static void ApplyBaseText(Control control)
    {
        control.AddThemeColorOverride("font_color", TextColor);
        control.AddThemeColorOverride("font_focus_color", TextColor);
        control.AddThemeColorOverride("font_hover_color", TextColor);
    }

    public static void ApplyMutedLabel(Label label)
    {
        label.AddThemeColorOverride("font_color", MutedTextColor);
    }

    public static void ApplyHeaderLabel(Label label)
    {
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.015f, 0.01f, 1f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
    }

    public static void ApplyTitle(Label label)
    {
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.015f, 0.01f, 1f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
    }

    public static void ApplyButton(Button button)
    {
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(ButtonColor, AccentColor, 1, 4));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(ButtonHoverColor, AccentColor, 1, 4));
        button.AddThemeStyleboxOverride("pressed", CreatePanelStyle(ButtonPressedColor, AccentColor, 1, 4));
        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeColorOverride("font_hover_color", TextColor);
        button.AddThemeColorOverride("font_pressed_color", AccentColor);
        button.AddThemeColorOverride("font_focus_color", TextColor);
    }

    public static Button CreateButton(string text, float width, float height)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, height),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        ApplyButton(button);
        return button;
    }

    public static Button CreateIconButton(string text, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(42, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
        };
        ApplyButton(button);
        return button;
    }

    public static void ApplyItemList(ItemList list)
    {
        list.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.11f, 0.075f, 0.048f, 0.96f), new Color(0.36f, 0.245f, 0.13f, 0.9f), 1, 4));
        list.AddThemeStyleboxOverride("selected", CreatePanelStyle(new Color(0.35f, 0.235f, 0.12f, 0.96f), AccentColor, 1, 3));
        list.AddThemeColorOverride("font_color", TextColor);
        list.AddThemeColorOverride("font_selected_color", TextColor);
    }
}

internal sealed record GameSessionState(
    bool IsMainMenu,
    bool IsMainMenuInsideTree,
    bool IsMainMenuVisible,
    bool IsActiveRun,
    bool HasUnfinishedRun,
    bool HasRunSave,
    bool HasMultiplayerRunSave,
    bool HasContinueRunInfo,
    bool IsRunSaveTaskInProgress,
    bool SignalsReliable,
    string SceneName,
    string RootChildNames,
    string Description)
{
    public bool IsSafeMainMenuWithoutRun => SignalsReliable && IsMainMenu && IsMainMenuInsideTree && IsMainMenuVisible && !IsActiveRun && !HasUnfinishedRun;

    public static GameSessionState Detect()
    {
        var reliable = true;
        var isMainMenu = false;
        var isActiveRun = false;
        var hasUnfinishedRun = false;
        var hasRunSave = false;
        var hasMultiplayerRunSave = false;
        var hasContinueRunInfo = false;
        var runSaveTaskInProgress = false;
        var mainMenuInsideTree = false;
        var mainMenuVisible = false;
        var sceneName = "";
        var rootChildNames = "";
        try
        {
            var game = NGame.Instance;
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                sceneName = SafeNodeName(tree.CurrentScene);
                rootChildNames = SafeRootChildNames(tree.Root);
            }

            if (game?.MainMenu is { } mainMenu)
            {
                mainMenuInsideTree = mainMenu.IsInsideTree();
                mainMenuVisible = mainMenu is CanvasItem canvas ? canvas.IsVisibleInTree() : mainMenuInsideTree;
            }
            isMainMenu = game?.MainMenu is not null && mainMenuInsideTree && mainMenuVisible && game.CurrentRunNode is null;
            isActiveRun = game?.CurrentRunNode is not null;
            hasContinueRunInfo = game?.MainMenu?.ContinueRunInfo?.HasResult == true;
        }
        catch (Exception ex)
        {
            reliable = false;
            CompanionLog.Write("Game node state detection failed: " + ex.Message);
        }

        try
        {
            var run = RunManager.Instance;
            if (run is not null)
            {
                isActiveRun = isActiveRun || (run.IsInProgress && !run.IsGameOver && !run.IsAbandoned);
            }
        }
        catch (Exception ex)
        {
            reliable = false;
            CompanionLog.Write("Run state detection failed: " + ex.Message);
        }

        try
        {
            var saves = SaveManager.Instance;
            if (saves is not null)
            {
                hasRunSave = saves.HasRunSave;
                hasMultiplayerRunSave = saves.HasMultiplayerRunSave;
                runSaveTaskInProgress = saves.CurrentRunSaveTask is { IsCompleted: false };
            }
        }
        catch (Exception ex)
        {
            reliable = false;
            CompanionLog.Write("Save run state detection failed: " + ex.Message);
        }

        hasUnfinishedRun = hasRunSave || hasMultiplayerRunSave || hasContinueRunInfo || runSaveTaskInProgress;
        var description =
            "main menu=" + BoolText(isMainMenu) +
            " (inside tree=" + BoolText(mainMenuInsideTree) +
            ", visible=" + BoolText(mainMenuVisible) + ")" +
            ", active run=" + BoolText(isActiveRun) +
            ", unfinished run=" + BoolText(hasUnfinishedRun) +
            " (run save=" + BoolText(hasRunSave) +
            ", multiplayer save=" + BoolText(hasMultiplayerRunSave) +
            ", continue panel=" + BoolText(hasContinueRunInfo) +
            ", saving=" + BoolText(runSaveTaskInProgress) + ")" +
            ", scene=" + (string.IsNullOrWhiteSpace(sceneName) ? "unknown" : sceneName) +
            ", root children=" + (string.IsNullOrWhiteSpace(rootChildNames) ? "unknown" : rootChildNames) +
            ", signals=" + (reliable ? "ok" : "partial");
        CompanionLog.Write("Detected game state: " + description);
        return new GameSessionState(
            isMainMenu,
            mainMenuInsideTree,
            mainMenuVisible,
            isActiveRun,
            hasUnfinishedRun,
            hasRunSave,
            hasMultiplayerRunSave,
            hasContinueRunInfo,
            runSaveTaskInProgress,
            reliable,
            sceneName,
            rootChildNames,
            description);
    }

    public static string BoolText(bool value) => value ? "yes" : "no";

    private static string SafeNodeName(Node? node)
    {
        try
        {
            return node is null ? "" : node.Name.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static string SafeRootChildNames(Node? root)
    {
        try
        {
            if (root is null)
            {
                return "";
            }

            return string.Join(",",
                root.GetChildren()
                    .OfType<Node>()
                    .Take(8)
                    .Select(child => child.Name.ToString()));
        }
        catch
        {
            return "";
        }
    }
}

internal static class ModScanner
{
    public enum HotApplyScope
    {
        RestartRequired,
        RuntimeSafe,
        MainMenuNoRun
    }

    public sealed record DependencyVersionRequirement(string Id, string MinVersion);
    public sealed record ModSummary(string Id, string Name, ModSource Source, string WorkshopId, string Version, string MinGameVersion, bool IsHotCandidate, bool IsEnabled, bool IsLoaded, bool IsFrameworkRoot, bool IsRunSafeRuntime, string Reason, string[] Dependencies, DependencyVersionRequirement[] DependencyVersionRequirements, string[] LoadAfter, string[] LoadBefore, HotApplyScope Scope);
    private static readonly System.Collections.Generic.Dictionary<string, string> SessionRestartRequired = new(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Collections.Generic.HashSet<string> KnownMainMenuContentMods = new(StringComparer.OrdinalIgnoreCase)
    {
        // WuWa Ancients exposes a main-menu setting for future-run ancient/event generation.
        // That is evidence for future per-mod setting integration, not proof that toggling the loaded mod itself is safe.
    };

    public static void MarkSessionRestartRequired(System.Collections.Generic.IEnumerable<string> ids, string reason)
    {
        var marked = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var id in marked)
        {
            SessionRestartRequired[id] = reason;
        }
        CompanionLog.Write("Session restart-required marked: " + string.Join(", ", marked));
    }

    public static ModSummary[] Discover()
    {
        return Discover(GameSessionState.Detect());
    }

    public static ModSummary[] Discover(GameSessionState gameState)
    {
        var modDir = LauncherActions.GetModDir();
        var gameDir = FindGameDir(modDir);
        var currentGameVersion = ReadCurrentGameVersion(gameDir);
        var enabled = ReadEnabledMods();
        var loaded = ReadRuntimeLoadedModHints();
        var roots = new[]
        {
            new ScanRoot(Path.Combine(gameDir, "mods"), ModSource.ModsDirectory),
            new ScanRoot(FindWorkshopDir(gameDir), ModSource.SteamWorkshop)
        };

        var discovered = roots
            .Where(root => Directory.Exists(root.Path))
            .SelectMany(root => EnumerateManifestFiles(root.Path)
                .Where(path => !IsRuntimeDataPath(path))
                .Select(path => new ManifestPath(path, root.Source)))
            .Select(path => TryReadManifest(path.Path, path.Source, enabled, loaded))
            .Where(mod => mod is not null)
            .Cast<ModSummary>()
            .GroupBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        discovered = CanonicalizeDependencies(discovered);
        return ApplyDependencyClassification(discovered, gameState, currentGameVersion);
    }

    private static ModSummary[] CanonicalizeDependencies(ModSummary[] mods)
    {
        var byId = mods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var nameGroups = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.Name))
            .GroupBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var workshopGroups = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.WorkshopId))
            .GroupBy(mod => mod.WorkshopId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        return mods
            .Select(mod =>
            {
                var changed = false;
                var dependencies = mod.Dependencies
                    .Select(dep => CanonicalizeReference(dep, byId, nameGroups, workshopGroups, ref changed))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var dependencyVersionRequirements = mod.DependencyVersionRequirements
                    .Select(requirement =>
                    {
                        var id = CanonicalizeReference(requirement.Id, byId, nameGroups, workshopGroups, ref changed);
                        return requirement with { Id = id };
                    })
                    .Where(requirement => !string.IsNullOrWhiteSpace(requirement.Id) && !string.IsNullOrWhiteSpace(requirement.MinVersion))
                    .GroupBy(requirement => requirement.Id + "\u001f" + requirement.MinVersion, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToArray();
                var loadAfter = mod.LoadAfter
                    .Select(dep => CanonicalizeReference(dep, byId, nameGroups, workshopGroups, ref changed))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var loadBefore = mod.LoadBefore
                    .Select(dep => CanonicalizeReference(dep, byId, nameGroups, workshopGroups, ref changed))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return changed ? mod with { Dependencies = dependencies, DependencyVersionRequirements = dependencyVersionRequirements, LoadAfter = loadAfter, LoadBefore = loadBefore } : mod;
            })
            .ToArray();
    }

    private static string CanonicalizeReference(
        string reference,
        System.Collections.Generic.Dictionary<string, ModSummary> byId,
        System.Collections.Generic.Dictionary<string, ModSummary[]> nameGroups,
        System.Collections.Generic.Dictionary<string, ModSummary[]> workshopGroups,
        ref bool changed)
    {
        if (byId.ContainsKey(reference))
        {
            return reference;
        }

        if (nameGroups.TryGetValue(reference, out var matches) && matches.Length == 1)
        {
            changed = true;
            return matches[0].Id;
        }

        if (workshopGroups.TryGetValue(reference, out var workshopMatches) && workshopMatches.Length == 1)
        {
            changed = true;
            return workshopMatches[0].Id;
        }

        return reference;
    }

    private static ModSummary? TryReadManifest(
        string path,
        ModSource source,
        System.Collections.Generic.HashSet<string> enabled,
        System.Collections.Generic.HashSet<string> loaded)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var id = GetString(root, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = IsJsonManifestFile(path) ? GetPckNameFallbackId(path, root) : null;
                if (string.IsNullOrWhiteSpace(id))
                {
                    return null;
                }
            }

            var name = GetString(root, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = id;
            }

            var manifestDir = Path.GetDirectoryName(path) ?? "";
            if (!IsPlausibleManifestCandidate(path, id, root, manifestDir))
            {
                CompanionLog.Write("Ignored non-manifest JSON candidate: " + path + " id=" + id);
                return null;
            }

            var hasDll = GetBool(root, "has_dll") || ContainsPayloadFile(manifestDir, "*.dll");
            var hasPck = GetBool(root, "has_pck") || ContainsPayloadFile(manifestDir, "*.pck");
            var affectsGameplay = GetBool(root, "affects_gameplay", defaultValue: true);
            var declaresHot = GetBool(root, "hot_apply") || GetBool(root, "hot_reload");
            var declaredScope = GetString(root, "hot_apply_scope") ?? GetString(root, "hot_reload_scope");
            var dependencies = GetDependencies(root);
            var dependencyVersionRequirements = GetDependencyVersionRequirements(root);
            var loadAfter = GetLoadAfter(root);
            var loadBefore = GetLoadBefore(root);
            var version = GetString(root, "version") ?? "";
            var minGameVersion = GetMinGameVersion(root);
            var isSelf = id.Equals("ModTheSpire2", StringComparison.OrdinalIgnoreCase);
            var isFrameworkRoot = IsFrameworkMod(id) || AnyFrameworkRootManifestToken(root);
            var restartRequiredTagReason = GetRestartRequiredManifestReason(root);
            var isRestartRequiredTagged = isFrameworkRoot || restartRequiredTagReason is not null;
            var isMainMenuContent = IsMainMenuContentCandidate(root, id, declaredScope);
            var isRuntimeSafeDeclared = IsRuntimeSafeCandidate(root, declaredScope);
            var isRunSafeRuntime = IsRunSafeRuntimeCandidate(root, declaredScope);
            var scope = HotApplyScope.RestartRequired;
            var hot = false;
            string reason;
            if (isSelf)
            {
                reason = "launcher/UI patch requires restart";
            }
            else if (isRestartRequiredTagged)
            {
                reason = isFrameworkRoot ? "framework DLL; restart required" : restartRequiredTagReason!;
            }
            else if (isMainMenuContent)
            {
                scope = HotApplyScope.MainMenuNoRun;
                reason = "main menu only; state check pending";
            }
            else if ((declaresHot || isRuntimeSafeDeclared || !affectsGameplay) && !hasDll && !hasPck && !affectsGameplay)
            {
                scope = HotApplyScope.RuntimeSafe;
                hot = true;
                reason = isRuntimeSafeDeclared || declaresHot ? "manifest declares runtime-safe hot-apply" : "utility/config candidate";
            }
            else
            {
                reason = hasDll || hasPck ? "DLL/PCK or unknown startup behavior" : "gameplay or unknown behavior";
            }

            var isLoaded =
                loaded.Contains(id) ||
                loaded.Contains(name!) ||
                Directory.EnumerateFiles(manifestDir, "*.dll", SearchOption.TopDirectoryOnly)
                    .Select(file => Path.GetFileNameWithoutExtension(file))
                    .Any(loaded.Contains);

            return new ModSummary(id, name!, source, GetWorkshopIdFromPath(path, source), version, minGameVersion, hot, enabled.Contains(id), isLoaded, isFrameworkRoot, isRunSafeRuntime, reason, dependencies, dependencyVersionRequirements, loadAfter, loadBefore, scope);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Manifest scan failed for " + path + ": " + ex.Message);
            return TryReadInvalidManifestFallback(path, source, enabled, loaded);
        }
    }

    private static ModSummary? TryReadInvalidManifestFallback(
        string path,
        ModSource source,
        System.Collections.Generic.HashSet<string> enabled,
        System.Collections.Generic.HashSet<string> loaded)
    {
        if (!IsJsonManifestFile(path) && !IsSidecarManifestFile(path))
        {
            return null;
        }

        var id = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(id) ||
            id.Equals("mod_manifest", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("variants", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var manifestDir = Path.GetDirectoryName(path) ?? "";
        if (!ContainsPayloadFile(manifestDir, "*.dll") && !ContainsPayloadFile(manifestDir, "*.pck"))
        {
            return null;
        }

        CompanionLog.Write("Using invalid manifest fallback for " + path + " as " + id);
        var isLoaded =
            loaded.Contains(id) ||
            Directory.EnumerateFiles(manifestDir, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(file => Path.GetFileNameWithoutExtension(file))
                .Any(loaded.Contains);
        return new ModSummary(
            id,
            id,
            source,
            GetWorkshopIdFromPath(path, source),
            "",
            "",
            false,
            enabled.Contains(id),
            isLoaded,
            false,
            false,
            "invalid manifest; restart required",
            [],
            [],
            [],
            [],
            HotApplyScope.RestartRequired);
    }

    private static bool IsRuntimeDataPath(string path)
    {
        try
        {
            for (var dir = new FileInfo(path).Directory; dir is not null; dir = dir.Parent)
            {
                if (dir.Name.Equals("ModTheSpire2Data", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool IsPlausibleManifestCandidate(string path, string id, JsonElement root, string manifestDir)
    {
        if (IsSidecarManifestFile(path))
        {
            return true;
        }

        var fileName = Path.GetFileName(path);
        var stem = Path.GetFileNameWithoutExtension(path);
        var parentName = new DirectoryInfo(manifestDir).Name;
        if (string.Equals(fileName, "mod_manifest.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "mod_mainfest.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stem, parentName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stem, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stem, GetString(root, "pck_name"), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (GetBool(root, "has_dll") || GetBool(root, "has_pck"))
        {
            return true;
        }

        if (DirectoryHasSidecarManifest(path, manifestDir))
        {
            return true;
        }

        return ContainsPayloadFile(manifestDir, "*.dll") || ContainsPayloadFile(manifestDir, "*.pck");
    }

    private static bool DirectoryHasSidecarManifest(string path, string manifestDir)
    {
        if (string.IsNullOrWhiteSpace(manifestDir))
        {
            return false;
        }

        foreach (var other in Directory.EnumerateFiles(manifestDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(other, path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileName(other);
            if (string.Equals(name, "mod_manifest.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "mod_mainfest.json", StringComparison.OrdinalIgnoreCase) ||
                IsSidecarManifestFile(other))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPayloadFile(string manifestDir, string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(manifestDir) || !Directory.Exists(manifestDir))
        {
            return false;
        }

        return Directory.EnumerateFiles(manifestDir, searchPattern, SearchOption.AllDirectories)
            .Any(path => !IsRuntimeDataPath(path));
    }

    private static string GetWorkshopIdFromPath(string path, ModSource source)
    {
        if (source != ModSource.SteamWorkshop)
        {
            return "";
        }

        try
        {
            for (var dir = new FileInfo(path).Directory; dir is not null; dir = dir.Parent)
            {
                if (dir.Parent?.Name.Equals("2868840", StringComparison.OrdinalIgnoreCase) == true &&
                    dir.Name.All(char.IsDigit))
                {
                    return dir.Name;
                }
            }
        }
        catch
        {
        }

        return "";
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateManifestFiles(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (IsJsonManifestFile(path) || IsSidecarManifestFile(path))
            {
                yield return path;
            }
        }
    }

    private static bool IsJsonManifestFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSidecarManifestFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".manifest", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetPckNameFallbackId(string path, JsonElement root)
    {
        var pckName = GetString(root, "pck_name");
        if (string.IsNullOrWhiteSpace(pckName))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return null;
        }

        try
        {
            foreach (var jsonPath in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .Where(manifestPath => IsJsonManifestFile(manifestPath) || IsSidecarManifestFile(manifestPath)))
            {
                if (string.Equals(jsonPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                if (!string.IsNullOrWhiteSpace(GetString(doc.RootElement, "id")))
                {
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("pck_name fallback scan failed for " + path + ": " + ex.Message);
            return null;
        }

        return pckName;
    }

    private static System.Collections.Generic.HashSet<string> ReadEnabledMods()
    {
        var enabled = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var manager = SaveManager.Instance;
            var modList = manager?.SettingsSave?.ModSettings?.ModList;
            if (modList is null)
            {
                return enabled;
            }
            foreach (var item in modList)
            {
                if (item.IsEnabled && !string.IsNullOrWhiteSpace(item.Id))
                {
                    enabled.Add(item.Id);
                }
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Read enabled mods failed: " + ex);
        }
        return enabled;
    }

    private static System.Collections.Generic.HashSet<string> ReadRuntimeLoadedModHints()
    {
        var loaded = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ModTheSpire2"
        };

        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    loaded.Add(name);
                }
            }
            CompanionLog.Write("Runtime loaded assembly hints=" + loaded.Count);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Read runtime loaded hints failed: " + ex.Message);
        }

        return loaded;
    }

    private static string[] GetDependencies(JsonElement root)
    {
        var result = new System.Collections.Generic.List<string>();
        foreach (var propertyName in new[] { "dependencies", "requires", "required_mods", "requiredMods" })
        {
            if (!root.TryGetProperty(propertyName, out var dependencies))
            {
                continue;
            }

            if (dependencies.ValueKind == JsonValueKind.Array)
            {
                result.AddRange(dependencies.EnumerateArray().Select(ReadDependencyId).Where(id => !string.IsNullOrWhiteSpace(id))!);
            }
            else
            {
                var dependency = ReadDependencyId(dependencies);
                if (!string.IsNullOrWhiteSpace(dependency))
                {
                    result.Add(dependency);
                }
            }
        }

        return result
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        static string? ReadDependencyId(JsonElement dep)
        {
            if (dep.ValueKind == JsonValueKind.String)
            {
                return dep.GetString();
            }
            if (dep.ValueKind == JsonValueKind.Object)
            {
                if (IsOptionalDependency(dep))
                {
                    return null;
                }

                foreach (var key in new[] { "id", "mod_id", "modId", "workshop_id", "workshopId", "steam_id", "steamId", "published_file_id", "publishedFileId" })
                {
                    if (dep.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        return value.GetString();
                    }
                }
            }
            return null;
        }

        static bool IsOptionalDependency(JsonElement dep)
        {
            foreach (var key in new[] { "optional", "is_optional", "isOptional" })
            {
                if (dep.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }

            return dep.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.False;
        }
    }

    private static DependencyVersionRequirement[] GetDependencyVersionRequirements(JsonElement root)
    {
        var result = new System.Collections.Generic.List<DependencyVersionRequirement>();
        foreach (var propertyName in new[] { "dependencies", "requires", "required_mods", "requiredMods" })
        {
            if (!root.TryGetProperty(propertyName, out var dependencies))
            {
                continue;
            }

            if (dependencies.ValueKind == JsonValueKind.Array)
            {
                foreach (var dependency in dependencies.EnumerateArray())
                {
                    AddRequirement(dependency);
                }
            }
            else
            {
                AddRequirement(dependencies);
            }
        }

        return result
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement.Id) && !string.IsNullOrWhiteSpace(requirement.MinVersion))
            .GroupBy(requirement => requirement.Id + "\u001f" + requirement.MinVersion, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        void AddRequirement(JsonElement dependency)
        {
            if (dependency.ValueKind != JsonValueKind.Object || IsOptionalDependency(dependency))
            {
                return;
            }

            var id = ReadDependencyId(dependency);
            var minVersion = ReadMinVersion(dependency);
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(minVersion))
            {
                result.Add(new DependencyVersionRequirement(id!, minVersion!));
            }
        }

        static string? ReadDependencyId(JsonElement dep)
        {
            foreach (var key in new[] { "id", "mod_id", "modId", "workshop_id", "workshopId", "steam_id", "steamId", "published_file_id", "publishedFileId" })
            {
                if (dep.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
            return null;
        }

        static string? ReadMinVersion(JsonElement dep)
        {
            foreach (var key in new[] { "min_version", "minVersion", "minimum_version", "minimumVersion", "version_min", "versionMin", "required_version", "requiredVersion" })
            {
                if (dep.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
            return null;
        }

        static bool IsOptionalDependency(JsonElement dep)
        {
            foreach (var key in new[] { "optional", "is_optional", "isOptional" })
            {
                if (dep.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }

            return dep.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.False;
        }
    }

    private static string[] GetLoadAfter(JsonElement root)
    {
        var result = new System.Collections.Generic.List<string>();
        foreach (var propertyName in new[] { "load_after", "loadAfter" })
        {
            if (!root.TryGetProperty(propertyName, out var loadAfter))
            {
                continue;
            }

            if (loadAfter.ValueKind == JsonValueKind.Array)
            {
                result.AddRange(loadAfter.EnumerateArray().Select(ReadOrderId).Where(id => !string.IsNullOrWhiteSpace(id))!);
            }
            else
            {
                var id = ReadOrderId(loadAfter);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result.Add(id);
                }
            }
        }

        return result
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        static string? ReadOrderId(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                return item.GetString();
            }
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "id", "mod_id", "modId", "workshop_id", "workshopId", "steam_id", "steamId", "published_file_id", "publishedFileId" })
                {
                    if (item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        return value.GetString();
                    }
                }
            }
            return null;
        }
    }

    private static string[] GetLoadBefore(JsonElement root)
    {
        var result = new System.Collections.Generic.List<string>();
        foreach (var propertyName in new[] { "load_before", "loadBefore" })
        {
            if (!root.TryGetProperty(propertyName, out var loadBefore))
            {
                continue;
            }

            if (loadBefore.ValueKind == JsonValueKind.Array)
            {
                result.AddRange(loadBefore.EnumerateArray().Select(ReadOrderId).Where(id => !string.IsNullOrWhiteSpace(id))!);
            }
            else
            {
                var id = ReadOrderId(loadBefore);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result.Add(id);
                }
            }
        }

        return result
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        static string? ReadOrderId(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                return item.GetString();
            }
            if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "id", "mod_id", "modId", "workshop_id", "workshopId", "steam_id", "steamId", "published_file_id", "publishedFileId" })
                {
                    if (item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        return value.GetString();
                    }
                }
            }
            return null;
        }
    }

    private static ModSummary[] ApplyDependencyClassification(ModSummary[] mods, GameSessionState gameState, string currentGameVersion)
    {
        var result = mods
            .Select(mod => ApplyStateClassification(mod, gameState))
            .ToArray();
        var changed = true;
        while (changed)
        {
            changed = false;
            var byId = result.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < result.Length; i++)
            {
                var mod = result[i];
                var missingDependency = mod.Dependencies.FirstOrDefault(dep => !byId.ContainsKey(dep));
                if (missingDependency is not null)
                {
                    if (!mod.Reason.StartsWith("missing dependency:", StringComparison.OrdinalIgnoreCase))
                    {
                        result[i] = mod with
                        {
                            IsHotCandidate = false,
                            Reason = "missing dependency: " + missingDependency
                        };
                        changed = true;
                    }
                    continue;
                }

                var tooLowRequirement = mod.DependencyVersionRequirements.FirstOrDefault(requirement =>
                    byId.TryGetValue(requirement.Id, out var dependency) &&
                    IsVersionLowerThan(dependency.Version, requirement.MinVersion));
                if (tooLowRequirement is not null)
                {
                    var foundVersion = byId[tooLowRequirement.Id].Version;
                    var reason = "dependency version too low: " + tooLowRequirement.Id + " requires " + tooLowRequirement.MinVersion + ", found " + foundVersion;
                    if (!string.Equals(mod.Reason, reason, StringComparison.Ordinal))
                    {
                        result[i] = mod with
                        {
                            IsHotCandidate = false,
                            Reason = reason
                        };
                        changed = true;
                    }
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mod.MinGameVersion) &&
                    IsVersionLowerThan(currentGameVersion, mod.MinGameVersion))
                {
                    var reason = "game version too low: requires " + mod.MinGameVersion + ", found " + currentGameVersion;
                    if (!string.Equals(mod.Reason, reason, StringComparison.Ordinal))
                    {
                        result[i] = mod with
                        {
                            IsHotCandidate = false,
                            Reason = reason
                        };
                        changed = true;
                    }
                    continue;
                }

                if (!mod.IsHotCandidate)
                {
                    continue;
                }

                if (SessionRestartRequired.TryGetValue(mod.Id, out var sessionReason))
                {
                    result[i] = mod with
                    {
                        IsHotCandidate = false,
                        Reason = sessionReason
                    };
                    changed = true;
                    continue;
                }

                var restartDependency = mod.Dependencies.FirstOrDefault(dep =>
                    byId.TryGetValue(dep, out var dependency) &&
                    !dependency.IsHotCandidate &&
                    !CanUseLoadedRestartDependency(mod, dependency));
                if (restartDependency is not null)
                {
                    result[i] = mod with
                    {
                        IsHotCandidate = false,
                        Reason = "depends on restart-required mod: " + restartDependency
                    };
                    changed = true;
                }
            }
        }
        return result;
    }

    private static bool IsVersionLowerThan(string actualVersion, string requiredVersion)
    {
        var actual = GetVersionSegments(actualVersion);
        var required = GetVersionSegments(requiredVersion);
        if (actual.Length == 0 || required.Length == 0)
        {
            return false;
        }

        var count = Math.Max(actual.Length, required.Length);
        for (var i = 0; i < count; i++)
        {
            var actualSegment = i < actual.Length ? actual[i] : 0;
            var requiredSegment = i < required.Length ? required[i] : 0;
            if (actualSegment < requiredSegment)
            {
                return true;
            }
            if (actualSegment > requiredSegment)
            {
                return false;
            }
        }

        return false;
    }

    private static int[] GetVersionSegments(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return [];
        }

        var text = version.Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            text = text[1..];
        }

        return System.Text.RegularExpressions.Regex.Split(text, @"\D+")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => int.TryParse(part, out var value) ? (int?)value : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
    }

    private static ModSummary ApplyStateClassification(ModSummary mod, GameSessionState gameState)
    {
        if (mod.Scope == HotApplyScope.RuntimeSafe)
        {
            if (SessionRestartRequired.TryGetValue(mod.Id, out var runtimeSessionReason))
            {
                return mod with
                {
                    IsHotCandidate = false,
                    Reason = runtimeSessionReason
                };
            }

            if (gameState.IsActiveRun && !mod.IsRunSafeRuntime)
            {
                return mod with
                {
                    IsHotCandidate = false,
                    Reason = "runtime/config apply blocked during active run"
                };
            }

            return mod;
        }

        if (mod.Scope != HotApplyScope.MainMenuNoRun)
        {
            return mod;
        }

        if (SessionRestartRequired.TryGetValue(mod.Id, out var sessionReason))
        {
            return mod with
            {
                IsHotCandidate = false,
                Reason = sessionReason
            };
        }

        if (gameState.IsSafeMainMenuWithoutRun)
        {
            if (!mod.IsLoaded)
            {
                return mod with
                {
                    IsHotCandidate = false,
                    Reason = "main menu only; mod is not loaded, start through launcher to enable"
                };
            }

            return mod with
            {
                IsHotCandidate = true,
                Reason = "future-run content toggle; main menu safe"
            };
        }

        var reason = gameState.IsActiveRun
            ? "main menu only; active run may reference this content"
            : gameState.HasUnfinishedRun
                ? "main menu only; unfinished run present"
                : "main menu only; safe menu state not confirmed";
        return mod with
        {
            IsHotCandidate = false,
            Reason = reason
        };
    }

    private static bool CanUseLoadedRestartDependency(ModSummary mod, ModSummary dependency) =>
        mod.Scope == HotApplyScope.MainMenuNoRun && dependency.IsLoaded && dependency.IsEnabled && dependency.IsFrameworkRoot;

    private static bool IsFrameworkMod(string id) =>
        id.Equals("BaseLib", StringComparison.OrdinalIgnoreCase) ||
        id.Equals("RitsuLib", StringComparison.OrdinalIgnoreCase) ||
        id.Equals("STS2-RitsuLib", StringComparison.OrdinalIgnoreCase);

    private static bool IsMainMenuContentCandidate(JsonElement root, string id, string? declaredScope) =>
        KnownMainMenuContentMods.Contains(id) ||
        IsMainMenuOnlyToken(declaredScope) ||
        AnyMainMenuOnlyManifestToken(root);

    private static bool IsRuntimeSafeCandidate(JsonElement root, string? declaredScope) =>
        IsRuntimeSafeToken(declaredScope) ||
        AnyRuntimeSafeManifestToken(root);

    private static bool IsRunSafeRuntimeCandidate(JsonElement root, string? declaredScope) =>
        IsRunSafeRuntimeToken(declaredScope) ||
        AnyRunSafeRuntimeManifestToken(root);

    private static string? GetRestartRequiredManifestReason(JsonElement root)
    {
        foreach (var token in EnumerateManifestTokens(root))
        {
            if (IsSaveSerializerToken(token))
            {
                return "save serializer; restart required";
            }
        }

        foreach (var token in EnumerateManifestTokens(root))
        {
            if (IsSaveAffectingToken(token))
            {
                return "save-affecting patch; restart required";
            }
        }

        foreach (var token in EnumerateManifestTokens(root))
        {
            if (IsActiveRunPatchToken(token))
            {
                return "active-run patch; restart required";
            }
        }

        return AnyManifestToken(root, IsRestartRequiredToken)
            ? "startup/core/UI patch; restart required"
            : null;
    }

    private static bool AnyFrameworkRootManifestToken(JsonElement root) =>
        AnyManifestToken(root, IsFrameworkRootToken);

    private static bool AnyMainMenuOnlyManifestToken(JsonElement root)
        => AnyManifestToken(root, IsMainMenuOnlyToken);

    private static bool AnyRuntimeSafeManifestToken(JsonElement root)
        => AnyManifestToken(root, IsRuntimeSafeToken);

    private static bool AnyRunSafeRuntimeManifestToken(JsonElement root)
        => AnyManifestToken(root, IsRunSafeRuntimeToken);

    private static bool AnyManifestToken(JsonElement root, Func<string?, bool> predicate)
    {
        return EnumerateManifestTokens(root).Any(predicate);
    }

    private static System.Collections.Generic.IEnumerable<string?> EnumerateManifestTokens(JsonElement root)
    {
        foreach (var propertyName in new[]
        {
            "hot_apply_content",
            "hot_reload_content",
            "content_type",
            "content_types",
            "content_tags",
            "mod_type",
            "mod_types",
            "tags",
            "categories"
        })
        {
            if (!root.TryGetProperty(propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                yield return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        yield return item.GetString();
                    }
                }
            }
        }
    }

    private static bool IsMainMenuOnlyToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return
            normalized.Equals("main-menu-no-run", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("future-run", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("future-run-content", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("future-run-generation", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("new-run-content", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("run-generation", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("event", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("events", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("character", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("characters", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("relic", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("relics", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("card", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("cards", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("encounter", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("encounters", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ancient", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ancients", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ancient-choices", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRestartRequiredToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return
            normalized.Equals("framework", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("library", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("shared-library", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("dependency-root", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("api", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("core-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ui-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("startup-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("harmony-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("active-run-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("run-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("combat-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("save-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("save-affecting", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("save-data", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("serializer", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("save-serializer", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("runtime-service", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActiveRunPatchToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return
            normalized.Equals("active-run-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("run-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("combat-patch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSaveAffectingToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return
            normalized.Equals("save-patch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("save-affecting", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("save-data", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("serializer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeSafeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return
            normalized.Equals("runtime", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("runtime-safe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("settings", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("config", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("configuration", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("utility", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRunSafeRuntimeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return
            normalized.Equals("run-safe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("during-run", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("active-run-safe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("combat-safe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSaveSerializerToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return normalized.Equals("save-serializer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrameworkRootToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('_', '-').Replace(' ', '-');
        return
            normalized.Equals("framework", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("library", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("shared-library", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("dependency-root", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("api", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("runtime-service", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindGameDir(string modDir)
    {
        var current = new DirectoryInfo(modDir);
        for (var i = 0; i < 8 && current is not null; i++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "SlayTheSpire2.exe")))
            {
                return current.FullName;
            }
            if (current.Name.Equals("mods", StringComparison.OrdinalIgnoreCase) &&
                current.Parent is not null &&
                File.Exists(Path.Combine(current.Parent.FullName, "SlayTheSpire2.exe")))
            {
                return current.Parent.FullName;
            }
        }

        var steamApps = FindAncestor(modDir, "steamapps", 12);
        if (steamApps is not null)
        {
            var workshopSibling = Path.Combine(steamApps.FullName, "common", "Slay the Spire 2");
            if (File.Exists(Path.Combine(workshopSibling, "SlayTheSpire2.exe")))
            {
                return workshopSibling;
            }
        }

        CompanionLog.Write("Game directory could not be discovered from modDir=" + modDir);
        return "";
    }

    private static string FindWorkshopDir(string gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir))
        {
            var steamApps = FindAncestor(LauncherActions.GetModDir(), "steamapps", 12);
            return steamApps is null ? "" : Path.Combine(steamApps.FullName, "workshop", "content", "2868840");
        }

        var common = Directory.GetParent(gameDir);
        var steamapps = common?.Parent;
        return steamapps is null ? "" : Path.Combine(steamapps.FullName, "workshop", "content", "2868840");
    }

    private static string ReadCurrentGameVersion(string gameDir)
    {
        try
        {
            var path = Path.Combine(gameDir, "release_info.json");
            if (!File.Exists(path))
            {
                return "";
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return GetString(doc.RootElement, "version") ?? GetString(doc.RootElement, "branch") ?? "";
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Read release_info.json failed: " + ex.Message);
            return "";
        }
    }

    private static DirectoryInfo? FindAncestor(string start, string name, int maxDepth)
    {
        var current = new DirectoryInfo(start);
        for (var i = 0; i < maxDepth && current is not null; i++, current = current.Parent)
        {
            if (current.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string GetMinGameVersion(JsonElement root)
    {
        foreach (var key in new[]
        {
            "min_game_version",
            "minGameVersion",
            "minimum_game_version",
            "minimumGameVersion",
            "game_version_min",
            "gameVersionMin",
            "required_game_version",
            "requiredGameVersion"
        })
        {
            var value = GetString(root, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value!;
            }
        }

        return "";
    }

    private static bool GetBool(JsonElement root, string name, bool defaultValue = false)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    private sealed record ScanRoot(string Path, ModSource Source);
    private sealed record ManifestPath(string Path, ModSource Source);
}

internal static class HotApplyService
{
    public enum ApplyResult
    {
        Succeeded,
        FailedDowngraded,
        UnavailableDowngraded,
        Unavailable
    }

    public static ApplyResult Apply(
        ModScanner.ModSummary[] selectedCandidates,
        ModScanner.ModSummary[] allHotCandidates,
        ModScanner.ModSummary[] allDiscoveredMods)
    {
        var started = DateTime.UtcNow;
        try
        {
            var manager = SaveManager.Instance;
            var settings = manager?.SettingsSave;
            var modSettings = settings?.ModSettings;
            var modList = modSettings?.ModList;
            if (manager is null || settings is null || modSettings is null || modList is null)
            {
                var attemptedIds = ResolveHotDependencies(selectedCandidates, allHotCandidates);
                if (attemptedIds.Count == 0)
                {
                    attemptedIds = allHotCandidates.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                ModScanner.MarkSessionRestartRequired(attemptedIds, "hot apply unavailable this session; restart required");
                NativeMessageBox.Show("The game's settings manager is not ready. Use the restart-required launcher flow for now.", "ModTheSpire2");
                return ApplyResult.UnavailableDowngraded;
            }

            BackupSettingsFile();
            var snapshot = modList
                .Select(item => new StateSnapshot(item.Id, item.Source, item.IsEnabled))
                .ToArray();

            try
            {
                var enabledHotIds = ResolveHotDependencies(selectedCandidates, allHotCandidates);
                var allHotIds = allHotCandidates.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var stateBlock = FindStateSafetyBlock(modList, allHotCandidates, enabledHotIds, GameSessionState.Detect());
                if (stateBlock is not null)
                {
                    NativeMessageBox.Show(
                        "Hot apply was blocked because the game state changed.\n\n" +
                        stateBlock +
                        "\n\nReturn to the main menu with no unfinished run, or use Close and Open Launcher for this change.",
                        "ModTheSpire2");
                    CompanionLog.Write("Hot apply blocked by state safety gate: " + stateBlock);
                    return ApplyResult.Unavailable;
                }

                var dependencyBlock = FindEnabledDependentBlock(modList, allHotCandidates, allDiscoveredMods, enabledHotIds);
                if (dependencyBlock is not null)
                {
                    NativeMessageBox.Show(
                        "Hot apply was blocked to preserve dependency safety.\n\n" +
                        dependencyBlock +
                        "\n\nDisable dependent mods first, or use Close and Open Launcher for this change.",
                        "ModTheSpire2");
                    CompanionLog.Write("Hot apply blocked by enabled dependent: " + dependencyBlock);
                    return ApplyResult.Unavailable;
                }

                EnsureHotEntries(modList, allHotCandidates);
                var applied = 0;
                foreach (var item in modList)
                {
                    if (DateTime.UtcNow - started > TimeSpan.FromSeconds(10))
                    {
                        throw new TimeoutException("Hot apply exceeded the 10 second safety limit.");
                    }

                    if (enabledHotIds.Contains(item.Id))
                    {
                        item.IsEnabled = true;
                        applied++;
                        continue;
                    }

                    if (allHotIds.Contains(item.Id))
                    {
                        item.IsEnabled = false;
                        applied++;
                    }
                }

                manager.SaveSettings();
                NativeMessageBox.Show(
                    "Hot-apply settings were saved for " + applied + " candidate mod(s).\n\n" +
                    "This does not true-load new DLL/PCK mods. Restart-required mods still need the launcher flow.",
                    "ModTheSpire2");
                CompanionLog.Write("Hot apply saved candidates=" + applied);
                return ApplyResult.Succeeded;
            }
            catch (Exception ex)
            {
                RestoreSnapshot(modList, snapshot);
                manager.SaveSettings();
                var attemptedIds = ResolveHotDependencies(selectedCandidates, allHotCandidates);
                if (attemptedIds.Count == 0)
                {
                    attemptedIds = allHotCandidates.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                ModScanner.MarkSessionRestartRequired(attemptedIds, "hot apply failed this session; restart required");
                CompanionLog.Write("Hot apply failed and rolled back: " + ex);
                NativeMessageBox.Show(
                    "Hot apply failed and was rolled back.\n\n" + ex.Message + "\n\nAffected hot candidates are treated as Restart Required for this session. Use Close and Open Launcher for this change.",
                    "ModTheSpire2");
                return ApplyResult.FailedDowngraded;
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Hot apply outer failure: " + ex);
            NativeMessageBox.Show("Hot apply is unavailable:\n" + ex.Message, "ModTheSpire2");
            return ApplyResult.Unavailable;
        }
    }

    private static void RestoreSnapshot(System.Collections.Generic.List<SettingsSaveMod> modList, StateSnapshot[] snapshot)
    {
        for (var i = modList.Count - 1; i >= 0; i--)
        {
            var current = modList[i];
            if (!snapshot.Any(state =>
                string.Equals(state.Id, current.Id, StringComparison.OrdinalIgnoreCase) &&
                state.Source == current.Source))
            {
                modList.RemoveAt(i);
            }
        }

        foreach (var state in snapshot)
        {
            var item = modList.FirstOrDefault(m =>
                string.Equals(m.Id, state.Id, StringComparison.OrdinalIgnoreCase) &&
                m.Source == state.Source);
            if (item is not null)
            {
                item.IsEnabled = state.IsEnabled;
            }
        }
    }

    private static void EnsureHotEntries(System.Collections.Generic.List<SettingsSaveMod> modList, ModScanner.ModSummary[] allHotCandidates)
    {
        foreach (var mod in allHotCandidates)
        {
            if (modList.Any(item =>
                string.Equals(item.Id, mod.Id, StringComparison.OrdinalIgnoreCase) &&
                item.Source == mod.Source))
            {
                continue;
            }

            modList.Add(new SettingsSaveMod
            {
                Id = mod.Id,
                Source = mod.Source,
                IsEnabled = false
            });
            CompanionLog.Write("Added missing settings entry for hot candidate: " + mod.Id);
        }
    }

    private static System.Collections.Generic.HashSet<string> ResolveHotDependencies(ModScanner.ModSummary[] selected, ModScanner.ModSummary[] allHotCandidates)
    {
        var allHot = allHotCandidates.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var resolved = selected.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var id in resolved.ToArray())
            {
                if (!allHot.TryGetValue(id, out var mod))
                {
                    continue;
                }
                foreach (var dep in mod.Dependencies)
                {
                    if (allHot.ContainsKey(dep) && resolved.Add(dep))
                    {
                        changed = true;
                    }
                }
            }
        }
        return resolved;
    }

    private static string? FindStateSafetyBlock(
        System.Collections.Generic.List<SettingsSaveMod> modList,
        ModScanner.ModSummary[] allHotCandidates,
        System.Collections.Generic.HashSet<string> enabledHotIds,
        GameSessionState latestState)
    {
        if (latestState.IsActiveRun)
        {
            foreach (var mod in allHotCandidates.Where(candidate => candidate.Scope == ModScanner.HotApplyScope.RuntimeSafe && !candidate.IsRunSafeRuntime))
            {
                var currentEnabled = modList.Any(item =>
                    string.Equals(item.Id, mod.Id, StringComparison.OrdinalIgnoreCase) &&
                    item.Source == mod.Source &&
                    item.IsEnabled);
                var desiredEnabled = enabledHotIds.Contains(mod.Id);
                if (currentEnabled != desiredEnabled)
                {
                    return "Cannot change " + mod.Name + " [" + mod.Id + "]: runtime/config changes require a run-safe manifest token while a run is active.";
                }
            }
        }

        foreach (var mod in allHotCandidates.Where(candidate => candidate.Scope == ModScanner.HotApplyScope.MainMenuNoRun))
        {
            var currentEnabled = modList.Any(item =>
                string.Equals(item.Id, mod.Id, StringComparison.OrdinalIgnoreCase) &&
                item.Source == mod.Source &&
                item.IsEnabled);
            var desiredEnabled = enabledHotIds.Contains(mod.Id);
            if (currentEnabled == desiredEnabled)
            {
                continue;
            }

            if (latestState.IsSafeMainMenuWithoutRun)
            {
                continue;
            }

            if (currentEnabled && !desiredEnabled)
            {
                var reason = latestState.IsActiveRun
                    ? "an active run may reference this content"
                    : latestState.HasUnfinishedRun
                        ? "an unfinished run may reference this content"
                        : "the current screen is not a confirmed safe main menu";
                return "Cannot disable " + mod.Name + " [" + mod.Id + "]: " + reason + ".";
            }

            return "Cannot change " + mod.Name + " [" + mod.Id + "]: main-menu content changes require a confirmed safe main menu with no unfinished run.";
        }

        return null;
    }

    private static string? FindEnabledDependentBlock(
        System.Collections.Generic.List<SettingsSaveMod> modList,
        ModScanner.ModSummary[] allHotCandidates,
        ModScanner.ModSummary[] allDiscoveredMods,
        System.Collections.Generic.HashSet<string> enabledHotIds)
    {
        var enabledSettings = modList
            .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentHotIds = allHotCandidates
            .Where(mod => mod.IsEnabled)
            .Select(mod => mod.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disabling = currentHotIds
            .Where(id => !enabledHotIds.Contains(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (disabling.Count == 0)
        {
            return null;
        }

        foreach (var disabledId in disabling)
        {
            var dependent = allDiscoveredMods.FirstOrDefault(mod =>
                !string.Equals(mod.Id, disabledId, StringComparison.OrdinalIgnoreCase) &&
                enabledSettings.Contains(mod.Id) &&
                mod.Dependencies.Any(dep => string.Equals(dep, disabledId, StringComparison.OrdinalIgnoreCase)));
            if (dependent is not null)
            {
                return disabledId + " is still required by enabled mod " + dependent.Name + " [" + dependent.Id + "]";
            }
        }

        return null;
    }

    private static void BackupSettingsFile()
    {
        try
        {
            var settings = FindLatestSettingsFile();
            if (settings is null)
            {
                CompanionLog.Write("Hot apply backup skipped: settings.save was not found");
                return;
            }

            var backupDir = Path.Combine(LauncherActions.GetModDir(), "ModTheSpire2Data", "hot-apply-backups");
            Directory.CreateDirectory(backupDir);
            var backup = Path.Combine(backupDir, "settings.save." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");
            File.Copy(settings.FullName, backup, overwrite: false);
            CompanionLog.Write("Hot apply backup created: " + backup + " from " + settings.FullName);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Hot apply backup failed: " + ex);
        }
    }

    private static FileInfo? FindLatestSettingsFile()
    {
        return EnumerateSettingsSearchRoots()
            .Where(Directory.Exists)
            .SelectMany(root =>
            {
                try
                {
                    return Directory.EnumerateFiles(root, "settings.save", SearchOption.AllDirectories)
                        .Select(path => new FileInfo(path));
                }
                catch (Exception ex)
                {
                    CompanionLog.Write("Settings search skipped: " + root + " :: " + ex.Message);
                    return Enumerable.Empty<FileInfo>();
                }
            })
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateSettingsSearchRoots()
    {
        var roaming = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roaming))
        {
            yield return Path.Combine(roaming, "SlayTheSpire2");
        }
    }

    private sealed record StateSnapshot(string Id, ModSource Source, bool IsEnabled);
}

internal static class LoadOrderManager
{
    public static System.Collections.Generic.List<ModScanner.ModSummary> CreateInitialOrder(ModScanner.ModSummary[] mods)
    {
        var enabled = CreateDefaultOrder(mods);
        var saved = ReadSavedIds();
        if (saved.Length == 0)
        {
            return enabled;
        }

        var byId = enabled.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new System.Collections.Generic.List<ModScanner.ModSummary>();
        foreach (var id in saved)
        {
            if (byId.TryGetValue(id, out var mod) && !ordered.Any(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(mod);
            }
        }

        ordered.AddRange(enabled.Where(mod => !ordered.Any(existing => string.Equals(existing.Id, mod.Id, StringComparison.OrdinalIgnoreCase))));
        RepairDependencyOrder(ordered);
        return ordered;
    }

    public static System.Collections.Generic.List<ModScanner.ModSummary> CreateDefaultOrder(ModScanner.ModSummary[] mods)
    {
        var current = ReadSettingsOrder();
        var enabled = mods.Where(m => m.IsEnabled).ToArray();
        var byId = enabled.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new System.Collections.Generic.List<ModScanner.ModSummary>();
        foreach (var id in current)
        {
            if (byId.TryGetValue(id, out var mod) && !ordered.Any(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                ordered.Add(mod);
            }
        }

        ordered.AddRange(enabled
            .Where(mod => !ordered.Any(existing => string.Equals(existing.Id, mod.Id, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase));
        RepairDependencyOrder(ordered);
        return ordered;
    }

    public static bool TryMove(System.Collections.Generic.List<ModScanner.ModSummary> order, int index, int delta, out string message)
    {
        var target = index + delta;
        if (index < 0 || index >= order.Count || target < 0 || target >= order.Count)
        {
            message = "Select a mod that can be moved.";
            return false;
        }

        var moving = order[index];
        var neighbor = order[target];
        if (delta < 0 && DependsOn(neighbor, moving.Id))
        {
            message = "Cannot move a mod above its dependency.";
            return false;
        }
        if (delta > 0 && DependsOn(moving, neighbor.Id))
        {
            message = "Cannot move a dependency below a mod that needs it.";
            return false;
        }

        order[index] = neighbor;
        order[target] = moving;
        RepairDependencyOrder(order);
        message = "Order changed. Use Save Order to keep it for future launcher starts.";
        return true;
    }

    public static string Save(System.Collections.Generic.IReadOnlyList<ModScanner.ModSummary> order)
    {
        try
        {
            var path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, order.Select(m => m.Id));
            CompanionLog.Write("Load order saved entries=" + order.Count + " path=" + path);
            return "Custom load order saved for the launcher.";
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Save load order failed: " + ex);
            return "Could not save load-order.txt: " + ex.Message;
        }
    }

    public static string Reset()
    {
        try
        {
            var path = GetPath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            CompanionLog.Write("Load order reset path=" + path);
            return "Custom load order reset. The current game settings order is shown again.";
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Reset load order failed: " + ex);
            return "Could not reset load order: " + ex.Message;
        }
    }

    private static string GetPath() => Path.Combine(LauncherActions.GetModDir(), "ModTheSpire2Data", "load-order.txt");

    private static string[] ReadSavedIds()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return [];
            }

            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Read saved load order failed: " + ex);
            return [];
        }
    }

    private static string[] ReadSettingsOrder()
    {
        try
        {
            var modList = SaveManager.Instance?.SettingsSave?.ModSettings?.ModList;
            if (modList is null)
            {
                return [];
            }

            return modList
                .Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Id))
                .Select(item => item.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Read settings load order failed: " + ex);
            return [];
        }
    }

    private static void RepairDependencyOrder(System.Collections.Generic.List<ModScanner.ModSummary> order)
    {
        var changed = true;
        var guard = 0;
        while (changed && guard++ < 64)
        {
            changed = false;
            for (var i = 0; i < order.Count; i++)
            {
                var mod = order[i];
                foreach (var dependency in mod.Dependencies.Concat(mod.LoadAfter))
                {
                    var depIndex = order.FindIndex(m => string.Equals(m.Id, dependency, StringComparison.OrdinalIgnoreCase));
                    if (depIndex > i)
                    {
                        var dep = order[depIndex];
                        order.RemoveAt(depIndex);
                        order.Insert(i, dep);
                        changed = true;
                        break;
                    }
                }
                if (!changed)
                {
                    foreach (var before in mod.LoadBefore)
                    {
                        var beforeIndex = order.FindIndex(m => string.Equals(m.Id, before, StringComparison.OrdinalIgnoreCase));
                        if (beforeIndex >= 0 && beforeIndex < i)
                        {
                            order.RemoveAt(i);
                            order.Insert(beforeIndex, mod);
                            changed = true;
                            break;
                        }
                    }
                }
                if (changed)
                {
                    break;
                }
            }
        }
    }

    private static bool DependsOn(ModScanner.ModSummary mod, string dependencyId) =>
        mod.Dependencies.Any(dep => string.Equals(dep, dependencyId, StringComparison.OrdinalIgnoreCase));
}

internal static class LauncherActions
{
    public static string GetModDir() => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

    public static string GetLauncherPath() => Path.Combine(GetModDir(), "ModTheSpire2Launcher.exe");

    public static string GetLaunchOption() => $"\"{GetLauncherPath()}\" -- %command%";

    public static void OpenLauncher()
    {
        var modDir = GetModDir();
        var launcher = GetLauncherPath();
        var readme = Path.Combine(modDir, "README.md");
        var target = File.Exists(launcher) ? launcher : readme;
        if (!File.Exists(target))
        {
            NativeMessageBox.Show("ModTheSpire2Launcher.exe was not found. Please check that the mod files are complete.", "ModTheSpire2");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            WorkingDirectory = modDir,
            UseShellExecute = true
        });
    }
}

internal static class ModConfigIntegration
{
    private static bool s_registered;

    public static void TryRegister()
    {
        if (s_registered)
        {
            return;
        }

        try
        {
            var registryType = BaseLibReflection.FindType("BaseLib.Config.ModConfigRegistry");
            var simpleConfigType = BaseLibReflection.FindType("BaseLib.Config.SimpleModConfig");
            var buttonAttributeType = BaseLibReflection.FindType("BaseLib.Config.ConfigButtonAttribute");
            if (registryType is null || simpleConfigType is null || buttonAttributeType is null)
            {
                CompanionLog.Write("BaseLib ModConfig not present");
                return;
            }

            BaseLibModConfigSubmenuPatch.TryPatchLate();
            var configType = DynamicModConfigType.Create(simpleConfigType, buttonAttributeType);
            var config = Activator.CreateInstance(configType);
            simpleConfigType.GetProperty("ModId")?.SetValue(config, "ModTheSpire2");
            registryType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, ["ModTheSpire2", config]);
            s_registered = true;
            CompanionLog.Write("BaseLib ModConfig registered");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("BaseLib ModConfig registration failed: " + ex);
        }
    }
}

internal static class BaseLibReflection
{
    public static Type? FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var direct = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                if (direct is not null)
                {
                    return direct;
                }
            }
            catch
            {
            }

            try
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    if (string.Equals(type.FullName, fullName, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static System.Collections.Generic.IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
        catch
        {
            return [];
        }
    }
}

[HarmonyPatch]
internal static class BaseLibModConfigSubmenuPatch
{
    private const string ButtonNodeName = "ModTheSpire2BaseLibButton";
    private static bool s_latePatchAttempted;
    private static bool s_latePatchSucceeded;

    public static bool Prepare()
    {
        return BaseLibReflection.FindType("BaseLib.Config.UI.NModConfigSubmenu") is not null;
    }

    public static void TryPatchLate()
    {
        if (s_latePatchSucceeded || s_latePatchAttempted)
        {
            return;
        }

        var target = TargetMethod();
        if (target is null)
        {
            return;
        }

        s_latePatchAttempted = true;
        try
        {
            var postfix = typeof(BaseLibModConfigSubmenuPatch).GetMethod(nameof(Postfix), BindingFlags.Public | BindingFlags.Static);
            new Harmony("HZDH.ModTheSpire2.BaseLibLate").Patch(target, postfix: postfix is null ? null : new HarmonyMethod(postfix));
            s_latePatchSucceeded = true;
            CompanionLog.Write("BaseLib submenu patch installed late");
        }
        catch (Exception ex)
        {
            CompanionLog.Write("BaseLib submenu late patch failed: " + ex);
        }
    }

    public static MethodBase? TargetMethod()
    {
        var type = BaseLibReflection.FindType("BaseLib.Config.UI.NModConfigSubmenu");
        return type?.GetMethod("_Ready", BindingFlags.Public | BindingFlags.Instance);
    }

    public static void Postfix(Node __instance)
    {
        try
        {
            ModConfigIntegration.TryRegister();
            if (__instance.FindChild(ButtonNodeName, recursive: true, owned: false) is not null)
            {
                return;
            }

            var listParent = FindListParent(__instance);
            if (listParent is null)
            {
                CompanionLog.Write("BaseLib submenu list parent not found: " + NodeDump(__instance));
                return;
            }

            var button = CreateBaseLibListButton(__instance);
            listParent.AddChild(button);
            CompanionLog.Write("BaseLib submenu hot-manage button added under " + listParent.GetPath());
        }
        catch (Exception ex)
        {
            CompanionLog.Write("BaseLib submenu hot-manage button failed: " + ex);
        }
    }

    private static Control? FindListParent(Node root)
    {
        var existing = root.FindChildren("*", recursive: true, owned: false)
            .OfType<Node>()
            .FirstOrDefault(n => n.GetType().FullName == "BaseLib.Config.UI.NModListButton");
        return existing?.GetParent() as Control;
    }

    private static Control CreateBaseLibListButton(Node owner)
    {
        var buttonType = BaseLibReflection.FindType("BaseLib.Config.UI.NModListButton");
        if (buttonType is not null)
        {
            try
            {
                if (Activator.CreateInstance(buttonType, ["ModTheSpire2"]) is Control nativeButton)
                {
                    nativeButton.Name = ButtonNodeName;
                    nativeButton.TooltipText = "Open ModTheSpire2 management.";
                    nativeButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    AttachReleased(nativeButton, () => ModManagementDialog.Show(owner));
                    return nativeButton;
                }
            }
            catch (Exception ex)
            {
                CompanionLog.Write("Create BaseLib native list button failed: " + ex);
            }
        }

        var fallback = new Button
        {
            Name = ButtonNodeName,
            Text = "ModTheSpire2",
            CustomMinimumSize = new Vector2(260, 60),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "Open ModTheSpire2 management.",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        UiStyle.ApplyButton(fallback);
        fallback.Pressed += () => ModManagementDialog.Show(owner);
        return fallback;
    }

    private static void AttachReleased(object target, Action action)
    {
        try
        {
            var eventInfo = target.GetType().GetEvent("Released");
            if (eventInfo is null)
            {
                if (target is Control control)
                {
                    control.GuiInput += input =>
                    {
                        if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
                        {
                            action();
                        }
                    };
                }
                return;
            }

            var invoke = eventInfo.EventHandlerType?.GetMethod("Invoke");
            var parameters = invoke?.GetParameters() ?? [];
            Delegate? handler = parameters.Length switch
            {
                0 => Delegate.CreateDelegate(eventInfo.EventHandlerType!, action.Target, action.Method),
                1 => CreateOneArgDelegate(eventInfo.EventHandlerType!, action),
                _ => null
            };
            if (handler is not null)
            {
                eventInfo.AddEventHandler(target, handler);
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Attach BaseLib button release failed: " + ex);
        }
    }

    private static Delegate CreateOneArgDelegate(Type delegateType, Action action)
    {
        var method = typeof(BaseLibModConfigSubmenuPatch).GetMethod(nameof(OnNativeListButtonReleased), BindingFlags.NonPublic | BindingFlags.Static)!;
        return Delegate.CreateDelegate(delegateType, action, method);
    }

    private static void OnNativeListButtonReleased(this Action action, object _)
    {
        action();
    }

    private static string NodeDump(Node root)
    {
        try
        {
            return string.Join(" | ", root.FindChildren("*", recursive: true, owned: false)
                .OfType<Node>()
                .Take(80)
                .Select(n => n.GetPath() + " [" + n.GetType().FullName + "]"));
        }
        catch
        {
            return "<node dump failed>";
        }
    }
}

internal static class DynamicModConfigType
{
    private static Type? s_type;

    public static Type Create(Type baseType, Type buttonAttributeType)
    {
        if (s_type is not null)
        {
            return s_type;
        }

        var assemblyName = new AssemblyName("ModTheSpire2DynamicModConfig");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var type = module.DefineType(
            "ModTheSpire2.DynamicModConfig",
            TypeAttributes.Public | TypeAttributes.Class,
            baseType);

        var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var ctorIl = ctor.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        var baseCtor = baseType.GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException("SimpleModConfig constructor not found");
        ctorIl.Emit(OpCodes.Call, baseCtor);
        ctorIl.Emit(OpCodes.Ret);

        DefineButtonMethod(type, buttonAttributeType, "OpenManagementButton", "Open ModTheSpire2 Management", nameof(OpenManagementFromConfig));
        DefineButtonMethod(type, buttonAttributeType, "CopyLaunchOptionButton", "Copy Steam Launch Option", nameof(CopyLaunchOptionFromConfig));
        DefineButtonMethod(type, buttonAttributeType, "OpenMismatchWorkshopLinksButton", "Open Missing Mod Links", nameof(OpenMismatchWorkshopLinksFromConfig));
        DefineButtonMethod(type, buttonAttributeType, "CopyMismatchReportButton", "Copy Mismatch Report", nameof(CopyMismatchReportFromConfig));

        var method = type.DefineMethod(
            "SetupConfigUI",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(Control)]);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        var baseMethod = baseType.GetMethod("SetupConfigUI", [typeof(Control)])
            ?? throw new InvalidOperationException("SimpleModConfig.SetupConfigUI not found");
        il.Emit(OpCodes.Call, baseMethod);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, typeof(DynamicModConfigType).GetMethod(nameof(BuildUi), BindingFlags.Public | BindingFlags.Static)!);
        il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(method, baseMethod);

        s_type = type.CreateType()!;
        return s_type;
    }

    private static void DefineButtonMethod(TypeBuilder type, Type attributeType, string methodName, string labelKey, string targetName)
    {
        var method = type.DefineMethod(methodName, MethodAttributes.Public, typeof(void), Type.EmptyTypes);
        var attrCtor = attributeType.GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("ConfigButtonAttribute constructor not found");
        method.SetCustomAttribute(new CustomAttributeBuilder(attrCtor, [labelKey]));
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(DynamicModConfigType).GetMethod(targetName, BindingFlags.Public | BindingFlags.Static)!);
        il.Emit(OpCodes.Ret);
    }

    public static void BuildUi(Control optionContainer)
    {
        try
        {
            var text = new RichTextLabel
            {
                Text = "Open ModTheSpire2 to review loaded mods, manage launcher order, or close the game and reopen the launcher.",
                FitContent = true,
                BbcodeEnabled = false,
                CustomMinimumSize = new Vector2(760, 70)
            };
            text.AddThemeColorOverride("default_color", UiStyle.MutedTextColor);
            optionContainer.AddChild(text);

            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(760, 56),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            optionContainer.AddChild(row);

            var open = CreateConfigButton("Open Management");
            open.Pressed += () => ModManagementDialog.Show(optionContainer);
            row.AddChild(open);

            var links = CreateConfigButton("Open Missing Mod Links");
            links.TooltipText = "Open Workshop links from the latest multiplayer mod mismatch report.";
            links.Pressed += MultiplayerMismatchActions.OpenLastWorkshopLinks;
            row.AddChild(links);

            var copyReport = CreateConfigButton("Copy Mismatch Report");
            copyReport.TooltipText = "Copy the latest multiplayer mismatch report for feedback.";
            copyReport.Pressed += MultiplayerMismatchActions.CopyLastReport;
            row.AddChild(copyReport);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Build BaseLib ModConfig UI failed: " + ex);
        }
    }

    private static Button CreateConfigButton(string text)
    {
        var button = UiStyle.CreateButton(text, 205, 46);
        button.TooltipText = text;
        return button;
    }

    public static void OpenManagementFromConfig()
    {
        if (Engine.GetMainLoop() is SceneTree tree && tree.Root is Node root)
        {
            ModManagementDialog.Show(root);
        }
    }

    public static void CopyLaunchOptionFromConfig()
    {
        DisplayServer.ClipboardSet(LauncherActions.GetLaunchOption());
    }

    public static void OpenMismatchWorkshopLinksFromConfig()
    {
        MultiplayerMismatchActions.OpenLastWorkshopLinks();
    }

    public static void CopyMismatchReportFromConfig()
    {
        MultiplayerMismatchActions.CopyLastReport();
    }
}

internal static class NativeMessageBox
{
    private const uint MbOk = 0x00000000;
    private const uint MbOkCancel = 0x00000001;
    private const uint MbIconInformation = 0x00000040;
    private const uint MbIconQuestion = 0x00000020;
    private const int IdOk = 1;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    public static void Show(string text, string caption)
    {
        MessageBoxW(0, text, caption, MbOk | MbIconInformation);
    }

    public static bool Confirm(string text, string caption)
    {
        return MessageBoxW(0, text, caption, MbOkCancel | MbIconQuestion) == IdOk;
    }
}

internal static class CompanionLog
{
    public static void Write(string message)
    {
        try
        {
            var modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(modDir))
            {
                return;
            }

            var dataDir = Path.Combine(modDir, "ModTheSpire2Data");
            Directory.CreateDirectory(dataDir);
            File.AppendAllText(
                Path.Combine(dataDir, "companion.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + "\n");
        }
        catch
        {
        }
    }
}
