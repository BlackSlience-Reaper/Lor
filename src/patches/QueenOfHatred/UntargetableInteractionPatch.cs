using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.combat;
using LibraryOfRuina.powers.BigBadWolf;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.patches.QueenOfHatred;

internal static class UntargetableInteractionFilter
{
    internal static bool CanBeHit(
        Creature? creature,
        Creature? source = null)
    {
        if (creature == null || !creature.IsAlive)
        {
            return false;
        }

        var combatState = creature.CombatState;
        if (combatState == null)
        {
            return true;
        }

        BigBadWolfUntargetablePower? protection =
            creature.GetPower<BigBadWolfUntargetablePower>();
        if (protection == null || source == null)
        {
            return creature.IsHittable;
        }

        if (!IsFriendlySourceFor(source, creature))
        {
            return false;
        }

        return combatState.IterateHookListeners().All(listener =>
            ReferenceEquals(listener, protection)
            || listener.ShouldAllowHitting(creature));
    }

    internal static bool CanBeSelected(
        Creature? creature,
        Creature? source = null)
    {
        if (creature == null || !CanBeHit(creature, source))
        {
            return false;
        }

        var combatState = creature.CombatState;
        if (combatState == null)
        {
            return true;
        }

        BigBadWolfUntargetablePower? protection =
            creature.GetPower<BigBadWolfUntargetablePower>();
        if (protection != null
            && source != null
            && IsFriendlySourceFor(source, creature))
        {
            return combatState.IterateHookListeners().All(listener =>
                ReferenceEquals(listener, protection)
                || listener.ShouldAllowTargeting(creature));
        }

        return Hook.ShouldAllowTargeting(combatState, creature, out _);
    }

    internal static IReadOnlyList<Creature> FilterHittable(
        IEnumerable<Creature> creatures,
        Creature? source = null)
    {
        Creature[] targets = creatures.ToArray();
        return targets.All(target => CanBeHit(target, source))
            ? targets
            : targets.Where(target => CanBeHit(target, source)).ToArray();
    }

    private static bool IsFriendlySourceFor(
        Creature source,
        Creature target)
    {
        return source.Side == target.Side
            || (target.Side == CombatSide.Player
                && AllyTurnRegistry.IsFriendlyAlly(source));
    }

    internal static IReadOnlyList<Creature> FilterPlayerAttackTargets(
        IEnumerable<Creature> creatures,
        Creature? attacker)
    {
        IReadOnlyList<Creature> hittableTargets = FilterHittable(
            creatures,
            attacker);
        if (!AllyTurnRegistry.IsPlayerAlignedForTargeting(attacker))
        {
            return hittableTargets;
        }

        return AllyTurnRegistry.FilterPlayerEnemyTargets(hittableTargets);
    }
}

internal static class UntargetableAttackTargetsPatch
{
    internal static void FilterAttackTargets(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        __result = UntargetableInteractionFilter.FilterPlayerAttackTargets(
            __result,
            __instance.Attacker);
    }
}

[HarmonyPatch(
    typeof(CreatureCmd),
    nameof(CreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay))]
internal static class UntargetableDamageTargetsPatch
{
    // 其他模组会在同一重载的普通优先级 Prefix 中改写 targets，因此最终友方过滤
    // 必须在这些改写之后执行。LibraryOfRuinaLib 的 Last Prefix 可能短路原方法，
    // 显式 Before 关系保证它接收到已经过滤的稳定目标集合。
    // 只改写目标，过滤后为空时原方法自己返回空结果，不跳过原方法。
    [HarmonyPriority(Priority.Last)]
    [HarmonyBefore("LibraryOfRuinaLib")]
    private static void Prefix(
        ref IEnumerable<Creature> targets,
        Creature? dealer)
    {
        targets = UntargetableInteractionFilter.FilterPlayerAttackTargets(
            targets,
            dealer);
    }
}

internal static class UntargetableCardTargetPatch
{
    internal static void FilterIsValidTarget(CardModel __instance, Creature? target, ref bool __result)
    {
        if (!__result || target == null)
        {
            return;
        }

        if (!UntargetableInteractionFilter.CanBeSelected(
                target,
                __instance.Owner?.Creature)
            || (__instance.TargetType == TargetType.AnyEnemy
                && AllyTurnRegistry.IsFriendlyAlly(target)))
        {
            __result = false;
        }
    }
}

[HarmonyPatch(
    typeof(CreatureCmd),
    nameof(CreatureCmd.Stun), typeof(Creature), typeof(Func<IReadOnlyList<Creature>, Task>), typeof(string))]
internal static class UntargetableCreatureStunPatch
{
    private static bool Prefix(Creature creature, ref Task __result)
    {
        if (UntargetableInteractionFilter.CanBeHit(creature))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(
    typeof(LibraryCreatureCmd),
    "Stun", typeof(LibraryCreature), typeof(Func<IReadOnlyList<Creature>, Task>), typeof(string))]
internal static class UntargetableLibraryCreatureStunPatch
{
    private static bool Prefix(LibraryCreature creature, ref Task __result)
    {
        if (UntargetableInteractionFilter.CanBeHit(creature))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
