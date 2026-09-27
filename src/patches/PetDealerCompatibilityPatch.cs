using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.patches;






[HarmonyPatch(typeof(PersonalHivePower), nameof(PersonalHivePower.AfterDamageReceived))]
public static class PetDealerCompatibilityPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        ref Creature? dealer,
        ref Task __result)
    {
        if (dealer?.IsPet == true)
        {
            __result = Task.CompletedTask;
            return false;
        }

        return true;
    }
}
