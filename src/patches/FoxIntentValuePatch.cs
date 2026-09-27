using HarmonyLib;
using LibraryOfRuina.intents;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
public static class FoxIntentValuePatch
{
    [HarmonyPostfix]
    public static void Postfix(NIntent __instance, AbstractIntent ____intent)
    {
        string? text = null;

        if (____intent is FoxDefendIntent foxDefend)
            text = foxDefend.BlockAmount.ToString();
        else if (____intent is FoxBuffIntent foxBuff)
            text = foxBuff.Amount.ToString();
        else if (____intent is FoxEnergyIntent foxEnergy)
            text = foxEnergy.Amount.ToString();
        else if (____intent is FoxWeakIntent foxWeak)
            text = foxWeak.Amount.ToString();

        if (text != null && __instance.HasNode("%Value"))
        {
            __instance.GetNode("%Value").Set("text", text);
        }
    }
}
