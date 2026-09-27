namespace LibraryOfRuina.visuals.DawnOffice;

public partial class GinCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        DawnOfficeTripleAttackCreatureVisuals.BuildTripleAttackProfile(
            "gin",
            "res://images/monsters/gin.png",
            15f,
            -145.2f,
            0.53f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
