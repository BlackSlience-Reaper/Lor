using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 在终局奖励界面继续时调用；返回 false 表示已接管。</summary>
internal static class HistoryFloorLiberationSettlementRedirect
{
    internal static bool TryRedirect(RunManager __instance, ref Task __result)
    {
        if (__instance.DebugOnlyGetState()?.CurrentRoom is not CombatRoom { Encounter: HistoryFloorLiberationEncounter encounter })
        {
            return true;
        }

        LiberationSettlementProceedHelper.ClearLingeringCardPreviews();

        if (!HistoryFloorLiberationSettlementStore.PendingSettlement && encounter.KilledBossCount >= 2)
        {
            HistoryFloorLiberationSettlementStore.Record(encounter);
        }

        if (!HistoryFloorLiberationSettlementStore.PendingSettlement)
        {
            return true;
        }

        HistoryFloorLiberationSettlementStore.Consume();
        __result = __instance.EnterRoom(new EventRoom(ModelDb.AncientEvent<HistoryFloorLiberationSettlementEvent>()));
        return false;
    }
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CreateScene))]
[LibraryPatch(Reason = "原版 CreateScene 非虚且场景路径按遭遇 id 固定，无法按阶段换场景；只作用于历史层解放遭遇的第 3–5 阶段。")]
internal static class HistoryFloorLiberationCreateScenePatch
{
    private static bool Prefix(EncounterModel __instance, ref Control __result)
    {
        if (__instance is not HistoryFloorLiberationEncounter encounter)
        {
            return true;
        }

        switch (encounter.CurrentPhase)
        {
            case 3:
                __result = HistoryFloorLiberationEncounter.InstantiateFlutteringEncounterScene();
                return false;
            case 4:
                __result = HistoryFloorLiberationEncounter.InstantiateWaspEncounterScene();
                return false;
            case 5:
                __result = HistoryFloorLiberationEncounter.InstantiateEmeraldBoughEncounterScene();
                return false;
            default:
                return true;
        }
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets))]
internal static class HistoryFloorLiberationPlayerPositionPatch
{
    private static readonly Vector2 PlayerOffset = new(70f, 0f);

    private static void Postfix(List<NCreature> creatureNodes)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not CombatRoom { Encounter: HistoryFloorLiberationEncounter })
        {
            return;
        }

        foreach (NCreature node in creatureNodes.Where(static node => node.Entity.IsPlayer))
        {
            node.Position += PlayerOffset;
        }
    }
}

[HarmonyPatch(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay.SetCreature))]
internal static class HistoryFloorForgottenStateDisplayPatch
{
    private const float StateDisplayDropY = 32f;


    private static void Postfix(Creature creature, NCreatureStateDisplay __instance)
    {
        if (creature.Monster is not HistoryFloorForgottenBoss)
        {
            return;
        }

        Vector2 droppedPosition = __instance.Position + Vector2.Down * StateDisplayDropY;
        __instance.Position = droppedPosition;
        VanillaPrivate.CreatureStateDisplayOriginalPosition.Set(__instance, droppedPosition);
    }
}
