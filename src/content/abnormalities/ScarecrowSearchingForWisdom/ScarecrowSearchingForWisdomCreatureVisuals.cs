using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

/// <summary>
/// 求知的稻草人的外观：Spine 身体（tools/spine_from_sprite/scarecrow.json 生成）按躯干、头、两臂分层，待机随风晃；
/// 劈砍、突刺、收割先用分层身体蓄力，再换成原攻击图里的人物叠特效；受击换成原受击图，死亡整根倒下。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public sealed partial class ScarecrowSearchingForWisdomCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ScarecrowSearchingForWisdom))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -28f), new(0.50f, 0.50f), -136f, -336f, 120f, 8f, new(0f, -130f), new(0f, -390f))
    {
        TalkPos = new Vector2(0f, -280f),
        StateDisplayLiftY = 20f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>
    /// 劈砍、突刺换成攻击姿势后命中的时刻，秒，与原版怪物攻击的默认等待相同。多段劈砍每段都等这么久，
    /// 第二段约在 0.6 秒落下，仍在攻击姿势里（约 0.66 秒起才淡回）。
    /// </summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>收割每段的等待，秒。四段约在 0.25–1.0 秒落下，都在收割姿势里（1.12 秒后才换回）。</summary>
    internal const float HarvestHitSeconds = 0.25f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        ScarecrowSearchingForWisdom.SpineAtlasPath,
        ScarecrowSearchingForWisdom.SpineSkeletonPath,
        IdleAnimation: "idle",
        AttackAnimation: "strike",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
        DefaultMix: 0.12f,
        HurtHoldSeconds: 0.1f,
        Ghosts: null,
        ExtraTriggers: new Dictionary<string, string>
        {
            ["AttackStrike"] = "strike",
            ["AttackThrust"] = "thrust",
            ["Special"] = "special",
        });

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ScarecrowSearchingForWisdom.IdleTexturePath);
        profile.Frame(
            "hit",
            ScarecrowSearchingForWisdom.HitTexturePath);
        profile.Frame(
            "strike",
            ScarecrowSearchingForWisdom.StrikeTexturePath);
        profile.Frame(
            "thrust",
            ScarecrowSearchingForWisdom.ThrustTexturePath);
        profile.Frame(
                "special",
                ScarecrowSearchingForWisdom.SpecialTexturePath)
            .Nudge(-215f, 0f);
        profile.Swap("strike", 0.46f, "AttackStrike");
        profile.Swap("thrust", 0.46f, "AttackThrust");
        profile.Swap("special", 0.64f, "Special");
        profile.Swap("hit", 0.36f, "Hit");
        return profile;
    }
}
