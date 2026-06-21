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
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace ModTheSpire2;

[ModInitializer(nameof(Initialize))]
public static class ModTheSpire2Entry
{
    private const string HarmonyId = "HZDH.ModTheSpire2";
    private const string BuildMarker = "0.4.0-hot-order-ui";

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
            UiStyle.ApplyButton(restart);
            restart.Pressed += () => ModManagementDialog.Show(screen);
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
            "To show ModTheSpire2 every time you press Play in Steam, set this Steam launch option:\n\n" +
            launchOption + "\n\n" +
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

            var panel = new PanelContainer
            {
                Name = "Panel",
                AnchorsPreset = (int)Control.LayoutPreset.Center,
                CustomMinimumSize = new Vector2(760, 430),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            panel.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(PanelColor, UiStyle.PanelBorderColor, 3, 8));
            dialog.AddChild(panel);

            var root = new VBoxContainer
            {
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
                CustomMinimumSize = new Vector2(700, 34)
            };
            UiStyle.ApplyTitle(title);
            root.AddChild(title);

            var body = new Label
            {
                Text = "Close the current game and open the ModTheSpire2 launcher?\n\nThe launcher will appear after the game has fully exited. From there, you can launch vanilla or choose which mods to enable for this session.\n\nTo show ModTheSpire2 every time you press Play in Steam, set this Steam launch option:",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(700, 154),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            UiStyle.ApplyMutedLabel(body);
            root.AddChild(body);

            var optionBox = new PanelContainer
            {
                CustomMinimumSize = new Vector2(700, 72),
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
            root.AddChild(optionBox);

            var note = new Label
            {
                Text = "Steam Workshop cannot change launch options automatically.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(700, 34),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            UiStyle.ApplyMutedLabel(note);
            root.AddChild(note);

            var buttons = new FlowContainer
            {
                CustomMinimumSize = new Vector2(700, 56),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
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

            CenterRestartDialog(dialog, panel);
            dialog.Resized += () => CenterRestartDialog(dialog, panel);
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

    private static void CenterRestartDialog(Control root, Control panel)
    {
        var size = root.GetViewportRect().Size;
        var panelWidth = Math.Min(760, Math.Max(360, size.X - 48));
        var panelHeight = Math.Min(430, Math.Max(380, size.Y - 48));
        var panelSize = new Vector2(panelWidth, panelHeight);
        panel.CustomMinimumSize = panelSize;
        panel.Position = new Vector2(
            Math.Max(12, (size.X - panelSize.X) * 0.5f),
            Math.Max(12, (size.Y - panelSize.Y) * 0.5f));
        panel.Size = panelSize;
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

            var mods = ModScanner.Discover();
            var hot = mods.Where(m => m.IsHotCandidate).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var restart = mods.Where(m => !m.IsHotCandidate).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToArray();

            var selectedHot = new System.Collections.Generic.Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
            var dialog = new Control
            {
                Name = "ModTheSpire2ManagementDialog",
                AnchorsPreset = (int)Control.LayoutPreset.FullRect,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 400
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

            var panel = new PanelContainer
            {
                Name = "Panel",
                AnchorsPreset = (int)Control.LayoutPreset.Center,
                CustomMinimumSize = metrics.PanelSize,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            panel.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(PanelColor, UiStyle.PanelBorderColor, 3, 8));
            dialog.AddChild(panel);

            var root = new VBoxContainer
            {
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
                Text = "ModTheSpire2 Management",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(metrics.ContentWidth, 34)
            };
            UiStyle.ApplyTitle(title);
            root.AddChild(title);

            var intro = new Label
            {
                Text = "Hot-Apply saves safe settings for this session. DLL/PCK, gameplay, and unknown mods still require restarting through the launcher.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(metrics.ContentWidth, 42)
            };
            UiStyle.ApplyMutedLabel(intro);
            root.AddChild(intro);

            var scroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(metrics.ContentWidth, metrics.ScrollHeight),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            scroll.AddThemeStyleboxOverride("panel", UiStyle.CreatePanelStyle(new Color(0.09f, 0.065f, 0.045f, 0.55f), new Color(0.31f, 0.21f, 0.12f, 0.9f), 1, 6));
            root.AddChild(scroll);

            var lists = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Theme = new Theme()
            };
            scroll.AddChild(lists);

            AddSectionHeader(lists, "Hot-Apply candidates (" + hot.Length + ")");
            if (hot.Length == 0)
            {
                lists.AddChild(CreateInfoLabel("None detected."));
            }
            foreach (var mod in hot)
            {
                var row = CreateHotCandidateRow(mod);
                row.Toggled += pressed =>
                {
                    if (pressed && !DependenciesSatisfied(mod, selectedHot))
                    {
                        row.ButtonPressed = false;
                        NativeMessageBox.Show("Select this mod's hot-apply dependencies first:\n" + string.Join(", ", mod.Dependencies), "ModTheSpire2");
                    }
                };
                selectedHot[mod.Id] = row;
                lists.AddChild(WrapRow(row));
            }

            AddSectionHeader(lists, "Restart Required (" + restart.Length + ")");
            if (restart.Length == 0)
            {
                lists.AddChild(CreateInfoLabel("None detected."));
            }
            foreach (var mod in restart.Take(40))
            {
                lists.AddChild(CreateRestartRow(mod));
            }
            if (restart.Length > 40)
            {
                lists.AddChild(CreateInfoLabel("...and " + (restart.Length - 40) + " more."));
            }

            AddLoadOrderSection(lists, mods);

            var buttons = new FlowContainer
            {
                CustomMinimumSize = new Vector2(metrics.ContentWidth, metrics.ButtonAreaHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            root.AddChild(buttons);

            var apply = CreateActionButton("Apply Hot Changes");
            apply.Pressed += () =>
            {
                var selected = selectedHot
                    .Where(pair => pair.Value.ButtonPressed)
                    .Select(pair => hot.First(m => string.Equals(m.Id, pair.Key, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                var result = HotApplyService.Apply(selected, hot);
                if (result is HotApplyService.ApplyResult.Succeeded)
                {
                    TryRefreshNativeModdingScreen(dialog);
                    var reopenOwner = dialog.GetParent();
                    dialog.Name = "ModTheSpire2ManagementDialogClosing";
                    CloseDialog("hot-apply-success-refresh");
                    if (reopenOwner is not null)
                    {
                        Show(reopenOwner);
                    }
                }
                else if (result is HotApplyService.ApplyResult.FailedDowngraded or HotApplyService.ApplyResult.UnavailableDowngraded)
                {
                    var reopenOwner = dialog.GetParent();
                    dialog.Name = "ModTheSpire2ManagementDialogClosing";
                    CloseDialog("hot-apply-refresh");
                    if (reopenOwner is not null)
                    {
                        Show(reopenOwner);
                    }
                }
            };
            buttons.AddChild(apply);

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
                CenterPanel(dialog, panel);
            };
            CenterPanel(dialog, panel);
            dialog.CallDeferred(Control.MethodName.GrabFocus);
            CompanionLog.Write("Management dialog shown. hot=" + hot.Length + " restart=" + restart.Length);
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

    private static CheckBox CreateHotCandidateRow(ModScanner.ModSummary mod)
    {
        var row = new CheckBox
        {
            Text = mod.Name + " [" + mod.Id + "] - " + mod.Reason,
            ButtonPressed = mod.IsEnabled,
            TooltipText = DependencyTooltip(mod),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        UiStyle.ApplyBaseText(row);
        row.AddThemeColorOverride("font_hover_color", UiStyle.TextColor);
        row.AddThemeColorOverride("font_pressed_color", UiStyle.AccentColor);
        row.AddThemeColorOverride("font_disabled_color", UiStyle.MutedTextColor);
        return row;
    }

    private static Control CreateRestartRow(ModScanner.ModSummary mod)
    {
        var label = new Label
        {
            Text = mod.Name + " [" + mod.Id + "] - " + mod.Reason,
            TooltipText = DependencyTooltip(mod),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        UiStyle.ApplyMutedLabel(label);
        return WrapRow(label, alternate: true);
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

    private static void CenterPanel(Control root, Control panel)
    {
        var size = root.GetViewportRect().Size;
        var panelSize = UiMetrics.From(size).PanelSize;
        panel.CustomMinimumSize = panelSize;
        panel.Position = new Vector2(
            Math.Max(12, (size.X - panelSize.X) * 0.5f),
            Math.Max(12, (size.Y - panelSize.Y) * 0.5f));
        panel.Size = panelSize;
    }

    private readonly record struct UiMetrics(Vector2 PanelSize, float ContentWidth, float ScrollHeight, float ButtonAreaHeight)
    {
        public static UiMetrics From(Vector2 viewport)
        {
            var availableWidth = Math.Max(360, viewport.X - 48);
            var availableHeight = Math.Max(420, viewport.Y - 48);
            var panelWidth = Math.Min(960, availableWidth);
            var panelHeight = Math.Min(690, availableHeight);
            var contentWidth = Math.Max(300, panelWidth - 60);
            var buttonAreaHeight = panelWidth < 880 ? 104 : 56;
            var scrollHeight = Math.Max(300, panelHeight - 174 - buttonAreaHeight);
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
        mod.Dependencies.Length == 0 ? mod.Reason : mod.Reason + "\nDepends on: " + string.Join(", ", mod.Dependencies);
}

internal static class UiStyle
{
    public static readonly Color PanelBorderColor = new(0.68f, 0.47f, 0.23f, 1f);
    public static readonly Color TextColor = new(0.93f, 0.86f, 0.72f, 1f);
    public static readonly Color MutedTextColor = new(0.76f, 0.67f, 0.52f, 1f);
    public static readonly Color AccentColor = new(0.84f, 0.58f, 0.27f, 1f);
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

    public static void ApplyItemList(ItemList list)
    {
        list.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.11f, 0.075f, 0.048f, 0.96f), new Color(0.36f, 0.245f, 0.13f, 0.9f), 1, 4));
        list.AddThemeStyleboxOverride("selected", CreatePanelStyle(new Color(0.35f, 0.235f, 0.12f, 0.96f), AccentColor, 1, 3));
        list.AddThemeColorOverride("font_color", TextColor);
        list.AddThemeColorOverride("font_selected_color", TextColor);
    }
}

internal static class ModScanner
{
    public sealed record ModSummary(string Id, string Name, ModSource Source, bool IsHotCandidate, bool IsEnabled, string Reason, string[] Dependencies);
    private static readonly System.Collections.Generic.Dictionary<string, string> SessionRestartRequired = new(StringComparer.OrdinalIgnoreCase);

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
        var modDir = LauncherActions.GetModDir();
        var gameDir = FindGameDir(modDir);
        var enabled = ReadEnabledMods();
        var roots = new[]
        {
            new ScanRoot(Path.Combine(gameDir, "mods"), ModSource.ModsDirectory),
            new ScanRoot(FindWorkshopDir(gameDir), ModSource.SteamWorkshop)
        };

        var discovered = roots
            .Where(root => Directory.Exists(root.Path))
            .SelectMany(root => Directory.EnumerateFiles(root.Path, "*.json", SearchOption.AllDirectories)
                .Select(path => new ManifestPath(path, root.Source)))
            .Select(path => TryReadManifest(path.Path, path.Source, enabled))
            .Where(mod => mod is not null)
            .Cast<ModSummary>()
            .GroupBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        return ApplyDependencyClassification(discovered);
    }

    private static ModSummary? TryReadManifest(string path, ModSource source, System.Collections.Generic.HashSet<string> enabled)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var id = GetString(root, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var name = GetString(root, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = id;
            }

            var hasDll = GetBool(root, "has_dll") || Directory.EnumerateFiles(Path.GetDirectoryName(path) ?? "", "*.dll", SearchOption.TopDirectoryOnly).Any();
            var hasPck = GetBool(root, "has_pck") || Directory.EnumerateFiles(Path.GetDirectoryName(path) ?? "", "*.pck", SearchOption.TopDirectoryOnly).Any();
            var affectsGameplay = GetBool(root, "affects_gameplay", defaultValue: true);
            var declaresHot = GetBool(root, "hot_apply") || GetBool(root, "hot_reload");
            var dependencies = GetDependencies(root);
            var isSelf = id.Equals("ModTheSpire2", StringComparison.OrdinalIgnoreCase);
            var hot = !isSelf && (declaresHot || (!affectsGameplay && !hasDll && !hasPck));
            var reason = hot
                ? (declaresHot ? "manifest declares hot-apply" : affectsGameplay ? "ModTheSpire2 utility" : "utility/config candidate")
                : (isSelf ? "launcher/UI patch requires restart" : hasDll || hasPck ? "DLL/PCK or unknown startup behavior" : "gameplay or unknown behavior");

            return new ModSummary(id, name!, source, hot, enabled.Contains(id), reason, dependencies);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Manifest scan failed for " + path + ": " + ex.Message);
            return null;
        }
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

    private static string[] GetDependencies(JsonElement root)
    {
        if (!root.TryGetProperty("dependencies", out var dependencies) || dependencies.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return dependencies.EnumerateArray()
            .Select(dep =>
            {
                if (dep.ValueKind == JsonValueKind.String)
                {
                    return dep.GetString();
                }
                if (dep.ValueKind == JsonValueKind.Object)
                {
                    if (dep.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        return id.GetString();
                    }
                    if (dep.TryGetProperty("mod_id", out var modId) && modId.ValueKind == JsonValueKind.String)
                    {
                        return modId.GetString();
                    }
                }
                return null;
            })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ModSummary[] ApplyDependencyClassification(ModSummary[] mods)
    {
        var result = mods.ToArray();
        var changed = true;
        while (changed)
        {
            changed = false;
            var byId = result.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < result.Length; i++)
            {
                var mod = result[i];
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

                var missingDependency = mod.Dependencies.FirstOrDefault(dep => !byId.ContainsKey(dep));
                if (missingDependency is not null)
                {
                    result[i] = mod with
                    {
                        IsHotCandidate = false,
                        Reason = "missing dependency: " + missingDependency
                    };
                    changed = true;
                    continue;
                }

                var restartDependency = mod.Dependencies.FirstOrDefault(dep =>
                    byId.TryGetValue(dep, out var dependency) && !dependency.IsHotCandidate);
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

    public static ApplyResult Apply(ModScanner.ModSummary[] selectedCandidates, ModScanner.ModSummary[] allHotCandidates)
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

    private static void BackupSettingsFile()
    {
        try
        {
            var settings = Directory.EnumerateFiles(
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "steam"),
                    "settings.save",
                    SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            if (settings is null)
            {
                return;
            }

            var backupDir = Path.Combine(LauncherActions.GetModDir(), "ModTheSpire2Data", "hot-apply-backups");
            Directory.CreateDirectory(backupDir);
            var backup = Path.Combine(backupDir, "settings.save." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");
            File.Copy(settings.FullName, backup, overwrite: false);
            CompanionLog.Write("Hot apply backup created: " + backup);
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Hot apply backup failed: " + ex);
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
                foreach (var dependency in mod.Dependencies)
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
    private static bool s_attempted;

    public static void TryRegister()
    {
        if (s_registered || s_attempted)
        {
            return;
        }

        s_attempted = true;
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

[HarmonyPatch]
internal static class BaseLibModConfigSubmenuPatch
{
    private const string ButtonNodeName = "ModTheSpire2BaseLibButton";

    public static MethodBase? TargetMethod()
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
        var buttonType = Type.GetType("BaseLib.Config.UI.NModListButton, BaseLib", throwOnError: false);
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
                Text = "Open ModTheSpire2 to manage hot-apply candidates, restart-required mods, and the Steam launch option.",
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
        }
        catch (Exception ex)
        {
            CompanionLog.Write("Build BaseLib ModConfig UI failed: " + ex);
        }
    }

    private static Button CreateConfigButton(string text)
    {
        var button = UiStyle.CreateButton(text, 220, 46);
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
