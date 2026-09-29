using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.SpiderBud;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.SpiderBud;

public partial class SpiderBudSmallSpiderCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(SpiderBudSmallSpider))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -50f), new(0.6f, 0.6f), -60f, -190f, 60f, 5f, new(0f, -130f), new(0f, -220f));

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
