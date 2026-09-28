using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

// internal static class LittleRedMercenaryTurnRuntime
// {
//     private sealed class State
//     {
//         public int ActedRound;
//     }
//
//     private static readonly Dictionary<Creature, State> States = [];
//
//     public static bool IsLittleRedSpecialTurnCreature(Creature? creature)
//     {
//         return creature?.Monster is LittleRedRidingHoodedMercenary
//             && LittleRedMercenaryEncounterHelper.IsLittleRedEncounter(creature.CombatState);
//     }
//
//     public static bool HasActedThisRound(Creature? creature)
//     {
//         if (!IsLittleRedSpecialTurnCreature(creature) || creature?.CombatState == null)
//         {
//             return false;
//         }
//
//         return States.TryGetValue(creature, out State? state)
//             && state.ActedRound == creature.CombatState.RoundNumber;
//     }
//
//     public static bool ShouldPreventStandardEnemyBlockClear(Creature? creature)
//     {
//         return HasActedThisRound(creature);
//     }
//
//     public static void Clear(Creature? creature)
//     {
//         if (creature == null)
//         {
//             return;
//         }
//
//         States.Remove(creature);
//         TransferredBlockPower.Clear(creature);
//     }
//
//     public static async Task TryExecutePreEnemyTurn(CombatManager combatManager)
//     {
//         CombatState? combatState = combatManager.DebugOnlyGetState();
//         if (combatState == null
//             || !combatManager.IsInProgress
//             || combatState.CurrentSide != CombatSide.Player
//             || WouldAnyPlayerTakeExtraTurn(combatState)
//             || !LittleRedMercenaryEncounterHelper.IsLittleRedEncounter(combatState))
//         {
//             return;
//         }
//
//         Creature? littleRed = LittleRedMercenaryEncounterHelper.FindLittleRed(combatState);
//         if (littleRed == null || littleRed.IsDead || HasActedThisRound(littleRed))
//         {
//             return;
//         }
//
//         NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(littleRed);
//         if (creatureNode != null)
//         {
//             await creatureNode.PerformIntent();
//         }
//
//         await littleRed.Monster!.PerformMove();
//         MarkActed(littleRed);
//         await combatManager.WaitForUnpause();
//         await combatManager.CheckWinCondition();
//     }
//
//     private static bool WouldAnyPlayerTakeExtraTurn(CombatState combatState)
//     {
//         foreach (Player player in combatState.Players)
//         {
//             if (Hook.ShouldTakeExtraTurn(combatState, player))
//             {
//                 return true;
//             }
//         }
//
//         return false;
//     }
//
//     private static void MarkActed(Creature littleRed)
//     {
//         if (littleRed.CombatState == null)
//         {
//             return;
//         }
//
//         GetOrCreateState(littleRed).ActedRound = littleRed.CombatState.RoundNumber;
//     }
//
//     private static State GetOrCreateState(Creature littleRed)
//     {
//         if (!States.TryGetValue(littleRed, out State? state))
//         {
//             state = new State();
//             States[littleRed] = state;
//         }
//
//         return state;
//     }
// }
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart))]
[UsedImplicitly]
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
internal static class AllyMultiplayerHpScalingPatch
{
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool Prefix(Creature __instance) =>
        !AllyTurnRegistry.ShouldSkipMultiplayerScaling(__instance);
}

[HarmonyPatch(typeof(LibraryCreature), nameof(LibraryCreature.ScaleMonsterChaoValueForMultiplayer))]
[UsedImplicitly]
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
