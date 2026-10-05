using System.Collections.Generic;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

/// <summary>
/// 蜘蛛巢的外观：Spine 身体（tools/spine_from_sprite/spider_bud.json 生成）。所有动画只转动、缩放茧这个部件，骨头在丝顶，挂点始终不动：待机像钟摆一样摇、轻轻鼓动；攻击往后荡再往前荡，前方炸开原攻击图抠出的红色尖刺；施法鼓动并染红；受击被打得来回摆；死亡挂着荡几下停住，不倒下。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class SpiderBudCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(SpiderBud))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -152f), new(0.72f, 0.72f), -130f, -305f, 126f, 8f, new(0f, -145f), new(0f, -330f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>死亡动画时长，秒；原版等它播完再做溶解消失。</summary>
    internal const float DeathSeconds = 1.6f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/spider_bud/spider_bud.atlas",
        "res://images/monsters/spider_bud/spider_bud.spine-json",
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
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
            SpiderBud.IdleTexturePath);
        profile.Frame(
            "attack",
            SpiderBud.AttackTexturePath);
        profile.Frame(
            "guard",
            SpiderBud.GuardTexturePath);
        profile.Swap("attack", 0.2f, "Attack");
        profile.Swap("guard", 0.12f, "Hit");
        profile.Swap("guard", 0.2f, "Cast");
        return profile;
    }
}
