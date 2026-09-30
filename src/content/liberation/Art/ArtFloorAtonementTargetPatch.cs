using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Art;

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

    internal static void FilterAttackTargets(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (__instance.ModelSource is not CardModel card || !IsNonCrownPlayerCard(card))
        {
            return;
        }

        __result = __result
            .Where(target => !IsAtonementProtectedTarget(target))
            .ToArray();
    }

    internal static class CardModelIsValidTargetPatch
    {
        internal static void FilterIsValidTarget(CardModel __instance, Creature? target, ref bool __result)
        {
            if (__result && ShouldBlockCardTarget(__instance, target))
            {
                __result = false;
            }
        }
    }

    internal static class TargetManagerStartTargetingFromCardPatch
    {
        internal static void OnStartTargetingFromCard(TargetType validTargetsType, Control control)
        {
            ActiveTargetingCard = validTargetsType == TargetType.AnyEnemy && control is NCard cardNode
                ? cardNode.Model
                : null;
        }
    }

    internal static class TargetManagerStartTargetingFromPositionPatch
    {
        internal static void OnStartTargetingFromPosition()
        {
            ActiveTargetingCard = null;
        }
    }

    internal static class TargetManagerFinishTargetingPatch
    {
        internal static void OnFinishTargeting()
        {
            ActiveTargetingCard = null;
        }
    }

    internal static class TargetManagerAllowedToTargetCreaturePatch
    {
        internal static void FilterAllowedToTargetCreature(Creature creature, ref bool __result)
        {
            if (__result && ShouldBlockCardTarget(ActiveTargetingCard, creature))
            {
                __result = false;
            }
        }
    }
}
