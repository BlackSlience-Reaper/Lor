using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

public sealed partial class ScarecrowSearchingForWisdomCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ScarecrowSearchingForWisdom))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -28f), new(0.50f, 0.50f), -120f, -330f, 120f, 8f, new(0f, -130f), new(0f, -390f))
    {
        TalkPos = new Vector2(0f, -280f),
        StateDisplayLiftY = 20f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ScarecrowSearchingForWisdom.IdleTexturePath);
        profile.Frame(
            "hit",
            ScarecrowSearchingForWisdom.HitTexturePath);
        profile.Frame(
            "strike",
            ScarecrowSearchingForWisdom.StrikeTexturePath);
        profile.Frame(
            "thrust",
            ScarecrowSearchingForWisdom.ThrustTexturePath);
        profile.Frame(
                "special",
                ScarecrowSearchingForWisdom.SpecialTexturePath)
            .Nudge(-215f, 0f);
        profile.Swap("strike", 0.46f, "AttackStrike");
        profile.Swap("thrust", 0.46f, "AttackThrust");
        profile.Swap("special", 0.64f, "Special");
        profile.Swap("hit", 0.36f, "Hit");
        return profile;
    }
}
