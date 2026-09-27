namespace LibraryOfRuina.visuals.CosmicFragment;

public sealed partial class CosmicFragmentCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.CosmicFragment.CosmicFragment.IdleTexturePath);
        profile.Frame(
            "attack",
            monsters.CosmicFragment.CosmicFragment.AttackTexturePath);
        profile.Frame(
            "attack2",
            monsters.CosmicFragment.CosmicFragment.Attack2TexturePath);
        profile.Frame(
            "hit",
            monsters.CosmicFragment.CosmicFragment.HitTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("attack2", 0.42f, "Attack2");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}
