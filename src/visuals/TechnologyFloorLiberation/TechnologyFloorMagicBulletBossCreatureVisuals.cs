using LibraryOfRuina.monsters.TechnologyFloorLiberation;

namespace LibraryOfRuina.visuals.TechnologyFloorLiberation;

public sealed partial class TechnologyFloorMagicBulletBossCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            TechnologyFloorMagicBulletBoss.IdleTexturePath);
        profile.Frame(
            "attack",
            TechnologyFloorMagicBulletBoss.AttackTexturePath);
        profile.Frame("hit", TechnologyFloorMagicBulletBoss.HitTexturePath);
        profile.Frame(
            "special",
            TechnologyFloorMagicBulletBoss.SpecialTexturePath);
        profile.Swap("attack", 0.7f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("special", 3.0f, "Special");
        return profile;
    }
}
