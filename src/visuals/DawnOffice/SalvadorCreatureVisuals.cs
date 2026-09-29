using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.DawnOffice;

public partial class SalvadorCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Salvador))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-10f, -145.2f), new(0.50f, 0.50f), -122f, -299.7f, 122f, 5f, new(-10f, -139.8f), new(-10f, -333.7f))
    {
        TalkPos = new Vector2(-18f, -270f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "salvador",
            "res://images/monsters/salvador.png",
            2f,
            -145.2f,
            0.55f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
