using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

public partial class AllAroundHelperCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(AllAroundHelper))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -108f), new(0.58f, 0.58f), -108f, -244f, 108f, 12f, new(0f, -108f), new(0f, -292f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            AllAroundHelperAssets.AllAroundHelperMonsterPrefix + ".webp");
        profile.Frame("attack", AllAroundHelperAssets.AllAroundHelperMonsterPrefix + "_attack.webp");
        profile.Frame("hit", AllAroundHelperAssets.AllAroundHelperMonsterPrefix + "_hit.webp");
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
