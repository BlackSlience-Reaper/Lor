namespace LibraryOfRuina.visuals.DawnOffice;

public partial class SalvadorCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "salvador",
            "res://images/monsters/salvador.png",
            2f,
            -145.2f,
            0.55f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
