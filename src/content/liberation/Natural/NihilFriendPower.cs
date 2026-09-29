using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NihilFriendPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NIHIL_FRIEND_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.None;

    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar("FriendEnergy", WrathServantEnhancedPageRelic.FriendEnergy),
        new DynamicVar("FriendCards", WrathServantEnhancedPageRelic.FriendCards)
    ];
}
