using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.liberation.Art;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.patches.WedgeOffice;

internal static class FanaticWorshipTargetPatch
{
    private static CardModel? ActiveTargetingCard { get; set; }

    private static bool IsFanaticTargetingActive(Player? owner)
    {
        return owner?.Creature is { IsAlive: true } creature
            && creature.HasPower<FanaticWorshipPower>();
    }

    private static IReadOnlyList<Creature> GetFanaticTargets(Player? owner)
    {
        if (!IsFanaticTargetingActive(owner) || owner?.Creature.CombatState == null)
        {
            return [];
        }

        return owner.Creature.CombatState.Creatures
            .Where(static creature => creature.IsAlive && creature.IsHittable)
            .OrderBy(static creature => creature.CombatId)
            .ToArray();
    }

    internal static bool CanManuallyTargetFanatic(CardModel card, Creature? target)
    {
        return target != null
            && card.TargetType == TargetType.AnyEnemy
            && card.Type == CardType.Attack
            && GetFanaticTargets(card.Owner).Contains(target);
    }

    internal static void FilterAttackTargets(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (__instance.ModelSource is not CardModel card
            || !IsFanaticTargetingActive(card.Owner)
            || card.Type != CardType.Attack
            || card.TargetType is not (TargetType.AllEnemies or TargetType.RandomEnemy))
        {
            return;
        }

        __result = GetFanaticTargets(card.Owner);
    }

    internal static class CardModelIsValidTargetPatch
    {
        internal static void FilterIsValidTarget(CardModel __instance, Creature? target, ref bool __result)
        {
            if (!__result && CanManuallyTargetFanatic(__instance, target))
            {
                __result = true;
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
        internal static void FilterAllowedToTargetCreature(
            Creature creature,
            TargetType ____validTargetsType,
            ref bool __result)
        {
            if (!__result
                && ____validTargetsType == TargetType.AnyEnemy
                && ActiveTargetingCard is { } card
                && CanManuallyTargetFanatic(card, creature))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(
        typeof(NCombatRoom),
        nameof(NCombatRoom.RestrictControllerNavigation), typeof(IEnumerable<Control>))]
    private static class CombatRoomRestrictControllerNavigationPatch
    {
        private static void Prefix(NCombatRoom __instance, ref IEnumerable<Control> whitelist)
        {
            if (ActiveTargetingCard is not { } card
                || !IsFanaticTargetingActive(card.Owner)
                || card.TargetType != TargetType.AnyEnemy
                || card.Type != CardType.Attack)
            {
                return;
            }

            Control[] extraHitboxes = GetFanaticTargets(card.Owner)
                .Select(creature => __instance.GetCreatureNode(creature)?.Hitbox)
                .OfType<Control>()
                .ToArray();
            if (extraHitboxes.Length > 0)
            {
                whitelist = whitelist.Concat(extraHitboxes).Distinct().ToArray();
            }
        }
    }

    [HarmonyPatch(typeof(NCardPlay), "ShowMultiCreatureTargetingVisuals")]
    private static class CardPlayShowMultiCreatureTargetingVisualsPatch
    {
        private static void Postfix(NCardPlay __instance)
        {
            CardModel? card = __instance.Holder?.CardModel;
            if (card?.TargetType is not (TargetType.AllEnemies or TargetType.RandomEnemy)
                || !IsFanaticTargetingActive(card.Owner)
                || card.Type != CardType.Attack)
            {
                return;
            }

            foreach (Creature creature in GetFanaticTargets(card.Owner))
            {
                NCombatRoom.Instance?.GetCreatureNode(creature)?.ShowMultiselectReticle();
            }
        }
    }
}
