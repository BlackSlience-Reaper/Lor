using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.DeadButterfly;

public partial class DeadButterflyCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(DeadButterfly))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -88f), new(0.36f, 0.36f), -78f, -182f, 78f, 8f, new(0f, -88f), new(0f, -218f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            DeadButterfly.IdleTexturePath);
        profile.Frame(
            "attack",
            DeadButterfly.AttackTexturePath);
        profile.Frame(
            "hit",
            DeadButterfly.HitTexturePath);
        profile.Swap("attack", 0.18f * 2.25f, "Attack");
        profile.Swap("hit", 0.16f * 2.25f, "Cast", "Hit");
        return profile;
    }
}
