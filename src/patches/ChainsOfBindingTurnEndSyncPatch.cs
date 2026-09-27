using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.patches;

[HarmonyPatch(
    typeof(CombatState),
    nameof(CombatState.IterateHookListeners))]
internal static class DetachedAfflictionHookListenerPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref IEnumerable<AbstractModel> __result)
    {
        __result = __result.Where(static model =>
            model is not AfflictionModel affliction
            || affliction.HasCard);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnEnd))]
internal static class ChainsOfBindingTurnEndSyncPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        CombatSide side,
        IEnumerable<Creature> participants,
        ref Task __result)
    {
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
