using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NRelicInventory), nameof(NRelicInventory.AnimateRelic))]
[LibraryPatch(Reason = "奖励、商店、宝箱在 await RelicCmd.Obtain 之后才调用 AnimateRelic，遗物若在获得后被移除，原版 _relicNodes.First 会抛异常；方法非虚无 Hook。只在遗物不可变、无主或已不在主人遗物列表时跳过，这些情况原版要么空操作要么崩溃。")]
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
