using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.powers.BlueStar;
using LibraryOfRuina.relics.BlueStar;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.BlueStar;

[CardPool(typeof(TokenCardPool))]
public sealed class BlueStarMartyrdomCard() :
    CardModel(0, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public const int BaseChaoDamage = 7;
    public const int UpgradedChaoDamage = 9;
    public const int SelfHpLoss = 1;
    public const string SharedPortraitPath =
        "res://images/packed/card_portraits/colorless/blue_star_martyrdom.png";

    public override CardPoolModel VisualCardPool =>
        ModelDb.CardPool<ColorlessCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath => SharedPortraitPath;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaoDamage", BaseChaoDamage),
        new HpLossVar(SelfHpLoss)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BlueStarMartyrdomPower>()
    ];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmdCompat.Apply<BlueStarMartyrdomPower>(
            Owner.Creature,
            DynamicVars["ChaoDamage"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ChaoDamage"].UpgradeValueBy(
            UpgradedChaoDamage - BaseChaoDamage);
    }
}

public abstract class BlueStarPageChoiceCardBase : PageChoiceCard<BlueStarPageMode>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaoDamage", BlueStarMartyrdomCard.BaseChaoDamage),
        new DynamicVar(
            "UpgradedChaoDamage",
            BlueStarMartyrdomCard.UpgradedChaoDamage),
        new HpLossVar(BlueStarMartyrdomCard.SelfHpLoss),
        new DynamicVar("AtonementPercent", BlueStarPageRelic.AtonementPercent),
        new DynamicVar("TurnInterval", BlueStarPageRelic.VoiceTurnInterval),
        new DynamicVar("VoiceChaoDamage", BlueStarPageRelic.VoiceChaoDamage)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromCardWithCardHoverTips<BlueStarMartyrdomCard>()
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlueStarMartyrdomChoiceCard :
    BlueStarPageChoiceCardBase
{
    public override BlueStarPageMode PageMode => BlueStarPageMode.Martyrdom;

    protected override string PortraitFileName =>
        "blue_star_martyrdom.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlueStarAtonementChoiceCard :
    BlueStarPageChoiceCardBase
{
    public override BlueStarPageMode PageMode => BlueStarPageMode.Atonement;

    protected override string PortraitFileName =>
        "blue_star_atonement_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlueStarVoiceOfRemembranceChoiceCard :
    BlueStarPageChoiceCardBase
{
    public override BlueStarPageMode PageMode => BlueStarPageMode.VoiceOfRemembrance;

    protected override string PortraitFileName =>
        "blue_star_voice_of_remembrance_choice_card.png";
}
