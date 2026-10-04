using Godot;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Technology;

/// <summary>小帮手Mk2 与小帮手共用同一套贴图，也共用它的 Spine 身体。</summary>
public partial class TechnologyFloorMk4HelperCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(TechnologyFloorMk4Helper))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -85f), new(0.50f, 0.50f), -80f, -190f, 80f, 8f, new(0f, -85f), new(0f, -225f))
    {
        TalkPos = new Vector2(0f, -175f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => AllAroundHelperCreatureVisuals.Spine;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                TechnologyFloorMk4Helper.IdleTexturePath)
            .At(0f, -85f)
            .Scale(0.50f)
            .IdleOnly();
        profile.Frame(
            "attack",
            TechnologyFloorMk4Helper.AttackTexturePath);
        profile.Frame("hit", TechnologyFloorMk4Helper.HitTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
