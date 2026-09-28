using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.cards.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

// 神奇粉末的目标限制只属于该牌，不能通过全战斗 ShouldAllowTargeting Hook 限制队友。
internal static class SocialFloorMagicalPowderTargetPatch
{
    internal static void FilterIsValidTarget(CardModel __instance, Creature? target, ref bool __result)
    {
        if (__instance is SocialFloorMagicalPowderCard)
        {
            __result = __result && SocialFloorMagicalPowderCard.IsPowderTarget(target);
        }
    }
}

internal static class SocialFloorMagicalPowderTargetSelectionPatch
{
    internal static void OnStartTargetingFromCard(Control control, ref Func<Node, bool>? nodeFilter)
    {
        if (control is not NCard { Model: SocialFloorMagicalPowderCard })
        {
            return;
        }

        Func<Node, bool>? previousFilter = nodeFilter;
        nodeFilter = node =>
            (previousFilter?.Invoke(node) ?? true)
            && node is NCreature creature
            && SocialFloorMagicalPowderCard.IsPowderTarget(creature.Entity);
    }
}
