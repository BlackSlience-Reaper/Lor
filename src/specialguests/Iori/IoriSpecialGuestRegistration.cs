using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.relics;
using LibraryOfRuina.specialguests.Kali;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.specialguests.Iori;

public static class IoriSpecialGuestRegistration
{
    private const int RequiredActIndex = 2;
    private const int RequiredActFloor = 3;
    private const int RequiredPageRelicCount = 10;
    private const int SourceStoryLineCount = 160;
    private const int PlayedStoryLineCount = 104;
    private const string StoryId = "IORI_SPECIAL_GUEST_STORY";
    private const string EventImage =
        "res://images/events/iori_special_guest_event.png";
    private const string StoryImageRoot =
        "res://images/special_guests/iori/story/";
    private const string StoryAudioRoot =
        "res://audio/special_guests/iori/story/";
    private const string VoiceRoot = StoryAudioRoot + "voices/";
    private const string BackgroundRoot = StoryImageRoot + "backgrounds/";
    private const string CharacterRoot = StoryImageRoot + "characters/";
    private const string BgmRoot = StoryAudioRoot + "bgm/";
    private const string SfxRoot = StoryAudioRoot + "sfx/";
    private static bool _initialized;

    private static readonly string[] VoiceNames =
    [
        "ch6_Puple_Tear_ep1_Iori_1",
        "ch6_Puple_Tear_ep1_Argalia_1",
        "ch6_Puple_Tear_ep1_Argalia_2",
        "ch6_Puple_Tear_ep1_Iori_2",
        "ch6_Puple_Tear_ep1_Argalia_3",
        "ch6_Puple_Tear_ep1_Iori_3",
        "ch6_Puple_Tear_ep1_Argalia_4",
        "ch6_Puple_Tear_ep1_Argalia_5",
        "ch6_Puple_Tear_ep1_Argalia_6",
        "ch6_Puple_Tear_ep1_Iori_4-01",
        "ch6_Puple_Tear_ep1_Iori_5-01",
        "ch6_Puple_Tear_ep1_Argalia_7",
        "ch6_Puple_Tear_ep1_Argalia_8",
        "ch6_Puple_Tear_ep1_Argalia_9",
        "ch6_Puple_Tear_ep1_Iori_6",
        "ch6_Puple_Tear_ep1_Argalia_10",
        "ch6_Puple_Tear_ep1_Argalia_11-01",
        "ch6_Puple_Tear_ep1_Argalia_12",
        "ch6_Puple_Tear_ep1_Argalia_13-02",
        "ch6_Puple_Tear_ep1_Iori_7",
        "ch6_Puple_Tear_ep1_Argalia_14-01",
        "ch6_Puple_Tear_ep1_Argalia_15",
        "ch6_Puple_Tear_ep1_Argalia_16-01",
        "ch6_Puple_Tear_ep1_Argalia_17-01",
        "ch6_Puple_Tear_ep1_Argalia_18-01",
        "ch6_Puple_Tear_ep1_Argalia_19-01",
        "ch6_Puple_Tear_ep1_Argalia_20-01",
        "ch6_Puple_Tear_ep1_Argalia_21-01",
        "ch6_Puple_Tear_ep1_Argalia_22-01",
        "ch6_Puple_Tear_ep1_Argalia_23-01",
        "ch6_Puple_Tear_ep1_Argalia_24",
        "ch6_Puple_Tear_ep1_Iori_8",
        "ch6_Puple_Tear_ep1_Argalia_25-03",
        "ch6_Puple_Tear_ep1_Argalia_26-01",
        "ch6_Puple_Tear_ep1_Argalia_27-01",
        "ch6_Puple_Tear_ep1_Argalia_28",
        "ch6_Puple_Tear_ep1_Argalia_29-02",
        "ch6_Puple_Tear_ep1_Argalia_30",
        "ch6_Puple_Tear_ep1_Argalia_31-01",
        "ch6_Puple_Tear_ep1_Iori_9",
        "ch6_Puple_Tear_ep1_Iori_10",
        "ch6_Puple_Tear_ep1_Argalia_32",
        "ch6_Puple_Tear_ep1_Argalia_33",
        "ch6_Puple_Tear_ep1_Argalia_34",
        "ch6_Puple_Tear_ep1_Argalia_35",
        "ch6_Puple_Tear_ep1_Argalia_36",
        "ch6_Puple_Tear_ep1_Argalia_37",
        "ch6_Puple_Tear_ep1_Argalia_38",
        "ch6_Puple_Tear_ep1_Argalia_39",
        "ch6_Puple_Tear_ep1_Iori_11",
        "ch6_Puple_Tear_ep1_Argalia_40",
        "ch6_Puple_Tear_ep1_Argalia_41",
        "ch6_Puple_Tear_ep1_Argalia_42",
        "ch6_Puple_Tear_ep1_Argalia_43",
        "ch6_Puple_Tear_ep1_Iori_12",
        "ch6_Puple_Tear_ep1_Argalia_44",
        "ch6_Puple_Tear_ep1_Argalia_45",
        "ch6_Puple_Tear_ep1_Argalia_46",
        "ch6_Puple_Tear_ep1_Iori_13",
        "ch6_Puple_Tear_ep1_Iori_14",
        "ch6_Puple_Tear_ep1_Argalia_47-02",
        "ch6_Puple_Tear_ep1_Argalia_48",
        "ch6_Puple_Tear_ep1_Argalia_49",
        "ch6_Puple_Tear_ep1_Iori_15",
        "ch6_Puple_Tear_ep1_Argalia_50",
        "ch6_Puple_Tear_ep1_Iori_16-01",
        "ch6_Puple_Tear_ep1_Iori_17-01",
        "ch6_Puple_Tear_ep1_Argalia_51-01",
        "ch6_Puple_Tear_ep1_Argalia_52",
        "ch6_Puple_Tear_ep1_Argalia_53",
        "ch6_Puple_Tear_ep1_Iori_18-02",
        "ch6_Puple_Tear_ep1_Argalia_54",
        "ch6_Puple_Tear_ep1_Argalia_55-01",
        "ch6_Puple_Tear_ep1_Iori_19-01",
        "ch6_Puple_Tear_ep1_Argalia_56",
        "ch6_Puple_Tear_ep1_Argalia_57",
        "ch6_Puple_Tear_ep1_Pluto_1-01",
        "ch6_Puple_Tear_ep1_Iori_20",
        "ch6_Puple_Tear_ep1_Eilin_1-01",
        "ch6_Puple_Tear_ep1_Iori_21",
        "ch6_Puple_Tear_ep1_Tanya_1_b-03",
        "ch6_Puple_Tear_ep1_Iori_22",
        "ch6_Puple_Tear_ep1_Philip_1",
        "ch6_Puple_Tear_ep1_Iori_23",
        "ch6_Puple_Tear_ep1_Oswald_1",
        "ch6_Puple_Tear_ep1_Iori_24",
        "ch6_Puple_Tear_ep1_Breamen_1",
        "ch6_Puple_Tear_ep1_Iori_25-01",
        "ch6_Puple_Tear_ep1_Elena_1-03",
        "ch6_Puple_Tear_ep1_Iori_26",
        "ch6_Puple_Tear_ep1_Greta_1-01",
        "ch6_Puple_Tear_ep1_Iori_27",
        "ch6_Puple_Tear_ep1_Puppeteer_1-03",
        "ch6_Puple_Tear_ep1_Iori_28-01",
        "ch6_Puple_Tear_ep1_Argalia_58-03",
        "ch6_Puple_Tear_ep1_Iori_29",
        "ch6_Puple_Tear_ep1_Iori_30-01",
        "ch6_Puple_Tear_ep1_Argalia_59-03",
        "ch6_Puple_Tear_ep1_Iori_31",
        "ch6_Puple_Tear_ep1_Eilin_2_B-01",
        "ch6_Puple_Tear_ep1_Tanya_2-02",
        "ch6_Puple_Tear_ep1_Argalia_60-06",
        "ch6_Puple_Tear_ep1_Iori_32-03",
        "ch6_Puple_Tear_ep1_Argalia_61-03",
        "ch6_Puple_Tear_ep1_Roland_1-02",
        "ch6_Puple_Tear_ep1_Roland_2-01",
        "ch6_Puple_Tear_ep1_Angela_1",
        "ch6_Puple_Tear_ep1_Roland_3",
        "ch6_Puple_Tear_ep1_Angela_2",
        "ch6_Puple_Tear_ep1_Angela_3",
        "ch6_Puple_Tear_ep1_Roland_4",
        "ch6_Puple_Tear_ep1_Angela_4",
        "ch6_Puple_Tear_ep1_Angela_5",
        "ch6_Puple_Tear_ep1_Roland_5",
        "ch6_Puple_Tear_ep1_Angela_6",
        "ch6_Puple_Tear_ep1_Roland_6",
        "ch6_Puple_Tear_ep1_Angela_7-01",
        "ch6_Puple_Tear_ep1_Angela_8",
        "ch6_Puple_Tear_ep1_Angela_9",
        "ch6_Puple_Tear_ep1_Angela_10",
        "ch6_Puple_Tear_ep1_Angela_11-01",
        "ch6_Puple_Tear_ep1_Angela_12",
        "ch6_Puple_Tear_ep1_Angela_13",
        "ch6_Puple_Tear_ep1_Roland_7",
        "ch6_Puple_Tear_ep1_Angela_14",
        "ch6_Puple_Tear_ep1_Iori_33-01",
        "ch6_Puple_Tear_ep1_Iori_34",
        "ch6_Puple_Tear_ep1_Iori_35",
        "ch6_Puple_Tear_ep1_Angela_15-01",
        "ch6_Puple_Tear_ep1_Iori_36-02",
        "ch6_Puple_Tear_ep1_Iori_37-01",
        "ch6_Puple_Tear_ep1_Angela_16",
        "ch6_Puple_Tear_ep1_Iori_38",
        "ch6_Puple_Tear_ep1_Iori_39-02",
        "ch6_Puple_Tear_ep1_Angela_17",
        "ch6_Puple_Tear_ep1_Iori_40-01",
        "ch6_Puple_Tear_ep1_Angela_18",
        "ch6_Puple_Tear_ep1_Iori_41",
        "ch6_Puple_Tear_ep1_Angela_19",
        "ch6_Puple_Tear_ep1_Iori_42",
        "ch6_Puple_Tear_ep1_Angela_20",
        "ch6_Puple_Tear_ep1_Iori_43-01",
        "ch6_Puple_Tear_ep1_Iori_44",
        "ch6_Puple_Tear_ep1_Iori_45",
        "ch6_Puple_Tear_ep1_Angela_21",
        "ch6_Puple_Tear_ep1_Iori_46",
        "ch6_Puple_Tear_ep1_Iori_47-01",
        "ch6_Puple_Tear_ep1_Iori_48-01",
        "ch6_Puple_Tear_ep1_Angela_22",
        "ch6_Puple_Tear_ep1_Iori_49",
        "ch6_Puple_Tear_ep1_Iori_50",
        "ch6_Puple_Tear_ep1_Angela_23",
        "ch6_Puple_Tear_ep1_Iori_51",
        "ch6_Puple_Tear_ep1_Iori_52-01",
        "ch6_Puple_Tear_ep1_Angela_24",
        "ch6_Puple_Tear_ep1_Iori_53",
        "ch6_Puple_Tear_ep1_Iori_54",
        "ch6_Puple_Tear_ep1_Iori_55",
        "ch6_Puple_Tear_ep1_Iori_56",
        "ch6_Puple_Tear_ep1_Iori_57",
    ];

    // The source effect table changes expression state less often than it
    // advances dialogue. Equal flattened layers share the first line's file,
    // preserving the complete 160-line mapping without shipping duplicates.
    private static readonly int[] CharacterStateIndices =
    [
        0, 1, 1, 3, 4, 5, 6, 6, 8, 9, 9, 11, 11, 11, 14, 15,
        15, 17, 17, 19, 20, 20, 20, 20, 24, 25, 25, 25, 25, 25, 25, 31,
        6, 6, 34, 35, 35, 35, 34, 31, 31, 1, 6, 43, 43, 43, 8, 8,
        8, 49, 50, 50, 15, 15, 54, 55, 56, 24, 58, 58, 60, 60, 60, 63,
        64, 65, 66, 67, 67, 67, 70, 20, 72, 73, 74, 74, 76, 76, 76, 76,
        76, 76, 76, 76, 76, 76, 76, 76, 76, 76, 76, 76, 76, 76, 76, 76,
        76, 76, 76, 76, 76, 76, 76, 76, 104, 104, 106, 107, 108, 108, 110, 111,
        111, 113, 114, 115, 116, 116, 116, 116, 116, 116, 122, 107, 124, 125, 125, 125,
        128, 129, 129, 131, 132, 132, 134, 135, 134, 137, 138, 139, 140, 141, 142, 142,
        144, 145, 145, 145, 148, 149, 149, 151, 152, 152, 154, 155, 155, 155, 155, 159,
    ];

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        SpecialGuestRegistry.Register(
            new SpecialGuestDefinition(
                IoriSpecialGuestIds.Guest,
                new LocString("events", "IORI_SPECIAL_GUEST.name"),
                IsUnlocked,
                static () => ModelDb.Event<IoriSpecialGuestEvent>(),
                [
                    new SpecialGuestStageDefinition(
                        static () => ModelDb.Encounter<
                            IoriSpecialGuestStageOneEncounter>(),
                        BeforeCombatStory: CreateBeforeCombatStory(),
                        BeforeCombatStorySetup: PauseCombatBgmForStory,
                        AfterCombatStoryRelease: ResumeCombatBgmAfterStory,
                        ExtraAssetPaths: [EventImage]),
                    new SpecialGuestStageDefinition(
                        static () => ModelDb.Encounter<
                            IoriSpecialGuestStageTwoEncounter>(),
                        ExtraAssetPaths: [EventImage]),
                ],
                AvailabilityCondition: CanAppear));
        _initialized = true;
    }

    [SpecialGuestRegistration]
    public static void Register() => Initialize();

    internal static bool MeetsUnlockContract(
        int actIndex,
        IReadOnlyList<int> pageRelicCounts,
        bool kaliSuccessfullyReceived,
        bool secondActFullyLiberated) =>
        actIndex == RequiredActIndex
        && kaliSuccessfullyReceived
        && pageRelicCounts.Count > 0
        && pageRelicCounts.All(
            static count => count >= RequiredPageRelicCount)
        || secondActFullyLiberated;

    private static bool IsUnlocked(IRunState runState) =>
        MeetsUnlockContract(
            runState.CurrentActIndex,
            runState.Players
                .Select(static player => player.Relics.Count(
                    AbnormalityPageRewardPreselection.IsPageRelic))
                .ToArray(),
            SpecialGuestReceptionProgress.WasSuccessfullyReceived(
                runState,
                KaliSpecialGuestIds.Guest),
            LiberationFloorIds.SecondAct.All(
                floorId => FloorLiberationProgress.IsFullyLiberated(
                    runState,
                    floorId)));

    private static bool CanAppear(IRunState runState) =>
        runState.CurrentActIndex == RequiredActIndex
        && runState.ActFloor >= RequiredActFloor;

    private static SpecialGuestStorySequence CreateBeforeCombatStory()
    {
        if (VoiceNames.Length != SourceStoryLineCount
            || CharacterStateIndices.Length != VoiceNames.Length)
        {
            throw new InvalidOperationException(
                $"Iori's source story must map exactly {SourceStoryLineCount} lines.");
        }

        // End after source line 103: "……你真的听不见那人美妙的声音吗？"
        var lines = new List<SpecialGuestStoryLine>(PlayedStoryLineCount);
        for (int index = 0; index < PlayedStoryLineCount; index++)
        {
            string voice = VoiceNames[index];
            lines.Add(
                new SpecialGuestStoryLine(
                    new LocString("events", $"{StoryId}.{index}"),
                    Speaker: new LocString(
                        "events",
                        StoryId + ".speakers." + GetSpeakerId(index, voice)),
                    CgTexturePath: GetBackgroundPath(index),
                    ExpressionTexturePath:
                        CharacterRoot
                        + $"state_{CharacterStateIndices[index]:D3}.png",
                    VoicePath: VoiceRoot + voice + ".ogg",
                    SoundEffectPath: GetSoundEffectPath(index),
                    BgmCue: GetBgmCue(index)));
        }

        return new SpecialGuestStorySequence(
            StoryId,
            lines,
            HasBackgroundMusic: true);
    }

    private static string GetBackgroundPath(int index) => index switch
    {
        <= 75 => BackgroundRoot + "ch6_PupleTear.png",
        <= 97 => BackgroundRoot + "ch6_PupleTear_1.png",
        <= 103 => BackgroundRoot + "ch6_PupleTear_2.png",
        <= 124 => BackgroundRoot + "ch1_library_inside.png",
        _ => BackgroundRoot + "ch1_library_entry2.png",
    };

    private static string GetSpeakerId(int lineIndex, string voiceName)
    {
        // The official Simplified Chinese script spells Tanya differently on
        // line 100.  Preserve that line-level teller instead of flattening it
        // into the shared character name used on line 80.
        if (lineIndex == 100)
        {
            return "TanyaLine100";
        }

        string[] speakerIds =
        [
            "Iori", "Argalia", "Roland", "Angela", "Greta", "Breamen",
            "Eilin", "Elena", "Oswald", "Puppeteer", "Tanya", "Pluto",
            "Philip",
        ];
        return speakerIds.First(
            id => voiceName.Contains(
                "_" + id + "_",
                StringComparison.Ordinal));
    }

    private static string? GetSoundEffectPath(int index) => index switch
    {
        0 => SfxRoot + "ch8_YesodWalk.ogg",
        128 => SfxRoot + "ch1_FingerSnap_128.ogg",
        155 => SfxRoot + "ch1_FingerSnap_155.ogg",
        _ => null,
    };

    private static SpecialGuestStoryBgmCue? GetBgmCue(int index) => index switch
    {
        0 => new SpecialGuestStoryBgmCue(
            SpecialGuestStoryBgmAction.FadeIn,
            BgmRoot + "ch1_Serious.ogg",
            FadeSeconds: 0f),
        76 => new SpecialGuestStoryBgmCue(
            SpecialGuestStoryBgmAction.FadeIn,
            BgmRoot + "ch1_Nervous.ogg",
            FadeSeconds: 1f),
        104 => new SpecialGuestStoryBgmCue(
            SpecialGuestStoryBgmAction.FadeIn,
            BgmRoot + "Lobby.ogg",
            FadeSeconds: 1f),
        125 => new SpecialGuestStoryBgmCue(
            SpecialGuestStoryBgmAction.FadeIn,
            BgmRoot + "Bgm3.ogg",
            FadeSeconds: 1f),
        _ => null,
    };

    private static Task PauseCombatBgmForStory(
        SpecialGuestStageContext context)
    {
        if (!context.State.IsStoryCompleted(StoryId))
        {
            EncounterBgmController.StopRuntimeSession();
            IoriSpecialGuestBgmController.Stop();
        }

        return Task.CompletedTask;
    }

    private static Task ResumeCombatBgmAfterStory(
        SpecialGuestStageContext context)
    {
        if (!RunManager.Instance.IsInProgress
            || !CombatManager.Instance.IsInProgress
            || !ReferenceEquals(
                CombatManager.Instance.DebugOnlyGetState(),
                context.Room.CombatState))
        {
            return Task.CompletedTask;
        }

        IoriSpecialGuestBgmController.Ensure(context.Room.CombatState);

        return Task.CompletedTask;
    }
}
