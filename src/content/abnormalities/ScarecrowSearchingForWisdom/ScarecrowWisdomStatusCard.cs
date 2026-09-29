using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

[CardPool(typeof(StatusCardPool))]
public sealed class ScarecrowWisdomStatusCard : CardModel
{
    internal const int WisdomStacks = 1;

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/status/scarecrow_wisdom_status_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ScarecrowWisdomPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ScarecrowWisdomPower>(WisdomStacks)
    ];

    public ScarecrowWisdomStatusCard()
        : base(1, CardType.Status, CardRarity.Status, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmdCompat.Apply<ScarecrowWisdomPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(ScarecrowWisdomPower)].IntValue,
            Owner.Creature,
            this);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is ScarecrowWisdomStatusCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}
