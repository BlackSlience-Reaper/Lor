using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.CosmicFragment;

public sealed partial class CosmicFragmentCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(CosmicFragment))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -120f), new(0.55f, 0.55f), -130f, -300f, 130f, 10f, new(0f, -120f), new(0f, -340f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            CosmicFragment.IdleTexturePath);
        profile.Frame(
            "attack",
            CosmicFragment.AttackTexturePath);
        profile.Frame(
            "attack2",
            CosmicFragment.Attack2TexturePath);
        profile.Frame(
            "hit",
            CosmicFragment.HitTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("attack2", 0.42f, "Attack2");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}
