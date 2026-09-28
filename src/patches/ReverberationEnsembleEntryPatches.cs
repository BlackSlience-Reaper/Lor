using Godot;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(RunManager), "EnterRoomInternal")]
internal static class ReverberationEnsembleEntryRoomPatch
{
    [HarmonyPrefix]
    private static void Prefix(RunManager __instance, ref AbstractRoom room, bool isRestoringRoomStackBase)
    {
        RunState? state = __instance.DebugOnlyGetState();

        // 选幕及地图生成完成后，用序幕事件接替首次地图房间；先古仍是地图起点。
        // 直接使用原版淡入、事件投票与房间生命周期，读档也根据当前幕和已走节点判断。
        if (!isRestoringRoomStackBase
            && room is MapRoom
            && state?.Act is ReverberationEnsembleAct
            && state.VisitedMapCoords.Count == 0)
        {
            room = new EventRoom(ModelDb.Event<ReverberationEnsembleEntryEvent>());
        }
    }
}

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.SetEvent))]
internal static class ReverberationEnsembleEntryLayoutPatch
{
    [HarmonyPostfix]
    private static void Postfix(NEventLayout __instance, EventModel eventModel)
    {
        if (eventModel is not ReverberationEnsembleEntryEvent)
        {
            return;
        }

        __instance.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        TextureRect portrait = __instance.GetNode<TextureRect>("%Portrait");
        portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        portrait.Scale = Vector2.One;
        portrait.PivotOffset = Vector2.Zero;
        portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        portrait.MouseFilter = Control.MouseFilterEnum.Ignore;

        // 保留原版选项控件、键盘与联机投票，将唯一选项放到插图下方的暗色区域。
        Control content = __instance.GetNode<Control>("%Title").GetParent<Control>();
        content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        content.OffsetLeft = -400;
        content.OffsetRight = 400;
        content.OffsetTop = -205;
        content.OffsetBottom = -60;
        __instance.GetNode<Control>("%Title").Hide();
        __instance.GetNode<Control>("%EventDescription").Hide();
    }
}
