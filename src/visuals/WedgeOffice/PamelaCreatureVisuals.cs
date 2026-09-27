using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.WedgeOffice;

public partial class PamelaCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "pamela",
            "res://images/monsters/pamela.webp",
            18f,
            -145.2f,
            0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
