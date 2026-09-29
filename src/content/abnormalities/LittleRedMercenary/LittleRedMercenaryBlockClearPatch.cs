using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

// 盟友保留格挡的规则在 LorMonsterModel.ShouldClearBlock 覆写里。
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldClearBlock))]
[LibraryPatch(Reason = "生物已被移出战斗时原版 Hook.ShouldClearBlock 会因 CombatState 为空抛异常，没有监听者可用；只把该异常路径改成与原版结束战斗分支相同的清格挡结果。")]
internal static class LittleRedMercenaryBlockClearPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CombatStateLike combatState, ref bool __result, ref AbstractModel? preventer)
    {
        if (combatState == null)
        {
            __result = true;
            preventer = null;
            return false;
        }

        return true;
    }
}
