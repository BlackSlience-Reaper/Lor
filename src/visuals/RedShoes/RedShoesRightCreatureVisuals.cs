using LibraryOfRuina.monsters.RedShoes;

namespace LibraryOfRuina.visuals.RedShoes;

public partial class RedShoesRightCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            RedShoesRight.IdleTexturePath);
        profile.Frame("attack", RedShoesRight.AttackTexturePath);
        profile.Frame("hit", RedShoesRight.HitTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
