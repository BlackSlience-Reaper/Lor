using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.SpinyBus;

/// <summary>
/// 棘刺公交的外观：Spine 身体（tools/spine_from_sprite/spiny_bus.json 生成）。待机时花头点头、花苞摇晃；两种攻击与格挡换成原图人物。小头爆炸三段交替两种攻击，每段都是一次新动画，所以每段都按 AttackImpactSeconds 等待。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public sealed partial class SpinyBusCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(SpinyBus))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -126f), new(0.58f, 0.58f), -132f, -320f, 132f, 10f, new(0f, -126f), new(0f, -348f))
    {
        TalkPos = new Vector2(0f, -266f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>死亡动画时长，秒；原版等它播完再做溶解消失。</summary>
    internal const float DeathSeconds = 1.6f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/spiny_bus/spiny_bus.atlas",
        "res://images/monsters/spiny_bus/spiny_bus.spine-json",
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string> { ["Attack2"] = "attack2", ["Parry"] = "parry" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    protected override void BeforeResolveSpriteTrigger(string triggerName)
    {
        if (triggerName == "Hit")
        {
            LocalOggOneShotPlayer.Play(
                SpinyBus.HitSfxPath,
                -2f);
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            SpinyBus.IdleTexturePath);
        profile.Frame(
            "parry",
            SpinyBus.ParryTexturePath);
        profile.Frame(
            "attack",
            SpinyBus.AttackTexturePath);
        profile.Frame(
            "attack2",
            SpinyBus.Attack2TexturePath);
        profile.Frame(
            "hit",
            SpinyBus.HitTexturePath);
        profile.Swap("parry", 0.50f, "Parry");
        profile.Swap("attack", 0.48f, "Attack");
        profile.Swap("attack2", 0.48f, "Attack2");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}
