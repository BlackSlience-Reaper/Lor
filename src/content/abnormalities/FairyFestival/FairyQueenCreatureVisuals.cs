using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

/// <summary>
/// 妖精女王的外观：Spine 身体（tools/spine_from_sprite/fairy_queen.json 生成）。三片翅膀快速扇动、长腿摆动；攻击、施法、受击换成原图人物；死亡不播动画，照原版直接溶解。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class FairyQueenCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(FairyQueen))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -114f), new(0.58f, 0.58f), -167f, -284f, 148f, 8f, new(0f, -118f), new(0f, -318f))
    {
        TalkPos = new Vector2(0f, -258f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>饥饿振翅后续各段的等待，秒；三段约在 0.3、0.5、0.7 秒落下，都在攻击姿势里（0.9 秒后才换回）。</summary>
    internal const float FollowUpHitSeconds = 0.2f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    internal static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/fairy_festival/fairy_queen.atlas",
        "res://images/monsters/fairy_festival/fairy_queen.spine-json",
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        // 不做死亡动画：死时保持当前姿势，原版立即溶解（与换成 Spine 之前一致）
        DeathAnimation: null,
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string> { ["Cast"] = "cast", ["Parry"] = "cast" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            FairyQueen.IdleTexturePath);
        profile.Frame("idle_alt", FairyQueen.IdleAltTexturePath);
        profile.Frame("attack", FairyQueen.AttackTexturePath)
            .Nudge(34f, -112f)
            .Scale(0.56f);
        profile.Frame("cast", FairyQueen.CastTexturePath);
        profile.Frame("hit", FairyQueen.HitTexturePath);
        profile.Lunge("attack", 0.25f, 0.25f, 0.28f, "Attack");
        profile.Swap("cast", 0.7f, "Cast", "Parry");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
