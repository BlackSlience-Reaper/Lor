using System;
using System.Linq;
using Godot;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.features.intentgraph;

public partial class NMonsterIntentGraphPanel : MarginContainer
{
    private const string ToggleAction = "INTENTGRAPH2-TOGGLE_INTENT_GRAPH_KEY";
    private const float RefreshIntervalSeconds = 0.15f;

    private static bool _toggleActionRegistered;
    private static bool _isGraphEnabled = true;

    private Label _monsterName = null!;
    private NIntentGraph _intentGraph = null!;

    private NCreature? _creature;
    private float _timeSinceRefresh;

    public bool HasRenderableGraph { get; private set; }

    internal string LastHiddenReason { get; private set; } = "none";

    public override void _Ready()
    {
        EnsureNodesResolved();
        RegisterToggleActionOnce();
    }

    public override void _Process(double delta)
    {
        if (!InputMap.HasAction(ToggleAction))
        {
            RegisterToggleActionOnce();
        }

        if (InputMap.HasAction(ToggleAction) && Input.IsActionJustPressed(ToggleAction))
        {
            _isGraphEnabled = !_isGraphEnabled;
        }

        if (!_isGraphEnabled)
        {
            HideGraph("overlay-disabled-by-toggle");
            return;
        }

        _timeSinceRefresh += (float)delta;
        if (_timeSinceRefresh >= RefreshIntervalSeconds)
        {
            RefreshNow();
        }
    }

    public void BindCreature(NCreature creature)
    {
        _creature = creature;
    }

    public void RefreshNow()
    {
        _timeSinceRefresh = 0f;

        if (!EnsureNodesResolved())
        {
            HideGraph("missing-panel-nodes");
            return;
        }

        if (_creature == null || !IsInstanceValid(_creature))
        {
            HideGraph("no-bound-creature");
            return;
        }

        Creature? owner = _creature.Entity;
        if (owner == null)
        {
            HideGraph("no-bound-creature-entity");
            return;
        }

        if (owner.Monster == null)
        {
            HideGraph("no-bound-monster");
            return;
        }

        if (owner.CombatState == null)
        {
            HideGraph("no-combat-state");
            return;
        }

        if (!TryBuildRenderModel(owner, out IntentGraphRenderModel renderModel, out string monsterName, out string failureReason))
        {
            HideGraph(failureReason);
            return;
        }

        LastHiddenReason = "none";
        _monsterName.Text = string.IsNullOrWhiteSpace(monsterName) ? "Monster" : monsterName;
        _intentGraph.SetRenderModel(renderModel);
        HasRenderableGraph = true;
        Visible = true;
    }

    /// <summary>为怪物生成意图图的绘制数据：先查缓存，再按状态机生成，失败时退回只画当前招式。</summary>
    internal static bool TryBuildRenderModel(
        Creature owner,
        out IntentGraphRenderModel renderModel,
        out string monsterName,
        out string failureReason)
    {
        renderModel = new IntentGraphRenderModel();
        monsterName = string.Empty;
        failureReason = string.Empty;
        if (owner.Monster == null || owner.CombatState == null)
        {
            failureReason = owner.Monster == null ? "no-bound-monster" : "no-combat-state";
            return false;
        }

        bool graphBuilt = MonsterIntentGraphRuntimeCache.TryGet(owner, out renderModel, out monsterName);
        if (!graphBuilt)
        {
            try
            {
                graphBuilt = MonsterStateMachineIntentGraphFeature.TryBuild(owner, out renderModel, out monsterName);
                if (graphBuilt)
                {
                    MonsterIntentGraphRuntimeCache.Store(owner.Monster, renderModel, monsterName);
                }
            }
            catch (Exception exception)
            {
                graphBuilt = false;
                renderModel = new IntentGraphRenderModel();
                monsterName = string.Empty;

                string monsterTypeName = owner.Monster.GetType().FullName ?? owner.Monster.GetType().Name;
                if (LorLog.FirstTime("IntentGraph.TryBuildException:" + monsterTypeName))
                {
                    LorLog.Warn("[LibraryOfRuina.IntentGraph] TryBuild threw for " + monsterTypeName
                             + ", using current-move fallback. " + exception);
                }
            }
        }

        if (!graphBuilt
            && !TryBuildSingleMoveFallback(owner, out renderModel, out monsterName, out string fallbackFailureReason))
        {
            string reason = string.IsNullOrWhiteSpace(fallbackFailureReason)
                ? "state-machine-trybuild-failed-and-current-move-fallback-failed"
                : "state-machine-trybuild-failed-and-current-move-fallback-failed:" + fallbackFailureReason;
            failureReason = reason;
            return false;
        }

        return true;
    }

    private static bool TryBuildSingleMoveFallback(
        Creature owner,
        out IntentGraphRenderModel renderModel,
        out string monsterName,
        out string failureReason)
    {
        renderModel = new IntentGraphRenderModel();
        monsterName = string.Empty;
        failureReason = string.Empty;

        if (owner.Monster == null)
        {
            failureReason = "no-bound-monster";
            return false;
        }

        if (owner.CombatState == null)
        {
            failureReason = "no-combat-state";
            return false;
        }

        if (owner.Monster.NextMove == null)
        {
            failureReason = "no-next-move";
            return false;
        }

        string monsterTypeName = owner.Monster.GetType().FullName ?? owner.Monster.GetType().Name;
        if (LorLog.FirstTime("IntentGraph.Fallback:" + monsterTypeName))
        {
            LorLog.Warn("[LibraryOfRuina.IntentGraph] State-machine graph unavailable for " + monsterTypeName
                     + ", using current-move fallback.");
        }

        List<IntentGraphIntentIcon> icons = new List<IntentGraphIntentIcon>();
        IReadOnlyList<Creature> targets = owner.CombatState.Players.Select(static p => p.Creature).ToList();
        foreach (var intent in owner.Monster.NextMove.Intents)
        {
            string intentTypeName = intent.GetType().FullName ?? intent.GetType().Name;
            string keyPrefix = monsterTypeName + "::" + intentTypeName;

            string? text = null;
            try
            {
                text = intent.GetIntentLabel(targets, owner).GetFormattedText();
            }
            catch (Exception exception)
            {
                if (LorLog.FirstTime("IntentGraph.FallbackResolve:" + keyPrefix + ":label"))
                {
                    LorLog.Warn("[LibraryOfRuina.IntentGraph] Failed to resolve intent label for " + keyPrefix
                             + " in current-move fallback. " + exception.Message);
                }
            }

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
                if (LorLog.FirstTime("IntentGraph.FallbackResolve:" + keyPrefix + ":texture"))
                {
                    LorLog.Warn("[LibraryOfRuina.IntentGraph] Failed to resolve intent texture for " + keyPrefix
                             + " in current-move fallback. " + exception.Message);
                }
            }

            icons.Add(new IntentGraphIntentIcon(texture, text, intent.IntentType));
        }

        renderModel.Moves.Add(new IntentGraphMoveNode
        {
            MoveId = owner.Monster.NextMove.Id,
            PositionUnits = Vector2.Zero,
            Intents = icons,
            IsCurrentMove = true
        });
        renderModel.WidthUnits = IntentGraphLayouter.MoveWidth(icons.Count);
        renderModel.HeightUnits = 1f;
        monsterName = owner.Monster.Title.GetFormattedText();
        return true;
    }

    private void HideGraph(string reason)
    {
        LastHiddenReason = reason;
        HasRenderableGraph = false;
        Visible = false;
    }

    private bool EnsureNodesResolved()
    {
        if (_monsterName == null)
        {
            _monsterName = GetNodeOrNull<Label>("%MonsterName")
                           ?? GetNodeOrNull<Label>("MonsterName")
                           ?? FindChild("MonsterName", true, false) as Label
                           ?? null!;
        }

        if (_intentGraph == null)
        {
            _intentGraph = GetNodeOrNull<NIntentGraph>("%IntentGraph")
                           ?? GetNodeOrNull<NIntentGraph>("IntentGraph")
                           ?? FindChild("IntentGraph", true, false) as NIntentGraph
                           ?? null!;
        }

        return _monsterName != null && _intentGraph != null;
    }

    private static void RegisterToggleActionOnce()
    {
        if (_toggleActionRegistered)
        {
            return;
        }

        _toggleActionRegistered = true;
        if (InputMap.HasAction(ToggleAction))
        {
            return;
        }

        InputMap.AddAction(ToggleAction);
        InputEventKey eventKey = new InputEventKey
        {
            PhysicalKeycode = Key.G
        };
        InputMap.ActionAddEvent(ToggleAction, eventKey);
    }
}
