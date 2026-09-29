using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.SpiderBud;

public partial class SpiderBudCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.SpiderBud.SpiderBud))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -152f), new(0.72f, 0.72f), -140f, -305f, 140f, 8f, new(0f, -145f), new(0f, -330f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.SpiderBud.SpiderBud.IdleTexturePath);
        profile.Frame(
            "attack",
            monsters.SpiderBud.SpiderBud.AttackTexturePath);
        profile.Frame(
            "guard",
            monsters.SpiderBud.SpiderBud.GuardTexturePath);
        profile.Swap("attack", 0.2f, "Attack");
        profile.Swap("guard", 0.12f, "Hit");
        profile.Swap("guard", 0.2f, "Cast");
        return profile;
    }
}
