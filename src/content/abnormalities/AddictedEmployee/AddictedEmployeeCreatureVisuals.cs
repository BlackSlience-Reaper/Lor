using System.Collections.Generic;
using Godot;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.AddictedEmployee;

/// <summary>
/// 着魔的职员（及技术科学层和弦里的同名职员）的外观：Spine 身体（tools/spine_from_sprite/addicted_employee.json 生成）
/// 按躯干、头、持棍手臂分层，手臂按肩、肘、腕三节挥动；待机、挥棍攻击、举棍防御、受击、死亡。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public sealed partial class AddictedEmployeeCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(AddictedEmployee))]
    [MonsterVisual(typeof(TechnologyFloorChordStaff))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -122f), new(0.58f, 0.58f), -96f, -230f, 99f, -20f, new(0f, -122f), new(20f, -290f))
    {
        TalkPos = new Vector2(0f, -286f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>挥棍劈到位（命中）的时刻，秒；多段攻击的第一段按它等待后结算伤害，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>
    /// 多段攻击后续各段的等待，秒。整个招式只播一次攻击动画（后续段的触发被忽略）；
    /// 加上招式里段间的 0.18 秒，第三段约在 0.96 秒落下，仍在劈下后的定格里（0.92 秒后才收回）。
    /// </summary>
    internal const float FollowUpHitSeconds = 0.15f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        AddictedEmployeeAssets.AddictedEmployeeSpineAtlas,
        AddictedEmployeeAssets.AddictedEmployeeSpineSkeleton,
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.08f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string> { ["Guard"] = "guard" });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "idle.png");
        profile.Frame("attack", AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "attack.png");
        profile.Frame("guard", AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "guard.png");
        profile.Frame("hit", AddictedEmployeeAssets.AddictedEmployeeMonsterPrefix + "hit.png");
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("guard", 0.40f, "Guard");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}
