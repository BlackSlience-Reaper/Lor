using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.ForsakenMurderer;

/// <summary>
/// 被遗弃的杀人魔的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块），加载失败时退回下面的逐帧换图。
/// 各姿势按膝盖着地的点对齐，动作只做水平位移，膝盖始终在同一高度。
/// </summary>
public partial class ForsakenMurdererCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "forsaken_murderer",
        "forsaken_murderer",
        "attack",
        new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ForsakenMurderer))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -100f), new(0.31f, 0.31f), -160f, -250f, 160f, 20f, new(0f, -100f), new(0f, -280f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root = "res://images/monsters/forsaken_murderer";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + ".webp");
        profile.Frame("attack", root + "_attack.webp")
            .Nudge(18f, -100f)
            .Scale(0.33f);
        profile.Frame("hit", root + "_hit.webp");
        profile.Lunge("attack", 0.18f, 0.1f, 0.22f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
