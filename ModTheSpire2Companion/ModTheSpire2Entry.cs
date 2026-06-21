using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Localization;

namespace ModTheSpire2;

[ModInitializer(nameof(Initialize))]
public static class ModTheSpire2Entry
{
    private const string HarmonyId = "HZDH.ModTheSpire2";

    public static void Initialize()
    {
        try
        {
            CompanionLog.Write("Initialize");
            new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
            ModConfigIntegration.TryRegister();
            CompanionLog.Write("PatchAll complete");
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("Initialize failed: " + ex);
            // Never break game startup if UI patching fails.
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuReadyPatch
{
    private const string ButtonNodeName = "ModTheSpire2Button";

    public static void Prefix()
    {
        ModConfigIntegration.TryRegister();
    }

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

            var template = FindTemplateButton(__instance);
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
            button.Released += OpenLauncherOrGuide;
            CompanionLog.Write("Main menu button added under " + parent.GetPath());
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("Main menu button failed: " + ex);
            // Best effort only. A missing menu button is better than a broken main menu.
        }
    }

    private static NMainMenuTextButton? FindTemplateButton(Node root) =>
        root.FindChildren("*", nameof(NMainMenuTextButton), recursive: true, owned: false)
            .OfType<NMainMenuTextButton>()
            .FirstOrDefault();

    private static void OpenLauncherOrGuide(NClickableControl _)
    {
        LauncherActions.OpenLauncher();
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
            if (screen.FindChild(RestartButtonNodeName, recursive: true, owned: false) is not null)
            {
                return;
            }

            AddPlainFallbackButton(screen, source);
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("Modding screen button failed from " + source + ": " + ex);
            // Best effort only. Do not break the game's mod management screen.
        }
    }

    private static void AddPlainFallbackButton(NModdingScreen screen, string source)
    {
        if (screen.FindChild(RestartButtonNodeName, recursive: true, owned: false) is null)
        {
            var parent = screen.FindChild("ModsBorder", recursive: true, owned: false) as Control ?? screen;
            var template = parent.FindChild("GetModsButton", recursive: true, owned: false) as Control;
            var restart = CreatePlainButton(RestartButtonNodeName, I18n.RestartButton, FindButtonPosition(parent), template);
            restart.Pressed += () => RestartToLauncher.ShowConfirmFromButton(screen);
            parent.AddChild(restart);
            parent.MoveChild(restart, parent.GetChildCount() - 1);
            restart.Show();
            restart.ZIndex = 100;
        }

        CompanionLog.Write("Visible fixed modding screen button added from " + source);
    }

    private static Vector2 FindButtonPosition(Node parent)
    {
        try
        {
            var title = parent.FindChild("InstalledModsTitle", recursive: true, owned: false) as Control;
            if (title is not null)
            {
                var x = title.Position.X + title.Size.X + 28;
                if (x < 260)
                {
                    x = 260;
                }
                return new Vector2(x, title.Position.Y - 8);
            }
        }
        catch
        {
        }

        return new Vector2(330, 16);
    }

    private static Button CreatePlainButton(string nodeName, string text, Vector2 position, Control? template)
    {
        var button = new Button
        {
            Name = nodeName,
            Text = text,
            CustomMinimumSize = template?.CustomMinimumSize ?? new Vector2(260, 56),
            Size = template?.Size ?? new Vector2(260, 48),
            Position = position,
            AnchorsPreset = (int)Control.LayoutPreset.TopLeft,
            TooltipText = text,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = true,
            TopLevel = false
        };
        return button;
    }

    private static string NodeDump(Node root)
    {
        try
        {
            return string.Join(" | ", root.FindChildren("*", recursive: true, owned: false)
                .OfType<Node>()
                .Take(80)
                .Select(n => n.GetPath() + " [" + n.GetType().Name + "]"));
        }
        catch
        {
            return "<node dump failed>";
        }
    }
}

internal static class RestartToLauncher
{
    public static void ShowConfirm(NClickableControl _)
    {
        ShowConfirmDialog(_);
    }

    public static void ShowConfirmFromButton(Node owner)
    {
        ShowConfirmDialog(owner);
    }

    private static void ShowConfirmDialog(Node owner)
    {
        var modDir = LauncherActions.GetModDir();
        var launcher = LauncherActions.GetLauncherPath();
        var launchOption = LauncherActions.GetLaunchOption();
        var message = I18n.RestartDialogMessage(launchOption);

        try
        {
            var dialog = new ConfirmationDialog
            {
                Name = "ModTheSpire2RestartDialog",
                Title = I18n.RestartDialogTitle,
                DialogText = message,
                OkButtonText = I18n.RestartOk,
                CancelButtonText = I18n.Cancel,
                DialogAutowrap = true,
                MinSize = new Vector2I(720, 420),
                Unresizable = true,
                Exclusive = true
            };

            owner.AddChild(dialog);
            var copyButton = dialog.AddButton(I18n.CopyLaunchOption, right: true, action: "copy_launch_option");
            copyButton.Pressed += () =>
            {
                DisplayServer.ClipboardSet(launchOption);
                copyButton.Text = I18n.Copied;
            };
            dialog.Confirmed += () => Restart(launcher, modDir);
            dialog.Canceled += dialog.QueueFree;
            dialog.CloseRequested += dialog.QueueFree;
            dialog.PopupCentered(new Vector2I(720, 420));
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("Godot dialog failed: " + ex);
            if (NativeMessageBox.Confirm(message, I18n.RestartDialogTitle))
            {
                Restart(launcher, modDir);
            }
        }
    }

    private static void Restart(string launcher, string modDir)
    {
        if (!File.Exists(launcher))
        {
            NativeMessageBox.Show(I18n.MissingLauncher, "ModTheSpire2");
            return;
        }
        try
        {
            var pid = Process.GetCurrentProcess().Id;
            Process.Start(new ProcessStartInfo
            {
                FileName = launcher,
                Arguments = "--wait-for-pid " + pid,
                WorkingDirectory = modDir,
                UseShellExecute = true
            });
            CompanionLog.Write("Started launcher for restart, pid=" + pid);
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("Start launcher failed: " + ex);
            NativeMessageBox.Show(I18n.StartLauncherFailed + "\n" + ex.Message, "ModTheSpire2");
            return;
        }

        try
        {
            NGame.Instance?.Quit();
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("NGame.Quit failed: " + ex);
            try
            {
                if (Engine.GetMainLoop() is SceneTree tree)
                {
                    tree.Quit(0);
                }
            }
            catch (System.Exception treeEx)
            {
                CompanionLog.Write("SceneTree.Quit failed: " + treeEx);
            }
        }
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
            var registryType = Type.GetType("BaseLib.Config.ModConfigRegistry, BaseLib", throwOnError: false);
            var simpleConfigType = Type.GetType("BaseLib.Config.SimpleModConfig, BaseLib", throwOnError: false);
            var buttonAttributeType = Type.GetType("BaseLib.Config.ConfigButtonAttribute, BaseLib", throwOnError: false);
            if (registryType is null || simpleConfigType is null || buttonAttributeType is null)
            {
                CompanionLog.Write("BaseLib ModConfig not present");
                return;
            }

            var configType = DynamicConfigType.Create(simpleConfigType, buttonAttributeType);
            var config = System.Activator.CreateInstance(configType);
            simpleConfigType.GetProperty("ModId")?.SetValue(config, "ModTheSpire2");
            registryType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, ["ModTheSpire2", config]);
            s_registered = true;
            CompanionLog.Write("BaseLib ModConfig registered");
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("BaseLib ModConfig registration failed: " + ex);
        }
    }
}

[HarmonyPatch]
internal static class BaseLibModConfigSubmenuPatch
{
    private const string ButtonNodeName = "ModTheSpire2BaseLibButton";

    public static System.Reflection.MethodBase? TargetMethod()
    {
        var type = Type.GetType("BaseLib.Config.UI.NModConfigSubmenu, BaseLib", throwOnError: false);
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
                CompanionLog.Write("BaseLib submenu fallback list parent not found: " + NodeDump(__instance));
                return;
            }

            var button = CreateBaseLibListButton(__instance);
            listParent.AddChild(button);
            CompanionLog.Write("BaseLib submenu fallback button added under " + listParent.GetPath());
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("BaseLib submenu fallback failed: " + ex);
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
        var buttonType = Type.GetType("BaseLib.Config.UI.NModListButton, BaseLib", throwOnError: false);
        if (buttonType is not null)
        {
            try
            {
                if (System.Activator.CreateInstance(buttonType, ["ModTheSpire2"]) is Control nativeButton)
                {
                    nativeButton.Name = ButtonNodeName;
                    nativeButton.TooltipText = I18n.ConfigIntro;
                    nativeButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    if (!AttachReleased(nativeButton, () => RestartToLauncher.ShowConfirmFromButton(owner)))
                    {
                        CompanionLog.Write("BaseLib native list button event attach failed; using overlay input event");
                        nativeButton.GuiInput += input =>
                        {
                            if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
                            {
                                RestartToLauncher.ShowConfirmFromButton(owner);
                            }
                        };
                    }
                    return nativeButton;
                }
            }
            catch (System.Exception ex)
            {
                CompanionLog.Write("Create BaseLib native list button failed: " + ex);
            }
        }

        var fallback = new Button
        {
            Name = ButtonNodeName,
            Text = "ModTheSpire2",
            CustomMinimumSize = new Vector2(260, 66),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = I18n.ConfigIntro,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        fallback.Pressed += () => RestartToLauncher.ShowConfirmFromButton(owner);
        return fallback;
    }

    private static bool AttachReleased(object target, System.Action action)
    {
        try
        {
            var eventInfo = target.GetType().GetEvent("Released");
            if (eventInfo is null)
            {
                return false;
            }

            var invoke = eventInfo.EventHandlerType?.GetMethod("Invoke");
            var parameters = invoke?.GetParameters() ?? [];
            var handler = parameters.Length switch
            {
                0 => System.Delegate.CreateDelegate(eventInfo.EventHandlerType!, action.Target, action.Method),
                1 => CreateOneArgDelegate(eventInfo.EventHandlerType!, action),
                _ => null
            };
            if (handler is null)
            {
                return false;
            }

            eventInfo.AddEventHandler(target, handler);
            return true;
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("Attach Released failed: " + ex);
            return false;
        }
    }

    private static System.Delegate CreateOneArgDelegate(Type delegateType, System.Action action)
    {
        var method = typeof(BaseLibModConfigSubmenuPatch).GetMethod(nameof(OnNativeListButtonReleased), BindingFlags.NonPublic | BindingFlags.Static)!;
        return System.Delegate.CreateDelegate(delegateType, action, method);
    }

    private static void OnNativeListButtonReleased(this System.Action action, object _)
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

internal static class DynamicConfigType
{
    private static Type? s_type;

    public static Type Create(Type baseType, Type buttonAttributeType)
    {
        if (s_type is not null)
        {
            return s_type;
        }

        var assemblyName = new AssemblyName("ModTheSpire2DynamicConfig");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var type = module.DefineType(
            "ModTheSpire2.DynamicConfig",
            TypeAttributes.Public | TypeAttributes.Class,
            baseType);

        var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var ctorIl = ctor.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        var baseCtor = baseType.GetConstructor(Type.EmptyTypes) ?? throw new System.InvalidOperationException("SimpleModConfig constructor not found");
        ctorIl.Emit(OpCodes.Call, baseCtor);
        ctorIl.Emit(OpCodes.Ret);

        DefineButtonMethod(type, buttonAttributeType, "OpenLauncherButton", "ModTheSpire2_OpenLauncher", nameof(OpenLauncherFromConfig));
        DefineButtonMethod(type, buttonAttributeType, "CopyLaunchOptionButton", "ModTheSpire2_CopyLaunchOption", nameof(CopyLaunchOptionFromConfig));

        var method = type.DefineMethod(
            "SetupConfigUI",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(void),
            [typeof(Control)]);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        var baseMethod = baseType.GetMethod("SetupConfigUI", [typeof(Control)])
            ?? throw new System.InvalidOperationException("SimpleModConfig.SetupConfigUI not found");
        il.Emit(OpCodes.Call, baseMethod);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, typeof(DynamicConfigType).GetMethod(nameof(BuildUi), BindingFlags.Public | BindingFlags.Static)!);
        il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(method, baseMethod);

        s_type = type.CreateType()!;
        return s_type;
    }

    private static void DefineButtonMethod(TypeBuilder type, Type attributeType, string methodName, string labelKey, string targetName)
    {
        var method = type.DefineMethod(
            methodName,
            MethodAttributes.Public,
            typeof(void),
            [type.BaseType!]);

        var attrCtor = attributeType.GetConstructor([typeof(string)])
            ?? throw new System.InvalidOperationException("ConfigButtonAttribute constructor not found");
        method.SetCustomAttribute(new CustomAttributeBuilder(attrCtor, [labelKey]));

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(DynamicConfigType).GetMethod(targetName, BindingFlags.Public | BindingFlags.Static)!);
        il.Emit(OpCodes.Ret);
    }

    public static void BuildUi(Control optionContainer)
    {
        try
        {
            var label = new RichTextLabel
            {
                Text = I18n.ConfigIntro,
                FitContent = true,
                CustomMinimumSize = new Vector2(760, 54),
                BbcodeEnabled = false
            };
            optionContainer.AddChild(label);

            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(760, 64),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            optionContainer.AddChild(row);

            var open = CreateConfigButton(I18n.OpenLauncher);
            open.Pressed += LauncherActions.OpenLauncher;
            row.AddChild(open);

            var copy = CreateConfigButton(I18n.CopyLaunchOption);
            copy.Pressed += () =>
            {
                DisplayServer.ClipboardSet(LauncherActions.GetLaunchOption());
                copy.Text = I18n.Copied;
            };
            row.AddChild(copy);
        }
        catch (System.Exception ex)
        {
            CompanionLog.Write("Build BaseLib ModConfig UI failed: " + ex);
        }
    }

    private static Button CreateConfigButton(string text) =>
        new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(260, 52),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            TooltipText = text
        };

    public static void OpenLauncherFromConfig()
    {
        LauncherActions.OpenLauncher();
    }

    public static void CopyLaunchOptionFromConfig()
    {
        DisplayServer.ClipboardSet(LauncherActions.GetLaunchOption());
    }
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
            NativeMessageBox.Show(I18n.MissingLauncher, "ModTheSpire2");
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

internal static class I18n
{
    private static bool IsChinese
    {
        get
        {
            try
            {
                var language = LocManager.Instance?.Language ?? TranslationServer.GetLocale();
                return language.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase) ||
                       language.StartsWith("zhs", System.StringComparison.OrdinalIgnoreCase) ||
                       language.StartsWith("zht", System.StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public static string RestartButton => IsChinese ? "ModTheSpire2 启动器" : "ModTheSpire2 Launcher";
    public static string RestartDialogTitle => IsChinese ? "ModTheSpire2 启动器" : "ModTheSpire2 Launcher";
    public static string RestartOk => IsChinese ? "关闭并打开启动器" : "Close and Open Launcher";
    public static string CopyLaunchOption => IsChinese ? "复制启动选项" : "Copy Launch Option";
    public static string Copied => IsChinese ? "已复制" : "Copied";
    public static string Cancel => IsChinese ? "取消" : "Cancel";
    public static string ConfigIntro => IsChinese ? "从这里可以打开启动器，或复制 Steam 启动选项。" : "Open the launcher or copy the Steam launch option from here.";
    public static string OpenLauncher => IsChinese ? "打开启动器" : "Open Launcher";
    public static string MissingLauncher => IsChinese ? "找不到 ModTheSpire2Launcher.exe。请检查 mod 文件是否完整。" : "ModTheSpire2Launcher.exe was not found. Please check that the mod files are complete.";
    public static string StartLauncherFailed => IsChinese ? "启动 ModTheSpire2Launcher.exe 失败：" : "Failed to start ModTheSpire2Launcher.exe:";

    public static string RestartDialogMessage(string launchOption) =>
        IsChinese
            ? "要关闭当前游戏，并打开 ModTheSpire2 启动器吗？\n\n" +
              "启动器会在游戏完全退出后显示。你可以选择原版启动，或选择本次要启用的模组。\n\n" +
              "如果你希望以后每次从 Steam 点击“开始游戏”时都先打开 ModTheSpire2，请在 Steam 启动选项里填入：\n\n" +
              launchOption + "\n\n" +
              "Steam Workshop 不能自动替玩家修改启动选项。"
            : "Close the current game and open the ModTheSpire2 launcher?\n\n" +
              "The launcher will appear after the game has fully exited. From there, you can launch vanilla or choose which mods to enable for this session.\n\n" +
              "To show ModTheSpire2 every time you press Play in Steam, set this Steam launch option:\n\n" +
              launchOption + "\n\n" +
              "Steam Workshop cannot change launch options automatically.";
}

internal static class ButtonText
{
    public static void Set(Node node, string text)
    {
        TryCall(node, "SetText", text);
        TryCall(node, "SetLocalization", text);
        SetTextProperties(node, text);

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                Set(childNode, text);
            }
        }
    }

    private static void TryCall(object target, string methodName, string text)
    {
        try
        {
            target.GetType().GetMethod(methodName, [typeof(string)])?.Invoke(target, [text]);
        }
        catch
        {
            // Some game nodes expose one text API but not the other.
        }
    }

    private static void SetTextProperties(object target, string text)
    {
        try
        {
            var property = target.GetType().GetProperty("Text");
            if (property?.CanWrite == true && property.PropertyType == typeof(string))
            {
                property.SetValue(target, text);
            }
        }
        catch
        {
            // Best effort only.
        }
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
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + "\n");
        }
        catch
        {
            // Logging must never affect game startup.
        }
    }
}
