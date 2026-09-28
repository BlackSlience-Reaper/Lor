using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.cards.PunishingBird;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.PunishingBird;

// 锁链只限制自身的目标，不能通过全战斗 Hook 阻止卡牌和药水选中玩家。
internal static class ForestKeeperLockTargetPatch
{
    internal static void FilterIsValidTarget(CardModel __instance, Creature? target, ref bool __result)
    {
        if (__instance is ForestKeeperLockStatusCard)
        {
            __result = __result && ForestKeeperLockStatusCard.IsLockTarget(target);
        }
    }
}

internal static class ForestKeeperLockTargetSelectionPatch
{
    internal static void OnStartTargetingFromCard(Control control, ref Func<Node, bool>? nodeFilter)
    {
        if (control is not NCard { Model: ForestKeeperLockStatusCard })
        {
            return;
        }

        Func<Node, bool>? previousFilter = nodeFilter;
        nodeFilter = node =>
            (previousFilter?.Invoke(node) ?? true)
            && node is NCreature creature
            && ForestKeeperLockStatusCard.IsLockTarget(creature.Entity);
    }
}
