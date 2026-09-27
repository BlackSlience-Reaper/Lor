using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.features.intentgraph;

internal static class MonsterIntentGraphRuntimeCache
{
    private sealed class CachedGraph
    {
        public required IntentGraphRenderModel Render { get; init; }

        public required string MonsterName { get; init; }
    }

    private static readonly ConditionalWeakTable<MonsterModel, CachedGraph> GeneratedGraphs = new();

    public static void Warmup(Creature creature)
    {
        MonsterModel? monster = creature.Monster;
        if (monster == null)
        {
            return;
        }

        if (!MonsterStateMachineIntentGraphFeature.TryBuild(creature, out IntentGraphRenderModel render, out string monsterName))
        {
            return;
        }

        Store(monster, render, monsterName);
    }

    public static bool TryGet(Creature creature, out IntentGraphRenderModel render, out string monsterName)
    {
        render = new IntentGraphRenderModel();
        monsterName = string.Empty;

        MonsterModel? monster = creature.Monster;
        if (monster == null || !GeneratedGraphs.TryGetValue(monster, out CachedGraph? cached))
        {
            return false;
        }

        render = CloneRender(cached.Render);
        monsterName = cached.MonsterName;
        string? nextMoveId = monster.NextMove?.Id;
        for (int i = 0; i < render.Moves.Count; i++)
        {
            IntentGraphMoveNode move = render.Moves[i];
            move.IsCurrentMove = !string.IsNullOrWhiteSpace(nextMoveId)
                                 && string.Equals(move.MoveId, nextMoveId, StringComparison.Ordinal);
        }

        return true;
    }

    public static void Store(MonsterModel monster, IntentGraphRenderModel render, string monsterName)
    {
        GeneratedGraphs.Remove(monster);
        GeneratedGraphs.Add(monster, new CachedGraph
        {
            Render = CloneRender(render),
            MonsterName = monsterName
        });
    }

    private static IntentGraphRenderModel CloneRender(IntentGraphRenderModel source)
    {
        IntentGraphRenderModel copy = new IntentGraphRenderModel
        {
            WidthUnits = source.WidthUnits,
            HeightUnits = source.HeightUnits
        };

        foreach (IntentGraphMoveNode move in source.Moves)
        {
            List<IntentGraphIntentIcon> icons = new List<IntentGraphIntentIcon>(move.Intents.Count);
            for (int i = 0; i < move.Intents.Count; i++)
            {
                icons.Add(move.Intents[i]);
            }

            copy.Moves.Add(new IntentGraphMoveNode
            {
                MoveId = move.MoveId,
                PositionUnits = move.PositionUnits,
                Intents = icons,
                IsCurrentMove = move.IsCurrentMove
            });
        }

        foreach (IntentGraphArrowPath arrow in source.Arrows)
        {
            List<Vector2> points = new List<Vector2>(arrow.PointsUnits.Count);
            for (int i = 0; i < arrow.PointsUnits.Count; i++)
            {
                points.Add(arrow.PointsUnits[i]);
            }

            copy.Arrows.Add(new IntentGraphArrowPath { PointsUnits = points });
        }

        foreach (IntentGraphLabelNode label in source.Labels)
        {
            copy.Labels.Add(label);
        }

        foreach (IntentGraphGroupNode group in source.Groups)
        {
            copy.Groups.Add(group);
        }

        return copy;
    }
}
