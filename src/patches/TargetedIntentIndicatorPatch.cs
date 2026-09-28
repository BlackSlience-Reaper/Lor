using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.combat;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches.QueenOfHatred;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.patches;

internal static class TargetedIntentIndicatorPatch
{
    private const string OverlayNodeName = "LibraryOfRuinaTargetedIntentLines";

    internal static void OnUpdateIntent(NCreature __instance)
    {
        RemoveOverlay(__instance);
    }

    internal static void Show(
        NIntent sourceIntent,
        AbstractIntent intent,
        IEnumerable<Creature> ordinaryTargets,
        Creature owner)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (ownerNode == null
            || (intent is not AttackIntent
                && intent is not IIntentTargetLineProvider)
            // 按怪物模型的定义程序集限定来源，本模组的敌方和友方怪物均保留指示线。
            || owner.Monster?.GetType().Assembly != typeof(TargetedIntentIndicatorPatch).Assembly)
        {
            if (ownerNode != null)
            {
                RemoveOverlay(ownerNode);
            }

            return;
        }

        IReadOnlyList<Creature> targets = ResolveTargets(intent, owner, ordinaryTargets);
        if (targets.Count == 0)
        {
            RemoveOverlay(ownerNode);
            return;
        }

        GetOrCreateOverlay(ownerNode).Configure(
            ownerNode,
            sourceIntent,
            targets,
            AllyTurnRegistry.IsAllyCreature(owner));
    }

    internal static void Hide(Creature owner)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(owner) is { } ownerNode)
        {
            RemoveOverlay(ownerNode);
        }
    }

    // 指示线与我方受伤预览共用同一套目标解析，保证预览只统计实际命中本地玩家的攻击。
    internal static IReadOnlyList<Creature> ResolveTargets(
        AbstractIntent intent,
        Creature owner,
        IEnumerable<Creature> ordinaryTargets)
    {
        IReadOnlyList<Creature> fallbackTargets = AllyTurnRegistry.IsAllyCreature(owner)
            ? ResolveAllyFallbackTargets(owner)
            : ordinaryTargets
                .Where(target => target is { IsAlive: true } && target != owner)
                .Distinct()
                .ToArray();

        IEnumerable<Creature> resolvedTargets = intent switch
        {
            IIntentTargetLineProvider provider => provider
                .GetIntentTargetLineTargets(owner, fallbackTargets)
                .Select(static target => target.Target),
            ITargetedIntentIndicator => TargetedMonsterAttackHelper.GetTargetList(owner, fallbackTargets),
            _ => fallbackTargets
        };

        return resolvedTargets
            .Where(target => target is { IsAlive: true }
                && target != owner
                && CanPointAtTarget(target, owner))
            .Distinct()
            .ToArray();
    }

    internal static bool CanPointAtTarget(
        Creature target,
        Creature source)
    {
        return UntargetableInteractionFilter.CanBeHit(target, source);
    }

    private static IReadOnlyList<Creature> ResolveAllyFallbackTargets(Creature owner)
    {
        return owner.CombatState?.Enemies
            .Where(target => target is { IsAlive: true }
                && target != owner
                && !AllyTurnRegistry.IsAllyCreature(target))
            .Distinct()
            .ToArray()
            ?? Array.Empty<Creature>();
    }

    private static NTargetedIntentLines GetOrCreateOverlay(NCreature creatureNode)
    {
        if (creatureNode.GetNodeOrNull<NTargetedIntentLines>(OverlayNodeName) is { } existing)
        {
            return existing;
        }

        var overlay = new NTargetedIntentLines
        {
            Name = OverlayNodeName
        };
        creatureNode.AddChildSafely(overlay);
        return overlay;
    }

    private static void RemoveOverlay(NCreature creatureNode)
    {
        if (creatureNode.GetNodeOrNull<NTargetedIntentLines>(OverlayNodeName) is not { } overlay)
        {
            return;
        }

        creatureNode.RemoveChildSafely(overlay);
        overlay.QueueFreeSafely();
    }
}

[HarmonyPatch(typeof(NIntent), "OnHovered")]
internal static class TargetedIntentHoverPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        NIntent __instance,
        AbstractIntent ____intent,
        IEnumerable<Creature> ____targets,
        Creature ____owner)
    {
        try
        {
            TargetedIntentIndicatorPatch.Show(__instance, ____intent, ____targets, ____owner);
        }
        catch (Exception e)
        {
            Log.Warn("[LibraryOfRuina.TargetedIntent] Failed to show target arrows: " + e);
        }
    }
}

[HarmonyPatch(typeof(NIntent), "OnUnhovered")]
internal static class TargetedIntentUnhoverPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature ____owner)
    {
        try
        {
            TargetedIntentIndicatorPatch.Hide(____owner);
        }
        catch (Exception e)
        {
            Log.Warn("[LibraryOfRuina.TargetedIntent] Failed to hide target arrows: " + e);
        }
    }
}

internal partial class NTargetedIntentLines : Node2D
{
    private const int SegmentCount = 50;
    private const string ArrowTexturePath = "res://images/vfx/targeted_intent/arrow.png";
    private const string ArrowStartTexturePath = "res://images/vfx/targeted_intent/arrowstart.png";

    private static readonly Color EnemySoftColor = new(1f, 0.10f, 0.18f, 0.88f);
    private static readonly Color EnemyStrongColor = new(1f, 0.42f, 0.36f);
    private static readonly Color AllySoftColor = new(0.35f, 0.72f, 1f, 0.78f);
    private static readonly Color AllyStrongColor = new(0.62f, 0.90f, 1f, 0.96f);

    private static Texture2D? _arrowTexture;
    private static Texture2D? _arrowStartTexture;

    private readonly List<LinePair> _lines = [];
    private NCreature? _owner;
    private Control? _source;
    private IReadOnlyList<Creature> _targets = Array.Empty<Creature>();
    private bool _isAlly;

    public NTargetedIntentLines()
    {
        TopLevel = true;
        ZIndex = 120;
        GlobalPosition = Vector2.Zero;
    }

    public void Configure(
        NCreature owner,
        NIntent sourceIntent,
        IReadOnlyList<Creature> targets,
        bool isAlly)
    {
        _owner = owner;
        _source = sourceIntent.GetNodeOrNull<Control>("%IntentHolder") ?? sourceIntent;
        _targets = targets;
        _isAlly = isAlly;
        EnsureLineCount(targets.Count);
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!TryGetSourceCenter(out Vector2 from, out float alpha))
        {
            Visible = false;
            return;
        }

        float pulse = (Mathf.Sin(Time.GetTicksMsec() * 0.001f * Mathf.Tau) + 1f) * 0.5f;
        Color lineColor = _isAlly
            ? LerpColor(AllySoftColor, AllyStrongColor, pulse)
            : LerpColor(EnemySoftColor, EnemyStrongColor, pulse);
        lineColor.A *= alpha;

        int visibleLineCount = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            LinePair pair = _lines[i];
            if (i >= _targets.Count || !TryGetTargetCenter(_targets[i], out Vector2 to))
            {
                pair.SetVisible(false);
                continue;
            }

            pair.SetVisible(true);
            UpdateCurve(pair, from, to, i);
            pair.SetColors(lineColor);
            visibleLineCount++;
        }

        Visible = visibleLineCount > 0;
    }

    private bool TryGetSourceCenter(out Vector2 center, out float alpha)
    {
        center = Vector2.Zero;
        alpha = 0f;

        if (NCombatUi.IsDebugHidingIntent
            || _owner == null
            || _source == null
            || !IsInstanceValid(_owner)
            || !IsInstanceValid(_source)
            || _owner.Entity.IsDead
            || !_source.IsVisibleInTree())
        {
            return false;
        }

        Rect2 sourceRect = _source.GetGlobalRect();
        center = sourceRect.Position + sourceRect.Size * 0.5f;
        alpha = _owner.IntentContainer.Modulate.A;
        return alpha > 0.01f;
    }

    private bool TryGetTargetCenter(Creature target, out Vector2 center)
    {
        center = Vector2.Zero;
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (targetNode == null
            || _owner == null
            || !IsInstanceValid(targetNode)
            || !IsInstanceValid(targetNode.Hitbox)
            || !target.IsAlive
            || !TargetedIntentIndicatorPatch.CanPointAtTarget(
                target,
                _owner.Entity))
        {
            return false;
        }

        Rect2 targetRect = targetNode.Hitbox.GetGlobalRect();
        center = targetRect.Position + targetRect.Size * 0.5f;
        return true;
    }

    private void EnsureLineCount(int count)
    {
        while (_lines.Count < count)
        {
            LinePair pair = LinePair.Create();
            AddChild(pair.Dashes);
            AddChild(pair.StartMarker);
            AddChild(pair.ArrowHead);
            _lines.Add(pair);
        }

        for (int i = count; i < _lines.Count; i++)
        {
            _lines[i].SetVisible(false);
        }
    }

    private static void UpdateCurve(LinePair pair, Vector2 from, Vector2 to, int targetIndex)
    {
        float distance = from.DistanceTo(to);
        float curveHeight = Mathf.Clamp(distance * 0.175f + targetIndex * 22f, 36f, 220f);
        Vector2 control = (from + to) * 0.5f + Vector2.Up * curveHeight;

        for (int i = 0; i <= SegmentCount; i++)
        {
            Vector2 position = MathHelper.BezierCurve(from, to, control, i / (float)SegmentCount);
            pair.Dashes.SetCurvePoint(i, position);
        }

        pair.Dashes.Commit();
        Vector2 previous = pair.Dashes.GetCurvePoint(SegmentCount - 1);
        pair.StartMarker.GlobalPosition = from;
        pair.ArrowHead.GlobalPosition = to;
        pair.ArrowHead.GlobalRotation = (to - previous).Angle() + Mathf.Pi * 0.5f;
    }

    private static Color LerpColor(Color from, Color to, float weight)
    {
        return new Color(
            Mathf.Lerp(from.R, to.R, weight),
            Mathf.Lerp(from.G, to.G, weight),
            Mathf.Lerp(from.B, to.B, weight),
            Mathf.Lerp(from.A, to.A, weight));
    }

    private static Texture2D? LoadTexture(ref Texture2D? texture, string path)
    {
        texture ??= ResourceLoader.Load<Texture2D>(path);
        return texture;
    }

    private readonly record struct LinePair(
        NTargetArrowDashLine Dashes,
        Sprite2D StartMarker,
        Sprite2D ArrowHead)
    {
        public static LinePair Create()
        {
            var dashes = new NTargetArrowDashLine
            {
                Name = "LineDashes"
            };
            var startMarker = new Sprite2D
            {
                Name = "ArrowStart",
                Texture = LoadTexture(ref _arrowStartTexture, ArrowStartTexturePath),
                Scale = Vector2.One * 0.16f,
                TextureFilter = TextureFilterEnum.Linear
            };
            var arrowHead = new Sprite2D
            {
                Name = "ArrowHead",
                Texture = LoadTexture(ref _arrowTexture, ArrowTexturePath),
                Scale = Vector2.One * 0.24f,
                TextureFilter = TextureFilterEnum.Linear
            };

            return new LinePair(dashes, startMarker, arrowHead);
        }

        public void SetVisible(bool visible)
        {
            Dashes.Visible = visible;
            StartMarker.Visible = visible;
            ArrowHead.Visible = visible;
        }

        public void SetColors(Color lineColor)
        {
            Dashes.SetLineColor(lineColor);
            StartMarker.Modulate = new Color(lineColor.R, lineColor.G, lineColor.B, lineColor.A * 0.72f);
            ArrowHead.Modulate = lineColor;
        }
    }
}

internal partial class NTargetArrowDashLine : Node2D
{
    private const int PointCount = 51;
    private const float DashLength = 92f;
    private const float GapLength = 28f;
    private const float DashSampleStep = 8f;
    private const float FlowCyclesPerSecond = 1.5f;
    private const float GlowWidth = 14f;
    private const float FrontWidth = 8f;

    private readonly Vector2[] _curvePoints = new Vector2[PointCount];
    private readonly float[] _cumulativeLengths = new float[PointCount];
    private Color _lineColor = Colors.Red;
    private float _phase;

    public override void _Ready()
    {
        SetProcess(true);
    }

    public void SetCurvePoint(int index, Vector2 position)
    {
        _curvePoints[index] = position;
    }

    public Vector2 GetCurvePoint(int index) => _curvePoints[index];

    public void Commit()
    {
        QueueRedraw();
    }

    public void SetLineColor(Color color)
    {
        _lineColor = color;
    }

    public override void _Process(double delta)
    {
        float patternLength = DashLength + GapLength;
        _phase = (_phase + (float)delta * patternLength * FlowCyclesPerSecond) % patternLength;
        QueueRedraw();
    }

    public override void _Draw()
    {
        _cumulativeLengths[0] = 0f;
        for (int i = 1; i < PointCount; i++)
        {
            _cumulativeLengths[i] = _cumulativeLengths[i - 1]
                + _curvePoints[i - 1].DistanceTo(_curvePoints[i]);
        }

        float totalLength = _cumulativeLengths[^1];
        if (totalLength <= 0.01f)
        {
            return;
        }

        float patternLength = DashLength + GapLength;
        for (float dashStart = _phase - patternLength; dashStart < totalLength; dashStart += patternLength)
        {
            float visibleStart = Math.Max(0f, dashStart);
            float visibleEnd = Math.Min(totalLength, dashStart + DashLength);
            if (visibleEnd - visibleStart <= 1f)
            {
                continue;
            }

            Vector2 from = SampleAtDistance(visibleStart);
            Vector2 to = SampleAtDistance(visibleEnd);
            float centerRatio = (visibleStart + visibleEnd) * 0.5f / totalLength;
            float fade = Mathf.Lerp(0.15f, 1f, Mathf.Clamp(centerRatio / 0.20f, 0f, 1f));
            Color glowColor = new(
                _lineColor.R,
                _lineColor.G,
                _lineColor.B,
                _lineColor.A * 0.32f * fade);
            Color frontColor = new(_lineColor.R, _lineColor.G, _lineColor.B, _lineColor.A * fade);
            Vector2[] dashPoints = SampleRange(visibleStart, visibleEnd);
            DrawPolyline(dashPoints, glowColor, GlowWidth, true);
            DrawPolyline(dashPoints, frontColor, FrontWidth, true);
        }
    }

    private Vector2[] SampleRange(float startDistance, float endDistance)
    {
        int pointCount = Math.Max(
            2,
            Mathf.CeilToInt((endDistance - startDistance) / DashSampleStep) + 1);
        var points = new Vector2[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            float weight = i / (float)(pointCount - 1);
            points[i] = SampleAtDistance(Mathf.Lerp(startDistance, endDistance, weight));
        }

        return points;
    }

    private Vector2 SampleAtDistance(float distance)
    {
        if (distance <= 0f)
        {
            return _curvePoints[0];
        }

        float totalLength = _cumulativeLengths[^1];
        if (distance >= totalLength)
        {
            return _curvePoints[^1];
        }

        int segment = 1;
        while (segment < PointCount && _cumulativeLengths[segment] < distance)
        {
            segment++;
        }

        float segmentStart = _cumulativeLengths[segment - 1];
        float segmentLength = _cumulativeLengths[segment] - segmentStart;
        float weight = segmentLength <= 0.001f ? 0f : (distance - segmentStart) / segmentLength;
        return _curvePoints[segment - 1].Lerp(_curvePoints[segment], weight);
    }
}
