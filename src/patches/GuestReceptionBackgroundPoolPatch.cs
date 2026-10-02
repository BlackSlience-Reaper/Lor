using System;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.AddictedEmployee;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.content.abnormalities.BigBadWolf;
using LibraryOfRuina.content.abnormalities.BigBird;
using LibraryOfRuina.content.abnormalities.BlueStar;
using LibraryOfRuina.content.abnormalities.BurrowingHeaven;
using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.content.abnormalities.FairyFestival;
using LibraryOfRuina.content.abnormalities.GalaxyChild;
using LibraryOfRuina.content.abnormalities.HeartOfAspiration;
using LibraryOfRuina.content.abnormalities.JudgementBird;
using LibraryOfRuina.content.abnormalities.KingOfGreed;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.Nosferatu;
using LibraryOfRuina.content.abnormalities.PriceOfSilence;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.RoadHome;
using LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;
using LibraryOfRuina.content.abnormalities.SpinyBus;
using LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(EncounterModel), "CreateBackgroundAssetsForCustom")]
[LibraryPatch(Reason = "原版自定义背景的标题只能是遭遇 id，且方法私有、没有 Hook；只处理本模组遭遇类型和实现 IGuestReceptionEncounter 的接待遭遇，给它们指定共享背景或抽取接待层。")]
internal static class GuestReceptionBackgroundPoolPatch
{
    private const string QueenOfHatredBackgroundTitle = "queen_of_hatred";
    private const string KingOfGreedBackgroundTitle = "king_of_greed";
    private const string AllAroundHelperBackgroundTitle = "all_around_helper";
    private const string LittleRedMercenaryEliteBackgroundTitle = "little_red_mercenary_elite";
    private const string FairyFestivalStrongBackgroundTitle = "fairy_festival_strong";
    private const string AddictedEmployeeBackgroundTitle = "addicted_employee";
    private const string LeticiaEliteBackgroundTitle = "leticia_elite";
    private const string GalaxyChildBackgroundTitle = "galaxy_child";
    private const string SpinyBusWeakBackgroundTitle = "spiny_bus_weak";
    private const string BigBadWolfWeakBackgroundTitle = "big_bad_wolf_weak";
    private const string DespairKnightStrongBackgroundTitle = "despair_knight_strong";
    private const string WrathServantStrongBackgroundTitle = "wrath_servant_strong";
    private const string NosferatuEliteBackgroundTitle = "nosferatu_elite";
    private const string ScarecrowSearchingForWisdomBackgroundTitle = "scarecrow_searching_for_wisdom_weak";
    private const string BurrowingHeavenWeakBackgroundTitle = "burrowing_heaven_weak";
    private const string HeartOfAspirationWeakBackgroundTitle = "heart_of_aspiration_weak";
    private const string WarmheartedWoodsmanStrongBackgroundTitle = "warmhearted_woodsman_strong";
    private const string PriceOfSilenceStrongBackgroundTitle = "price_of_silence_strong";
    private const string BlueStarStrongBackgroundTitle = "blue_star_strong";
    private const string BigBirdStrongBackgroundTitle = "big_bird_strong";
    private const string PunishingBirdStrongBackgroundTitle = "punishing_bird_strong";
    private const string JudgementBirdEliteBackgroundTitle = "judgement_bird_elite";
    private const string RoadHomeEliteBackgroundTitle = "road_home_elite";

    [HarmonyPrefix]
    private static bool Prefix(EncounterModel __instance, Rng rng, ref BackgroundAssets __result)
    {
        Type encounterType = __instance.GetType();
        if (encounterType == typeof(KaliSpecialGuestEncounter))
        {
            __result = new BackgroundAssets(GuestReceptionPoolRegistry.SharedBackgroundTitle, rng);
            __result.BgLayers.Clear();
            __result.BgLayers.Add(GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " fixed layer: " + GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath);
            return false;
        }

        // 不看“启用内容”开关：本模组遭遇只要出现（例如关闭内容后继续旧局）就需要这些背景，
        // 交还原版会按遭遇 id 找背景目录，找不到时原版直接抛异常。
        if (encounterType == typeof(RedMistElite))
        {
            __result = new BackgroundAssets(GuestReceptionPoolRegistry.SharedBackgroundTitle, rng);
            __result.BgLayers.Clear();
            __result.BgLayers.Add(GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " fixed layer: " + GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath);
            return false;
        }

        if (encounterType == typeof(LibraryOfRuina.content.abnormalities.Alriune.AlriuneStrong))
        {
            __result = new BackgroundAssets("alriune_strong", rng);
            return false;
        }

        if (encounterType == typeof(QueenOfHatredStrong))
        {
            __result = new BackgroundAssets(QueenOfHatredBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + QueenOfHatredBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(KingOfGreedElite))
        {
            __result = new BackgroundAssets(KingOfGreedBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + KingOfGreedBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(AllAroundHelperWeak) || encounterType == typeof(AllAroundHelperStrong))
        {
            __result = new BackgroundAssets(AllAroundHelperBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + AllAroundHelperBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(AddictedEmployeeWeak) || encounterType == typeof(AddictedEmployeeStrong))
        {
            __result = new BackgroundAssets(AddictedEmployeeBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + AddictedEmployeeBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(LeticiaElite))
        {
            __result = new BackgroundAssets(LeticiaEliteBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + LeticiaEliteBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(LittleRedMercenaryElite))
        {
            __result = new BackgroundAssets(LittleRedMercenaryEliteBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + LittleRedMercenaryEliteBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(FairyFestivalStrong))
        {
            __result = new BackgroundAssets(FairyFestivalStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + FairyFestivalStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(GalaxyChildWeak))
        {
            __result = new BackgroundAssets(GalaxyChildBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + GalaxyChildBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(SpinyBusWeak))
        {
            __result = new BackgroundAssets(SpinyBusWeakBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + SpinyBusWeakBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(BigBadWolfWeak))
        {
            __result = new BackgroundAssets(BigBadWolfWeakBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + BigBadWolfWeakBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(DespairKnightStrong))
        {
            __result = new BackgroundAssets(DespairKnightStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + DespairKnightStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(WrathServantStrong))
        {
            __result = new BackgroundAssets(WrathServantStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + WrathServantStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(NosferatuElite))
        {
            __result = new BackgroundAssets(NosferatuEliteBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + NosferatuEliteBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(ScarecrowSearchingForWisdomWeak))
        {
            __result = new BackgroundAssets(ScarecrowSearchingForWisdomBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + ScarecrowSearchingForWisdomBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(HeartOfAspirationWeak))
        {
            __result = new BackgroundAssets(HeartOfAspirationWeakBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + HeartOfAspirationWeakBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(BurrowingHeavenWeak))
        {
            __result = new BackgroundAssets(BurrowingHeavenWeakBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + BurrowingHeavenWeakBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(WarmheartedWoodsmanStrong))
        {
            __result = new BackgroundAssets(WarmheartedWoodsmanStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + WarmheartedWoodsmanStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(PriceOfSilenceStrong))
        {
            __result = new BackgroundAssets(PriceOfSilenceStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + PriceOfSilenceStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(BigBirdStrong))
        {
            __result = new BackgroundAssets(BigBirdStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + BigBirdStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(BlueStarStrong))
        {
            __result = new BackgroundAssets(BlueStarStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + BlueStarStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(PunishingBirdStrong))
        {
            __result = new BackgroundAssets(PunishingBirdStrongBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + PunishingBirdStrongBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(JudgementBirdElite))
        {
            __result = new BackgroundAssets(JudgementBirdEliteBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + JudgementBirdEliteBackgroundTitle);
            return false;
        }

        if (encounterType == typeof(RoadHomeElite))
        {
            __result = new BackgroundAssets(RoadHomeEliteBackgroundTitle, rng);
            Log.Info("[GuestReceptionPool] " + encounterType.Name + " dedicated background title: " + RoadHomeEliteBackgroundTitle);
            return false;
        }

        if (!GuestReceptionPoolRegistry.IsGuestEncounterType(encounterType))
        {
            return true;
        }

        __result = new BackgroundAssets(GuestReceptionPoolRegistry.SharedBackgroundTitle, rng);
        string selectedLayerPath = GuestReceptionPoolRegistry.DrawNextLayerScenePath(rng);
        __result.BgLayers.Clear();
        __result.BgLayers.Add(selectedLayerPath);
        Log.Info("[GuestReceptionPool] " + encounterType.Name + " selected layer: " + selectedLayerPath);
        return false;
    }
}
