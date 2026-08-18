using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ModTheSpire2;

internal interface ICompanionPaths
{
    string ModDirectory { get; }
    string DataDirectory { get; }
    string LauncherPath { get; }
    string ReadmePath { get; }
}

internal interface ICompanionLogSink
{
    void Write(string message);
}

internal interface ILauncherGateway
{
    string LaunchOption { get; }
    bool TryOpen(out string error);
}

internal interface IGameSessionProbe
{
    GameSessionState Detect();
}

internal interface IModCatalog
{
    ModScanner.ModSummary[] Discover(GameSessionState gameState);
}

internal interface ILoadOrderService
{
    List<ModScanner.ModSummary> CreateInitialOrder(ModScanner.ModSummary[] mods);
    List<ModScanner.ModSummary> CreateDefaultOrder(ModScanner.ModSummary[] mods);
    bool TryMove(List<ModScanner.ModSummary> order, int index, int delta, out string message);
    string Save(IReadOnlyList<ModScanner.ModSummary> order);
    string Reset();
}

internal sealed record RuntimeModSnapshot(
    HashSet<string> LoadedModIds,
    HashSet<string> LoadedAssemblyNames,
    IReadOnlyDictionary<string, int> AssemblyCounts,
    bool UsedOfficialAssemblyMap);

internal interface IRuntimeModStateProvider
{
    RuntimeModSnapshot Capture();
}

internal interface IGameCompatibilityInspector
{
    string DescribeCapabilities();
}

internal sealed record CompanionServiceSet(
    ICompanionPaths Paths,
    ICompanionLogSink Log,
    ILauncherGateway Launcher,
    IGameSessionProbe GameSession,
    IModCatalog Mods,
    ILoadOrderService LoadOrder,
    IRuntimeModStateProvider RuntimeMods,
    IGameCompatibilityInspector Compatibility);

internal static class CompanionServices
{
    private static CompanionServiceSet s_current = CreateDefault();

    public static ICompanionPaths Paths => s_current.Paths;
    public static ICompanionLogSink Log => s_current.Log;
    public static ILauncherGateway Launcher => s_current.Launcher;
    public static IGameSessionProbe GameSession => s_current.GameSession;
    public static IModCatalog Mods => s_current.Mods;
    public static ILoadOrderService LoadOrder => s_current.LoadOrder;
    public static IRuntimeModStateProvider RuntimeMods => s_current.RuntimeMods;
    public static IGameCompatibilityInspector Compatibility => s_current.Compatibility;

    internal static void Configure(CompanionServiceSet services) =>
        s_current = services ?? throw new ArgumentNullException(nameof(services));

    internal static void ResetDefaults() => s_current = CreateDefault();

    private static CompanionServiceSet CreateDefault()
    {
        var paths = new DefaultCompanionPaths();
        return new CompanionServiceSet(
            paths,
            new FileCompanionLogSink(paths),
            new DefaultLauncherGateway(paths),
            new DefaultGameSessionProbe(),
            new DefaultModCatalog(),
            new DefaultLoadOrderService(),
            new OfficialRuntimeModStateProvider(),
            new OfficialGameCompatibilityInspector());
    }
}

internal sealed class DefaultCompanionPaths : ICompanionPaths
{
    public string ModDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
    public string DataDirectory => Path.Combine(ModDirectory, "ModTheSpire2Data");
    public string LauncherPath => Path.Combine(ModDirectory, "ModTheSpire2Launcher.exe");
    public string ReadmePath => Path.Combine(ModDirectory, "README.md");
}

internal sealed class FileCompanionLogSink(ICompanionPaths paths) : ICompanionLogSink
{
    public void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(paths.DataDirectory);
            File.AppendAllText(
                Path.Combine(paths.DataDirectory, "companion.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + "\n");
        }
        catch
        {
        }
    }
}

internal sealed class DefaultLauncherGateway(ICompanionPaths paths) : ILauncherGateway
{
    public string LaunchOption => $"\"{paths.LauncherPath}\" -- %command%";

    public bool TryOpen(out string error)
    {
        var target = File.Exists(paths.LauncherPath) ? paths.LauncherPath : paths.ReadmePath;
        if (!File.Exists(target))
        {
            error = "ModTheSpire2Launcher.exe was not found. Please check that the mod files are complete.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                WorkingDirectory = paths.ModDirectory,
                UseShellExecute = true
            });
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

internal sealed class DefaultGameSessionProbe : IGameSessionProbe
{
    public GameSessionState Detect() => GameSessionState.Detect();
}

internal sealed class DefaultModCatalog : IModCatalog
{
    public ModScanner.ModSummary[] Discover(GameSessionState gameState) => ModScanner.Discover(gameState);
}

internal sealed class DefaultLoadOrderService : ILoadOrderService
{
    public List<ModScanner.ModSummary> CreateInitialOrder(ModScanner.ModSummary[] mods) => LoadOrderManager.CreateInitialOrder(mods);
    public List<ModScanner.ModSummary> CreateDefaultOrder(ModScanner.ModSummary[] mods) => LoadOrderManager.CreateDefaultOrder(mods);
    public bool TryMove(List<ModScanner.ModSummary> order, int index, int delta, out string message) =>
        LoadOrderManager.TryMove(order, index, delta, out message);
    public string Save(IReadOnlyList<ModScanner.ModSummary> order) => LoadOrderManager.Save(order);
    public string Reset() => LoadOrderManager.Reset();
}

internal sealed class OfficialRuntimeModStateProvider : IRuntimeModStateProvider
{
    public RuntimeModSnapshot Capture()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ModTheSpire2" };
        var assemblyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var usedOfficialMap = false;

        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    assemblies.Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Runtime assembly enumeration failed: " + ex.Message);
        }

        try
        {
            var assemblyInfo = FindType("MegaCrit.Sts2.Core.Modding.AssemblyInfo");
            var modMap = assemblyInfo?.GetProperty("ModMap", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as IEnumerable;
            if (modMap is not null)
            {
                foreach (var pair in modMap)
                {
                    if (pair is null)
                    {
                        continue;
                    }

                    var pairType = pair.GetType();
                    var assembly = pairType.GetProperty("Key")?.GetValue(pair) as Assembly;
                    var mod = pairType.GetProperty("Value")?.GetValue(pair);
                    var id = ReadModId(mod);
                    if (assembly is not null && !string.IsNullOrWhiteSpace(assembly.GetName().Name))
                    {
                        assemblies.Add(assembly.GetName().Name!);
                    }
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        ids.Add(id);
                        assemblyCounts[id] = assemblyCounts.TryGetValue(id, out var count) ? count + 1 : 1;
                    }
                }
                usedOfficialMap = true;
            }
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Official AssemblyInfo.ModMap read failed: " + ex.Message);
        }

        return new RuntimeModSnapshot(ids, assemblies, assemblyCounts, usedOfficialMap);
    }

    private static string ReadModId(object? mod)
    {
        if (mod is null)
        {
            return "";
        }

        var manifest = mod.GetType().GetField("manifest", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mod);
        return manifest?.GetType().GetField("id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(manifest) as string ?? "";
    }

    private static Type? FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                if (type is not null)
                {
                    return type;
                }
            }
            catch
            {
            }
        }
        return null;
    }
}

internal sealed class OfficialGameCompatibilityInspector : IGameCompatibilityInspector
{
    public string DescribeCapabilities()
    {
        try
        {
            var modManager = FindType("MegaCrit.Sts2.Core.Modding.ModManager");
            var assemblyInfo = FindType("MegaCrit.Sts2.Core.Modding.AssemblyInfo");
            var hasOfficialModLists = modManager?.GetMethod("GetLoadedMods", BindingFlags.Public | BindingFlags.Static) is not null;
            var hasAssemblyAssociation = modManager?.GetMethod("AssociateAssemblyWithMod", BindingFlags.Public | BindingFlags.Static) is not null;
            var hasAssemblyMap = assemblyInfo?.GetProperty("ModMap", BindingFlags.Public | BindingFlags.Static) is not null;
            return $"official APIs: loadedMods={hasOfficialModLists}, multiAssembly={hasAssemblyAssociation && hasAssemblyMap}";
        }
        catch (Exception ex)
        {
            return "official API inspection failed: " + ex.Message;
        }
    }

    private static Type? FindType(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly =>
        {
            try { return assembly.GetType(fullName, throwOnError: false, ignoreCase: false); }
            catch { return null; }
        })
        .FirstOrDefault(type => type is not null);
}
