using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

/// <summary>
/// 妖精群的外观：Spine 身体（tools/spine_from_sprite/fairy_mass.json 生成）。悬停起伏、四片翅膀快速扇动、细腿摆动；攻击、受击换成原图人物。
/// 接入方式见 <see cref="SpineSpriteAttackCreatureVisuals"/>。
/// </summary>
public partial class FairyMassCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(FairyMass))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -108f), new(0.46f, 0.46f), -116f, -250f, 110f, 8f, new(0f, -108f), new(0f, -284f))
    {
        TalkPos = new Vector2(0f, -220f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>换成攻击图后命中的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>振翅、贪食后续各段的等待，秒；两段约在 0.3、0.55 秒落下，都在攻击姿势里（0.85 秒后才换回）。</summary>
    internal const float FollowUpHitSeconds = 0.2f;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine = new(
        "res://images/monsters/fairy_festival/fairy_mass.atlas",
        "res://images/monsters/fairy_festival/fairy_mass.spine-json",
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
            FairyMass.IdleTexturePath);
        profile.Frame("idle_alt", FairyMass.IdleAltTexturePath);
        profile.Frame("attack", FairyMass.AttackTexturePath)
            .Nudge(24f, -104f)
            .Scale(0.50f);
        profile.Frame("hit", FairyMass.HitTexturePath);
        profile.Lunge("attack", 0.24f, 0.2f, 0.28f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}
