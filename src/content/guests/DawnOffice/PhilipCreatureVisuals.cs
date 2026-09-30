using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.DawnOffice;

public partial class PhilipCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Philip))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(6f, -145.2f), new(0.45f, 0.45f), -116f, -299.7f, 116f, 5f, new(6f, -139.8f), new(6f, -333.7f))
    {
        TalkPos = new Vector2(8f, -258f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "philip",
            DawnOfficeAssets.PhilipTexture,
            18f,
            -145.2f,
            0.5f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
