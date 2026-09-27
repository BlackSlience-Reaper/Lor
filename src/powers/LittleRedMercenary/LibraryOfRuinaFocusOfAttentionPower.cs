using MegaCrit.Sts2.Core.Entities.Powers;

namespace LibraryOfRuina.powers.LittleRedMercenary;

public sealed class LibraryOfRuinaFocusOfAttentionPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LIBRARY_OF_RUINA_FOCUS_OF_ATTENTION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}
