using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.enchantments.Leticia;

public sealed class LeticiaPartnerMarkEnchantment : EnchantmentModel
{
    internal const int PlayCostReduction = 1;
    internal const int HandEndCostIncrease = 1;

    public override bool HasExtraCardText => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CostReduction", PlayCostReduction),
        new DynamicVar("CostIncrease", HandEndCostIncrease)
    ];

    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Skill;
    }

    public override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (cardPlay == null)
        {
            return Task.CompletedTask;
        }

        Card.EnergyCost.AddThisCombat(-(int)DynamicVars["CostReduction"].BaseValue, reduceOnly: true);
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!HasCard || Card.Owner?.Creature?.Side != side || Card.Pile?.Type != PileType.Hand)
        {
            return Task.CompletedTask;
        }

        Card.EnergyCost.AddThisCombat((int)DynamicVars["CostIncrease"].BaseValue);
        return Task.CompletedTask;
    }
}
