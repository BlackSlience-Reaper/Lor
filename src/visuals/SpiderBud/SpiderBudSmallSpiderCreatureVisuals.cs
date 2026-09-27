using LibraryOfRuina.monsters.SpiderBud;

namespace LibraryOfRuina.visuals.SpiderBud;

public partial class SpiderBudSmallSpiderCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            SpiderBudSmallSpider.IdleTexturePath);
        profile.Frame("attack", SpiderBudSmallSpider.AttackTexturePath);
        profile.Frame("cast", SpiderBudSmallSpider.CastTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("cast", 0.18f, "Cast");
        profile.Swap("cast", 0.12f, "Hit");
        return profile;
    }
}
