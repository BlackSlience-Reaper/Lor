using MegaCrit.Sts2.Core.Entities.Powers;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.content.abnormalities.RedShoes;

public sealed class LibraryOfRuinaBloodThirstPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BLOOD_THIRST_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;
}
