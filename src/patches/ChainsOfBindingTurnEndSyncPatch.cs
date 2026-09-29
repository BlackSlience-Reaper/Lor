using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.patches;

// Both patches back up this mod's card-swallowing encounters, where an afflicted card can leave
// combat while its affliction is still registered. Vanilla combats never reach that state, so they
// stay untouched (no filtering on the hot hook-dispatch path, no extra Chains of Binding dispatch).

[HarmonyPatch(
    typeof(CombatState),
    nameof(CombatState.IterateHookListeners))]
internal static class DetachedAfflictionHookListenerPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CombatState __instance, ref IEnumerable<AbstractModel> __result)
    {
        if (!ModOwnership.IsOwn(__instance.Encounter))
        {
            return;
        }

        __result = __result.Where(static model =>
            model is not AfflictionModel affliction
            || affliction.HasCard);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnEnd))]
[LibraryPatch(Reason = "BeforeSideTurnEnd 的监听者可能暂停后继续，原版在 WhenAll 与 DoTurnEnd 之间没有扩展点；锁链是原版 sealed 能力，施加它的本模组怪物死亡后不再是监听者。只在本模组遭遇、participant 带锁链时，于全部回合结束任务完成后补一次清理。")]
internal static class ChainsOfBindingTurnEndSyncPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        CombatStateLike combatState,
        CombatSide side,
        IEnumerable<Creature> participants,
        ref Task __result)
    {
        if (!ModOwnership.IsOwn(combatState?.Encounter))
        {
            return;
        }

        Creature[] participantSnapshot = participants.ToArray();
        __result = CompleteParticipantCleanupAsync(
            __result,
            side,
            participantSnapshot);
    }

    internal static async Task CompleteParticipantCleanupAsync(
        Task original,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        await original;

        foreach (Creature participant in participants)
        {
            ChainsOfBindingPower? chains =
                participant.GetPower<ChainsOfBindingPower>();
            if (chains == null)
            {
                continue;
            }

            await chains.BeforeSideTurnEnd(
                new ThrowingPlayerChoiceContext(),
                side,
                [participant]);
        }
    }
}
