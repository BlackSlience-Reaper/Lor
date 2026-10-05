using Godot;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.DawnOffice;

public partial class SalvadorCreatureVisuals : DawnOfficeTripleAttackCreatureVisuals
{
    [MonsterVisual(typeof(Salvador))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-10f, -145.2f), new(0.50f, 0.50f), -122f, -299.7f, 112f, 5f, new(-10f, -139.8f), new(-10f, -333.7f))
    {
        TalkPos = new Vector2(-18f, -270f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildTripleAttackProfile(
            "salvador",
            DawnOfficeAssets.SalvadorTexture,
            2f,
            -145.2f,
            0.55f);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("", "salvador", GuestSpine.StrikeThrustSlash);
}
