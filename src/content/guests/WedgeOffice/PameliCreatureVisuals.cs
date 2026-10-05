using Godot;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.WedgeOffice;

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
            WedgeOfficeAssets.PameliTexture,
            18f,
            -145.2f,
            0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("", "pameli", GuestSpine.StrikeThrustSlash);
}
