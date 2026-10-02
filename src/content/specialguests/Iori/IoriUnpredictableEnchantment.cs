using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace LibraryOfRuina.content.specialguests.Iori;

/// <summary>
/// Purple Tear's event enchantment. Whenever the card is drawn, it receives a
/// synced 0-3 combat cost, auto-plays for free, then returns to its owner's hand.
/// </summary>
public sealed partial class IoriUnpredictableEnchantment : EnchantmentModel
{
    public override bool HasExtraCardText => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Energy),
    ];

    public override bool CanEnchantCardType(CardType cardType) =>
        cardType == CardType.Attack;

    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card)
        && !card.EnergyCost.CostsX
        && !card.Keywords.Contains(CardKeyword.Unplayable);

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        _ = fromHandDraw;
        if (card != Card || Card.Pile?.Type != PileType.Hand)
        {
            return;
        }

        CombatStateLike? combatState = Card.CombatState;
        if (combatState is null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Card.EnergyCost.SetThisCombat(
            Card.Owner.RunState.Rng.CombatEnergyCosts.NextInt(4));
        NCard.FindOnTable(card)?.PlayRandomizeCostAnim();

        await CardCmd.AutoPlay(
            choiceContext,
            Card,
            target: null,
            skipXCapture: true);

        // AutoPlay can take its early result-pile path when another model
        // prevents the play or the target became invalid. That path bypasses
        // ModifyCardPlayResultLocation, so restore the draw-trigger contract.
        if (!CombatManager.Instance.IsOverOrEnding
            && Card.CombatState == combatState
            && Card.Pile is { IsCombatPile: true, Type: not PileType.Hand })
        {
            await CardPileCmd.Add(Card, PileType.Hand);
        }
    }

}
