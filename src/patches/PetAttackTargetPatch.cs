using HarmonyLib;
using LibraryOfRuina.intents;
using MegaCrit.Sts2.Core.Commands.Builders;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
public static class TargetedMonsterAttackTargetPatch
{
    [HarmonyPostfix]
    public static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (__instance.Attacker == null
            || (!TargetedMonsterAttackHelper.HasForcedTargets(__instance.Attacker)
                && !TargetedMonsterAttackHelper.TryGetProvider(__instance.Attacker, out _)))
        {
            return;
        }

        __result = TargetedMonsterAttackHelper.GetTargetList(__instance.Attacker, __result);
    }
}
