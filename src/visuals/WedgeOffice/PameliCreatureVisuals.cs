using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.WedgeOffice;

public partial class PameliCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "pameli",
            "res://images/monsters/pameli.webp",
            18f,
            -145.2f,
            0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
