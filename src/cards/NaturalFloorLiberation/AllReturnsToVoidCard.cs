using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.NaturalFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class AllReturnsToVoidCard() : CardModel(BaseEnergyCost, CardType.Power, CardRarity.Ancient, TargetType.Self)
{
    // 万物归虚：未升级时的能量费用。
    public const int BaseEnergyCost = 2;

    // 万物归虚：升级后的能量费用。
    public const int UpgradedEnergyCost = 1;

    // 万物归虚：每张弃牌提供的强壮及忍耐层数。
    public const int BuffPerDiscard = 1;

    // 万物归虚：强壮与忍耐持续的回合数。
    public const int BuffTurns = 1;

    public override CardPoolModel VisualCardPool => ModelDb.CardPool<ColorlessCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath => "res://images/packed/card_portraits/colorless/nihil_emptiness.png";

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BuffPerDiscard", BuffPerDiscard),
        new DynamicVar("Turns", BuffTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<AllReturnsToVoidPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmdCompat.Apply<AllReturnsToVoidPower>(choiceContext, Owner.Creature,
            BuffPerDiscard, Owner.Creature, this);

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(UpgradedEnergyCost - BaseEnergyCost);
    }
}
