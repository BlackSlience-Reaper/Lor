using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using LibraryOfRuina.powers.SocialFloorLiberation;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace LibraryOfRuina.ui.SocialFloorLiberation;

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class FalseThroneTrialHoverTipReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Monster is not FalseThrone
            || __instance.GetNodeOrNull(
                FalseThroneTrialHoverTipOverlay.NodeName) != null)
        {
            return;
        }

        var overlay = new FalseThroneTrialHoverTipOverlay();
        overlay.Bind(__instance);
        __instance.AddChild(overlay);
    }
}

internal partial class FalseThroneTrialHoverTipOverlay : Control
{
    internal const string NodeName =
        "LibraryOfRuinaFalseThroneTrialHoverTip";

    private const int MaxTrialHoverTips = 2;
    private const int HoverTipZIndex = -79;
    private const float HoverTipWidth = 360f;
    private const float HorizontalSpacing = 24f;

    private static readonly Vector2 AnchorOffset = new(0f, -110f);

    private NCreature? _creatureNode;
    private readonly List<Control> _anchors = [];
    private readonly List<NHoverTipSet?> _hoverTipSets = [];
    private SocialFloorTrial? _displayedTrial;
    private int _activeTipCount;

    internal FalseThroneTrialHoverTipOverlay()
    {
        Name = NodeName;
        MouseFilter = MouseFilterEnum.Ignore;
        Size = Vector2.One;
    }

    internal void Bind(NCreature creatureNode)
    {
        _creatureNode = creatureNode;
    }

    public override void _Ready()
    {
        EnsureAnchors();
        SetProcess(true);
        Refresh();
    }

    public override void _Process(double delta)
    {
        Refresh();
    }

    public override void _ExitTree()
    {
        ClearHoverTips();
    }

    private void Refresh()
    {
        if (_creatureNode == null
            || !IsInstanceValid(_creatureNode)
            || _creatureNode.Entity?.Monster is not FalseThrone throne
            || _creatureNode.Entity.CombatState?.Encounter is not
                SocialFloorLiberationEncounter encounter)
        {
            ClearHoverTips();
            return;
        }

        Vector2 anchorOrigin =
            _creatureNode.Hitbox.GlobalPosition + AnchorOffset;
        GlobalPosition = anchorOrigin;
        PositionAnchors(anchorOrigin, _activeTipCount);

        if (NHoverTipSet.shouldBlockHoverTips)
        {
            ClearHoverTips();
            return;
        }

        if (_displayedTrial == encounter.Trial && !HasMissingHoverTip())
        {
            return;
        }

        RebuildHoverTips(throne, encounter.Trial, anchorOrigin);
    }

    private void EnsureAnchors()
    {
        if (_anchors.Count != 0)
        {
            return;
        }

        for (int index = 0; index < MaxTrialHoverTips; index++)
        {
            var anchor = new Control
            {
                Name = $"TrialHoverTipAnchor{index + 1}",
                MouseFilter = MouseFilterEnum.Ignore,
                Size = Vector2.One
            };
            AddChild(anchor);
            _anchors.Add(anchor);
            _hoverTipSets.Add(null);
        }
    }

    private void RebuildHoverTips(
        FalseThrone throne,
        SocialFloorTrial trial,
        Vector2 anchorOrigin)
    {
        IReadOnlyList<IHoverTip> tips = CreateTrialHoverTips(throne, trial);
        ClearHoverTips();
        EnsureAnchors();
        PositionAnchors(anchorOrigin, tips.Count);

        for (int index = 0; index < tips.Count; index++)
        {
            Control anchor = _anchors[index];
            NHoverTipSet? hoverTipSet = NHoverTipSet.CreateAndShow(
                anchor,
                tips[index],
                HoverTipAlignment.Left);
            if (hoverTipSet != null)
            {
                hoverTipSet.ZAsRelative = false;
                hoverTipSet.ZIndex = HoverTipZIndex;
                hoverTipSet.SetFollowOwner();
            }

            _hoverTipSets[index] = hoverTipSet;
        }

        _activeTipCount = tips.Count;
        _displayedTrial = trial;
    }

    private void PositionAnchors(Vector2 anchorOrigin, int tipCount)
    {
        for (int index = 0; index < tipCount; index++)
        {
            float x = -(tipCount - index - 1)
                * (HoverTipWidth + HorizontalSpacing);

            // Anchors are children of the scaled creature node. Set their
            // global positions so the 360px UI tips do not inherit that
            // creature scale and overlap each other.
            _anchors[index].GlobalPosition =
                anchorOrigin + new Vector2(x, 0f);
        }
    }

    private bool HasMissingHoverTip()
    {
        if (_activeTipCount == 0)
        {
            return true;
        }

        for (int index = 0; index < _activeTipCount; index++)
        {
            NHoverTipSet? hoverTipSet = _hoverTipSets[index];
            if (hoverTipSet == null
                || !IsInstanceValid(hoverTipSet)
                || hoverTipSet.IsQueuedForDeletion()
                || !hoverTipSet.IsInsideTree())
            {
                return true;
            }
        }

        return false;
    }

    private void ClearHoverTips()
    {
        for (int index = 0; index < _anchors.Count; index++)
        {
            NHoverTipSet.Remove(_anchors[index]);
            _hoverTipSets[index] = null;
        }

        _activeTipCount = 0;
        _displayedTrial = null;
    }

    private static IReadOnlyList<IHoverTip> CreateTrialHoverTips(
        FalseThrone throne,
        SocialFloorTrial trial)
    {
        Type[] expectedPowerTypes = trial switch
        {
            SocialFloorTrial.Woodsman =>
            [
                typeof(FalseThroneShowYourWarmHeartPower),
                typeof(FalseThroneEmptyChestPower)
            ],
            SocialFloorTrial.Scarecrow =>
            [
                typeof(FalseThroneShowYourWisdomPower),
                typeof(FalseThroneInsignificantWisdomPower)
            ],
            SocialFloorTrial.Lion =>
            [
                typeof(FalseThroneShowYourCouragePower),
                typeof(FalseThroneTrulyCowardPower)
            ],
            SocialFloorTrial.Home =>
            [
                typeof(FalseThroneWhatCanYouDoPower)
            ],
            SocialFloorTrial.Rage =>
            [
                typeof(FalseThroneRagePower)
            ],
            _ =>
            [
                typeof(FalseThroneWizardsTrialPower)
            ]
        };

        var tips = new List<IHoverTip>(expectedPowerTypes.Length);
        foreach (Type expectedPowerType in expectedPowerTypes)
        {
            PowerModel? power = throne.Creature.Powers.FirstOrDefault(
                candidate => candidate.GetType() == expectedPowerType);
            IHoverTip? smartTip = power?.HoverTips.FirstOrDefault(
                static tip => tip.IsSmart);
            if (smartTip == null)
            {
                // Trial powers are synchronized asynchronously. Returning no
                // tips makes Refresh retry next frame without falling back to
                // the canonical power's static description.
                return [];
            }

            tips.Add(smartTip);
        }

        return tips;
    }
}
