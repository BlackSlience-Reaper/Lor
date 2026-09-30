using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

/// <summary>
/// 死尸吸收的累计回血统计：任意来源的 CreatureCmd.Heal 都计入
/// “战斗中每累计恢复10%的生命”，不仅限于遗物自身的击杀回血。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
internal static class SmilingBodiesAbsorptionHealPatch
{
    private readonly record struct HealState(int CurrentHp, bool WasDead);

    private static void Prefix(Creature? creature, out HealState __state)
    {
        __state = new HealState(
            creature?.CurrentHp ?? 0,
            creature?.IsDead ?? true);
    }

    private static void Postfix(Creature? creature, HealState __state)
    {
        if (creature == null
            || creature.Player == null
            || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        int healed = CalculateTrackableHeal(
            __state.CurrentHp,
            __state.WasDead,
            creature.CurrentHp);
        if (healed <= 0)
        {
            return;
        }

        SmilingBodiesPageRelic? relic = creature.Player.Relics
            .OfType<SmilingBodiesPageRelic>()
            .FirstOrDefault(candidate => candidate.Mode == SmilingBodiesPageMode.CorpseAbsorption);
        relic?.OnHealReceived(creature, healed);
    }

    internal static int CalculateTrackableHeal(
        int hpBefore,
        bool wasDeadBefore,
        int hpAfter)
    {
        // CreatureCmd.Heal treats healing a dead creature as a revive. Some
        // multiplayer card implementations rebuild their owner this way only
        // on the authority, so counting it would mutate this saved relic state
        // on one peer only.
        if (wasDeadBefore || hpBefore <= 0)
        {
            return 0;
        }

        return Math.Max(0, hpAfter - hpBefore);
    }
}
