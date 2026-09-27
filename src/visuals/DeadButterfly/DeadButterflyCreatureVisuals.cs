namespace LibraryOfRuina.visuals.DeadButterfly;

public partial class DeadButterflyCreatureVisuals : SpriteAttackCreatureVisuals
{
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
