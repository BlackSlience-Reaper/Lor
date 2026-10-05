using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.DawnOffice;

public partial class GinCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(Gin))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(0, -139.8f), new(0, -333.7f));

    internal static readonly SpriteVisualProfile Profile =
        DawnOfficeTripleAttackCreatureVisuals.BuildTripleAttackProfile(
            "gin",
            DawnOfficeAssets.GinTexture,
            15f,
            -145.2f,
            0.53f);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("", "gin", GuestSpine.StrikeThrustSlash);
}
