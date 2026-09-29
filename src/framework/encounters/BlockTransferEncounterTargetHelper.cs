using System.Linq;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.RoadHome;
using LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.framework.combat;

namespace LibraryOfRuina.framework.encounters;

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
                LibraryOfRuina.content.abnormalities.WrathServant.WrathServant => WrathServantEncounterHelper.IsWrathServantEncounter(combatState),
                WoodsmanTree => WarmheartedWoodsmanEncounterHelper.IsWarmheartedWoodsmanEncounter(combatState),
                RoadHomeHouse => RoadHomeEncounterHelper.IsRoadHomeElite(combatState),
                ForestKeeperBirdBase => PunishingBirdEncounterHelper.IsEncounter(combatState),
                _ => false
            });
    }
}
