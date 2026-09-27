using LibraryOfRuina.monsters.RedShoes;

namespace LibraryOfRuina.visuals.RedShoes;

public partial class RedShoesLeftCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            RedShoesLeft.IdleTexturePath);
        profile.Frame("attack", RedShoesLeft.AttackTexturePath);
        profile.Frame("hit", RedShoesLeft.HitTexturePath);
        profile.Frame("parry", RedShoesLeft.ParryTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        profile.Swap("parry", 0.18f, "Cast");
        return profile;
    }
}
