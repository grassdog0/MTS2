using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

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

            var parent = screen.FindChild("ModsBorder", recursive: true, owned: false) as Control ?? screen;
            var template = parent.FindChild("GetModsButton", recursive: true, owned: false) as Control;
            var restart = new Button
            {
                Name = RestartButtonNodeName,
                Text = "ModTheSpire2 Launcher",
                CustomMinimumSize = template?.CustomMinimumSize ?? new Vector2(260, 56),
                Size = template?.Size ?? new Vector2(260, 48),
                Position = FindButtonPosition(parent),
                AnchorsPreset = (int)Control.LayoutPreset.TopLeft,
                TooltipText = "ModTheSpire2 Launcher",
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = true,
                TopLevel = false,
                ZIndex = 100
            };
            restart.Pressed += () => RestartToLauncher.ShowConfirm(screen);
            parent.AddChild(restart);
            parent.MoveChild(restart, parent.GetChildCount() - 1);
            restart.Show();
            CompanionLog.Write("Visible fixed modding screen button added from " + source);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Modding screen button failed from " + source + ": " + ex);
        }
    }

    private static Vector2 FindButtonPosition(Node parent)
    {
        try
        {
            if (parent.FindChild("InstalledModsTitle", recursive: true, owned: false) is Control title)
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
}

internal static class RestartToLauncher
{
    public static void ShowConfirm(Node owner)
    {
        var modDir = LauncherActions.GetModDir();
        var launcher = LauncherActions.GetLauncherPath();
        var launchOption = LauncherActions.GetLaunchOption();
        var message =
            "Close the current game and open the ModTheSpire2 launcher?\n\n" +
            "The launcher will appear after the game has fully exited. From there, you can launch vanilla or choose which mods to enable for this session.\n\n" +
            "To show ModTheSpire2 every time you press Play in Steam, set this Steam launch option:\n\n" +
            launchOption + "\n\n" +
            "Steam Workshop cannot change launch options automatically.";

        try
        {
            var dialog = new ConfirmationDialog
            {
                Name = "ModTheSpire2RestartDialog",
                Title = "ModTheSpire2 Launcher",
                DialogText = message,
                OkButtonText = "Close and Open Launcher",
                CancelButtonText = "Cancel",
                DialogAutowrap = true,
                MinSize = new Vector2I(720, 420),
                Unresizable = true,
                Exclusive = true
            };

            owner.AddChild(dialog);
            var copyButton = dialog.AddButton("Copy Launch Option", right: true, action: "copy_launch_option");
            copyButton.Pressed += () =>
            {
                DisplayServer.ClipboardSet(launchOption);
                copyButton.Text = "Copied";
            };
            dialog.Confirmed += () => Restart(launcher, modDir);
            dialog.Canceled += dialog.QueueFree;
            dialog.CloseRequested += dialog.QueueFree;
            dialog.PopupCentered(new Vector2I(720, 420));
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Godot dialog failed: " + ex);
            if (NativeMessageBox.Confirm(message, "ModTheSpire2 Launcher"))
            {
                Restart(launcher, modDir);
            }
        }
    }

    private static void Restart(string launcher, string modDir)
    {
        if (!File.Exists(launcher))
        {
            NativeMessageBox.Show("ModTheSpire2Launcher.exe was not found. Please check that the mod files are complete.", "ModTheSpire2");
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
