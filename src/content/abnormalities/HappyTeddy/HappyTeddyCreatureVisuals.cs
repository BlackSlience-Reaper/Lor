using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.HappyTeddy;

/// <summary>
/// 快乐泰迪的外观：Spine 身体（tools/spine_from_sprite/happy_teddy.json 生成）。待机时左右摇晃、歪头；示爱、怀念的拥抱
/// 蓄力后换成原攻击图的人物往前冲，羞怯的亲昵往后缩，受击换成原受击图，死亡仰面倒下。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class HappyTeddyCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HappyTeddyMonster))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(6f, -120f), new(0.58f, 0.58f), -120f, -272f, 122f, 11f, new(6f, -120f), new(-4f, -340f))
    {
        TalkPos = new Vector2(-8f, -272f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后第一拳命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>示爱后续各段的等待，秒。三拳约在 0.3、0.5、0.7 秒落下，和往前顶的时刻对齐，都在出拳姿势里（0.88 秒后才换回）。</summary>
    internal const float FollowUpHitSeconds = 0.2f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        HappyTeddyAssets.HappyTeddySpineAtlas,
        HappyTeddyAssets.HappyTeddySpineSkeleton,
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        // 不做死亡动画：死时保持当前姿势，原版立即溶解（整体向后倒的死亡动画观感不好，去掉了）
        DeathAnimation: null,
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string>
        {
            ["NostalgicEmbrace"] = "embrace",
            ["Cast"] = "cast",
        });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HappyTeddyAssets.HappyTeddyMonsterPrefix + ".webp");
        profile.Frame("attack", HappyTeddyAssets.HappyTeddyMonsterPrefix + "_attack_1.webp")
            .Nudge(20f, -120f)
            .Scale(0.60f);
        profile.Frame("nostalgic_embrace", HappyTeddyAssets.HappyTeddyMonsterPrefix + "_attack_2.webp")
            .Nudge(30f, -112f)
            .Scale(0.62f);
        profile.Frame("hit", HappyTeddyAssets.HappyTeddyMonsterPrefix + "_hit.webp");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Lunge(
            "nostalgic_embrace",
            0.2f,
            0.1f,
            0.25f,
            "NostalgicEmbrace");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
