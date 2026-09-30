using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.DawnOffice;

public partial class YangCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(Yang))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0, -145.2f), new(0.53f, 0.53f), -124f, -299.7f, 124f, 5f, new(0, -139.8f), new(0, -333.7f));

    internal static readonly SpriteVisualProfile Profile =
        DawnOfficeTripleAttackCreatureVisuals.BuildTripleAttackProfile(
            "yang",
            DawnOfficeAssets.YangTexture,
            12f,
            -145.2f,
            0.58f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
