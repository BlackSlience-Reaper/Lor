using System.Linq;
using LibraryOfRuina.encounters;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.specialguests.Kali;

public sealed class KaliSpecialGuestEvent : SpecialGuestEventBase
{
    protected override string GuestDefinitionId => KaliSpecialGuestIds.Guest;
}

public static class KaliSpecialGuestRegistration
{
    internal const int RequiredPageRelics = 9;
    internal const int RequiredActIndex = 1;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        SpecialGuestRegistry.Register(
            new SpecialGuestDefinition(
                KaliSpecialGuestIds.Guest,
                new LocString("monsters", "KALI.name"),
                IsUnlocked,
                static () => ModelDb.Event<KaliSpecialGuestEvent>(),
                [
                    new SpecialGuestStageDefinition(
                        static () => ModelDb.Encounter<KaliSpecialGuestEncounter>(),
                        ExtraAssetPaths: [KaliSpecialGuestIds.EventImage])
                ],
                AvailabilityCondition: IsSecondAct));
        _initialized = true;
    }

    [SpecialGuestRegistration]
    public static void Register() => Initialize();

    private static bool IsUnlocked(IRunState runState) =>
        MeetsUnlockContract(
            runState.CurrentActIndex,
            runState.Players
                .Select(static player => player.Relics.Count(
                    AbnormalityPageRewardPreselection.IsPageRelic))
                .ToArray(),
            FloorLiberationProgress.IsAnyFullyLiberated(
                runState,
                LiberationFloorIds.FirstAct))
        && runState.ActFloor >= 6;

    private static bool IsSecondAct(IRunState runState) =>
        runState.CurrentActIndex == RequiredActIndex;

    internal static bool MeetsUnlockContract(
        int actIndex,
        IReadOnlyList<int> pageRelicCounts,
        bool firstActFloorFullyLiberated) =>
        actIndex == RequiredActIndex
        && pageRelicCounts.Count > 0
        && pageRelicCounts.All(static count => count >= RequiredPageRelics)
        || firstActFloorFullyLiberated;
}
