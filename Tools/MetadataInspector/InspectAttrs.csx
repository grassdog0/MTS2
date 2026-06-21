using System.Reflection;
var gameDir = @"E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64";
var baseLib = @"E:\SteamLibrary\steamapps\workshop\content\2868840\3737335127\BaseLib\BaseLib.dll";
var paths = Directory.EnumerateFiles(gameDir, "*.dll").Append(baseLib).Distinct().ToArray();
var resolver = new PathAssemblyResolver(paths);
using var context = new MetadataLoadContext(resolver, "System.Private.CoreLib");
var asm = context.LoadFromAssemblyPath(baseLib);
var t = asm.GetType("BaseLib.Config.BaseLibConfig")!;
foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)) {
    var attrs = m.GetCustomAttributesData().ToArray();
    if (attrs.Length == 0) continue;
    Console.WriteLine("METHOD " + m.Name + " (" + string.Join(", ", m.GetParameters().Select(p=>p.ParameterType.FullName+" "+p.Name)) + ")");
    foreach (var a in attrs) Console.WriteLine("  ATTR " + a.AttributeType.FullName + " args=" + string.Join(",", a.ConstructorArguments.Select(x=>x.Value)) + " named=" + string.Join(",", a.NamedArguments.Select(x=>x.MemberName+"="+x.TypedValue.Value)));
}
foreach (var p in t.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)) {
    var attrs = p.GetCustomAttributesData().ToArray();
    if (attrs.Length == 0) continue;
    Console.WriteLine("PROP " + p.PropertyType.FullName + " " + p.Name);
    foreach (var a in attrs) Console.WriteLine("  ATTR " + a.AttributeType.FullName + " args=" + string.Join(",", a.ConstructorArguments.Select(x=>x.Value)) + " named=" + string.Join(",", a.NamedArguments.Select(x=>x.MemberName+"="+x.TypedValue.Value)));
}
