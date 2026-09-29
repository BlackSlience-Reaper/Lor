using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using LibraryOfRuina.cards.ArtFloorLiberation;
using LibraryOfRuina.cards.HistoryFloorLiberation;
using LibraryOfRuina.cards.LanguageFloorLiberation;
using LibraryOfRuina.cards.LiteratureFloorLiberation;
using LibraryOfRuina.cards.NaturalFloorLiberation;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.content.abnormalities.BigBadWolf;
using LibraryOfRuina.content.abnormalities.BigBird;
using LibraryOfRuina.content.abnormalities.BlueStar;
using LibraryOfRuina.content.abnormalities.BurrowingHeaven;
using LibraryOfRuina.content.abnormalities.CosmicFragment;
using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.content.abnormalities.FairyFestival;
using LibraryOfRuina.content.abnormalities.ForsakenMurderer;
using LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;
using LibraryOfRuina.content.abnormalities.GalaxyChild;
using LibraryOfRuina.content.abnormalities.HappyTeddy;
using LibraryOfRuina.content.abnormalities.HeartOfAspiration;
using LibraryOfRuina.content.abnormalities.JudgementBird;
using LibraryOfRuina.content.abnormalities.KingOfGreed;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.Nosferatu;
using LibraryOfRuina.content.abnormalities.Ozma;
using LibraryOfRuina.content.abnormalities.PriceOfSilence;
using LibraryOfRuina.content.abnormalities.QueenBee;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.RedShoes;
using LibraryOfRuina.content.abnormalities.RoadHome;
using LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;
using LibraryOfRuina.content.abnormalities.SmilingBodies;
using LibraryOfRuina.content.abnormalities.SongMachine;
using LibraryOfRuina.content.abnormalities.SpiderBud;
using LibraryOfRuina.content.abnormalities.SpinyBus;
using LibraryOfRuina.content.abnormalities.TodaysShyLook;
using LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;
using LibraryOfRuina.content.abnormalities.WrathServant;

namespace LibraryOfRuina.patches;

internal sealed class PageChoiceHoverTip(CardModel card) : CardHoverTip(card);

[HarmonyPatch]
internal static class PageRelicHoverTipsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.HoverTips));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.HoverTipsExcludingRelic));
    }

    [HarmonyPostfix]
    private static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        // 书页绑定具体模式后，仅保留遗物原有的效果提示。
        if (LibraryOfRuina.framework.relics.AbnormalityPageRewardPreselection.HasConcreteModeForPatch(__instance))
        {
            return;
        }

        IEnumerable<IHoverTip> choices = GetChoicePreviews(__instance);
        __result = __result.Concat(choices);
    }

    private static IHoverTip Preview<TCard>(bool upgrade) where TCard : CardModel
    {
        CardHoverTip tip = (CardHoverTip)HoverTipFactory.FromCard<TCard>(upgrade);
        return new PageChoiceHoverTip(tip.Card);
    }

    private static IEnumerable<IHoverTip> GetChoicePreviews(RelicModel relic)
    {
        return relic switch
        {
            LibraryOfRuina.relics.LanguageFloorLiberation.NothingTherePageRelic =>
            [
                Preview<NothingThereGoodbyeChoiceCard>(upgrade: false),
                Preview<NothingThereHelloChoiceCard>(upgrade: false),
                Preview<NothingThereShellChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.relics.ArtFloorLiberation.SilentOrchestraPageRelic =>
            [
                Preview<SilentOrchestraEverRepeatingPerformanceChoiceCard>(upgrade: false),
                Preview<SilentOrchestraFerventAdorationChoiceCard>(upgrade: false),
                Preview<SilentOrchestraFinaleChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.FairyFestival.FairyFestivalPageRelic =>
            [
                Preview<FairyCareChoiceCard>(upgrade: false),
                Preview<FairyGluttonyChoiceCard>(upgrade: false),
                Preview<FairyPredationChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.Nosferatu.NosferatuPageRelic =>
            [
                Preview<NosferatuHydrophobiaChoiceCard>(upgrade: false),
                Preview<NosferatuVampirismChoiceCard>(upgrade: false),
                Preview<NosferatuWineChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.WrathServant.WrathServantPageRelic =>
            [
                Preview<WrathServantWrathChoiceCard>(upgrade: false),
                Preview<WrathServantFriendChoiceCard>(upgrade: false),
                Preview<WrathServantVenomChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.CosmicFragment.CosmicFragmentPageRelic =>
            [
                Preview<CosmicFragmentOtherworldlyEchoChoiceCard>(upgrade: false),
                Preview<CosmicFragmentTentacleChoiceCard>(upgrade: false),
                Preview<CosmicFragmentIncomprehensibleChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.QueenBee.QueenBeePageRelic =>
            [
                Preview<QueenBeeSporeChoiceCard>(upgrade: false),
                Preview<QueenBeeWorkerBeeChoiceCard>(upgrade: false),
                Preview<QueenBeeLoyaltyChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.relics.LiteratureFloorLiberation.BlackSwanDreamPageRelic =>
            [
                Preview<BlackSwanFilthChoiceCard>(upgrade: false),
                Preview<BlackSwanBrokenUmbrellaChoiceCard>(upgrade: false),
                Preview<BlackSwanDearFamilyChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.relics.TechnologyFloorLiberation.MagicBulletShooterPageRelic =>
            [
                Preview<MagicBulletCommissionChoiceCard>(upgrade: false),
                Preview<MagicBulletSeventhBulletChoiceCard>(upgrade: false),
                Preview<MagicBulletBlackFlameChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.GalaxyChild.GalaxyChildPageRelic =>
            [
                Preview<GalaxyChildPebbleChoiceCard>(upgrade: false),
                Preview<GalaxyChildProofOfFriendshipChoiceCard>(upgrade: false),
                Preview<GalaxyChildTearsChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.WarmheartedWoodsman.WarmheartedWoodsmanPageRelic =>
            [
                Preview<WarmheartedWoodsmanWarmHeartChoiceCard>(upgrade: false),
                Preview<WarmheartedWoodsmanHeartChoiceCard>(upgrade: false),
                Preview<WarmheartedWoodsmanLoggingChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.Leticia.LeticiaPageRelic =>
            [
                Preview<LeticiaPageSurpriseGiftChoiceCard>(upgrade: false),
                Preview<LeticiaPageBuddyChoiceCard>(upgrade: false),
                Preview<LeticiaPageMischiefChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.SpiderBud.SpiderBudPageRelic =>
            [
                Preview<SpiderBudCocoonBindChoiceCard>(upgrade: false),
                Preview<SpiderBudFeedingChoiceCard>(upgrade: false),
                Preview<SpiderBudVigilanceChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.KingOfGreed.KingOfGreedPageRelic =>
            [
                Preview<KingOfGreedIndulgenceChoiceCard>(upgrade: false),
                Preview<KingOfGreedHappinessPathChoiceCard>(upgrade: false),
                Preview<KingOfGreedGreedChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.relics.NaturalFloorLiberation.WrathServantEnhancedPageRelic =>
            [
                Preview<WrathServantWrathChoiceCard>(upgrade: true),
                Preview<WrathServantFriendChoiceCard>(upgrade: true),
                Preview<WrathServantVenomChoiceCard>(upgrade: true)
            ],
            LibraryOfRuina.relics.NaturalFloorLiberation.QueenOfHatredEnhancedPageRelic =>
            [
                Preview<QueenOfHatredPhilanthropyChoiceCard>(upgrade: true),
                Preview<QueenOfHatredJusticeChoiceCard>(upgrade: true),
                Preview<QueenOfHatredHatredChoiceCard>(upgrade: true)
            ],
            LibraryOfRuina.relics.NaturalFloorLiberation.NihilPageRelic =>
            [
                Preview<NihilMagicalGirlsChoiceCard>(upgrade: false),
                Preview<NihilEmptinessChoiceCard>(upgrade: false),
                Preview<NihilNihilityChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.relics.NaturalFloorLiberation.KingOfGreedEnhancedPageRelic =>
            [
                Preview<KingOfGreedIndulgenceChoiceCard>(upgrade: true),
                Preview<KingOfGreedHappinessPathChoiceCard>(upgrade: true),
                Preview<KingOfGreedGreedChoiceCard>(upgrade: true)
            ],
            LibraryOfRuina.relics.NaturalFloorLiberation.DespairKnightEnhancedPageRelic =>
            [
                Preview<DespairKnightBlessingChoiceCard>(upgrade: true),
                Preview<DespairKnightDespairChoiceCard>(upgrade: true),
                Preview<DespairKnightTearSwordChoiceCard>(upgrade: true)
            ],
            LibraryOfRuina.content.abnormalities.DespairKnight.DespairKnightPageRelic =>
            [
                Preview<DespairKnightBlessingChoiceCard>(upgrade: false),
                Preview<DespairKnightDespairChoiceCard>(upgrade: false),
                Preview<DespairKnightTearSwordChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.AllAroundHelper.AllAroundHelperPageRelic =>
            [
                Preview<AllAroundHelperChargeChoiceCard>(upgrade: false),
                Preview<AllAroundHelperRecognitionFunctionChoiceCard>(upgrade: false),
                Preview<AllAroundHelperCleanChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.BurrowingHeaven.BurrowingHeavenPageRelic =>
            [
                Preview<BurrowingHeavenWitheringBloodWingsChoiceCard>(upgrade: false),
                Preview<BurrowingHeavenOthersGazeChoiceCard>(upgrade: false),
                Preview<BurrowingHeavenAttentionAndFocusChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.Ozma.OzmaPageRelic =>
            [
                Preview<OzmaOldPowerChoiceCard>(upgrade: false),
                Preview<OzmaForgetChoiceCard>(upgrade: false),
                Preview<OzmaLifePowderChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.LittleRedMercenary.LittleRedMercenaryPageRelic =>
            [
                Preview<LittleRedScarChoiceCard>(upgrade: false),
                Preview<LittleRedRevengeChoiceCard>(upgrade: false),
                Preview<LittleRedPreyChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.JudgementBird.JudgementBirdPageRelic =>
            [
                Preview<JudgementBirdWeightOfSinChoiceCard>(upgrade: false),
                Preview<JudgementBirdJudgementChoiceCard>(upgrade: false),
                Preview<JudgementBirdTiltedScaleChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.PriceOfSilence.PriceOfSilencePageRelic =>
            [
                Preview<PriceOfSilenceTimeChoiceCard>(upgrade: false),
                Preview<PriceOfSilenceThirteenthTollChoiceCard>(upgrade: false),
                Preview<PriceOfSilenceSilenceChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.TodaysShyLook.TodaysShyLookPageRelic =>
            [
                Preview<TodaysShyLookTodaysExpressionChoiceCard>(upgrade: false),
                Preview<TodaysShyLookShynessChoiceCard>(upgrade: false),
                Preview<TodaysShyLookSocialDistanceChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.SongMachine.SongMachinePageRelic =>
            [
                Preview<SongMachineMusicChoiceCard>(upgrade: false),
                Preview<SongMachineMelodyChoiceCard>(upgrade: false),
                Preview<SongMachineAddictionChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterfliesPageRelic =>
            [
                Preview<FuneralRestChoiceCard>(upgrade: false),
                Preview<FuneralCoffinChoiceCard>(upgrade: false),
                Preview<FuneralMourningChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.BigBird.BigBirdPageRelic =>
            [
                Preview<BigBirdWatchfulEyeChoiceCard>(upgrade: false),
                Preview<BigBirdEverBurningLampChoiceCard>(upgrade: false),
                Preview<BigBirdSalvationChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.RoadHome.RoadHomePageRelic =>
            [
                Preview<RoadHomeCourageChoiceCard>(upgrade: false),
                Preview<RoadHomeCompanionRoadChoiceCard>(upgrade: false),
                Preview<RoadHomeHomeChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.RedShoes.RedShoesPageRelic =>
            [
                Preview<RedShoesGlitterChoiceCard>(upgrade: false),
                Preview<RedShoesBloodThirstChoiceCard>(upgrade: false),
                Preview<RedShoesAxeChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.QueenOfHatred.QueenOfHatredPageRelic =>
            [
                Preview<QueenOfHatredPhilanthropyChoiceCard>(upgrade: false),
                Preview<QueenOfHatredJusticeChoiceCard>(upgrade: false),
                Preview<QueenOfHatredHatredChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.BigBadWolf.BigBadWolfPageRelic =>
            [
                Preview<BigBadWolfPredatoryInstinctChoiceCard>(upgrade: false),
                Preview<BigBadWolfWolfRoleChoiceCard>(upgrade: false),
                Preview<BigBadWolfCruelClawsChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.relics.HistoryFloorLiberation.SnowWhiteApplePageRelic =>
            [
                Preview<SnowWhiteStranglingVineChoiceCard>(upgrade: false),
                Preview<SnowWhitePoisonStingBarrierChoiceCard>(upgrade: false),
                Preview<SnowWhiteMaliceChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.relics.HistoryFloorLiberation.MatchMarkRelic =>
            [
                Preview<MatchMarkEmberChoiceCard>(upgrade: false),
                Preview<MatchMarkFootstepsChoiceCard>(upgrade: false),
                Preview<MatchMarkAfterglowChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.ForsakenMurderer.ForsakenMurdererPageRelic =>
            [
                Preview<ForsakenMurdererIronEchoChoiceCard>(upgrade: false),
                Preview<ForsakenMurdererBoundWrathChoiceCard>(upgrade: false),
                Preview<ForsakenMurdererExtremeViolenceChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.SpinyBus.SpinyBusPageRelic =>
            [
                Preview<SpinyBusThornsChoiceCard>(upgrade: false),
                Preview<SpinyBusPleasureChoiceCard>(upgrade: false),
                Preview<SpinyBusLaughingPowderChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.HappyTeddy.HappyTeddyPageRelic =>
            [
                Preview<HappyTeddyLongingEmbraceChoiceCard>(upgrade: false),
                Preview<HappyTeddyHappyMemoryChoiceCard>(upgrade: false),
                Preview<HappyTeddyExpressAffectionChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.SmilingBodies.SmilingBodiesPageRelic =>
            [
                Preview<SmilingBodiesCorpseLaughsChoiceCard>(upgrade: false),
                Preview<SmilingBodiesCorpseAbsorptionChoiceCard>(upgrade: false),
                Preview<SmilingBodiesCorpseMountainChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom.ScarecrowPageRelic =>
            [
                Preview<ScarecrowRakeChoiceCard>(upgrade: false),
                Preview<ScarecrowHarvestChoiceCard>(upgrade: false),
                Preview<ScarecrowTornWisdomChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.BlueStar.BlueStarPageRelic =>
            [
                Preview<BlueStarMartyrdomChoiceCard>(upgrade: false),
                Preview<BlueStarAtonementChoiceCard>(upgrade: false),
                Preview<BlueStarVoiceOfRemembranceChoiceCard>(upgrade: false)
            ],
            LibraryOfRuina.content.abnormalities.HeartOfAspiration.HeartOfAspirationPageRelic =>
            [
                Preview<HeartOfAspirationPulseChoiceCard>(upgrade: false),
                Preview<HeartOfAspirationAspirationChoiceCard>(upgrade: false),
                Preview<HeartOfAspirationViolentPulseChoiceCard>(upgrade: false)
            ],
            _ => []
        };
    }
}

[HarmonyPatch(typeof(NHoverTipCardContainer), nameof(NHoverTipCardContainer.Add))]
internal static class PageChoiceHoverTipSizePatch
{
    // 书页遗物附加卡牌预览的宽高为原始尺寸的 86.25%。
    private const float PreviewScale = 0.8625f;

    [HarmonyPostfix]
    private static void Postfix(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PageChoiceHoverTip)
        {
            return;
        }

        Control tip = __instance.GetChild<Control>(__instance.GetChildCount() - 1);
        NCard card = tip.GetNode<NCard>("%Card");
        card.Scale *= PreviewScale;
        card.Position *= PreviewScale;
        tip.CustomMinimumSize *= PreviewScale;
        tip.Size *= PreviewScale;
    }
}
