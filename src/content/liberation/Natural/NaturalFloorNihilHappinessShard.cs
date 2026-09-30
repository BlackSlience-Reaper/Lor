using System.Threading.Tasks;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

[CardPool(typeof(StatusCardPool))]
public sealed class NaturalFloorNihilHappinessShard() : CardModel(
    NaturalFloorNihilMoves.ShardCost, CardType.Status, CardRarity.Status, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;

    public override string PortraitPath => NaturalFloorAssets.HappinessShardTexture;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(NaturalFloorNihilMoves.ShardBlock, ValueProp.Unpowered)];

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Unpowered, cardPlay);

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is NaturalFloorNihilHappinessShard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}
