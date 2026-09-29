using MegaCrit.Sts2.Core.Entities.Powers;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

public sealed class WrathServantFriendPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WRATH_SERVANT_FRIEND_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.None;

    public override PowerInstanceType InstanceType =>
        PowerInstanceType.InstancedPerApplier;
}
