using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.core.settings.ui;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace LibraryOfRuina.patches;

/// <summary>
/// 模组设置子菜单挂在主菜单子菜单栈上，每个栈复用一个实例。原版 GetSubmenuType 不认识这个类型，
/// 所以不经过 PushSubmenuType，直接 Push 这个实例（原版 PushSubmenuType 本身就是 GetSubmenuType 加 Push）。
/// </summary>
internal static class ExtSettingsSubmenuHost
{
    private static readonly ConditionalWeakTable<NMainMenuSubmenuStack, NExtSettingsSubmenu> SubmenuCache = new();

    internal static NExtSettingsSubmenu GetOrCreateSubmenu(NMainMenuSubmenuStack stack)
    {
        if (SubmenuCache.TryGetValue(stack, out var existing) && GodotObject.IsInstanceValid(existing))
            return existing;

        var menu = new NExtSettingsSubmenu();
        menu.Visible = false;
        stack.AddChild(menu);
        SubmenuCache.AddOrUpdate(stack, menu);
        return menu;
    }
}

[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
public static class InjectSettingsScreenModConfigPatch
{
    private const string ButtonNodeName = "ExtModConfigButton";
    private const string ModNameLocKey = "LIBRARYOFRUINA-MOD_NAME.title";
    private const string ModConfigLocKey = "LIBRARYOFRUINA-MOD_CONFIG.title";

    [HarmonyPostfix]
    public static void Postfix(NSettingsScreen __instance)
    {
        if (ExtSettingsRegistry.GetAll().Count == 0) return;
        if (__instance.FindChild(ButtonNodeName, true, false) != null) return;

        try
        {
            InjectSettingsEntry(__instance);
        }
        catch (Exception e)
        {
            Log.Error("[LibraryOfRuina] Failed to add Mod Config entry to settings screen: " + e);
        }
    }

    private static void InjectSettingsEntry(NSettingsScreen settingsScreen)
    {
        var generalSettings = settingsScreen.GetNodeOrNull<Control>("ScrollContainer/Mask/Clipper/GeneralSettings");
        if (generalSettings == null) return;

        var origDivider = generalSettings.GetNodeOrNull<ColorRect>("VBoxContainer/SendFeedbackDivider");
        var moddingRow = generalSettings.GetNodeOrNull<MarginContainer>("VBoxContainer/Modding");
        if (origDivider == null || moddingRow == null) return;

        var newDivider = origDivider.Duplicate();
        var newRow = (MarginContainer)moddingRow.Duplicate();

        newRow.UniqueNameInOwner = false;
        newRow.Name = "ExtModConfig";
        newRow.Visible = true;

        var newButton = newRow.GetNodeOrNull<Control>("ModdingButton");
        if (newButton == null) return;
        newButton.Name = ButtonNodeName;
        newButton.UniqueNameInOwner = true;

        moddingRow.AddSibling(newDivider);
        newDivider.AddSibling(newRow);
        newButton.Owner = settingsScreen;

        string modNameLabel = new LocString("settings_ui", ModNameLocKey).GetFormattedText();
        string modConfigLabel = new LocString("settings_ui", ModConfigLocKey).GetFormattedText();
        var rowLabel = newRow.GetNodeOrNull<RichTextLabel>("Label");
        if (rowLabel != null) rowLabel.Text = modNameLabel;

        var buttonLabel = newButton.GetNodeOrNull<Label>("Label");
        if (buttonLabel != null) buttonLabel.Text = modConfigLabel;

        newButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            var stack = VanillaPrivate.SubmenuStack.Get(settingsScreen);
            if (stack is NMainMenuSubmenuStack stackInstance)
                stackInstance.Push(ExtSettingsSubmenuHost.GetOrCreateSubmenu(stackInstance));
            else
                Log.Warn("[LibraryOfRuina] Mod Config is only available from the main menu settings.");
        }));

        var moddingButton = moddingRow.GetNodeOrNull<Control>("%ModdingButton");
        var creditsButton = generalSettings.GetNodeOrNull<Control>("VBoxContainer/Credits/CreditsButton");

        if (moddingButton != null && creditsButton != null)
        {
            moddingButton.FocusNeighborBottom = moddingButton.GetPathTo(newButton);
            newButton.FocusNeighborTop = newButton.GetPathTo(moddingButton);
            newButton.FocusNeighborBottom = newButton.GetPathTo(creditsButton);
            creditsButton.FocusNeighborTop = creditsButton.GetPathTo(newButton);
        }
    }

    internal static void HideExistingButton(NSettingsScreen settingsScreen)
    {
        if (settingsScreen.FindChild(ButtonNodeName, true, false) is not Control button)
        {
            return;
        }

        button.Visible = false;
        if (button is NButton nButton)
        {
            nButton.Disable();
        }
    }
}

[HarmonyPatch(typeof(NSettingsScreen), "OnSubmenuShown")]
public static class SettingsScreenModConfigVisibilityPatch
{
    [HarmonyPostfix]
    public static void Postfix(NSettingsScreen __instance)
    {
        var button = __instance.FindChild("ExtModConfigButton", true, false) as NButton;
        if (button == null) return;

        var stack = VanillaPrivate.SubmenuStack.Get(__instance);
        if (stack is NMainMenuSubmenuStack)
            button.Enable();
        else
            button.Disable();
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
public static class MainMenuShowPendingSettingsErrorsPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (ExtModSettings.SettingsLogger.PendingUserMessages.Count == 0) return;
        Callable.From(ExtModSettings.ShowAndClearPendingErrors).CallDeferred();
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.Quit))]
public static class NGameQuitExtSettingsSavePatch
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        Log.Info("[LibraryOfRuina] NGame.Quit(): saving all mod settings");
        foreach (var config in ExtSettingsRegistry.GetAll())
            config.Save();
    }
}


// [HarmonyPatch(typeof(NGame), nameof(NGame.IsReleaseGame))]
// public static class NGamePatch
// {
//     public static void Postfix(ref bool __result)
//     {
//         __result = false;
//     }
// }
