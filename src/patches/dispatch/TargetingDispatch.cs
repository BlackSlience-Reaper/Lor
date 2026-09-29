using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.BigBird;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.patches.ArtFloorLiberation;
using LibraryOfRuina.patches.TechnologyFloorLiberation;
using LibraryOfRuina.patches.WedgeOffice;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// 选目标相关的补丁。每个嵌套类对应原来同一目标、同一优先级的一段补丁，按原执行顺序调用各功能的处理函数；
/// 优先级与原来相同（默认或 <c>Priority.Last</c>），所以与 RitsuLib 等其他模组补丁的相对顺序不变。
/// 新增选目标规则时在对应的段里按需要的位置加一行，不要再单独挂补丁。
/// </summary>
internal static class TargetingDispatch
{
    [HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
    private static class AttackTargets
    {
        [HarmonyPostfix]
        private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
        {
            TargetedMonsterAttackTargetPatch.FilterAttackTargets(__instance, ref __result);
            WrathServantPageTargetPatch.FilterAttackTargets(__instance, ref __result);
            FanaticWorshipTargetPatch.FilterAttackTargets(__instance, ref __result);
            BigBirdTargetingPatch.FilterAttackTargets(__instance, ref __result);
            ArtFloorAtonementTargetPatch.FilterAttackTargets(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
    private static class AttackTargetsLast
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
        {
            MagicBulletShooterAttackTargetsPatch.FilterAttackTargets(__instance, ref __result);
            UntargetableAttackTargetsPatch.FilterAttackTargets(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    private static class IsValidTarget
    {
        [HarmonyPostfix]
        private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
        {
            SocialFloorMagicalPowderTargetPatch.FilterIsValidTarget(__instance, target, ref __result);
            WrathServantPageTargetPatch.CardModelIsValidTargetPatch.FilterIsValidTarget(__instance, target, ref __result);
            FanaticWorshipTargetPatch.CardModelIsValidTargetPatch.FilterIsValidTarget(__instance, target, ref __result);
            BigBirdTargetingPatch.CardModelIsValidTargetPatch.FilterIsValidTarget(__instance, target, ref __result);
            ArtFloorAtonementTargetPatch.CardModelIsValidTargetPatch.FilterIsValidTarget(__instance, target, ref __result);
        }
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    private static class IsValidTargetLast
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
        {
            UntargetableCardTargetPatch.FilterIsValidTarget(__instance, target, ref __result);
            ForestKeeperLockTargetPatch.FilterIsValidTarget(__instance, target, ref __result);
        }
    }

    [HarmonyPatch(typeof(NTargetManager), "AllowedToTargetCreature")]
    private static class AllowedToTargetCreature
    {
        [HarmonyPostfix]
        private static void Postfix(Creature creature, TargetType ____validTargetsType, ref bool __result)
        {
            WrathServantPageTargetPatch.TargetManagerAllowedToTargetCreaturePatch.FilterAllowedToTargetCreature(creature, ____validTargetsType, ref __result);
            FanaticWorshipTargetPatch.TargetManagerAllowedToTargetCreaturePatch.FilterAllowedToTargetCreature(creature, ____validTargetsType, ref __result);
            BigBirdTargetingPatch.TargetManagerAllowedToTargetCreaturePatch.FilterAllowedToTargetCreature(creature, ____validTargetsType, ref __result);
            ArtFloorAtonementTargetPatch.TargetManagerAllowedToTargetCreaturePatch.FilterAllowedToTargetCreature(creature, ref __result);
        }
    }

    [HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
    private static class FinishTargeting
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            WrathServantPageTargetPatch.TargetManagerFinishTargetingPatch.OnFinishTargeting();
            FanaticWorshipTargetPatch.TargetManagerFinishTargetingPatch.OnFinishTargeting();
            BigBirdTargetingPatch.TargetManagerFinishTargetingPatch.OnFinishTargeting();
            ArtFloorAtonementTargetPatch.TargetManagerFinishTargetingPatch.OnFinishTargeting();
        }
    }

    [HarmonyPatch(
        typeof(NTargetManager),
        nameof(NTargetManager.StartTargeting), typeof(TargetType), typeof(Control), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>))]
    private static class StartTargetingFromCard
    {
        [HarmonyPrefix]
        private static void Prefix(TargetType validTargetsType, Control control, ref Func<Node, bool>? nodeFilter)
        {
            SocialFloorMagicalPowderTargetSelectionPatch.OnStartTargetingFromCard(control, ref nodeFilter);
            ForestKeeperLockTargetSelectionPatch.OnStartTargetingFromCard(control, ref nodeFilter);
            WrathServantPageTargetPatch.TargetManagerStartTargetingFromCardPatch.OnStartTargetingFromCard(validTargetsType, control);
            FanaticWorshipTargetPatch.TargetManagerStartTargetingFromCardPatch.OnStartTargetingFromCard(validTargetsType, control);
            BigBirdTargetingPatch.TargetManagerStartTargetingFromCardPatch.OnStartTargetingFromCard(validTargetsType, control);
            ArtFloorAtonementTargetPatch.TargetManagerStartTargetingFromCardPatch.OnStartTargetingFromCard(validTargetsType, control);
        }
    }

    [HarmonyPatch(
        typeof(NTargetManager),
        nameof(NTargetManager.StartTargeting), typeof(TargetType), typeof(Vector2), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>))]
    private static class StartTargetingFromPosition
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            WrathServantPageTargetPatch.TargetManagerStartTargetingFromPositionPatch.OnStartTargetingFromPosition();
            FanaticWorshipTargetPatch.TargetManagerStartTargetingFromPositionPatch.OnStartTargetingFromPosition();
            BigBirdTargetingPatch.TargetManagerStartTargetingFromPositionPatch.OnStartTargetingFromPosition();
            ArtFloorAtonementTargetPatch.TargetManagerStartTargetingFromPositionPatch.OnStartTargetingFromPosition();
        }
    }
}
