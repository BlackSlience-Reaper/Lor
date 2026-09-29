using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.RedShoes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.RedShoes;

public sealed class LibraryOfRuinaRedShoesBloodAttractionPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "RED_SHOES_BLOOD_ATTRACTION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BleedBonusDamage", RedShoesRight.BleedBonusDamage),
        new DynamicVar("Strength", RedShoesRight.ObsessionStrengthGain),
        new DynamicVar("HealMultiplier", RedShoesRight.BleedHealMultiplier),
        new DynamicVar("StunTurns", RedShoesRight.SelfStunTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>(),
        HoverTipFactory.Static(StaticHoverTip.Stun)
    ];

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner == null || Owner.CombatState == null || side == Owner.Side)
        {
            return Task.CompletedTask;
        }

        if (Owner.Monster is RedShoesRight rightShoe)
        {
            rightShoe.CacheTargetForCurrentIntent();
        }

        return Task.CompletedTask;
    }
}
