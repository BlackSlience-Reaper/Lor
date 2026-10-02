using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

public partial class SpiderBudCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(SpiderBud))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -152f), new(0.72f, 0.72f), -130f, -305f, 126f, 8f, new(0f, -145f), new(0f, -330f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            SpiderBud.IdleTexturePath);
        profile.Frame(
            "attack",
            SpiderBud.AttackTexturePath);
        profile.Frame(
            "guard",
            SpiderBud.GuardTexturePath);
        profile.Swap("attack", 0.2f, "Attack");
        profile.Swap("guard", 0.12f, "Hit");
        profile.Swap("guard", 0.2f, "Cast");
        return profile;
    }
}
