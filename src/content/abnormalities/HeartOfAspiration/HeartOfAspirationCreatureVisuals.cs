using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.HeartOfAspiration;

public sealed partial class HeartOfAspirationCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HeartOfAspiration))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.984f, 0.984f), -190f, -390f, 190f, 14f, new(0f, -165f), new(0f, -355f))
    {
        TalkPos = new Vector2(0f, -320f),
        StateDisplayLiftY = 32f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HeartOfAspiration.IdleTexturePath);
        profile.Frame(
            "attack",
            HeartOfAspiration.AttackTexturePath);
        profile.Frame(
            "hit",
            HeartOfAspiration.HitTexturePath);
        profile.Frame(
            "guard",
            HeartOfAspiration.GuardTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("hit", 0.36f, "Hit");
        profile.Swap("guard", 0.4f, "Guard", "Cast");
        return profile;
    }
}

public sealed partial class LungOfAspirationCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(LungOfAspiration))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -6f), new(-0.984f, 0.984f), -170f, -350f, 170f, 14f, new(0f, -150f), new(0f, -365f))
    {
        TalkPos = new Vector2(0f, -290f),
        StateDisplayLiftY = 28f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            LungOfAspiration.IdleTexturePath);
        profile.Frame("attack", LungOfAspiration.AttackTexturePath);
        profile.Frame("hit", LungOfAspiration.HitTexturePath);
        profile.Frame("special", LungOfAspiration.SpecialTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("hit", 0.36f, "Hit");
        profile.Swap("special", 0.52f, "Special", "Cast");
        return profile;
    }
}
