using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.AllAroundHelper;

public partial class AllAroundHelperCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.AllAroundHelper.AllAroundHelper))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -108f), new(0.58f, 0.58f), -108f, -244f, 108f, 12f, new(0f, -108f), new(0f, -292f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root = "res://images/monsters/all_around_helper";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + ".webp");
        profile.Frame("attack", root + "_attack.webp");
        profile.Frame("hit", root + "_hit.webp");
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
