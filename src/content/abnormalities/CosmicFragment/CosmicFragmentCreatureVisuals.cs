using System.Collections.Generic;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.CosmicFragment;

/// <summary>
/// 宇宙碎片的外观：Spine 身体（tools/spine_from_sprite/cosmic_fragment.json 生成）。待机时腹部起伏、花头点头、腿尖摆动；
/// 穿刺、异界回响蓄力后换成原攻击图的人物，受击换成原受击图，死亡时四腿摊开。接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public sealed partial class CosmicFragmentCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(CosmicFragment))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -120f), new(0.55f, 0.55f), -130f, -259f, 129f, 10f, new(0f, -120f), new(0f, -340f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>
    /// 异界回响后续各段的等待，秒。加上段间的 0.08 秒，三段约在 0.3、0.58、0.86 秒落下，和换图人物鼓起的时刻对齐，
    /// 都在回响姿势里（1.08 秒后才换回）。
    /// </summary>
    internal const float EchoFollowUpSeconds = 0.2f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        CosmicFragment.SpineAtlasPath,
        CosmicFragment.SpineSkeletonPath,
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        // 不做死亡动画：死时保持当前姿势，原版立即溶解（整体向后倒的死亡动画观感不好，去掉了）
        DeathAnimation: null,
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string> { ["Attack2"] = "echo" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            CosmicFragment.IdleTexturePath);
        profile.Frame(
            "attack",
            CosmicFragment.AttackTexturePath);
        profile.Frame(
            "attack2",
            CosmicFragment.Attack2TexturePath);
        profile.Frame(
            "hit",
            CosmicFragment.HitTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("attack2", 0.42f, "Attack2");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}
