using System;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Literature;

[MonsterVisual(typeof(LiteratureFloorTodaysExpressionBoss), ScenePath = LiteratureFloorTodaysExpressionCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorTodaysExpressionCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/literature_floor_todays_expression_boss.tscn";

    internal static IReadOnlyList<string> FaceTexturePaths { get; } =
    [
        "res://images/vfx/literature_floor_liberation/todays_expression/face_1.png",
        "res://images/vfx/literature_floor_liberation/todays_expression/face_2.png",
        "res://images/vfx/literature_floor_liberation/todays_expression/face_3.png",
        "res://images/vfx/literature_floor_liberation/todays_expression/face_4.png",
        "res://images/vfx/literature_floor_liberation/todays_expression/face_5.png"
    ];

    private Sprite2D? _expressionFace;
    private Tween? _expressionTween;
    private int _currentExpression;

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorTodaysExpressionAnimationContract.Library;

    public override void _Ready()
    {
        base._Ready();
        _expressionFace = GetNodeOrNull<Sprite2D>("%ExpressionFace");

        if (GetParent() is NCreature node
            && node.Entity.Monster
                is LiteratureFloorTodaysExpressionBoss expression)
        {
            _currentExpression = expression.CurrentExpression;
        }

        if (_currentExpression is >= 1 and <= 5)
        {
            SetExpression(_currentExpression, animate: false);
        }
    }

    public override void _ExitTree()
    {
        if (_expressionTween != null && _expressionTween.IsValid())
        {
            _expressionTween.Kill();
        }

        _expressionTween = null;
        _expressionFace = null;
        base._ExitTree();
    }

    internal void SetExpression(int expression, bool animate)
    {
        if (expression is < 1 or > 5
            || _expressionFace == null
            || !IsInstanceValid(_expressionFace))
        {
            _currentExpression = expression;
            return;
        }

        _currentExpression = expression;
        Texture2D? texture = ResourceLoader.Load<Texture2D>(
            FaceTexturePaths[expression - 1]);
        if (texture == null)
        {
            return;
        }

        if (_expressionTween != null && _expressionTween.IsValid())
        {
            _expressionTween.Kill();
        }

        _expressionFace.Texture = texture;
        _expressionFace.Visible = true;
        _expressionFace.Scale = Vector2.One * (animate ? 0.64f : 0.78f);
        _expressionFace.Modulate = new Color(1f, 1f, 1f, animate ? 0.2f : 0.72f);
        if (!animate)
        {
            return;
        }

        _expressionTween = CreateTween();
        _expressionTween
            .SetParallel()
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _expressionTween.TweenProperty(
            _expressionFace,
            "scale",
            Vector2.One * 0.86f,
            0.22f);
        _expressionTween.TweenProperty(
            _expressionFace,
            "modulate:a",
            1f,
            0.12f);
        _expressionTween.Chain().TweenProperty(
            _expressionFace,
            "scale",
            Vector2.One * 0.78f,
            0.22f);
        _expressionTween.Parallel().TweenProperty(
            _expressionFace,
            "modulate:a",
            0.72f,
            0.22f);
    }
}

internal static class LiteratureFloorTodaysExpressionAnimationContract
{
    internal const string Library = "todays_expression";
    internal const float GuardDurationSeconds = 1.24f;
    internal const float AttackDurationSeconds = 1.30f;
    internal const float AngryDurationSeconds = 2.10f;
    internal const float WaveringFeelingsDurationSeconds = 2.50f;
    internal const float CastDurationSeconds = 0.90f;
    internal const float HitDurationSeconds = 0.36f;

    internal static IReadOnlyList<float> AttackHitFrameTimesSeconds { get; } = [0.64f];

    internal static IReadOnlyList<float> AngryHitFrameTimesSeconds { get; } = [0.44f, 1.04f, 1.64f];

    internal static IReadOnlyList<float>
        WaveringFeelingsHitFrameTimesSeconds { get; } = [1.44f];

    internal static IReadOnlyList<string> Animations { get; } =
    [
        "Idle",
        "Guard",
        "Attack",
        "Angry",
        "WaveringFeelings",
        "Cast",
        "Hit"
    ];

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "Guard" => GuardDurationSeconds,
            "Attack" => AttackDurationSeconds,
            "Angry" => AngryDurationSeconds,
            "WaveringFeelings" => WaveringFeelingsDurationSeconds,
            "Cast" => CastDurationSeconds,
            "Hit" => HitDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle Today's Expression actions have a duration.")
        };
}
