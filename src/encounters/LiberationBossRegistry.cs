using System;
using System.Linq;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.PhilosophyFloorLiberation;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.encounters;

/// <summary>
/// 楼层解放的登记开关与查询。开关存放在各层的 <see cref="LiberationFloorDescriptor.Registered"/> 上，
/// 这里的属性只是按层转发，保留给按名字读写开关的调用方。
/// </summary>
internal static class LiberationBossRegistry
{
    public static bool RegisterNaturalFloorLiberation
    {
        get => LiberationFloors.Natural.Registered;
        set => LiberationFloors.Natural.Registered = value;
    }

    /// <summary>
    /// If false, Art floor liberation encounter will not be added to any boss pool.
    /// </summary>
    public static bool RegisterArtFloorLiberation
    {
        get => LiberationFloors.Art.Registered;
        set => LiberationFloors.Art.Registered = value;
    }

    /// <summary>
    /// If false, Technology floor liberation encounter will not be added to any boss pool.
    /// </summary>
    public static bool RegisterTechnologyFloorLiberation
    {
        get => LiberationFloors.Technology.Registered;
        set => LiberationFloors.Technology.Registered = value;
    }

    /// <summary>
    /// If false, History floor liberation encounter will not be added to any boss pool.
    /// </summary>
    public static bool RegisterHistoryFloorLiberation
    {
        get => LiberationFloors.History.Registered;
        set => LiberationFloors.History.Registered = value;
    }

    /// <summary>
    /// If false, Literature floor liberation encounter will not be added to any boss pool.
    /// </summary>
    public static bool RegisterLiteratureFloorLiberation
    {
        get => LiberationFloors.Literature.Registered;
        set => LiberationFloors.Literature.Registered = value;
    }

    /// <summary>
    /// If false, Language floor liberation encounter will not be added to any boss pool.
    /// Phase one remains available through direct debug entry.
    /// </summary>
    public static bool RegisterLanguageFloorLiberation
    {
        get => LiberationFloors.Language.Registered;
        set => LiberationFloors.Language.Registered = value;
    }

    /// <summary>
    /// If false, Philosophy floor liberation encounter will not be added to any boss pool.
    /// </summary>
    public static bool RegisterPhilosophyFloorLiberation
    {
        get => LiberationFloors.Philosophy.Registered;
        set => LiberationFloors.Philosophy.Registered = value;
    }

    /// <summary>
    /// If false, Social floor liberation encounter will not be added to any boss pool.
    /// </summary>
    public static bool RegisterSocialFloorLiberation
    {
        get => LiberationFloors.Social.Registered;
        set => LiberationFloors.Social.Registered = value;
    }

    /// <summary>
    /// True if at least one liberation floor encounter is registered for boss pools.
    /// </summary>
    public static bool AnyLiberationRegistered =>
        LiberationFloors.All.Any(static descriptor => descriptor.Registered);

    public static bool IsLiberationEncounter(EncounterModel encounter) =>
        LiberationFloors.ForEncounter(encounter) != null;

    public static bool IsEncounterRegistered(EncounterModel encounter) =>
        LiberationFloors.ForEncounter(encounter)?.Registered ?? false;

    public static bool IsEncounterRegistered<T>()
        where T : EncounterModel =>
        LiberationFloors.ForEncounterType(typeof(T))?.Registered ?? false;

    /// <summary>
    /// Randomly chooses between registered floor liberation encounters.
    /// Respects per-floor registration flags; if only one is registered, always returns that one.
    /// Throws if no included floor is registered.
    /// </summary>
    public static EncounterModel ChooseRandomLiberationEncounter(ulong seed, string label)
    {
        return TryChooseRandomLiberationEncounter(
                seed,
                label,
                includeTechnology: true,
                includeHistory: true,
                includeArt: true,
                includeLanguage: true,
                includePhilosophy: true,
                includeSocial: true,
                includeLiterature: true,
                includeNatural: true)
            ?? throw new InvalidOperationException(
                "ChooseRandomLiberationEncounter called but no liberation floor is registered.");
    }

    public static EncounterModel? TryChooseRandomLiberationEncounter(
        ulong seed,
        string label,
        bool includeTechnology,
        bool includeHistory,
        bool includeArt,
        bool includeLanguage = false,
        bool includePhilosophy = false,
        bool includeSocial = false,
        bool includeLiterature = false,
        bool includeNatural = false)
    {
        List<Func<EncounterModel>> candidates = [];
        if (includeTechnology && RegisterTechnologyFloorLiberation)
        {
            candidates.Add(static () => ModelDb.Encounter<TechnologyFloorLiberationEncounter>());
        }

        if (includeHistory && RegisterHistoryFloorLiberation)
        {
            candidates.Add(static () => ModelDb.Encounter<HistoryFloorLiberationEncounter>());
        }

        if (includeLiterature && RegisterLiteratureFloorLiberation)
        {
            candidates.Add(static () =>
                ModelDb.Encounter<LiteratureFloorLiberationEncounter>());
        }

        if (includeArt && RegisterArtFloorLiberation)
        {
            candidates.Add(static () => ModelDb.Encounter<ArtFloorLiberationEncounter>());
        }

        if (includeLanguage && RegisterLanguageFloorLiberation)
        {
            candidates.Add(static () => ModelDb.Encounter<LanguageFloorLiberationEncounter>());
        }

        if (includePhilosophy && RegisterPhilosophyFloorLiberation)
        {
            candidates.Add(static () => ModelDb.Encounter<PhilosophyFloorLiberationEncounter>());
        }

        if (includeSocial && RegisterSocialFloorLiberation)
        {
            candidates.Add(static () => ModelDb.Encounter<SocialFloorLiberationEncounter>());
        }

        if (includeNatural && RegisterNaturalFloorLiberation)
        {
            candidates.Add(static () => ModelDb.Encounter<NaturalFloorLiberationEncounter>());
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0]();
        }

        Rng liberationRng = new(seed, label);
        int index = Math.Clamp((int)(liberationRng.NextDouble() * candidates.Count), 0, candidates.Count - 1);
        return candidates[index]();
    }
}
