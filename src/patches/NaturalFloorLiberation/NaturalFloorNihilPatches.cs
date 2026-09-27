using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using LibraryOfRuina.events.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.patches.NaturalFloorLiberation;

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots))]
internal static class NaturalFloorNihilEntryPatch
{
    private static void Prefix(EncounterModel __instance, IRunState runState)
    {
        if (__instance is NaturalFloorLiberationEncounter encounter)
        {
            encounter.ChooseEntryBranch(runState);
        }
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Stun), typeof(Creature), typeof(Func<IReadOnlyList<Creature>, Task>), typeof(string))]
internal static class NaturalFloorNihilExternalStunPatch
{
    private static bool Prefix(Creature creature, ref Task __result)
    {
        if (creature.Monster is not NaturalFloorNihilBoss)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(LibraryCreatureCmd), nameof(LibraryCreatureCmd.Stun), typeof(LibraryCreature), typeof(Func<IReadOnlyList<Creature>, Task>), typeof(string))]
internal static class NaturalFloorNihilChaosStunPatch
{
    private static bool Prefix(LibraryCreature creature, ref Task __result)
    {
        if (creature.Monster is not NaturalFloorNihilBoss boss
            || boss.Form == NaturalFloorNihilForm.Wrath && creature.CurrentChaoValue <= 0)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.ScaleMonsterHpForMultiplayer))]
internal static class NaturalFloorMagicalGirlFixedHpPatch
{
    // 终战仅魔法少女同伴保持固定生命；虚无缥缈与石像沿用多人生命缩放。
    private static bool Prefix(Creature __instance) =>
        __instance.Monster is not NaturalFloorMagicalGirl;
}

[HarmonyPatch(typeof(LibraryCreature), nameof(LibraryCreature.SaveAndSetStunResistance))]
internal static class NaturalFloorNihilRestoreStunWindowPatch
{
    private static void Postfix(LibraryCreature __instance, ref int ____stunPlayerTurnsRemaining)
    {
        if (__instance.Monster is NaturalFloorNihilMonster { RestoringStunTurns: > 0 } monster)
        {
            ____stunPlayerTurnsRemaining = monster.RestoringStunTurns;
            monster.RestoringStunTurns = 0;
        }
    }
}

[HarmonyPatch(typeof(EventRoom), nameof(EventRoom.ToSerializable))]
internal static class NaturalFloorSpecialRewardSavePatch
{
    private static void Postfix(EventRoom __instance, SerializableRoom __result)
    {
        if (__instance.CanonicalEvent is NaturalFloorLiberationSettlementEvent)
        {
            __result.EncounterState["NaturalFloorKills"] = NaturalFloorLiberationSettlementStore.KilledBossCount.ToString();
            __result.EncounterState["NaturalFloorNihilComplete"] = NaturalFloorLiberationSettlementStore.NihilCompleted.ToString();
        }
    }
}

[HarmonyPatch(typeof(AbstractRoom), nameof(AbstractRoom.FromSerializable))]
internal static class NaturalFloorSpecialRewardLoadPatch
{
    private static void Prefix(SerializableRoom? serializableRoom)
    {
        if (serializableRoom?.EventId != ModelDb.AncientEvent<NaturalFloorLiberationSettlementEvent>().Id)
        {
            return;
        }

        serializableRoom.EncounterState.TryGetValue("NaturalFloorKills", out string? savedKills);
        serializableRoom.EncounterState.TryGetValue("NaturalFloorNihilComplete", out string? savedComplete);
        int.TryParse(savedKills, out int kills);
        bool.TryParse(savedComplete, out bool complete);
        NaturalFloorLiberationSettlementStore.Restore(kills, complete);
    }
}
