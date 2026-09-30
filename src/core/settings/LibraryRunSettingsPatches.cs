using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.specialguests;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.core.settings;

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

/// <summary>
/// 房主与客户端都经过这里，参数是房主发出的那份修改器列表。未注入模式也安装（见 LibraryRunInjectionGuard）：
/// 客户端记下房主有没有发载体，并照样取出载体，免得标准模式的选角界面因列表非空报错。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunLocally")]
internal static class LibraryRunSettingsBeginRunLocallyPatch
{
    [HarmonyPrefix]
    private static void Prefix(StartRunLobby __instance, string seed, ref List<ModifierModel> modifiers)
    {
        if (__instance.NetService.Type == NetGameType.Client)
        {
            LibraryRunInjectionGuard.RecordLobbyHost(modifiers.OfType<LibraryRunSettingsModifier>().Any());
        }

        modifiers = LibraryRunSettings.TakeLobbyCarrier(seed, modifiers);
    }
}

/// <summary>
/// 客户端新局：两端注入状态不一致时在这里抛出。原版在 NGame.StartNewMultiplayerRun 里调用它，
/// 各界面（标准、自定义、每日挑战）的调用点都在 try 里，catch 断开大厅并回主菜单弹窗。此时 RunState 已建好
/// 但还没交给 RunManager，没有留下局内状态。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class LibraryRunInjectionNewRunGuardPatch
{
    [HarmonyPrefix]
    private static void Prefix(StartRunLobby lobby)
    {
        bool? hostInjected = LibraryRunInjectionGuard.TakeLobbyHost();
        if (lobby.NetService.Type == NetGameType.Client
            && hostInjected is { } injected
            && LibraryRunInjectionGuard.Check(LibraryOfRuinaSettings.ContentInjected, injected, "new run") is { } mismatch)
        {
            throw mismatch;
        }
    }
}

/// <summary>客户端读档：同上，原版三个读档界面的 StartRun 都在 try 里调用它。</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class LibraryRunInjectionLoadGuardPatch
{
    [HarmonyPrefix]
    private static void Prefix(LoadRunLobby lobby)
    {
        if (lobby.NetService.Type == NetGameType.Client
            && LibraryRunInjectionGuard.Check(
                LibraryOfRuinaSettings.ContentInjected,
                LibraryRunInjectionGuard.HasCarrier(lobby.Run),
                "loaded run") is { } mismatch)
        {
            throw mismatch;
        }
    }
}

/// <summary>未注入的房主去掉联机存档里残留的载体；注入模式下什么也不做（载体由 FromSerializable 补建写入）。</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CanonicalizeSave))]
internal static class LibraryRunInjectionCanonicalizePatch
{
    [HarmonyPostfix]
    private static void Postfix(SerializableRun __result)
    {
        if (!LibraryOfRuinaSettings.ContentInjected)
        {
            LibraryRunInjectionGuard.StripCarrier(__result);
        }
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

/// <summary>
/// 本模组挂在 <c>RunState.Modifiers</c> 上的状态载体：本局设置、特邀嘉宾状态、第二升华。它们只借修改器随存档与开局、读档消息保存，
/// 没有给玩家看的效果，而原版界面按 <c>Modifiers</c> 逐个显示（顶栏图标、联机每日挑战读档界面的格子）。
/// 这些界面都只经这里判断要不要跳过；以后新增载体只改这里。
/// <para>
/// 未注入模式也用它（见 <see cref="LibraryRunInjectionGuard.PatchForNonInjectedProcess"/>）：模型类型随程序集登记，
/// 未注入的一端照样能读出带载体的旧局；判断只看类型或模型 ID，不依赖注入时的任何状态。
/// </para>
/// </summary>
internal static class LibraryHiddenModifiers
{
    private static readonly Type[] CarrierTypes =
    [
        typeof(LibraryRunSettingsModifier),
        typeof(SpecialGuestRunStateModifier),
        typeof(LibrarySecondAscensionModifier),
    ];

    internal static bool IsHidden(ModifierModel modifier) => CarrierTypes.Contains(modifier.GetType());

    internal static bool IsHidden(ModelId id) => CarrierTypes.Any(type => ModelDb.GetId(type) == id);
}

[HarmonyPatch(typeof(NTopBarModifier), nameof(NTopBarModifier.Create))]
[LibraryPatch(Reason = "NTopBar.Initialize 为每个局内修饰符创建顶栏图标，ModifierModel 无隐藏开关；仅对本模组的状态载体（LibraryHiddenModifiers）返回 null（原版 TestMode 同样返回 null）。")]
internal static class LibraryHiddenModifierTopBarPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ModifierModel modifier, ref NTopBarModifier? __result)
    {
        if (!LibraryHiddenModifiers.IsHidden(modifier))
        {
            return true;
        }

        __result = null;
        return false;
    }
}

/// <summary>
/// 原版按 <c>Modifiers.Count &gt; 0</c> 显示修改器容器；载体的图标都跳过之后容器是空的，原版 <c>UpdateNavigation</c>
/// 对空容器取 <c>First()</c> 会抛出（它经 CallDeferred 调用，在这个后缀之后）。只要有载体而容器里没有图标，就隐藏容器。
/// 注入模式下第二升华的顶栏后缀随后会按同样的条件再算一次可见性。
/// </summary>
[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
internal static class LibraryHiddenModifierEmptyContainerPatch
{
    [HarmonyPostfix]
    private static void Postfix(NTopBar __instance, IRunState runState)
    {
        if (!runState.Modifiers.Any(LibraryHiddenModifiers.IsHidden)
            || VanillaPrivate.TopBarModifiersContainer.Get(__instance) is not Control container)
        {
            return;
        }

        bool hasVisibleChildren = container.GetChildren()
            .OfType<Control>()
            .Any(static child => GodotObject.IsInstanceValid(child));
        if (!hasVisibleChildren)
        {
            container.Visible = false;
        }
    }
}

/// <summary>
/// 联机每日挑战的读档界面按 <c>Run.Modifiers</c> 的下标逐个填进场景里固定数量的格子（与每日的 3 个修改器一一对应），
/// 多出的载体会越界抛出，房主一端建大厅失败。只在填充期间换成不含载体的列表，结束后换回：
/// 大厅稍后发给客户端的仍是完整的存档。
/// </summary>
[HarmonyPatch(typeof(NDailyRunLoadScreen), "InitializeDisplay")]
internal static class LibraryHiddenModifierDailyLoadScreenPatch
{
    [HarmonyPrefix]
    private static void Prefix(LoadRunLobby? ____lobby, out List<SerializableModifier>? __state)
    {
        __state = null;
        if (____lobby?.Run is not { Modifiers: { } modifiers } run
            || !modifiers.Any(static modifier => LibraryHiddenModifiers.IsHidden(modifier.Id)))
        {
            return;
        }

        __state = modifiers;
        run.Modifiers = modifiers.Where(static modifier => !LibraryHiddenModifiers.IsHidden(modifier.Id)).ToList();
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
