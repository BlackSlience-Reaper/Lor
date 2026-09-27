namespace LibraryOfRuina.visuals.DawnOffice;

public partial class PhilipCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "philip",
            "res://images/monsters/philip.png",
            18f,
            -145.2f,
            0.5f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
