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
        // 规范（不可变）遗物上读 Owner 会先断言可变并抛异常；原版对它们本来就是空操作。
        if (relic is not { IsMutable: true } || relic.Owner == null || !relic.Owner.Relics.Contains(relic))
        {
            return false;
        }

        return true;
    }
}
