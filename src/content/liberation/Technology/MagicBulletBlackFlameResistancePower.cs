using MegaCrit.Sts2.Core.Entities.Powers;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class MagicBulletBlackFlameResistancePower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "MAGIC_BULLET_BLACK_FLAME_RESISTANCE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}
