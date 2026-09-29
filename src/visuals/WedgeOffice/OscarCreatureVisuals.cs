using Godot;
using LibraryOfRuina.guests.WedgeOffice;
using LibraryOfRuina.patches;
using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.WedgeOffice;

public partial class OscarCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Oscar))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-6f, -145.2f), new(0.48f, 0.48f), -122f, -299.7f, 122f, 5f, new(-6f, -139.8f), new(-6f, -333.7f))
    {
        TalkPos = new Vector2(-8f, -262f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "oscar",
            "res://images/monsters/oscar.webp",
            30f,
            -145.2f,
            0.54f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
