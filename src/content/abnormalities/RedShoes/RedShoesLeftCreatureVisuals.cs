using System.Collections.Generic;
using Godot;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.RedShoes;

/// <summary>
/// 红舞鞋（左，含文学层的强化左鞋）的外观：Spine 身体（tools/spine_from_sprite/red_shoes_left.json 生成）。鞋是身体，长袜、斧柄、斧头一条链，待机绕鞋摇晃；攻击时骨骼把斧头举到身后、从头顶绕到左前方劈下，叠上原攻击图抠出的弧光；欲望时斧头整根放下挡在鞋前；受击时被击退、斧头甩向后上方再回正。全部是骨骼动作，不换图。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class RedShoesLeftCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(LiteratureFloorEnhancedLeftShoe))]
    [MonsterVisual(typeof(RedShoesLeft))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -98f), new(0.52f, 0.52f), -137f, -252f, 150f, 35f, new(0f, -98f), new(0f, -310f))
    {
        TalkPos = new Vector2(0f, -230f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    internal static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/red_shoes/red_shoes_left.atlas",
        "res://images/monsters/red_shoes/red_shoes_left.spine-json",
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
            RedShoesLeft.IdleTexturePath);
        profile.Frame("attack", RedShoesLeft.AttackTexturePath);
        profile.Frame("hit", RedShoesLeft.HitTexturePath);
        profile.Frame("parry", RedShoesLeft.ParryTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        profile.Swap("parry", 0.18f, "Cast");
        return profile;
    }
}
