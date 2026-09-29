using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.DawnOffice;

public partial class GinCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(Gin))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(0, -139.8f), new(0, -333.7f));

    internal static readonly SpriteVisualProfile Profile =
        DawnOfficeTripleAttackCreatureVisuals.BuildTripleAttackProfile(
            "gin",
            "res://images/monsters/gin.png",
            15f,
            -145.2f,
            0.53f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
