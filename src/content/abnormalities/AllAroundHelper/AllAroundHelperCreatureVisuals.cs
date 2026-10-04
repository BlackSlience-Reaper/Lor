using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

/// <summary>
/// 全能助手的外观：贴图外观照旧建好（布局、碰撞框、目标头像都读它），再在上面换上 Spine 身体
/// （tools/spine_from_sprite/all_around_helper.json 生成），由 Spine 播待机、攻击、受击、死亡。
/// Spine 加载失败时什么都不换，退回原来的贴图与换图动作。
/// </summary>
public partial class AllAroundHelperCreatureVisuals : SpriteAttackCreatureVisuals, INonSpineVisualTriggerHandler
{
    [MonsterVisual(typeof(AllAroundHelper))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -108f), new(0.58f, 0.58f), -101f, -198f, 96f, -17f, new(0f, -108f), new(0f, -292f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    /// <summary>攻击动画里冲到最远（命中）的时刻，秒；多段攻击的第一段按它等待后结算伤害。</summary>
    internal const float AttackImpactSeconds = 0.6f;

    /// <summary>
    /// 多段攻击后续各段的等待，秒。整个招式只播一次攻击动画（后续段的触发被忽略），
    /// 第二段在第一段结算后约 0.25 秒落下，仍在动画的旋转阶段（约 0.95 秒前）。
    /// </summary>
    internal const float FollowUpHitSeconds = 0.25f;

    private static readonly RuntimeSpineBody.Spec SpineSpec = new(
        AllAroundHelperAssets.AllAroundHelperSpineAtlas,
        AllAroundHelperAssets.AllAroundHelperSpineSkeleton,
        IdleAnimation: "idle",
        AttackAnimation: "attack",
        HurtAnimation: "hurt",
        DeathAnimation: "die",
        DefaultMix: 0.12f,
        // 快速模式下原版把攻击等待缩到 0.25 秒，攻击动画按比例加速，命中仍落在冲刺最远处
        FastModeAttackTimeScale: AttackImpactSeconds / 0.25f,
        HurtHoldSeconds: 0.08f,
        Ghosts: new RuntimeSpineBody.GhostSpec(
            LagSeconds: 0.016f,
            Alpha: [0.5f, 0.36f, 0.26f, 0.18f, 0.12f, 0.07f],
            FadeInStart: 0.34f,
            FadeInEnd: 0.46f,
            FadeOutStart: 0.84f,
            FadeOutEnd: 1.0f));

    private RuntimeSpineBody? _spine;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            AllAroundHelperAssets.AllAroundHelperMonsterPrefix + ".webp");
        profile.Frame("attack", AllAroundHelperAssets.AllAroundHelperMonsterPrefix + "_attack.webp");
        profile.Frame("hit", AllAroundHelperAssets.AllAroundHelperMonsterPrefix + "_hit.webp");
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }

    public override void _Ready()
    {
        base._Ready();
        if (GetNodeOrNull<Sprite2D>("%Visuals") is { } idle)
        {
            _spine = RuntimeSpineBody.TryCreate(idle, SpineSpec);
        }

        HideSprites();
    }

    // 触发桥按接口调用；这里重新实现接口，有 Spine 身体时由它处理，否则走基类的换图动作。
    bool INonSpineVisualTriggerHandler.TryPlayTrigger(string triggerName) =>
        _spine?.Play(triggerName, MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this)) ?? TryPlayTrigger(triggerName);

    // 基类在混乱状态切换、动作结束时回到这里重设两张贴图；有 Spine 身体时贴图一直藏着，由 Spine 定格或解除受击姿势。
    protected override void RestoreIdleState()
    {
        base.RestoreIdleState();
        HideSprites();
        _spine?.SyncHoldHurt(MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this));
    }

    private void HideSprites()
    {
        if (_spine == null)
        {
            return;
        }

        foreach (string name in new[] { "%Visuals", "%AttackVisuals" })
        {
            if (GetNodeOrNull<Sprite2D>(name) is { } sprite)
            {
                sprite.Visible = false;
            }
        }
    }
}
