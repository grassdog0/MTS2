using System.Reflection;

var gameDir = @"E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64";
var baseLib = @"E:\SteamLibrary\steamapps\workshop\content\2868840\3737335127\BaseLib\BaseLib.dll";
var targetAssemblyPath = baseLib;

if (args.Length > 1 && args[0] == "--assembly")
{
    targetAssemblyPath = args[1] switch
    {
        "sts2" => Path.Combine(gameDir, "sts2.dll"),
        "baselib" => baseLib,
        var path => path
    };
    args = args.Skip(2).ToArray();
}

var paths = Directory.EnumerateFiles(gameDir, "*.dll").Append(baseLib).Append(targetAssemblyPath).Distinct().ToArray();
var resolver = new PathAssemblyResolver(paths);
using var context = new MetadataLoadContext(resolver, "System.Private.CoreLib");
var assembly = context.LoadFromAssemblyPath(targetAssemblyPath);

if (args.Length >= 2 && args[0] == "--list")
{
    var pattern = args[1];
    foreach (var type in assembly.GetTypes().Where(t => t.FullName?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true))
    {
        Console.WriteLine(type.FullName);
    }
    return;
}

foreach (var typeName in args.Length == 0 ? GetDefaultTypes() : args)
{
    var type = assembly.GetType(typeName);
    Console.WriteLine($"=== {typeName} ===");
    if (type is null)
    {
        Console.WriteLine("MISSING");
        continue;
    }

    Console.WriteLine("Base: " + type.BaseType?.FullName);
    Console.WriteLine("Constructors:");
    foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        Console.WriteLine("  " + DescribeMethod(ctor));
    }

    Console.WriteLine("Methods:");
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        Console.WriteLine("  " + DescribeMethod(method));
    }

    Console.WriteLine("Properties:");
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        Console.WriteLine("  " + property.PropertyType.FullName + " " + property.Name);
    }

    Console.WriteLine("Fields:");
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        Console.WriteLine("  " + field.FieldType.FullName + " " + field.Name);
    }
}

static string[] GetDefaultTypes() =>
[
    "BaseLib.Config.ModConfig",
    "BaseLib.Config.SimpleModConfig",
    "BaseLib.Config.ModConfigRegistry",
    "BaseLib.Config.ConfigButtonAttribute",
    "BaseLib.Config.ConfigSectionAttribute",
    "BaseLib.Config.ConfigTextInputAttribute",
    "BaseLib.Config.UI.NConfigButton",
];

static string DescribeMethod(MethodBase method)
{
    var returnType = method is MethodInfo info ? info.ReturnType.FullName + " " : "";
    var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name));
    var flags = method is MethodInfo mi ? $" virtual={mi.IsVirtual} final={mi.IsFinal}" : "";
    return returnType + method.Name + "(" + parameters + ")" + flags;
}
