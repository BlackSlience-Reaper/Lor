using System;
using System.Linq;
using LibraryOfRuina.afflictions;
using LibraryOfRuina.cards;
using LibraryOfRuina.cards.HistoryFloorLiberation;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.content.abnormalities.BigBadWolf;
using LibraryOfRuina.content.abnormalities.DeadButterfly;
using LibraryOfRuina.content.abnormalities.FairyFestival;
using LibraryOfRuina.content.abnormalities.ForsakenMurderer;
using LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;
using LibraryOfRuina.content.abnormalities.HappyTeddy;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.RedShoes;
using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.content.abnormalities.SongMachine;
using LibraryOfRuina.content.abnormalities.SpiderBud;
using LibraryOfRuina.content.abnormalities.TodaysShyLook;
using LibraryOfRuina.encounters.BrotherhoodOfIron;
using LibraryOfRuina.encounters.DawnOffice;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.HookOffice;
using LibraryOfRuina.encounters.KuroKumo;
using LibraryOfRuina.encounters.MusiciansOfBremen;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.encounters.Tomerry;
using LibraryOfRuina.encounters.WedgeOffice;
using LibraryOfRuina.encounters.YunOffice;
using LibraryOfRuina.events.HistoryFloorLiberation;
using LibraryOfRuina.events.TechnologyFloorLiberation;
using LibraryOfRuina.events.WarpTrain;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.guests.HookOffice;
using LibraryOfRuina.guests.MusiciansOfBremen;
using LibraryOfRuina.guests.WedgeOffice;
using LibraryOfRuina.guests.YunOffice;
using LibraryOfRuina.intents;
using LibraryOfRuina.interop.descriptors;
using LibraryOfRuina.interop.ids;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using LibraryOfRuina.monsters.Tomerry;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.DawnOffice;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.powers.MusiciansOfBremen;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.powers.WedgeOffice;
using LibraryOfRuina.relics.StandaloneRelics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.interop.catalog;

internal static class LibraryOfRuinaInteropRegistry
{
    private const string PublicIdPrefix = "library_of_ruina.";

    private static readonly IReadOnlyList<LibraryOfRuinaInteropDescriptor> _allModelDescriptors =
    [
        
        Model(LibraryOfRuinaPublicIds.Afflictions.CostReduction, LibraryOfRuinaInteropCategory.Afflictions, RawModelCategories.Affliction, "LIBRARY_OF_RUINA_COST_REDUCTION_AFFLICTION", typeof(LibraryOfRuinaCostReductionAffliction), "LIBRARY_OF_RUINA_COST_REDUCTION_AFFLICTION"),
        Model(LibraryOfRuinaPublicIds.Afflictions.FuneralSeal, LibraryOfRuinaInteropCategory.Afflictions, RawModelCategories.Affliction, "FUNERAL_SEAL_AFFLICTION", typeof(FuneralSealAffliction), "FUNERAL_SEAL_AFFLICTION"),

        
        Model(LibraryOfRuinaPublicIds.Cards.AllAroundHelperChargeChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "ALL_AROUND_HELPER_CHARGE_CHOICE_CARD", typeof(AllAroundHelperChargeChoiceCard), "ALL_AROUND_HELPER_CHARGE_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.AllAroundHelperRecognitionFunctionChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "ALL_AROUND_HELPER_RECOGNITION_FUNCTION_CHOICE_CARD", typeof(AllAroundHelperRecognitionFunctionChoiceCard), "ALL_AROUND_HELPER_RECOGNITION_FUNCTION_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.AllAroundHelperCleanChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "ALL_AROUND_HELPER_CLEAN_CHOICE_CARD", typeof(AllAroundHelperCleanChoiceCard), "ALL_AROUND_HELPER_CLEAN_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.LittleRedScarChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "LITTLE_RED_SCAR_CHOICE_CARD", typeof(LittleRedScarChoiceCard), "LITTLE_RED_SCAR_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.LittleRedRevengeChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "LITTLE_RED_REVENGE_CHOICE_CARD", typeof(LittleRedRevengeChoiceCard), "LITTLE_RED_REVENGE_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.LittleRedPreyChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "LITTLE_RED_PREY_CHOICE_CARD", typeof(LittleRedPreyChoiceCard), "LITTLE_RED_PREY_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.Tasty, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "TASTY_CARD", typeof(TastyCard), "TASTY_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.TodaysShyLookTodaysExpressionChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "TODAYS_SHY_LOOK_TODAYS_EXPRESSION_CHOICE_CARD", typeof(TodaysShyLookTodaysExpressionChoiceCard), "TODAYS_SHY_LOOK_TODAYS_EXPRESSION_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.TodaysShyLookShynessChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "TODAYS_SHY_LOOK_SHYNESS_CHOICE_CARD", typeof(TodaysShyLookShynessChoiceCard), "TODAYS_SHY_LOOK_SHYNESS_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.TodaysShyLookSocialDistanceChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "TODAYS_SHY_LOOK_SOCIAL_DISTANCE_CHOICE_CARD", typeof(TodaysShyLookSocialDistanceChoiceCard), "TODAYS_SHY_LOOK_SOCIAL_DISTANCE_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.RedShoesGlitterChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "RED_SHOES_GLITTER_CHOICE_CARD", typeof(RedShoesGlitterChoiceCard), "RED_SHOES_GLITTER_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.RedShoesBloodThirstChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "RED_SHOES_BLOOD_THIRST_CHOICE_CARD", typeof(RedShoesBloodThirstChoiceCard), "RED_SHOES_BLOOD_THIRST_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.RedShoesAxeChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "RED_SHOES_AXE_CHOICE_CARD", typeof(RedShoesAxeChoiceCard), "RED_SHOES_AXE_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.SpiderBudCocoonBindChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "SPIDER_BUD_COCOON_BIND_CHOICE_CARD", typeof(SpiderBudCocoonBindChoiceCard), "SPIDER_BUD_COCOON_BIND_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.SpiderBudFeedingChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "SPIDER_BUD_FEEDING_CHOICE_CARD", typeof(SpiderBudFeedingChoiceCard), "SPIDER_BUD_FEEDING_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.SpiderBudVigilanceChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "SPIDER_BUD_VIGILANCE_CHOICE_CARD", typeof(SpiderBudVigilanceChoiceCard), "SPIDER_BUD_VIGILANCE_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.FuneralRestChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "FUNERAL_REST_CHOICE_CARD", typeof(FuneralRestChoiceCard), "FUNERAL_REST_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.FuneralCoffinChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "FUNERAL_COFFIN_CHOICE_CARD", typeof(FuneralCoffinChoiceCard), "FUNERAL_COFFIN_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.FuneralMourningChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "FUNERAL_MOURNING_CHOICE_CARD", typeof(FuneralMourningChoiceCard), "FUNERAL_MOURNING_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.SongMachineMusicChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "SONG_MACHINE_MUSIC_CHOICE_CARD", typeof(SongMachineMusicChoiceCard), "SONG_MACHINE_MUSIC_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.SongMachineMelodyChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "SONG_MACHINE_MELODY_CHOICE_CARD", typeof(SongMachineMelodyChoiceCard), "SONG_MACHINE_MELODY_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.SongMachineAddictionChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "SONG_MACHINE_ADDICTION_CHOICE_CARD", typeof(SongMachineAddictionChoiceCard), "SONG_MACHINE_ADDICTION_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.LeticiaPageSurpriseGiftChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "LETICIA_PAGE_SURPRISE_GIFT_CHOICE_CARD", typeof(LeticiaPageSurpriseGiftChoiceCard), "LETICIA_PAGE_SURPRISE_GIFT_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.LeticiaPageBuddyChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "LETICIA_PAGE_BUDDY_CHOICE_CARD", typeof(LeticiaPageBuddyChoiceCard), "LETICIA_PAGE_BUDDY_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.LeticiaPageMischiefChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "LETICIA_PAGE_MISCHIEF_CHOICE_CARD", typeof(LeticiaPageMischiefChoiceCard), "LETICIA_PAGE_MISCHIEF_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.ForgottenLongingEmbraceEgo, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "FORGOTTEN_LONGING_EMBRACE_EGO_CARD", typeof(ForgottenLongingEmbraceEgoCard), "FORGOTTEN_LONGING_EMBRACE_EGO_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.FlutteringHungerFrenzyEgo, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "FLUTTERING_HUNGER_FRENZY_EGO_CARD", typeof(FlutteringHungerFrenzyEgoCard), "FLUTTERING_HUNGER_FRENZY_EGO_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.PunishmentStrikeEgo, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "PUNISHMENT_STRIKE_EGO_CARD", typeof(PunishmentStrikeEgoCard), "PUNISHMENT_STRIKE_EGO_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.ShatteredLifeEgo, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "SHATTERED_LIFE_EGO_CARD", typeof(ShatteredLifeEgoCard), "SHATTERED_LIFE_EGO_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.RegretEgo, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "REGRET_EGO_CARD", typeof(RegretEgoCard), "REGRET_EGO_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.ChordEgo, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "CHORD_EGO_CARD", typeof(ChordEgoCard), "CHORD_EGO_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.ForestKeeperLockStatus, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "FOREST_KEEPER_LOCK_STATUS_CARD", typeof(ForestKeeperLockStatusCard), "FOREST_KEEPER_LOCK_STATUS_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.PunishingBirdPunishmentChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "PUNISHING_BIRD_PUNISHMENT_CHOICE_CARD", typeof(PunishingBirdPunishmentChoiceCard), "PUNISHING_BIRD_PUNISHMENT_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.PunishingBirdPunitiveBeakChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "PUNISHING_BIRD_PUNITIVE_BEAK_CHOICE_CARD", typeof(PunishingBirdPunitiveBeakChoiceCard), "PUNISHING_BIRD_PUNITIVE_BEAK_CHOICE_CARD"),
        Model(LibraryOfRuinaPublicIds.Cards.PunishingBirdFlutteringWingsChoice, LibraryOfRuinaInteropCategory.Cards, RawModelCategories.Card, "PUNISHING_BIRD_FLUTTERING_WINGS_CHOICE_CARD", typeof(PunishingBirdFlutteringWingsChoiceCard), "PUNISHING_BIRD_FLUTTERING_WINGS_CHOICE_CARD"),

        
        Model(LibraryOfRuinaPublicIds.Enchantments.LeticiaPartnerMark, LibraryOfRuinaInteropCategory.Enchantments, RawModelCategories.Enchantment, "LETICIA_PARTNER_MARK_ENCHANTMENT", typeof(LeticiaPartnerMarkEnchantment), "LETICIA_PARTNER_MARK_ENCHANTMENT"),

        
        Model(LibraryOfRuinaPublicIds.Encounters.AllAroundHelperStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "ALL_AROUND_HELPER_STRONG", typeof(AllAroundHelperStrong), "ALL_AROUND_HELPER_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.AllAroundHelperWeak, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "ALL_AROUND_HELPER_WEAK", typeof(AllAroundHelperWeak), "ALL_AROUND_HELPER_WEAK"),
        Model(LibraryOfRuinaPublicIds.Encounters.BigBadWolfWeak, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "BIG_BAD_WOLF_WEAK", typeof(BigBadWolfWeak), "BIG_BAD_WOLF_WEAK"),
        Model(LibraryOfRuinaPublicIds.Encounters.BrotherhoodOfIronStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "BROTHERHOOD_OF_IRON_STRONG", typeof(BrotherhoodOfIronStrong), "BROTHERHOOD_OF_IRON_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.DawnOfficeNormal, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "DAWN_OFFICE_NORMAL", typeof(DawnOfficeNormal), "DAWN_OFFICE_NORMAL"),
        Model(LibraryOfRuinaPublicIds.Encounters.DeadButterflyWeak, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "DEAD_BUTTERFLY_WEAK", typeof(DeadButterflyWeak), "DEAD_BUTTERFLY_WEAK"),
        Model(LibraryOfRuinaPublicIds.Encounters.DeadButterflyStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "DEAD_BUTTERFLY_STRONG", typeof(DeadButterflyStrong), "DEAD_BUTTERFLY_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.FairyFestivalStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "FAIRY_FESTIVAL_STRONG", typeof(FairyFestivalStrong), "FAIRY_FESTIVAL_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.FinnWeak, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "FINN_WEAK", typeof(FinnWeak), "FINN_WEAK"),
        Model(LibraryOfRuinaPublicIds.Encounters.ForsakenMurdererWeak, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "FORSAKEN_MURDERER_WEAK", typeof(ForsakenMurdererWeak), "FORSAKEN_MURDERER_WEAK"),
        Model(LibraryOfRuinaPublicIds.Encounters.FuneralOfTheDeadButterflies, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "FUNERAL_OF_THE_DEAD_BUTTERFLIES_ENCOUNTER", typeof(FuneralOfTheDeadButterfliesEncounter), "FUNERAL_OF_THE_DEAD_BUTTERFLIES_ENCOUNTER"),
        Model(LibraryOfRuinaPublicIds.Encounters.HappyTeddyWeak, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "HAPPY_TEDDY_WEAK", typeof(HappyTeddyWeak), "HAPPY_TEDDY_WEAK"),
        Model(LibraryOfRuinaPublicIds.Encounters.HistoryFloorLiberation, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "HISTORY_FLOOR_LIBERATION_ENCOUNTER", typeof(HistoryFloorLiberationEncounter), "HISTORY_FLOOR_LIBERATION_ENCOUNTER"),
        Model(LibraryOfRuinaPublicIds.Encounters.TechnologyFloorLiberation, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "TECHNOLOGY_FLOOR_LIBERATION_ENCOUNTER", typeof(TechnologyFloorLiberationEncounter), "TECHNOLOGY_FLOOR_LIBERATION_ENCOUNTER"),
        Model(LibraryOfRuinaPublicIds.Encounters.HookOfficeStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "HOOK_OFFICE_STRONG", typeof(HookOfficeStrong), "HOOK_OFFICE_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.KuroKumoNormal, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "KURO_KUMO_NORMAL", typeof(KuroKumoNormal), "KURO_KUMO_NORMAL"),
        Model(LibraryOfRuinaPublicIds.Encounters.MusiciansOfBremenNormal, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "MUSICIANS_OF_BREMEN_NORMAL", typeof(MusiciansOfBremenNormal), "MUSICIANS_OF_BREMEN_NORMAL"),
        Model(LibraryOfRuinaPublicIds.Encounters.QueenOfHatredStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "QUEEN_OF_HATRED_STRONG", typeof(QueenOfHatredStrong), "QUEEN_OF_HATRED_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.RedShoesStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "RED_SHOES_STRONG", typeof(RedShoesStrong), "RED_SHOES_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.SpiderBudStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "SPIDER_BUD_STRONG", typeof(SpiderBudStrong), "SPIDER_BUD_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.ScorchedGirl, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "SCORCHED_GIRL", typeof(ScorchedGirl), "SCORCHED_GIRL"),
        Model(LibraryOfRuinaPublicIds.Encounters.Tomerry, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "TOMERRY_ENCOUNTER", typeof(TomerryEncounter), "TOMERRY_ENCOUNTER"),
        Model(LibraryOfRuinaPublicIds.Encounters.TodaysShyLookStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "TODAYS_SHY_LOOK_STRONG", typeof(TodaysShyLookStrong), "TODAYS_SHY_LOOK_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.WedgeOfficeNormal, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "WEDGE_OFFICE_NORMAL", typeof(WedgeOfficeNormal), "WEDGE_OFFICE_NORMAL"),
        Model(LibraryOfRuinaPublicIds.Encounters.PunishingBirdStrong, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "PUNISHING_BIRD_STRONG", typeof(PunishingBirdStrong), "PUNISHING_BIRD_STRONG"),
        Model(LibraryOfRuinaPublicIds.Encounters.YunOfficeNormal, LibraryOfRuinaInteropCategory.Encounters, RawModelCategories.Encounter, "YUN_OFFICE_NORMAL", typeof(YunOfficeNormal), "YUN_OFFICE_NORMAL"),

        
        Model(LibraryOfRuinaPublicIds.Events.FuneralOfTheDeadButterflies, LibraryOfRuinaInteropCategory.Events, RawModelCategories.Event, "FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT", typeof(FuneralOfTheDeadButterfliesEvent), "FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT"),
        Model(LibraryOfRuinaPublicIds.Events.SingingMachine, LibraryOfRuinaInteropCategory.Events, RawModelCategories.Event, "SINGING_MACHINE_EVENT", typeof(SingingMachineEvent), "SINGING_MACHINE_EVENT"),
        Model(LibraryOfRuinaPublicIds.Events.WarpTrain, LibraryOfRuinaInteropCategory.Events, RawModelCategories.Event, "WARP_TRAIN_EVENT", typeof(WarpTrainEvent), "WARP_TRAIN_EVENT"),

        
        Model(LibraryOfRuinaPublicIds.Ancients.HistoryFloorLiberationSettlement, LibraryOfRuinaInteropCategory.Ancients, RawModelCategories.AncientEvent, "HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT", typeof(HistoryFloorLiberationSettlementEvent), "HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT"),
        Model(LibraryOfRuinaPublicIds.Ancients.TechnologyFloorLiberationSettlement, LibraryOfRuinaInteropCategory.Ancients, RawModelCategories.AncientEvent, "TECHNOLOGY_FLOOR_LIBERATION_SETTLEMENT_EVENT", typeof(TechnologyFloorLiberationSettlementEvent), "TECHNOLOGY_FLOOR_LIBERATION_SETTLEMENT_EVENT"),

        
        Model(LibraryOfRuinaPublicIds.Monsters.AllAroundHelper, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "ALL_AROUND_HELPER", typeof(AllAroundHelper), "ALL_AROUND_HELPER"),
        Model(LibraryOfRuinaPublicIds.Monsters.Arnold, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "ARNOLD", typeof(Arnold), "ARNOLD"),
        Model(LibraryOfRuinaPublicIds.Monsters.BigBadWolf, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "BIG_BAD_WOLF", typeof(BigBadWolf), "BIG_BAD_WOLF"),
        Model(LibraryOfRuinaPublicIds.Monsters.SpiderBud, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "SPIDER_BUD", typeof(SpiderBud), "SPIDER_BUD"),
        Model(LibraryOfRuinaPublicIds.Monsters.SpiderBudSmallSpider, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "SPIDER_BUD_SMALL_SPIDER", typeof(SpiderBudSmallSpider), "SPIDER_BUD_SMALL_SPIDER"),
        Model(LibraryOfRuinaPublicIds.Monsters.Consta, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "CONSTA", typeof(Consta), "CONSTA"),
        Model(LibraryOfRuinaPublicIds.Monsters.DeadButterfly, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "DEAD_BUTTERFLY", typeof(DeadButterfly), "DEAD_BUTTERFLY"),
        Model(LibraryOfRuinaPublicIds.Monsters.Eri, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "ERI", typeof(Eri), "ERI"),
        Model(LibraryOfRuinaPublicIds.Monsters.Finn, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "FINN", typeof(Finn), "FINN"),
        Model(LibraryOfRuinaPublicIds.Monsters.ForsakenMurderer, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "FORSAKEN_MURDERER", typeof(ForsakenMurderer), "FORSAKEN_MURDERER"),
        Model(LibraryOfRuinaPublicIds.Monsters.FuneralOfTheDeadButterflies, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "FUNERAL_OF_THE_DEAD_BUTTERFLIES", typeof(FuneralOfTheDeadButterflies), "FUNERAL_OF_THE_DEAD_BUTTERFLIES"),
        Model(LibraryOfRuinaPublicIds.Monsters.FairyMass, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "FAIRY_MASS", typeof(FairyMass), "FAIRY_MASS"),
        Model(LibraryOfRuinaPublicIds.Monsters.FairyQueen, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "FAIRY_QUEEN", typeof(FairyQueen), "FAIRY_QUEEN"),
        Model(LibraryOfRuinaPublicIds.Monsters.HappyTeddyMonster, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HAPPY_TEDDY_MONSTER", typeof(HappyTeddyMonster), "HAPPY_TEDDY_MONSTER"),
        Model(LibraryOfRuinaPublicIds.Monsters.Gin, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "GIN", typeof(Gin), "GIN"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorEndLightBoss, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_END_LIGHT_BOSS", typeof(HistoryFloorEndLightBoss), "HISTORY_FLOOR_END_LIGHT_BOSS"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorForgottenBoss, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_FORGOTTEN_BOSS", typeof(HistoryFloorForgottenBoss), "HISTORY_FLOOR_FORGOTTEN_BOSS"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorFlutteringBoss, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_FLUTTERING_BOSS", typeof(HistoryFloorFlutteringBoss), "HISTORY_FLOOR_FLUTTERING_BOSS"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorFlutteringMass, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_FLUTTERING_MASS", typeof(HistoryFloorFlutteringMass), "HISTORY_FLOOR_FLUTTERING_MASS"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorLastMatch, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_LAST_MATCH", typeof(HistoryFloorLastMatch), "HISTORY_FLOOR_LAST_MATCH"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorPhaseBoss, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_PHASE_BOSS", typeof(HistoryFloorPhaseBoss), "HISTORY_FLOOR_PHASE_BOSS"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorWaspBoss, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_WASP_BOSS", typeof(HistoryFloorWaspBoss), "HISTORY_FLOOR_WASP_BOSS"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorWorkerBee, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_WORKER_BEE", typeof(HistoryFloorWorkerBee), "HISTORY_FLOOR_WORKER_BEE"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorEmeraldBoughBoss, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_EMERALD_BOUGH_BOSS", typeof(HistoryFloorEmeraldBoughBoss), "HISTORY_FLOOR_EMERALD_BOUGH_BOSS"),
        Model(LibraryOfRuinaPublicIds.Monsters.HistoryFloorVineBarrier, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "HISTORY_FLOOR_VINE_BARRIER", typeof(HistoryFloorVineBarrier), "HISTORY_FLOOR_VINE_BARRIER"),
        Model(LibraryOfRuinaPublicIds.Monsters.TechnologyFloorRegretBoss, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "TECHNOLOGY_FLOOR_REGRET_BOSS", typeof(TechnologyFloorRegretBoss), "TECHNOLOGY_FLOOR_REGRET_BOSS"),
        Model(LibraryOfRuinaPublicIds.Monsters.Meow, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "MEOW", typeof(Meow), "MEOW"),
        Model(LibraryOfRuinaPublicIds.Monsters.Mccullin, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "MCCULLIN", typeof(Mccullin), "MCCULLIN"),
        Model(LibraryOfRuinaPublicIds.Monsters.Mo, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "MO", typeof(Mo), "MO"),
        Model(LibraryOfRuinaPublicIds.Monsters.MuMu, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "MU_MU", typeof(MuMu), "MU_MU"),
        Model(LibraryOfRuinaPublicIds.Monsters.Naoki, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "NAOKI", typeof(Naoki), "NAOKI"),
        Model(LibraryOfRuinaPublicIds.Monsters.Oink, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "OINK", typeof(Oink), "OINK"),
        Model(LibraryOfRuinaPublicIds.Monsters.Oscar, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "OSCAR", typeof(Oscar), "OSCAR"),
        Model(LibraryOfRuinaPublicIds.Monsters.Pamela, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "PAMELA", typeof(Pamela), "PAMELA"),
        Model(LibraryOfRuinaPublicIds.Monsters.Pameli, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "PAMELI", typeof(Pameli), "PAMELI"),
        Model(LibraryOfRuinaPublicIds.Monsters.Philip, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "PHILIP", typeof(Philip), "PHILIP"),
        Model(LibraryOfRuinaPublicIds.Monsters.QueenOfHatred, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "QUEEN_OF_HATRED", typeof(QueenOfHatred), "QUEEN_OF_HATRED"),
        Model(LibraryOfRuinaPublicIds.Monsters.RedShoesLeft, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "RED_SHOES_LEFT", typeof(RedShoesLeft), "RED_SHOES_LEFT"),
        Model(LibraryOfRuinaPublicIds.Monsters.RedShoesRight, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "RED_SHOES_RIGHT", typeof(RedShoesRight), "RED_SHOES_RIGHT"),
        Model(LibraryOfRuinaPublicIds.Monsters.Salvador, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "SALVADOR", typeof(Salvador), "SALVADOR"),
        Model(LibraryOfRuinaPublicIds.Monsters.Sayo, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "SAYO", typeof(Sayo), "SAYO"),
        Model(LibraryOfRuinaPublicIds.Monsters.ScorchedGirlMonster, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "SCORCHED_GIRL_MONSTER", typeof(ScorchedGirlMonster), "SCORCHED_GIRL_MONSTER"),
        Model(LibraryOfRuinaPublicIds.Monsters.Taein, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "TAEIN", typeof(Taein), "TAEIN"),
        Model(LibraryOfRuinaPublicIds.Monsters.TheFourthMatchFlame, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "THE_FOURTH_MATCH_FLAME", typeof(TheFourthMatchFlame), "THE_FOURTH_MATCH_FLAME"),
        Model(LibraryOfRuinaPublicIds.Monsters.Tomerry, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "TOMERRY", typeof(Tomerry), "TOMERRY"),
        Model(LibraryOfRuinaPublicIds.Monsters.TodaysShyLook, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "TODAYS_SHY_LOOK", typeof(TodaysShyLook), "TODAYS_SHY_LOOK"),
        Model(LibraryOfRuinaPublicIds.Monsters.Yang, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "YANG", typeof(Yang), "YANG"),
        Model(LibraryOfRuinaPublicIds.Monsters.Yun, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "YUN", typeof(Yun), "YUN"),
        Model(LibraryOfRuinaPublicIds.Monsters.Yuna, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "YUNA", typeof(Yuna), "YUNA"),
        Model(LibraryOfRuinaPublicIds.Monsters.PunishingBird, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "PUNISHING_BIRD", typeof(PunishingBird), "PUNISHING_BIRD"),
        Model(LibraryOfRuinaPublicIds.Monsters.ForestKeeperBirdLeft, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "FOREST_KEEPER_BIRD_LEFT", typeof(ForestKeeperBirdLeft), "FOREST_KEEPER_BIRD_LEFT"),
        Model(LibraryOfRuinaPublicIds.Monsters.ForestKeeperBirdRight, LibraryOfRuinaInteropCategory.Monsters, RawModelCategories.Monster, "FOREST_KEEPER_BIRD_RIGHT", typeof(ForestKeeperBirdRight), "FOREST_KEEPER_BIRD_RIGHT"),

        
        Model(LibraryOfRuinaPublicIds.Powers.AllAroundHelperRecognitionMode, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_ALL_AROUND_HELPER_RECOGNITION_MODE_POWER", typeof(LibraryOfRuinaAllAroundHelperRecognitionModePower), "ALL_AROUND_HELPER_RECOGNITION_MODE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.AllAroundHelperSwift, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_ALL_AROUND_HELPER_SWIFT_POWER", typeof(LibraryOfRuinaAllAroundHelperSwiftPower), "ALL_AROUND_HELPER_SWIFT_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ImprovDrumming, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_IMPROV_DRUMMING_POWER", typeof(LibraryOfRuinaImprovDrummingPower), "IMPROV_DRUMMING_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.BleedThorns, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_BLEED_THORNS_POWER", typeof(LibraryOfRuinaBleedThornsPower), "BLEED_THORNS_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.BigBadWolfPunishEvil, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "BIG_BAD_WOLF_PUNISH_EVIL_POWER", typeof(BigBadWolfPunishEvilPower), "BIG_BAD_WOLF_PUNISH_EVIL_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.BigBadWolfBornToBe, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "BIG_BAD_WOLF_BORN_TO_BE_POWER", typeof(BigBadWolfBornToBePower), "BIG_BAD_WOLF_BORN_TO_BE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.BlindNavigation, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_BLIND_NAVIGATION_POWER", typeof(LibraryOfRuinaBlindNavigationPower), "BLIND_NAVIGATION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.BloodThirst, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_BLOOD_THIRST_POWER", typeof(LibraryOfRuinaBloodThirstPower), "BLOOD_THIRST_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.CostReduction, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_COST_REDUCTION_POWER", typeof(LibraryOfRuinaCostReductionPower), "COST_REDUCTION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.DawnFire, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_DAWN_FIRE_POWER", typeof(LibraryOfRuinaDawnFirePower), "DAWN_FIRE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ForsakenMurdererFear, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_FORSAKEN_MURDERER_FEAR_POWER", typeof(LibraryOfRuinaForsakenMurdererFearPower), "FORSAKEN_MURDERER_FEAR_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.GhostConceal, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_GHOST_CONCEAL_POWER", typeof(LibraryOfRuinaGhostConcealPower), "GHOST_CONCEAL_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HappyTeddyAffection, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_HAPPY_TEDDY_AFFECTION_POWER", typeof(LibraryOfRuinaHappyTeddyAffectionPower), "HAPPY_TEDDY_AFFECTION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ForgottenAffection, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FORGOTTEN_AFFECTION_POWER", typeof(ForgottenAffectionPower), "FORGOTTEN_AFFECTION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ForgottenAffectionAttack, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FORGOTTEN_AFFECTION_ATTACK_POWER", typeof(ForgottenAffectionAttackPower), "FORGOTTEN_AFFECTION_ATTACK_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ForgottenLongingEmbrace, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FORGOTTEN_LONGING_EMBRACE_POWER", typeof(ForgottenLongingEmbracePower), "FORGOTTEN_LONGING_EMBRACE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FlutteringMomentarySatiety, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FLUTTERING_MOMENTARY_SATIETY_POWER", typeof(FlutteringMomentarySatietyPower), "FLUTTERING_MOMENTARY_SATIETY_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FlutteringHunger, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FLUTTERING_HUNGER_POWER", typeof(FlutteringHungerPower), "FLUTTERING_HUNGER_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FlutteringHungerFrenzy, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FLUTTERING_HUNGER_FRENZY_POWER", typeof(FlutteringHungerFrenzyPower), "FLUTTERING_HUNGER_FRENZY_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FlutteringFreshMeatPassive, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FLUTTERING_FRESH_MEAT_PASSIVE_POWER", typeof(FlutteringFreshMeatPassivePower), "FLUTTERING_FRESH_MEAT_PASSIVE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FlutteringFreshMeat, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FLUTTERING_FRESH_MEAT_POWER", typeof(FlutteringFreshMeatPower), "FLUTTERING_FRESH_MEAT_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FlutteringMassCare, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FLUTTERING_MASS_CARE_POWER", typeof(FlutteringMassCarePower), "FLUTTERING_MASS_CARE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.Confusion, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_CONFUSION_POWER", typeof(LibraryOfRuinaConfusionPower), "LIBRARY_OF_RUINA_CONFUSION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HistoryFloorCorrosion, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "HISTORY_FLOOR_CORROSION_POWER", typeof(HistoryFloorCorrosionPower), "HISTORY_FLOOR_CORROSION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HistoryFloorLiberationController, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "HISTORY_FLOOR_LIBERATION_CONTROLLER_POWER", typeof(HistoryFloorLiberationControllerPower), "HISTORY_FLOOR_LIBERATION_CONTROLLER_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HistoryFloorRekindledSpark, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "HISTORY_FLOOR_REKINDLED_SPARK_POWER", typeof(HistoryFloorRekindledSparkPower), "HISTORY_FLOOR_REKINDLED_SPARK_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HistoryFloorWaspSpore, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "HISTORY_FLOOR_WASP_SPORE_POWER", typeof(HistoryFloorWaspSporePower), "HISTORY_FLOOR_WASP_SPORE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HistoryFloorWaspParalysis, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "HISTORY_FLOOR_WASP_PARALYSIS_POWER", typeof(HistoryFloorWaspParalysisPower), "HISTORY_FLOOR_WASP_PARALYSIS_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HistoryFloorWaspExpansion, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "HISTORY_FLOOR_WASP_EXPANSION_POWER", typeof(HistoryFloorWaspExpansionPower), "HISTORY_FLOOR_WASP_EXPANSION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.HistoryFloorWaspPheromone, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "HISTORY_FLOOR_WASP_PHEROMONE_POWER", typeof(HistoryFloorWaspPheromonePower), "HISTORY_FLOOR_WASP_PHEROMONE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.Paralysis, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_PARALYSIS_POWER", typeof(LibraryOfRuinaParalysisPower), "LIBRARY_OF_RUINA_PARALYSIS_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.TechnologyFloorErosion, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "TECHNOLOGY_FLOOR_EROSION_POWER", typeof(TechnologyFloorErosionPower), "TECHNOLOGY_FLOOR_EROSION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.TechnologyFloorLiberationController, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "TECHNOLOGY_FLOOR_LIBERATION_CONTROLLER_POWER", typeof(TechnologyFloorLiberationControllerPower), "TECHNOLOGY_FLOOR_LIBERATION_CONTROLLER_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.RegretIronEcho, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "REGRET_IRON_ECHO_POWER", typeof(RegretIronEchoPower), "REGRET_IRON_ECHO_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.RegretExtremeViolence, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "REGRET_EXTREME_VIOLENCE_POWER", typeof(RegretExtremeViolencePower), "REGRET_EXTREME_VIOLENCE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.RegretFear, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "REGRET_FEAR_POWER", typeof(RegretFearPower), "REGRET_FEAR_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.RegretEndBeginEnd, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "REGRET_END_BEGIN_END_POWER", typeof(RegretEndBeginEndPower), "REGRET_END_BEGIN_END_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.EmeraldBoughVineBarrier, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "EMERALD_BOUGH_VINE_BARRIER_POWER", typeof(EmeraldBoughVineBarrierPower), "EMERALD_BOUGH_VINE_BARRIER_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.EmeraldBoughForestApple, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "EMERALD_BOUGH_FOREST_APPLE_POWER", typeof(EmeraldBoughForestApplePower), "EMERALD_BOUGH_FOREST_APPLE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.EmeraldBoughWhereAreYou, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "EMERALD_BOUGH_WHERE_ARE_YOU_POWER", typeof(EmeraldBoughWhereAreYouPower), "EMERALD_BOUGH_WHERE_ARE_YOU_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.EmeraldBoughStranglingVine, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "EMERALD_BOUGH_STRANGLING_VINE_POWER", typeof(EmeraldBoughStranglingVinePower), "EMERALD_BOUGH_STRANGLING_VINE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.StaggerResistance, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_STAGGER_RESISTANCE_POWER", typeof(LibraryStaggerResistancePower), "LIBRARY_OF_RUINA_STAGGER_RESISTANCE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.InkOver, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_INK_OVER_POWER", typeof(LibraryOfRuinaInkOverPower), "INK_OVER_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.LittleRedPrey, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LITTLE_RED_PREY_POWER", typeof(LittleRedPreyPower), "LITTLE_RED_PREY_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.SalvationHand, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_SALVATION_HAND_POWER", typeof(LibraryOfRuinaSalvationHandPower), "LIBRARY_OF_RUINA_SALVATION_HAND_POWER"),
        
        Model(LibraryOfRuinaPublicIds.Powers.NextTurnStrength, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_NEXT_TURN_STRENGTH", typeof(LibraryOfRuinaNextTurnStrength), "NEXT_TURN_STRENGTH_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.PoisonFang, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_POISON_FANG_POWER", typeof(LibraryOfRuinaPoisonFangPower), "POISON_FANG_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.PreservedDamage, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "PRESERVED_DAMAGE_POWER", typeof(PreservedDamagePower), "PRESERVED_DAMAGE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.QueenBadGuy, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_QUEEN_BAD_GUY_POWER", typeof(LibraryOfRuinaQueenBadGuyPower), "QUEEN_BAD_GUY_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.QueenBind, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_QUEEN_BIND_POWER", typeof(LibraryOfRuinaQueenBindPower), "QUEEN_BIND_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.QueenInTheNameOfHatred, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_QUEEN_IN_THE_NAME_OF_HATRED_POWER", typeof(LibraryOfRuinaQueenInTheNameOfHatredPower), "QUEEN_IN_THE_NAME_OF_HATRED_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.QueenInversion, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_QUEEN_INVERSION_POWER", typeof(LibraryOfRuinaQueenInversionPower), "QUEEN_INVERSION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.QueenMagic, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_QUEEN_MAGIC_POWER", typeof(LibraryOfRuinaQueenMagicPower), "QUEEN_MAGIC_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.RedShoesBloodAttraction, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_RED_SHOES_BLOOD_ATTRACTION_POWER", typeof(LibraryOfRuinaRedShoesBloodAttractionPower), "RED_SHOES_BLOOD_ATTRACTION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.Mark, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_MARK_POWER", typeof(LibraryOfRuinaMarkPower), "LIBRARY_OF_RUINA_MARK_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ScorchedGirlAshes, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_SCORCHED_GIRL_ASHES_POWER", typeof(LibraryOfRuinaScorchedGirlAshesPower), "SCORCHED_GIRL_ASHES_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ScorchedGirlExtinguishedSpark, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_SCORCHED_GIRL_EXTINGUISHED_SPARK_POWER", typeof(LibraryOfRuinaScorchedGirlExtinguishedSparkPower), "SCORCHED_GIRL_EXTINGUISHED_SPARK_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.LastLove, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_LAST_LOVE_POWER", typeof(LibraryOfRuinaLastLovePower), "LAST_LOVE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.TodaysShyLookExpression, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_TODAYS_SHY_LOOK_EXPRESSION_POWER", typeof(LibraryOfRuinaTodaysShyLookExpressionPower), "TODAYS_SHY_LOOK_EXPRESSION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FaintMemories, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_FAINT_MEMORIES_POWER", typeof(LibraryOfRuinaFaintMemoriesPower), "FAINT_MEMORIES_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FocusOfAttention, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "LIBRARY_OF_RUINA_FOCUS_OF_ATTENTION_POWER", typeof(LibraryOfRuinaFocusOfAttentionPower), "LIBRARY_OF_RUINA_FOCUS_OF_ATTENTION_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FairyFestivalReservedFood, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FAIRY_FESTIVAL_RESERVED_FOOD_POWER", typeof(FairyFestivalReservedFoodPower), "FAIRY_FESTIVAL_RESERVED_FOOD_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FairyMassCare, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FAIRY_MASS_CARE_POWER", typeof(FairyMassCarePower), "FAIRY_MASS_CARE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FairyQueenMomentarySatiety, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FAIRY_QUEEN_MOMENTARY_SATIETY_POWER", typeof(FairyQueenMomentarySatietyPower), "FAIRY_QUEEN_MOMENTARY_SATIETY_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.FairyQueenStarvedFrenzy, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FAIRY_QUEEN_STARVED_FRENZY_POWER", typeof(FairyQueenStarvedFrenzyPower), "FAIRY_QUEEN_STARVED_FRENZY_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.WedgePerseverance, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "WEDGE_PERSEVERANCE_POWER", typeof(WedgePerseverancePower), "WEDGE_PERSEVERANCE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.WedgePiercing, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "WEDGE_PIERCING_POWER", typeof(WedgePiercingPower), "WEDGE_PIERCING_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.PunishingBirdNoBad, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "PUNISHING_BIRD_NO_BAD_POWER", typeof(PunishingBirdNoBadPower), "PUNISHING_BIRD_NO_BAD_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.PunishingBirdPunish, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "PUNISHING_BIRD_PUNISH_POWER", typeof(PunishingBirdPunishPower), "PUNISHING_BIRD_PUNISH_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.PunishingBirdCageChains, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "PUNISHING_BIRD_CAGE_CHAINS_POWER", typeof(PunishingBirdCageChainsPower), "PUNISHING_BIRD_CAGE_CHAINS_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.ForestKeeperStolenChains, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "FOREST_KEEPER_STOLEN_CHAINS_POWER", typeof(ForestKeeperStolenChainsPower), "FOREST_KEEPER_STOLEN_CHAINS_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.SpiderBudStartHunting, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "SPIDER_BUD_START_HUNTING_POWER", typeof(SpiderBudStartHuntingPower), "SPIDER_BUD_START_HUNTING_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.Untargetable, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "UNTARGETABLE_POWER", typeof(UntargetablePower), "UNTARGETABLE_POWER"),
        Model(LibraryOfRuinaPublicIds.Powers.SpiderBudUntargetable, LibraryOfRuinaInteropCategory.Powers, RawModelCategories.Power, "SPIDER_BUD_UNTARGETABLE_POWER", typeof(SpiderBudUntargetablePower), "SPIDER_BUD_UNTARGETABLE_POWER"),

        
        Model(LibraryOfRuinaPublicIds.Relics.SpiderBudPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "SPIDER_BUD_PAGE_RELIC", typeof(SpiderBudPageRelic), "SPIDER_BUD_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.AllAroundHelperPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "ALL_AROUND_HELPER_PAGE_RELIC", typeof(AllAroundHelperPageRelic), "ALL_AROUND_HELPER_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.DeadButterfliesBook, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "DEAD_BUTTERFLIES_BOOK_RELIC", typeof(DeadButterfliesBookRelic), "DEAD_BUTTERFLIES_BOOK_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.LittleRedMercenaryPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "LITTLE_RED_MERCENARY_PAGE_RELIC", typeof(LittleRedMercenaryPageRelic), "LITTLE_RED_MERCENARY_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.MagicCurse, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "MAGIC_CURSE_RELIC", typeof(MagicCurseRelic), "MAGIC_CURSE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.ForsakenMurdererPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "FORSAKEN_MURDERER_PAGE_RELIC", typeof(ForsakenMurdererPageRelic), "FORSAKEN_MURDERER_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.FuneralOfTheDeadButterfliesPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "FUNERAL_OF_THE_DEAD_BUTTERFLIES_PAGE_RELIC", typeof(FuneralOfTheDeadButterfliesPageRelic), "FUNERAL_OF_THE_DEAD_BUTTERFLIES_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.LeticiaPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "LETICIA_PAGE_RELIC", typeof(LeticiaPageRelic), "LETICIA_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.PunishingBirdPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "PUNISHING_BIRD_PAGE_RELIC", typeof(PunishingBirdPageRelic), "PUNISHING_BIRD_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.FairyFestivalPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "FAIRY_FESTIVAL_PAGE_RELIC", typeof(FairyFestivalPageRelic), "FAIRY_FESTIVAL_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.HappyTeddyPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "HAPPY_TEDDY_PAGE_RELIC", typeof(HappyTeddyPageRelic), "HAPPY_TEDDY_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.QueenOfHatredPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "QUEEN_OF_HATRED_PAGE_RELIC", typeof(QueenOfHatredPageRelic), "QUEEN_OF_HATRED_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.RedShoesPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "RED_SHOES_PAGE_RELIC", typeof(RedShoesPageRelic), "RED_SHOES_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.SongMachinePage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "SONG_MACHINE_PAGE_RELIC", typeof(SongMachinePageRelic), "SONG_MACHINE_PAGE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.ProofOfExistence, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "PROOF_OF_EXISTENCE_RELIC", typeof(ProofOfExistenceRelic), "PROOF_OF_EXISTENCE_RELIC"),
        Model(LibraryOfRuinaPublicIds.Relics.TodaysShyLookPage, LibraryOfRuinaInteropCategory.Relics, RawModelCategories.Relic, "TODAYS_SHY_LOOK_PAGE_RELIC", typeof(TodaysShyLookPageRelic), "TODAYS_SHY_LOOK_PAGE_RELIC"),
    ];

    private static readonly IReadOnlyList<LibraryOfRuinaInteropDescriptor> _allIntentDescriptors =
    [
        Intent(LibraryOfRuinaPublicIds.Intents.BadgedAttack, typeof(BadgedAttackIntent), IntentType.Attack, "BADGED_ATTACK"),
        Intent(LibraryOfRuinaPublicIds.Intents.BadgedTargetedAttack, typeof(BadgedTargetedAttackIntent), IntentType.Attack, "BADGED_TARGETED_ATTACK"),
        Intent(LibraryOfRuinaPublicIds.Intents.BadgedDefend, typeof(BadgedDefendIntent), IntentType.Defend, "BADGED_DEFEND"),
        Intent(LibraryOfRuinaPublicIds.Intents.BadgedBuff, typeof(BadgedBuffIntent), IntentType.Buff, "BADGED_BUFF"),
        Intent(LibraryOfRuinaPublicIds.Intents.BadgedDebuff, typeof(BadgedDebuffIntent), IntentType.Debuff, "BADGED_DEBUFF"),
        Intent(LibraryOfRuinaPublicIds.Intents.DynamicAttack, typeof(DynamicAttackIntent), IntentType.Attack, "ATTACK"),
        Intent(LibraryOfRuinaPublicIds.Intents.FoxAttack, typeof(FoxAttackIntent), IntentType.Attack, "FOX_ATTACK"),
        Intent(LibraryOfRuinaPublicIds.Intents.FoxBuff, typeof(FoxBuffIntent), IntentType.Buff, "FOX_BUFF"),
        Intent(LibraryOfRuinaPublicIds.Intents.FoxDefend, typeof(FoxDefendIntent), IntentType.Defend, "FOX_DEFEND"),
        Intent(LibraryOfRuinaPublicIds.Intents.FoxEnergy, typeof(FoxEnergyIntent), IntentType.Buff, "FOX_ENERGY"),
        Intent(LibraryOfRuinaPublicIds.Intents.FoxWeak, typeof(FoxWeakIntent), IntentType.Debuff, "FOX_WEAK"),
        Intent(LibraryOfRuinaPublicIds.Intents.SpiderBudWebDebuff, typeof(SpiderBudWebDebuffIntent), IntentType.Debuff, "SPIDER_BUD_WEB_DEBUFF"),
    ];

    private static readonly IReadOnlyList<LibraryOfRuinaInteropDescriptor> _allDescriptors;
    private static readonly IReadOnlyDictionary<string, LibraryOfRuinaInteropDescriptor> _byPublicId;
    private static readonly IReadOnlyDictionary<ModelId, LibraryOfRuinaInteropDescriptor> _byRawModelId;

    internal static LibraryOfRuinaModelCatalog Models { get; }

    internal static LibraryOfRuinaIntentCatalog Intents { get; }

    internal static IReadOnlyList<LibraryOfRuinaInteropDescriptor> AllDescriptors => _allDescriptors;

    static LibraryOfRuinaInteropRegistry()
    {
        _allDescriptors = [.. _allModelDescriptors, .. _allIntentDescriptors];

        Dictionary<string, LibraryOfRuinaInteropDescriptor> byPublicId = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<ModelId, LibraryOfRuinaInteropDescriptor> byRawModelId = new();
        foreach (LibraryOfRuinaInteropDescriptor descriptor in _allDescriptors)
        {
            byPublicId.Add(descriptor.PublicId, descriptor);
            if (descriptor.RawModelId != null)
            {
                byRawModelId.Add(descriptor.RawModelId, descriptor);
            }
        }

        _byPublicId = byPublicId;
        _byRawModelId = byRawModelId;

        Models = new LibraryOfRuinaModelCatalog(
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Afflictions),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Cards),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Enchantments),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Encounters),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Events),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Ancients),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Monsters),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Powers),
            GetModelsByCategory(LibraryOfRuinaInteropCategory.Relics));
        Intents = new LibraryOfRuinaIntentCatalog(_allIntentDescriptors);

        List<string> validationErrors = ValidateContract().ToList();
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException("LibraryOfRuina interop contract is invalid:\n" + string.Join('\n', validationErrors));
        }
    }

    internal static bool TryGetDescriptor(string publicId, out LibraryOfRuinaInteropDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            descriptor = null!;
            return false;
        }

        return _byPublicId.TryGetValue(publicId, out descriptor!);
    }

    internal static bool TryGetDescriptor(ModelId rawModelId, out LibraryOfRuinaInteropDescriptor descriptor)
    {
        if (rawModelId == null)
        {
            descriptor = null!;
            return false;
        }

        return _byRawModelId.TryGetValue(rawModelId, out descriptor!);
    }

    internal static bool TryGetModelDescriptor(string publicId, out LibraryOfRuinaInteropDescriptor descriptor)
    {
        if (TryGetDescriptor(publicId, out descriptor) && descriptor.RawModelId != null)
        {
            return true;
        }

        descriptor = null!;
        return false;
    }

    internal static bool TryParseCategoryFromPublicId(string publicId, out LibraryOfRuinaInteropCategory category)
    {
        category = default;
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return false;
        }

        string[] parts = publicId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "library_of_ruina", StringComparison.Ordinal))
        {
            return false;
        }

        return parts[1] switch
        {
            "afflictions" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Afflictions, out category),
            "cards" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Cards, out category),
            "enchantments" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Enchantments, out category),
            "encounters" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Encounters, out category),
            "events" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Events, out category),
            "ancients" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Ancients, out category),
            "monsters" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Monsters, out category),
            "powers" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Powers, out category),
            "relics" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Relics, out category),
            "intents" => ReturnParsedCategory(LibraryOfRuinaInteropCategory.Intents, out category),
            _ => false
        };
    }

    private static bool ReturnParsedCategory(LibraryOfRuinaInteropCategory parsedCategory, out LibraryOfRuinaInteropCategory category)
    {
        category = parsedCategory;
        return true;
    }

    private static IReadOnlyList<LibraryOfRuinaInteropDescriptor> GetModelsByCategory(LibraryOfRuinaInteropCategory category)
    {
        return _allModelDescriptors.Where(d => d.Category == category).ToArray();
    }

    private static LibraryOfRuinaInteropDescriptor Model(
        string publicId,
        LibraryOfRuinaInteropCategory category,
        string rawCategory,
        string rawEntry,
        Type runtimeType,
        string textKeyRoot)
    {
        return new LibraryOfRuinaInteropDescriptor(
            publicId,
            category,
            new ModelId(rawCategory, rawEntry),
            runtimeType,
            textKeyRoot,
            isExperimental: false,
            isDebug: false,
            intentKind: null);
    }

    private static LibraryOfRuinaInteropDescriptor Intent(
        string publicId,
        Type runtimeType,
        IntentType intentKind,
        string textKeyRoot)
    {
        return new LibraryOfRuinaInteropDescriptor(
            publicId,
            LibraryOfRuinaInteropCategory.Intents,
            rawModelId: null,
            runtimeType,
            textKeyRoot,
            isExperimental: false,
            isDebug: false,
            intentKind);
    }

    private static IEnumerable<string> ValidateContract()
    {
        HashSet<string> seenPublicIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<ModelId> seenRawModelIds = new();

        foreach (LibraryOfRuinaInteropDescriptor descriptor in _allDescriptors)
        {
            if (!seenPublicIds.Add(descriptor.PublicId))
            {
                yield return $"Duplicate public ID: {descriptor.PublicId}";
            }

            if (!descriptor.PublicId.StartsWith(PublicIdPrefix, StringComparison.Ordinal))
            {
                yield return $"Public ID must start with '{PublicIdPrefix}': {descriptor.PublicId}";
            }

            if (!string.Equals(descriptor.PublicId, descriptor.PublicId.ToLowerInvariant(), StringComparison.Ordinal))
            {
                yield return $"Public ID must be lowercase: {descriptor.PublicId}";
            }

            if (!TryParseCategoryFromPublicId(descriptor.PublicId, out LibraryOfRuinaInteropCategory parsedCategory))
            {
                yield return $"Public ID format is invalid: {descriptor.PublicId}";
            }
            else if (parsedCategory != descriptor.Category)
            {
                yield return $"Public ID category mismatch: {descriptor.PublicId} -> {parsedCategory}, descriptor={descriptor.Category}";
            }

            if (descriptor.IsExperimental)
            {
                yield return $"v1 formal descriptor cannot be experimental: {descriptor.PublicId}";
            }

            if (descriptor.IsDebug)
            {
                yield return $"v1 formal descriptor cannot be debug: {descriptor.PublicId}";
            }

            if (descriptor.Category == LibraryOfRuinaInteropCategory.Intents)
            {
                if (descriptor.RawModelId != null)
                {
                    yield return $"Intent descriptor should not have RawModelId: {descriptor.PublicId}";
                }

                if (descriptor.IntentKind == null)
                {
                    yield return $"Intent descriptor missing IntentKind: {descriptor.PublicId}";
                }

                if (!typeof(AbstractIntent).IsAssignableFrom(descriptor.RuntimeType))
                {
                    yield return $"Intent runtime type must inherit AbstractIntent: {descriptor.PublicId} ({descriptor.RuntimeType.FullName})";
                }
            }
            else
            {
                if (descriptor.RawModelId == null)
                {
                    yield return $"Model descriptor missing RawModelId: {descriptor.PublicId}";
                }

                if (descriptor.IntentKind != null)
                {
                    yield return $"Model descriptor should not have IntentKind: {descriptor.PublicId}";
                }

                if (!typeof(AbstractModel).IsAssignableFrom(descriptor.RuntimeType))
                {
                    yield return $"Model runtime type must inherit AbstractModel: {descriptor.PublicId} ({descriptor.RuntimeType.FullName})";
                }

                if (descriptor.RawModelId != null && !seenRawModelIds.Add(descriptor.RawModelId))
                {
                    yield return $"Duplicate raw ModelId: {descriptor.RawModelId}";
                }
            }
        }
    }

    private static class RawModelCategories
    {
        public const string Affliction = "AFFLICTION";
        public const string Card = "CARD";
        public const string Enchantment = "ENCHANTMENT";
        public const string Encounter = "ENCOUNTER";
        public const string Event = "EVENT";
        public const string AncientEvent = "ANCIENT_EVENT";
        public const string Monster = "MONSTER";
        public const string Power = "POWER";
        public const string Relic = "RELIC";
    }
}
