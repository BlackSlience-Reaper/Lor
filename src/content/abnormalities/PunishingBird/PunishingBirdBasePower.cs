using MegaCrit.Sts2.Core.Helpers;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.content.abnormalities.PunishingBird;

public abstract class PunishingBirdBasePower : LibraryOfRuinaPowerModel
{
    protected abstract string IconFileName { get; }

    public override string PackedIconPath => ImageHelper.GetImagePath("powers/" + IconFileName);

    public override string ResolvedBigIconPath => PackedIconPath;
}