using System;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.RoadHome;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.ui;

[HarmonyPatch(typeof(NCombatRoom), "OnCombatSetUp")]
internal static class BlockTransferOverlayPatch
{
    private const string OverlayNodeName = "LibraryOfRuinaBlockTransferOverlay";

    private static void Postfix(NCombatRoom __instance, CombatState state)
    {
        try
        {
            if (!BlockTransferEncounterTargetHelper.IsSupportedEncounter(state)
                || __instance.Ui.GetNodeOrNull<Node>(OverlayNodeName) != null)
            {
                return;
            }

            var container = new Node { Name = OverlayNodeName };
            __instance.Ui.AddChildSafely(container);
            if (state.Encounter is NaturalFloorLiberationEncounter)
            {
                // 第二阶段与终战同伴都可能在开战后生成，按角色动态解析按钮目标。
                container.AddChildSafely(new BlockTransferOverlay(state));
                foreach (NaturalFloorGirlKind kind in Enum.GetValues<NaturalFloorGirlKind>())
                {
                    container.AddChildSafely(new BlockTransferOverlay(state, girlKind: kind));
                }

                return;
            }

            foreach (Creature partner in BlockTransferEncounterTargetHelper.FindPartners(state))
            {
                container.AddChildSafely(new BlockTransferOverlay(state, partner.CombatId));
            }
        }
        catch (Exception ex)
        {
            Log.Warn("[LittleRedBlockTransfer] Failed to add overlay: " + ex);
        }
    }
}

internal partial class BlockTransferOverlay : BlockTransferButton
{
    private const int ButtonZIndex = -10;
    private const float ButtonGap = 8f;
    private const float PartnerHeadAnchorY = 0.50f;
    private static readonly Vector2 ForestKeeperButtonOffset = new(50f, -62f);
    private const float RoadHomeHouseButtonGap = 36f;
    private const float RoadHomeHouseAnchorY = 0.32f;

    private readonly CombatStateLike? _combatState;
    private readonly uint? _partnerCombatId;
    private readonly NaturalFloorGirlKind? _girlKind;
    private bool _isBusy;
    private int _frameCounter;

    public BlockTransferOverlay(
        CombatStateLike? combatState = null,
        uint? partnerCombatId = null,
        NaturalFloorGirlKind? girlKind = null)
    {
        _combatState = combatState;
        _partnerCombatId = partnerCombatId;
        _girlKind = girlKind;
        TopLevel = false;
        ZIndex = ButtonZIndex;
    }

    protected override List<IHoverTip> CreateHoverTips()
    {
        Creature? partner = FindPartner(_combatState);
        string partnerName = partner?.Name ?? "";

        var tips = new List<IHoverTip>();
        var titleLoc = LocString.GetIfExists("gameplay_ui", "LITTLE_RED_BLOCK_TRANSFER.title");
        var descLoc = LocString.GetIfExists("gameplay_ui", "LITTLE_RED_BLOCK_TRANSFER.description");
        if (titleLoc != null && descLoc != null)
        {
            descLoc.Add("PartnerName", partnerName);
            tips.Add(new HoverTip(titleLoc, descLoc));
        }

        tips.Add(HoverTipFactory.Static(StaticHoverTip.Block));
        return tips;
    }

    public override void _Process(double delta)
    {
        _frameCounter++;
        if ((_frameCounter & 7) == 0)
        {
            RefreshAvailabilityAndPosition();
        }
    }

    protected override void OnRelease()
    {
        if (_isBusy || !IsEnabled)
        {
            return;
        }

        TaskHelper.RunSafely(TransferBlock());
    }

    private void RefreshAvailabilityAndPosition()
    {
        CombatStateLike? combatState = _combatState;
        Creature? partner = FindPartner(combatState);

        Player? localPlayer;
        try
        {
            localPlayer = LocalContextCompat.GetMe(combatState);
        }
        catch (InvalidOperationException)
        {
            // Combat state exists but local player is not yet (or no longer) in it —
            // happens during room transitions when _Process fires before cleanup.
            Visible = false;
            return;
        }

        Creature? localCreature = localPlayer?.Creature;
        CombatManager? combatManager = CombatManager.Instance;
        bool canShow = combatManager?.IsInProgress == true
            && combatState?.CurrentSide == CombatSide.Player
            && partner is { IsAlive: true }
            && AllyTurnRegistry.CanTransferBlockWith(partner)
            && localPlayer != null
            && localCreature is { IsAlive: true };
        bool enabled = !_isBusy
            && localPlayer != null
            && localCreature != null
            && canShow
            && localCreature.Block > 0
            && (RunManagerCompat.IsSinglePlayerOrFakeMultiplayer(RunManager.Instance)
                || !RunManager.Instance.ActionQueueSet.ActionQueueIsPaused(localPlayer.NetId));

        bool hasPosition = TryGetButtonPosition(partner, out Vector2 position);
        Visible = canShow && hasPosition;
        if (Visible)
        {
            GlobalPosition = position;
        }

        SetEnabled(enabled);
        Modulate = enabled ? Colors.White : new Color(1f, 1f, 1f, 0.45f);
    }

    private static bool TryGetButtonPosition(Creature? partner, out Vector2 position)
    {
        position = Vector2.Zero;
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null || partner == null)
        {
            return false;
        }

        NCreature? partnerNode = room.GetCreatureNode(partner);
        Rect2 anchorRect = partnerNode?.Hitbox.GetGlobalRect() ?? default;
        if (anchorRect.Size.X <= 0f || anchorRect.Size.Y <= 0f)
        {
            return false;
        }

        Vector2 viewportSize = room.GetViewportRect().Size;
        bool isRoadHomeHouse = partner.Monster is RoadHomeHouse;
        float buttonGap = isRoadHomeHouse ? RoadHomeHouseButtonGap : ButtonGap;
        float anchorY = isRoadHomeHouse ? RoadHomeHouseAnchorY : PartnerHeadAnchorY;
        Vector2 currentVisualCenter = new(
            anchorRect.Position.X + anchorRect.Size.X + buttonGap + VisualSize * 0.5f,
            anchorRect.Position.Y + anchorRect.Size.Y * anchorY);
        Vector2 headAnchor = partnerNode?.Visuals?.TalkPosition?.GlobalPosition
            ?? new Vector2(
                anchorRect.Position.X + anchorRect.Size.X * 0.5f,
                anchorRect.Position.Y + anchorRect.Size.Y * anchorY);
        Vector2 midpointVisualCenter = currentVisualCenter.Lerp(headAnchor, 0.5f);
        float x = midpointVisualCenter.X - VisualSize * 0.5f;
        float y = midpointVisualCenter.Y - VisualSize * 0.5f;
        Vector2 offset = partner.Monster is ForestKeeperBirdBase
            ? ForestKeeperButtonOffset
            : Vector2.Zero;
        position = new Vector2(
            Math.Clamp(x + offset.X, 0f, Math.Max(0f, viewportSize.X - HitboxWidth)),
            Math.Clamp(y + offset.Y, 0f, Math.Max(0f, viewportSize.Y - HitboxHeight)));
        return true;
    }

    private async Task TransferBlock()
    {
        _isBusy = true;
        try
        {
            CombatStateLike? combatState = _combatState;
            Creature? partner = FindPartner(combatState);

            Player? localPlayer;
            try
            {
                localPlayer = LocalContextCompat.GetMe(combatState);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            Creature? localCreature = localPlayer?.Creature;
            CombatManager? combatManager = CombatManager.Instance;
            if (combatState == null
                || partner is not { IsAlive: true }
                || localCreature is not { IsAlive: true }
                || localPlayer == null
                || localCreature.Block <= 0
                || combatState.CurrentSide != CombatSide.Player
                || combatManager?.IsInProgress != true)
            {
                return;
            }

            BlockTransferAction.TryRequest(
                localPlayer,
                combatState.RoundNumber,
                partner.CombatId);
            await Task.CompletedTask;
        }
        finally
        {
            _isBusy = false;
            RefreshAvailabilityAndPosition();
        }
    }

    private Creature? FindPartner(CombatStateLike? combatState)
    {
        if (_girlKind.HasValue)
        {
            return (combatState?.Encounter as NaturalFloorLiberationEncounter)
                ?.FindNihilGirl(_girlKind.Value)?.Creature;
        }

        if (_partnerCombatId.HasValue)
        {
            return combatState?.GetCreature(_partnerCombatId.Value);
        }

        return BlockTransferEncounterTargetHelper.FindPartner(combatState);
    }
    
}
