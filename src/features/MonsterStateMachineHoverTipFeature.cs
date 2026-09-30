using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Godot;
using LibraryOfRuina.features.intentgraph;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.features;

/// <summary>
/// 从怪物的招式状态机生成意图图。状态机先转成状态节点（招式、分支），再由 <see cref="IntentGraphLayouter"/> 排版；
/// 配置（intentgraph.json）沿用 Intent Graph（Chaofan，创意工坊 3747528152）的格式与语义：
/// secondaryInitialStates 额外的起点行，moveReplacements 的次数写法，graph 整张图替换，graphPatch 追加招式、连线、标签和分组，
/// 坐标都是 Intent Graph 布局里的格子坐标（招式为左上角、标签为基线起点）。
/// </summary>
internal static class MonsterStateMachineIntentGraphFeature
{
    private const string InitMoveId = "INIT_MOVE";

    private readonly record struct BranchOption(MonsterState Target, string Label);

    /// <summary>转换状态节点时共用的数据；RootNodes 按状态复用顶层节点。</summary>
    private sealed record BuildContext(
        MonsterMoveStateMachine Machine,
        Creature Owner,
        string MonsterTypeName,
        Dictionary<MonsterState, IntentGraphStateNode> RootNodes,
        Font LabelFont);

    public static bool TryBuild(Creature owner, out IntentGraphRenderModel render, out string monsterName)
    {
        render = new IntentGraphRenderModel();
        monsterName = string.Empty;

        MonsterModel? monster = owner.Monster;
        MonsterMoveStateMachine? machine = monster?.MoveStateMachine;
        if (monster == null || owner.CombatState == null || machine == null || !machine.States.Values.OfType<MoveState>().Any())
        {
            return false;
        }

        string monsterTypeName = monster.GetType().FullName ?? monster.GetType().Name;
        IntentGraphMonsterConfig? config = IntentGraphConfigRepository.GetConfigForMonster(monsterTypeName);
        IReadOnlyList<Creature> targets = owner.CombatState.Players.Select(static p => p.Creature).ToList();
        string? nextMoveId = monster.NextMove?.Id;
        Font labelFont = IntentGraphFonts.CreateLabelFont();

        IntentGraphMoveNode? CreateMove(MoveState move, float x, float y) =>
            BuildMoveNode(move, x, y, owner, targets, monsterTypeName, config, nextMoveId);
        IntentGraphLayouter layouter = new IntentGraphLayouter(
            (node, x, y) => node.State is MoveState move ? CreateMove(move, x, y) : null,
            node => node.State is MoveState move ? ResolveMoveName(monster, move.Id) : null);

        if (config?.Graph != null)
        {
            render = BuildFromLayout(config.Graph, machine, CreateMove, move => ResolveMoveName(monster, move.Id));
        }
        else
        {
            List<IntentGraphStateNode> roots = BuildStateNodes(machine, owner, monsterTypeName, config, labelFont);
            if (roots.Count == 0)
            {
                return false;
            }

            IntentGraphLayouter.Simplify(roots);
            render = layouter.Layout(roots);
            if (config?.GraphPatch != null)
            {
                IntentGraphRenderModel patch = BuildFromLayout(config.GraphPatch, machine, CreateMove, move => ResolveMoveName(monster, move.Id));
                render.Moves.AddRange(patch.Moves);
                render.Arrows.AddRange(patch.Arrows);
                render.Labels.AddRange(patch.Labels);
                render.Groups.AddRange(patch.Groups);
                render.WidthUnits = Math.Max(render.WidthUnits, patch.WidthUnits);
                render.HeightUnits = Math.Max(render.HeightUnits, patch.HeightUnits);
            }
        }

        if (render.Moves.Count == 0)
        {
            return false;
        }

        monsterName = monster.Title.GetFormattedText();
        return true;
    }

    public static void ReloadLocalization()
    {
        IntentGraphConfigRepository.ReloadLocalization();
    }

    // ---------------------------------------------------------------- state nodes

    /// <summary>
    /// 起点是状态机的初始状态（开局的条件分支按当前状态求值成具体招式），配置里的 secondaryInitialStates 各开一行。
    /// </summary>
    private static List<IntentGraphStateNode> BuildStateNodes(
        MonsterMoveStateMachine machine,
        Creature owner,
        string monsterTypeName,
        IntentGraphMonsterConfig? config,
        Font labelFont)
    {
        BuildContext context = new BuildContext(machine, owner, monsterTypeName, new Dictionary<MonsterState, IntentGraphStateNode>(), labelFont);
        List<IntentGraphStateNode> roots = new List<IntentGraphStateNode>();
        MonsterState? initial = VanillaPrivate.MonsterMoveStateMachineInitialState.Get(machine) as MonsterState;
        if (initial is ConditionalBranchState initialBranch
            && TryEvaluateConditionalStateId(initialBranch, owner) is { } evaluatedId
            && machine.States.TryGetValue(evaluatedId, out MonsterState? evaluated))
        {
            IntentGraphStateNode node = ToStateNode(evaluated, null, context);
            if (node.Next?.State != initial)
            {
                roots.Add(node);
            }
        }

        if (roots.Count == 0 && initial != null && IsGraphable(initial))
        {
            roots.Add(ToStateNode(initial, null, context));
        }

        if (roots.Count == 0 && owner.Monster?.NextMove is MoveState next)
        {
            roots.Add(ToStateNode(next, null, context));
        }

        foreach (string id in config?.SecondaryInitialStates ?? Enumerable.Empty<string>())
        {
            if (machine.States.TryGetValue(id, out MonsterState? state) && !context.RootNodes.ContainsKey(state))
            {
                roots.Add(ToStateNode(state, null, context));
            }
        }

        return roots;
    }

    /// <summary>
    /// 招式节点的下一个节点是它后续状态的顶层节点；分支节点的子节点是各分支目标（不复用顶层节点），
    /// 子节点都通往同一个节点时改由分支节点连过去。分支条件文字比招式宽时，子节点按文字宽度加宽。
    /// </summary>
    private static IntentGraphStateNode ToStateNode(MonsterState state, IntentGraphStateNode? parent, BuildContext context)
    {
        Dictionary<MonsterState, IntentGraphStateNode> rootNodes = context.RootNodes;
        if (parent == null && rootNodes.TryGetValue(state, out IntentGraphStateNode? existing))
        {
            return existing;
        }

        if (state is MoveState move)
        {
            IntentGraphStateNode moveNode = new IntentGraphStateNode
            {
                Id = move.Id,
                State = move,
                IconCount = move.Intents.Count,
                Width = IntentGraphLayouter.MoveWidth(move.Intents.Count),
                Height = 1f,
                NextCount = 1,
                Parent = parent
            };
            if (parent == null)
            {
                rootNodes[state] = moveNode;
            }

            MonsterState? follow = ResolveFollowUpState(move, context.Machine);
            moveNode.Next = follow == null || !IsGraphable(follow) ? null : ToStateNode(follow, null, context);
            return moveNode;
        }

        IntentGraphStateNode branchNode = new IntentGraphStateNode { Id = state.Id, State = state, Parent = parent };
        if (parent == null)
        {
            rootNodes[state] = branchNode;
        }

        IReadOnlyList<BranchOption> options = state switch
        {
            RandomBranchState random => ResolveRandomOptions(random, context.Machine, context.MonsterTypeName),
            ConditionalBranchState conditional => ResolveConditionalOptions(conditional, context.Machine, context.Owner, context.MonsterTypeName),
            _ => Array.Empty<BranchOption>()
        };

        List<IntentGraphStateNode> children = new List<IntentGraphStateNode>();
        foreach (BranchOption option in options)
        {
            if (!IsGraphable(option.Target) || IsInParentChain(branchNode, option.Target))
            {
                continue;
            }

            IntentGraphStateNode child = ToStateNode(option.Target, branchNode, context);
            child.Label = option.Label;
            float labelWidth = context.LabelFont.GetStringSize(option.Label, HorizontalAlignment.Left, -1f, IntentGraphLayouter.GroupLabelFontSize).X;
            child.Width = Math.Max(child.Width, labelWidth / NIntentGraph.GridSize);
            children.Add(child);
        }

        List<IntentGraphStateNode?> nexts = children.Select(static c => c.Next).Distinct().ToList();
        if (nexts.Count == 1)
        {
            foreach (IntentGraphStateNode child in children)
            {
                child.Next = null;
                child.NextCount = 0;
            }
        }

        branchNode.Children = children;
        branchNode.UpdateSize();
        branchNode.Next = nexts.Count == 1 ? nexts[0] : null;
        branchNode.NextCount = (branchNode.Next != null ? 1 : 0) + children.Select(static c => c.NextCount).DefaultIfEmpty(0).Max();
        return branchNode;
    }

    /// <summary>
    /// 能画进图的状态：招式、随机分支、条件分支。本模组的路由状态（DelegatingMonsterRouterState 这类按代码决定下一招的状态）
    /// 看不出去向，画出来只会是一个空节点和一支悬空的箭头，所以在它这里断开。
    /// </summary>
    private static bool IsGraphable(MonsterState state) => state is MoveState or RandomBranchState or ConditionalBranchState;

    private static bool IsInParentChain(IntentGraphStateNode node, MonsterState state)
    {
        for (IntentGraphStateNode? current = node; current != null; current = current.Parent)
        {
            if (current.State == state)
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- moves

    private static IntentGraphMoveNode BuildMoveNode(
        MoveState move,
        float x,
        float y,
        Creature owner,
        IReadOnlyList<Creature> targets,
        string monsterTypeName,
        IntentGraphMonsterConfig? config,
        string? nextMoveId)
    {
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
                if (LorLog.FirstTime("IntentGraphFeature.IntentResolve:" + resolveKeyPrefix + ":label"))
                {
                    LorLog.Warn("[LibraryOfRuina.IntentGraph] Failed to resolve intent label for " + resolveKeyPrefix + ". " + exception.Message);
                }
            }

            text = ApplyMoveReplacement(config, move.Id, i, text);
            Texture2D? texture = null;
            try
            {
                texture = intent.GetTexture(targets, owner);
            }
            catch (Exception exception)
            {
                if (LorLog.FirstTime("IntentGraphFeature.IntentResolve:" + resolveKeyPrefix + ":texture"))
                {
                    LorLog.Warn("[LibraryOfRuina.IntentGraph] Failed to resolve intent texture for " + resolveKeyPrefix + ". " + exception.Message);
                }
            }

            icons.Add(new IntentGraphIntentIcon(texture, string.IsNullOrWhiteSpace(text) ? null : text, intent.IntentType));
        }

        return new IntentGraphMoveNode
        {
            MoveId = move.Id,
            PositionUnits = new Vector2(x, y),
            Intents = icons,
            IsCurrentMove = string.Equals(move.Id, nextMoveId, StringComparison.Ordinal)
        };
    }

    /// <summary>
    /// 招式名取怪物本地化表里的 &lt;怪物&gt;.moves.&lt;招式&gt;.title；招式 ID 带 _MOVE 后缀或数字编号时去掉再找，
    /// 仍找不到时去掉第一个下划线前的部分（与 Intent Graph 的查找顺序相同）。
    /// </summary>
    private static string? ResolveMoveName(MonsterModel monster, string moveId)
    {
        string prefix = monster.Id.Entry + ".moves.";
        string? Title(string id) => LocString.GetIfExists("monsters", prefix + id + ".title")?.GetFormattedText();

        string? title = Title(moveId);
        if (title != null)
        {
            return title;
        }

        string trimmed = moveId;
        bool changed = true;
        while (changed)
        {
            changed = false;
            if (trimmed.EndsWith("_MOVE", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^"_MOVE".Length];
                changed = true;
            }

            int end = trimmed.Length;
            while (end > 0 && (char.IsAsciiDigit(trimmed[end - 1]) || trimmed[end - 1] == '_'))
            {
                end--;
            }

            if (end < trimmed.Length)
            {
                trimmed = trimmed[..end];
                changed = true;
            }
        }

        title = Title(trimmed);
        int underscore = trimmed.IndexOf('_');
        if (title == null && underscore >= 0 && underscore + 1 < trimmed.Length)
        {
            title = Title(trimmed[(underscore + 1)..]);
        }

        return title;
    }

    // ---------------------------------------------------------------- config graphs

    /// <summary>配置里的整张图或补丁：招式坐标是左上角，连线是 Intent Graph 的路径格式，标签左对齐、坐标是基线。</summary>
    private static IntentGraphRenderModel BuildFromLayout(
        IntentGraphLayoutDefinition layout,
        MonsterMoveStateMachine machine,
        Func<MoveState, float, float, IntentGraphMoveNode?> createMove,
        Func<MoveState, string?> moveName)
    {
        IntentGraphRenderModel render = new IntentGraphRenderModel();
        foreach (IntentGraphMoveLayout moveLayout in layout.Moves ?? Enumerable.Empty<IntentGraphMoveLayout>())
        {
            if (!machine.States.TryGetValue(moveLayout.Id, out MonsterState? state) || state is not MoveState move)
            {
                continue;
            }

            IntentGraphMoveNode? node = createMove(move, moveLayout.X, moveLayout.Y);
            if (node == null)
            {
                continue;
            }

            render.Moves.Add(node);
            string? name = node.Intents.Count > 0 ? moveName(move) : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                float width = IntentGraphLayouter.MoveWidth(node.Intents.Count);
                render.Labels.Add(new IntentGraphLabelNode(
                    new Vector2(moveLayout.X + width / 2f, moveLayout.Y + 0.2f), name!, IntentGraphLayouter.MoveNameFontSize, 0.5f));
            }
        }

        foreach (IntentGraphArrowLayout arrow in layout.Arrows ?? Enumerable.Empty<IntentGraphArrowLayout>())
        {
            List<Vector2> points = IntentGraphLayouter.ToPoints(arrow.Path);
            if (points.Count >= 2)
            {
                render.Arrows.Add(new IntentGraphArrowPath { PointsUnits = points });
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

        render.WidthUnits = layout.Width ?? render.Moves.Select(static m => m.PositionUnits.X + IntentGraphLayouter.MoveWidth(m.Intents.Count)).DefaultIfEmpty(1f).Max();
        render.HeightUnits = layout.Height ?? render.Moves.Select(static m => m.PositionUnits.Y + 1f).DefaultIfEmpty(1f).Max();
        return render;
    }

    // ---------------------------------------------------------------- branches

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

    /// <summary>条件分支：每个分支目标一项，条件文字取 branch.&lt;怪物&gt;.&lt;分支&gt;.&lt;目标&gt;；开局分支只保留当前求值出的那一项。</summary>
    private static IReadOnlyList<BranchOption> ResolveConditionalOptions(
        ConditionalBranchState conditional,
        MonsterMoveStateMachine machine,
        Creature owner,
        string monsterTypeName)
    {
        List<BranchOption> options = new List<BranchOption>();
        if (VanillaPrivate.ConditionalBranchStateStates.Get(conditional) is not IEnumerable branches || !VanillaPrivate.ConditionalBranchId.IsAvailable)
        {
            return options;
        }

        string? forcedBranchId = string.Equals(conditional.Id, InitMoveId, StringComparison.Ordinal)
            ? TryEvaluateConditionalStateId(conditional, owner)
            : null;
        foreach (object? branch in branches)
        {
            if (branch == null
                || VanillaPrivate.ConditionalBranchId.Get(branch) is not string id
                || !machine.States.TryGetValue(id, out MonsterState? target)
                || options.Any(o => o.Target == target))
            {
                continue;
            }

            if (forcedBranchId != null && !string.Equals(id, forcedBranchId, StringComparison.Ordinal))
            {
                continue;
            }

            string key = $"branch.{monsterTypeName}.{conditional.Id}.{id}";
            options.Add(new BranchOption(target, ResolveLocalizationText(key) ?? string.Empty));
        }

        return options;
    }

    /// <summary>随机分支：条件文字是概率，加上本地化的附加条件和原版的重复限制。</summary>
    private static IReadOnlyList<BranchOption> ResolveRandomOptions(
        RandomBranchState random,
        MonsterMoveStateMachine machine,
        string monsterTypeName)
    {
        List<(MonsterState Target, float Weight, RandomBranchState.StateWeight StateWeight)> weighted = new();
        foreach (RandomBranchState.StateWeight stateWeight in random.States)
        {
            if (!machine.States.TryGetValue(stateWeight.stateId, out MonsterState? target))
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

            weighted.Add((target, weight, stateWeight));
        }

        float total = weighted.Sum(static w => w.Weight);
        List<BranchOption> options = new List<BranchOption>();
        foreach ((MonsterState target, float weight, RandomBranchState.StateWeight stateWeight) in weighted)
        {
            List<string> parts = new List<string> { FormatPercent(weight, total) };
            string? condition = ResolveLocalizationText($"branch.{monsterTypeName}.{random.Id}.{target.Id}");
            if (!string.IsNullOrWhiteSpace(condition))
            {
                parts.Add(condition!);
            }

            string? extra = BuildRandomConstraintText(stateWeight);
            if (!string.IsNullOrWhiteSpace(extra))
            {
                parts.Add(extra!);
            }

            options.Add(new BranchOption(target, string.Join(", ", parts)));
        }

        return options;
    }

    private static string FormatPercent(float weight, float total)
    {
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

    /// <summary>
    /// 条件分支当前会走哪一支。0.111 用公开的 GetNextState 求值：两个参数都不参与，只按顺序检查各分支条件，
    /// 没有成立的分支时抛异常；较旧的游戏版本另有私有的 EvaluateStates。
    /// </summary>
    private static string? TryEvaluateConditionalStateId(ConditionalBranchState conditional, Creature owner)
    {
        try
        {
            return conditional.GetNextState(owner, null!);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Exception)
        {
            // 条件本身出错时退回旧版本的私有方法（若存在），否则当作无法预判。
        }

        if (!VanillaPrivate.ConditionalBranchStateEvaluateStates.IsAvailable)
        {
            return null;
        }

        try
        {
            return VanillaPrivate.ConditionalBranchStateEvaluateStates.Invoke(conditional, null) as string;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveInlineLabelText(string text)
    {
        if (text.StartsWith("text.", StringComparison.Ordinal) || text.StartsWith("branch.", StringComparison.Ordinal) || text.StartsWith("ui.", StringComparison.Ordinal))
        {
            return ResolveLocalizationText(text) ?? text;
        }

        return text;
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
        // 原版各语言的 FORMAT_DAMAGE_MULTI 用拉丁 x、乘号 ×（简中）或西里尔字母 х（俄语）分隔伤害与次数。
        char[] splitChars = { 'x', 'X', '×', 'х' };
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
}
