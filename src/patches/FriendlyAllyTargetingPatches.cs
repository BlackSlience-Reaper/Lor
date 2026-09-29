using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.combat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.patches.QueenOfHatred;
using LibraryOfRuina.patches.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.patches;

// 玩家方用敌方范围卡给友方盟友施加能力的规则在 LorMonsterModel.ModifyPowerAmountGivenMultiplicative 覆写里。
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyPowerAmountReceived))]
[LibraryPatch(Reason = "ModifyPowerAmountReceived 是串行链，覆写不能保证最后执行，排在盟友之后的第三方接收修正可以把 0 改回；Last 后缀保证本模组友方盟友不吃玩家方减益。")]
internal static class FriendlyAllyEnemyDebuffPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? giver,
        ref decimal __result)
    {
        bool appliesDebuff = amount > 0m
            && canonicalPower.GetTypeForAmount(amount) == PowerType.Debuff;
        bool reducesBuff = amount < 0m
            && canonicalPower.Type == PowerType.Buff
            && canonicalPower.AllowNegative;
        if ((appliesDebuff || reducesBuff)
            && giver != target
            && AllyTurnRegistry.IsPlayerAlignedForTargeting(giver)
            && AllyTurnRegistry.IsFriendlyAlly(target))
        {
            // 原版与 LibraryPowerCmd 的施加、叠层都经过此入口，覆盖外部模组的敌方减益。
            __result = 0m;
        }
    }
}

/// <summary>
/// Keeps a Friendly ally's monster-backed runtime identity while giving it the
/// same enemy-target exclusion as a player creature.
/// </summary>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.HittableEnemies), MethodType.Getter)]
internal static class FriendlyAllyHittableEnemiesPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IReadOnlyList<Creature> __result)
    {
        __result = AllyTurnRegistry.FilterPlayerEnemyTargets(__result);
    }
}

[HarmonyPatch(typeof(CombatState), nameof(CombatState.GetOpponentsOf))]
internal static class FriendlyAllyOpponentsPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        CombatState __instance,
        Creature creature,
        ref IReadOnlyList<Creature> __result)
    {
        if (!AllyTurnRegistry.IsPlayerAlignedForTargeting(creature))
        {
            return;
        }

        __result = AllyTurnRegistry.FilterPlayerEnemyTargets(__instance.Enemies);
    }
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.IsValidTarget))]
internal static class FriendlyAllyPotionTargetPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        PotionModel __instance,
        Creature? target,
        ref bool __result)
    {
        if (__result
            && __instance.TargetType == TargetType.AnyEnemy
            && AllyTurnRegistry.IsFriendlyAlly(target))
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(NTargetManager), nameof(NTargetManager.AllowedToTargetNode))]
internal static class FriendlyAllyTargetManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Node node,
        TargetType ____validTargetsType,
        ref bool __result)
    {
        if (__result
            && ____validTargetsType == TargetType.AnyEnemy
            && node is NCreature creatureNode
            && AllyTurnRegistry.IsFriendlyAlly(creatureNode.Entity))
        {
            __result = false;
        }
    }
}

internal static class FriendlyAllyLibraryAttackTargetsPatch
{
    internal static void FilterLibraryAttackTargets(
        LibraryAttackCommand __instance,
        ref IReadOnlyList<Creature> __result)
    {
        if (MagicBulletShooterPageAttackPatch.IsSeventhBulletAttack(
            __instance.ModelSource as CardModel))
        {
            return;
        }

        __result = UntargetableInteractionFilter.FilterPlayerAttackTargets(
            __result,
            __instance.Attacker);
    }
}

[HarmonyPatch(
    typeof(LibraryCreatureCmd),
    nameof(LibraryCreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(LibraryDamageType), typeof(CardPlay), typeof(Func<Task>))]
internal static class FriendlyAllyLibraryDamageTargetsPatch
{
    // 只改写目标、不跳过原方法：过滤后为空时原方法自己返回空结果。若在这里跳过，
    // 同目标上其他前缀（例如齿轮教会命中上下文）不执行，它们的 Finalizer/后缀拿到空 __state 会抛空引用。
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(
        ref IEnumerable<Creature> targets,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (MagicBulletShooterPageAttackPatch.IsSeventhBulletAttack(cardSource))
        {
            return;
        }

        targets = UntargetableInteractionFilter.FilterPlayerAttackTargets(
            targets,
            dealer);
    }
}

[HarmonyPatch(
    typeof(LibraryCreatureCmd),
    nameof(LibraryCreatureCmd.ChaoDamage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay), typeof(LibraryDamageType), typeof(IEnumerable<DamageResult>))]
internal static class FriendlyAllyLibraryChaoTargetsPatch
{
    // 同上：只改写目标，过滤后为空时由原方法返回空结果，不跳过原方法。
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(
        ref IEnumerable<Creature> targets,
        Creature? dealer)
    {
        if (!AllyTurnRegistry.IsPlayerAlignedForTargeting(dealer))
        {
            return;
        }

        targets = AllyTurnRegistry.FilterPlayerEnemyTargets(targets);
    }
}
