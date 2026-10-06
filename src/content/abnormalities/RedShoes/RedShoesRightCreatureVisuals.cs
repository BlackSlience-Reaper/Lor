using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.RedShoes;

/// <summary>
/// 红舞鞋（右）的外观：Spine 身体（tools/spine_from_sprite/red_shoes_right.json 生成），做法同左鞋。欲望爆发三段每段都按 AttackImpactSeconds 等待，都落在劈下后的定格里（0.95 秒后才收回）。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class RedShoesRightCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(RedShoesRight))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -98f), new(0.52f, 0.52f), -136f, -254f, 150f, 35f, new(0f, -98f), new(0f, -310f))
    {
        TalkPos = new Vector2(0f, -230f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/red_shoes/red_shoes_right.atlas",
        "res://images/monsters/red_shoes/red_shoes_right.spine-json",
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        // 不做死亡动画：死时保持当前姿势，原版立即溶解（整体向后倒的死亡动画观感不好，去掉了）
        DeathAnimation: null,
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: null);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            RedShoesRight.IdleTexturePath);
        profile.Frame("attack", RedShoesRight.AttackTexturePath);
        profile.Frame("hit", RedShoesRight.HitTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
