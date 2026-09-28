using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.cards.BigBird;
using LibraryOfRuina.encounters.BigBird;
using LibraryOfRuina.powers.BigBird;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.patches.BigBird;

internal static class BigBirdTargetingPatch
{
    private static CardModel? ActiveTargetingCard { get; set; }

    internal static bool CanManuallyTargetCharmedAlly(CardModel? card, Creature? target)
    {
        if (card?.Owner?.Creature == null || target == null)
        {
            return false;
        }

        return card.TargetType == TargetType.AnyEnemy
            && card.Type == CardType.Attack
            && target.Side == card.Owner.Creature.Side
            && target != card.Owner.Creature
            && target.GetPower<BigBirdCharmedPower>() != null;
    }

    internal static Creature? ResolveForcedAttackTarget(CardModel? card)
    {
        Creature? ownerCreature = card?.Owner?.Creature;
        if (card == null || ownerCreature == null)
        {
            return null;
        }

        Creature? boss = BigBirdEncounterHelper.FindBoss(ownerCreature.CombatState);
        if (card is SoulSnareStatusCard)
        {
            return boss;
        }

        if (card.Type != CardType.Attack)
        {
            return null;
        }

        if (ownerCreature.GetPower<BigBirdPatrolPower>() is { } patrol)
        {
            return patrol.ResolveSource();
        }

        return null;
    }

    internal static bool IsValidBigBirdAttackTarget(CardModel card, Creature? target)
    {
        if (target == null)
        {
            return true;
        }

        if (card is SoulSnareStatusCard)
        {
            return target.Monster is monsters.BigBird.BigBird;
        }

        Creature? forcedTarget = ResolveForcedAttackTarget(card);
        return forcedTarget == null || target == forcedTarget;
    }

    internal static void FilterAttackTargets(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (__instance.ModelSource is not CardModel card
            || card.Type != CardType.Attack
            || __instance.Attacker?.CombatState == null)
        {
            return;
        }

        Creature? forcedTarget = ResolveForcedAttackTarget(card);
        if (forcedTarget == null || !forcedTarget.IsAlive || !forcedTarget.IsHittable)
        {
            return;
        }

        __result = [forcedTarget];
    }

    internal static class CardModelIsValidTargetPatch
    {
        internal static void FilterIsValidTarget(CardModel __instance, Creature? target, ref bool __result)
        {
            if (!__result && CanManuallyTargetCharmedAlly(__instance, target))
            {
                __result = true;
            }

            if (__result && !IsValidBigBirdAttackTarget(__instance, target))
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
        internal static void FilterAllowedToTargetCreature(
            Creature creature,
            TargetType ____validTargetsType,
            ref bool __result)
        {
            if (____validTargetsType != TargetType.AnyEnemy
                || ActiveTargetingCard is not { } card)
            {
                return;
            }

            if (!__result && CanManuallyTargetCharmedAlly(card, creature))
            {
                __result = true;
            }

            if (__result && !IsValidBigBirdAttackTarget(card, creature))
            {
                __result = false;
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
                || card.TargetType != TargetType.AnyEnemy
                || (card.Type != CardType.Attack && card is not SoulSnareStatusCard))
            {
                return;
            }

            var extraTargets = new List<Creature>();
            Creature? forcedTarget = ResolveForcedAttackTarget(card);
            if (forcedTarget != null)
            {
                extraTargets.Add(forcedTarget);
            }

            Creature? ownerCreature = card.Owner?.Creature;
            var combatState = ownerCreature?.CombatState;
            if (combatState == null)
            {
                return;
            }

            extraTargets.AddRange(combatState.Creatures
                .Where(creature => CanManuallyTargetCharmedAlly(card, creature)));

            Control[] extraHitboxes = extraTargets
                .Select(creature => __instance.GetCreatureNode(creature)?.Hitbox)
                .OfType<Control>()
                .ToArray();
            if (extraHitboxes.Length > 0)
            {
                whitelist = whitelist.Concat(extraHitboxes).Distinct().ToArray();
            }
        }
    }
}
