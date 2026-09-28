using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryLib.Commands;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.relics.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.patches.TechnologyFloorLiberation;

internal static class MagicBulletShooterPageAttackPatch
{
    internal static bool HasSeventhBulletMode(CardModel? card) =>
        card?.Owner?.Relics
            .OfType<MagicBulletShooterPageRelic>()
            .Any(static relic =>
                relic.Mode == MagicBulletShooterPageMode.SeventhBullet)
        == true;

    internal static bool IsSeventhBulletAttack(CardModel? card) =>
        card?.Owner?.Relics
            .OfType<MagicBulletShooterPageRelic>()
            .Any(relic => relic.IsSeventhBulletCard(card))
        == true;

    internal static IReadOnlyList<Creature> ResolveAllTargets(
        CardModel card)
    {
        CombatStateLike? combatState = card.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return [];
        }

        return combatState.Players
            .Select(static player => player.Creature)
            .Concat(combatState.Enemies)
            .Where(static creature => creature.IsAlive && creature.IsHittable)
            .Distinct()
            .OrderBy(static creature => creature.Side)
            .ThenBy(static creature => creature.CombatId ?? uint.MaxValue)
            .ToArray();
    }

    internal static void ModifyDamageArguments(
        CardModel? card,
        ref IEnumerable<Creature> targets,
        ref ValueProp props)
    {
        if (!HasSeventhBulletMode(card)
            || card?.Type != CardType.Attack
            || !ValuePropCompat.IsCardOrMonsterMove(props))
        {
            return;
        }

        props |= ValueProp.Unblockable;
        IReadOnlyList<Creature> targetList =
            targets as IReadOnlyList<Creature> ?? targets.ToArray();
        targets = targetList;
        if (IsSeventhBulletAttack(card)
            && targetList.Count > 0
            && targetList.All(static target => !target.IsPlayer)
            && ValuePropCompat.IsPoweredAttack(props & ~ValueProp.Unblockable))
        {
            targets = ResolveAllTargets(card);
        }
    }
}

internal static class MagicBulletShooterAttackTargetsPatch
{
    internal static void FilterAttackTargets(
        AttackCommand __instance,
        ref IReadOnlyList<Creature> __result)
    {
        if (__instance.ModelSource is CardModel card
            && MagicBulletShooterPageAttackPatch.IsSeventhBulletAttack(card))
        {
            __result = MagicBulletShooterPageAttackPatch.ResolveAllTargets(card);
        }
    }
}

internal static class MagicBulletShooterLibraryAttackTargetsPatch
{
    internal static void FilterLibraryAttackTargets(
        LibraryAttackCommand __instance,
        ref IReadOnlyList<Creature> __result)
    {
        if (__instance.ModelSource is CardModel card
            && MagicBulletShooterPageAttackPatch.IsSeventhBulletAttack(card))
        {
            __result = MagicBulletShooterPageAttackPatch.ResolveAllTargets(card);
        }
    }
}

[HarmonyPatch]
internal static class MagicBulletShooterCreatureDamagePatch
{
    private static int _targetsIndex;
    private static int _propsIndex;
    private static int _cardSourceIndex;

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        MethodInfo method = AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
            .Where(static candidate => candidate.Name == nameof(CreatureCmd.Damage))
            .Where(static candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Any(parameter =>
                           parameter.ParameterType == typeof(IEnumerable<Creature>))
                    && parameters.Any(parameter =>
                        parameter.ParameterType == typeof(decimal))
                    && parameters.Any(parameter =>
                        parameter.ParameterType == typeof(ValueProp))
                    && parameters.Any(parameter =>
                        parameter.ParameterType == typeof(CardModel));
            })
            .OrderByDescending(static candidate => candidate.GetParameters().Length)
            .First();

        ParameterInfo[] parameters = method.GetParameters();
        _targetsIndex = Array.FindIndex(
            parameters,
            static parameter =>
                parameter.ParameterType == typeof(IEnumerable<Creature>));
        _propsIndex = Array.FindIndex(
            parameters,
            static parameter => parameter.ParameterType == typeof(ValueProp));
        _cardSourceIndex = Array.FindIndex(
            parameters,
            static parameter => parameter.ParameterType == typeof(CardModel));
        return method;
    }

    [HarmonyPrefix]
    private static void Prefix(object[] __args)
    {
        if (_targetsIndex < 0 || _propsIndex < 0 || _cardSourceIndex < 0)
        {
            return;
        }

        CardModel? card = __args[_cardSourceIndex] as CardModel;
        IEnumerable<Creature> targets =
            __args[_targetsIndex] as IEnumerable<Creature> ?? [];
        ValueProp props = (ValueProp)__args[_propsIndex];
        MagicBulletShooterPageAttackPatch.ModifyDamageArguments(
            card,
            ref targets,
            ref props);
        __args[_targetsIndex] = targets;
        __args[_propsIndex] = props;
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
internal static class MagicBulletShooterLibraryDamagePatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(
        ref IEnumerable<Creature> targets,
        ref ValueProp props,
        CardModel? cardSource)
    {
        MagicBulletShooterPageAttackPatch.ModifyDamageArguments(
            cardSource,
            ref targets,
            ref props);
    }
}

[HarmonyPatch]
internal static class MagicBulletShooterBlackFlameResistancePatch
{
    [HarmonyTargetMethods]
    private static MethodBase[] TargetMethods() =>
    [
        AccessTools.Method(
            typeof(LibraryCreature),
            nameof(LibraryCreature.GetPhysicalResistanceLevel)),
        AccessTools.Method(
            typeof(LibraryCreature),
            nameof(LibraryCreature.GetChaosResistanceLevel))
    ];

    [HarmonyPostfix]
    private static void Postfix(
        LibraryCreature __instance,
        ref LibraryResistanceLevel __result)
    {
        if (__instance.Side != CombatSide.Enemy
            || !MagicBulletShooterPageRelic.IsBlackFlameRoundActive(
                __instance.CombatState))
        {
            return;
        }

        __result =
            MagicBulletShooterPageRelic.TransformBlackFlameResistance(
                __result);
    }
}
