using Godot;
using LibraryOfRuina.guests.WedgeOffice;
using LibraryOfRuina.patches;
using LibraryOfRuina.visuals.DawnOffice;

namespace LibraryOfRuina.visuals.WedgeOffice;

public partial class PameliCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Pameli))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-8f, -145.2f), new(0.47f, 0.47f), -118f, -299.7f, 118f, 5f, new(-8f, -139.8f), new(-8f, -333.7f))
    {
        TalkPos = new Vector2(-10f, -260f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "pameli",
            "res://images/monsters/pameli.webp",
            18f,
            -145.2f,
            0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}
