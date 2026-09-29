using LibraryOfRuina.acts;
using LibraryOfRuina.content.abnormalities.AddictedEmployee;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.content.abnormalities.BigBadWolf;
using LibraryOfRuina.content.abnormalities.BigBird;
using LibraryOfRuina.content.abnormalities.BlueStar;
using LibraryOfRuina.content.abnormalities.BurrowingHeaven;
using LibraryOfRuina.content.abnormalities.CosmicFragment;
using LibraryOfRuina.content.abnormalities.DeadButterfly;
using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.content.abnormalities.FairyFestival;
using LibraryOfRuina.content.abnormalities.ForsakenMurderer;
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
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.QueenBee;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.RedShoes;
using LibraryOfRuina.content.abnormalities.RoadHome;
using LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;
using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.content.abnormalities.SmilingBodies;
using LibraryOfRuina.content.abnormalities.SpiderBud;
using LibraryOfRuina.content.abnormalities.SpinyBus;
using LibraryOfRuina.content.abnormalities.TodaysShyLook;
using LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.encounters.BrotherhoodOfIron;
using LibraryOfRuina.encounters.DawnOffice;
using LibraryOfRuina.encounters.HookOffice;
using LibraryOfRuina.encounters.KuroKumo;
using LibraryOfRuina.encounters.MusiciansOfBremen;
using LibraryOfRuina.encounters.WedgeOffice;
using LibraryOfRuina.encounters.YunOffice;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

/// <summary>
/// Encounter pools owned by the six Library acts. The old vanilla-act Harmony injections
/// were removed so Overgrowth, Underdocks, Hive and Glory keep their native encounters.
/// </summary>
internal static class LibraryActEncounterPools
{
    public static IEnumerable<EncounterModel> Build(
        LibraryActFamily family,
        EncounterModel fixedBoss)
    {
        var encounters = new List<EncounterModel>();

        switch (family)
        {
            case LibraryActFamily.First:
                AppendFirstFamily(encounters);
                break;
            case LibraryActFamily.Second:
                AppendSecondFamily(encounters);
                break;
            case LibraryActFamily.Third:
                AppendThirdFamily(encounters);
                break;
        }

        encounters.Add(fixedBoss);
        return encounters;
    }

    private static void AppendFirstFamily(ICollection<EncounterModel> encounters)
    {
        LibraryEncounterWeighting.AddWeightedCopies<LeticiaElite>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<ScorchedGirl>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<FinnWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<HappyTeddyWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<ForsakenMurdererWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<AddictedEmployeeWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<AddictedEmployeeStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<TodaysShyLookStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<BrotherhoodOfIronStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<FairyFestivalStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<RedShoesStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<SpiderBudStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<DeadButterflyWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<DeadButterflyStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<YunOfficeNormal>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<HookOfficeStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<AllAroundHelperWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<AllAroundHelperStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<QueenBeeElite>(encounters);
    }

    private static void AppendSecondFamily(ICollection<EncounterModel> encounters)
    {
        LibraryEncounterWeighting.AddWeightedCopies<QueenOfHatredStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<KingOfGreedElite>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<LittleRedMercenaryElite>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<NosferatuElite>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<CosmicFragmentWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<GalaxyChildWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<SpinyBusWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<BigBadWolfWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<SmilingBodiesStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<DespairKnightStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<WrathServantStrong>(encounters);
    }

    private static void AppendThirdFamily(ICollection<EncounterModel> encounters)
    {
        LibraryEncounterWeighting.AddWeightedCopies<KuroKumoNormal>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<MusiciansOfBremenNormal>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<DawnOfficeNormal>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<WedgeOfficeNormal>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<BurrowingHeavenWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<ScarecrowSearchingForWisdomWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<HeartOfAspirationWeak>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<WarmheartedWoodsmanStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<PriceOfSilenceStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<BlueStarStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<BigBirdStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<PunishingBirdStrong>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<JudgementBirdElite>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<RoadHomeElite>(encounters);
        LibraryEncounterWeighting.AddWeightedCopies<OzmaElite>(encounters);
    }
}
