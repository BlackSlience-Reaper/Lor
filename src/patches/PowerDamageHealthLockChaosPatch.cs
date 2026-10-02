using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryLib.Commands;
using LibraryLib.Entities.Creatures;
using LibraryLib.Models;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.patches;

internal static class PowerDamageHealthLockChaos
{
    private const decimal ChaosDamageRatio = 0.25m;

    internal static PowerDamageHealthLockState Capture(
        ref IEnumerable<Creature>? targets)
    {
        Creature[] targetList = targets?.ToArray() ?? [];
        targets = targetList;
        return new PowerDamageHealthLockState(
            targetList
                .Select(static target => new PowerDamageHealthLockTargetState(
                    target,
                    LibraryHealthBarForecastFeature.IsHealthBarLocked(target)))
                .ToArray());
    }

    internal static async Task<IEnumerable<DamageResult>> ApplyAfterDamage(
        Task<IEnumerable<DamageResult>> damageTask,
        PowerDamageHealthLockState? state,
        PlayerChoiceContext choiceContext,
        decimal damageAmount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        IReadOnlyList<DamageResult> results = (await damageTask).ToArray();
        // state 为 null：其他模组的跳过型前缀让 Capture 前缀没有执行，没有可比较的锁血快照。
        if (state == null || !IsPowerClassDamage(props) || damageAmount <= 0m)
        {
            return results;
        }

        foreach (PowerDamageHealthLockTargetState targetState in state.Targets)
        {
            Creature target = targetState.Target;
            DamageResult? result = results.FirstOrDefault(candidate =>
                ReferenceEquals(candidate.Receiver, target));
            if (result == null || !DidTriggerHealthLock(targetState, result, damageAmount, props))
            {
                continue;
            }

            decimal damageBeforeHealthLock = props.HasFlag(ValueProp.Unblockable)
                ? damageAmount
                : Math.Max(0m, damageAmount - result.BlockedDamage);
            int chaosDamage = Math.Max(
                1,
                (int)Math.Ceiling(damageBeforeHealthLock * ChaosDamageRatio));
            await LibraryCreatureCmd.ChaoDamage(
                choiceContext,
                target,
                chaosDamage,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer,
                cardSource,
                LibraryDamageType.None);
        }

        return results;
    }

    private static bool IsPowerClassDamage(ValueProp props) =>
        props.HasFlag(ValueProp.Unpowered)
        && !props.HasFlag(ValueProp.Move);

    private static bool DidTriggerHealthLock(
        PowerDamageHealthLockTargetState targetState,
        DamageResult result,
        decimal damageAmount,
        ValueProp props)
    {
        Creature target = targetState.Target;
        if (target.IsDead || !LibraryHealthBarForecastFeature.IsHealthBarLocked(target))
        {
            return false;
        }

        decimal damageBeforeHealthLock = props.HasFlag(ValueProp.Unblockable)
            ? damageAmount
            : Math.Max(0m, damageAmount - result.BlockedDamage);
        if (damageBeforeHealthLock <= 0m)
        {
            return false;
        }

        decimal resolvedHpDamage = result.UnblockedDamage + result.OverkillDamage;
        return !targetState.WasLocked || damageBeforeHealthLock > resolvedHpDamage;
    }
}

internal sealed record PowerDamageHealthLockState(
    IReadOnlyList<PowerDamageHealthLockTargetState> Targets);

internal sealed record PowerDamageHealthLockTargetState(
    Creature Target,
    bool WasLocked);

#if STS2_0_111_0
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay))]
#else
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
#endif
internal static class VanillaPowerDamageHealthLockChaosPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        ref IEnumerable<Creature>? targets,
        out PowerDamageHealthLockState __state)
    {
        __state = PowerDamageHealthLockChaos.Capture(ref targets);
    }

    [HarmonyPostfix]
    private static void Postfix(
        PlayerChoiceContext choiceContext,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        PowerDamageHealthLockState __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        __result = PowerDamageHealthLockChaos.ApplyAfterDamage(
            __result,
            __state,
            choiceContext,
            amount,
            props,
            dealer,
            cardSource);
    }
}

[HarmonyPatch(
    typeof(LibraryCreatureCmd),
    nameof(LibraryCreatureCmd.Damage),
    typeof(PlayerChoiceContext),
    typeof(IEnumerable<Creature>),
    typeof(decimal),
    typeof(ValueProp),
    typeof(Creature),
    typeof(CardModel),
    typeof(LibraryDamageType),
    typeof(CardPlay),
    typeof(Func<Task>))]
internal static class LibraryPowerDamageHealthLockChaosPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        ref IEnumerable<Creature> targets,
        out PowerDamageHealthLockState __state)
    {
        IEnumerable<Creature>? nullableTargets = targets;
        __state = PowerDamageHealthLockChaos.Capture(ref nullableTargets);
        targets = nullableTargets!;
    }

    [HarmonyPostfix]
    private static void Postfix(
        PlayerChoiceContext choiceContext,
        decimal damageAmount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        PowerDamageHealthLockState __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        __result = PowerDamageHealthLockChaos.ApplyAfterDamage(
            __result,
            __state,
            choiceContext,
            damageAmount,
            props,
            dealer,
            cardSource);
    }
}
