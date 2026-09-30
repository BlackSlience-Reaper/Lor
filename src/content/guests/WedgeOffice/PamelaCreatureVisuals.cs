using Godot;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.WedgeOffice;

public partial class PamelaCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Pamela))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(4f, -145.2f), new(0.47f, 0.47f), -118f, -299.7f, 118f, 5f, new(4f, -139.8f), new(4f, -333.7f))
    {
        TalkPos = new Vector2(6f, -260f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "pamela",
            WedgeOfficeAssets.PamelaTexture,
            18f,
            -145.2f,
            0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
