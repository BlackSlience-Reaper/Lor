using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.KingOfGreed;

[CardPool(typeof(StatusCardPool))]
public sealed class HappinessShard : CardModel
{
    private const int BlockAmount = 15;
    private const int ArtifactStacks = 1;

    public override int MaxUpgradeLevel => 0;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/status/happiness_shard.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Ethereal,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(BlockAmount, ValueProp.Unpowered),
        new DynamicVar("Artifact", ArtifactStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ArtifactPower>()
    ];

    public HappinessShard()
        : base(1, CardType.Status, CardRarity.Status, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmdCompat.Apply<ArtifactPower>(choiceContext, Owner.Creature, ArtifactStacks, Owner.Creature, this);
        await CreatureCmd.GainBlock(Owner.Creature, BlockAmount, ValueProp.Unpowered, cardPlay);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is HappinessShard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}
