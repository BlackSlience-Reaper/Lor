namespace LibraryOfRuina.visuals.DawnOffice;

public partial class YangCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        DawnOfficeTripleAttackCreatureVisuals.BuildTripleAttackProfile(
            "yang",
            "res://images/monsters/yang.png",
            12f,
            -145.2f,
            0.58f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
