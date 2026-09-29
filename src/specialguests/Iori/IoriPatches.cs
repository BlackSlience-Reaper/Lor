using HarmonyLib;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Iori;

[HarmonyPatch(
    typeof(Creature),
    nameof(Creature.LoseHpInternal), typeof(decimal), typeof(ValueProp))]
[HarmonyPriority(Priority.Last)]
internal static class IoriStageOneFinalHpFloorPatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature __instance, ref decimal amount)
    {
        if (amount > 0m
            && __instance.Monster is IoriStageOne iori
            && !iori.EscapeCompleted)
        {
            amount = iori.ClampFinalStageOneHpLoss(amount);
        }
    }
}
