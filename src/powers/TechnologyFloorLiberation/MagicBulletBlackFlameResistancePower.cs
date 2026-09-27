using MegaCrit.Sts2.Core.Entities.Powers;

namespace LibraryOfRuina.powers.TechnologyFloorLiberation;

public sealed class MagicBulletBlackFlameResistancePower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "MAGIC_BULLET_BLACK_FLAME_RESISTANCE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}
