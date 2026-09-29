using Godot;
using LibraryOfRuina.guests.YunOffice;
using LibraryOfRuina.patches;
using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.YunOffice;

public partial class YunCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Yun))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.52f, 0.52f), -112f, -299.7f, 112f, 5f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -260f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "yun",
            "res://images/monsters/yun.webp",
            22f,
            -145.2f,
            0.56f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
