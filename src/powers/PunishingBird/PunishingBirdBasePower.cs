using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.powers.PunishingBird;

public abstract class PunishingBirdBasePower : LibraryOfRuinaPowerModel
{
    protected abstract string IconFileName { get; }

    public override string PackedIconPath => ImageHelper.GetImagePath("powers/" + IconFileName);

    public override string ResolvedBigIconPath => PackedIconPath;
}