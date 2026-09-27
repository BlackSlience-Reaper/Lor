using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NRelicInventory), nameof(NRelicInventory.AnimateRelic))]
public static class AnimateRelicSafetyPatch
{
    [HarmonyPrefix]
    public static bool Prefix(NRelicInventory __instance, RelicModel relic)
    {
        if (relic?.Owner == null || !relic.Owner.Relics.Contains(relic))
        {
            return false;
        }

        return true;
    }
}
