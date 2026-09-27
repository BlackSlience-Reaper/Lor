using System;
using System.Linq;
using System.Collections.Generic;
using Expression = System.Linq.Expressions.Expression;
using HarmonyLib;
using Godot;
using LibraryLib.SpeedDice;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.ui.DamagePreview;

/// <summary>
/// 只读地查找基础库速度骰子 UI 上当前悬停或聚焦、且已装配卡牌的骰子，返回卡牌及其已选定目标；
/// 不触发装配、改目标等任何骰子交互。
/// </summary>
internal static class DamagePreviewSpeedDieSource
{
    // 基础库默认挂在血条下；罗兰把同一 UI 移到角色节点下以保持头顶布局。
    private const string HealthBarUiPath = "HealthBar/HealthBar/LibrarySpeedDiceUi";
    private const string CreatureUiPath = "LibrarySpeedDiceUi";

    // 各槽位按钮命名为 SpeedDie{槽位序号}（从 1 起）。
    private const string SlotButtonPrefix = "SpeedDie";

    private static readonly Func<LibrarySpeedDiceCombatState, LibrarySpeedDiceSlot, IReadOnlyList<Creature>> TargetLineTargets =
        BindTargetLineTargets();

    private static Func<LibrarySpeedDiceCombatState, LibrarySpeedDiceSlot, IReadOnlyList<Creature>> BindTargetLineTargets()
    {
        // 与速度骰 UI 共用分发入口，包括群攻扩展与卡牌专属的目标规则。
        var state = Expression.Parameter(typeof(LibrarySpeedDiceCombatState), "state");
        var slot = Expression.Parameter(typeof(LibrarySpeedDiceSlot), "slot");
        var registration = Expression.Property(state,
            AccessTools.Property(typeof(LibrarySpeedDiceCombatState), "Registration"));
        var dispatcher = Expression.Property(registration, "Dispatcher");
        var targets = Expression.Call(dispatcher, "GetTargetLineTargets", null, state, slot);
        return Expression.Lambda<Func<LibrarySpeedDiceCombatState, LibrarySpeedDiceSlot, IReadOnlyList<Creature>>>(
            targets, state, slot).Compile();
    }

    internal static bool TryGetHovered(NCombatRoom room, CombatState combat, out CardModel? card,
        out Creature? target, out IReadOnlyList<Creature> targets)
    {
        card = null;
        target = null;
        targets = Array.Empty<Creature>();
        foreach (Player player in combat.Players)
        {
            // 与骰子 UI 的高亮规则一致：仅本地玩家已投掷、未锁定、未结算的骰子可交互。
            if (!LocalContext.IsMe(player)
                || !LibrarySpeedDice.TryGetState(player, out LibrarySpeedDiceCombatState? state)
                || state == null || !state.HasRolled || state.IsLocked || state.IsResolving)
            {
                continue;
            }

            NCreature? creatureNode = room.GetCreatureNode(player.Creature);
            Control? ui = creatureNode != null && GodotObject.IsInstanceValid(creatureNode)
                ? creatureNode.GetNodeOrNull<Control>(CreatureUiPath)
                    ?? creatureNode.GetNodeOrNull<Control>(HealthBarUiPath)
                : null;
            if (ui == null || !ui.IsVisibleInTree())
            {
                continue;
            }

            Vector2 mouse = ui.GetViewport().GetMousePosition();
            foreach (Control button in ui.GetChildren().OfType<Control>())
            {
                if (!TryParseSlotIndex(button.Name, out int index) || index >= state.Slots.Count)
                {
                    continue;
                }

                LibrarySpeedDiceSlot slot = state.Slots[index];
                if (slot.IsSpent || slot.Card == null || !button.IsVisibleInTree()
                    || !(button.GetGlobalRect().HasPoint(mouse) || button.HasFocus()))
                {
                    continue;
                }

                card = slot.Card;
                target = slot.Target;
                targets = TargetLineTargets(state, slot);
                return true;
            }
        }

        return false;
    }

    internal static TargetType GetTargetType(CardModel card) =>
        card is ILibrarySpeedDiceCard speedDiceCard ? speedDiceCard.SpeedDiceTargetType : card.TargetType;

    private static bool TryParseSlotIndex(string name, out int index)
    {
        index = -1;
        if (!name.StartsWith(SlotButtonPrefix, StringComparison.Ordinal)
            || !int.TryParse(name.AsSpan(SlotButtonPrefix.Length), out int slotNumber) || slotNumber < 1)
        {
            return false;
        }

        index = slotNumber - 1;
        return true;
    }
}
