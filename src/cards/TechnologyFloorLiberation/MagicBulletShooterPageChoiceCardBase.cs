using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.relics.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public abstract class MagicBulletShooterPageChoiceCardBase : PageChoiceCard<MagicBulletShooterPageMode>
{
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
}
