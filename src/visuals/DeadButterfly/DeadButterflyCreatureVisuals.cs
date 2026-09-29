using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.DeadButterfly;

public partial class DeadButterflyCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.DeadButterfly.DeadButterfly))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -88f), new(0.36f, 0.36f), -78f, -182f, 78f, 8f, new(0f, -88f), new(0f, -218f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.DeadButterfly.DeadButterfly.IdleTexturePath);
        profile.Frame(
            "attack",
            monsters.DeadButterfly.DeadButterfly.AttackTexturePath);
        profile.Frame(
            "hit",
            monsters.DeadButterfly.DeadButterfly.HitTexturePath);
        profile.Swap("attack", 0.18f * 2.25f, "Attack");
        profile.Swap("hit", 0.16f * 2.25f, "Cast", "Hit");
        return profile;
    }
}
