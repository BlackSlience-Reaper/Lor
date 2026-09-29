using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart))]
[UsedImplicitly]
[LibraryPatch(Reason = "盟友在敌方一侧，原版只在本方回合开始时清格挡；需要在全部玩家侧开场监听者之后清除。不能改为怪物覆写：监听者列表在迭代开始时取好，樵夫在同一轮里重生的树不在列表中，覆写清不到它；排在盟友之后的监听者给的格挡也会留下。只作用于已激活 provider 的本模组盟友。")]
internal static class AllyClearBlockAtPlayerTurnStartPatch
{
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void Postfix(CombatStateLike combatState, CombatSide side, ref Task __result)
    {
        __result = Wrap(combatState, side, __result);
    }

    private static async Task Wrap(CombatStateLike combatState, CombatSide side, Task original)
    {
        await original;

        if (side != CombatSide.Player)
        {
            return;
        }
        AllyTurnRegistry.ForgetCombatEndedByAllyTurn();
        AllyTurnRegistry.ClearBlockBeforePlayerTurnStart(combatState);

    }
}

[HarmonyPatch]
[UsedImplicitly]
internal static class AllyPreEnemyTurnPatch
{
    private static MethodBase TargetMethod() =>
        CombatManagerTurnMethodCompat.Resolve("EndPlayerTurnPhaseTwoInternal");

    [HarmonyPostfix]
    [UsedImplicitly]
    private static void Postfix(CombatManager __instance, ref Task __result)
    {
        __result = Wrap(__instance, __result);
    }

    private static async Task Wrap(CombatManager combatManager, Task original)
    {
        await original;
        await AllyTurnRegistry.ExecuteAllyTurn(combatManager);
    }
}

[HarmonyPatch]
[UsedImplicitly]
[LibraryPatch(Reason = "原版切边（私有方法，无 Hook）开头只检查回合取消令牌，战斗正常结束不会取消它；调用方在第二阶段之前查过 IsInProgress，之后不再检查，所以盟友回合在第二阶段后打完最后一个敌人时仍会切边并询问额外回合。只在战斗刚被本模组盟友回合结束时跳过这一次，其他方式结束的战斗保持原版。")]
internal static class AllySkipEnemySideSwitchWhenCombatEndsPatch
{
    private static MethodBase TargetMethod() =>
        CombatManagerTurnMethodCompat.Resolve("SwitchFromPlayerToEnemySide");

    [HarmonyPrefix]
    private static bool Prefix(CombatManager __instance, ref Task __result)
    {
        if (__instance.IsInProgress || !AllyTurnRegistry.ConsumeCombatEndedByAllyTurn())
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.PerformIntent))]
[UsedImplicitly]
[LibraryPatch(Reason = "原版敌方回合对每个敌方生物无条件播放意图动画，没有 Hook；只对本回合已由本模组盟友回合行动过的单位跳过。")]
internal static class AllySkipOfficialEnemyIntentPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NCreature __instance, ref Task __result)
    {
        if (!AllyTurnRegistry.HasActedThisRound(__instance.Entity))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.TakeTurn))]
[UsedImplicitly]
[LibraryPatch(Reason = "原版敌方回合无法按单位跳过招式（TakeTurn/PerformMove 非虚、无 Hook）；只对本回合已在盟友回合行动过的本模组盟友跳过，避免行动两次。")]
internal static class AllySkipOfficialEnemyTurnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __instance, ref Task __result)
    {
        if (!AllyTurnRegistry.HasActedThisRound(__instance))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
[UsedImplicitly]
internal static class AllyTurnStateResetPatch
{
    [HarmonyPrefix]
    private static void Prefix(CombatManager __instance)
    {
        CombatState? combatState = __instance.DebugOnlyGetState();
        if (combatState == null)
        {
            return;
        }

        AllyTurnRegistry.ReSetAll(combatState);
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.ScaleMonsterHpForMultiplayer))]
[UsedImplicitly]
[LibraryPatch(Reason = "原版多人生命缩放没有 Hook 或虚方法；只对本模组盟友 provider 登记的友方/中立单位跳过，敌对形态仍按原版缩放。")]
internal static class AllyMultiplayerHpScalingPatch
{
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool Prefix(Creature __instance) =>
        !AllyTurnRegistry.ShouldSkipMultiplayerScaling(__instance);
}

[HarmonyPatch(typeof(LibraryCreature), nameof(LibraryCreature.ScaleMonsterChaoValueForMultiplayer))]
[UsedImplicitly]
[LibraryPatch(Reason = "基础库在多人生命缩放后缀里无条件缩放混乱值，没有扩展点；只对本模组盟友 provider 登记的友方/中立单位跳过多人混乱值缩放。")]
internal static class AllyMultiplayerChaoScalingPatch
{
    // 基础库在血量缩放的后置补丁中独立缩放混乱值，因此共用血量缩放的豁免判断。
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool Prefix(LibraryCreature __instance) =>
        !AllyTurnRegistry.ShouldSkipMultiplayerScaling(__instance);
}

[HarmonyPatch(
    typeof(MultiplayerScalingModel),
    nameof(MultiplayerScalingModel.ModifyBlockMultiplicative))]
[UsedImplicitly]
internal static class AllyMultiplayerBlockScalingPatch
{
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void Postfix(Creature target, ref decimal __result)
    {
        if (AllyTurnRegistry.ShouldSkipMultiplayerScaling(target))
        {
            __result = 1m;
        }
    }
}
