using LibraryOfRuina.acts;
using LibraryOfRuina.encounters.AddictedEmployee;
using LibraryOfRuina.encounters.AllAroundHelper;
using LibraryOfRuina.encounters.BigBadWolf;
using LibraryOfRuina.encounters.BigBird;
using LibraryOfRuina.encounters.BlueStar;
using LibraryOfRuina.encounters.BrotherhoodOfIron;
using LibraryOfRuina.encounters.BurrowingHeaven;
using LibraryOfRuina.encounters.CosmicFragment;
using LibraryOfRuina.encounters.DawnOffice;
using LibraryOfRuina.encounters.DeadButterfly;
using LibraryOfRuina.encounters.DespairKnight;
using LibraryOfRuina.encounters.FairyFestival;
using LibraryOfRuina.encounters.ForsakenMurderer;
using LibraryOfRuina.encounters.GalaxyChild;
using LibraryOfRuina.encounters.HappyTeddy;
using LibraryOfRuina.encounters.HeartOfAspiration;
using LibraryOfRuina.encounters.HookOffice;
using LibraryOfRuina.encounters.JudgementBird;
using LibraryOfRuina.encounters.KingOfGreed;
using LibraryOfRuina.encounters.KuroKumo;
using LibraryOfRuina.encounters.Leticia;
using LibraryOfRuina.encounters.LittleRedMercenary;
using LibraryOfRuina.encounters.MusiciansOfBremen;
using LibraryOfRuina.encounters.Nosferatu;
using LibraryOfRuina.encounters.Ozma;
using LibraryOfRuina.encounters.PriceOfSilence;
using LibraryOfRuina.encounters.PunishingBird;
using LibraryOfRuina.encounters.QueenBee;
using LibraryOfRuina.encounters.QueenOfHatred;
using LibraryOfRuina.encounters.RedShoes;
using LibraryOfRuina.encounters.RoadHome;
using LibraryOfRuina.encounters.ScarecrowSearchingForWisdom;
using LibraryOfRuina.encounters.ScorchedGirl;
using LibraryOfRuina.encounters.SmilingBodies;
using LibraryOfRuina.encounters.SpiderBud;
using LibraryOfRuina.encounters.SpinyBus;
using LibraryOfRuina.encounters.TodaysShyLook;
using LibraryOfRuina.encounters.WarmheartedWoodsman;
using LibraryOfRuina.encounters.WedgeOffice;
using LibraryOfRuina.encounters.WrathServant;
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
