using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Entities.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.assets;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.ui.DamagePreview;

[HarmonyPatch(typeof(NCombatRoom), "OnCombatSetUp")]
internal static class DamagePreviewOverlayPatch
{
    private static void Postfix(NCombatRoom __instance, CombatState state)
    {
        __instance.Ui.AddChild(new DamagePreviewOverlay(state));
    }
}

/// <summary>战斗 UI 拥有提示的整个生命周期，既不注册存档状态，也不参与网络同步。</summary>
internal sealed partial class DamagePreviewOverlay : Control
{
    // 预览最多每秒重算五次，移动与 Alt 输入仍逐帧响应。
    private const double RefreshInterval = 0.2;
    private const float TipWidth = 520f;
    private const float DetailedWidth = 720f;
    private const float MaximumDetailedHeight = 500f;
    // 原版提示背景下沿有额外装饰区域，下方多留空间，使可见留白平衡。
    private const int TopPadding = 24;
    private const int BottomPadding = 36;
    private const float VerticalMargins = TopPadding + BottomPadding;
    private const float AnchorGap = 12f;
    private const int TextSize = 24;
    private const float TextMargins = 48f;
    // 简洁伤害列保留短范围数值的空间，提示紧随其后，避免挤到右边框。
    private const float MinimumSummaryWidth = 100f;
    private const float HintGap = 16f;
    // 受伤预览只显示两行数值，面板按内容收窄，保留原版提示背景可正常绘制的最小宽度。
    private const float IncomingMinimumWidth = 150f;

    // 移动端没有 Alt 键，只显示简洁预览，不提示键盘切换。
    private static readonly bool HasModeHotkey = !OS.HasFeature("mobile");

    private readonly CombatState _combat;
    private readonly Dictionary<LibraryCreature, PreviewTip> _tips = [];
    private readonly Dictionary<Creature, PreviewTip> _incomingTips = [];
    private readonly List<Rect2> _incomingRects = [];
    private IReadOnlyDictionary<Creature, IncomingDamagePreviewResult> _incomingResults =
        new Dictionary<Creature, IncomingDamagePreviewResult>();
    private double _untilIncomingRefresh;
    private bool _incomingDirty = true;
    private ActionExecutor? _actionExecutor;
    private readonly HashSet<Creature> _observedCreatures = [];
    private readonly HashSet<PowerModel> _observedPowers = [];
    private static readonly IReadOnlyDictionary<Creature, IncomingDamagePreviewResult> EmptyIncomingResults =
        new Dictionary<Creature, IncomingDamagePreviewResult>();
    private bool _reportedIncomingError;
    private CardModel? _lastCard;
    private LibraryCreature? _lastTarget;
    private double _untilRefresh;
    private PreviewMode _mode;

    private bool IsDetailed => _mode == PreviewMode.Detailed;

    private bool ShowModeHint => HasModeHotkey && !IsDetailed;

    private bool HasIncomingPreview => _incomingResults.Count > 0;

    private enum PreviewMode
    {
        Simple,
        Detailed,
        Off
    }
    private readonly HashSet<Type> _reportedErrors = [];

    internal DamagePreviewOverlay(CombatState combat)
    {
        _combat = combat;
        MouseFilter = MouseFilterEnum.Ignore;
        Name = "LibraryDamagePreviewOverlay";
    }

    public override void _EnterTree()
    {
        _actionExecutor = RunManager.Instance.ActionExecutor;
        _actionExecutor.AfterActionExecuted += OnActionExecuted;
        _combat.CreaturesChanged += OnCreaturesChanged;
        CombatManager.Instance.TurnStarted += OnTurnStarted;
        ObserveCreatures();
    }

    public override void _ExitTree()
    {
        if (_actionExecutor != null)
        {
            _actionExecutor.AfterActionExecuted -= OnActionExecuted;
            _actionExecutor = null;
        }

        _combat.CreaturesChanged -= OnCreaturesChanged;
        CombatManager.Instance.TurnStarted -= OnTurnStarted;
        foreach (Creature creature in _observedCreatures)
        {
            ObserveCreature(creature, false);
        }

        foreach (PowerModel power in _observedPowers)
        {
            power.DisplayAmountChanged -= MarkIncomingDirty;
        }

        _observedCreatures.Clear();
        _observedPowers.Clear();
    }

    private void OnActionExecuted(GameAction action) => MarkIncomingDirty();

    private void OnTurnStarted(CombatState combat)
    {
        if (ReferenceEquals(combat, _combat))
        {
            MarkIncomingDirty();
        }
    }

    private void OnCreaturesChanged(ICombatState combat)
    {
        ObserveCreatures();
        MarkIncomingDirty();
    }

    private void ObserveCreatures()
    {
        foreach (Creature removed in _observedCreatures.Where(creature => !_combat.ContainsCreature(creature)).ToArray())
        {
            ObserveCreature(removed, false);
            _observedCreatures.Remove(removed);
            foreach (PowerModel power in removed.Powers)
            {
                OnPowerRemoved(power);
            }
        }

        foreach (Creature creature in _combat.Creatures)
        {
            if (_observedCreatures.Add(creature))
            {
                ObserveCreature(creature, true);
                foreach (PowerModel power in creature.Powers)
                {
                    OnPowerApplied(power);
                }
            }
        }
    }

    private void ObserveCreature(Creature creature, bool subscribe)
    {
        if (subscribe)
        {
            creature.CurrentHpChanged += OnCreatureValueChanged;
            creature.BlockChanged += OnCreatureValueChanged;
            creature.MaxHpChanged += OnCreatureValueChanged;
            creature.PowerApplied += OnPowerApplied;
            creature.PowerRemoved += OnPowerRemoved;
            creature.PowerIncreased += OnPowerIncreased;
            creature.PowerDecreased += OnPowerDecreased;
            if (creature is LibraryCreature library)
            {
                library.CurrentChaoValueChanged += OnCreatureValueChanged;
                library.MaxChaoValueChanged += OnCreatureValueChanged;
            }
        }
        else
        {
            creature.CurrentHpChanged -= OnCreatureValueChanged;
            creature.BlockChanged -= OnCreatureValueChanged;
            creature.MaxHpChanged -= OnCreatureValueChanged;
            creature.PowerApplied -= OnPowerApplied;
            creature.PowerRemoved -= OnPowerRemoved;
            creature.PowerIncreased -= OnPowerIncreased;
            creature.PowerDecreased -= OnPowerDecreased;
            if (creature is LibraryCreature library)
            {
                library.CurrentChaoValueChanged -= OnCreatureValueChanged;
                library.MaxChaoValueChanged -= OnCreatureValueChanged;
            }
        }
    }

    private void OnCreatureValueChanged(int before, int after) => MarkIncomingDirty();

    private void OnPowerIncreased(PowerModel power, int amount, bool silent) => MarkIncomingDirty();

    private void OnPowerDecreased(PowerModel power, bool silent) => MarkIncomingDirty();

    private void OnPowerApplied(PowerModel power)
    {
        if (_observedPowers.Add(power))
        {
            power.DisplayAmountChanged += MarkIncomingDirty;
        }

        MarkIncomingDirty();
    }

    private void OnPowerRemoved(PowerModel power)
    {
        if (_observedPowers.Remove(power))
        {
            power.DisplayAmountChanged -= MarkIncomingDirty;
        }

        MarkIncomingDirty();
    }

    private void MarkIncomingDirty() => _incomingDirty = true;

    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Alt }
            && (_lastCard != null || HasIncomingPreview))
        {
            _mode = _mode switch
            {
                PreviewMode.Simple => PreviewMode.Detailed,
                PreviewMode.Detailed => PreviewMode.Off,
                _ => PreviewMode.Simple
            };
            _untilRefresh = 0;
            _untilIncomingRefresh = 0;
            _incomingDirty = true;
        }

        if (IsDetailed && input is InputEventMouseButton { Pressed: true } mouse
            && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            foreach (PreviewTip tip in _tips.Values.Concat(_incomingTips.Values))
            {
                if (tip.Root.Visible)
                {
                    VScrollBar scroll = tip.Label.GetVScrollBar();
                    scroll.Value += mouse.ButtonIndex == MouseButton.WheelUp ? -scroll.Page / 4f : scroll.Page / 4f;
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        UpdateIncomingTips(room, delta);
        UpdateCardTips(room, delta);
    }

    private void UpdateCardTips(NCombatRoom? room, double delta)
    {
        if (room == null || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsEnding
            || CombatManager.Instance.PlayerActionsDisabled
            || _combat.Players.Any(CombatManager.Instance.IsExecutingCardOrPotionEffect))
        {
            _lastCard = null;
            HideAll();
            return;
        }

        NPlayerHand hand = room.Ui.Hand;
        if (hand.IsInCardSelection || hand.PeekButton.IsPeeking)
        {
            _lastCard = null;
            HideAll();
            return;
        }
        NCardPlay? play = VanillaPrivate.PlayerHandCurrentCardPlay.Get(hand);
        CardModel? card = play != null && GodotObject.IsInstanceValid(play)
            ? play.Holder?.CardModel
            : null;
        NTargetManager targeting = NTargetManager.Instance;
        LibraryCreature? selectedEnemy = targeting.IsInSelection
            ? (VanillaPrivate.TargetManagerHoveredNode.Get(targeting) as NCreature)?.Entity as LibraryCreature
            : null;
        TargetType? targetType = card?.TargetType;
        bool fromSpeedDie = false;
        IReadOnlyList<Creature> speedDieTargets = Array.Empty<Creature>();
        // 手牌没有正在打出的卡时，悬停在已装配卡牌的速度骰子上同样触发预览：
        // 卡牌与目标取自骰子槽位，目标类型按速度骰子打出时的类型判断。
        if (card == null && !targeting.IsInSelection
            && DamagePreviewSpeedDieSource.TryGetHovered(room, _combat, out CardModel? dieCard, out Creature? dieTarget,
                out speedDieTargets)
            && dieCard != null)
        {
            card = dieCard;
            selectedEnemy = dieTarget as LibraryCreature;
            targetType = DamagePreviewSpeedDieSource.GetTargetType(dieCard);
            fromSpeedDie = true;
        }

        bool groupPreview = targetType == TargetType.AllEnemies;
        // 装配到骰子上的卡牌位于打出堆，其余情况仍要求卡牌在手牌中。
        bool inPreviewPile = card?.Pile?.Type == PileType.Hand
            || (fromSpeedDie && card?.Pile?.Type == PileType.Play);
        if (card == null || (selectedEnemy == null && !groupPreview && speedDieTargets.Count == 0) || !card.IsMutable
            || card.CombatState != _combat || !inPreviewPile)
        {
            _lastCard = null;
            HideAll();
            return;
        }

        bool refresh = !ReferenceEquals(card, _lastCard) || !ReferenceEquals(selectedEnemy, _lastTarget)
            || (_untilRefresh -= delta) <= 0;
        _lastCard = card;
        _lastTarget = selectedEnemy;
        if (refresh)
        {
            _untilRefresh = RefreshInterval;
        }

        if (_mode == PreviewMode.Off)
        {
            HideAll();
            return;
        }

        List<Rect2> occupied = [.. _incomingRects];
        IReadOnlyList<Creature> hittableEnemies = _combat.HittableEnemies;
        foreach (LibraryCreature enemy in _combat.Enemies.OfType<LibraryCreature>())
        {
            NCreature? creatureNode = room.GetCreatureNode(enemy);
            // 骰子的选敌类型可能仍为单体；完整命中列表由同一目标线分发入口给出。
            bool isPreviewTarget = fromSpeedDie
                ? speedDieTargets.Contains(enemy)
                : ReferenceEquals(enemy, selectedEnemy)
                    || (groupPreview && hittableEnemies.Contains(enemy)
                        && creatureNode != null && GodotObject.IsInstanceValid(creatureNode)
                        && VanillaPrivate.CreatureIsInMultiselect.Get(creatureNode));
            if (!isPreviewTarget)
            {
                if (_tips.TryGetValue(enemy, out PreviewTip? unselected))
                {
                    unselected.Root.Hide();
                }
                continue;
            }

            if (enemy.Monster?.GetType().Assembly != typeof(DamagePreviewOverlay).Assembly)
            {
                continue;
            }

            if (enemy.IsDead || creatureNode == null || !GodotObject.IsInstanceValid(creatureNode)
                || !creatureNode.IsVisibleInTree())
            {
                if (_tips.TryGetValue(enemy, out PreviewTip? hidden))
                {
                    hidden.Root.Hide();
                }
                continue;
            }

            if (!_tips.TryGetValue(enemy, out PreviewTip? tip))
            {
                tip = CreateTip();
                _tips.Add(enemy, tip);
                refresh = true;
            }

            if (refresh || !tip.Root.Visible)
            {
                DamagePreviewResult? result;
                try
                {
                    result = DamagePreviewCalculator.Calculate(card, enemy);
                }
                catch (Exception error)
                {
                    if (_reportedErrors.Add(card.GetType()))
                    {
                        Log.Warn($"Damage preview failed for {card.Id}: {error}");
                    }
                    result = new DamagePreviewResult("?", "?");
                }
                tip.Root.Visible = result != null;
                if (result != null)
                {
                    string content = IsDetailed
                        ? result.Details
                        : result.Summary;
                    if (tip.Label.Text != content)
                    {
                        tip.Label.Text = content;
                    }

                    tip.Hint.Visible = ShowModeHint;
                    tip.Label.AutowrapMode = IsDetailed ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
                    tip.Label.FitContent = false;
                    tip.Label.AddThemeFontSizeOverride("normal_font_size", TextSize);
                    tip.Label.AddThemeFontSizeOverride("bold_font_size", TextSize);
                }
            }

            if (tip.Root.Visible)
            {
                LayoutTip(tip, creatureNode.Hitbox, occupied);
            }
        }

        foreach (LibraryCreature removed in _tips.Keys.Where(enemy => !_combat.Enemies.Contains(enemy)).ToArray())
        {
            _tips[removed].Root.QueueFree();
            _tips.Remove(removed);
        }
    }

    /// <summary>
    /// 玩家回合内在本地玩家与友方盟友的生命条右侧显示下一次敌方回合的受伤预览。
    /// 动作结算期间沿用上次结果，结算结束后按最新状态重算，避免在受伤修正与其后置回调之间读取模型。
    /// </summary>
    private void UpdateIncomingTips(NCombatRoom? room, double delta)
    {
        _incomingRects.Clear();
        // 关闭显示同时停止模拟；保留缓存以便 Alt 能重新打开预览。
        if (_mode == PreviewMode.Off)
        {
            HideIncoming();
            return;
        }

        if (room == null || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsEnding
            || _combat.CurrentSide != CombatSide.Player
            || room.Ui.Hand.IsInCardSelection || room.Ui.Hand.PeekButton.IsPeeking)
        {
            _incomingResults = EmptyIncomingResults;
            _incomingDirty = true;
            HideIncoming();
            return;
        }

        bool isResolving = CombatManager.Instance.PlayerActionsDisabled
            || RunManager.Instance.ActionExecutor.IsRunning
            || _combat.Players.Any(CombatManager.Instance.IsExecutingCardOrPotionEffect);
        _untilIncomingRefresh -= delta;
        // 战斗动作、生命、格挡与能力变化才使缓存失效，静止画面不再每秒模拟五遍。
        bool refresh = _incomingDirty && !isResolving && _untilIncomingRefresh <= 0;
        if (refresh)
        {
            _untilIncomingRefresh = RefreshInterval;
            _incomingResults = CalculateIncoming();
            _incomingDirty = false;
        }

        if (refresh)
        {
            foreach ((Creature creature, PreviewTip existing) in _incomingTips.ToArray())
            {
                if (!_incomingResults.ContainsKey(creature))
                {
                    existing.Root.Hide();
                }

                if (!_combat.ContainsCreature(creature))
                {
                    existing.Root.QueueFree();
                    _incomingTips.Remove(creature);
                }
            }
        }

        foreach ((Creature creature, IncomingDamagePreviewResult result) in _incomingResults)
        {
            Control? hpBar = FindHpBar(room.GetCreatureNode(creature));
            if (creature.IsDead || hpBar == null || !GodotObject.IsInstanceValid(hpBar) || !hpBar.IsVisibleInTree())
            {
                if (_incomingTips.TryGetValue(creature, out PreviewTip? hidden))
                {
                    hidden.Root.Hide();
                }

                continue;
            }

            if (!_incomingTips.TryGetValue(creature, out PreviewTip? tip))
            {
                tip = CreateTip();
                _incomingTips.Add(creature, tip);
            }

            string content = IsDetailed ? result.Details : result.Summary;
            if (tip.Label.Text != content)
            {
                tip.Label.Text = content;
            }

            var wrap = IsDetailed ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            if (tip.Label.AutowrapMode != wrap)
            {
                tip.Label.AutowrapMode = wrap;
            }
            tip.Root.Show();
            LayoutIncomingTip(tip, hpBar);
        }
    }

    private IReadOnlyDictionary<Creature, IncomingDamagePreviewResult> CalculateIncoming()
    {
        Creature? localPlayer = LocalContextCompat.GetMe(_combat)?.Creature;
        List<Creature> targets = [];
        if (localPlayer is { IsAlive: true })
        {
            targets.Add(localPlayer);
        }

        targets.AddRange(_combat.Enemies.Where(static creature =>
            creature.IsAlive && AllyTurnRegistry.IsFriendlyAlly(creature)));
        if (targets.Count == 0)
        {
            return new Dictionary<Creature, IncomingDamagePreviewResult>();
        }

        try
        {
            return IncomingDamagePreviewCalculator.Calculate(_combat, localPlayer, targets, IsDetailed);
        }
        catch (Exception error)
        {
            if (!_reportedIncomingError)
            {
                _reportedIncomingError = true;
                Log.Warn($"Incoming damage preview failed: {error}");
            }

            return new Dictionary<Creature, IncomingDamagePreviewResult>();
        }
    }

    /// <summary>受伤预览优先贴在生命条右侧并垂直居中，与其他受伤预览重叠时改贴生命条上下沿或左侧。</summary>
    private void LayoutIncomingTip(PreviewTip tip, Control hpBar)
    {
        Rect2 safe = GetSafeArea(out Transform2D viewportToLocal);
        Vector2 size = SizeTip(tip, safe, false, IncomingMinimumWidth);
        Rect2 bar = viewportToLocal * hpBar.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, hpBar.Size);
        Vector2[] candidates =
        [
            new(bar.End.X + AnchorGap, bar.GetCenter().Y - size.Y / 2f),
            new(bar.End.X + AnchorGap, bar.Position.Y),
            new(bar.End.X + AnchorGap, bar.End.Y - size.Y),
            new(bar.Position.X - size.X - AnchorGap, bar.GetCenter().Y - size.Y / 2f)
        ];

        Vector2 best = candidates[0];
        float bestOverlap = float.MaxValue;
        foreach (Vector2 candidate in candidates)
        {
            Vector2 position = new(
                Math.Clamp(candidate.X, safe.Position.X, Math.Max(safe.Position.X, safe.End.X - size.X)),
                Math.Clamp(candidate.Y, safe.Position.Y, Math.Max(safe.Position.Y, safe.End.Y - size.Y)));
            Rect2 rect = new(position, size);
            float overlapArea = 0f;
            foreach (Rect2 other in _incomingRects)
            {
                Rect2 collision = rect.Intersection(other.Grow(AnchorGap));
                overlapArea += collision.Size.X * collision.Size.Y;
            }

            if (overlapArea < bestOverlap)
            {
                bestOverlap = overlapArea;
                best = position;
            }
        }

        tip.Root.Position = best;
        _incomingRects.Add(new Rect2(best, size));
    }

    private static Control? FindHpBar(NCreature? creatureNode)
    {
        if (creatureNode == null || !GodotObject.IsInstanceValid(creatureNode))
        {
            return null;
        }

        NCreatureStateDisplay? stateDisplay = VanillaPrivate.CreatureStateDisplay.Get(creatureNode);
        if (stateDisplay == null || !GodotObject.IsInstanceValid(stateDisplay))
        {
            return null;
        }

        NHealthBar? healthBar = VanillaPrivate.CreatureStateDisplayHealthBar.Get(stateDisplay);
        return healthBar != null && GodotObject.IsInstanceValid(healthBar) ? healthBar.HpBarContainer : null;
    }

    private void HideIncoming()
    {
        foreach (PreviewTip tip in _incomingTips.Values)
        {
            tip.Root.Hide();
        }
    }

    private void LayoutTip(PreviewTip tip, Control hitbox, List<Rect2> occupied)
    {
        Rect2 safe = GetSafeArea(out Transform2D viewportToLocal);
        Vector2 size = SizeTip(tip, safe, ShowModeHint, TipWidth);
        Rect2 enemy = viewportToLocal * hitbox.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, hitbox.Size);
        List<Vector2> candidates =
        [
            new(enemy.Position.X - size.X - AnchorGap, enemy.Position.Y - size.Y - AnchorGap),
            new(enemy.Position.X - size.X - AnchorGap, enemy.Position.Y),
            new(enemy.End.X + AnchorGap, enemy.Position.Y),
            new(enemy.GetCenter().X - size.X / 2f, enemy.Position.Y - size.Y - AnchorGap),
            new(enemy.GetCenter().X - size.X / 2f, enemy.End.Y + AnchorGap)
        ];

        foreach (Rect2 other in occupied)
        {
            candidates.Add(new Vector2(other.Position.X, other.End.Y + AnchorGap));
            candidates.Add(new Vector2(other.Position.X, other.Position.Y - size.Y - AnchorGap));
            candidates.Add(new Vector2(other.End.X + AnchorGap, other.Position.Y));
            candidates.Add(new Vector2(other.Position.X - size.X - AnchorGap, other.Position.Y));
        }

        Vector2 best = safe.Position;
        float bestOverlap = float.MaxValue;
        float bestDistance = float.MaxValue;
        foreach (Vector2 candidate in candidates)
        {
            Vector2 position = new(
                Math.Clamp(candidate.X, safe.Position.X, Math.Max(safe.Position.X, safe.End.X - size.X)),
                Math.Clamp(candidate.Y, safe.Position.Y, Math.Max(safe.Position.Y, safe.End.Y - size.Y)));
            Rect2 overlap = new Rect2(position, size).Intersection(enemy);
            float overlapArea = overlap.Size.X * overlap.Size.Y;
            foreach (Rect2 other in occupied)
            {
                Rect2 collision = new Rect2(position, size).Intersection(other.Grow(AnchorGap));
                overlapArea += collision.Size.X * collision.Size.Y;
            }
            float distance = position.DistanceSquaredTo(candidates[0]);
            if (overlapArea < bestOverlap || (overlapArea == bestOverlap && distance < bestDistance))
            {
                bestOverlap = overlapArea;
                bestDistance = distance;
                best = position;
            }
        }

        tip.Root.Position = best;
        occupied.Add(new Rect2(best, size));
    }

    // 视口、顶部栏与各锚点节点先转换到同一局部坐标，兼容战斗 UI 缩放。
    private Rect2 GetSafeArea(out Transform2D viewportToLocal)
    {
        viewportToLocal = GetGlobalTransformWithCanvas().AffineInverse();
        Rect2 safe = (viewportToLocal * GetViewportRect()).Grow(-AnchorGap);
        // TopBar 根节点铺满视口，遮挡范围取实际背景，避免误把整屏排除。
        Control? topBar = NRun.Instance?.GlobalUi.TopBar.GetNodeOrNull<Control>("BgImage");
        if (topBar != null && topBar.IsVisibleInTree())
        {
            Rect2 topBarRect = viewportToLocal * topBar.GetGlobalTransformWithCanvas()
                * new Rect2(Vector2.Zero, topBar.Size);
            float top = Math.Clamp(topBarRect.End.Y + AnchorGap, safe.Position.Y, safe.End.Y - 1f);
            safe = new Rect2(new Vector2(safe.Position.X, top), new Vector2(safe.Size.X, safe.End.Y - top));
        }

        return safe;
    }

    /// <summary>按当前模式排版文字与提示并返回缩放后的面板尺寸。</summary>
    private Vector2 SizeTip(PreviewTip tip, Rect2 safe, bool showHint, float minimumWidth)
    {
        if (tip.LastText != tip.Label.Text || tip.LastSafeSize != safe.Size
            || tip.LastDetailed != IsDetailed || tip.LastShowHint != showHint)
        {
            tip.LastText = tip.Label.Text;
            tip.LastSafeSize = safe.Size;
            tip.LastDetailed = IsDetailed;
            tip.LastShowHint = showHint;
            // 给 Godot 两帧完成宽度变化后的文字排版，随后复用尺寸。
            tip.LayoutFrames = 2;
        }

        if (tip.LayoutFrames == 0)
        {
            return tip.ScaledSize;
        }

        tip.LayoutFrames--;
        tip.Hint.Visible = showHint;
        float hintWidth = showHint ? tip.Hint.GetCombinedMinimumSize().X : 0f;
        float textWidth;
        float width;
        if (IsDetailed)
        {
            width = Math.Min(DetailedWidth, Math.Max(TextMargins + 1f, safe.Size.X));
            textWidth = width - TextMargins;
        }
        else
        {
            // 按实际排版宽度计算，包含抗性图片和伤害范围；面板容纳完整提示及左右留白。
            textWidth = Math.Max(MinimumSummaryWidth, tip.Label.GetContentWidth() + 8f);
            float hintSpace = showHint ? HintGap + hintWidth : 0f;
            width = Math.Max(minimumWidth, TextMargins + textWidth + hintSpace);
        }

        tip.Label.CustomMinimumSize = Vector2.Zero;
        tip.Label.Size = new Vector2(textWidth, tip.Label.Size.Y);
        // 宽度变化后的文本排版可能在下一帧完成，因此每帧按真实内容高度收敛。
        float contentHeight = Math.Max(TextSize, tip.Label.GetContentHeight());
        float hintHeight = showHint ? tip.Hint.GetCombinedMinimumSize().Y : 0f;
        float heightLimit = Math.Max(1f, safe.Size.Y - VerticalMargins);
        if (IsDetailed)
        {
            heightLimit = Math.Min(heightLimit, MaximumDetailedHeight);
        }

        float textHeight = Math.Min(contentHeight, heightLimit);
        float bodyHeight = Math.Max(textHeight, hintHeight);
        tip.Label.ScrollActive = contentHeight > heightLimit;
        tip.Label.Size = new Vector2(textWidth, textHeight);
        tip.Label.Position = new Vector2(0f, (bodyHeight - textHeight) / 2f);
        tip.Hint.Size = new Vector2(hintWidth, hintHeight);
        tip.Hint.Position = new Vector2(textWidth + HintGap, (bodyHeight - hintHeight) / 2f);
        tip.Body.CustomMinimumSize = new Vector2(width - TextMargins, bodyHeight);
        tip.Body.Size = tip.Body.CustomMinimumSize;
        tip.Root.CustomMinimumSize = new Vector2(width, bodyHeight + VerticalMargins);
        tip.Root.Size = tip.Root.CustomMinimumSize;

        Vector2 size = tip.Root.Size;
        // 极窄窗口下连原版背景的最小边框都容纳不下时，整体缩放到安全区。
        float scale = Math.Min(1f, Math.Min(safe.Size.X / Math.Max(1f, size.X), safe.Size.Y / Math.Max(1f, size.Y)));
        tip.Root.Scale = Vector2.One * scale;
        tip.ScaledSize = size * scale;
        return tip.ScaledSize;
    }

    private PreviewTip CreateTip()
    {
        // 独立实例化原版提示场景；选牌时的全局 shouldBlockHoverTips 不影响本模块。
        Control root = PreloadManager.Cache.GetScene("res://scenes/ui/hover_tip.tscn").Instantiate<Control>();
        AddChild(root);
        root.AddThemeConstantOverride("margin_right", 0);
        root.AddThemeConstantOverride("margin_bottom", 0);
        root.GetNode<MegaLabel>("%Title").Hide();
        root.GetNode<TextureRect>("%Icon").Hide();
        MarginContainer margins = root.GetNode<MarginContainer>("TextContainer");
        margins.AddThemeConstantOverride("margin_left", (int)TextMargins / 2);
        margins.AddThemeConstantOverride("margin_right", (int)TextMargins / 2);
        margins.AddThemeConstantOverride("margin_top", TopPadding);
        margins.AddThemeConstantOverride("margin_bottom", BottomPadding);
        root.CustomMinimumSize = new Vector2(TipWidth, 0f);
        MakeMouseTransparent(root);
        MegaRichTextLabel label = root.GetNode<MegaRichTextLabel>("%Description");
        label.AutoSizeEnabled = false;
        label.AddThemeFontOverride("normal_font", ResourceLoader.Load<Font>(SharedAssets.KreonRegularGlyphSpaceOneResource));
        label.AddThemeFontOverride("bold_font", ResourceLoader.Load<Font>(SharedAssets.KreonBoldGlyphSpaceOneResource));
        label.AddThemeConstantOverride("line_separation", 4);
        label.AddThemeFontSizeOverride("normal_font_size", TextSize);
        label.AddThemeFontSizeOverride("bold_font_size", TextSize);
        label.FitContent = false;
        // 使用独立内容区域直接布局，避免容器的延迟排序让文字与背景尺寸错位。
        Node originalTextColumn = label.GetParent();
        var body = new Control { MouseFilter = MouseFilterEnum.Ignore };
        margins.AddChild(body);
        label.Reparent(body);
        ((Control)originalTextColumn).Hide();
        var hint = new MegaLabel
        {
            Text = LocManager.Instance.Language switch
            {
                "eng" => "[Alt: details / off]",
                "jpn" => "[Alt: 詳細表示 / 非表示]",
                "kor" => "[Alt: 상세 보기 / 끄기]",
                _ => "[按Alt切换详细视图/关闭]"
            },
            AutoSizeEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        // MegaLabel 在进入场景树时沿用原版当前语言字体替换流程。
        hint.AddThemeFontOverride("font", ResourceLoader.Load<Font>(SharedAssets.KreonRegularGlyphSpaceOneResource));
        hint.AddThemeFontSizeOverride("font_size", 18);
        hint.AddThemeColorOverride("font_color", StsColors.gray);
        body.AddChild(hint);
        return new PreviewTip(root, label, hint, body);
    }

    private static void MakeMouseTransparent(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
        }

        foreach (Node child in node.GetChildren())
        {
            MakeMouseTransparent(child);
        }
    }

    private void HideAll()
    {
        foreach (PreviewTip tip in _tips.Values)
        {
            tip.Root.Hide();
        }
    }

    private sealed record PreviewTip(Control Root, MegaRichTextLabel Label, MegaLabel Hint, Control Body)
    {
        internal string? LastText { get; set; }

        internal Vector2 LastSafeSize { get; set; }

        internal bool LastDetailed { get; set; }

        internal bool LastShowHint { get; set; }

        internal int LayoutFrames { get; set; }

        internal Vector2 ScaledSize { get; set; }
    }
}
