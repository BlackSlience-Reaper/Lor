using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.Leticia;

/// <summary>
/// 惊喜礼盒（红色人偶）：Spine 身体见 <see cref="LayeredBossSpine"/>（整块，各姿势按标注点对齐），加载失败时退回逐帧换图。
/// 原来攻击图按 0.65 倍画、比待机小一圈，骨架里放大到和待机同大。
/// </summary>
public partial class SurpriseGiftBoxCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "leticia",
        "gift_box",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "cast",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(SurpriseGiftBox))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -98.8f), new(0.806f, 0.806f), -72.8f, -235f, 72.8f, 10.4f, new(0f, -106.6f), new(0f, -306.8f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            LeticiaAssets.SurpriseGiftBoxMonsterPrefix + "idle.png");
        profile.Frame("attack", LeticiaAssets.SurpriseGiftBoxMonsterPrefix + "attack.png")
            .Nudge(23.4f, -106.6f)
            .Scale(0.65f);
        profile.Frame("cast", LeticiaAssets.SurpriseGiftBoxMonsterPrefix + "cast.png");
        profile.Frame("hit", LeticiaAssets.SurpriseGiftBoxMonsterPrefix + "hit.png");
        profile.Lunge("attack", 0.16f, 0.08f, 0.2f, "Attack");
        profile.Swap("cast", 0.36f, "Cast");
        profile.Swap("hit", 0.36f, "Hit");
        return profile;
    }
}
