using System.Linq;
using MegaCrit.Sts2.Core.Models;

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
}
