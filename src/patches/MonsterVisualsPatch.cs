using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using LibraryOfRuina.monsters.BurrowingHeaven;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.monsters.Ozma;
using LibraryOfRuina.visuals;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LibraryOfRuina.infra.patching;
using AddictedEmployeeCreatureVisuals = LibraryOfRuina.visuals.AddictedEmployee.AddictedEmployeeCreatureVisuals;
using AllAroundHelperCreatureVisuals = LibraryOfRuina.visuals.AllAroundHelper.AllAroundHelperCreatureVisuals;
using ArnoldCreatureVisuals = LibraryOfRuina.visuals.BrotherhoodOfIron.ArnoldCreatureVisuals;
using ArtFloorDaCapoCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.ArtFloorDaCapoCreatureVisuals;
using ArtFloorDaCapoPerformerCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.ArtFloorDaCapoPerformerCreatureVisuals;
using ArtFloorDustbornPersonCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.ArtFloorDustbornPersonCreatureVisuals;
using ArtFloorFirstPerformerCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.ArtFloorFirstPerformerCreatureVisuals;
using ArtFloorLittleGalaxyCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.ArtFloorLittleGalaxyCreatureVisuals;
using ArtFloorNostalgicScentCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.ArtFloorNostalgicScentCreatureVisuals;
using ArtFloorPleasureCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.ArtFloorPleasureCreatureVisuals;
using BeyondFragmentCreatureVisuals = LibraryOfRuina.visuals.ArtFloorLiberation.BeyondFragmentCreatureVisuals;
using BigBadWolfCreatureVisuals = LibraryOfRuina.visuals.BigBadWolf.BigBadWolfCreatureVisuals;
using BigBirdCreatureVisuals = LibraryOfRuina.visuals.BigBird.BigBirdCreatureVisuals;
using BlueStarAltarCreatureVisuals = LibraryOfRuina.visuals.BlueStar.BlueStarAltarCreatureVisuals;
using BlueStarFollowerCreatureVisuals = LibraryOfRuina.visuals.BlueStar.BlueStarFollowerCreatureVisuals;
using BloodBatCreatureVisuals = LibraryOfRuina.visuals.Nosferatu.BloodBatCreatureVisuals;
using BurrowingHeavenCreatureVisuals = LibraryOfRuina.visuals.BurrowingHeaven.BurrowingHeavenCreatureVisuals;
using ConstaCreatureVisuals = LibraryOfRuina.visuals.BrotherhoodOfIron.ConstaCreatureVisuals;
using CosmicFragmentCreatureVisuals = LibraryOfRuina.visuals.CosmicFragment.CosmicFragmentCreatureVisuals;
using DeadButterflyCreatureVisuals = LibraryOfRuina.visuals.DeadButterfly.DeadButterflyCreatureVisuals;
using DespairKnightCreatureVisuals = LibraryOfRuina.visuals.DespairKnight.DespairKnightCreatureVisuals;
using EriCreatureVisuals = LibraryOfRuina.visuals.WedgeOffice.EriCreatureVisuals;
using EyeballBirdCreatureVisuals = LibraryOfRuina.visuals.BigBird.EyeballBirdCreatureVisuals;
using FairyMassCreatureVisuals = LibraryOfRuina.visuals.FairyFestival.FairyMassCreatureVisuals;
using FairyQueenCreatureVisuals = LibraryOfRuina.visuals.FairyFestival.FairyQueenCreatureVisuals;
using ForgottenKnightSwordCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.ForgottenKnightSwordCreatureVisuals;
using ForsakenMurdererCreatureVisuals = LibraryOfRuina.visuals.ForsakenMurderer.ForsakenMurdererCreatureVisuals;
using FuneralOfTheDeadButterfliesCreatureVisuals = LibraryOfRuina.visuals.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterfliesCreatureVisuals;
using GalaxyFriendCreatureVisuals = LibraryOfRuina.visuals.GalaxyChild.GalaxyFriendCreatureVisuals;
using GinCreatureVisuals = LibraryOfRuina.visuals.DawnOffice.GinCreatureVisuals;
using GoldenAmberCreatureVisuals = LibraryOfRuina.visuals.KingOfGreed.GoldenAmberCreatureVisuals;
using GreenStemHermitCreatureVisuals = LibraryOfRuina.visuals.WrathServant.GreenStemHermitCreatureVisuals;
using HappyTeddyCreatureVisuals = LibraryOfRuina.visuals.HappyTeddy.HappyTeddyCreatureVisuals;
using HeartOfAspirationCreatureVisuals = LibraryOfRuina.visuals.HeartOfAspiration.HeartOfAspirationCreatureVisuals;
using HeavenThornCreatureVisuals = LibraryOfRuina.visuals.BurrowingHeaven.HeavenThornCreatureVisuals;
using HermitStaffCreatureVisuals = LibraryOfRuina.visuals.WrathServant.HermitStaffCreatureVisuals;
using HistoryFloorEmeraldBoughCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorEmeraldBoughCreatureVisuals;
using HistoryFloorEndLightCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorEndLightCreatureVisuals;
using HistoryFloorFlutteringBossCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorFlutteringBossCreatureVisuals;
using HistoryFloorFlutteringMassCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorFlutteringMassCreatureVisuals;
using HistoryFloorForgottenCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorForgottenCreatureVisuals;
using HistoryFloorLastMatchCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorLastMatchCreatureVisuals;
using HistoryFloorPhaseBossCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorPhaseBossCreatureVisuals;
using HistoryFloorVineBarrierCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorVineBarrierCreatureVisuals;
using HistoryFloorWaspBossCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorWaspBossCreatureVisuals;
using HistoryFloorWorkerBeeCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.HistoryFloorWorkerBeeCreatureVisuals;
using QueenBeeCreatureVisuals = LibraryOfRuina.visuals.QueenBee.QueenBeeCreatureVisuals;
using QueenBeeWorkerCreatureVisuals = LibraryOfRuina.visuals.QueenBee.QueenBeeWorkerCreatureVisuals;
using KaliCreatureVisuals = LibraryOfRuina.visuals.RedMist.KaliCreatureVisuals;
using KingOfGreedCreatureVisuals = LibraryOfRuina.visuals.KingOfGreed.KingOfGreedCreatureVisuals;
using LeticiaCreatureVisuals = LibraryOfRuina.visuals.Leticia.LeticiaCreatureVisuals;
using LiteratureFloorGiftBoxCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorGiftBoxCreatureVisuals;
using LiteratureFloorEnhancedSmallSpiderCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorEnhancedSmallSpiderCreatureVisuals;
using LiteratureFloorLaetitiaBossCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorLaetitiaBossCreatureVisuals;
using LiteratureFloorLittleWitchFriendCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorLittleWitchFriendCreatureVisuals;
using LiteratureFloorRedEyesCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorRedEyesCreatureVisuals;
using LiteratureFloorBloodlustCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorBloodlustCreatureVisuals;
using LiteratureFloorTodaysExpressionCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorTodaysExpressionCreatureVisuals;
using LiteratureFloorBlackSwanCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorBlackSwanCreatureVisuals;
using LiteratureFloorBlackSwanBrotherCreatureVisuals = LibraryOfRuina.visuals.LiteratureFloorLiberation.LiteratureFloorBlackSwanBrotherCreatureVisuals;
using LiteratureFloorBlackSwanBrotherBase = LibraryOfRuina.monsters.LiteratureFloorLiberation.LiteratureFloorBlackSwanBrotherBase;
using LanguageFloorDipsiaCreatureVisuals = LibraryOfRuina.visuals.LanguageFloorLiberation.LanguageFloorDipsiaCreatureVisuals;
using LanguageFloorMimicryCreatureVisuals = LibraryOfRuina.visuals.LanguageFloorLiberation.LanguageFloorMimicryCreatureVisuals;
using LanguageFloorLostEverythingWolfCreatureVisuals = LibraryOfRuina.visuals.LanguageFloorLiberation.LanguageFloorLostEverythingWolfCreatureVisuals;
using LanguageFloorCobaltScarCreatureVisuals = LibraryOfRuina.visuals.LanguageFloorLiberation.LanguageFloorCobaltScarCreatureVisuals;
using LanguageFloorMeltingCorpseCreatureVisuals = LibraryOfRuina.visuals.LanguageFloorLiberation.LanguageFloorMeltingCorpseCreatureVisuals;
using LanguageFloorScarletScarCreatureVisuals = LibraryOfRuina.visuals.LanguageFloorLiberation.LanguageFloorScarletScarCreatureVisuals;
using LanguageFloorSmilingFaceCreatureVisuals = LibraryOfRuina.visuals.LanguageFloorLiberation.LanguageFloorSmilingFaceCreatureVisuals;
using LittleRedMercenaryCreatureVisuals = LibraryOfRuina.visuals.LittleRedMercenary.LittleRedMercenaryCreatureVisuals;
using LittleWitchFriendCreatureVisuals = LibraryOfRuina.visuals.Leticia.LittleWitchFriendCreatureVisuals;
using LungOfAspirationCreatureVisuals = LibraryOfRuina.visuals.HeartOfAspiration.LungOfAspirationCreatureVisuals;
using MccullinCreatureVisuals = LibraryOfRuina.visuals.HookOffice.MccullinCreatureVisuals;
using MeltingCorpseCreatureVisuals = LibraryOfRuina.visuals.SmilingBodies.MeltingCorpseCreatureVisuals;
using MoCreatureVisuals = LibraryOfRuina.visuals.BrotherhoodOfIron.MoCreatureVisuals;
using NaokiCreatureVisuals = LibraryOfRuina.visuals.HookOffice.NaokiCreatureVisuals;
using NosferatuCreatureVisuals = LibraryOfRuina.visuals.Nosferatu.NosferatuCreatureVisuals;
using OzmaCreatureVisuals = LibraryOfRuina.visuals.Ozma.OzmaCreatureVisuals;
using OzmaJackCreatureVisuals = LibraryOfRuina.visuals.Ozma.OzmaJackCreatureVisuals;
using OscarCreatureVisuals = LibraryOfRuina.visuals.WedgeOffice.OscarCreatureVisuals;
using PamelaCreatureVisuals = LibraryOfRuina.visuals.WedgeOffice.PamelaCreatureVisuals;
using PameliCreatureVisuals = LibraryOfRuina.visuals.WedgeOffice.PameliCreatureVisuals;
using PhilipCreatureVisuals = LibraryOfRuina.visuals.DawnOffice.PhilipCreatureVisuals;
using PriceOfSilenceCreatureVisuals = LibraryOfRuina.visuals.PriceOfSilence.PriceOfSilenceCreatureVisuals;
using PhilosophyFloorTwilightCreatureVisuals = LibraryOfRuina.visuals.PhilosophyFloorLiberation.PhilosophyFloorTwilightCreatureVisuals;
using PunishingBirdCreatureVisuals = LibraryOfRuina.visuals.PunishingBird.PunishingBirdCreatureVisuals;
using ForestKeeperBirdCreatureVisuals = LibraryOfRuina.visuals.PunishingBird.ForestKeeperBirdCreatureVisuals;
using JudgementBirdCreatureVisuals = LibraryOfRuina.visuals.JudgementBird.JudgementBirdCreatureVisuals;
using EscapedBirdCreatureVisuals = LibraryOfRuina.visuals.JudgementBird.EscapedBirdCreatureVisuals;
using FalseThroneCreatureVisuals = LibraryOfRuina.visuals.SocialFloorLiberation.FalseThroneCreatureVisuals;
using EmeraldCrystalCreatureVisuals = LibraryOfRuina.visuals.SocialFloorLiberation.EmeraldCrystalCreatureVisuals;
using Environment = System.Environment;
using QueenOfHatredCreatureVisuals = LibraryOfRuina.visuals.QueenOfHatred.QueenOfHatredCreatureVisuals;
using RedShoesLeftCreatureVisuals = LibraryOfRuina.visuals.RedShoes.RedShoesLeftCreatureVisuals;
using RedShoesRightCreatureVisuals = LibraryOfRuina.visuals.RedShoes.RedShoesRightCreatureVisuals;
using RoadHomeCreatureVisuals = LibraryOfRuina.visuals.RoadHome.RoadHomeCreatureVisuals;
using RoadHomeHouseCreatureVisuals = LibraryOfRuina.visuals.RoadHome.RoadHomeHouseCreatureVisuals;
using SalvadorCreatureVisuals = LibraryOfRuina.visuals.DawnOffice.SalvadorCreatureVisuals;
using SayoCreatureVisuals = LibraryOfRuina.visuals.DawnOffice.SayoCreatureVisuals;
using ScarecrowSearchingForWisdomCreatureVisuals = LibraryOfRuina.visuals.ScarecrowSearchingForWisdom.ScarecrowSearchingForWisdomCreatureVisuals;
using ScowlingFaceCreatureVisuals = LibraryOfRuina.visuals.SocialFloorLiberation.ScowlingFaceCreatureVisuals;
using ScaredyCatCompanionCreatureVisuals = LibraryOfRuina.visuals.RoadHome.ScaredyCatCompanionCreatureVisuals;
using ScaredyCatCreatureVisuals = LibraryOfRuina.visuals.RoadHome.ScaredyCatCreatureVisuals;
using ScorchedGirlMonsterCreatureVisuals = LibraryOfRuina.visuals.ScorchedGirl.ScorchedGirlMonsterCreatureVisuals;
using ShiningHappinessCreatureVisuals = LibraryOfRuina.visuals.KingOfGreed.ShiningHappinessCreatureVisuals;
using SmilingBodiesCreatureVisuals = LibraryOfRuina.visuals.SmilingBodies.SmilingBodiesCreatureVisuals;
using SpiderBudCreatureVisuals = LibraryOfRuina.visuals.SpiderBud.SpiderBudCreatureVisuals;
using SpiderBudSmallSpiderCreatureVisuals = LibraryOfRuina.visuals.SpiderBud.SpiderBudSmallSpiderCreatureVisuals;
using SpinyBusCreatureVisuals = LibraryOfRuina.visuals.SpinyBus.SpinyBusCreatureVisuals;
using SurpriseGiftBoxCreatureVisuals = LibraryOfRuina.visuals.Leticia.SurpriseGiftBoxCreatureVisuals;
using TaeinCreatureVisuals = LibraryOfRuina.visuals.HookOffice.TaeinCreatureVisuals;
using TechnologyFloorChordBossCreatureVisuals = LibraryOfRuina.visuals.TechnologyFloorLiberation.TechnologyFloorChordBossCreatureVisuals;
using TechnologyFloorGrinderMk4BossCreatureVisuals = LibraryOfRuina.visuals.TechnologyFloorLiberation.TechnologyFloorGrinderMk4BossCreatureVisuals;
using TechnologyFloorMagicBulletBossCreatureVisuals = LibraryOfRuina.visuals.TechnologyFloorLiberation.TechnologyFloorMagicBulletBossCreatureVisuals;
using TechnologyFloorMk4HelperCreatureVisuals = LibraryOfRuina.visuals.TechnologyFloorLiberation.TechnologyFloorMk4HelperCreatureVisuals;
using TechnologyFloorRegretBossCreatureVisuals = LibraryOfRuina.visuals.TechnologyFloorLiberation.TechnologyFloorRegretBossCreatureVisuals;
using TechnologyFloorSolemnMourningBossCreatureVisuals = LibraryOfRuina.visuals.TechnologyFloorLiberation.TechnologyFloorSolemnMourningBossCreatureVisuals;
using TheFourthMatchFlameCreatureVisuals = LibraryOfRuina.visuals.HistoryFloorLiberation.TheFourthMatchFlameCreatureVisuals;
using TimeTraceCreatureVisuals = LibraryOfRuina.visuals.PriceOfSilence.TimeTraceCreatureVisuals;
using TodaysShyLookCreatureVisuals = LibraryOfRuina.visuals.TodaysShyLook.TodaysShyLookCreatureVisuals;
using TomerryCreatureVisuals = LibraryOfRuina.visuals.Tomerry.TomerryCreatureVisuals;
using WarmheartedWoodsmanCreatureVisuals = LibraryOfRuina.visuals.WarmheartedWoodsman.WarmheartedWoodsmanCreatureVisuals;
using WolfInHerNightmaresCreatureVisuals = LibraryOfRuina.visuals.LittleRedMercenary.WolfInHerNightmaresCreatureVisuals;
using WoodsmanTreeCreatureVisuals = LibraryOfRuina.visuals.WarmheartedWoodsman.WoodsmanTreeCreatureVisuals;
using WrathServantCreatureVisuals = LibraryOfRuina.visuals.WrathServant.WrathServantCreatureVisuals;
using YangCreatureVisuals = LibraryOfRuina.visuals.DawnOffice.YangCreatureVisuals;
using YunaCreatureVisuals = LibraryOfRuina.visuals.DawnOffice.YunaCreatureVisuals;
using YunCreatureVisuals = LibraryOfRuina.visuals.YunOffice.YunCreatureVisuals;

// ReSharper disable UnusedType.Global

namespace LibraryOfRuina.patches;

// CreatureVisualLayout 参数顺序：
// 1. SpritePos：怪物立绘位置，改它会移动怪物本体。
// 2. SpriteScale：怪物立绘缩放；X 为负数会左右翻转。
// 3-6. BoundsLeft/BoundsTop/BoundsRight/BoundsBottom：点击框和选择框范围。
//      Left/Right 还会影响血条和 power 栏宽度、X 对齐；Top/Bottom 不控制血条或 power 栏 Y。
// 7. CenterPos：怪物中心点，主要给受击特效/VFX 用。
// 8. IntentPos：敌人意图图标位置。
// 可选项：
// - TalkPos：说话气泡位置。
// - StateDisplayLiftY：血条、power 栏、名字条整体上移量；正数越大越往上。
internal readonly record struct CreatureVisualLayout(
    Vector2 SpritePos,
    Vector2 SpriteScale,
    float BoundsLeft,
    float BoundsTop,
    float BoundsRight,
    float BoundsBottom,
    Vector2 CenterPos,
    Vector2 IntentPos)
{
    public Vector2? TalkPos { get; init; }

    public Vector2? StolenCardPos { get; init; }

    public Vector2? StolenCardScale { get; init; }

    // Positive values move the HP bar, power row, and nameplate upward together.
    public float StateDisplayLiftY { get; init; }

    public static CreatureVisualLayout Default(float halfWidth = 120f) => new(
        SpritePos: new Vector2(0, -150f),
        SpriteScale: new Vector2(0.31f, 0.31f),
        BoundsLeft: -halfWidth,
        BoundsTop: -299.7f,
        BoundsRight: halfWidth,
        BoundsBottom: 5f,
        CenterPos: new Vector2(0, -139.8f),
        IntentPos: new Vector2(0, -333.7f));
}

internal sealed record MonsterVisualCatalogEntry(
    CreatureVisualLayout? Layout,
    SpriteVisualProfile? Profile,
    string? StaticDefaultIdleTexturePath,
    Func<MonsterModel, NCreatureVisuals> Factory,
    string? ScenePath = null);

internal static class MonsterVisualCatalog
{
    private const string BaselineLayoutIdText = """
        SAYO
        YANG
        GIN
        SCORCHED_GIRL_MONSTER
        ADDICTED_EMPLOYEE
        QUEEN_OF_HATRED
        NATURAL_FLOOR_LOVE_AND_HATRED_BOSS
        NATURAL_FLOOR_GOLD_RUSH_BOSS
        NATURAL_FLOOR_NIHIL_BOSS
        NATURAL_FLOOR_LOVE_GIRL
        NATURAL_FLOOR_JUSTICE_GIRL
        NATURAL_FLOOR_HAPPINESS_GIRL
        NATURAL_FLOOR_COURAGE_GIRL
        NATURAL_FLOOR_LOVE_STATUE
        NATURAL_FLOOR_JUSTICE_STATUE
        NATURAL_FLOOR_HAPPINESS_STATUE
        NATURAL_FLOOR_COURAGE_STATUE
        NATURAL_FLOOR_SHINING_HAPPINESS
        NATURAL_FLOOR_TEAR_EDGE_BOSS
        NATURAL_FLOOR_FORGOTTEN_SWORD
        NATURAL_FLOOR_BLIND_RAGE_BOSS
        NATURAL_FLOOR_GREEN_STEM_HERMIT
        NATURAL_FLOOR_HERMIT_STAFF
        GOLDEN_AMBER
        KING_OF_GREED
        SHINING_HAPPINESS
        LETICIA
        LITERATURE_FLOOR_LAETITIA_BOSS
        LITERATURE_FLOOR_SURPRISE_GIFT_BOX
        LITERATURE_FLOOR_LITTLE_WITCH_FRIEND
        LITERATURE_FLOOR_RED_EYES_BOSS
        LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER
        LITERATURE_FLOOR_BLOODLUST_BOSS
        LITERATURE_FLOOR_ENHANCED_LEFT_SHOE
        LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS
        LITERATURE_FLOOR_BLACK_SWAN_BOSS
        LITERATURE_FLOOR_BLACK_SWAN_FIRST_BROTHER
        LITERATURE_FLOOR_BLACK_SWAN_SECOND_BROTHER
        LITERATURE_FLOOR_BLACK_SWAN_THIRD_BROTHER
        LITERATURE_FLOOR_BLACK_SWAN_FOURTH_BROTHER
        LITERATURE_FLOOR_BLACK_SWAN_FIFTH_BROTHER
        LITERATURE_FLOOR_BLACK_SWAN_SIXTH_BROTHER
        NOSFERATU
        BLOOD_BAT
        LANGUAGE_FLOOR_BLOOD_BAT
        SURPRISE_GIFT_BOX
        LITTLE_WITCH_FRIEND
        LITTLE_RED_RIDING_HOODED_MERCENARY
        WOLF_IN_HER_NIGHTMARES
        LANGUAGE_FLOOR_SCARLET_SCAR
        LANGUAGE_FLOOR_LOST_EVERYTHING_WOLF
        LANGUAGE_FLOOR_COBALT_SCAR
        LANGUAGE_FLOOR_SMILING_FACE
        LANGUAGE_FLOOR_DIPSIA
        LANGUAGE_FLOOR_MIMICRY
        LANGUAGE_FLOOR_MELTING_CORPSE
        HAPPY_TEDDY_MONSTER
        FAIRY_QUEEN
        FAIRY_MASS
        RED_SHOES_LEFT
        RED_SHOES_RIGHT
        ALL_AROUND_HELPER
        TODAYS_SHY_LOOK
        FORSAKEN_MURDERER
        THE_FOURTH_MATCH_FLAME
        TOMERRY
        FINN
        SALVADOR
        YUNA
        OSCAR
        PAMELI
        PAMELA
        YUN
        ERI
        TAEIN
        MCCULLIN
        NAOKI
        MO
        CONSTA
        ARNOLD
        PHILIP
        SPIDER_BUD
        SPIDER_BUD_SMALL_SPIDER
        DEAD_BUTTERFLY
        FUNERAL_OF_THE_DEAD_BUTTERFLIES
        TECHNOLOGY_FLOOR_REGRET_BOSS
        TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS
        HISTORY_FLOOR_PHASE_BOSS
        HISTORY_FLOOR_END_LIGHT_BOSS
        HISTORY_FLOOR_FORGOTTEN_BOSS
        HISTORY_FLOOR_FLUTTERING_BOSS
        HISTORY_FLOOR_FLUTTERING_MASS
        HISTORY_FLOOR_WASP_BOSS
        HISTORY_FLOOR_WORKER_BEE
        HISTORY_FLOOR_LAST_MATCH
        HISTORY_FLOOR_EMERALD_BOUGH_BOSS
        HISTORY_FLOOR_VINE_BARRIER
        QUEEN_BEE
        QUEEN_BEE_WORKER
        TECHNOLOGY_FLOOR_CHORD_BOSS
        TECHNOLOGY_FLOOR_CHORD_STAFF
        TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS
        TECHNOLOGY_FLOOR_MK4_HELPER
        TECHNOLOGY_FLOOR_MAGIC_BULLET_BOSS
        ART_FLOOR_DA_CAPO_BOSS
        ART_FLOOR_FIRST_PERFORMER
        ART_FLOOR_BEYOND_FRAGMENT_BOSS
        ART_FLOOR_LITTLE_GALAXY_BOSS
        ART_FLOOR_PLEASURE_BOSS
        ART_FLOOR_NOSTALGIC_SCENT_BOSS
        ART_FLOOR_DUSTBORN_PERSON
        ART_FLOOR_GALAXY_FRIEND
        ART_FLOOR_FINAL_DA_CAPO_BOSS
        ART_FLOOR_DA_CAPO_PERFORMER
        COSMIC_FRAGMENT
        SPINY_BUS
        GALAXY_FRIEND
        BIG_BAD_WOLF
        SMILING_BODIES
        REVERBERATION_EILEEN
        GEAR_CHURCH_FOLLOWER
        REVERBERATION_PHILIP
        UNSPEAKING_CHILD
        MELTING_CORPSE
        DESPAIR_KNIGHT
        FORGOTTEN_KNIGHT_SWORD
        WRATH_SERVANT
        SCARECROW_SEARCHING_FOR_WISDOM
        BURROWING_HEAVEN
        HEAVEN_THORN
        LUNG_OF_ASPIRATION
        HEART_OF_ASPIRATION
        WARMHEARTED_WOODSMAN
        WOODSMAN_TREE
        PRICE_OF_SILENCE
        TIME_TRACE
        BIG_BIRD
        BLUE_STAR_ALTAR
        BLUE_STAR_FOLLOWER
        OZMA
        OZMA_JACK
        EYEBALL_BIRD
        PUNISHING_BIRD
        FOREST_KEEPER_BIRD_LEFT
        FOREST_KEEPER_BIRD_RIGHT
        JUDGEMENT_BIRD
        ESCAPED_BIRD
        ROAD_HOME_HOUSE
        ROAD_HOME
        SCAREDY_CAT
        SCAREDY_CAT_COMPANION
        KALI
        GREEN_STEM_HERMIT
        HERMIT_STAFF
        PHILOSOPHY_FLOOR_TWILIGHT
        FALSE_THRONE
        EMERALD_CRYSTAL
        SCOWLING_FACE
        """;

    private static readonly IReadOnlySet<string> BaselineLayoutIds =
        new HashSet<string>(
            BaselineLayoutIdText.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string>
        SpecialScenePaths = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["MEOW"] = SceneHelper.GetScenePath("creature_visuals/meow"),
            ["MU_MU"] = SceneHelper.GetScenePath("creature_visuals/mu_mu"),
            ["OINK"] = SceneHelper.GetScenePath("creature_visuals/oink"),
        };

    private static readonly object ModelAssetPathValidationLock = new();

    private static readonly HashSet<string> ValidatedModelAssetPathIds =
        new(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, MonsterVisualCatalogEntry> Entries =
        new Dictionary<string, MonsterVisualCatalogEntry>(StringComparer.Ordinal)
    {
        ["SAYO"] = new(
            Layout: new(new(-30f, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(-30f, -139.8f), new(-30f, -333.7f)),
            Profile: SayoCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<SayoCreatureVisuals>(
                    monster.Id.Entry)),
        ["YANG"] = new(
            Layout: new(new(0, -145.2f), new(0.53f, 0.53f), -124f, -299.7f, 124f, 5f, new(0, -139.8f), new(0, -333.7f)),
            Profile: YangCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<YangCreatureVisuals>(
                    monster.Id.Entry)),
        ["GIN"] = new(
            Layout: new(new(0, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(0, -139.8f), new(0, -333.7f)),
            Profile: GinCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<GinCreatureVisuals>(
                    monster.Id.Entry)),
        ["SCORCHED_GIRL_MONSTER"] = new(
            Layout: new(new(10f, -98f), new(0.52f, 0.52f), -124f, -218f, 124f, 8f, new(10f, -98f), new(-20f, -286f))
        {
            TalkPos = new Vector2(-20f, -214f)
        },
            Profile: ScorchedGirlMonsterCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ScorchedGirlMonsterCreatureVisuals>(
                    monster.Id.Entry)),
        ["ADDICTED_EMPLOYEE"] = new(
            Layout: new(new(0f, -122f), new(0.58f, 0.58f), -118f, -330f, 118f, 12f, new(0f, -122f), new(20f, -290f))
        {
            TalkPos = new Vector2(0f, -286f)
        },
            Profile: AddictedEmployeeCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<AddictedEmployeeCreatureVisuals>(
                    monster.Id.Entry)),
        ["QUEEN_OF_HATRED"] = new(
            Layout: new(new(0f, -118f), new(0.84f, 0.84f), -150f, -280f, 150f, 8f, new(0f, -120f), new(0f, -315f)),
            Profile: QueenOfHatredCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<QueenOfHatredCreatureVisuals>(
                    monster.Id.Entry)),
        ["NATURAL_FLOOR_LOVE_AND_HATRED_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorLoveAndHatredVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorLoveAndHatredVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorLoveAndHatredVisuals.ScenePath),
        ["NATURAL_FLOOR_NIHIL_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("boss")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("boss")),
        ["NATURAL_FLOOR_LOVE_GIRL"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("love")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("love")),
        ["NATURAL_FLOOR_JUSTICE_GIRL"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("justice")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("justice")),
        ["NATURAL_FLOOR_HAPPINESS_GIRL"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("happiness")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("happiness")),
        ["NATURAL_FLOOR_COURAGE_GIRL"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("courage")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("courage")),
        ["NATURAL_FLOOR_LOVE_STATUE"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("love_statue")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("love_statue")),
        ["NATURAL_FLOOR_JUSTICE_STATUE"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("justice_statue")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("justice_statue")),
        ["NATURAL_FLOOR_HAPPINESS_STATUE"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("happiness_statue")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("happiness_statue")),
        ["NATURAL_FLOOR_COURAGE_STATUE"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals>(
                    monster.Id.Entry,
                    LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("courage_statue")),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorNihilVisuals.ScenePath("courage_statue")),
        ["NATURAL_FLOOR_GOLD_RUSH_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorGoldRushVisuals>(
                monster.Id.Entry, LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorGoldRushVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorGoldRushVisuals.ScenePath),
        ["NATURAL_FLOOR_SHINING_HAPPINESS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHappinessVisuals>(
                monster.Id.Entry, LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHappinessVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHappinessVisuals.ScenePath),
        ["NATURAL_FLOOR_TEAR_EDGE_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorTearEdgeVisuals>(
                monster.Id.Entry, LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorTearEdgeVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorTearEdgeVisuals.ScenePath),
        ["NATURAL_FLOOR_FORGOTTEN_SWORD"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorForgottenSwordVisuals>(
                monster.Id.Entry, LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorForgottenSwordVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorForgottenSwordVisuals.ScenePath),
        ["GOLDEN_AMBER"] = new(
            Layout: new(new(0f, 0f), new(0.48f, 0.48f), -155f, -430f, 155f, 12f, new(0f, -215f), new(0f, -465f)),
            Profile: GoldenAmberCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<GoldenAmberCreatureVisuals>(
                    monster.Id.Entry)),
        ["KING_OF_GREED"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<KingOfGreedCreatureVisuals>(
                        monster.Id.Entry,
                        KingOfGreedCreatureVisuals.ScenePath),
            ScenePath: KingOfGreedCreatureVisuals.ScenePath),
        ["SHINING_HAPPINESS"] = new(
            Layout: new(new(0f, 0f), new(0.50f, 0.50f), -82f, -188f, 82f, 8f, new(0f, -88f), new(0f, -222f)),
            Profile: ShiningHappinessCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ShiningHappinessCreatureVisuals>(
                    monster.Id.Entry)),
        ["LETICIA"] = new(
            Layout: new(new(0f, -124f), new(0.63f, 0.63f), -92f, -305f, 92f, 8f, new(0f, -126f), new(0f, -330f))
        {
            TalkPos = new Vector2(0f, -260f)
        },
            Profile: LeticiaCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LeticiaCreatureVisuals>(
                    monster.Id.Entry)),
        ["LITERATURE_FLOOR_LAETITIA_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<LiteratureFloorLaetitiaBossCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorLaetitiaBossCreatureVisuals.ScenePath),
            ScenePath: LiteratureFloorLaetitiaBossCreatureVisuals.ScenePath),
        ["LITERATURE_FLOOR_SURPRISE_GIFT_BOX"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<LiteratureFloorGiftBoxCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorGiftBoxCreatureVisuals.ScenePath),
            ScenePath: LiteratureFloorGiftBoxCreatureVisuals.ScenePath),
        ["LITERATURE_FLOOR_LITTLE_WITCH_FRIEND"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<LiteratureFloorLittleWitchFriendCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorLittleWitchFriendCreatureVisuals.ScenePath),
            ScenePath: LiteratureFloorLittleWitchFriendCreatureVisuals.ScenePath),
        ["LITERATURE_FLOOR_RED_EYES_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<LiteratureFloorRedEyesCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorRedEyesCreatureVisuals.ScenePath),
            ScenePath: LiteratureFloorRedEyesCreatureVisuals.ScenePath),
        ["LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<LiteratureFloorEnhancedSmallSpiderCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorEnhancedSmallSpiderCreatureVisuals.ScenePath),
            ScenePath: LiteratureFloorEnhancedSmallSpiderCreatureVisuals.ScenePath),
        ["LITERATURE_FLOOR_BLOODLUST_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<LiteratureFloorBloodlustCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorBloodlustCreatureVisuals.ScenePath),
            ScenePath: LiteratureFloorBloodlustCreatureVisuals.ScenePath),
        ["LITERATURE_FLOOR_ENHANCED_LEFT_SHOE"] = new(
            Layout: new(new(0f, -98f), new(0.52f, 0.52f), -150f, -255f, 150f, 35f, new(0f, -98f), new(0f, -310f))
        {
            TalkPos = new Vector2(0f, -230f)
        },
            Profile: RedShoesLeftCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<RedShoesLeftCreatureVisuals>(
                    monster.Id.Entry)),
        ["LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<
                        LiteratureFloorTodaysExpressionCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorTodaysExpressionCreatureVisuals
                            .ScenePath),
            ScenePath: LiteratureFloorTodaysExpressionCreatureVisuals
                .ScenePath),
        ["LITERATURE_FLOOR_BLACK_SWAN_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<
                        LiteratureFloorBlackSwanCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorBlackSwanCreatureVisuals.ScenePath),
            ScenePath: LiteratureFloorBlackSwanCreatureVisuals.ScenePath),
        ["LITERATURE_FLOOR_BLACK_SWAN_FIRST_BROTHER"] =
            CreateBlackSwanBrotherRegistration(),
        ["LITERATURE_FLOOR_BLACK_SWAN_SECOND_BROTHER"] =
            CreateBlackSwanBrotherRegistration(),
        ["LITERATURE_FLOOR_BLACK_SWAN_THIRD_BROTHER"] =
            CreateBlackSwanBrotherRegistration(),
        ["LITERATURE_FLOOR_BLACK_SWAN_FOURTH_BROTHER"] =
            CreateBlackSwanBrotherRegistration(),
        ["LITERATURE_FLOOR_BLACK_SWAN_FIFTH_BROTHER"] =
            CreateBlackSwanBrotherRegistration(),
        ["LITERATURE_FLOOR_BLACK_SWAN_SIXTH_BROTHER"] =
            CreateBlackSwanBrotherRegistration(),
        ["NOSFERATU"] = new(
            Layout: new(new(0f, -128f), new(0.72f, 0.72f), -160f, -360f, 160f, 12f, new(0f, -150f), new(0f, -395f))
        {
            TalkPos = new Vector2(0f, -305f),
            StateDisplayLiftY = 18f
        },
            Profile: NosferatuCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<NosferatuCreatureVisuals>(
                    monster.Id.Entry)),
        ["BLOOD_BAT"] = new(
            Layout: new(new(0f, -70f), new(0.78f, 0.78f), -92f, -190f, 92f, 12f, new(0f, -82f), new(0f, -230f))
        {
            TalkPos = new Vector2(0f, -170f)
        },
            Profile: BloodBatCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<BloodBatCreatureVisuals>(
                    monster.Id.Entry)),
        ["LANGUAGE_FLOOR_BLOOD_BAT"] = new(
            Layout: new(new(0f, -70f), new(0.78f, 0.78f), -92f, -190f, 92f, 12f, new(0f, -82f), new(0f, -230f))
        {
            TalkPos = new Vector2(0f, -170f)
        },
            Profile: BloodBatCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<BloodBatCreatureVisuals>(
                    monster.Id.Entry)),
        ["SURPRISE_GIFT_BOX"] = new(
            Layout: new(new(0f, -98.8f), new(0.806f, 0.806f), -72.8f, -260f, 72.8f, 10.4f, new(0f, -106.6f), new(0f, -306.8f)),
            Profile: SurpriseGiftBoxCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<SurpriseGiftBoxCreatureVisuals>(
                    monster.Id.Entry)),
        ["LITTLE_WITCH_FRIEND"] = new(
            Layout: new(new(0f, -94f), new(0.43f, 0.43f), -126f, -202f, 126f, 8f, new(0f, -98f), new(0f, -242f)),
            Profile: LittleWitchFriendCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LittleWitchFriendCreatureVisuals>(
                    monster.Id.Entry)),
        ["LITTLE_RED_RIDING_HOODED_MERCENARY"] = new(
            Layout: new(new(0f, -138f), new(-0.71f, 0.71f), -101f, -356f, 101f, 8f, new(0f, -146f), new(13f, -349f))
        {
            TalkPos = new Vector2(18f, -310f)
        },
            Profile: LittleRedMercenaryCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LittleRedMercenaryCreatureVisuals>(
                    monster.Id.Entry)),
        ["WOLF_IN_HER_NIGHTMARES"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<WolfInHerNightmaresCreatureVisuals>(
                        monster.Id.Entry,
                        WolfInHerNightmaresCreatureVisuals.ScenePath),
            ScenePath: WolfInHerNightmaresCreatureVisuals.ScenePath),
        ["LANGUAGE_FLOOR_SCARLET_SCAR"] = new(
            Layout: new(new(0f, 42f), new(-0.50f, 0.50f), -158f, -388f, 158f, 12f, new(0f, -76f), new(0f, -330f))
        {
            TalkPos = new Vector2(0f, -248f),
            StateDisplayLiftY = 18f
        },
            Profile: LanguageFloorScarletScarCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LanguageFloorScarletScarCreatureVisuals>(
                    monster.Id.Entry)),
        ["LANGUAGE_FLOOR_LOST_EVERYTHING_WOLF"] = new(
            Layout: new(new(0f, 20f), new(0.62f, 0.62f), -235f, -400f, 235f, 12f, new(-20f, -86f), new(-70f, -300f))
        {
            TalkPos = new Vector2(-155f, -193f),
            StateDisplayLiftY = 12f
        },
            Profile: LanguageFloorLostEverythingWolfCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LanguageFloorLostEverythingWolfCreatureVisuals>(
                    monster.Id.Entry)),
        ["LANGUAGE_FLOOR_COBALT_SCAR"] = new(
            Layout: new(new(0f, 18f), new(0.62f, 0.62f), -235f, -400f, 235f, 12f, new(-20f, -86f), new(-70f, -300f))
        {
            TalkPos = new Vector2(-155f, -193f),
            StateDisplayLiftY = 12f
        },
            Profile: LanguageFloorCobaltScarCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LanguageFloorCobaltScarCreatureVisuals>(
                    monster.Id.Entry)),
        ["LANGUAGE_FLOOR_SMILING_FACE"] = new(
            Layout: new(new(0f, 48f), new(0.48f, 0.48f), -245f, -390f, 245f, 12f, new(0f, -110f), new(0f, -320f))
        {
            TalkPos = new Vector2(35f, -300f),
            StateDisplayLiftY = 12f
        },
            Profile: LanguageFloorSmilingFaceCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LanguageFloorSmilingFaceCreatureVisuals>(
                    monster.Id.Entry)),
        ["LANGUAGE_FLOOR_DIPSIA"] = new(
            Layout: new(new(0f, -128f), new(0.72f, 0.72f), -160f, -360f, 160f, 12f, new(0f, -150f), new(0f, -395f))
        {
            TalkPos = new Vector2(0f, -305f),
            StateDisplayLiftY = 18f
        },
            Profile: LanguageFloorDipsiaCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LanguageFloorDipsiaCreatureVisuals>(
                    monster.Id.Entry)),
        ["LANGUAGE_FLOOR_MIMICRY"] = new(
            Layout: new(new(0f, -80f), new(0.78f, 0.78f), -180f, -390f, 180f, 12f, new(0f, -130f), new(0f, -410f))
        {
            TalkPos = new Vector2(0f, -325f),
            StateDisplayLiftY = 18f
        },
            Profile: LanguageFloorMimicryCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LanguageFloorMimicryCreatureVisuals>(
                    monster.Id.Entry)),
        ["LANGUAGE_FLOOR_MELTING_CORPSE"] = new(
            Layout: new(new(0f, 5f), new(0.28f, 0.28f), -105f, -150f, 105f, 10f, new(0f, -60f), new(0f, -190f)),
            Profile: LanguageFloorMeltingCorpseCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LanguageFloorMeltingCorpseCreatureVisuals>(
                    monster.Id.Entry)),
        ["HAPPY_TEDDY_MONSTER"] = new(
            Layout: new(new(6f, -120f), new(0.58f, 0.58f), -145f, -320f, 145f, 10f, new(6f, -120f), new(-4f, -340f))
        {
            TalkPos = new Vector2(-8f, -272f)
        },
            Profile: HappyTeddyCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HappyTeddyCreatureVisuals>(
                    monster.Id.Entry)),
        ["FAIRY_QUEEN"] = new(
            Layout: new(new(0f, -114f), new(0.58f, 0.58f), -152f, -284f, 152f, 8f, new(0f, -118f), new(0f, -318f))
        {
            TalkPos = new Vector2(0f, -258f)
        },
            Profile: FairyQueenCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<FairyQueenCreatureVisuals>(
                    monster.Id.Entry)),
        ["FAIRY_MASS"] = new(
            Layout: new(new(0f, -108f), new(0.46f, 0.46f), -116f, -250f, 116f, 8f, new(0f, -108f), new(0f, -284f))
        {
            TalkPos = new Vector2(0f, -220f)
        },
            Profile: FairyMassCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<FairyMassCreatureVisuals>(
                    monster.Id.Entry)),
        ["RED_SHOES_LEFT"] = new(
            Layout: new(new(0f, -98f), new(0.52f, 0.52f), -150f, -255f, 150f, 35f, new(0f, -98f), new(0f, -310f))
        {
            TalkPos = new Vector2(0f, -230f)
        },
            Profile: RedShoesLeftCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<RedShoesLeftCreatureVisuals>(
                    monster.Id.Entry)),
        ["RED_SHOES_RIGHT"] = new(
            Layout: new(new(0f, -98f), new(0.52f, 0.52f), -150f, -255f, 150f, 35f, new(0f, -98f), new(0f, -310f))
        {
            TalkPos = new Vector2(0f, -230f)
        },
            Profile: RedShoesRightCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<RedShoesRightCreatureVisuals>(
                    monster.Id.Entry)),
        ["ALL_AROUND_HELPER"] = new(
            Layout: new(new(0f, -108f), new(0.58f, 0.58f), -108f, -244f, 108f, 12f, new(0f, -108f), new(0f, -292f)),
            Profile: AllAroundHelperCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<AllAroundHelperCreatureVisuals>(
                    monster.Id.Entry)),
        ["TODAYS_SHY_LOOK"] = new(
            Layout: new(new(0f, -142f), new(0.58f, 0.58f), -118f, -330f, 118f, 12f, new(0f, -142f), new(0f, -366f))
        {
            TalkPos = new Vector2(0f, -286f)
        },
            Profile: TodaysShyLookCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TodaysShyLookCreatureVisuals>(
                    monster.Id.Entry)),
        ["FORSAKEN_MURDERER"] = new(
            Layout: new(new(0f, -100f), new(0.31f, 0.31f), -160f, -250f, 160f, 20f, new(0f, -100f), new(0f, -280f)),
            Profile: ForsakenMurdererCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ForsakenMurdererCreatureVisuals>(
                    monster.Id.Entry)),
        ["THE_FOURTH_MATCH_FLAME"] = new(
            Layout: new(new(0f, -54f), new(0.38f, 0.38f), -96f, -152f, 96f, 8f, new(0f, -56f), new(0f, -182f)),
            Profile: TheFourthMatchFlameCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TheFourthMatchFlameCreatureVisuals>(
                    monster.Id.Entry)),
        ["TOMERRY"] = new(
            Layout: new(new(0f, -118f), new(0.46f, 0.46f), -140f, -280f, 140f, 8f, new(0f, -120f), new(0f, -315f))
        {
            TalkPos = new Vector2(-6f, -250f)
        },
            Profile: TomerryCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TomerryCreatureVisuals>(
                    monster.Id.Entry)),
        ["FINN"] = new(
            Layout: new(new(0f, -145f), new(0.42f, 0.42f), -105f, -299.7f, 105f, 5f, new(0f, -139.8f), new(0f, -333.7f)),
            Profile: null,
            StaticDefaultIdleTexturePath: "res://images/monsters/finn.png",
            Factory: static monster => WrappedMonsterVisualFactory.CreateStaticSpriteVisuals(
                monster.Id.Entry,
                GetLayout(monster.Id.Entry),
                GetStaticDefaultIdleTexturePath(monster.Id.Entry))),
        ["SALVADOR"] = new(
            Layout: new(new(-10f, -145.2f), new(0.50f, 0.50f), -122f, -299.7f, 122f, 5f, new(-10f, -139.8f), new(-10f, -333.7f))
        {
            TalkPos = new Vector2(-18f, -270f)
        },
            Profile: SalvadorCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<SalvadorCreatureVisuals>(
                    monster.Id.Entry)),
        ["YUNA"] = new(
            Layout: new(new(0f, -145.2f), new(0.48f, 0.48f), -118f, -299.7f, 118f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(-2f, -266f)
        },
            Profile: YunaCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<YunaCreatureVisuals>(
                    monster.Id.Entry)),
        ["OSCAR"] = new(
            Layout: new(new(-6f, -145.2f), new(0.48f, 0.48f), -122f, -299.7f, 122f, 5f, new(-6f, -139.8f), new(-6f, -333.7f))
        {
            TalkPos = new Vector2(-8f, -262f)
        },
            Profile: OscarCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<OscarCreatureVisuals>(
                    monster.Id.Entry)),
        ["PAMELI"] = new(
            Layout: new(new(-8f, -145.2f), new(0.47f, 0.47f), -118f, -299.7f, 118f, 5f, new(-8f, -139.8f), new(-8f, -333.7f))
        {
            TalkPos = new Vector2(-10f, -260f)
        },
            Profile: PameliCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<PameliCreatureVisuals>(
                    monster.Id.Entry)),
        ["PAMELA"] = new(
            Layout: new(new(4f, -145.2f), new(0.47f, 0.47f), -118f, -299.7f, 118f, 5f, new(4f, -139.8f), new(4f, -333.7f))
        {
            TalkPos = new Vector2(6f, -260f)
        },
            Profile: PamelaCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<PamelaCreatureVisuals>(
                    monster.Id.Entry)),
        ["YUN"] = new(
            Layout: new(new(0f, -145.2f), new(0.52f, 0.52f), -112f, -299.7f, 112f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -260f)
        },
            Profile: YunCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<YunCreatureVisuals>(
                    monster.Id.Entry)),
        ["ERI"] = new(
            Layout: new(new(0f, -145.2f), new(0.42f, 0.42f), -122f, -299.7f, 122f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -260f)
        },
            Profile: EriCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<EriCreatureVisuals>(
                    monster.Id.Entry)),
        ["TAEIN"] = new(
            Layout: new(new(0f, -145.2f), new(0.48f, 0.48f), -124f, -299.7f, 124f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -262f)
        },
            Profile: TaeinCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TaeinCreatureVisuals>(
                    monster.Id.Entry)),
        ["MCCULLIN"] = new(
            Layout: new(new(0f, -145.2f), new(0.49f, 0.49f), -108f, -299.7f, 108f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -262f)
        },
            Profile: MccullinCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<MccullinCreatureVisuals>(
                    monster.Id.Entry)),
        ["NAOKI"] = new(
            Layout: new(new(0f, -145.2f), new(0.50f, 0.50f), -104f, -299.7f, 104f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -262f)
        },
            Profile: NaokiCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<NaokiCreatureVisuals>(
                    monster.Id.Entry)),
        ["MO"] = new(
            Layout: new(new(0f, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -264f)
        },
            Profile: MoCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<MoCreatureVisuals>(
                    monster.Id.Entry)),
        ["CONSTA"] = new(
            Layout: new(new(0f, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -264f)
        },
            Profile: ConstaCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ConstaCreatureVisuals>(
                    monster.Id.Entry)),
        ["ARNOLD"] = new(
            Layout: new(new(0f, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(0f, -139.8f), new(0f, -333.7f))
        {
            TalkPos = new Vector2(0f, -264f)
        },
            Profile: ArnoldCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArnoldCreatureVisuals>(
                    monster.Id.Entry)),
        ["PHILIP"] = new(
            Layout: new(new(6f, -145.2f), new(0.45f, 0.45f), -116f, -299.7f, 116f, 5f, new(6f, -139.8f), new(6f, -333.7f))
        {
            TalkPos = new Vector2(8f, -258f)
        },
            Profile: PhilipCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<PhilipCreatureVisuals>(
                    monster.Id.Entry)),
        ["SPIDER_BUD"] = new(
            Layout: new(new(0f, -152f), new(0.72f, 0.72f), -140f, -305f, 140f, 8f, new(0f, -145f), new(0f, -330f)),
            Profile: SpiderBudCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<SpiderBudCreatureVisuals>(
                    monster.Id.Entry)),
        ["SPIDER_BUD_SMALL_SPIDER"] = new(
            Layout: new(new(0f, -50f), new(0.6f, 0.6f), -60f, -190f, 60f, 5f, new(0f, -130f), new(0f, -220f)),
            Profile: SpiderBudSmallSpiderCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<SpiderBudSmallSpiderCreatureVisuals>(
                    monster.Id.Entry)),
        ["DEAD_BUTTERFLY"] = new(
            Layout: new(new(0f, -88f), new(0.36f, 0.36f), -78f, -182f, 78f, 8f, new(0f, -88f), new(0f, -218f)),
            Profile: DeadButterflyCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<DeadButterflyCreatureVisuals>(
                    monster.Id.Entry)),
        ["FUNERAL_OF_THE_DEAD_BUTTERFLIES"] = new(
            Layout: new(new(0f, -126f), new(0.58f, 0.58f), -165f, -315f, 165f, 12f, new(0f, -126f), new(0f, -350f))
        {
            TalkPos = new Vector2(0f, -278f)
        },
            Profile: FuneralOfTheDeadButterfliesCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<FuneralOfTheDeadButterfliesCreatureVisuals>(
                    monster.Id.Entry)),
        ["TECHNOLOGY_FLOOR_REGRET_BOSS"] = new(
            Layout: new(new(0f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(-20f, -340f))
        {
            TalkPos = new Vector2(-20f, -280f)
        },
            Profile: TechnologyFloorRegretBossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TechnologyFloorRegretBossCreatureVisuals>(
                    monster.Id.Entry)),
        ["TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS"] = new(
            Layout: new(new(0f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(-20f, -340f))
        {
            TalkPos = new Vector2(-20f, -280f)
        },
            Profile: TechnologyFloorGrinderMk4BossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TechnologyFloorGrinderMk4BossCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_PHASE_BOSS"] = new(
            Layout: new(new(0f, -118f), new(0.58f, 0.58f), -155f, -310f, 155f, 10f, new(0f, -120f), new(0f, -340f))
        {
            TalkPos = new Vector2(0f, -260f)
        },
            Profile: HistoryFloorPhaseBossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
            {
                string id = monster.Id.Entry;
                int phase = monster is HistoryFloorPhaseBoss boss ? boss.Phase : 1;
                return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorPhaseBossCreatureVisuals>(
                    id,
                    HistoryFloorPhaseBoss.IdleTexturePathForPhase(phase));

            }),
        ["HISTORY_FLOOR_END_LIGHT_BOSS"] = new(
            Layout: new(new(0f, -98f), new(0.58f, 0.58f), -155f, -290f, 155f, 30f, new(0f, -100f), new(0f, -320f))
        {
            TalkPos = new Vector2(0f, -240f)
        },
            Profile: HistoryFloorEndLightCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorEndLightCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_FORGOTTEN_BOSS"] = new(
            Layout: new(new(0f, 45f), new(0.50f, 0.50f), -190f, -235f, 190f, 5f, new(0f, -88f), new(0f, -300f))
        {
            TalkPos = new Vector2(0f, -220f)
        },
            Profile: HistoryFloorForgottenCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorForgottenCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_FLUTTERING_BOSS"] = new(
            Layout: new(new(0f, -122f), new(0.4756f, 0.4756f), -145f, -260f, 145f, 30f, new(0f, -86f), new(0f, -292f))
        {
            TalkPos = new Vector2(0f, -215f)
        },
            Profile: HistoryFloorFlutteringBossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorFlutteringBossCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_FLUTTERING_MASS"] = new(
            Layout: new(new(0f, -92f), new(0.3116f, 0.3116f), -94f, -205f, 94f, 8f, new(0f, -96f), new(0f, -250f))
        {
            TalkPos = new Vector2(0f, -198f)
        },
            Profile: HistoryFloorFlutteringMassCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorFlutteringMassCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_WASP_BOSS"] = new(
            Layout: new(new(0f, -128f), new(0.46f, 0.46f), -170f, -315f, 170f, 24f, new(0f, -126f), new(0f, -342f))
        {
            TalkPos = new Vector2(0f, -268f)
        },
            Profile: HistoryFloorWaspBossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorWaspBossCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_WORKER_BEE"] = new(
            Layout: new(new(0f, -76f), new(0.44f, 0.44f), -82f, -184f, 82f, 8f, new(0f, -82f), new(0f, -224f))
        {
            TalkPos = new Vector2(0f, -178f)
        },
            Profile: HistoryFloorWorkerBeeCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorWorkerBeeCreatureVisuals>(
                    monster.Id.Entry)),
        ["QUEEN_BEE"] = new(
            Layout: new(new(0f, -200f), new(0.46f, 0.46f), -170f, -315f, 170f, 24f, new(0f, -126f), new(0f, -412f))
        {
            TalkPos = new Vector2(0f, -268f)
        },
            Profile: QueenBeeCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<QueenBeeCreatureVisuals>(
                    monster.Id.Entry)),
        ["QUEEN_BEE_WORKER"] = new(
            Layout: new(new(0f, -76f), new(0.54f, 0.54f), -82f, -184f, 82f, 8f, new(0f, -82f), new(0f, -224f))
        {
            TalkPos = new Vector2(0f, -178f)
        },
            Profile: QueenBeeWorkerCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<QueenBeeWorkerCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_LAST_MATCH"] = new(
            Layout: new(new(0f, 0f), new(0.38f, 0.38f), -78f, -120f, 78f, 8f, new(0f, -56f), new(0f, -150f)),
            Profile: HistoryFloorLastMatchCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorLastMatchCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_EMERALD_BOUGH_BOSS"] = new(
            Layout: new(new(0f, -128f), new(0.50f, 0.50f), -170f, -315f, 170f, 24f, new(0f, -126f), new(0f, -342f))
        {
            TalkPos = new Vector2(0f, -268f)
        },
            Profile: HistoryFloorEmeraldBoughCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorEmeraldBoughCreatureVisuals>(
                    monster.Id.Entry)),
        ["HISTORY_FLOOR_VINE_BARRIER"] = new(
            Layout: new(new(0f, -88f), new(0.40f, 0.40f), -94f, -198f, 94f, 8f, new(0f, -92f), new(0f, -236f))
        {
            TalkPos = new Vector2(0f, -188f)
        },
            Profile: HistoryFloorVineBarrierCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorVineBarrierCreatureVisuals>(
                    monster.Id.Entry)),
        ["TECHNOLOGY_FLOOR_CHORD_BOSS"] = new(
            Layout: new(new(0f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(-20f, -340f))
        {
            TalkPos = new Vector2(-20f, -280f)
        },
            Profile: TechnologyFloorChordBossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TechnologyFloorChordBossCreatureVisuals>(
                    monster.Id.Entry)),
        ["TECHNOLOGY_FLOOR_CHORD_STAFF"] = new(
            Layout: new(new(0f, -122f), new(0.58f, 0.58f), -118f, -330f, 118f, 12f, new(0f, -122f), new(20f, -290f))
        {
            TalkPos = new Vector2(0f, -286f)
        },
            Profile: AddictedEmployeeCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<AddictedEmployeeCreatureVisuals>(
                    monster.Id.Entry)),
        ["TECHNOLOGY_FLOOR_SOLEMN_MOURNING_BOSS"] = new(
            Layout: new(new(0f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(-20f, -340f))
        {
            TalkPos = new Vector2(-20f, -280f)
        },
            Profile: TechnologyFloorSolemnMourningBossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TechnologyFloorSolemnMourningBossCreatureVisuals>(
                    monster.Id.Entry)),
        ["TECHNOLOGY_FLOOR_MK4_HELPER"] = new(
            Layout: new(new(0f, -85f), new(0.50f, 0.50f), -80f, -190f, 80f, 8f, new(0f, -85f), new(0f, -225f))
        {
            TalkPos = new Vector2(0f, -175f)
        },
            Profile: TechnologyFloorMk4HelperCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TechnologyFloorMk4HelperCreatureVisuals>(
                    monster.Id.Entry)),
        ["TECHNOLOGY_FLOOR_MAGIC_BULLET_BOSS"] = new(
            Layout: new(new(-95f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(0f, -340f))
        {
            TalkPos = new Vector2(0f, -280f)
        },
            Profile: TechnologyFloorMagicBulletBossCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TechnologyFloorMagicBulletBossCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_DA_CAPO_BOSS"] = new(
            Layout: new(new(0f, -188f), new(0.58f, 0.58f), -215f, -472f, 215f, 36f, new(0f, -205f), new(0f, -452f))
        {
            TalkPos = new Vector2(0f, -392f)
        },
            Profile: ArtFloorDaCapoCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorDaCapoCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_FIRST_PERFORMER"] = new(
            Layout: new(new(0f, -188f), new(0.64f, 0.64f), -88f, -345f, 88f, -10f, new(0f, -185f), new(0f, -382f))
        {
            TalkPos = new Vector2(0f, -300f)
        },
            Profile: ArtFloorFirstPerformerCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorFirstPerformerCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_BEYOND_FRAGMENT_BOSS"] = new(
            Layout: new(new(-108f, -205f), new(0.58f, 0.58f), -400f, -435f, 190f, 14f, new(0f, -210f), new(0f, -438f))
        {
            TalkPos = new Vector2(0f, -352f)
        },
            Profile: BeyondFragmentCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<BeyondFragmentCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_LITTLE_GALAXY_BOSS"] = new(
            Layout: new(new(0f, -28f), new(0.52f, 0.52f), -170f, -330f, 170f, 38f, new(0f, -88f), new(0f, -420f))
        {
            TalkPos = new Vector2(0f, -240f)
        },
        // 只想调血条/power 栏上下位置时，改 StateDisplayLiftY；不要改 SpritePos 或 BoundsTop/Bottom。,
            Profile: ArtFloorLittleGalaxyCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorLittleGalaxyCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_PLEASURE_BOSS"] = new(
            Layout: new(new(0f, -24f), new(0.58f, 0.58f), -132f, -290f, 132f, 40f, new(0f, -132f), new(0f, -328f))
        {
            TalkPos = new Vector2(0f, -236f),
            StateDisplayLiftY = 10f
        },
            Profile: ArtFloorPleasureCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorPleasureCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_NOSTALGIC_SCENT_BOSS"] = new(
            Layout: new(new(0f, -30f), new(0.58f, 0.58f), -148f, -340f, 148f, 12f, new(0f, -154f), new(0f, -370f))
        {
            TalkPos = new Vector2(0f, -292f),
            StateDisplayLiftY = 30f
        },
            Profile: ArtFloorNostalgicScentCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorNostalgicScentCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_DUSTBORN_PERSON"] = new(
            Layout: new(new(0f, -80f), new(0.50f, 0.50f), -116f, -260f, 116f, 10f, new(0f, -112f), new(0f, -375f))
        {
            TalkPos = new Vector2(0f, -220f),
            StateDisplayLiftY = 70f
        },
            Profile: ArtFloorDustbornPersonCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorDustbornPersonCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_GALAXY_FRIEND"] = new(
            Layout: new(new(0f, -110f), new(0.55f, 0.55f), -122f, -278f, 122f, 8f, new(0f, -112f), new(0f, -295f))
        {
            TalkPos = new Vector2(0f, -242f)
        },
            Profile: GalaxyFriendCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<GalaxyFriendCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_FINAL_DA_CAPO_BOSS"] = new(
            Layout: new(new(0f, -188f), new(0.58f, 0.58f), -215f, -472f, 215f, 36f, new(0f, -205f), new(0f, -452f))
        {
            TalkPos = new Vector2(0f, -392f),
            StateDisplayLiftY = 35f
        },
            Profile: ArtFloorDaCapoCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorDaCapoCreatureVisuals>(
                    monster.Id.Entry)),
        ["ART_FLOOR_DA_CAPO_PERFORMER"] = new(
            Layout: new(new(0f, -48f), new(0.48f, 0.48f), -88f, -260f, 88f, 10f, new(0f, -136f), new(0f, -298f))
        {
            TalkPos = new Vector2(0f, -228f),
            StateDisplayLiftY = 30f
        },
            Profile: ArtFloorDaCapoPerformerCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
            {
                string id = monster.Id.Entry;
                string idleTexturePath = monster is ArtFloorDaCapoPerformer performer
                    ? performer.IdleTexturePath
                    : "res://images/monsters/art_floor/dacapo_performers/performer_1_idle.png";
                return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ArtFloorDaCapoPerformerCreatureVisuals>(
                    id,
                    idleTexturePath);

            }),
        ["COSMIC_FRAGMENT"] = new(
            Layout: new(new(0f, -120f), new(0.55f, 0.55f), -130f, -300f, 130f, 10f, new(0f, -120f), new(0f, -340f)),
            Profile: CosmicFragmentCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<CosmicFragmentCreatureVisuals>(
                    monster.Id.Entry)),
        ["SPINY_BUS"] = new(
            Layout: new(new(0f, -126f), new(0.58f, 0.58f), -132f, -320f, 132f, 10f, new(0f, -126f), new(0f, -348f))
        {
            TalkPos = new Vector2(0f, -266f)
        },
            Profile: SpinyBusCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<SpinyBusCreatureVisuals>(
                    monster.Id.Entry)),
        ["GALAXY_FRIEND"] = new(
            Layout: new(new(0f, -110f), new(0.55f, 0.55f), -122f, -278f, 122f, 8f, new(0f, -112f), new(0f, -318f))
        {
            TalkPos = new Vector2(0f, -242f)
        },
            Profile: GalaxyFriendCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<GalaxyFriendCreatureVisuals>(
                    monster.Id.Entry)),
        ["BIG_BAD_WOLF"] = new(
            Layout: new(new(0f, -178f), new(0.72f, 0.72f), -150f, -370f, 150f, 10f, new(0f, -178f), new(0f, -405f))
        {
            TalkPos = new Vector2(0f, -315f),
            StolenCardPos = new Vector2(0f, -218f),
            StolenCardScale = new Vector2(0.48f, 0.48f)
        },
            Profile: BigBadWolfCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<BigBadWolfCreatureVisuals>(
                    monster.Id.Entry)),
        ["SMILING_BODIES"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<SmilingBodiesCreatureVisuals>(
                        monster.Id.Entry,
                        SmilingBodiesCreatureVisuals.ScenePath),
            ScenePath: SmilingBodiesCreatureVisuals.ScenePath),
        ["REVERBERATION_EILEEN"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<LibraryOfRuina.reverberation.GearChurch.ReverberationEileenVisuals>(
                    monster.Id.Entry, LibraryOfRuina.reverberation.GearChurch.GearChurchAssets.EileenScene),
            ScenePath: LibraryOfRuina.reverberation.GearChurch.GearChurchAssets.EileenScene),
        ["GEAR_CHURCH_FOLLOWER"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<LibraryOfRuina.reverberation.GearChurch.GearChurchFollowerVisuals>(
                    monster.Id.Entry, LibraryOfRuina.reverberation.GearChurch.GearChurchAssets.FollowerScene),
            ScenePath: LibraryOfRuina.reverberation.GearChurch.GearChurchAssets.FollowerScene),
        ["REVERBERATION_PHILIP"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<LibraryOfRuina.reverberation.CryingChildren.ReverberationPhilipVisuals>(
                    monster.Id.Entry, LibraryOfRuina.reverberation.CryingChildren.CryingChildrenAssets.PhilipScene),
            ScenePath: LibraryOfRuina.reverberation.CryingChildren.CryingChildrenAssets.PhilipScene),
        ["UNSPEAKING_CHILD"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<LibraryOfRuina.reverberation.CryingChildren.UnspeakingChildVisuals>(
                    monster.Id.Entry, LibraryOfRuina.reverberation.CryingChildren.CryingChildrenAssets.ChildScene),
            ScenePath: LibraryOfRuina.reverberation.CryingChildren.CryingChildrenAssets.ChildScene),
        ["MELTING_CORPSE"] = new(
            Layout: new(new(0f, -1f), new(0.28f, 0.28f), -145f, -124f, 145f, 36f, new(0f, -14f), new(0f, -151f))
        {
            TalkPos = new Vector2(0f, -81f)
        },
            Profile: MeltingCorpseCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<MeltingCorpseCreatureVisuals>(
                    monster.Id.Entry)),
        ["DESPAIR_KNIGHT"] = new(
            Layout: new(new(0f, 6f), new(0.77f, 0.77f), -181.5f, -336.7f, 181.5f, 12f, new(0f, -160.1f), new(0f, -369.7f))
        {
            TalkPos = new Vector2(0f, -286.6f)
        },
            Profile: DespairKnightCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<DespairKnightCreatureVisuals>(
                    monster.Id.Entry)),
        ["FORGOTTEN_KNIGHT_SWORD"] = new(
            Layout: new(new(0f, 6f), new(0.54f, 0.54f), -120f, -265f, 120f, 14f, new(0f, -120f), new(0f, -292f))
        {
            TalkPos = new Vector2(0f, -230f)
        },
            Profile: ForgottenKnightSwordCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ForgottenKnightSwordCreatureVisuals>(
                    monster.Id.Entry)),
        ["WRATH_SERVANT"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.WrathServant.WrathServantCreatureVisuals>(monster.Id.Entry, LibraryOfRuina.visuals.WrathServant.WrathServantCreatureVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.WrathServant.WrathServantCreatureVisuals.ScenePath),
        ["SCARECROW_SEARCHING_FOR_WISDOM"] = new(
            Layout: new(new(0f, -28f), new(0.50f, 0.50f), -120f, -330f, 120f, 8f, new(0f, -130f), new(0f, -390f))
        {
            TalkPos = new Vector2(0f, -280f),
            StateDisplayLiftY = 20f
        },
            Profile: ScarecrowSearchingForWisdomCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ScarecrowSearchingForWisdomCreatureVisuals>(
                    monster.Id.Entry)),
        ["BURROWING_HEAVEN"] = new(
            Layout: new(new(0f, -18f), new(0.58f, 0.58f), -165f, -390f, 165f, 12f, new(0f, -170f), new(0f, -430f))
        {
            TalkPos = new Vector2(0f, -330f),
            StateDisplayLiftY = 34f
        },
            Profile: BurrowingHeavenCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
            {
                string id = monster.Id.Entry;
                return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<BurrowingHeavenCreatureVisuals>(
                    id,
                    monster is BurrowingHeaven { IsAwake: false }
                        ? BurrowingHeaven.SleepTexturePath
                        : BurrowingHeaven.AwakeTexturePath,
                    visuals => visuals.StartsAwake = monster is not BurrowingHeaven { IsAwake: false });

            }),
        ["HEAVEN_THORN"] = new(
            Layout: new(new(0f, -8f), new(0.48f, 0.48f), -98f, -275f, 98f, 12f, new(0f, -118f), new(0f, -315f))
        {
            StateDisplayLiftY = 14f
        },
            Profile: HeavenThornCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
            {
                string id = monster.Id.Entry;
                return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HeavenThornCreatureVisuals>(
                    id,
                    monster is HeavenThorn { IsAwake: false }
                        ? HeavenThorn.SleepTexturePath
                        : HeavenThorn.AwakeTexturePath,
                    visuals => visuals.StartsAwake = monster is not HeavenThorn { IsAwake: false });

            }),
        ["LUNG_OF_ASPIRATION"] = new(
            Layout: new(new(0f, -6f), new(-0.984f, 0.984f), -170f, -350f, 170f, 14f, new(0f, -150f), new(0f, -365f))
        {
            TalkPos = new Vector2(0f, -290f),
            StateDisplayLiftY = 28f
        },
            Profile: LungOfAspirationCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<LungOfAspirationCreatureVisuals>(
                    monster.Id.Entry)),
        ["HEART_OF_ASPIRATION"] = new(
            Layout: new(new(0f, -8f), new(0.984f, 0.984f), -190f, -390f, 190f, 14f, new(0f, -165f), new(0f, -355f))
        {
            TalkPos = new Vector2(0f, -320f),
            StateDisplayLiftY = 32f
        },
            Profile: HeartOfAspirationCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HeartOfAspirationCreatureVisuals>(
                    monster.Id.Entry)),
        ["WARMHEARTED_WOODSMAN"] = new(
            Layout: new(new(0f, -8f), new(0.87f, 0.87f), -190f, -430f, 190f, 16f, new(0f, -190f), new(0f, -455f))
        {
            TalkPos = new Vector2(0f, -360f),
            StateDisplayLiftY = 40f
        },
            Profile: WarmheartedWoodsmanCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
            {
                string id = monster.Id.Entry;
                return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<WarmheartedWoodsmanCreatureVisuals>(
                    id,
                    monster is monsters.WarmheartedWoodsman.WarmheartedWoodsman { HasWarmHeart: true }
                        ? monsters.WarmheartedWoodsman.WarmheartedWoodsman.WarmIdleTexturePath
                        : monsters.WarmheartedWoodsman.WarmheartedWoodsman.EmptyIdleTexturePath,
                    visuals => visuals.StartsWarm = monster is monsters.WarmheartedWoodsman.WarmheartedWoodsman { HasWarmHeart: true });

            }),
        ["WOODSMAN_TREE"] = new(
            Layout: new(new(0f, -10f), new(0.36f, 0.36f), -190f, -420f, 190f, 12f, new(0f, -200f), new(0f, -455f))
        {
            StateDisplayLiftY = 18f
        },
            Profile: WoodsmanTreeCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<WoodsmanTreeCreatureVisuals>(
                    monster.Id.Entry)),
        ["PRICE_OF_SILENCE"] = new(
            Layout: new(new(0f, -10f), new(0.56f, 0.56f), -168f, -360f, 168f, 16f, new(0f, -166f), new(70f, -470f))
        {
            TalkPos = new Vector2(0f, -300f),
            StateDisplayLiftY = 26f
        },
            Profile: PriceOfSilenceCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<PriceOfSilenceCreatureVisuals>(
                    monster.Id.Entry)),
        ["TIME_TRACE"] = new(
            Layout: new(new(10f, 20f), new(1.24f, 1.24f), -158f, -340f, 158f, 12f, new(0f, -174f), new(-95f, -360f))
        {
            StateDisplayLiftY = 5f
        },
            Profile: TimeTraceCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TimeTraceCreatureVisuals>(
                    monster.Id.Entry)),
        ["BIG_BIRD"] = new(
            Layout: new(new(0f, -12f), new(0.60f, 0.60f), -190f, -420f, 190f, 16f, new(0f, -204f), new(44f, -372f))
        {
            TalkPos = new Vector2(0f, -356f),
            StateDisplayLiftY = 36f
        },
            Profile: BigBirdCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<BigBirdCreatureVisuals>(
                    monster.Id.Entry)),
        ["BLUE_STAR_ALTAR"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<BlueStarAltarCreatureVisuals>(
                        monster.Id.Entry,
                        BlueStarAltarCreatureVisuals.ScenePath),
            ScenePath: BlueStarAltarCreatureVisuals.ScenePath),
        ["BLUE_STAR_FOLLOWER"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<BlueStarFollowerCreatureVisuals>(
                        monster.Id.Entry,
                        BlueStarFollowerCreatureVisuals.ScenePath),
            ScenePath: BlueStarFollowerCreatureVisuals.ScenePath),
        ["OZMA"] = new(
            Layout: new(new(0f, -8f), new(0.48f, 0.48f), -196f, -382f, 196f, 16f, new(0f, -184f), new(20f, -414f))
        {
            StateDisplayLiftY = 30f
        },
            Profile: OzmaCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<OzmaCreatureVisuals>(
                    monster.Id.Entry)),
        ["OZMA_JACK"] = new(
            Layout: new(new(0f, -8f), new(0.48f, 0.48f), -98f, -228f, 98f, 12f, new(0f, -108f), new(0f, -264f))
        {
            StateDisplayLiftY = 12f
        },
            Profile: OzmaJackCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
            {
                string id = monster.Id.Entry;
                return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<OzmaJackCreatureVisuals>(
                    id,
                    monster is OzmaJack { IsAwake: true }
                        ? OzmaJack.AwakeTexturePath
                        : OzmaJack.DormantTexturePath,
                    visuals => visuals.StartsAwake = monster is OzmaJack { IsAwake: true });

            }),
        ["EYEBALL_BIRD"] = new(
            Layout: new(new(0f, 13f), new(0.56f, 0.56f), -104f, -222f, 104f, 10f, new(0f, -96f), new(0f, -238f))
        {
            TalkPos = new Vector2(0f, -202f)
        },
            Profile: EyeballBirdCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<EyeballBirdCreatureVisuals>(
                    monster.Id.Entry)),
        ["PUNISHING_BIRD"] = new(
            Layout: new(new(0f, -12f), new(2.2f, 2.2f), -190f, -420f, 190f, 16f, new(0f, -190f), new(-90f, -435f))
        {
            StateDisplayLiftY = 32f
        },
            Profile: PunishingBirdCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<PunishingBirdCreatureVisuals>(
                    monster.Id.Entry)),
        ["FOREST_KEEPER_BIRD_LEFT"] = new(
            Layout: new(new(0f, -8f), new(0.44f, 0.44f), -150f, -320f, 150f, 12f, new(0f, -148f), new(0f, -350f))
        {
            StateDisplayLiftY = 20f
        },
            Profile: ForestKeeperBirdCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ForestKeeperBirdCreatureVisuals>(
                    monster.Id.Entry)),
        ["FOREST_KEEPER_BIRD_RIGHT"] = new(
            Layout: new(new(0f, -8f), new(0.44f, 0.44f), -150f, -320f, 150f, 12f, new(0f, -148f), new(0f, -350f))
        {
            StateDisplayLiftY = 20f
        },
            Profile: ForestKeeperBirdCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ForestKeeperBirdCreatureVisuals>(
                    monster.Id.Entry)),
        ["JUDGEMENT_BIRD"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<JudgementBirdCreatureVisuals>(
                        monster.Id.Entry,
                        JudgementBirdCreatureVisuals.ScenePath),
            ScenePath: JudgementBirdCreatureVisuals.ScenePath),
        ["ESCAPED_BIRD"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<EscapedBirdCreatureVisuals>(
                        monster.Id.Entry,
                        EscapedBirdCreatureVisuals.ScenePath),
            ScenePath: EscapedBirdCreatureVisuals.ScenePath),
        ["ROAD_HOME_HOUSE"] = new(
            Layout: new(new(0f, -18f), new(0.62f, 0.62f), -170f, -260f, 170f, 14f, new(0f, -124f), new(0f, -292f))
        {
            TalkPos = new Vector2(0f, -230f),
            StateDisplayLiftY = 18f
        },
            Profile: RoadHomeHouseCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<RoadHomeHouseCreatureVisuals>(
                    monster.Id.Entry)),
        ["ROAD_HOME"] = new(
            Layout: new(new(0f, -18f), new(0.60f, 0.60f), -155f, -360f, 155f, 14f, new(0f, -170f), new(0f, -390f))
        {
            TalkPos = new Vector2(0f, -316f),
            StateDisplayLiftY = 28f
        },
            Profile: RoadHomeCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<RoadHomeCreatureVisuals>(
                    monster.Id.Entry)),
        ["SCAREDY_CAT"] = new(
            Layout: new(new(0f, -12f), new(0.88f, 0.88f), -132f, -270f, 132f, 12f, new(0f, -126f), new(0f, -332f))
        {
            TalkPos = new Vector2(0f, -238f),
            StateDisplayLiftY = 14f
        },
            Profile: ScaredyCatCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ScaredyCatCreatureVisuals>(
                    monster.Id.Entry)),
        ["SCAREDY_CAT_COMPANION"] = new(
            Layout: new(new(-20f, 12f), new(-0.52f, 0.52f), -118f, -246f, 118f, 12f, new(0f, -112f), new(0f, -170f))
        {
            TalkPos = new Vector2(0f, -216f),
            StateDisplayLiftY = -8f
        },
            Profile: ScaredyCatCompanionCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ScaredyCatCompanionCreatureVisuals>(
                    monster.Id.Entry)),
        ["KALI"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                KaliCreatureVisuals.Create(monster.Id.Entry),
            ScenePath: KaliCreatureVisuals.ScenePath),
        ["GREEN_STEM_HERMIT"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.WrathServant.GreenStemHermitCreatureVisuals>(monster.Id.Entry, LibraryOfRuina.visuals.WrathServant.GreenStemHermitCreatureVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.WrathServant.GreenStemHermitCreatureVisuals.ScenePath),
        ["NATURAL_FLOOR_BLIND_RAGE_BOSS"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorBlindRageVisuals>(monster.Id.Entry, LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorBlindRageVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorBlindRageVisuals.ScenePath),
        ["NATURAL_FLOOR_GREEN_STEM_HERMIT"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHermitVisuals>(monster.Id.Entry, LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHermitVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHermitVisuals.ScenePath),
        ["NATURAL_FLOOR_HERMIT_STAFF"] = new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHermitStaffVisuals>(monster.Id.Entry, LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHermitStaffVisuals.ScenePath),
            ScenePath: LibraryOfRuina.visuals.NaturalFloorLiberation.NaturalFloorHermitStaffVisuals.ScenePath),
        ["HERMIT_STAFF"] = new(
            Layout: new(new(0f, -100f), new(0.46f, 0.46f), -90f, -220f, 90f, 8f, new(0f, -100f), new(0f, -250f)),
            Profile: HermitStaffCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HermitStaffCreatureVisuals>(
                    monster.Id.Entry)),
        ["PHILOSOPHY_FLOOR_TWILIGHT"] = new(
            Layout: new(new(0f, -216f), new(0.45f, 0.45f), -315f, -624f, 315f, 12f, new(0f, -202f), new(36f, -508f))
        {
            TalkPos = new Vector2(0f, -540f),
            StateDisplayLiftY = 52f
        },
            Profile: PhilosophyFloorTwilightCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<PhilosophyFloorTwilightCreatureVisuals>(
                    monster.Id.Entry)),
        ["FALSE_THRONE"] = new(
            Layout: new(new(0f, -8f), new(0.504f, 0.504f), -190f, -620f, 190f, 10f, new(0f, -285f), new(20f, -600f))
        {
            TalkPos = new Vector2(0f, -500f),
            StateDisplayLiftY = 20f
        },
            Profile: FalseThroneCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<FalseThroneCreatureVisuals>(
                    monster.Id.Entry)),
        ["EMERALD_CRYSTAL"] = new(
            Layout: new(new(0f, -8f), new(0.38f, 0.38f), -95f, -210f, 95f, 8f, new(0f, -98f), new(0f, -240f))
        {
            StateDisplayLiftY = 10f
        },
            Profile: EmeraldCrystalCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<EmeraldCrystalCreatureVisuals>(
                    monster.Id.Entry)),
        ["SCOWLING_FACE"] = new(
            Layout: new(new(-78f, -6f), new(0.30f, 0.30f), -170f, -180f, 14f, 8f, new(0f, -85f), new(-78f, -215f))
        {
            StateDisplayLiftY = 8f
        },
            Profile: ScowlingFaceCreatureVisuals.Profile,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<ScowlingFaceCreatureVisuals>(
                    monster.Id.Entry)),
    };

    private static MonsterVisualCatalogEntry
        CreateBlackSwanBrotherRegistration() => new(
            Layout: null,
            Profile: null,
            StaticDefaultIdleTexturePath: null,
            Factory: static monster =>
                WrappedMonsterVisualFactory
                    .CreateSceneBackedVisuals<
                        LiteratureFloorBlackSwanBrotherCreatureVisuals>(
                        monster.Id.Entry,
                        LiteratureFloorBlackSwanBrotherCreatureVisuals
                            .ScenePath),
            ScenePath: LiteratureFloorBlackSwanBrotherCreatureVisuals
                .ScenePath);

    static MonsterVisualCatalog()
    {
        Validate();
    }

    internal static IEnumerable<string> RegisteredIds => Entries.Keys;

    internal static bool Contains(string monsterIdEntry) =>
        Entries.ContainsKey(monsterIdEntry)
        || SpecialScenePaths.ContainsKey(monsterIdEntry);

    internal static CreatureVisualLayout GetLayout(string monsterIdEntry)
    {
        if (Entries.TryGetValue(
                monsterIdEntry,
                out MonsterVisualCatalogEntry? entry))
        {
            if (entry.Layout is { } layout)
            {
                return layout;
            }

            throw new InvalidOperationException(
                $"Monster visual '{monsterIdEntry}' owns its layout in a scene.");
        }

        throw new InvalidOperationException(
            $"No monster visual layout is registered for '{monsterIdEntry}'.");
    }

    internal static SpriteVisualProfile GetRequiredProfile(
        string monsterIdEntry)
    {
        if (Entries.TryGetValue(
                monsterIdEntry,
                out MonsterVisualCatalogEntry? entry)
            && entry.Profile != null)
        {
            return entry.Profile;
        }

        throw new InvalidOperationException(
            $"No sprite visual profile is registered for '{monsterIdEntry}'.");
    }

    internal static string GetStaticDefaultIdleTexturePath(
        string monsterIdEntry)
    {
        if (Entries.TryGetValue(
                monsterIdEntry,
                out MonsterVisualCatalogEntry? entry)
            && !string.IsNullOrWhiteSpace(
                entry.StaticDefaultIdleTexturePath))
        {
            return entry.StaticDefaultIdleTexturePath;
        }

        throw new InvalidOperationException(
            $"No static idle texture is registered for '{monsterIdEntry}'.");
    }

    internal static NCreatureVisuals Create(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        if (SpecialScenePaths.TryGetValue(id, out string? scenePath))
        {
            MonsterVisualDebug.Trace(
                $"Create id={id} scenePath={scenePath}");
            return WrappedMonsterVisualFactory.CreateFromScene(scenePath);
        }

        if (Entries.TryGetValue(id, out MonsterVisualCatalogEntry? entry))
        {
            ValidateModelAssetPaths(monster, entry);
            return entry.Factory(monster);
        }

        throw new InvalidOperationException(
            $"No monster visual catalog entry is registered for '{id}'.");
    }

    private static void ValidateModelAssetPaths(
        MonsterModel monster,
        MonsterVisualCatalogEntry entry)
    {
        string id = monster.Id.Entry;
        lock (ModelAssetPathValidationLock)
        {
            if (ValidatedModelAssetPathIds.Contains(id))
            {
                return;
            }

            var declaredPaths = new HashSet<string>(
                monster.AssetPaths,
                StringComparer.Ordinal);
            IEnumerable<string> requiredPaths = entry.Profile?.AssetPaths
                ?? (!string.IsNullOrWhiteSpace(entry.StaticDefaultIdleTexturePath)
                    ? [entry.StaticDefaultIdleTexturePath!]
                    : !string.IsNullOrWhiteSpace(entry.ScenePath)
                        ? [entry.ScenePath!]
                        : throw new InvalidOperationException(
                            $"Monster visual '{id}' has no preloadable "
                            + "resource source."));
            string? missingPath = requiredPaths.FirstOrDefault(
                path => !declaredPaths.Contains(path));
            if (missingPath != null)
            {
                throw new InvalidOperationException(
                    $"Monster '{id}' AssetPaths does not declare sprite "
                    + $"resource '{missingPath}'.");
            }

            ValidatedModelAssetPathIds.Add(id);
        }
    }

    internal static void Validate()
    {
        var validatedResourcePaths = new HashSet<string>(
            StringComparer.Ordinal);

        EnsureSameIds(
            "layout baseline",
            BaselineLayoutIds,
            Entries.Keys);

        int expectedLayoutCount = BaselineLayoutIds.Count;
        if (Entries.Count != expectedLayoutCount
            || Entries.Keys.Distinct(StringComparer.Ordinal).Count()
                != expectedLayoutCount)
        {
            throw new InvalidOperationException(
                "Monster visual catalog must contain "
                + $"{expectedLayoutCount} unique layout IDs.");
        }

        foreach ((string id, MonsterVisualCatalogEntry entry) in Entries)
        {
            if (entry.Factory == null)
            {
                throw new InvalidOperationException(
                    $"Monster visual '{id}' has no node factory.");
            }

            int sourceCount = (entry.Profile != null ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(
                    entry.StaticDefaultIdleTexturePath) ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(entry.ScenePath) ? 1 : 0);
            if (sourceCount != 1)
            {
                throw new InvalidOperationException(
                    $"Monster visual '{id}' must declare exactly one source: "
                    + "Profile, static texture, or ScenePath.");
            }

            if (!string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                if (entry.Layout != null)
                {
                    throw new InvalidOperationException(
                        $"Scene-backed monster visual '{id}' must keep layout "
                        + "inside its scene instead of the catalog.");
                }

                ValidateResourcePath(
                    id,
                    entry.ScenePath!,
                    validatedResourcePaths);
                continue;
            }

            if (entry.Layout is not { } layout)
            {
                throw new InvalidOperationException(
                    $"Code-backed monster visual '{id}' has no layout.");
            }
            ValidateLayout(id, layout);

            if (entry.Profile != null)
            {
                entry.Profile.Validate();
                if (entry.Profile.AssetPaths.Count == 0
                    || string.IsNullOrWhiteSpace(
                        entry.Profile.DefaultIdleTexturePath))
                {
                    throw new InvalidOperationException(
                        $"Sprite profile for '{id}' has no default idle resource.");
                }

                foreach (string assetPath in entry.Profile.AssetPaths)
                {
                    ValidateResourcePath(
                        id,
                        assetPath,
                        validatedResourcePaths);
                }
                continue;
            }

            ValidateResourcePath(
                id,
                entry.StaticDefaultIdleTexturePath!,
                validatedResourcePaths);

            if (id != "FINN")
            {
                throw new InvalidOperationException(
                    $"FINN must remain the single static sprite fallback; "
                    + $"found '{id}'.");
            }
        }

        if (SpecialScenePaths.Count != 3
            || !SpecialScenePaths.ContainsKey("MEOW")
            || !SpecialScenePaths.ContainsKey("MU_MU")
            || !SpecialScenePaths.ContainsKey("OINK"))
        {
            throw new InvalidOperationException(
                "MEOW, MU_MU, and OINK must remain the three scene visual "
                + "exceptions.");
        }

        foreach ((string id, string scenePath) in SpecialScenePaths)
        {
            ValidateResourcePath(id, scenePath, validatedResourcePaths);
        }
    }

    private static void ValidateLayout(
        string id,
        CreatureVisualLayout layout)
    {
        if (!float.IsFinite(layout.SpriteScale.X)
            || !float.IsFinite(layout.SpriteScale.Y)
            || Mathf.IsZeroApprox(layout.SpriteScale.X)
            || Mathf.IsZeroApprox(layout.SpriteScale.Y))
        {
            throw new InvalidOperationException(
                $"Monster visual '{id}' has a non-finite or zero sprite scale.");
        }
    }

    private static void ValidateResourcePath(
        string id,
        string path,
        ISet<string> validatedResourcePaths)
    {
        if (!validatedResourcePaths.Add(path))
        {
            return;
        }

        if (!ResourceLoader.Exists(path))
        {
            throw new InvalidOperationException(
                $"Monster visual '{id}' references a missing resource: "
                + path);
        }
    }

    private static void EnsureSameIds(
        string label,
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        string[] missing = expected
            .Except(actual, StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        string[] unexpected = actual
            .Except(expected, StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length == 0 && unexpected.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Monster visual {label} mismatch. "
            + $"Missing=[{string.Join(", ", missing)}], "
            + $"Unexpected=[{string.Join(", ", unexpected)}].");
    }
}

internal sealed partial class CreatureStateDisplayOffset : Node
{
    public float LiftY { get; init; }

    public Vector2 AdditionalOffset { get; set; }

    private readonly List<(Control Control, Vector2 BasePosition)> _targets = new();

    public override void _Ready()
    {
        SetProcess(false);
        if (Mathf.IsZeroApprox(LiftY) && AdditionalOffset == Vector2.Zero)
        {
            return;
        }

        CallDeferred(nameof(InitializeOffset));
    }

    public override void _Process(double delta)
    {
        ApplyOffset();
    }

    private void InitializeOffset()
    {
        if (GetParent()?.GetParent() is not NCreature creatureNode)
        {
            return;
        }

        NCreatureStateDisplay? stateDisplay = creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        if (stateDisplay == null)
        {
            return;
        }

        CaptureTarget(stateDisplay, "%HealthBar");
        CaptureTarget(stateDisplay, "%PowerContainer");
        CaptureTarget(stateDisplay, "%NameplateContainer");
        ApplyOffset();
        SetProcess(_targets.Count > 0);
    }

    private void CaptureTarget(Node parent, NodePath path)
    {
        if (parent.GetNodeOrNull<Control>(path) is { } control)
        {
            _targets.Add((control, control.Position));
        }
    }

    private void ApplyOffset()
    {
        // NPowerContainer rewrites its own Position after power changes, so keep the lifted Y stable.
        foreach ((Control control, Vector2 basePosition) in _targets)
        {
            control.Position = basePosition + new Vector2(
                AdditionalOffset.X,
                AdditionalOffset.Y - LiftY);
        }
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.CreateVisuals))]
[LibraryPatch(Reason = "原版 CreateVisuals 非虚，只会实例化 VisualsPath（可覆写）指向的场景；本模组外观多为运行时用代码拼装的精灵节点，没有对应场景可指，换路径做不到。只作用于 MonsterVisualCatalog 登记的本模组怪物 id。")]
public static class MonsterModelCreateVisualsPatch
{
    private static bool Prefix(MonsterModel __instance, ref NCreatureVisuals __result)
    {
        if (!WrappedMonsterVisualFactory.ShouldWrap(__instance))
            return true;

        try
        {
            __result = WrappedMonsterVisualFactory.Create(__instance);
            return false;
        }
        catch (Exception ex)
        {
            MonsterVisualDebug.Write($"CreateVisuals FAILED id={__instance.Id.Entry}: {ex}");
            Log.Error($"[LibraryOfRuina] CreateVisuals failed for {__instance.Id.Entry}: {ex}");
            throw;
        }
    }
}

internal static class WrappedMonsterVisualFactory
{
    private static readonly Vector2 DefaultStolenCardScale = new(0.55f, 0.55f);

    public static bool ShouldWrap(MonsterModel monster)
    {
        return MonsterVisualCatalog.Contains(monster.Id.Entry);
    }

    public static NCreatureVisuals Create(MonsterModel monster) =>
        MonsterVisualCatalog.Create(monster);

    internal static NCreatureVisuals CreateStaticSpriteVisuals(
        string id,
        CreatureVisualLayout layout,
        string texturePath)
    {
        var visuals = new NCreatureVisuals { Name = id };
        var sprite = CreateSpriteNode("Visuals", layout, visible: true);

        Texture2D? texture = TryLoadTexture(id, texturePath);
        GodotTextureSafety.TrySetTexture(sprite, texture);

        AddLayoutNodes(visuals, layout, sprite, attackSprite: null);
        return visuals;
    }

    internal static NCreatureVisuals CreateScriptedSpriteVisuals<TVisuals>(
        string id,
        Action<TVisuals>? configure = null)
        where TVisuals : SpriteAttackCreatureVisuals, new()
    {
        SpriteVisualProfile profile =
            MonsterVisualCatalog.GetRequiredProfile(id);
        return CreateScriptedSpriteVisuals(
            id,
            profile.DefaultIdleTexturePath,
            configure);
    }

    internal static NCreatureVisuals CreateScriptedSpriteVisuals<TVisuals>(
        string id,
        string idleTexturePath,
        Action<TVisuals>? configure = null)
        where TVisuals : SpriteAttackCreatureVisuals, new()
    {
        CreatureVisualLayout layout = MonsterVisualCatalog.GetLayout(id);

        var visuals = new TVisuals { Name = id };
        configure?.Invoke(visuals);
        SpriteVisualProfile profile = visuals.SpriteProfile
            ?? throw new InvalidOperationException(
                $"{typeof(TVisuals).Name} has no sprite visual profile.");
        SpriteVisualProfile catalogProfile =
            MonsterVisualCatalog.GetRequiredProfile(id);
        if (!ReferenceEquals(profile, catalogProfile))
        {
            throw new InvalidOperationException(
                $"Catalog profile for '{id}' does not match "
                + $"{typeof(TVisuals).Name}.SpriteProfile.");
        }
        if (!profile.AssetPaths.Contains(
                idleTexturePath,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Initial idle texture '{idleTexturePath}' for '{id}' is not "
                + "declared by its sprite visual profile.");
        }

        MonsterVisualDebug.Trace(
            $"Create id={id} visualClass={typeof(TVisuals).Name} "
            + $"texturePath={idleTexturePath}");
        var idleSprite = CreateSpriteNode("Visuals", layout, visible: true);
        var attackSprite = CreateSpriteNode("AttackVisuals", layout, visible: false);
        var motionRoot = new Node2D
        {
            Name = "MotionRoot",
            UniqueNameInOwner = true,
            Position = Vector2.Zero,
        };
        motionRoot.AddChild(idleSprite);
        motionRoot.AddChild(attackSprite);

        Texture2D? texture = TryLoadTexture(id, idleTexturePath);
        GodotTextureSafety.TrySetTexture(idleSprite, texture);

        AddLayoutNodes(visuals, layout, idleSprite, attackSprite, motionRoot);

        MonsterVisualDebug.Trace(
            $"Created id={id} visualClass={typeof(TVisuals).Name} children={visuals.GetChildCount()} " +
            $"hasMotionRoot={visuals.HasNode("MotionRoot")} hasVisuals={visuals.HasNode("Visuals")} hasAttackVisuals={visuals.HasNode("AttackVisuals")} " +
            $"hasBounds={visuals.HasNode("Bounds")} hasCenter={visuals.HasNode("CenterPos")} hasIntent={visuals.HasNode("IntentPos")}");
        return visuals;
    }

    private static Sprite2D CreateSpriteNode(string nodeName, CreatureVisualLayout layout, bool visible)
    {
        return new Sprite2D
        {
            Name = nodeName,
            UniqueNameInOwner = true,
            Position = layout.SpritePos,
            Scale = layout.SpriteScale,
            Visible = visible,
        };
    }

    private static Texture2D? TryLoadTexture(string id, string texturePath)
    {
        Texture2D? texture = null;
        try
        {
            texture = ResourceLoader.Load<Texture2D>(texturePath);
        }
        catch (Exception ex)
        {
            MonsterVisualDebug.Write($"Texture load error id={id}: {ex.Message}");
        }

        if (texture != null)
        {
            MonsterVisualDebug.Trace($"Texture loaded id={id} size={texture.GetSize()}");
        }
        else
        {
            MonsterVisualDebug.Write($"Texture NULL id={id} path={texturePath} exists={ResourceLoader.Exists(texturePath)}");
        }

        return texture;
    }

    private static void AddLayoutNodes(
        NCreatureVisuals visuals,
        CreatureVisualLayout layout,
        Sprite2D visualSprite,
        Sprite2D? attackSprite,
        Node2D? motionRoot = null)
    {
        var bounds = new Control
        {
            Name = "Bounds",
            UniqueNameInOwner = true,
            LayoutMode = 3,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = layout.BoundsLeft,
            OffsetTop = layout.BoundsTop,
            OffsetRight = layout.BoundsRight,
            OffsetBottom = layout.BoundsBottom,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var center = new Marker2D
        {
            Name = "CenterPos",
            UniqueNameInOwner = true,
            Position = layout.CenterPos,
        };

        var intent = new Marker2D
        {
            Name = "IntentPos",
            UniqueNameInOwner = true,
            Position = layout.IntentPos,
        };

        Marker2D? talk = null;
        if (layout.TalkPos.HasValue)
        {
            talk = new Marker2D
            {
                Name = "TalkPos",
                UniqueNameInOwner = true,
                Position = layout.TalkPos.Value,
            };
        }

        Marker2D? stolenCard = null;
        if (layout.StolenCardPos.HasValue)
        {
            stolenCard = new Marker2D
            {
                Name = "StolenCardPos",
                UniqueNameInOwner = true,
                Position = layout.StolenCardPos.Value,
                Scale = layout.StolenCardScale ?? DefaultStolenCardScale,
            };
        }

        if (motionRoot != null)
        {
            visuals.AddChild(motionRoot);
        }
        else
        {
            visuals.AddChild(visualSprite);
            if (attackSprite != null)
            {
                visuals.AddChild(attackSprite);
            }
        }
        visuals.AddChild(bounds);
        visuals.AddChild(center);
        visuals.AddChild(intent);
        if (talk != null)
        {
            visuals.AddChild(talk);
        }
        if (stolenCard != null)
        {
            visuals.AddChild(stolenCard);
        }
        if (!Mathf.IsZeroApprox(layout.StateDisplayLiftY))
        {
            var stateDisplayOffset = new CreatureStateDisplayOffset
            {
                Name = "StateDisplayOffset",
                LiftY = layout.StateDisplayLiftY,
            };
            visuals.AddChild(stateDisplayOffset);
            AssignOwnerRecursive(stateDisplayOffset, visuals);
        }

        if (motionRoot != null)
        {
            AssignOwnerRecursive(motionRoot, visuals);
        }
        else
        {
            AssignOwnerRecursive(visualSprite, visuals);
            if (attackSprite != null)
            {
                AssignOwnerRecursive(attackSprite, visuals);
            }
        }
        AssignOwnerRecursive(bounds, visuals);
        AssignOwnerRecursive(center, visuals);
        AssignOwnerRecursive(intent, visuals);
        if (talk != null)
        {
            AssignOwnerRecursive(talk, visuals);
        }
        if (stolenCard != null)
        {
            AssignOwnerRecursive(stolenCard, visuals);
        }
    }

    internal static NCreatureVisuals CreateSceneBackedVisuals<TVisuals>(
        string id,
        string scenePath)
        where TVisuals : SceneAnimatedCreatureVisuals, new()
    {
        PackedScene packed = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException(
                $"Cannot load scene-backed monster visual: {scenePath}");
        Node2D templateRoot = packed.Instantiate<Node2D>();

        try
        {
            if (templateRoot.GetScript().VariantType != Variant.Type.Nil)
            {
                throw new InvalidOperationException(
                    $"Scene-backed monster visual '{id}' must use a "
                    + "scriptless template root.");
            }
            if (!templateRoot.Position.IsEqualApprox(Vector2.Zero)
                || !templateRoot.Scale.IsEqualApprox(Vector2.One)
                || !Mathf.IsZeroApprox(templateRoot.Rotation)
                || !Mathf.IsZeroApprox(templateRoot.Skew))
            {
                throw new InvalidOperationException(
                    $"Scene-backed monster visual '{id}' requires an identity "
                    + "template root; move MotionRoot instead.");
            }

            var visuals = new TVisuals { Name = id };
            while (templateRoot.GetChildCount() > 0)
            {
                Node child = templateRoot.GetChild(0);
                templateRoot.RemoveChild(child);
                visuals.AddChild(child);
                AssignOwnerRecursive(child, visuals);
            }

            ValidateSceneBackedNodes(id, visuals);
            MonsterVisualDebug.Trace(
                $"Create id={id} sceneVisualClass={typeof(TVisuals).Name} "
                + $"scenePath={scenePath} children={visuals.GetChildCount()}");
            return visuals;
        }
        finally
        {
            templateRoot.Free();
        }
    }

    private static void ValidateSceneBackedNodes(
        string id,
        NCreatureVisuals visuals)
    {
        Node2D motionRoot = visuals.GetNodeOrNull<Node2D>("MotionRoot")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing MotionRoot.");
        Sprite2D idle = visuals.GetNodeOrNull<Sprite2D>(
                "MotionRoot/Visuals")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing Visuals.");
        Sprite2D attack = visuals.GetNodeOrNull<Sprite2D>(
                "MotionRoot/AttackVisuals")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing AttackVisuals.");
        AnimationPlayer animationPlayer =
            visuals.GetNodeOrNull<AnimationPlayer>("AnimationPlayer")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing AnimationPlayer.");
        Control bounds = visuals.GetNodeOrNull<Control>("Bounds")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing Bounds.");
        Marker2D center = visuals.GetNodeOrNull<Marker2D>("CenterPos")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing CenterPos.");
        Marker2D intent = visuals.GetNodeOrNull<Marker2D>("IntentPos")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing IntentPos.");

        foreach (Node node in new Node[]
                 {
                     motionRoot,
                     idle,
                     attack,
                     animationPlayer,
                     bounds,
                     center,
                     intent
                 })
        {
            if (!node.UniqueNameInOwner)
            {
                throw new InvalidOperationException(
                    $"Scene-backed monster visual '{id}' node '{node.Name}' "
                    + "must keep unique_name_in_owner=true.");
            }
        }
    }

    public static NCreatureVisuals CreateFromScene(string scenePath)
    {
        PackedScene? packed = null;
        try { packed = ResourceLoader.Load<PackedScene>(scenePath); }
        catch (Exception ex) { MonsterVisualDebug.Write($"Scene load error path={scenePath}: {ex.Message}"); }

        if (packed == null)
            throw new InvalidOperationException($"Cannot load scene: {scenePath}");

        Node2D templateRoot = packed.Instantiate<Node2D>();
        MonsterVisualDebug.Trace($"Scene instantiated path={scenePath} rootType={templateRoot.GetType().FullName} rootClass={templateRoot.GetClass()}");

        if (templateRoot is NCreatureVisuals cv)
        {
            MonsterVisualDebug.Trace($"Scene returned direct NCreatureVisuals path={scenePath} name={cv.Name}");
            return cv;
        }

        var visuals = new NCreatureVisuals { Name = templateRoot.Name };
        while (templateRoot.GetChildCount() > 0)
        {
            Node child = templateRoot.GetChild(0);
            templateRoot.RemoveChild(child);
            visuals.AddChild(child);
            AssignOwnerRecursive(child, visuals);
        }
        templateRoot.Free();
        MonsterVisualDebug.Trace($"Scene wrapped into NCreatureVisuals path={scenePath} name={visuals.Name} children={visuals.GetChildCount()}");
        return visuals;
    }

    private static void AssignOwnerRecursive(Node node, Node owner)
    {
        node.Owner = owner;
        foreach (Node child in node.GetChildren())
            AssignOwnerRecursive(child, owner);
    }
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class AttackAnimationHitSuppressionAttackPatch
{
    private static void Prefix(AttackCommand __instance, out AttackAnimationHitSuppression.Scope? __state)
    {
        __state = AttackAnimationHitSuppression.Begin(__instance);
    }

    private static void Postfix(ref Task<AttackCommand> __result, AttackAnimationHitSuppression.Scope? __state)
    {
        if (__state == null)
        {
            return;
        }

        __result = ClearWhenComplete(__result, __state);
    }

    private static async Task<AttackCommand> ClearWhenComplete(
        Task<AttackCommand> result,
        AttackAnimationHitSuppression.Scope state)
    {
        try
        {
            return await result;
        }
        finally
        {
            state.Dispose();
        }
    }
}

internal static class AttackAnimationHitSuppression
{
    private static readonly FieldInfo? AttackerAnimNameField =
        AccessTools.Field(typeof(AttackCommand), "_attackerAnimName");

    private static readonly FieldInfo? ShouldPlayAnimationField =
        AccessTools.Field(typeof(AttackCommand), "_shouldPlayAnimation");

    private static readonly FieldInfo? VisualAttackerField =
        AccessTools.Field(typeof(AttackCommand), "_visualAttacker");

    private static readonly object Gate = new();
    private static readonly Dictionary<Creature, int> ActiveAttackAnimations = new();

    public static Scope? Begin(AttackCommand command)
    {
        if (!ShouldTrack(command))
        {
            return null;
        }

        List<Creature> creatures = new();
        AddDistinct(command.Attacker);
        AddDistinct(VisualAttackerField?.GetValue(command) as Creature);

        if (creatures.Count == 0)
        {
            return null;
        }

        lock (Gate)
        {
            foreach (Creature creature in creatures)
            {
                ActiveAttackAnimations.TryGetValue(creature, out int count);
                ActiveAttackAnimations[creature] = count + 1;
            }
        }

        return new Scope(creatures);

        void AddDistinct(Creature? creature)
        {
            if (creature != null && !creatures.Contains(creature))
            {
                creatures.Add(creature);
            }
        }
    }

    public static bool ShouldSuppress(Creature? creature, string trigger)
    {
        if (creature == null || !string.Equals(trigger, "Hit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lock (Gate)
        {
            return ActiveAttackAnimations.ContainsKey(creature);
        }
    }

    private static bool ShouldTrack(AttackCommand command)
    {
        bool shouldPlayAnimation = ShouldPlayAnimationField?.GetValue(command) as bool? ?? true;
        if (!shouldPlayAnimation)
        {
            return false;
        }

        string? attackerAnimName = AttackerAnimNameField?.GetValue(command) as string;
        return !string.IsNullOrWhiteSpace(attackerAnimName);
    }

    public sealed class Scope : IDisposable
    {
        private readonly IReadOnlyList<Creature> _creatures;
        private bool _disposed;

        public Scope(IReadOnlyList<Creature> creatures)
        {
            _creatures = creatures;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (Gate)
            {
                foreach (Creature creature in _creatures)
                {
                    if (!ActiveAttackAnimations.TryGetValue(creature, out int count))
                    {
                        continue;
                    }

                    if (count <= 1)
                    {
                        ActiveAttackAnimations.Remove(creature);
                    }
                    else
                    {
                        ActiveAttackAnimations[creature] = count - 1;
                    }
                }
            }
        }
    }
}

/// <summary>
/// 本模组怪物的外观不是 Spine，原版 SetAnimationTrigger 只驱动 Spine 动画机，对它们不起作用；这里把触发转给
/// 非 Spine 外观的处理器或 AnimationPlayer。攻击动画进行中收到的 "Hit" 会被忽略，免得打断攻击动作。
/// 只处理本模组怪物：原来的前缀会对任何 Spine 生物（原版角色、原版与其他模组的怪物）吞掉攻击中的受击触发。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class NonSpineAnimationTriggerBridgePatch
{
    private static readonly FieldInfo? SpineAnimatorField = AccessTools.Field(typeof(NCreature), "_spineAnimator");

    private static void Postfix(NCreature __instance, string trigger)
    {
        if (!ModOwnership.IsOwnMonster(__instance.Entity)
            || AttackAnimationHitSuppression.ShouldSuppress(__instance.Entity, trigger))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(trigger))
        {
            return;
        }

        if (HasSpineAnimator(__instance))
        {
            return;
        }

        NCreatureVisuals? visuals = __instance.Visuals;
        if (visuals == null)
        {
            return;
        }

        if (visuals is INonSpineVisualTriggerHandler triggerHandler
            && triggerHandler.TryPlayTrigger(trigger))
        {
            return;
        }

        AnimationPlayer? animationPlayer = ResolveAnimationPlayer(visuals);
        if (animationPlayer == null)
        {
            return;
        }

        if (!animationPlayer.HasAnimation(trigger))
        {
            return;
        }

        animationPlayer.Play(trigger);
    }

    private static AnimationPlayer? ResolveAnimationPlayer(NCreatureVisuals visuals)
    {
        AnimationPlayer? direct = visuals.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (direct != null)
        {
            return direct;
        }

        foreach (Node child in visuals.GetChildren())
        {
            if (child is AnimationPlayer player)
            {
                return player;
            }

            AnimationPlayer? nested = child.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool HasSpineAnimator(NCreature creature)
    {
        if (SpineAnimatorField == null)
        {
            return creature.HasSpineAnimation;
        }

        return SpineAnimatorField.GetValue(creature) != null;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class LiteratureFloorBlackSwanBrotherDeathVisualPatch
{
    private static void Prefix(NCreature __instance)
    {
        if (__instance.Entity.Monster
                is LiteratureFloorBlackSwanBrotherBase brother
            && __instance.Visuals
                is LiteratureFloorBlackSwanBrotherCreatureVisuals visuals)
        {
            visuals.ShowInactiveOrDeadIdle(brother);
        }
    }
}

[HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals._Ready))]
internal static class MonsterVisualsReadyDebugPatch
{
    private static Exception? Finalizer(NCreatureVisuals __instance, Exception? __exception)
    {
        if (__exception == null) return null;
        MonsterModel? monster = (__instance.GetParent() as NCreature)?.Entity?.Monster;
        if (monster != null && WrappedMonsterVisualFactory.ShouldWrap(monster))
        {
            MonsterVisualDebug.Write($"NCreatureVisuals._Ready exception id={monster.Id.Entry}: {__exception}");
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class MonsterNodeReadyDebugPatch
{
    private static Exception? Finalizer(NCreature __instance, Exception? __exception)
    {
        if (__exception == null) return null;
        MonsterModel? monster = __instance.Entity?.Monster;
        if (monster != null && WrappedMonsterVisualFactory.ShouldWrap(monster))
        {
            MonsterVisualDebug.Write($"NCreature._Ready exception id={monster.Id.Entry}: {__exception}");
        }
        return __exception;
    }
}

internal static class MonsterVisualDebug
{
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly string LogPath = ProjectSettings.GlobalizePath("user://mods/LibraryOfRuina/monster_visuals_debug.log");

    /// <summary>成功路径的诊断，每只怪生成外观都会走到，只进 Debug 级日志。</summary>
    public static void Trace(string message)
    {
        Log.Debug($"[MonsterVisualDebug] {message}");
    }

    /// <summary>失败路径：进游戏日志，同时追加到单独文件方便玩家反馈；文件超过 1 MB 时轮换为 .old。</summary>
    public static void Write(string message)
    {
        string text = $"[MonsterVisualDebug {DateTime.Now:HH:mm:ss.fff}] {message}";
        Log.Warn(text);
        try
        {
            string? dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var info = new FileInfo(LogPath);
            if (info.Exists && info.Length > MaxLogBytes)
                File.Move(LogPath, LogPath + ".old", overwrite: true);
            File.AppendAllText(LogPath, text + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 日志文件只是反馈辅助，写不进去时游戏日志里已有同一条。
        }
    }
}
