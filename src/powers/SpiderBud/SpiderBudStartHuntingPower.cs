using MegaCrit.Sts2.Core.Entities.Powers;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.powers.SpiderBud;

public sealed class SpiderBudStartHuntingPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SPIDER_BUD_START_HUNTING_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}
