using System.Collections.Generic;
using Godot;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// 贴图外观上换 Spine 身体的公共部分（骨骼由 tools/spine_from_sprite 从贴图生成，见 <see cref="RuntimeSpineBody"/>）。
/// 贴图外观照旧建好：布局、碰撞框、目标头像都读那张待机 Sprite2D；Spine 加载成功后两张 Sprite2D 一直藏着，
/// 触发交给 Spine，混乱时定格受击姿势。加载失败时什么都不换，退回基类的贴图与换图动作。
/// 会换形态的怪物（贴图外观的 Variant）可以按形态给不同骨架，见 <see cref="SpineSpecFor"/>。
/// </summary>
public abstract partial class SpineSpriteAttackCreatureVisuals : SpriteAttackCreatureVisuals, INonSpineVisualTriggerHandler
{
    // 每份骨架只建一次：骨架原点按建立时那张待机贴图对齐，各形态的待机贴图不同，所以第一次换到该形态时才建
    private readonly Dictionary<RuntimeSpineBody.Spec, RuntimeSpineBody?> _bodies = new();
    private RuntimeSpineBody? _spine;
    private RuntimeSpineBody.Spec? _activeSpec;
    private bool _ready;

    internal abstract RuntimeSpineBody.Spec SpineSpec { get; }

    /// <summary>当前形态用的骨架；默认所有形态共用 <see cref="SpineSpec"/>。</summary>
    internal virtual RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => SpineSpec;

    /// <summary>出场时先读进缓存的全部骨架；会换形态的外观要列出各形态的，战斗中换形态才不会现读。</summary>
    internal virtual IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [SpineSpec];

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        foreach (RuntimeSpineBody.Spec spec in AllSpineSpecs)
        {
            RuntimeSpineBody.Preload(spec);
        }

        SyncSpineBody();
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
        return PlaySpineTrigger(triggerName);
    }

    /// <summary>有 Spine 身体在接管触发（自己管触发入口的外观，例如薄暝，要先问这个）。</summary>
    internal bool HasSpineBody => _spine != null;

    /// <summary>自己重新摆放待机贴图的外观（薄暝）摆完后调用：骨架重新对齐到待机贴图，贴图再藏起来。</summary>
    protected void RealignSpineBody()
    {
        if (_spine != null && GetNodeOrNull<Sprite2D>("%Visuals") is { } idle)
        {
            _spine.AlignTo(idle);
        }

        HideSprites();
    }

    /// <summary>
    /// 把触发交给 Spine。触发里声明的换形态照样切换（伪王座的变形），切换后由新形态的骨架播放；
    /// 新骨架不认识这个触发时停在它的待机，也算处理过。
    /// </summary>
    protected bool PlaySpineTrigger(string triggerName)
    {
        ApplyTriggerVariantSwitch(triggerName);
        _spine?.Play(triggerName, MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this));
        return true;
    }

    // 基类在混乱状态切换、动作结束、换形态时回到这里重设两张贴图；有 Spine 身体时贴图一直藏着，由 Spine 定格或解除受击姿势。
    protected override void RestoreIdleState()
    {
        base.RestoreIdleState();
        SyncSpineBody();
        HideSprites();
        _spine?.SyncHoldHurt(MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this));
    }

    /// <summary>
    /// 原版 <c>NCreature.StartDeathAnim</c> 只给原版 Spine 怪物发 "Dead" 触发，贴图外观收不到，由死亡补丁转到这里。
    /// 怪物要同时把 <c>DeathAnimLengthOverride</c> 设成死亡动画时长，原版才会等动画播完再做溶解消失。
    /// </summary>
    internal void PlayDeath() => _spine?.Play("Dead", holdHurtPose: false);

    // 基类 _Ready 里设初始形态时也会走到 RestoreIdleState，那时还不建骨架，等 _Ready 末尾再建。
    private void SyncSpineBody()
    {
        if (!_ready)
        {
            return;
        }

        RuntimeSpineBody.Spec spec = SpineSpecFor(CurrentSpriteVariantKey);
        if (ReferenceEquals(spec, _activeSpec))
        {
            return;
        }

        _activeSpec = spec;
        if (!_bodies.TryGetValue(spec, out RuntimeSpineBody? body))
        {
            body = GetNodeOrNull<Sprite2D>("%Visuals") is { } idle ? RuntimeSpineBody.TryCreate(idle, spec) : null;
            _bodies[spec] = body;
        }
        else
        {
            body?.Play("Idle", holdHurtPose: false);
        }

        foreach (RuntimeSpineBody? other in _bodies.Values)
        {
            if (other != null)
            {
                other.Visible = other == body;
                other.ProcessMode = other == body ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            }
        }

        _spine = body;
        // 这个形态的骨架没加载成功：先前藏起来的待机贴图要露出来，退回换图
        if (body == null && GetNodeOrNull<Sprite2D>("%Visuals") is { } sprite)
        {
            sprite.Visible = true;
        }
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
