namespace LibraryOfRuina.visuals.DawnOffice;

public partial class YunaCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "yuna",
            "res://images/monsters/yuna.png",
            12f,
            -145.2f,
            0.53f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
