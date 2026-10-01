using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.features.intentgraph;

/// <summary>
/// 意图图的状态节点：一个招式，或一个分支（Children 是各分支目标，子节点的 Label 是分支条件）。
/// 顶层节点按状态复用，分支里的子节点不复用：同一个招式既是分支目标又在后续循环里出现时会画两次，
/// 与 Intent Graph（Chaofan，创意工坊 3747528152）一致。
/// </summary>
internal sealed class IntentGraphStateNode
{
    public required string Id { get; init; }

    public MonsterState? State { get; init; }

    public string? Label { get; set; }

    public int IconCount { get; init; }

    public float Width { get; set; }

    public float Height { get; set; }

    public IntentGraphStateNode? Parent { get; set; }

    public List<IntentGraphStateNode>? Children { get; set; }

    public bool HorizontalLayout { get; set; }

    public IntentGraphStateNode? Next { get; set; }

    public int NextCount { get; set; }

    public bool SimpleLoopStart { get; set; }

    public int SimpleLoopLength { get; set; }

    public int SimpleLoopPredecessors { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public bool Placed { get; set; }

    public int XIndex { get; set; }

    public int YIndex { get; set; }

    public bool ArrowAdded { get; set; }

    public float ArrowRight { get; set; }

    public float ArrowBottom { get; set; }

    /// <summary>分组的尺寸：纵向排列时各子节点上方留一行分支条件，四周留 0.1 格。</summary>
    public void UpdateSize()
    {
        if (Children == null)
        {
            return;
        }

        if (HorizontalLayout)
        {
            Width = Children.Sum(static c => c.Width) + IntentGraphLayouter.GroupPadding * (Children.Count - 1) + IntentGraphLayouter.GroupPadding * 2f;
            Height = Children.Select(static c => c.Height).DefaultIfEmpty(1f).Max() + IntentGraphLayouter.GroupLabelHeight + IntentGraphLayouter.GroupPadding * 2f;
        }
        else
        {
            Width = Children.Select(static c => c.Width).DefaultIfEmpty(1f).Max() + IntentGraphLayouter.GroupPadding * 2f;
            Height = IntentGraphLayouter.GroupLabelHeight * Children.Count + IntentGraphLayouter.GroupPadding * 2f + Children.Sum(static c => c.Height);
        }
    }

    public static HashSet<IntentGraphStateNode> CollectAll(IEnumerable<IntentGraphStateNode> roots)
    {
        HashSet<IntentGraphStateNode> all = new HashSet<IntentGraphStateNode>();
        Queue<IntentGraphStateNode> queue = new Queue<IntentGraphStateNode>(roots);
        while (queue.Count > 0)
        {
            IntentGraphStateNode node = queue.Dequeue();
            if (!all.Add(node))
            {
                continue;
            }

            foreach (IntentGraphStateNode child in node.Children ?? Enumerable.Empty<IntentGraphStateNode>())
            {
                queue.Enqueue(child);
            }

            if (node.Next != null)
            {
                queue.Enqueue(node.Next);
            }
        }

        return all;
    }
}

/// <summary>
/// 把状态节点排成意图图。坐标单位是格（1 格 = 80 像素），招式坐标是第一个图标格子的左上角，连线的折点落在节点边缘，
/// 标签坐标是文字基线。排法照 Intent Graph（Chaofan）的规则由本模组重新实现：
/// <list type="bullet">
/// <item>节点从左到右排，每个初始状态开一行；节点之间横向留 0.25 格，另按出边数每条再留 0.25 格给竖向连线。</item>
/// <item>分支画成分组框：分支条件写在各分支招式的正上方并左对齐，框随条件文字宽度加宽。</item>
/// <item>三个及以上节点组成、只有一个入口的简单循环排成上下两行的环。</item>
/// <item>连线优先横向直连；两点互指时画成上下错开的一对；不同行时从节点右侧或底部绕行，
///   需要时把整行下移 0.25 格腾出横线位置；最后把重叠的平行线段错开。</item>
/// </list>
/// 没有移植的部分：按配置覆盖连线、合并相同子图、删去不可能出现的不重复分支（会改变显示的内容）。
/// </summary>
internal sealed class IntentGraphLayouter
{
    public const float GroupPadding = 0.1f;
    public const float GroupLabelHeight = 0.25f;
    // 同一招式的多个意图图标相互叠 0.33 格。
    public const float IconStep = 0.67f;
    public const int GroupLabelFontSize = 18;
    public const int MoveNameFontSize = 15;

    private readonly Func<IntentGraphStateNode, float, float, IntentGraphMoveNode?> _createMove;
    private readonly Func<IntentGraphStateNode, string?> _moveName;
    private readonly IntentGraphRenderModel _graph = new IntentGraphRenderModel();
    private readonly List<float[]> _arrows = new List<float[]>();
    private readonly Dictionary<float[], IntentGraphStateNode> _arrowTarget = new Dictionary<float[], IntentGraphStateNode>(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<float, IntentGraphStateNode> _hLineTarget = new Dictionary<float, IntentGraphStateNode>();
    private readonly Dictionary<float, IntentGraphStateNode> _vLineTarget = new Dictionary<float, IntentGraphStateNode>();
    private readonly Dictionary<(int X, int Y), IntentGraphStateNode> _indexToNode = new Dictionary<(int X, int Y), IntentGraphStateNode>();
    private readonly Dictionary<int, Row> _rows = new Dictionary<int, Row>();
    private int _nextXIndex;
    private int _yIndex;
    private float _nextX;
    private float _y;

    /// <param name="createMove">为招式节点生成图标；返回 null 时不画这个招式。</param>
    /// <param name="moveName">招式名，画在图标上方；null 表示不画。</param>
    public IntentGraphLayouter(
        Func<IntentGraphStateNode, float, float, IntentGraphMoveNode?> createMove,
        Func<IntentGraphStateNode, string?> moveName)
    {
        _createMove = createMove;
        _moveName = moveName;
    }

    public static float MoveWidth(int iconCount) => iconCount == 0 ? 0f : iconCount - (iconCount - 1) * (1f - IconStep);

    public IntentGraphRenderModel Layout(List<IntentGraphStateNode> roots)
    {
        foreach (IntentGraphStateNode root in roots)
        {
            _rows[_yIndex] = new Row { Y = _y };
            if (roots.Count == 1 && root.Next == root && root.Children == null)
            {
                root.Next = null;
                root.NextCount = 0;
            }

            AddNode(root, null, 0f);
            NewLine(_graph.HeightUnits + 0.25f);
        }

        SpreadOverlappingArrows();
        foreach (float[] path in _arrows)
        {
            _graph.Arrows.Add(new IntentGraphArrowPath { PointsUnits = ToPoints(path) });
        }

        return _graph;
    }

    /// <summary>化简：子节点多于两个且都很窄的分组改成横排；找出只有一个入口的简单循环。</summary>
    public static void Simplify(List<IntentGraphStateNode> roots)
    {
        HashSet<IntentGraphStateNode> all = IntentGraphStateNode.CollectAll(roots);
        int rootCount = all.Count(static n => n.Parent == null);
        foreach (IntentGraphStateNode node in all)
        {
            List<IntentGraphStateNode>? children = node.Children;
            if (children == null || children.Count <= 2 || node.Parent?.HorizontalLayout == true)
            {
                continue;
            }

            if (children.Any(static c => c.HorizontalLayout || c.Next != null || c.Width > 1.5f))
            {
                continue;
            }

            if ((node.Parent != null && node.Parent.Width >= children.Sum(static c => c.Width)) || rootCount <= 4)
            {
                node.HorizontalLayout = true;
                node.UpdateSize();
                for (IntentGraphStateNode? parent = node.Parent; parent != null; parent = parent.Parent)
                {
                    parent.UpdateSize();
                }
            }
        }

        Dictionary<IntentGraphStateNode, int> predecessors = roots.ToDictionary(static r => r, static _ => 1);
        foreach (IntentGraphStateNode node in all)
        {
            if (node.Next != null)
            {
                predecessors[node.Next] = predecessors.GetValueOrDefault(node.Next) + 1;
            }
        }

        foreach (IntentGraphStateNode start in all.Where(n => n.Parent == null && n.Children == null && predecessors.GetValueOrDefault(n) > 1).ToList())
        {
            HashSet<IntentGraphStateNode> chain = new HashSet<IntentGraphStateNode>();
            IntentGraphStateNode? current = start;
            while (current != null && !chain.Contains(current))
            {
                if (current != start && (current.Children != null || predecessors.GetValueOrDefault(current) > 1))
                {
                    break;
                }

                chain.Add(current);
                current = current.Next;
            }

            if (current == start && chain.Count > 1)
            {
                start.SimpleLoopStart = true;
                start.SimpleLoopLength = chain.Count;
                start.SimpleLoopPredecessors = predecessors.GetValueOrDefault(start) - 1;
            }
        }
    }

    private void NewLine(float y)
    {
        _nextXIndex = 0;
        _nextX = 0f;
        _yIndex++;
        _vLineTarget.Clear();
        _y = y;
    }

    private void AddNode(IntentGraphStateNode node, IntentGraphStateNode? predecessor, float x)
    {
        if (node.Placed)
        {
            return;
        }

        bool loopFits = node.SimpleLoopLength >= 4
                        || (node.SimpleLoopLength == 3 && (_nextX >= 3f || _graph.HeightUnits - _y >= 2f));
        if (node.SimpleLoopStart && node.SimpleLoopPredecessors == 1 && loopFits)
        {
            AddSimpleLoop(node, predecessor, x);
        }
        else
        {
            AddNormal(node, x, 0f);
        }
    }

    private void AddNormal(IntentGraphStateNode node, float x, float yOffset)
    {
        if (node.Placed)
        {
            return;
        }

        node.Placed = true;
        node.X = x;
        node.Y = _y + yOffset;
        if (node.Parent != null)
        {
            node.XIndex = node.Parent.XIndex;
            node.YIndex = node.Parent.YIndex;
        }
        else
        {
            node.XIndex = _nextXIndex++;
            node.YIndex = _yIndex;
            _indexToNode[(node.XIndex, node.YIndex)] = node;
        }

        _rows[node.YIndex].Nodes.Add(node);
        _nextX = Math.Max(_nextX, x + node.Width + 0.25f + 0.25f * node.NextCount);
        _graph.WidthUnits = Math.Max(_graph.WidthUnits, x + node.Width);
        _graph.HeightUnits = Math.Max(_graph.HeightUnits, node.Y + node.Height);
        node.ArrowRight = node.Parent?.ArrowRight ?? x + node.Width + 0.25f;
        node.ArrowBottom = node.Parent?.ArrowBottom ?? node.Y + node.Height + 0.25f;

        if (node.Children == null)
        {
            AddMove(node, x, node.Y);
        }
        else
        {
            Row row = _rows[node.YIndex];
            float childY = yOffset + GroupLabelHeight + GroupPadding;
            float childX = x + GroupPadding;
            foreach (IntentGraphStateNode child in node.Children)
            {
                AddLabel(new Vector2(childX, _y + childY - 0.04f), child.Label ?? string.Empty, GroupLabelFontSize, 0f, row);
                if (node.HorizontalLayout)
                {
                    AddNormal(child, childX, childY);
                    childX += child.Width + GroupPadding;
                    continue;
                }

                AddNormal(child, childX, childY);
                childY += child.Height + GroupLabelHeight;
            }

            _graph.Groups.Add(new IntentGraphGroupNode(new Rect2(node.X, node.Y, node.Width, node.Height)));
            row.GroupIndices.Add(_graph.Groups.Count - 1);
        }

        if (node.Next != null)
        {
            AddNode(node.Next, node, _nextX);
            AddArrow(node, node.Next);
        }
    }

    private void AddMove(IntentGraphStateNode node, float x, float y)
    {
        IntentGraphMoveNode? move = _createMove(node, x, y);
        if (move == null)
        {
            return;
        }

        _graph.Moves.Add(move);
        Row row = _rows[node.YIndex];
        row.MoveIndices.Add(_graph.Moves.Count - 1);
        string? name = move.Intents.Count > 0 ? _moveName(node) : null;
        if (!string.IsNullOrWhiteSpace(name))
        {
            AddLabel(new Vector2(x + MoveWidth(move.Intents.Count) / 2f, y + 0.2f), name!, MoveNameFontSize, 0.5f, row);
        }
    }

    private void AddLabel(Vector2 position, string text, int fontSize, float align, Row row)
    {
        _graph.Labels.Add(new IntentGraphLabelNode(position, text, fontSize, align));
        row.LabelIndices.Add(_graph.Labels.Count - 1);
    }

    /// <summary>
    /// 简单循环排成两行：上一行从左往右，下一行从右往左回到起点；循环后半段只有一个节点且入口在正左边时，
    /// 那个节点放在上一行下方正中。
    /// </summary>
    private void AddSimpleLoop(IntentGraphStateNode start, IntentGraphStateNode? predecessor, float x)
    {
        if (start.Placed)
        {
            return;
        }

        float y = _y;
        bool enteredFromLeft = predecessor == null || (predecessor.XIndex == _nextXIndex - 1 && predecessor.Y < y + 0.5f);
        List<IntentGraphStateNode> loop = new List<IntentGraphStateNode>();
        for (IntentGraphStateNode? node = start; node != null && !loop.Contains(node); node = node.Next)
        {
            loop.Add(node);
        }

        int startIndex = loop.IndexOf(start);
        int split = startIndex + (start.SimpleLoopLength + 1) / 2;
        while (TopRowWidth(loop, startIndex, split, enteredFromLeft, start) < BottomRowWidth(loop, split))
        {
            split++;
        }

        if (loop.Count - split <= 0
            || (loop.Count - split == 1 && loop.Count > 3 && loop[split].Width < 1.5f && _nextX < 2f))
        {
            AddNormal(start, x, 0f);
            return;
        }

        bool singleBelow = loop.Count - split <= 1 && enteredFromLeft;
        float bottomY = y + 1.35f;
        float startX = x;
        for (int i = 0; i < split; i++)
        {
            IntentGraphStateNode node = loop[i];
            node.X = x;
            node.Y = y;
            if (i == startIndex)
            {
                startX = x;
            }

            if (node.Children == null)
            {
                node.YIndex = _yIndex;
                AddMove(node, node.X, node.Y);
            }

            if (node.Next != null)
            {
                if (i == split - 1)
                {
                    if (!singleBelow)
                    {
                        float arrowX = x + node.Width - Math.Min(node.Width, node.Next.Width) / 2f;
                        AddArrowPath([1f, arrowX, node.Y + node.Height, bottomY], node.Next);
                    }
                }
                else
                {
                    AddArrowPath([0f, x + node.Width, node.Y + 0.5f, x + node.Width + 0.5f], node.Next);
                }
            }

            x += node.Width + 0.5f;
        }

        x -= 0.5f;
        _nextX = x + 0.25f;
        _graph.WidthUnits = Math.Max(_graph.WidthUnits, x);
        _graph.HeightUnits = Math.Max(bottomY + 1f, _graph.HeightUnits);
        if (!singleBelow)
        {
            float topWidth = x - startX;
            float gap = enteredFromLeft && loop.Count - split != 1
                ? (topWidth - loop.Skip(split).Sum(static n => n.Width)) / (loop.Count - split - 1)
                : 0.5f;
            for (int i = split; i < loop.Count; i++)
            {
                IntentGraphStateNode node = loop[i];
                node.X = x - node.Width;
                node.Y = bottomY;
                if (node.Children == null)
                {
                    node.YIndex = _yIndex;
                    AddMove(node, node.X, node.Y);
                }

                if (node.Next != null)
                {
                    if (i == loop.Count - 1)
                    {
                        if (enteredFromLeft)
                        {
                            AddArrowPath([1f, node.X + Math.Min(node.Width, node.Next.Width) / 2f, node.Y, start.Y + start.Height], node.Next);
                        }
                        else
                        {
                            AddArrowPath([0f, node.X, node.Y + 0.5f, startX + start.Width / 2f, start.Y + start.Height], node.Next);
                        }
                    }
                    else
                    {
                        AddArrowPath([0f, node.X, node.Y + 0.5f, node.X - gap], node.Next);
                    }
                }

                x -= node.Width + gap;
            }
        }
        else
        {
            IntentGraphStateNode last = loop[split - 1];
            IntentGraphStateNode below = loop[split];
            below.X = startX + (x - startX) / 2f - below.Width / 2f;
            below.Y = bottomY;
            if (below.Children == null)
            {
                below.YIndex = _yIndex;
                AddMove(below, below.X, below.Y);
            }

            if (last.X + last.Width / 2f < below.X + below.Width - 0.25f)
            {
                AddArrowPath([1f, last.X + last.Width / 2f, last.Y + last.Height, below.Y], below);
            }
            else
            {
                AddArrowPath([1f, Math.Max(last.X + last.Width / 2f, below.X + below.Width + 0.25f), last.Y + last.Height, below.Y + 0.5f, below.X + below.Width], below);
            }

            if (start.X + start.Width / 2f > below.X + 0.25f)
            {
                AddArrowPath([1f, start.X + start.Width / 2f, below.Y, start.Y + start.Height], start);
            }
            else
            {
                AddArrowPath([0f, below.X, below.Y + 0.5f, Math.Min(start.X + start.Width / 2f, below.X - 0.25f), start.Y + start.Height], start);
            }
        }

        foreach (IntentGraphStateNode node in loop)
        {
            node.Placed = true;
            node.ArrowAdded = true;
            node.XIndex = _nextXIndex++;
            node.YIndex = _yIndex;
            _indexToNode[(node.XIndex, node.YIndex)] = node;
            _rows[node.YIndex].Nodes.Add(node);
        }
    }

    private static float TopRowWidth(List<IntentGraphStateNode> loop, int startIndex, int split, bool enteredFromLeft, IntentGraphStateNode start) =>
        loop.Skip(startIndex).Take(split - startIndex).Sum(static n => n.Width)
        + 0.5f * (split - startIndex - 1)
        + (enteredFromLeft ? 0.5f : -start.Width / 2f - 0.5f);

    private static float BottomRowWidth(List<IntentGraphStateNode> loop, int split) =>
        loop.Skip(split).Sum(static n => n.Width) + 0.5f * (loop.Count - split - 1);

    private void AddArrow(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        if (node.ArrowAdded)
        {
            return;
        }

        node.ArrowAdded = true;
        if (node.YIndex == next.YIndex)
        {
            if (node.XIndex != next.XIndex && (TryHorizontalStraight(node, next) || TryHorizontalThenUp(node, next)))
            {
                return;
            }

            AddSameRowDetour(node, next);
            return;
        }

        if ((node.YIndex == next.YIndex + 1 && TryVerticalStraight(node, next)) || TryVerticalFromTop(node, next))
        {
            return;
        }

        AddOtherRowDetour(node, next);
    }

    /// <summary>同一行、两节点之间的招式都不挡住时画横向直线；两点互指时画上下错开 0.2 格的一对。</summary>
    private bool TryHorizontalStraight(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        float low = Math.Max(node.Y + 0.25f, next.Y + 0.25f);
        float high = Math.Min(node.Y + node.Height - 0.25f, next.Y + next.Height - 0.25f);
        foreach (IntentGraphStateNode between in NodesBetween(node, next))
        {
            low = Math.Max(low, between.Y + between.Height + 0.2f);
        }

        if (low > high)
        {
            return false;
        }

        float lineY = (low + high) / 2f;
        bool rightward = node.X < next.X;
        float from = rightward ? node.X + node.Width : node.X;
        float to = rightward ? next.X : next.X + next.Width;
        if (next.Next == node && !next.ArrowAdded)
        {
            float offset = rightward ? -0.2f : 0.2f;
            AddArrowPath([0f, from, lineY + offset, to], next);
            AddArrowPath([0f, to, lineY - offset, from], node);
            next.ArrowAdded = true;
            return true;
        }

        if (node.Parent != null)
        {
            from += rightward ? -0.1f : 0.1f;
        }

        AddArrowPath([0f, from, lineY, to], next);
        return true;
    }

    /// <summary>同一行、下一个节点比这个节点矮时，横向走到下一个节点正下方再向上。</summary>
    private bool TryHorizontalThenUp(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        float low = Math.Max(node.Y + 0.25f, next.Y + next.Height + 0.25f);
        float high = node.Y + node.Height - 0.25f;
        foreach (IntentGraphStateNode between in NodesBetween(node, next))
        {
            low = Math.Max(low, between.Y + between.Height + 0.2f);
        }

        if (high < low)
        {
            return false;
        }

        float lineY = (low + high) / 2f;
        if (_hLineTarget.ContainsKey(lineY))
        {
            return false;
        }

        _hLineTarget[lineY] = next;
        bool rightward = node.X < next.X;
        float from = rightward ? node.X + node.Width : node.X;
        if (node.Parent != null)
        {
            from += rightward ? -0.1f : 0.1f;
        }

        AddArrowPath([0f, from, lineY, next.X + next.Width / 2f, next.Y + next.Height], next);
        return true;
    }

    /// <summary>同一行的其他情况：从节点右侧出发，向下绕到下一个节点底部。</summary>
    private void AddSameRowDetour(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        float lineX = node.ArrowRight;
        while (_vLineTarget.TryGetValue(lineX, out IntentGraphStateNode? target) && target != next)
        {
            lineX += 0.25f;
        }

        float lineY = next.XIndex <= node.XIndex ? node.ArrowBottom : 0f;
        foreach (IntentGraphStateNode between in NodesBetween(node, next))
        {
            lineY = Math.Max(lineY, between.Y + between.Height + 0.25f);
        }

        lineY = Math.Max(lineY, next.Y + next.Height + 0.25f);
        while (_hLineTarget.TryGetValue(lineY, out IntentGraphStateNode? target) && target != next)
        {
            lineY += 0.25f;
        }

        _vLineTarget[lineX] = next;
        _hLineTarget[lineY] = next;
        float fromX = node.X + node.Width - (node.Parent != null && node.Children == null ? 0.1f : 0f);
        AddArrowPath([0f, fromX, node.Y + node.Height / 2f, lineX, lineY, next.X + next.Width / 2f, next.Y + next.Height], next);
        _graph.WidthUnits = Math.Max(_graph.WidthUnits, lineX);
        _graph.HeightUnits = Math.Max(_graph.HeightUnits, lineY);
    }

    /// <summary>下一个节点在上一行、横向有重叠时竖直向上直连。</summary>
    private bool TryVerticalStraight(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        if (node.Parent != null && !node.Parent.HorizontalLayout)
        {
            return false;
        }

        float low = Math.Max(node.X + 0.25f, next.X + 0.25f);
        float high = Math.Min(node.X + node.Width - 0.25f, next.X + next.Width - 0.25f);
        if (low > high)
        {
            return false;
        }

        float[] path = [1f, (low + high) / 2f, node.Y, next.Y + next.Height];
        AddArrowPath(path, next, rowSegments: [(node.YIndex, 0, 3), (next.YIndex, 3, 1)]);
        return true;
    }

    /// <summary>下一个节点在别的行：从节点顶部向上，到本行上方的横线再转向下一个节点底部；本行上方没有空位时整行下移 0.25 格。</summary>
    private bool TryVerticalFromTop(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        if (node.Parent != null && !node.Parent.HorizontalLayout)
        {
            return false;
        }

        float lineY = ReserveLineAbove(node, next);
        float[] path = [1f, node.X + node.Width / 2f, node.Y, lineY, next.X + next.Width / 2f, next.Y + next.Height];
        AddArrowPath(path, next, rowSegments: [(node.YIndex, 0, 3), (RowIndexAt(lineY), 3, 2), (next.YIndex, 5, 1)]);
        return true;
    }

    /// <summary>下一个节点在别的行、不能从顶部出发时：从节点右侧出发，向上到本行上方的横线，再到下一个节点底部。</summary>
    private void AddOtherRowDetour(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        float lineX = node.ArrowRight;
        while (_vLineTarget.TryGetValue(lineX, out IntentGraphStateNode? target) && target != next)
        {
            lineX += 0.25f;
        }

        float lineY = ReserveLineAbove(node, next);
        _vLineTarget[lineX] = next;
        float fromX = node.X + node.Width - (node.Parent != null && node.Children == null ? 0.1f : 0f);
        float[] path = [0f, fromX, node.Y + node.Height / 2f, lineX, lineY, next.X + next.Width / 2f, next.Y + next.Height];
        AddArrowPath(path, next, rowSegments: [(node.YIndex, 0, 4), (RowIndexAt(lineY), 4, 2), (next.YIndex, 6, 1)]);
        _graph.WidthUnits = Math.Max(_graph.WidthUnits, lineX);
    }

    /// <summary>
    /// 本行上方通往 next 的横线：已有通往同一节点的横线就复用，否则把本行（及之后记录的横线）下移 0.25 格，
    /// 空出来的位置就是横线。
    /// </summary>
    private float ReserveLineAbove(IntentGraphStateNode node, IntentGraphStateNode next)
    {
        float? existing = _hLineTarget.Where(p => p.Value == next).Select(static p => (float?)p.Key).OrderBy(static k => k).FirstOrDefault();
        if (existing.HasValue)
        {
            return existing.Value;
        }

        Row row = _rows[node.YIndex];
        float lineY = row.Y;
        const float shift = 0.25f;
        _y += shift;
        _graph.HeightUnits += shift;
        row.MoveY(this, shift);
        foreach ((float key, IntentGraphStateNode value) in _hLineTarget.Where(p => p.Key > lineY).ToList())
        {
            _hLineTarget.Remove(key);
            _hLineTarget[key + shift] = value;
        }

        _hLineTarget[lineY] = next;
        return lineY;
    }

    private IEnumerable<IntentGraphStateNode> NodesBetween(IntentGraphStateNode a, IntentGraphStateNode b)
    {
        for (int i = Math.Min(a.XIndex, b.XIndex) + 1; i < Math.Max(a.XIndex, b.XIndex); i++)
        {
            if (_indexToNode.TryGetValue((i, a.YIndex), out IntentGraphStateNode? node))
            {
                yield return node;
            }
        }
    }

    private int RowIndexAt(float y)
    {
        int result = 0;
        for (int i = 0; _rows.TryGetValue(i, out Row? row); i++)
        {
            if (row.Y >= y)
            {
                break;
            }

            result = i;
        }

        return result;
    }

    /// <summary>
    /// 路径格式：[起始方向（0 横 1 竖）, 起点 x, 起点 y, 之后横竖交替给出下一段终点的 x 或 y]。
    /// rowSegments 记下路径的哪些分量属于哪一行，行整体下移时一起移动；默认整条路径随目标所在行移动。
    /// </summary>
    private void AddArrowPath(float[] path, IntentGraphStateNode target, (int Row, int Start, int Length)[]? rowSegments = null)
    {
        _arrows.Add(path);
        _arrowTarget[path] = target;
        foreach ((int rowIndex, int start, int length) in rowSegments ?? [(target.YIndex, 0, path.Length)])
        {
            if (_rows.TryGetValue(rowIndex, out Row? row))
            {
                row.Arrows.Add((path, start, length));
            }
        }
    }

    /// <summary>把同向、相互重叠的平行线段错开 0.15 格；通往同一节点、终点相同的线段合成一条。</summary>
    private void SpreadOverlappingArrows()
    {
        for (int i = 0; i < _arrows.Count; i++)
        {
            float[] a = _arrows[i];
            for (int j = i + 1; j < _arrows.Count; j++)
            {
                float[] b = _arrows[j];
                bool sameTarget = _arrowTarget[a] == _arrowTarget[b];
                foreach ((bool aHorizontal, Vector2 aStart, Vector2 aEnd, int aIndex) in Segments(a))
                {
                    foreach ((bool bHorizontal, Vector2 bStart, Vector2 bEnd, int bIndex) in Segments(b))
                    {
                        if (aHorizontal != bHorizontal)
                        {
                            continue;
                        }

                        if (aHorizontal && Math.Abs(aStart.Y - bStart.Y) < 0.2f)
                        {
                            if (sameTarget && Math.Abs(aEnd.X - bEnd.X) < 0.001f)
                            {
                                float merged = (aStart.Y + bStart.Y) / 2f;
                                a[aIndex] = merged;
                                b[bIndex] = merged;
                                continue;
                            }

                            if (Math.Max(Math.Min(aStart.X, aEnd.X), Math.Min(bStart.X, bEnd.X)) < Math.Min(Math.Max(aStart.X, aEnd.X), Math.Max(bStart.X, bEnd.X)))
                            {
                                float mid = (aStart.Y + bStart.Y) / 2f;
                                a[aIndex] = mid + (aStart.X < bStart.X ? -0.15f : 0.15f);
                                b[bIndex] = mid + (aStart.X < bStart.X ? 0.15f : -0.15f);
                            }
                        }

                        if (aHorizontal || Math.Abs(aStart.X - bStart.X) >= 0.12f)
                        {
                            continue;
                        }

                        if (sameTarget && Math.Abs(aEnd.Y - bEnd.Y) < 0.001f)
                        {
                            float merged = (aStart.X + bStart.X) / 2f;
                            a[aIndex] = merged;
                            b[bIndex] = merged;
                            continue;
                        }

                        if (Math.Max(Math.Min(aStart.Y, aEnd.Y), Math.Min(bStart.Y, bEnd.Y)) < Math.Min(Math.Max(aStart.Y, aEnd.Y), Math.Max(bStart.Y, bEnd.Y)))
                        {
                            float mid = (aStart.X + bStart.X) / 2f;
                            a[aIndex] = mid + (aStart.Y < bStart.Y ? -0.15f : 0.15f);
                            b[bIndex] = mid + (aStart.Y < bStart.Y ? 0.15f : -0.15f);
                        }
                    }
                }
            }
        }
    }

    /// <summary>逐段给出（是否横向, 起点, 终点, 决定这段位置的分量下标：横段是 y 分量，竖段是 x 分量）。</summary>
    private static IEnumerable<(bool Horizontal, Vector2 Start, Vector2 End, int Index)> Segments(float[] path)
    {
        bool horizontal = path[0] == 0f;
        float x = path[1];
        float y = path[2];
        int xIndex = 1;
        int yIndex = 2;
        for (int i = 3; i < path.Length; i++)
        {
            if (horizontal)
            {
                yield return (true, new Vector2(x, y), new Vector2(path[i], y), yIndex);
                x = path[i];
                xIndex = i;
            }
            else
            {
                yield return (false, new Vector2(x, y), new Vector2(x, path[i]), xIndex);
                y = path[i];
                yIndex = i;
            }

            horizontal = !horizontal;
        }
    }

    public static List<Vector2> ToPoints(IReadOnlyList<float> path)
    {
        List<Vector2> points = new List<Vector2>();
        if (path.Count < 4)
        {
            return points;
        }

        bool horizontal = path[0] == 0f;
        Vector2 current = new Vector2(path[1], path[2]);
        points.Add(current);
        for (int i = 3; i < path.Count; i++)
        {
            current = horizontal ? new Vector2(path[i], current.Y) : new Vector2(current.X, path[i]);
            points.Add(current);
            horizontal = !horizontal;
        }

        return points;
    }

    /// <summary>一个初始状态展开出的一行；行整体下移时，行内的招式、标签、分组和记下的连线分量一起移动。</summary>
    private sealed class Row
    {
        public float Y { get; set; }

        public List<int> MoveIndices { get; } = new List<int>();

        public List<int> LabelIndices { get; } = new List<int>();

        public List<int> GroupIndices { get; } = new List<int>();

        public List<(float[] Path, int Start, int Length)> Arrows { get; } = new List<(float[] Path, int Start, int Length)>();

        public List<IntentGraphStateNode> Nodes { get; } = new List<IntentGraphStateNode>();

        public void MoveY(IntentGraphLayouter layouter, float offset)
        {
            Y += offset;
            IntentGraphRenderModel graph = layouter._graph;
            foreach (int index in MoveIndices)
            {
                IntentGraphMoveNode move = graph.Moves[index];
                move.PositionUnits = new Vector2(move.PositionUnits.X, move.PositionUnits.Y + offset);
            }

            foreach (int index in LabelIndices)
            {
                IntentGraphLabelNode label = graph.Labels[index];
                graph.Labels[index] = label with { PositionUnits = label.PositionUnits + new Vector2(0f, offset) };
            }

            foreach (int index in GroupIndices)
            {
                Rect2 rect = graph.Groups[index].RectUnits;
                rect.Position += new Vector2(0f, offset);
                graph.Groups[index] = new IntentGraphGroupNode(rect);
            }

            foreach (IntentGraphStateNode node in Nodes)
            {
                node.Y += offset;
            }

            foreach ((float[] path, int start, int length) in Arrows)
            {
                if (start <= 2 && 2 < start + length)
                {
                    path[2] += offset;
                }

                for (int i = Math.Max(3, start); i < start + length; i++)
                {
                    // 横向起始的路径奇数下标是 x，偶数是 y；竖向起始相反。
                    if (i % 2 == (int)path[0])
                    {
                        path[i] += offset;
                    }
                }
            }
        }
    }
}
