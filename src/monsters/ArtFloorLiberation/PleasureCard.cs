using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.ArtFloorLiberation;

[CardPool(typeof(StatusCardPool))]
public sealed class PleasureCard() : CardModel(-1, CardType.Status, CardRarity.Status, TargetType.None)
{
    public const int EnergyGain = 1;
    public const int Threshold = 3;
    public const int HpLoss = 24;

    private const string PortraitResourcePath = "packed/card_portraits/status/pleasure_card.png";

    public override int MaxUpgradeLevel => 0;

    public override string PortraitPath => ImageHelper.GetImagePath(PortraitResourcePath);

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(EnergyGain),
        new DynamicVar("Threshold", Threshold),
        new DynamicVar("HpLoss", HpLoss)
    ];

    public override bool HasTurnEndInHandEffect => IsFirstPleasureCardInHand();

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this && Owner != null)
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        }
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || Owner.Creature == null)
        {
            return;
        }

        IReadOnlyList<PleasureCard> pleasureCards = PleasureCardsHeldForTurnEnd(Owner).ToArray();
        if (pleasureCards.Count < Threshold)
        {
            return;
        }

        var ownerCreature = Owner.Creature;

        foreach (CardModel card in pleasureCards.Cast<CardModel>().ToArray())
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        await CreatureCmdCompat.Damage(
            choiceContext,
            ownerCreature,
            HpLoss,
            ValueProp.Unpowered | ValueProp.Unblockable,
            dealer: null,
            cardSource: this);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is PleasureCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }

    private bool IsFirstPleasureCardInHand()
    {
        if (Owner == null || Pile?.Type != PileType.Hand)
        {
            return false;
        }

        return PileType.Hand.GetPile(Owner).Cards.OfType<PleasureCard>().FirstOrDefault() == this;
    }

    private IEnumerable<PleasureCard> PleasureCardsHeldForTurnEnd(Player owner)
    {
        foreach (PleasureCard card in PileType.Hand.GetPile(owner).Cards.OfType<PleasureCard>())
        {
            yield return card;
        }

        if (Pile?.Type == PileType.Play)
        {
            yield return this;
        }
    }

    public static string GetPortraitResourcePath() => PortraitResourcePath;
}
