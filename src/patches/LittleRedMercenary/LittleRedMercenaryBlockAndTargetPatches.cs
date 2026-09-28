using HarmonyLib;
using LibraryOfRuina.combat;
using LibraryOfRuina.encounters;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches.LittleRedMercenary;

// 后缀（本回合在盟友回合行动过、或是格挡转移搭档时保留格挡）可以改为 9 个盟友怪物各自覆写 ShouldClearBlock，
// preventer 仍是怪物模型，放到阶段 4 与盟友基类一起做。
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

    [HarmonyPostfix]
    private static void Postfix(CombatStateLike combatState, Creature creature, ref bool __result, ref AbstractModel? preventer)
    {
        if (!__result || !AllyTurnRegistry.ShouldPreventVanillaBlockClearing(creature))
        {
            if (__result && BlockTransferEncounterTargetHelper.IsBlockTransferPartner(combatState, creature))
            {
                __result = false;
                preventer = creature.Monster;
            }

            return;
        }

        __result = false;
        preventer = creature.Monster;
    }
}
