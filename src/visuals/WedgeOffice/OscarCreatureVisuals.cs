using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.WedgeOffice;

public partial class OscarCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "oscar",
            "res://images/monsters/oscar.webp",
            30f,
            -145.2f,
            0.54f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
