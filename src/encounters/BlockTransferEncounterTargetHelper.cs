using System.Linq;
using LibraryOfRuina.combat;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.LittleRedMercenary;
using LibraryOfRuina.encounters.PunishingBird;
using LibraryOfRuina.encounters.RoadHome;
using LibraryOfRuina.encounters.WarmheartedWoodsman;
using LibraryOfRuina.encounters.WrathServant;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using LibraryOfRuina.monsters.LittleRedMercenary;
using LibraryOfRuina.monsters.PunishingBird;
using LibraryOfRuina.monsters.RoadHome;
using LibraryOfRuina.monsters.WarmheartedWoodsman;

namespace LibraryOfRuina.encounters;

internal static class BlockTransferEncounterTargetHelper
{
    public static bool IsSupportedEncounter(CombatStateLike? combatState)
    {
        return LittleRedMercenaryEncounterHelper.IsLittleRedEncounter(combatState)
            || combatState?.Encounter is LanguageFloorLiberationEncounter
            || combatState?.Encounter is NaturalFloorLiberationEncounter
            || WrathServantEncounterHelper.IsWrathServantEncounter(combatState)
            || WarmheartedWoodsmanEncounterHelper.IsWarmheartedWoodsmanEncounter(combatState)
            || RoadHomeEncounterHelper.IsRoadHomeElite(combatState)
            || PunishingBirdEncounterHelper.IsEncounter(combatState);
    }

    public static Creature? FindPartner(CombatStateLike? combatState)
    {
        Creature? partner = LittleRedMercenaryEncounterHelper.FindLittleRed(combatState)
            ?? (combatState?.Encounter as NaturalFloorLiberationEncounter)?.Rage
            ?? combatState?.Creatures.FirstOrDefault(
                static creature => creature.Monster is LanguageFloorScarletScar)
            ?? WrathServantEncounterHelper.FindServant(combatState)
            ?? WarmheartedWoodsmanEncounterHelper.FindTree(combatState)
            ?? RoadHomeEncounterHelper.FindHouse(combatState);
        return AllyTurnRegistry.CanTransferBlockWith(partner) ? partner : null;
    }

    public static IReadOnlyList<Creature> FindPartners(CombatStateLike? combatState)
    {
        if (combatState?.Encounter is NaturalFloorLiberationEncounter { CurrentPhase: 5 } natural)
        {
            return natural.LivingNihilGirls().Select(girl => girl.Creature)
                .Where(AllyTurnRegistry.CanTransferBlockWith).ToArray();
        }

        if (PunishingBirdEncounterHelper.IsEncounter(combatState))
        {
            return PunishingBirdEncounterHelper.LivingKeepers(combatState)
                .Where(AllyTurnRegistry.CanTransferBlockWith)
                .ToArray();
        }

        Creature? partner = FindPartner(combatState);
        return AllyTurnRegistry.CanTransferBlockWith(partner) ? [partner!] : [];
    }

    public static bool IsBlockTransferPartner(CombatStateLike? combatState, Creature? creature)
    {
        return AllyTurnRegistry.CanTransferBlockWith(creature)
            && (creature?.Monster switch
            {
                LittleRedRidingHoodedMercenary => LittleRedMercenaryEncounterHelper.IsLittleRedEncounter(combatState),
                LanguageFloorScarletScar => combatState?.Encounter is LanguageFloorLiberationEncounter,
                NaturalFloorBlindRageBoss => combatState?.Encounter is NaturalFloorLiberationEncounter,
                NaturalFloorMagicalGirl => combatState?.Encounter is NaturalFloorLiberationEncounter { CurrentPhase: 5 },
                monsters.WrathServant.WrathServant => WrathServantEncounterHelper.IsWrathServantEncounter(combatState),
                WoodsmanTree => WarmheartedWoodsmanEncounterHelper.IsWarmheartedWoodsmanEncounter(combatState),
                RoadHomeHouse => RoadHomeEncounterHelper.IsRoadHomeElite(combatState),
                ForestKeeperBirdBase => PunishingBirdEncounterHelper.IsEncounter(combatState),
                _ => false
            });
    }
}
