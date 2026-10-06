using System.Collections.Generic;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

/// <summary>
/// 小蜘蛛的外观：Spine 身体（tools/spine_from_sprite/small_spider.json 生成）。两条镰刀状前肢摆动；尖牙时两条镰刀举起再劈下，叠原攻击图抠出的紫色月牙，两段每段都按 AttackImpactSeconds 等待（WithHitCount），都落在劈下后的定格里（0.78 秒后才收回）；施法时身体立起、镰刀高举。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class SpiderBudSmallSpiderCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(SpiderBudSmallSpider))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -50f), new(0.6f, 0.6f), -113f, -135f, 138f, 3f, new(0f, -130f), new(0f, -220f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/spider_bud/small_spider.atlas",
        "res://images/monsters/spider_bud/small_spider.spine-json",
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        // 不做死亡动画：死时保持当前姿势，原版立即溶解（整体向后倒的死亡动画观感不好，去掉了）
        DeathAnimation: null,
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string> { ["Cast"] = "cast" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            SpiderBudSmallSpider.IdleTexturePath);
        profile.Frame("attack", SpiderBudSmallSpider.AttackTexturePath);
        profile.Frame("cast", SpiderBudSmallSpider.CastTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("cast", 0.18f, "Cast");
        profile.Swap("cast", 0.12f, "Hit");
        return profile;
    }
}
