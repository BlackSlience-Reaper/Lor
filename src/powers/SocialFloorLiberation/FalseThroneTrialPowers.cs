using System.Globalization;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.SocialFloorLiberation;

public abstract class FalseThroneTrialPowerBase :
    LibraryOfRuinaPowerModel
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected sealed class ToughAscensionVar : DynamicVar
    {
        private readonly int _normalValue;
        private readonly int _toughValue;

        public ToughAscensionVar(
            string name,
            int normalValue,
            int toughValue)
            : base(name, normalValue)
        {
            _normalValue = normalValue;
            _toughValue = toughValue;
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is FalseThroneTrialPowerBase
                {
                    IsMutable: true
                } power
                && power.Owner?.Monster is FalseThrone throne
                && throne.UsesToughValues
                    ? _toughValue
                    : _normalValue;
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible()
                .ToString(CultureInfo.InvariantCulture);
        }
    }
}

public sealed class FalseThroneWizardsTrialPower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_WIZARDS_TRIAL_POWER";
}

public sealed class FalseThroneShowYourWarmHeartPower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_SHOW_YOUR_WARM_HEART_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];
}

public sealed class FalseThroneEmptyChestPower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_EMPTY_CHEST_POWER";
}

public sealed class FalseThroneShowYourWisdomPower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_SHOW_YOUR_WISDOM_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ToughAscensionVar(
            "WisdomCards",
            SocialFloorLiberationEncounter.NormalWisdomCardCount,
            SocialFloorLiberationEncounter.ToughWisdomCardCount)
    ];
}

public sealed class FalseThroneInsignificantWisdomPower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_INSIGNIFICANT_WISDOM_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ToughAscensionVar(
            "WisdomRequired",
            SocialFloorLiberationEncounter.NormalWisdomRequirement,
            SocialFloorLiberationEncounter.ToughWisdomRequirement),
        new DynamicVar(
            "HpLossPercent",
            SocialFloorLiberationEncounter.ScarecrowPenaltyHpLossPercent)
    ];
}

public sealed class FalseThroneShowYourCouragePower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_SHOW_YOUR_COURAGE_POWER";
}

public sealed class FalseThroneTrulyCowardPower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_TRULY_COWARD_POWER";
}

public sealed class FalseThroneWhatCanYouDoPower :
    FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId =>
        "FALSE_THRONE_WHAT_CAN_YOU_DO_POWER";
}

public sealed class FalseThroneRagePower : FalseThroneTrialPowerBase
{
    protected override string LegacyPowerId => "FALSE_THRONE_RAGE_POWER";
}
