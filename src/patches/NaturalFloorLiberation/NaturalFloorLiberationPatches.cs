using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.backgrounds.NaturalFloorLiberation;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.events.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.NaturalFloorLiberation;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.SetUpBackground))]
internal static class NaturalFloorAnimatedBackgroundPatch
{
    private static void Postfix(NCombatRoom __instance, ICombatRoomVisuals ____visuals)
    {
        if (____visuals.Encounter is not NaturalFloorLiberationEncounter encounter
            || __instance.Background is not { } background)
        {
            return;
        }

        // 原版 AddChildSafely 可能延迟挂载背景图层，等待整棵背景节点就绪再绑定阶段。
        if (background.IsNodeReady())
        {
            AttachPhaseBackground(background, encounter);
        }
        else
        {
            background.Connect(Node.SignalName.Ready,
                Callable.From(() => AttachPhaseBackground(background, encounter)),
                (uint)GodotObject.ConnectFlags.OneShot);
        }
    }

    private static void AttachPhaseBackground(
        NCombatBackground background,
        NaturalFloorLiberationEncounter encounter)
    {
        if (background.GetNodeOrNull<TextureRect>("Layer_00/A") is not { } layer)
        {
            Log.Warn($"[NaturalFloorBackground] Phase {encounter.CurrentPhase}: background layer Layer_00/A is missing.");
            return;
        }

        if (layer.GetNodeOrNull("AnimatedBackground") != null)
        {
            return;
        }

        // Construct the mod script directly; exported scenes retain a scriptless fallback.
        var animation = new NaturalFloorLiberationBackground
        {
            Name = "AnimatedBackground",
            Encounter = encounter,
            Texture = layer.Texture,
            ExpandMode = layer.ExpandMode,
            StretchMode = layer.StretchMode,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.AddChild(animation);
        animation.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        animation.SetPhaseBackground(encounter.TransitionPending ? encounter.CurrentPhase - 1 : encounter.CurrentPhase);
        Log.Info($"[NaturalFloorBackground] Bound phase={encounter.CurrentPhase}, texture={animation.Texture?.ResourcePath}.");
    }
}

/// <summary>
/// 由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 在终局奖励界面继续时调用；返回 false 表示已接管。
/// 结算存储的所有读取方都在这里之后，所以只在这里补记一次：遭遇把 SettlementTriggered 置位时已经记过，读档恢复的遭遇在这里补上。
/// </summary>
internal static class NaturalFloorSettlementRedirect
{
    internal static bool TryRedirect(RunManager __instance, ref Task __result)
    {
        if (__instance.DebugOnlyGetState()?.CurrentRoom is not CombatRoom
            { Encounter: NaturalFloorLiberationEncounter encounter } || !encounter.SettlementTriggered) return true;
        LiberationSettlementProceedHelper.ClearLingeringCardPreviews();
        NaturalFloorLiberationSettlementStore.Record(encounter);
        if (!NaturalFloorLiberationSettlementStore.PendingSettlement) return true;
        NaturalFloorLiberationSettlementStore.Consume();
        __result = __instance.EnterRoom(new EventRoom(ModelDb.AncientEvent<NaturalFloorLiberationSettlementEvent>()));
        return false;
    }
}
