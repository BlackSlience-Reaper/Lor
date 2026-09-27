using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using LibraryOfRuina.powers.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.ArtFloorLiberation;

[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class ArtFloorAtonementTargetPatch
{
    private static CardModel? ActiveTargetingCard { get; set; }

    private static bool IsAtonementProtectedTarget(Creature? target)
    {
        return target?.Monster is ArtFloorNostalgicScentBoss
            && target.HasPower<ArtFloorSuffocatingAtonementPower>();
    }

    private static bool IsNonCrownPlayerCard(CardModel? card)
    {
        if (card?.IsMutable != true)
        {
            return false;
        }

        Player? owner = card.Owner;
        Creature? creature = owner?.Creature;
        return creature is { IsPlayer: true }
            && !creature.HasPower<ArtFloorAtonementCrownPower>();
    }

    private static bool ShouldBlockCardTarget(CardModel? card, Creature? target)
    {
        return IsAtonementProtectedTarget(target)
            && IsNonCrownPlayerCard(card);
    }

    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (__instance.ModelSource is not CardModel card || !IsNonCrownPlayerCard(card))
        {
            return;
        }

        __result = __result
            .Where(target => !IsAtonementProtectedTarget(target))
            .ToArray();
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    private static class CardModelIsValidTargetPatch
    {
        private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
        {
            if (__result && ShouldBlockCardTarget(__instance, target))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(
        typeof(NTargetManager),
        nameof(NTargetManager.StartTargeting), typeof(TargetType), typeof(Control), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>))]
    private static class TargetManagerStartTargetingFromCardPatch
    {
        private static void Prefix(TargetType validTargetsType, Control control)
        {
            ActiveTargetingCard = validTargetsType == TargetType.AnyEnemy && control is NCard cardNode
                ? cardNode.Model
                : null;
        }
    }

    [HarmonyPatch(
        typeof(NTargetManager),
        nameof(NTargetManager.StartTargeting), typeof(TargetType), typeof(Vector2), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>))]
    private static class TargetManagerStartTargetingFromPositionPatch
    {
        private static void Prefix()
        {
            ActiveTargetingCard = null;
        }
    }

    [HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
    private static class TargetManagerFinishTargetingPatch
    {
        private static void Postfix()
        {
            ActiveTargetingCard = null;
        }
    }

    [HarmonyPatch(typeof(NTargetManager), "AllowedToTargetCreature")]
    private static class TargetManagerAllowedToTargetCreaturePatch
    {
        private static void Postfix(Creature creature, ref bool __result)
        {
            if (__result && ShouldBlockCardTarget(ActiveTargetingCard, creature))
            {
                __result = false;
            }
        }
    }
}
