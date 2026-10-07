#if STS2_0_107_1
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.core.compat;

internal static partial class CreatureCmdCompat
{
    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageVar damageVar,
        CardModel cardSource)
        => CreatureCmd.Damage(choiceContext, target, damageVar, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
        => CreatureCmd.Damage(choiceContext, target, amount, props, cardSource?.Owner.Creature, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        DamageVar damageVar,
        Creature dealer)
        => CreatureCmd.Damage(choiceContext, targets, damageVar, dealer);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature dealer)
        => CreatureCmd.Damage(choiceContext, targets, amount, props, dealer);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageVar damageVar,
        Creature dealer)
        => CreatureCmd.Damage(choiceContext, target, damageVar, dealer);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer)
        => CreatureCmd.Damage(choiceContext, target, amount, props, dealer);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageVar damageVar,
        Creature? dealer,
        CardModel? cardSource)
        => CreatureCmd.Damage(choiceContext, target, damageVar, dealer, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageVar damageVar,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
        => CreatureCmd.Damage(choiceContext, target, damageVar, dealer, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
        => CreatureCmd.Damage(choiceContext, target, amount, props, dealer, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
        => CreatureCmd.Damage(choiceContext, target, amount, props, dealer, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature>? targets,
        DamageVar damageVar,
        Creature? dealer,
        CardModel? cardSource)
        => CreatureCmd.Damage(choiceContext, targets ?? [], damageVar, dealer, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature>? targets,
        DamageVar damageVar,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
        => CreatureCmd.Damage(choiceContext, targets ?? [], damageVar, dealer, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature>? targets,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
        => CreatureCmd.Damage(choiceContext, targets ?? [], amount, props, dealer, cardSource);

    public static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature>? targets,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
        => CreatureCmd.Damage(choiceContext, targets ?? [], amount, props, dealer, cardSource);
}

#endif
