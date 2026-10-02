using Godot;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.features.intentgraph;

/// <summary>
/// 放在 Intent Graph 面板里、紧挨着它的图节点：定时生成本模组的意图图并显示，同时隐藏它的图；
/// 生成失败时恢复显示它的图。面板本身（位置、固定、拖动、缩放、快捷键）仍由 Intent Graph 管理，
/// 面板被它释放时本节点随之释放。
/// </summary>
public partial class NLibraryIntentGraphSwap : Node
{
    private const string GraphScenePath = "res://LibraryOfRuina/intentgraph/scenes/intent_graph.tscn";
    private const string LibraryGraphNodeName = "LibraryOfRuinaIntentGraph";
    private const string SwapNodeName = "LibraryOfRuinaIntentGraphSwap";
    // Intent Graph 面板里的节点名与它的图的缩放属性。
    private const string WorkshopGraphNodePath = "%IntentGraph";
    private const string WorkshopOutdatedMarkNodePath = "%OutdatedMarkContainer";
    private const string WorkshopGraphScaleProperty = "GraphScale";
    // 与本模组原面板相同的刷新间隔，跟上当前招式的变化。
    private const float RefreshIntervalSeconds = 0.15f;

    private Control _panel = null!;
    private Control _workshopGraph = null!;
    private NIntentGraph _libraryGraph = null!;
    private NCreature? _creature;
    private float _timeSinceRefresh;
    private bool _showingLibraryGraph;
    private bool _outdatedMarkWasVisible;

    /// <summary>在 Intent Graph 的面板里放入（或复用）本模组的图并立即刷新。</summary>
    internal static void Attach(Control panel, NCreature creature)
    {
        Control? workshopGraph = panel.GetNodeOrNull<Control>(WorkshopGraphNodePath);
        if (workshopGraph?.GetParent() is not Node graphParent)
        {
            LorLog.WarnOnce(
                "IntentGraph.WorkshopGraphMissing",
                "[LibraryOfRuina.IntentGraph] Intent Graph panel has no %IntentGraph node; its graph is left unchanged.");
            return;
        }

        if (graphParent.GetNodeOrNull<NIntentGraph>(LibraryGraphNodeName)?.GetNodeOrNull<NLibraryIntentGraphSwap>(SwapNodeName)
            is { } existing)
        {
            existing._creature = creature;
            existing.RefreshNow();
            return;
        }

        if (ResourceLoader.Load<PackedScene>(GraphScenePath)?.Instantiate() is not NIntentGraph libraryGraph)
        {
            LorLog.WarnOnce(
                "IntentGraph.LibraryGraphSceneMissing",
                "[LibraryOfRuina.IntentGraph] Failed to instantiate " + GraphScenePath + ".");
            return;
        }

        libraryGraph.Name = LibraryGraphNodeName;
        libraryGraph.SizeFlagsHorizontal = workshopGraph.SizeFlagsHorizontal;
        libraryGraph.SizeFlagsVertical = workshopGraph.SizeFlagsVertical;
        libraryGraph.Visible = false;
        graphParent.AddChild(libraryGraph);
        graphParent.MoveChild(libraryGraph, workshopGraph.GetIndex() + 1);

        NLibraryIntentGraphSwap swap = new NLibraryIntentGraphSwap
        {
            Name = SwapNodeName,
            _panel = panel,
            _workshopGraph = workshopGraph,
            _libraryGraph = libraryGraph,
            _creature = creature,
        };
        libraryGraph.AddChild(swap);
        swap.RefreshNow();
    }

    public override void _Process(double delta)
    {
        _timeSinceRefresh += (float)delta;
        if (_timeSinceRefresh >= RefreshIntervalSeconds)
        {
            RefreshNow();
        }
    }

    private void RefreshNow()
    {
        _timeSinceRefresh = 0f;
        if (!IsInstanceValid(_panel) || !IsInstanceValid(_workshopGraph))
        {
            return;
        }

        Creature? owner = _creature != null && IsInstanceValid(_creature) ? _creature.Entity : null;
        if (owner == null
            || !NMonsterIntentGraphPanel.TryBuildRenderModel(owner, out IntentGraphRenderModel renderModel, out _, out _))
        {
            ShowWorkshopGraph();
            return;
        }

        Vector2 sizeBefore = _libraryGraph.CustomMinimumSize;
        _libraryGraph.GraphScale = ReadWorkshopGraphScale();
        _libraryGraph.SetRenderModel(renderModel);
        bool sizeChanged = sizeBefore != _libraryGraph.CustomMinimumSize;
        if (!_showingLibraryGraph)
        {
            _showingLibraryGraph = true;
            _workshopGraph.Visible = false;
            _libraryGraph.Visible = true;
            HideWorkshopOutdatedMark();
            sizeChanged = true;
        }

        if (sizeChanged)
        {
            // 面板按新尺寸重新排版；它挂在 Resized 上的定位逻辑据此重新摆放面板。
            _panel.ResetSize();
        }
    }

    private void ShowWorkshopGraph()
    {
        if (!_showingLibraryGraph)
        {
            return;
        }

        _showingLibraryGraph = false;
        _libraryGraph.Visible = false;
        _workshopGraph.Visible = true;
        if (_panel.GetNodeOrNull<Control>(WorkshopOutdatedMarkNodePath) is { } outdatedMark)
        {
            outdatedMark.Visible = _outdatedMarkWasVisible;
        }

        _panel.ResetSize();
    }

    // 过期提示说的是 Intent Graph 自己的图，换成本模组的图时一并隐藏。
    private void HideWorkshopOutdatedMark()
    {
        if (_panel.GetNodeOrNull<Control>(WorkshopOutdatedMarkNodePath) is not { } outdatedMark)
        {
            return;
        }

        _outdatedMarkWasVisible = outdatedMark.Visible;
        outdatedMark.Visible = false;
    }

    private Vector2 ReadWorkshopGraphScale()
    {
        Variant scale = _workshopGraph.Get(WorkshopGraphScaleProperty);
        return scale.VariantType == Variant.Type.Vector2 ? scale.AsVector2() : Vector2.One;
    }
}
