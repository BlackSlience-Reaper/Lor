using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.WedgeOffice;

public partial class EriCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "eri",
            "res://images/monsters/eri.webp",
            24f,
            -145.2f,
            0.48f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
