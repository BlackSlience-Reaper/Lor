using Godot;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.WedgeOffice;

public partial class EriCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Eri))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.42f, 0.42f), -122f, -299.7f, 122f, 5f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -260f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "eri",
            WedgeOfficeAssets.EriTexture,
            24f,
            -145.2f,
            0.48f);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("", "eri", GuestSpine.StrikeThrustSlash);
}
