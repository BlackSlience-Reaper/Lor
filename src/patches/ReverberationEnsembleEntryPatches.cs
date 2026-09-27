using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.events;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
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
        ReverberationEnsemblePreviewGodMode.Update(state);

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

/// <summary>
/// 临时预览补丁：进入残响乐团幕后，为每位玩家启用原版 godmode 命令。
/// </summary>
internal static class ReverberationEnsemblePreviewGodMode
{
    private static readonly Dictionary<Player, GodModeConsoleCmd> ActiveCommands = new();

    private static RunState? _activeRunState;

    internal static void Update(RunState? state)
    {
        if (state?.Act is not ReverberationEnsembleAct)
        {
            DeactivateAll();
            _activeRunState = null;
            return;
        }

        if (!ReferenceEquals(state, _activeRunState))
        {
            DeactivateAll();
            _activeRunState = state;
        }

        foreach (Player player in state.Players)
        {
            if (ActiveCommands.ContainsKey(player))
            {
                continue;
            }

            GodModeConsoleCmd command = new();
            CmdResult result = command.Process(player, Array.Empty<string>());
            if (!result.success)
            {
                continue;
            }

            ActiveCommands.Add(player, command);
            if (result.task != null)
            {
                TaskHelper.RunSafely(result.task);
            }
        }
    }

    private static void DeactivateAll()
    {
        if (!RunManager.Instance.IsInProgress)
        {
            return;
        }

        foreach (KeyValuePair<Player, GodModeConsoleCmd> entry in new List<KeyValuePair<Player, GodModeConsoleCmd>>(ActiveCommands))
        {
            CmdResult result = entry.Value.Process(entry.Key, Array.Empty<string>());
            if (!result.success)
            {
                continue;
            }

            ActiveCommands.Remove(entry.Key);
            if (result.task != null)
            {
                TaskHelper.RunSafely(result.task);
            }
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
