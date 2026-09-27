using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Godot;
using LibraryOfRuina.features.intentgraph;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.features;

internal static class MonsterStateMachineIntentGraphFeature
{
    private const int MaxDepth = 24;
    private const float Dx = 1.62f;
    private const float Dy = 1.1f;

    private static readonly PropertyInfo? ConditionalStatesProperty =
        typeof(ConditionalBranchState).GetProperty("States", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? ConditionalBranchIdField =
        typeof(ConditionalBranchState)
            .GetNestedType("ConditionalBranch", BindingFlags.NonPublic)?
            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly MethodInfo? ConditionalEvaluateStatesMethod =
        typeof(ConditionalBranchState).GetMethod("EvaluateStates", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? StateMachineInitialStateField =
        typeof(MonsterMoveStateMachine).GetField("_initialState", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly HashSet<string> IntentResolveFailureKeysLogged = new(StringComparer.Ordinal);

    private readonly record struct RandomBranchOption(
        MoveState Move,
        float Weight,
        RandomBranchState.StateWeight StateWeight,
        string? ConditionText);

    private readonly record struct ConditionalBranchOption(MoveState Move, string? ConditionText);

    public static bool TryBuild(Creature owner, out IntentGraphRenderModel render, out string monsterName)
    {
        render = new IntentGraphRenderModel();
        monsterName = string.Empty;

        MonsterModel? monster = owner.Monster;
        if (monster == null || owner.CombatState == null || monster.MoveStateMachine == null)
        {
            return false;
        }

        Dictionary<string, MoveState> moves = monster.MoveStateMachine.States.Values
            .OfType<MoveState>()
            .ToDictionary(static m => m.Id, static m => m, StringComparer.Ordinal);
        if (moves.Count == 0)
        {
            return false;
        }

        string monsterTypeName = monster.GetType().FullName ?? monster.GetType().Name;
        IntentGraphMonsterConfig? config = IntentGraphConfigRepository.GetConfigForMonster(monsterTypeName);

        List<string> roots = new List<string>();
        AddInitialRootMoves(monster.MoveStateMachine, moves, roots);
        string? nextMoveId = monster.NextMove?.Id;
        AddRootMoveId(roots, moves, nextMoveId);

        if (config != null)
        {
            foreach (string id in config.SecondaryInitialStates)
            {
                AddRootMoveId(roots, moves, id);
            }
        }

        if (roots.Count == 0)
        {
            roots.Add(moves.Keys.First());
        }

        Dictionary<string, int> depth = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.Ordinal);
        List<IntentGraphTransition> transitions = new List<IntentGraphTransition>();
        Queue<string> queue = new Queue<string>();

        int idx = 0;
        foreach (string root in roots)
        {
            if (depth.TryAdd(root, 0))
            {
                order[root] = idx++;
                queue.Enqueue(root);
            }
        }

        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!moves.TryGetValue(id, out MoveState? move))
            {
                continue;
            }

            int d = depth[id];
            if (d >= MaxDepth)
            {
                continue;
            }

            foreach (IntentGraphTransition t in ResolveTransitions(move, monster.MoveStateMachine, owner, monsterTypeName, moves))
            {
                transitions.Add(t);
                if (!depth.ContainsKey(t.ToMoveId))
                {
                    depth[t.ToMoveId] = d + 1;
                    order[t.ToMoveId] = idx++;
                    queue.Enqueue(t.ToMoveId);
                }
            }
        }

        if (depth.Count == 0)
        {
            return false;
        }

        Dictionary<string, Vector2> autoPos = GenerateAutoPositions(depth, order);
        IntentGraphLayoutDefinition autoLayout = BuildAutoLayout(autoPos, transitions);
        IntentGraphLayoutDefinition layout = config?.Graph?.Clone() ?? autoLayout.Clone();
        if (config?.GraphPatch != null)
        {
            layout.ApplyPatch(config.GraphPatch);
        }

        if (layout.Moves == null)
        {
            layout.Moves = autoLayout.Moves?.ConvertAll(static m => m.Clone()) ?? new List<IntentGraphMoveLayout>();
        }

        HashSet<string> known = new HashSet<string>(layout.Moves.Select(static m => m.Id), StringComparer.Ordinal);
        foreach ((string moveId, Vector2 p) in autoPos)
        {
            if (known.Contains(moveId))
            {
                continue;
            }

            layout.Moves.Add(new IntentGraphMoveLayout { Id = moveId, X = p.X, Y = p.Y });
        }

        Dictionary<string, Vector2> pos = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        foreach (IntentGraphMoveLayout m in layout.Moves)
        {
            if (moves.ContainsKey(m.Id))
            {
                pos[m.Id] = new Vector2(m.X, m.Y);
            }
        }

        foreach ((string id, Vector2 p) in autoPos)
        {
            if (moves.ContainsKey(id) && !pos.ContainsKey(id))
            {
                pos[id] = p;
            }
        }

        IReadOnlyList<Creature> targets = owner.CombatState.Players.Select(static p => p.Creature).ToList();
        foreach ((string id, Vector2 p) in pos)
        {
            MoveState move = moves[id];
            List<IntentGraphIntentIcon> icons = new List<IntentGraphIntentIcon>();
            for (int i = 0; i < move.Intents.Count; i++)
            {
                AbstractIntent intent = move.Intents[i];
                string intentTypeName = intent.GetType().FullName ?? intent.GetType().Name;
                string resolveKeyPrefix = monsterTypeName + "::" + move.Id + "::" + intentTypeName;

                string? text = null;
                try
                {
                    text = intent.GetIntentLabel(targets, owner).GetFormattedText();
                }
                catch (Exception exception)
                {
                    if (IntentResolveFailureKeysLogged.Add(resolveKeyPrefix + ":label"))
                    {
                        Log.Warn(
                            "[LibraryOfRuina.IntentGraph] Failed to resolve intent label for "
                            + resolveKeyPrefix + ". " + exception.Message);
                    }
                }

                text = ApplyMoveReplacement(config, move.Id, i, text);
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = null;
                }

                Texture2D? texture = null;
                try
                {
                    texture = intent.GetTexture(targets, owner);
                }
                catch (Exception exception)
                {
                    if (IntentResolveFailureKeysLogged.Add(resolveKeyPrefix + ":texture"))
                    {
                        Log.Warn(
                            "[LibraryOfRuina.IntentGraph] Failed to resolve intent texture for "
                            + resolveKeyPrefix + ". " + exception.Message);
                    }
                }

                icons.Add(new IntentGraphIntentIcon(texture, text, intent.IntentType));
            }

            render.Moves.Add(new IntentGraphMoveNode
            {
                MoveId = id,
                PositionUnits = p,
                Intents = icons,
                IsCurrentMove = !string.IsNullOrWhiteSpace(nextMoveId)
                                && string.Equals(id, nextMoveId, StringComparison.Ordinal)
            });
        }

        List<List<Vector2>> configuredPaths = ParseConfiguredPaths(layout);
        HashSet<int> usedConfiguredPaths = new HashSet<int>();
        foreach (IntentGraphTransition t in transitions)
        {
            if (!pos.TryGetValue(t.FromMoveId, out Vector2 from) || !pos.TryGetValue(t.ToMoveId, out Vector2 to))
            {
                continue;
            }

            IReadOnlyList<Vector2> path = MatchConfiguredPath(configuredPaths, usedConfiguredPaths, from, to)
                                          ?? CreateAutoArrowPoints(from, to);

            render.Arrows.Add(new IntentGraphArrowPath { PointsUnits = path.ToList() });

            if (!string.IsNullOrWhiteSpace(t.LabelText))
            {
                Vector2 mid = new Vector2((from.X + to.X) * 0.5f, (from.Y + to.Y) * 0.5f - 0.32f);
                render.Labels.Add(new IntentGraphLabelNode(mid, t.LabelText!));
            }
        }

        foreach (IntentGraphLabelLayout label in layout.Labels ?? Enumerable.Empty<IntentGraphLabelLayout>())
        {
            string text = ResolveInlineLabelText(label.Text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                render.Labels.Add(new IntentGraphLabelNode(new Vector2(label.X, label.Y), text));
            }
        }

        foreach (IntentGraphGroupLayout group in layout.IconGroups ?? Enumerable.Empty<IntentGraphGroupLayout>())
        {
            render.Groups.Add(new IntentGraphGroupNode(new Rect2(group.X, group.Y, group.Width, group.Height)));
        }

        foreach (IGrouping<string, IntentGraphTransition> bySource in transitions
                     .GroupBy(static t => t.FromMoveId)
                     .Where(static g => g.Count() > 1))
        {
            List<Vector2> targetsPos = bySource
                .Select(t => pos.TryGetValue(t.ToMoveId, out Vector2 p) ? (Vector2?)p : null)
                .Where(static p => p.HasValue)
                .Select(static p => p!.Value)
                .ToList();

            if (targetsPos.Count < 2)
            {
                continue;
            }

            float minX = targetsPos.Min(static p => p.X) - 0.55f;
            float minY = targetsPos.Min(static p => p.Y) - 0.52f;
            float maxX = targetsPos.Max(static p => p.X) + 0.55f;
            float maxY = targetsPos.Max(static p => p.Y) + 0.52f;
            render.Groups.Add(new IntentGraphGroupNode(new Rect2(minX, minY, maxX - minX, maxY - minY)));
        }

        NormalizeAndSize(render, layout);
        monsterName = monster.Title.GetFormattedText();
        return true;
    }

    public static void ReloadLocalization()
    {
        IntentGraphConfigRepository.ReloadLocalization();
    }

    private static IReadOnlyList<IntentGraphTransition> ResolveTransitions(
        MoveState move,
        MonsterMoveStateMachine stateMachine,
        Creature owner,
        string monsterTypeName,
        IReadOnlyDictionary<string, MoveState> moves)
    {
        List<IntentGraphTransition> result = new List<IntentGraphTransition>();
        MonsterState? follow = ResolveFollowUpState(move, stateMachine);
        if (follow == null)
        {
            return result;
        }

        if (follow is MoveState next)
        {
            result.Add(new IntentGraphTransition(move.Id, next.Id, null));
            return result;
        }

        if (follow is RandomBranchState random)
        {
            IReadOnlyList<RandomBranchOption> options = ResolveRandomOptions(random, stateMachine, monsterTypeName);
            bool suppressPercent = false;
            foreach (RandomBranchOption option in options)
            {
                List<string> parts = new List<string>();
                if (!suppressPercent)
                {
                    parts.Add(FormatPercent(option.Weight, options));
                }

                if (!string.IsNullOrWhiteSpace(option.ConditionText))
                {
                    parts.Add(option.ConditionText!);
                }

                string? extra = BuildRandomConstraintText(option.StateWeight);
                if (!string.IsNullOrWhiteSpace(extra))
                {
                    parts.Add(extra!);
                }

                if (parts.Count == 0)
                {
                    parts.Add(FormatPercent(option.Weight, options));
                }

                result.Add(new IntentGraphTransition(move.Id, option.Move.Id, string.Join(", ", parts)));
            }

            return result;
        }

        if (follow is ConditionalBranchState conditional)
        {
            foreach (ConditionalBranchOption option in ResolveConditionalOptions(conditional, owner, monsterTypeName, moves))
            {
                result.Add(new IntentGraphTransition(move.Id, option.Move.Id, option.ConditionText));
            }
        }

        return result;
    }

    private static MonsterState? ResolveFollowUpState(MoveState moveState, MonsterMoveStateMachine machine)
    {
        if (moveState.FollowUpState != null)
        {
            return moveState.FollowUpState;
        }

        string? id = moveState.FollowUpStateId;
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return machine.States.TryGetValue(id, out MonsterState? follow) ? follow : null;
    }

    private static IReadOnlyList<ConditionalBranchOption> ResolveConditionalOptions(
        ConditionalBranchState conditional,
        Creature owner,
        string monsterTypeName,
        IReadOnlyDictionary<string, MoveState> moves)
    {
        List<ConditionalBranchOption> options = new List<ConditionalBranchOption>();
        if (ConditionalStatesProperty?.GetValue(conditional) is not IEnumerable branches || ConditionalBranchIdField == null)
        {
            return options;
        }

        string? forcedBranchId = null;
        if (moves.Count > 0)
        {
            if (string.Equals(conditional.Id, "INIT_MOVE", StringComparison.Ordinal))
            {
                string? evaluatedStateId = TryEvaluateConditionalStateId(conditional);
                if (!string.IsNullOrWhiteSpace(evaluatedStateId) && moves.ContainsKey(evaluatedStateId))
                {
                    forcedBranchId = evaluatedStateId;
                }
            }
        }

        foreach (object? branch in branches)
        {
            if (branch == null || ConditionalBranchIdField.GetValue(branch) is not string id || !moves.TryGetValue(id, out MoveState? move))
            {
                continue;
            }

            if (forcedBranchId != null && !string.Equals(id, forcedBranchId, StringComparison.Ordinal))
            {
                continue;
            }

            string key = $"branch.{monsterTypeName}.{conditional.Id}.{move.Id}";
            options.Add(new ConditionalBranchOption(move, ResolveLocalizationText(key)));
            if (forcedBranchId != null)
            {
                break;
            }
        }

        return options;
    }

    private static IReadOnlyList<RandomBranchOption> ResolveRandomOptions(
        RandomBranchState random,
        MonsterMoveStateMachine stateMachine,
        string monsterTypeName)
    {
        List<RandomBranchOption> options = new List<RandomBranchOption>();
        foreach (RandomBranchState.StateWeight stateWeight in random.States)
        {
            if (!stateMachine.States.TryGetValue(stateWeight.stateId, out MonsterState? state) || state is not MoveState move)
            {
                continue;
            }

            float weight;
            try
            {
                weight = Math.Max(0f, stateWeight.GetWeight());
            }
            catch
            {
                weight = 0f;
            }

            string key = $"branch.{monsterTypeName}.{random.Id}.{move.Id}";
            options.Add(new RandomBranchOption(move, weight, stateWeight, ResolveLocalizationText(key)));
        }

        return options;
    }

    private static string FormatPercent(float weight, IReadOnlyList<RandomBranchOption> options)
    {
        float total = options.Sum(static o => o.Weight);
        if (total <= 0f)
        {
            return "0%";
        }

        float percent = weight / total * 100f;
        return percent.ToString(percent >= 10f ? "0.#" : "0.##", CultureInfo.InvariantCulture) + "%";
    }

    private static string? BuildRandomConstraintText(RandomBranchState.StateWeight stateWeight)
    {
        List<string> parts = new List<string>();
        switch (stateWeight.repeatType)
        {
            case MoveRepeatType.UseOnlyOnce:
                parts.Add(ResolveLocalizationText("ui.UseOnlyOnce") ?? GetIntentText("STATE_MACHINE.use_once", "Use only once per combat"));
                break;
            case MoveRepeatType.CannotRepeat:
                parts.Add(GetIntentText("STATE_MACHINE.no_consecutive", "Cannot repeat consecutively"));
                break;
            case MoveRepeatType.CanRepeatXTimes:
                parts.Add(GetIntentText("STATE_MACHINE.max_repeats", "Up to {Count} consecutive uses", ("Count", stateWeight.maxTimes)));
                break;
        }

        if (stateWeight.cooldown > 0)
        {
            parts.Add(GetIntentText("STATE_MACHINE.cooldown", "Cannot reappear within {Count} turns", ("Count", stateWeight.cooldown)));
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string GetIntentText(string key, string fallback, params (string Name, object Value)[] vars)
    {
        LocString? loc = LocString.GetIfExists("intents", key);
        if (loc != null)
        {
            foreach ((string name, object value) in vars)
            {
                loc.AddObj(name, value);
            }

            return loc.GetFormattedText();
        }

        string text = fallback;
        foreach ((string name, object value) in vars)
        {
            text = text.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal);
        }

        return text;
    }

    private static string? ResolveLocalizationText(string key)
    {
        string? text = IntentGraphConfigRepository.GetLocalizedText(key);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? TryEvaluateConditionalStateId(ConditionalBranchState conditional)
    {
        if (ConditionalEvaluateStatesMethod == null)
        {
            return null;
        }

        try
        {
            return ConditionalEvaluateStatesMethod.Invoke(conditional, null) as string;
        }
        catch
        {
            return null;
        }
    }

    private static void AddRootMoveId(
        ICollection<string> roots,
        IReadOnlyDictionary<string, MoveState> moves,
        string? moveId)
    {
        if (string.IsNullOrWhiteSpace(moveId)
            || !moves.ContainsKey(moveId)
            || roots.Contains(moveId, StringComparer.Ordinal))
        {
            return;
        }

        roots.Add(moveId);
    }

    private static void AddInitialRootMoves(
        MonsterMoveStateMachine stateMachine,
        IReadOnlyDictionary<string, MoveState> moves,
        ICollection<string> roots)
    {
        if (StateMachineInitialStateField?.GetValue(stateMachine) is not MonsterState initialState)
        {
            return;
        }

        HashSet<string> visitedStates = new HashSet<string>(StringComparer.Ordinal);
        CollectRootMovesFromState(initialState, stateMachine, moves, roots, visitedStates);
    }

    private static void CollectRootMovesFromState(
        MonsterState state,
        MonsterMoveStateMachine stateMachine,
        IReadOnlyDictionary<string, MoveState> moves,
        ICollection<string> roots,
        ISet<string> visitedStates)
    {
        if (!visitedStates.Add(state.Id))
        {
            return;
        }

        if (state is MoveState move)
        {
            AddRootMoveId(roots, moves, move.Id);
            return;
        }

        if (state is ConditionalBranchState conditional)
        {
            string? evaluated = TryEvaluateConditionalStateId(conditional);
            if (!string.IsNullOrWhiteSpace(evaluated)
                && stateMachine.States.TryGetValue(evaluated, out MonsterState? evaluatedState))
            {
                CollectRootMovesFromState(evaluatedState, stateMachine, moves, roots, visitedStates);
                return;
            }

            if (ConditionalStatesProperty?.GetValue(conditional) is not IEnumerable branches
                || ConditionalBranchIdField == null)
            {
                return;
            }

            foreach (object? branch in branches)
            {
                if (branch == null
                    || ConditionalBranchIdField.GetValue(branch) is not string branchId
                    || !stateMachine.States.TryGetValue(branchId, out MonsterState? branchState))
                {
                    continue;
                }

                CollectRootMovesFromState(branchState, stateMachine, moves, roots, visitedStates);
            }

            return;
        }

        if (state is RandomBranchState random)
        {
            foreach (RandomBranchState.StateWeight branch in random.States)
            {
                if (!stateMachine.States.TryGetValue(branch.stateId, out MonsterState? branchState))
                {
                    continue;
                }

                CollectRootMovesFromState(branchState, stateMachine, moves, roots, visitedStates);
            }
        }
    }

    private static Dictionary<string, Vector2> GenerateAutoPositions(IReadOnlyDictionary<string, int> depth, IReadOnlyDictionary<string, int> order)
    {
        Dictionary<string, Vector2> result = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        foreach (IGrouping<int, KeyValuePair<string, int>> g in depth.GroupBy(static p => p.Value).OrderBy(static g => g.Key))
        {
            List<string> ids = g.Select(static p => p.Key).OrderBy(id => order.TryGetValue(id, out int value) ? value : int.MaxValue).ToList();
            float y0 = -0.5f * (ids.Count - 1) * Dy;
            for (int i = 0; i < ids.Count; i++)
            {
                result[ids[i]] = new Vector2(g.Key * Dx, y0 + i * Dy);
            }
        }

        return result;
    }

    private static IntentGraphLayoutDefinition BuildAutoLayout(IReadOnlyDictionary<string, Vector2> pos, IReadOnlyList<IntentGraphTransition> transitions)
    {
        IntentGraphLayoutDefinition layout = new IntentGraphLayoutDefinition
        {
            Moves = pos.Select(static p => new IntentGraphMoveLayout { Id = p.Key, X = p.Value.X, Y = p.Value.Y }).ToList(),
            Arrows = new List<IntentGraphArrowLayout>(),
            Labels = new List<IntentGraphLabelLayout>(),
            IconGroups = new List<IntentGraphGroupLayout>()
        };

        foreach (IntentGraphTransition t in transitions)
        {
            if (!pos.TryGetValue(t.FromMoveId, out Vector2 from) || !pos.TryGetValue(t.ToMoveId, out Vector2 to))
            {
                continue;
            }

            List<float> path = new List<float>();
            foreach (Vector2 p in CreateAutoArrowPoints(from, to))
            {
                path.Add(p.X);
                path.Add(p.Y);
            }

            layout.Arrows!.Add(new IntentGraphArrowLayout { Path = path });
        }

        return layout;
    }

    private static string ResolveInlineLabelText(string text)
    {
        if (text.StartsWith("text.", StringComparison.Ordinal) || text.StartsWith("branch.", StringComparison.Ordinal) || text.StartsWith("ui.", StringComparison.Ordinal))
        {
            return ResolveLocalizationText(text) ?? text;
        }

        return text;
    }

    private static List<List<Vector2>> ParseConfiguredPaths(IntentGraphLayoutDefinition layout)
    {
        List<List<Vector2>> all = new List<List<Vector2>>();
        foreach (IntentGraphArrowLayout arrow in layout.Arrows ?? Enumerable.Empty<IntentGraphArrowLayout>())
        {
            if (arrow.Path.Count < 4)
            {
                continue;
            }

            List<Vector2> points = new List<Vector2>();
            for (int i = 0; i + 1 < arrow.Path.Count; i += 2)
            {
                points.Add(new Vector2(arrow.Path[i], arrow.Path[i + 1]));
            }

            if (points.Count >= 2)
            {
                all.Add(points);
            }
        }

        return all;
    }

    private static IReadOnlyList<Vector2>? MatchConfiguredPath(IReadOnlyList<List<Vector2>> paths, ISet<int> used, Vector2 from, Vector2 to)
    {
        float best = float.MaxValue;
        int bestIndex = -1;
        bool reverse = false;

        for (int i = 0; i < paths.Count; i++)
        {
            if (used.Contains(i))
            {
                continue;
            }

            IReadOnlyList<Vector2> path = paths[i];
            float f = path[0].DistanceTo(from) + path[path.Count - 1].DistanceTo(to);
            float r = path[0].DistanceTo(to) + path[path.Count - 1].DistanceTo(from);
            float d = Math.Min(f, r);
            if (d < best)
            {
                best = d;
                bestIndex = i;
                reverse = r < f;
            }
        }

        if (bestIndex < 0 || best > 2.2f)
        {
            return null;
        }

        used.Add(bestIndex);
        if (!reverse)
        {
            return paths[bestIndex];
        }

        List<Vector2> rev = new List<Vector2>(paths[bestIndex]);
        rev.Reverse();
        return rev;
    }

    private static IReadOnlyList<Vector2> CreateAutoArrowPoints(Vector2 from, Vector2 to)
    {
        if (Math.Abs(from.Y - to.Y) < 0.01f)
        {
            return new[] { from, to };
        }

        float midX = (from.X + to.X) * 0.5f;
        return new[] { from, new Vector2(midX, from.Y), new Vector2(midX, to.Y), to };
    }

    private static string? ApplyMoveReplacement(IntentGraphMonsterConfig? config, string moveId, int index, string? label)
    {
        if (config == null
            || string.IsNullOrWhiteSpace(label)
            || !config.MoveReplacements.TryGetValue(moveId, out List<IntentGraphMoveReplacement>? replacements)
            || index < 0
            || index >= replacements.Count
            || string.IsNullOrWhiteSpace(replacements[index].TimesText))
        {
            return label;
        }

        string times = replacements[index].TimesText!;
        char[] splitChars = { 'x', 'X', '脳' };
        int split = label!.LastIndexOfAny(splitChars);
        if (split < 0)
        {
            return label;
        }

        int start = split + 1;
        while (start < label.Length && char.IsWhiteSpace(label[start]))
        {
            start++;
        }

        int end = start;
        while (end < label.Length && char.IsDigit(label[end]))
        {
            end++;
        }

        return end > start ? label.Substring(0, start) + times + label.Substring(end) : label;
    }

    private static void NormalizeAndSize(IntentGraphRenderModel render, IntentGraphLayoutDefinition layout)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (IntentGraphMoveNode move in render.Moves)
        {
            minX = Math.Min(minX, move.PositionUnits.X - 0.42f);
            minY = Math.Min(minY, move.PositionUnits.Y - 0.42f);
            maxX = Math.Max(maxX, move.PositionUnits.X + 0.42f);
            maxY = Math.Max(maxY, move.PositionUnits.Y + 0.42f);
        }

        foreach (IntentGraphArrowPath arrow in render.Arrows)
        {
            foreach (Vector2 p in arrow.PointsUnits)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        foreach (IntentGraphLabelNode label in render.Labels)
        {
            minX = Math.Min(minX, label.PositionUnits.X - 0.3f);
            minY = Math.Min(minY, label.PositionUnits.Y - 0.3f);
            maxX = Math.Max(maxX, label.PositionUnits.X + 0.3f);
            maxY = Math.Max(maxY, label.PositionUnits.Y + 0.3f);
        }

        foreach (IntentGraphGroupNode group in render.Groups)
        {
            minX = Math.Min(minX, group.RectUnits.Position.X);
            minY = Math.Min(minY, group.RectUnits.Position.Y);
            maxX = Math.Max(maxX, group.RectUnits.End.X);
            maxY = Math.Max(maxY, group.RectUnits.End.Y);
        }

        if (minX == float.MaxValue)
        {
            minX = 0f;
            minY = 0f;
            maxX = 1f;
            maxY = 1f;
        }

        float ox = minX < 0f ? -minX + 0.5f : 0.5f;
        float oy = minY < 0f ? -minY + 0.5f : 0.5f;

        for (int i = 0; i < render.Moves.Count; i++)
        {
            IntentGraphMoveNode move = render.Moves[i];
            move.PositionUnits = new Vector2(move.PositionUnits.X + ox, move.PositionUnits.Y + oy);
        }

        for (int i = 0; i < render.Arrows.Count; i++)
        {
            IntentGraphArrowPath arrow = render.Arrows[i];
            render.Arrows[i] = new IntentGraphArrowPath { PointsUnits = arrow.PointsUnits.Select(p => new Vector2(p.X + ox, p.Y + oy)).ToList() };
        }

        for (int i = 0; i < render.Labels.Count; i++)
        {
            IntentGraphLabelNode label = render.Labels[i];
            render.Labels[i] = label with { PositionUnits = new Vector2(label.PositionUnits.X + ox, label.PositionUnits.Y + oy) };
        }

        for (int i = 0; i < render.Groups.Count; i++)
        {
            Rect2 rect = render.Groups[i].RectUnits;
            rect.Position += new Vector2(ox, oy);
            render.Groups[i] = new IntentGraphGroupNode(rect);
        }

        render.WidthUnits = maxX - minX + 1f;
        render.HeightUnits = maxY - minY + 1f;

        if (layout.Width.HasValue)
        {
            render.WidthUnits = Math.Max(render.WidthUnits, layout.Width.Value);
        }

        if (layout.Height.HasValue)
        {
            render.HeightUnits = Math.Max(render.HeightUnits, layout.Height.Value);
        }
    }
}
