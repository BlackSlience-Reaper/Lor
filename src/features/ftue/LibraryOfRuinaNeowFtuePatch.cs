using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.ftue;

/// <summary>
/// Shows the Special Guest introduction after the Library Act's Neow event
/// layout has finished building, so the tutorial is layered over a complete
/// and interactive event screen.
/// </summary>
[HarmonyPatch(typeof(NEventRoom), "SetupLayout")]
internal static class LibraryOfRuinaNeowFtuePatch
{
    private const int ModalWaitFrameLimit = 600;

    [HarmonyPostfix]
    private static void Postfix(NEventRoom __instance, ref Task __result)
    {
        __result = ShowAfterLayoutAsync(__result, __instance);
    }

    private static async Task ShowAfterLayoutAsync(
        Task original,
        NEventRoom eventRoom)
    {
        await original;

        if (!IsCurrentLibraryNeowRoom(eventRoom))
        {
            return;
        }

        bool shouldShowUpdateLogLocation = FtueGuard.ShouldShow(
            LibraryOfRuinaFtueIds.UpdateLogLocationTutorial);
        bool shouldShowSpecialGuestIntro = FtueGuard.ShouldShow(
            LibraryOfRuinaFtueIds.NeowSpecialGuestIntro);
        if (!shouldShowUpdateLogLocation && !shouldShowSpecialGuestIntro)
        {
            return;
        }

        SceneTree? tree = eventRoom.GetTree();
        if (tree == null)
        {
            return;
        }

        await eventRoom.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        for (int frame = 0;
             NModalContainer.Instance?.OpenModal != null
             && frame < ModalWaitFrameLimit;
             frame++)
        {
            await eventRoom.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!IsCurrentLibraryNeowRoom(eventRoom))
            {
                return;
            }
        }

        if (!IsCurrentLibraryNeowRoom(eventRoom))
        {
            return;
        }

        if (shouldShowUpdateLogLocation)
        {
            await ShowUpdateLogLocationFtue(eventRoom, tree);
            await eventRoom.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        if (!shouldShowSpecialGuestIntro
            || !IsCurrentLibraryNeowRoom(eventRoom))
        {
            return;
        }

        for (int frame = 0;
             NModalContainer.Instance?.OpenModal != null
             && frame < ModalWaitFrameLimit;
             frame++)
        {
            await eventRoom.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!IsCurrentLibraryNeowRoom(eventRoom))
            {
                return;
            }
        }

        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer == null
            || modalContainer.OpenModal != null
            || !FtueGuard.TryConsumeShowRequest(
                LibraryOfRuinaFtueIds.NeowSpecialGuestIntro))
        {
            return;
        }

        modalContainer.Add(
            NLibraryOfRuinaCombatRulesFtue.Create(
                LibraryOfRuinaFtueIds.NeowSpecialGuestIntro,
                Pages));
        Log.Info(
            "[LibraryOfRuina.FTUE] Showing Neow Special Guest introduction.");
    }

    private static async Task ShowUpdateLogLocationFtue(
        NEventRoom eventRoom,
        SceneTree tree)
    {
        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer == null
            || modalContainer.OpenModal != null
            || !IsCurrentLibraryNeowRoom(eventRoom)
            || !FtueGuard.TryConsumeShowRequest(
                LibraryOfRuinaFtueIds.UpdateLogLocationTutorial))
        {
            return;
        }

        NLibraryOfRuinaFtuePopup popup =
            NLibraryOfRuinaFtuePopup.Create(
                LibraryOfRuinaFtueIds.UpdateLogLocationTutorial,
                "LOR_UPDATE_LOG_LOCATION_FTUE_TITLE",
                "LOR_UPDATE_LOG_LOCATION_FTUE_BODY");
        Task confirmation = popup.WaitForPlayerToConfirm();
        modalContainer.Add(popup);
        Log.Info(
            "[LibraryOfRuina.FTUE] Showing update-log location at Neow.");
        await confirmation;

        if (IsCurrentLibraryNeowRoom(eventRoom))
        {
            await eventRoom.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static bool IsCurrentLibraryNeowRoom(NEventRoom eventRoom)
    {
        if (!GodotObject.IsInstanceValid(eventRoom)
            || !ReferenceEquals(NEventRoom.Instance, eventRoom)
            || CurrentRun.State is not RunState
            {
                CurrentRoom: EventRoom { IsPreFinished: false }
            })
        {
            return false;
        }

        return RunManager.Instance.EventSynchronizer.GetLocalEvent() is Neow
        {
            Owner: { } owner
        }
            && LocalContext.IsMe(owner)
            && owner.RunState.Act is LibraryOfRuinaActModel;
    }

    private static readonly LibraryOfRuinaCombatFtuePage[] Pages =
    [
        new(
            "BOOK_SHADOW_RELIC.title",
            "LOR_NEOW_SPECIAL_GUEST_FTUE_RELIC_BODY",
            "res://images/relics/book_shadow_relic.png",
            LibraryOfRuinaCombatFtueVisual.CenteredImage,
            TitleTable: "relics"),
        new(
            "LOR_NEOW_SPECIAL_GUEST_FTUE_OVERVIEW_TITLE",
            "LOR_NEOW_SPECIAL_GUEST_FTUE_OVERVIEW_BODY",
            null,
            LibraryOfRuinaCombatFtueVisual.SpecialGuestPortraits),
        new(
            "LOR_NEOW_SPECIAL_GUEST_FTUE_EMOTION_TITLE",
            "LOR_NEOW_SPECIAL_GUEST_FTUE_EMOTION_BODY",
            null,
            LibraryOfRuinaCombatFtueVisual.EmotionTrack),
    ];
}
