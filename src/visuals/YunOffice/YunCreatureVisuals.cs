using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.YunOffice;

public partial class YunCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "yun",
            "res://images/monsters/yun.webp",
            22f,
            -145.2f,
            0.56f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
