using System.Linq;
using System.Threading.Tasks;
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

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public abstract class NaturalFloorHappinessCardBase(int cost) : CardModel(cost, CardType.Status, CardRarity.Status, TargetType.Self)
{
    protected abstract int BlockAmount { get; }

    public override int MaxUpgradeLevel => 0;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/status/happiness_shard.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(BlockAmount, ValueProp.Unpowered)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay cardPlay)
    {
        foreach (Creature boss in Owner.Creature.CombatState?.Enemies
                     .Where(static enemy => enemy.IsAlive && enemy.Monster is NaturalFloorGoldRushBoss).ToArray() ?? [])
        {
            foreach (VigorPower vigor in boss.Powers.OfType<VigorPower>().ToArray())
            {
                await PowerCmd.Remove(vigor);
            }
        }

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Unpowered, cardPlay);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card.GetType() == GetType())
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}

[CardPool(typeof(StatusCardPool))]
public sealed class NaturalFloorHappinessShard() : NaturalFloorHappinessCardBase(1)
{
    protected override int BlockAmount => 21; // 幸福的碎片：打出后获得的固定格挡。

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];
}

[CardPool(typeof(StatusCardPool))]
public sealed class NaturalFloorShiningHappinessCard() : NaturalFloorHappinessCardBase(2)
{
    protected override int BlockAmount => 14; // 闪耀的幸福：打出后获得的固定格挡。

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
}
