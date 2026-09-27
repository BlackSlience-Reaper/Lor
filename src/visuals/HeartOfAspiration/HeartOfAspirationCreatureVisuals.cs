using LibraryOfRuina.monsters.HeartOfAspiration;

namespace LibraryOfRuina.visuals.HeartOfAspiration;

public sealed partial class HeartOfAspirationCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.HeartOfAspiration.HeartOfAspiration.IdleTexturePath);
        profile.Frame(
            "attack",
            monsters.HeartOfAspiration.HeartOfAspiration.AttackTexturePath);
        profile.Frame(
            "hit",
            monsters.HeartOfAspiration.HeartOfAspiration.HitTexturePath);
        profile.Frame(
            "guard",
            monsters.HeartOfAspiration.HeartOfAspiration.GuardTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("hit", 0.36f, "Hit");
        profile.Swap("guard", 0.4f, "Guard", "Cast");
        return profile;
    }
}

public sealed partial class LungOfAspirationCreatureVisuals
    : SpriteAttackCreatureVisuals
{
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
