using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

/// <summary>
/// 全能助手的外观：Spine 身体（tools/spine_from_sprite/all_around_helper.json 生成）播待机、攻击、受击、死亡，
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class AllAroundHelperCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(AllAroundHelper))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -108f), new(0.58f, 0.58f), -101f, -198f, 96f, -17f, new(0f, -108f), new(0f, -292f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>
    /// 攻击动画里冲到最远（命中）的时刻，秒；多段攻击的第一段按它等待后结算伤害。与原版怪物攻击的默认等待相同，
    /// 快速模式下原版把它缩到一半，动画不加速（原版也不加速），伤害比冲刺最远处略早。
    /// </summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>
    /// 多段攻击后续各段的等待，秒。整个招式只播一次攻击动画（后续段的触发被忽略），
    /// 第二段在第一段结算后约 0.25 秒落下，仍在动画的旋转阶段（约 0.87 秒前）。
    /// </summary>
    internal const float FollowUpHitSeconds = 0.25f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        AllAroundHelperAssets.AllAroundHelperSpineAtlas,
        AllAroundHelperAssets.AllAroundHelperSpineSkeleton,
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.08f,
        // 残影跟着攻击动画的旋转段（0.12–0.87 秒）淡入淡出
        Ghosts: new RuntimeSpineBody.GhostSpec(
            LagSeconds: 0.016f,
            Alpha: [0.5f, 0.36f, 0.26f, 0.18f, 0.12f, 0.07f],
            FadeInStart: 0.12f,
            FadeInEnd: 0.22f,
            FadeOutStart: 0.74f,
            FadeOutEnd: 0.9f));

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
