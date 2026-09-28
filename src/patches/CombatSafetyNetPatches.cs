using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.combat;
using LibraryOfRuina.encounters.GalaxyChild;
using LibraryOfRuina.helpers;
using LibraryOfRuina.monsters.GalaxyChild;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

// The safety net only covers this mod's own monsters. It used to wrap every Hook task,
// TaskHelper.RunSafely, GameAction/ActionExecutor and the turn pipeline, swallowing any mod's
// exception in combat and "recovering" state on whichever client threw — which turned one-sided
// failures into permanent multiplayer desyncs (design philosophy §2, §4).

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom))]
internal static class CombatSafetyNestedCombatReplayPatch
{
    private static void Prefix(AbstractRoom room)
    {
        CombatSafetyNet.EnsureReplayInitializedBeforeNestedCombat(room);
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class CombatSafetyMonsterPerformMovePatch
{
    private static void Postfix(MonsterModel __instance, ref Task __result)
    {
        if (!ModOwnership.IsOwn(__instance))
        {
            return;
        }

        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext(
                "MonsterModel.PerformMove",
                __instance.Creature.CombatState,
                __instance.Creature,
                __instance));
    }
}

/// <summary>
/// Vanilla AttackCommand stops hitting once its attacker dies, but the rest of the monster's move
/// keeps running on a dead creature. For this mod's monsters (which can die to their own counters
/// mid-move) the remainder of the move is aborted and finalized by the PerformMove wrapper above.
/// </summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class CombatSafetyAttackCommandExecutePatch
{
    private static void Postfix(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        if (!ModOwnership.IsOwnMonster(__instance.Attacker))
        {
            return;
        }

        __result = CombatSafetyNet.WrapMonsterAttackTask(__result, __instance);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class CombatSafetyAfterDamageGivenPatch
{
    [HarmonyPriority(Priority.Low)]
    private static bool Prefix(Creature? dealer, ref Task __result)
    {
        if (!ModOwnership.IsOwnMonster(dealer) || !CombatSafetyNet.ShouldSkipDeadMonsterAttackFollowup(dealer))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterAttack))]
internal static class CombatSafetyAfterAttackPatch
{
    [HarmonyPriority(Priority.Low)]
    private static bool Prefix(AttackCommand command, ref Task __result)
    {
        if (!ModOwnership.IsOwnMonster(command.Attacker)
            || !CombatSafetyNet.ShouldSkipDeadMonsterAttackFollowup(command.Attacker))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>
/// Parting Tears victory must happen after the second Galaxy Friend's death dispatch has finished
/// (killing inside AfterDeath re-enters the death pipeline). Continuing the awaited AfterDeath task
/// keeps it inside the synchronized action on every client, unlike a deferred next-frame call.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
internal static class GalaxyChildPartingTearsAfterDeathPatch
{
    private static void Postfix(CombatStateLike combatState, ref Task __result)
    {
        if (combatState?.Encounter is not GalaxyChildWeak)
        {
            return;
        }

        __result = TriggerAfter(__result, combatState);
    }

    private static async Task TriggerAfter(Task afterDeath, CombatStateLike combatState)
    {
        await afterDeath;
        if (GalaxyFriend.ShouldTriggerPartingTears(combatState))
        {
            await GalaxyFriend.TriggerPartingTearsVictory(combatState);
        }
    }
}
