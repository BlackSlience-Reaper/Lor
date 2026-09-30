using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.patches;

/// <summary>
/// NCombatUi.Enable 在战斗 UI 完成 _Ready/Activate 之前被调用会抛空引用，本补丁负责在这段窗口内跳过原方法。
/// 但 NCombatUi.Disable 没有对应保护：任何界面（暂停菜单、抽牌堆、地图、选牌界面）覆盖战斗时都会调用
/// Disable 关掉结束回合按钮，关闭界面后只有 NCombatRoom.OnActiveScreenUpdated → Ui.Enable 会把它重新打开。
/// 因此一旦这里跳过 Enable，结束回合按钮会停在屏幕原位却永久不可点击，玩家只能弃跑。
/// 跳过时必须补刷结束回合按钮，且无法判断初始化进度时一律放行原方法。
/// </summary>
[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Enable))]
[LibraryPatch(Reason = "NCombatRoom 在界面上下文更新时无条件调用 NCombatUi.Enable，Activate 前字段为空必抛空引用，原版无 Hook；只在读到未初始化字段时跳过并补刷结束回合按钮，范围限于原版必然崩溃的窗口。")]
internal static class CombatUiEnableGuardPatch
{
    /// <summary>反射失败时记录一次告警的节流上限，避免日志被战斗每帧刷屏。</summary>
    private const int MaxLoggedSkips = 3;

    /// <summary>超过节流上限后的周期性提醒间隔，保证长期跳过仍然可见。</summary>
    private const int RepeatLogInterval = 60;



    private static int _loggedSkips;


    private static bool Prefix(NCombatUi __instance)
    {
        if (IsCombatUiReady(__instance, out string reason))
        {
            return true;
        }

        LogSkipped(reason);
        RefreshEndTurnButtonAfterSkip(__instance);
        return false;
    }

    /// <summary>
    /// 只有确实读到 null 才算未就绪；字段在当前游戏版本不存在时无法判断，这种情况按就绪处理并放行原方法，
    /// 否则 `field?.GetValue()` 会把“字段缺失”和“值为 null”混为一谈，导致战斗 UI 永远不会被启用。
    /// </summary>
    private static bool IsCombatUiReady(NCombatUi combatUi, out string reason)
    {
        WarnOnceIfFieldsMissing();

        if (VanillaPrivate.CombatUiState.IsAvailable && VanillaPrivate.CombatUiState.Get(combatUi) == null)
        {
            reason = "state";
            return false;
        }

        if (VanillaPrivate.CombatUiCombatPilesContainer.IsAvailable && VanillaPrivate.CombatUiCombatPilesContainer.Get(combatUi) == null)
        {
            reason = "combatPilesContainer";
            return false;
        }

        if (combatUi.Hand == null)
        {
            reason = "hand";
            return false;
        }

        if (combatUi.EndTurnButton == null)
        {
            reason = "endTurnButton";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 原版 Enable 会调用 EndTurnButton.RefreshEnabled 把按钮从覆盖界面造成的禁用状态里恢复出来。
    /// 跳过原方法时按钮已经存在就单独补刷，避免玩家看到按钮却点不动、无法结束回合。
    /// RefreshEnabled 自身只读取按钮状态与当前活动界面，不依赖 NCombatUi._state，在未就绪窗口内调用是安全的。
    /// </summary>
    private static void RefreshEndTurnButtonAfterSkip(NCombatUi combatUi)
    {
        NEndTurnButton? endTurnButton = combatUi.EndTurnButton;
        if (endTurnButton == null || combatUi.Hand == null)
        {
            return;
        }

        endTurnButton.RefreshEnabled();
    }

    private static void WarnOnceIfFieldsMissing()
    {
        if (VanillaPrivate.CombatUiState.IsAvailable && VanillaPrivate.CombatUiCombatPilesContainer.IsAvailable)
        {
            return;
        }

        LorLog.WarnOnce(
            "CombatUiEnableGuard.MissingFields",
            "[LibraryOfRuina] NCombatUi initialization fields are missing in this game build"
            + " (_state=" + (VanillaPrivate.CombatUiState.IsAvailable)
            + ", _combatPilesContainer=" + (VanillaPrivate.CombatUiCombatPilesContainer.IsAvailable)
            + "); the combat UI enable guard now defers to the vanilla method.");
    }

    private static void LogSkipped(string reason)
    {
        _loggedSkips++;
        if (_loggedSkips > MaxLoggedSkips && _loggedSkips % RepeatLogInterval != 0)
        {
            return;
        }

        Log.Warn(
            "[LibraryOfRuina] Skipped NCombatUi.Enable before combat UI activation (" + reason + ")"
            + "; refreshed the end turn button instead. count=" + _loggedSkips);
    }
}
