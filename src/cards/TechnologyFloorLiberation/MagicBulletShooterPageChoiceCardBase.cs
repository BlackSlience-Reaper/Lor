using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.relics.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public abstract class MagicBulletShooterPageChoiceCardBase : CardModel
{
    public const string CommissionChoiceId =
        "MAGIC_BULLET_COMMISSION_CHOICE_CARD";
    public const string SeventhBulletChoiceId =
        "MAGIC_BULLET_SEVENTH_BULLET_CHOICE_CARD";
    public const string BlackFlameChoiceId =
        "MAGIC_BULLET_BLACK_FLAME_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<MagicBulletCommissionTargetPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<MagicBulletBlackFlameResistancePower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "CommissionDamagePercent",
            MagicBulletShooterPageRelic.CommissionDamagePercent),
        new GoldVar(MagicBulletShooterPageRelic.CommissionGoldPerKill),
        new PowerVar<LibraryStrongPower>(
            "Strong",
            MagicBulletShooterPageRelic.SeventhBulletStrong),
        new DynamicVar(
            "AttackInterval",
            MagicBulletShooterPageRelic.SeventhBulletAttackInterval),
        new DynamicVar(
            "PlayerDamageReductionPercent",
            MagicBulletShooterPageRelic.SeventhBulletPlayerDamageReductionPercent),
        new DynamicVar(
            "DamageTakenPercent",
            MagicBulletShooterPageRelic.BlackFlameDamageTakenPercent),
        new DynamicVar(
            "BlackFlameTurnInterval",
            MagicBulletShooterPageRelic.BlackFlameTurnInterval)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            $"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected MagicBulletShooterPageChoiceCardBase()
        : base(
            -1,
            CardType.Skill,
            CardRarity.Ancient,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }

    public static bool IsMagicBulletShooterChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is CommissionChoiceId
            or SeventhBulletChoiceId
            or BlackFlameChoiceId;
    }
}
