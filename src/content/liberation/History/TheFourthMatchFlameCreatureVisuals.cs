using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>
/// 第四根火柴：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。
/// </summary>
public partial class TheFourthMatchFlameCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "history_floor", "fourth_match", "attack", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(TheFourthMatchFlame))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -54f), new(0.38f, 0.38f), -117f, -121f, 96f, 8f, new(0f, -56f), new(0f, -182f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HistoryFloorAssets.ImagesMonstersRoot + "the_fourth_match_flame.png");
        profile.Frame(
                "attack",
                HistoryFloorAssets.ImagesMonstersRoot + "the_fourth_match_flame_attack.webp")
            .Nudge(12f, -54f)
            .Scale(0.42f);
        profile.Frame(
            "hit",
            HistoryFloorAssets.ImagesMonstersRoot + "the_fourth_match_flame_hit.webp");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
