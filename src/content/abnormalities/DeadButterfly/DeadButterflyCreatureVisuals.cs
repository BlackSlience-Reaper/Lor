using System.Collections.Generic;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.DeadButterfly;

/// <summary>
/// 亡蝶的外观：Spine 身体（tools/spine_from_sprite/dead_butterfly.json 生成）里三只蝴蝶各自飘动、扇翅；
/// 攻击时聚拢扑出并炸开爆刺，施法时散开再聚回，受击被打散，死亡时逐只飘落。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class DeadButterflyCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(DeadButterfly))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -88f), new(0.36f, 0.36f), -78f, -182f, 78f, 8f, new(0f, -88f), new(0f, -218f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>扑到最远（命中）的时刻，秒；多段攻击的第一段按它等待后结算伤害，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>
    /// 多段攻击后续各段的等待，秒。整个招式只播一次攻击动画（后续段的触发被忽略）；
    /// 加上招式里段间的 0.18 秒，第二段约在 0.68 秒落下，蝴蝶还没飞回（0.72 秒后才回位）。
    /// </summary>
    internal const float FollowUpHitSeconds = 0.2f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        DeadButterflyAssets.DeadButterflySpineAtlas,
        DeadButterflyAssets.DeadButterflySpineSkeleton,
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.08f,
        // 残影跟着扑出段（0.1–0.6 秒）淡入淡出
        Ghosts: new RuntimeSpineBody.GhostSpec(
            LagSeconds: 0.016f,
            Alpha: [0.45f, 0.3f, 0.2f, 0.12f],
            FadeInStart: 0.1f,
            FadeInEnd: 0.18f,
            FadeOutStart: 0.42f,
            FadeOutEnd: 0.6f),
        ExtraTriggers: new Dictionary<string, string> { ["Cast"] = "cast" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            DeadButterfly.IdleTexturePath);
        profile.Frame(
            "attack",
            DeadButterfly.AttackTexturePath);
        profile.Frame(
            "hit",
            DeadButterfly.HitTexturePath);
        profile.Swap("attack", 0.18f * 2.25f, "Attack");
        profile.Swap("hit", 0.16f * 2.25f, "Cast", "Hit");
        return profile;
    }
}
