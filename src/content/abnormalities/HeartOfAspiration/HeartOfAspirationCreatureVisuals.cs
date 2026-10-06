using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.HeartOfAspiration;

/// <summary>
/// 渴望之心的外观：Spine 身体（tools/spine_from_sprite/heart_of_aspiration.json 生成）。待机时心跳般起伏，蓝色血管和小手摆动；攻击、防御蓄力后换成原图人物，受击换成原受击图，死亡时倒下。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public sealed partial class HeartOfAspirationCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HeartOfAspiration))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.984f, 0.984f), -190f, -390f, 190f, 14f, new(0f, -165f), new(0f, -355f))
    {
        TalkPos = new Vector2(0f, -320f),
        StateDisplayLiftY = 32f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/heart_of_aspiration/heart_of_aspiration.atlas",
        "res://images/monsters/heart_of_aspiration/heart_of_aspiration.spine-json",
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        // 不做死亡动画：死时保持当前姿势，原版立即溶解（整体向后倒的死亡动画观感不好，去掉了）
        DeathAnimation: null,
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string> { ["Guard"] = "guard", ["Cast"] = "guard" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HeartOfAspiration.IdleTexturePath);
        profile.Frame(
            "attack",
            HeartOfAspiration.AttackTexturePath);
        profile.Frame(
            "hit",
            HeartOfAspiration.HitTexturePath);
        profile.Frame(
            "guard",
            HeartOfAspiration.GuardTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("hit", 0.36f, "Hit");
        profile.Swap("guard", 0.4f, "Guard", "Cast");
        return profile;
    }
}

/// <summary>
/// 渴望之肺的外观：Spine 身体（tools/spine_from_sprite/lung_of_aspiration.json 生成）。待机时两片肺叶一张一缩、气管点头；剧烈搏动每段都按 AttackImpactSeconds 等待（WithHitCount），三段约在 0.3、0.6、0.9 秒落下，都在攻击姿势里（1.05 秒后才换回）。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public sealed partial class LungOfAspirationCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(LungOfAspiration))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -6f), new(-0.984f, 0.984f), -170f, -350f, 170f, 14f, new(0f, -150f), new(0f, -365f))
    {
        TalkPos = new Vector2(0f, -290f),
        StateDisplayLiftY = 28f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/lung_of_aspiration/lung_of_aspiration.atlas",
        "res://images/monsters/lung_of_aspiration/lung_of_aspiration.spine-json",
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        // 不做死亡动画：死时保持当前姿势，原版立即溶解（整体向后倒的死亡动画观感不好，去掉了）
        DeathAnimation: null,
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string> { ["Special"] = "special", ["Cast"] = "special" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            LungOfAspiration.IdleTexturePath);
        profile.Frame("attack", LungOfAspiration.AttackTexturePath);
        profile.Frame("hit", LungOfAspiration.HitTexturePath);
        profile.Frame("special", LungOfAspiration.SpecialTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("hit", 0.36f, "Hit");
        profile.Swap("special", 0.52f, "Special", "Cast");
        return profile;
    }
}
