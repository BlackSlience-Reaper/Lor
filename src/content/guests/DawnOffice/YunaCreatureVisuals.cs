using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.DawnOffice;

public partial class YunaCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Yuna))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.48f, 0.48f), -118f, -299.7f, 118f, 5f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(-2f, -266f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "yuna",
            "res://images/monsters/yuna.png",
            12f,
            -145.2f,
            0.53f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
