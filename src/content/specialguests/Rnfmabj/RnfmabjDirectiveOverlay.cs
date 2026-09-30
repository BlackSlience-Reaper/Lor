using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using LibraryOfRuina.framework.combat;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

[HarmonyPatch(typeof(NCombatRoom), "OnCombatSetUp")]
internal static class RnfmabjDirectiveOverlayPatch
{
    private const string OverlayNodeName =
        "LibraryOfRuinaRnfmabjDirectiveOverlay";

    private static void Postfix(NCombatRoom __instance, CombatState state)
    {
        if (state.Encounter is not RnfmabjSpecialGuestEncounter)
        {
            return;
        }

        if (__instance.Ui.GetNodeOrNull<RnfmabjDirectiveOverlay>(
                OverlayNodeName) is { } existing)
        {
            existing.QueueFreeSafely();
        }

        __instance.Ui.AddChildSafely(new RnfmabjDirectiveOverlay(state)
        {
            Name = OverlayNodeName,
        });
    }
}

internal sealed partial class RnfmabjDirectiveOverlay : Control
{
    internal const string CardHoverTipScenePath =
        "res://scenes/ui/card_hover_tip.tscn";

    private const int OverlayZIndex = -10;
    private const float OverlayScale = 0.65f;

    private readonly CombatState _combatState;
    private RnfmabjDirectiveCard? _previewCard;
    private NCard? _cardNode;
    private string _lastFingerprint = string.Empty;
    private int _frameCounter;

    internal RnfmabjDirectiveOverlay(CombatState combatState)
    {
        _combatState = combatState;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZIndex = OverlayZIndex;
    }

    public override void _Ready()
    {
        AnchorLeft = 1f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -513f;
        OffsetTop = 368f;
        OffsetRight = -274f;
        OffsetBottom = 691f;
        PivotOffset = new Vector2(119.5f, 161.5f);
        Scale = Vector2.One * OverlayScale;

        Control cardPreview = PreloadManager.Cache
            .GetScene(CardHoverTipScenePath)
            .Instantiate<Control>();
        this.AddChildSafely(cardPreview);
        cardPreview.Position = Vector2.Zero;
        _cardNode = cardPreview.GetNode<NCard>("%Card");
        _previewCard = (RnfmabjDirectiveCard)ModelDb
            .Card<RnfmabjDirectiveCard>()
            .ToMutable();
        _cardNode.Model = _previewCard;
        DisableInputRecursively(cardPreview);

        LocString.SubscribeToLocaleChange(OnLocaleChanged);
        Refresh(force: true);
    }

    public override void _ExitTree()
    {
        LocString.UnsubscribeToLocaleChange(OnLocaleChanged);
        _cardNode = null;
        _previewCard = null;
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        _frameCounter++;
        if ((_frameCounter & 3) == 0)
        {
            Refresh(force: false);
        }
    }

    private void Refresh(bool force)
    {
        Rnfmabj? boss = _combatState.LivingEnemies()
            .Select(static enemy => enemy.Monster)
            .OfType<Rnfmabj>()
            .FirstOrDefault();
        if (boss == null || _cardNode == null || _previewCard == null)
        {
            Visible = false;
            return;
        }

        RnfmabjDirectiveSnapshot snapshot = boss.GetDirectiveSnapshot(
            LocalContext.NetId);
        if (!snapshot.IsVisible)
        {
            Visible = false;
            _lastFingerprint = snapshot.Fingerprint;
            return;
        }

        Visible = true;
        if (!force
            && string.Equals(
                _lastFingerprint,
                snapshot.Fingerprint,
                StringComparison.Ordinal))
        {
            return;
        }

        _lastFingerprint = snapshot.Fingerprint;
        _previewCard.SetPresentation(snapshot);
        _cardNode.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
    }

    private void OnLocaleChanged()
    {
        _lastFingerprint = string.Empty;
        Refresh(force: true);
    }

    private static void DisableInputRecursively(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
            control.FocusMode = FocusModeEnum.None;
        }

        foreach (Node child in node.GetChildren())
        {
            DisableInputRecursively(child);
        }
    }
}

[HarmonyPatch(typeof(NCombatRoom), "OnCombatSetUp")]
internal static class RnfmabjPrescriptTaskOverlayPatch
{
    private const string OverlayNodeName =
        "LibraryOfRuinaRnfmabjPrescriptTaskOverlay";

    private static void Postfix(NCombatRoom __instance, CombatState state)
    {
        foreach (Player player in state.Players)
        {
            if (!LocalContext.IsMe(player))
            {
                __instance.GetCreatureNode(player.Creature)
                    ?.GetNodeOrNull<RnfmabjPrescriptTaskOverlay>(OverlayNodeName)
                    ?.QueueFreeSafely();
                continue;
            }

            if (player.PlayerCombatState?.AllCards
                    .OfType<RnfmabjWillOfThePrescriptCard>()
                    .Any() != true
                || __instance.GetCreatureNode(player.Creature) is not
                    { } creatureNode)
            {
                continue;
            }

            creatureNode.GetNodeOrNull<RnfmabjPrescriptTaskOverlay>(
                    OverlayNodeName)
                ?.QueueFreeSafely();
            creatureNode.AddChildSafely(
                new RnfmabjPrescriptTaskOverlay(player)
                {
                    Name = OverlayNodeName,
                });
        }
    }
}

internal sealed partial class RnfmabjPrescriptTaskOverlay : Control
{
    private const float OverlayScale = 0.8f;
    private const float CardHoverTipHeight = 323f;
    private const float PlayerNodeMargin = 18f;
    private const float OverlayDownOffset = 55f;

    private readonly Player _player = null!;
    private RnfmabjWillOfThePrescriptCard? _taskCard;
    private NCard? _cardNode;
    private string _lastFingerprint = string.Empty;
    private int _frameCounter;

    internal RnfmabjPrescriptTaskOverlay(Player player)
    {
        _player = player;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZAsRelative = false;
        ZIndex = -10;
    }

    public RnfmabjPrescriptTaskOverlay()
    {
    }

    public override void _Ready()
    {
        Position = GetParent() is NCreature creatureNode
            ? new Vector2(
                creatureNode.Hitbox.Position.X
                + creatureNode.Hitbox.Size.X
                + PlayerNodeMargin,
                creatureNode.Hitbox.Position.Y
                - CardHoverTipHeight * OverlayScale
                - PlayerNodeMargin
                + OverlayDownOffset)
            : new Vector2(145f, -401.4f);
        PivotOffset = Vector2.Zero;
        Scale = Vector2.One * OverlayScale;

        Control cardPreview = PreloadManager.Cache
            .GetScene(RnfmabjWillOfThePrescriptCard.CardHoverTipScenePath)
            .Instantiate<Control>();
        this.AddChildSafely(cardPreview);
        cardPreview.Position = Vector2.Zero;
        _cardNode = cardPreview.GetNode<NCard>("%Card");
        DisableInputRecursively(cardPreview);

        LocString.SubscribeToLocaleChange(OnLocaleChanged);
        Refresh(force: true);
    }

    public override void _ExitTree()
    {
        LocString.UnsubscribeToLocaleChange(OnLocaleChanged);
        _cardNode = null;
        _taskCard = null;
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        _frameCounter++;
        if ((_frameCounter & 3) == 0)
        {
            Refresh(force: false);
        }
    }

    private void Refresh(bool force)
    {
        RnfmabjWillOfThePrescriptCard? taskCard = _player
            .PlayerCombatState?
            .AllCards
            .OfType<RnfmabjWillOfThePrescriptCard>()
            .FirstOrDefault();
        if (taskCard == null || _cardNode == null || !taskCard.TaskActive)
        {
            Visible = false;
            _taskCard = taskCard;
            _lastFingerprint = taskCard?.GetTaskPresentationFingerprint()
                ?? string.Empty;
            return;
        }

        Visible = true;
        string fingerprint = taskCard.GetTaskPresentationFingerprint();
        bool sourceChanged = !ReferenceEquals(_taskCard, taskCard);

        if (!force
            && !sourceChanged
            && string.Equals(
                _lastFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            return;
        }

        _taskCard = taskCard;
        _lastFingerprint = fingerprint;
        _cardNode.Model = CreateKeywordlessPresentationCopy(taskCard);
        _cardNode.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
    }

    private static RnfmabjWillOfThePrescriptCard
        CreateKeywordlessPresentationCopy(
            RnfmabjWillOfThePrescriptCard source)
    {
        var presentationCopy = (RnfmabjWillOfThePrescriptCard)
            source.ClonePreservingMutability();
        foreach (CardKeyword keyword in presentationCopy.Keywords.ToArray())
        {
            presentationCopy.RemoveKeyword(keyword);
        }

        return presentationCopy;
    }

    private void OnLocaleChanged()
    {
        _lastFingerprint = string.Empty;
        Refresh(force: true);
    }

    private static void DisableInputRecursively(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
            control.FocusMode = FocusModeEnum.None;
        }

        foreach (Node child in node.GetChildren())
        {
            DisableInputRecursively(child);
        }
    }
}
