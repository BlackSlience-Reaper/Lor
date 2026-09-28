using System;
using System.Linq;
using LibraryOfRuina.encounters.AddictedEmployee;
using LibraryOfRuina.encounters.AllAroundHelper;
using LibraryOfRuina.encounters.CosmicFragment;
using LibraryOfRuina.encounters.DeadButterfly;
using LibraryOfRuina.encounters.FairyFestival;
using LibraryOfRuina.encounters.ForsakenMurderer;
using LibraryOfRuina.encounters.FuneralOfTheDeadButterflies;
using LibraryOfRuina.encounters.HappyTeddy;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.Leticia;
using LibraryOfRuina.encounters.LittleRedMercenary;
using LibraryOfRuina.encounters.QueenOfHatred;
using LibraryOfRuina.encounters.RedShoes;
using LibraryOfRuina.encounters.ScorchedGirl;
using LibraryOfRuina.encounters.SpiderBud;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.encounters.TodaysShyLook;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.features.ftue;

/// <summary>
/// Helper to check whether FTUE popups should be shown.
/// Centralizes all precondition checks.
/// </summary>
public static class FtueGuard
{
    private const char ShownFtueIdSeparator = ';';

    private static readonly HashSet<string> AbnormalityEncounterNames = new(StringComparer.Ordinal)
    {
        nameof(AddictedEmployeeStrong),
        nameof(AddictedEmployeeWeak),
        nameof(AllAroundHelperStrong),
        nameof(AllAroundHelperWeak),
        nameof(CosmicFragmentWeak),
        nameof(DeadButterflyStrong),
        nameof(DeadButterflyWeak),
        nameof(FairyFestivalStrong),
        nameof(ForsakenMurdererWeak),
        nameof(FuneralOfTheDeadButterfliesEncounter),
        nameof(HappyTeddyWeak),
        nameof(LeticiaElite),
        nameof(LittleRedMercenaryElite),
        nameof(QueenOfHatredStrong),
        nameof(RedShoesStrong),
        nameof(ScorchedGirl),
        nameof(SpiderBudStrong),
        nameof(TodaysShyLookStrong)
    };

    /// <summary>
    /// Returns true if all preconditions for showing a LoR mod FTUE are met:
    /// 1. The mod's monster extension is enabled
    /// 2. The mod tutorial toggle is currently enabled
    /// </summary>
    public static bool ShouldShow(string ftueId)
    {
        if (!LibraryOfRuinaSettings.MonsterExtensionActive)
            return false;

        if (!LibraryOfRuinaSettings.FtueTutorialEnabled)
            return false;

        return !HasBeenShown(ftueId);
    }

    public static bool HasBeenShown(string ftueId)
    {
        return GetShownFtueIds().Contains(ftueId);
    }

    public static bool TryConsumeShowRequest(string ftueId)
    {
        if (!ShouldShow(ftueId))
            return false;

        HashSet<string> shownFtueIds = GetShownFtueIds();
        shownFtueIds.Add(ftueId);
        LibraryOfRuinaSettings.FtueTutorialShownIds =
            string.Join(ShownFtueIdSeparator, shownFtueIds.OrderBy(static id => id, StringComparer.Ordinal));
        ExtSettingsRegistry.Get<LibraryOfRuinaSettings>()?.Save();
        return true;
    }

    private static HashSet<string> GetShownFtueIds()
    {
        return LibraryOfRuinaSettings.FtueTutorialShownIds
            .Split(ShownFtueIdSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Check if a given encounter belongs to the Library of Ruina mod
    /// by checking if its type is defined in our assembly.
    /// </summary>
    public static bool IsLibraryOfRuinaEncounter(EncounterModel? encounter)
    {
        if (encounter == null) return false;
        return encounter.GetType().Assembly == typeof(FtueGuard).Assembly;
    }

    public static bool IsLiberationEncounter(EncounterModel? encounter)
    {
        if (encounter == null || !IsLibraryOfRuinaEncounter(encounter))
            return false;

        return encounter is HistoryFloorLiberationEncounter
            || encounter is TechnologyFloorLiberationEncounter
            || encounter.GetType().Name.Contains("LiberationEncounter", StringComparison.Ordinal);
    }

    public static bool IsAbnormalityEncounter(EncounterModel? encounter)
    {
        if (encounter == null || !IsLibraryOfRuinaEncounter(encounter) || IsLiberationEncounter(encounter))
            return false;

        string encounterName = encounter.GetType().Name;
        if (AbnormalityEncounterNames.Contains(encounterName))
            return true;

        try
        {
            return encounter.AllPossibleMonsters.Any(static monster => monster is LibraryMonsterModel);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsOrdinaryLibraryEnemyEncounter(EncounterModel? encounter)
    {
        return IsLibraryOfRuinaEncounter(encounter)
            && !IsLiberationEncounter(encounter)
            && !IsAbnormalityEncounter(encounter);
    }
}
