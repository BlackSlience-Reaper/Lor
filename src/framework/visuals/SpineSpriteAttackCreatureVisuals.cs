using Godot;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// 贴图外观上换 Spine 身体的公共部分（骨骼由 tools/spine_from_sprite 从贴图生成，见 <see cref="RuntimeSpineBody"/>）。
/// 贴图外观照旧建好：布局、碰撞框、目标头像都读那张待机 Sprite2D；Spine 加载成功后两张 Sprite2D 一直藏着，
/// 触发交给 Spine，混乱时定格受击姿势。加载失败时什么都不换，退回基类的贴图与换图动作。
/// </summary>
public abstract partial class SpineSpriteAttackCreatureVisuals : SpriteAttackCreatureVisuals, INonSpineVisualTriggerHandler
{
    private RuntimeSpineBody? _spine;

    internal abstract RuntimeSpineBody.Spec SpineSpec { get; }

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
    // 基类换图前会调 BeforeResolveSpriteTrigger（棘刺公交在这里播受击音效），走 Spine 时同样先调。
    bool INonSpineVisualTriggerHandler.TryPlayTrigger(string triggerName)
    {
        if (_spine == null)
        {
            return TryPlayTrigger(triggerName);
        }

        BeforeResolveSpriteTrigger(triggerName);
        return _spine.Play(triggerName, MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this));
    }

    // 基类在混乱状态切换、动作结束时回到这里重设两张贴图；有 Spine 身体时贴图一直藏着，由 Spine 定格或解除受击姿势。
    protected override void RestoreIdleState()
    {
        base.RestoreIdleState();
        HideSprites();
        _spine?.SyncHoldHurt(MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this));
    }

    /// <summary>
    /// 原版 <c>NCreature.StartDeathAnim</c> 只给原版 Spine 怪物发 "Dead" 触发，贴图外观收不到，由死亡补丁转到这里。
    /// 怪物要同时把 <c>DeathAnimLengthOverride</c> 设成死亡动画时长，原版才会等动画播完再做溶解消失。
    /// </summary>
    internal void PlayDeath() => _spine?.Play("Dead", holdHurtPose: false);

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
