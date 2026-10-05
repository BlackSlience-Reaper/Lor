using System;
using Godot;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// 贴图外观里换上的 Spine 身体：骨骼与图集是 tools/spine_from_sprite 从贴图生成的原始文件（.spine-json、.atlas），
/// 运行时按 res:// 路径加载，不走编辑器的 Spine 导入（导出 PCK 用的 Godot 没有 Spine 模块）。
/// <list type="bullet">
/// <item>游戏的 Spine 是引擎内置模块，C# 侧没有生成的类，按 Godot 方法名调用。</item>
/// <item>骨架原点是源贴图底边中点、单位是贴图像素，所以按待机 Sprite2D 的位置、缩放和翻转对齐，与原贴图完全重合。</item>
/// <item>攻击动画的残影（动态模糊）：主体后面叠几份共用骨骼数据的 SpineSprite，手动推进，比主体晚固定时长、逐个变淡；
/// 只在开始攻击时各设一次动画，之后每帧按时间差推进。每帧清轨道再定位会把运行库卡死。</item>
/// </list>
/// 加载失败时返回 null，外观保持原来的贴图。只是本机表现，不参与同步。
/// </summary>
internal sealed partial class RuntimeSpineBody : Node2D
{
    internal sealed record Spec(
        string AtlasPath,
        string SkeletonPath,
        string IdleAnimation,
        string AttackAnimation,
        string HurtAnimation,
        // 为 null 时死亡不播动画（保持当前姿势），怪物也不设 DeathAnimLengthOverride，原版立即溶解
        string? DeathAnimation,
        float DefaultMix,
        // 混乱时定格的受击时间点（秒）
        float HurtHoldSeconds,
        GhostSpec? Ghosts,
        // 其他触发（例如 "Guard"、"Cast"）对应的动画：播一次再回待机；没列出的触发返回 false
        IReadOnlyDictionary<string, string>? ExtraTriggers = null);

    internal sealed record GhostSpec(float LagSeconds, float[] Alpha, float FadeInStart, float FadeInEnd, float FadeOutStart, float FadeOutEnd);

    private const int ManualUpdateMode = 2;

    private Spec _spec = null!;
    private Node2D _main = null!;
    private Node2D[] _ghosts = [];
    private float[] _ghostTimes = [];
    private bool _started;
    private bool _holdingHurt;

    internal static RuntimeSpineBody? TryCreate(Sprite2D anchor, Spec spec)
    {
        try
        {
            if (!ClassDB.ClassExists("SpineSprite"))
            {
                return null;
            }

            GodotObject? data = LoadData(spec, spec.DefaultMix);
            if (data == null)
            {
                return null;
            }

            var body = new RuntimeSpineBody { Name = "RuntimeSpineBody", _spec = spec };
            Rect2 rect = anchor.GetRect();
            Vector2 bottomCenter = new(rect.Position.X + rect.Size.X * 0.5f, rect.End.Y);
            Vector2 scale = anchor.Scale * new Vector2(anchor.FlipH ? -1f : 1f, 1f);
            body.Position = anchor.Position + bottomCenter * anchor.Scale;
            body.Scale = scale;
            body.ZIndex = anchor.ZIndex;
            body.ZAsRelative = anchor.ZAsRelative;

            if (spec.Ghosts is { } ghosts)
            {
                GodotObject? ghostData = LoadData(spec, 0f);
                if (ghostData != null)
                {
                    body._ghosts = new Node2D[ghosts.Alpha.Length];
                    body._ghostTimes = new float[ghosts.Alpha.Length];
                    // 越晚的残影越靠后
                    for (int i = ghosts.Alpha.Length - 1; i >= 0; i--)
                    {
                        Node2D ghost = NewSprite(ghostData);
                        ghost.Set("update_mode", ManualUpdateMode);
                        ghost.Visible = false;
                        body.AddChild(ghost);
                        body._ghosts[i] = ghost;
                    }
                }
            }

            body._main = NewSprite(data);
            body.AddChild(body._main);
            anchor.GetParent().AddChild(body);
            anchor.GetParent().MoveChild(body, anchor.GetIndex() + 1);
            return body;
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure("RuntimeSpineBody.Create", exception);
            return null;
        }
    }

    private static GodotObject? LoadData(Spec spec, float mix)
    {
        GodotObject atlas = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
        GodotObject file = ClassDB.Instantiate("SpineSkeletonFileResource").AsGodotObject();
        var atlasError = (Error)atlas.Call("load_from_atlas_file", spec.AtlasPath).AsInt32();
        var fileError = (Error)file.Call("load_from_file", spec.SkeletonPath).AsInt32();
        GodotObject data = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
        data.Set("atlas_res", atlas);
        data.Set("skeleton_file_res", file);
        data.Set("default_mix", mix);
        if (atlasError != Error.Ok || fileError != Error.Ok || !data.Call("is_skeleton_data_loaded").AsBool())
        {
            LorLog.PatchFailure(
                "RuntimeSpineBody.Load",
                new InvalidOperationException($"atlas={atlasError} skeleton={fileError} path={spec.SkeletonPath}"));
            return null;
        }

        return data;
    }

    private static Node2D NewSprite(GodotObject data)
    {
        var sprite = (Node2D)ClassDB.Instantiate("SpineSprite").AsGodotObject();
        sprite.Set("skeleton_data_res", data);
        return sprite;
    }

    private GodotObject State(Node2D sprite) => sprite.Call("get_animation_state").AsGodotObject();

    private GodotObject? Current(Node2D sprite) => State(sprite).Call("get_current", 0).AsGodotObject();

    private string? CurrentAnimation(Node2D sprite) =>
        Current(sprite)?.Call("get_animation").AsGodotObject()?.Call("get_name").AsString();

    public override void _Process(double delta)
    {
        if (!_started)
        {
            _started = true;
            PlayLoop(_spec.IdleAnimation);
        }

        SyncGhosts();
    }

    /// <summary>按触发播放；不认识的触发返回 false，交给原来的处理。</summary>
    internal bool Play(string trigger, bool holdHurtPose)
    {
        _started = true;
        switch (trigger)
        {
            case "Idle":
                if (holdHurtPose)
                {
                    HoldHurt();
                }
                else
                {
                    PlayLoop(_spec.IdleAnimation);
                }

                return true;
            case "Hit":
                if (holdHurtPose)
                {
                    HoldHurt();
                }
                else
                {
                    PlayOnce(_spec.HurtAnimation, thenIdle: true);
                }

                return true;
            case "Attack":
                PlayAttack();
                return true;
            case "Dead":
                if (_spec.DeathAnimation is { } death)
                {
                    PlayOnce(death, thenIdle: false);
                }

                HideGhosts();
                return true;
            default:
                if (_spec.ExtraTriggers?.TryGetValue(trigger, out string? animation) != true)
                {
                    return false;
                }

                if (holdHurtPose)
                {
                    HoldHurt();
                }
                else if (_holdingHurt || CurrentAnimation(_main) != animation)
                {
                    // 多段招式每段都会发同一个触发，和 Attack 一样只播一次
                    PlayOnce(animation!, thenIdle: true);
                    HideGhosts();
                }

                return true;
        }
    }

    /// <summary>混乱开始或结束时由外观调用：开始时定格受击姿势，结束时回到待机。</summary>
    internal void SyncHoldHurt(bool hold)
    {
        if (hold)
        {
            HoldHurt();
        }
        else if (_holdingHurt)
        {
            PlayLoop(_spec.IdleAnimation);
        }
    }

    private void PlayLoop(string animation)
    {
        _holdingHurt = false;
        OffsetLoop(State(_main).Call("set_animation", animation, true, 0).AsGodotObject());
    }

    // 照原版 CreatureAnimator.OffsetLoopingAnimation：循环动画随机 0.9–1.1 倍速、从随机时间点开始，同屏几只不同步。
    // 只是本机表现，用非同步的随机数。
    private static void OffsetLoop(GodotObject? entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.Call("set_time_scale", 0.9f + 0.2f * Random.Shared.NextSingle());
        float end = entry.Call("get_animation_end").AsSingle();
        if (end > 0f)
        {
            entry.Call("set_track_time", end * Random.Shared.NextSingle());
        }
    }

    private void PlayOnce(string animation, bool thenIdle)
    {
        _holdingHurt = false;
        GodotObject state = State(_main);
        state.Call("set_animation", animation, false, 0);
        if (thenIdle)
        {
            OffsetLoop(state.Call("add_animation", _spec.IdleAnimation, 0f, true, 0).AsGodotObject());
        }
    }

    private void HoldHurt()
    {
        if (_holdingHurt)
        {
            return;
        }

        _holdingHurt = true;
        GodotObject entry = State(_main).Call("set_animation", _spec.HurtAnimation, false, 0).AsGodotObject();
        entry.Call("set_track_time", _spec.HurtHoldSeconds);
        entry.Call("set_time_scale", 0f);
        HideGhosts();
    }

    // 一个多段攻击招式只播一次攻击动画：每段伤害都会发 "Attack" 触发，动画还在播时后面的触发直接忽略，
    // 各段伤害的等待由怪物按动画里的命中时刻安排。
    private void PlayAttack()
    {
        if (!_holdingHurt && CurrentAnimation(_main) == _spec.AttackAnimation)
        {
            return;
        }

        // 快速模式不改动画速度：原版也只缩短命令间的等待（Cmd.CustomScaledWait），命中帧靠前的动画因此照常对得上。
        _holdingHurt = false;
        GodotObject state = State(_main);
        state.Call("set_animation", _spec.AttackAnimation, false, 0);
        OffsetLoop(state.Call("add_animation", _spec.IdleAnimation, 0f, true, 0).AsGodotObject());

        for (int i = 0; i < _ghosts.Length; i++)
        {
            State(_ghosts[i]).Call("set_animation", _spec.AttackAnimation, false, 0);
            _ghosts[i].Call("update_skeleton", 0f);
            _ghostTimes[i] = 0f;
        }
    }

    private void SyncGhosts()
    {
        if (_ghosts.Length == 0 || _spec.Ghosts is not { } spec)
        {
            return;
        }

        GodotObject? current = Current(_main);
        if (current == null || _holdingHurt || CurrentAnimation(_main) != _spec.AttackAnimation)
        {
            HideGhosts();
            return;
        }

        float t = current.Call("get_track_time").AsSingle();
        float weight = Smooth((t - spec.FadeInStart) / (spec.FadeInEnd - spec.FadeInStart))
                       * (1f - Smooth((t - spec.FadeOutStart) / (spec.FadeOutEnd - spec.FadeOutStart)));
        for (int i = 0; i < _ghosts.Length; i++)
        {
            float lagged = Math.Max(0f, t - spec.LagSeconds * (i + 1));
            if (lagged > _ghostTimes[i])
            {
                _ghosts[i].Call("update_skeleton", lagged - _ghostTimes[i]);
                _ghostTimes[i] = lagged;
            }

            _ghosts[i].Visible = weight > 0.001f;
            _ghosts[i].Modulate = new Color(1f, 1f, 1f, spec.Alpha[i] * weight);
        }
    }

    private void HideGhosts()
    {
        foreach (Node2D ghost in _ghosts)
        {
            ghost.Visible = false;
        }
    }

    private static float Smooth(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }
}
