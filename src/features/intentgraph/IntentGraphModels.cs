using System;
using Godot;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.features.intentgraph;

internal sealed class IntentGraphRenderModel
{
    public float WidthUnits { get; set; }

    public float HeightUnits { get; set; }

    public List<IntentGraphMoveNode> Moves { get; } = new List<IntentGraphMoveNode>();

    public List<IntentGraphArrowPath> Arrows { get; } = new List<IntentGraphArrowPath>();

    public List<IntentGraphLabelNode> Labels { get; } = new List<IntentGraphLabelNode>();

    public List<IntentGraphGroupNode> Groups { get; } = new List<IntentGraphGroupNode>();
}

internal sealed class IntentGraphMoveNode
{
    public required string MoveId { get; set; }

    public required Vector2 PositionUnits { get; set; }

    public required IReadOnlyList<IntentGraphIntentIcon> Intents { get; set; }

    public bool IsCurrentMove { get; set; }
}

internal readonly record struct IntentGraphIntentIcon(Texture2D? Texture, string? ValueText, IntentType IntentType);

internal sealed class IntentGraphArrowPath
{
    public required IReadOnlyList<Vector2> PointsUnits { get; init; }
}

internal readonly record struct IntentGraphLabelNode(Vector2 PositionUnits, string Text);

internal readonly record struct IntentGraphGroupNode(Rect2 RectUnits);

internal readonly record struct IntentGraphTransition(string FromMoveId, string ToMoveId, string? LabelText);

internal sealed class IntentGraphMonsterConfig
{
    public List<string> SecondaryInitialStates { get; } = new List<string>();

    public Dictionary<string, List<IntentGraphMoveReplacement>> MoveReplacements { get; } =
        new Dictionary<string, List<IntentGraphMoveReplacement>>(StringComparer.Ordinal);

    public IntentGraphLayoutDefinition? Graph { get; set; }

    public IntentGraphLayoutDefinition? GraphPatch { get; set; }

    public IntentGraphMonsterConfig Clone()
    {
        IntentGraphMonsterConfig copy = new IntentGraphMonsterConfig();
        copy.SecondaryInitialStates.AddRange(SecondaryInitialStates);

        foreach ((string moveId, List<IntentGraphMoveReplacement> replacements) in MoveReplacements)
        {
            copy.MoveReplacements[moveId] = replacements.ConvertAll(static replacement => replacement.Clone());
        }

        copy.Graph = Graph?.Clone();
        copy.GraphPatch = GraphPatch?.Clone();
        return copy;
    }

    public void MergeFrom(IntentGraphMonsterConfig other)
    {
        if (other.SecondaryInitialStates.Count > 0)
        {
            SecondaryInitialStates.Clear();
            SecondaryInitialStates.AddRange(other.SecondaryInitialStates);
        }

        foreach ((string moveId, List<IntentGraphMoveReplacement> replacements) in other.MoveReplacements)
        {
            MoveReplacements[moveId] = replacements.ConvertAll(static replacement => replacement.Clone());
        }

        if (other.Graph != null)
        {
            Graph = other.Graph.Clone();
        }

        if (other.GraphPatch != null)
        {
            GraphPatch = other.GraphPatch.Clone();
        }
    }
}

internal sealed class IntentGraphMoveReplacement
{
    public string? TimesText { get; set; }

    public IntentGraphMoveReplacement Clone()
    {
        return new IntentGraphMoveReplacement
        {
            TimesText = TimesText
        };
    }
}

internal sealed class IntentGraphLayoutDefinition
{
    public float? Width { get; set; }

    public float? Height { get; set; }

    public List<IntentGraphMoveLayout>? Moves { get; set; }

    public List<IntentGraphArrowLayout>? Arrows { get; set; }

    public List<IntentGraphLabelLayout>? Labels { get; set; }

    public List<IntentGraphGroupLayout>? IconGroups { get; set; }

    public IntentGraphLayoutDefinition Clone()
    {
        return new IntentGraphLayoutDefinition
        {
            Width = Width,
            Height = Height,
            Moves = Moves?.ConvertAll(static move => move.Clone()),
            Arrows = Arrows?.ConvertAll(static arrow => arrow.Clone()),
            Labels = Labels?.ConvertAll(static label => label.Clone()),
            IconGroups = IconGroups?.ConvertAll(static group => group.Clone())
        };
    }

    public void ApplyPatch(IntentGraphLayoutDefinition patch)
    {
        if (patch.Width.HasValue)
        {
            Width = patch.Width;
        }

        if (patch.Height.HasValue)
        {
            Height = patch.Height;
        }

        if (patch.Moves != null)
        {
            Moves = patch.Moves.ConvertAll(static move => move.Clone());
        }

        if (patch.Arrows != null)
        {
            Arrows = patch.Arrows.ConvertAll(static arrow => arrow.Clone());
        }

        if (patch.Labels != null)
        {
            Labels = patch.Labels.ConvertAll(static label => label.Clone());
        }

        if (patch.IconGroups != null)
        {
            IconGroups = patch.IconGroups.ConvertAll(static group => group.Clone());
        }
    }
}

internal sealed class IntentGraphMoveLayout
{
    public float X { get; set; }

    public float Y { get; set; }

    public required string Id { get; set; }

    public IntentGraphMoveLayout Clone()
    {
        return new IntentGraphMoveLayout
        {
            X = X,
            Y = Y,
            Id = Id
        };
    }
}

internal sealed class IntentGraphArrowLayout
{
    public List<float> Path { get; set; } = new List<float>();

    public IntentGraphArrowLayout Clone()
    {
        return new IntentGraphArrowLayout
        {
            Path = new List<float>(Path)
        };
    }
}

internal sealed class IntentGraphLabelLayout
{
    public float X { get; set; }

    public float Y { get; set; }

    public required string Text { get; set; }

    public IntentGraphLabelLayout Clone()
    {
        return new IntentGraphLabelLayout
        {
            X = X,
            Y = Y,
            Text = Text
        };
    }
}

internal sealed class IntentGraphGroupLayout
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public IntentGraphGroupLayout Clone()
    {
        return new IntentGraphGroupLayout
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height
        };
    }
}

