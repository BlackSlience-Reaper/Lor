using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.relics.WrathServant;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.patches.WrathServant;

[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class WrathServantPageTargetPatch
{
    private static CardModel? ActiveTargetingCard { get; set; }

    private static bool HasWrathServantWrathMode(Player? owner)
    {
        return owner?.Relics.OfType<WrathServantPageRelic>().Any(static relic =>
            relic.Mode == WrathServantPageMode.Wrath) == true;
    }

    internal static bool CanTargetOwnerSelf(CardModel? card)
    {
        return card?.Type == CardType.Attack
            && HasWrathServantWrathMode(card.Owner)
            && IsWrathFriendlyFireActive(card.Owner);
    }

    private static bool IsWrathFriendlyFireActive(Player? owner)
    {
        bool originalActive = HasWrathServantWrathMode(owner)
            && owner?.Creature.CombatState?.RoundNumber <= WrathServantPageRelic.WrathSelfTargetTurns;
        bool enhancedActive = owner?.Relics.OfType<WrathServantEnhancedPageRelic>()
            .Any(relic => relic.Mode == WrathServantPageMode.Wrath) == true
            && owner.Creature.CombatState?.RoundNumber <= WrathServantEnhancedPageRelic.WrathSelfTargetTurns;
        return originalActive || enhancedActive;
    }

    private static IReadOnlyList<Creature> GetWrathFriendlyFireTargets(Player? owner)
    {
        if (!IsWrathFriendlyFireActive(owner) || owner?.Creature.CombatState == null)
        {
            return [];
        }

        return owner.Creature.CombatState.Creatures
            .Where(creature =>
                creature.IsAlive
                && creature.IsHittable)
            .OrderBy(creature => creature.CombatId)
            .ToArray();
    }

    internal static bool CanManuallyTargetWrathFriendlyFire(CardModel card, Creature? target)
    {
        return HasWrathServantWrathMode(card.Owner)
            && target != null
            && card.TargetType == TargetType.AnyEnemy
            && card.Type == CardType.Attack
            && target.Side == card.Owner.Creature.Side
            && GetWrathFriendlyFireTargets(card.Owner).Contains(target);
    }

    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (__instance.ModelSource is not CardModel card
            || !IsWrathFriendlyFireActive(card.Owner)
            || card.Type != CardType.Attack
            || card.TargetType is not (TargetType.AllEnemies or TargetType.RandomEnemy)
            || __instance.Attacker?.CombatState == null)
        {
            return;
        }

        __result = GetWrathFriendlyFireTargets(card.Owner);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    private static class CardModelIsValidTargetPatch
    {
        private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
        {
            if (!__result && CanManuallyTargetWrathFriendlyFire(__instance, target))
            {
                __result = true;
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
        private static void Postfix(
            Creature creature,
            TargetType ____validTargetsType,
            ref bool __result)
        {
            if (!__result
                && ____validTargetsType == TargetType.AnyEnemy
                && ActiveTargetingCard is { } card
                && CanManuallyTargetWrathFriendlyFire(card, creature))
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
                || !IsWrathFriendlyFireActive(card.Owner)
                || card.TargetType != TargetType.AnyEnemy
                || card.Type != CardType.Attack)
            {
                return;
            }

            Control[] extraHitboxes = GetWrathFriendlyFireTargets(card.Owner)
                .Where(creature => creature.Side == card.Owner.Creature.Side)
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
                || !IsWrathFriendlyFireActive(card.Owner)
                || card.Type != CardType.Attack)
            {
                return;
            }

            foreach (Creature creature in GetWrathFriendlyFireTargets(card.Owner)
                .Where(creature => creature.Side == card.Owner.Creature.Side))
            {
                NCombatRoom.Instance?.GetCreatureNode(creature)?.ShowMultiselectReticle();
            }
        }
    }
}
