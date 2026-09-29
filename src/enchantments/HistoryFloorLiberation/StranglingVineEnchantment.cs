using System.Threading.Tasks;
using LibraryOfRuina.interop;
using LibraryOfRuina.relics.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.enchantments.HistoryFloorLiberation;

public sealed class StranglingVineEnchantment : EnchantmentModel
{
    public override bool HasExtraCardText => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "Binding",
            SnowWhiteApplePageRelic.StranglingVineBinding),
        new DynamicVar(
            "Turns",
            SnowWhiteApplePageRelic.StranglingVineBindingTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBindingPower>()
    ];

    public override bool CanEnchantCardType(CardType cardType) =>
        cardType == CardType.Attack;

    protected override void OnEnchant()
    {
        foreach (DynamicVar dynamicVar in Card.DynamicVars.Values)
        {
            switch (dynamicVar)
            {
                case LibraryDamageVar damageVar:
                    damageVar.DamageType = LibraryDamageType.Pierce;
                    break;
                case LibraryCalculatedDamageVar calculatedDamageVar:
                    calculatedDamageVar.DamageType = LibraryDamageType.Pierce;
                    break;
                case LibraryOstyDamageVar ostyDamageVar:
                    ostyDamageVar.DamageType = LibraryDamageType.Pierce;
                    break;
            }
        }
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        _ = result;
        if (Status == EnchantmentStatus.Disabled
            || !ReferenceEquals(cardSource, Card)
            || (dealer != Card.Owner.Creature && dealer != Card.Owner.Osty)
            || target.Side == Card.Owner.Creature.Side
            || !target.IsAlive
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        await LibraryPowerCmd.Apply<LibraryBindingPower>(
            choiceContext,
            target,
            SnowWhiteApplePageRelic.StranglingVineBinding,
            turns: SnowWhiteApplePageRelic.StranglingVineBindingTurns,
            IsPermanent: false,
            Card.Owner.Creature,
            Card);
    }
}
