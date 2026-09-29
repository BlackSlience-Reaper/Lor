using System;
using System.Linq;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.features.settings;

// 本局设置载体的来源与去向见 LibraryRunSettings。

/// <summary>
/// 只有房主与单人会走到这里（客户端调用时原版直接抛异常）。改的是参数，原版随后用它构造
/// <c>LobbyBeginRunMessage</c> 并调用本机的 <c>BeginRunLocally</c>；大厅自己的修改器列表不变。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
internal static class LibraryRunSettingsHostBeginRunPatch
{
    [HarmonyPrefix]
    private static void Prefix(StartRunLobby __instance, ref List<ModifierModel> modifiers)
    {
        if (__instance.NetService.Type != NetGameType.Client)
        {
            modifiers = LibraryRunSettings.WithHostCarrier(modifiers);
        }
    }
}

/// <summary>房主与客户端都经过这里，参数是房主发出的那份修改器列表。</summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunLocally")]
internal static class LibraryRunSettingsBeginRunLocallyPatch
{
    [HarmonyPrefix]
    private static void Prefix(string seed, ref List<ModifierModel> modifiers)
    {
        modifiers = LibraryRunSettings.TakeLobbyCarrier(seed, modifiers);
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
internal static class LibraryRunSettingsNewRunPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunState __result, string seed)
    {
        LibraryRunSettings.AttachToNewRun(__result, seed);
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class LibraryRunSettingsLoadPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunState __result)
    {
        LibraryRunSettings.BackfillLoadedRun(__result);
    }
}

[HarmonyPatch(typeof(NTopBarModifier), nameof(NTopBarModifier.Create))]
[LibraryPatch(Reason = "NTopBar.Initialize 为每个局内修饰符创建顶栏图标，ModifierModel 无隐藏开关；仅对本模组的本局设置载体返回 null（原版 TestMode 同样返回 null）。")]
internal static class LibraryRunSettingsHideTopBarPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ModifierModel modifier, ref NTopBarModifier? __result)
    {
        if (modifier is not LibraryRunSettingsModifier)
        {
            return true;
        }

        __result = null;
        return false;
    }
}

/// <summary>
/// 联机每日挑战的读档界面按 <c>Run.Modifiers</c> 的下标逐个填进场景里固定数量的格子（与每日的 3 个修改器一一对应），
/// 多出的载体会越界抛出，房主一端建大厅失败。只在填充期间换成不含载体的列表，结束后换回：
/// 大厅稍后发给客户端的仍是完整的存档。
/// </summary>
[HarmonyPatch(typeof(NDailyRunLoadScreen), "InitializeDisplay")]
internal static class LibraryRunSettingsDailyLoadScreenPatch
{
    [HarmonyPrefix]
    private static void Prefix(LoadRunLobby? ____lobby, out List<SerializableModifier>? __state)
    {
        __state = null;
        if (____lobby?.Run is not { Modifiers: { } modifiers } run)
        {
            return;
        }

        ModelId carrierId = ModelDb.Modifier<LibraryRunSettingsModifier>().Id;
        if (!modifiers.Any(modifier => modifier.Id == carrierId))
        {
            return;
        }

        __state = modifiers;
        run.Modifiers = modifiers.Where(modifier => modifier.Id != carrierId).ToList();
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, LoadRunLobby? ____lobby, List<SerializableModifier>? __state)
    {
        if (__state != null && ____lobby?.Run is { } run)
        {
            run.Modifiers = __state;
        }

        return __exception;
    }
}
