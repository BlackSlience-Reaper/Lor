using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.content.specialguests.Rnfmabj;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.scene_transitions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.content.specialguests.Xiao;

public sealed class XiaoSpecialGuestEvent : SpecialGuestEventBase
{
    private const string ResonatedWithXiaoResolution =
        "resonated_with_xiao";
    private const string OptionRoot =
        "XIAO_SPECIAL_GUEST_EVENT.pages.INITIAL.options.";

    protected override string GuestDefinitionId => XiaoSpecialGuestIds.Guest;

    protected override string SecondOptionLocKey =>
        OptionRoot + "RESONATE_WITH_XIAO";

    protected override string ThirdOptionLocKey =>
        OptionRoot + "RUN_AWAY";

    protected override int EscapeGoldGain => 88;

    protected override int EscapeGoldReward => 88;

    protected override EventOption CreateCustomSecondOption() =>
        CreateOption(
            SecondOptionLocKey,
            ResonateWithXiao,
            HoverTipFactory.FromCardWithCardHoverTips<
                    XiaoPulaoBellEgoCard>()
                .Concat(HoverTipFactory.FromCardWithCardHoverTips<
                    XiaoYaziVengeanceEgoCard>())
                .Concat(HoverTipFactory.FromCardWithCardHoverTips<
                    XiaoTaotieFeastEgoCard>()));

    protected override LocString? ResolveCustomResolutionDescription(
        string resolution) => resolution switch
    {
        ResonatedWithXiaoResolution => new LocString(
            "events",
            "XIAO_SPECIAL_GUEST_EVENT.pages.RESONATED_WITH_XIAO.description"),
        _ => null,
    };

    public override IEnumerable<string> GetAssetPaths(IRunState runState) =>
        base.GetAssetPaths(runState)
            .Concat(
            [
                ImageHelper.GetImagePath(
                    XiaoPulaoBellEgoCard.PortraitAssetPath),
                ImageHelper.GetImagePath(
                    XiaoYaziVengeanceEgoCard.PortraitAssetPath),
                ImageHelper.GetImagePath(
                    XiaoTaotieFeastEgoCard.PortraitAssetPath)
            ])
            .Distinct(StringComparer.Ordinal);

    private async Task ResonateWithXiao()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        CardModel[] options =
        [
            owner.RunState.CreateCard<XiaoPulaoBellEgoCard>(owner),
            owner.RunState.CreateCard<XiaoYaziVengeanceEgoCard>(owner),
            owner.RunState.CreateCard<XiaoTaotieFeastEgoCard>(owner)
        ];
        foreach (CardModel option in options)
        {
            SaveManager.Instance.MarkCardAsSeen(option);
        }

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            owner,
            canSkip: true);
        if (chosen == null)
        {
            return;
        }

        SaveManager.Instance.MarkCardAsSeen(chosen);
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.Add(chosen, PileType.Deck));
        await FinishWithoutReceptionAsync(
            ResonatedWithXiaoResolution,
            new LocString(
                "events",
                "XIAO_SPECIAL_GUEST_EVENT.pages.RESONATED_WITH_XIAO.description"));
    }
}

public static class XiaoSpecialGuestRegistration
{
    private const string FirstStoryId = "XIAO_SPECIAL_GUEST_STORY_ONE";
    private const string SecondStoryId = "XIAO_SPECIAL_GUEST_STORY_TWO";
    private static readonly SpecialGuestStoryArtLayout SecondStoryArtLayout =
        new(Scale: 1.42f, OffsetX: 180f, OffsetY: -270f);
    private static bool _initialized;

    private static readonly string[] FirstStoryVoiceNames =
    [
        "53", "54", "55", "56", "57", "58-01", "59", "60", "61", "62",
        "63-01", "64", "65", "66", "67", "68", "69", "70", "71-01", "72",
        "73", "74-01", "75", "76-01", "77", "78-01",
    ];

    private static readonly string[] SecondStoryVoiceNames =
        ["79-03", "80-01", "81-01", "82-03"];

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }
        SpecialGuestStorySequence firstStory = CreateFirstStory();
        SpecialGuestStorySequence secondStory = CreateSecondStory();

        SpecialGuestRegistry.Register(
            new SpecialGuestDefinition(
                XiaoSpecialGuestIds.Guest,
                new LocString("monsters", "XIAO_STAGE_ONE.name"),
                IsUnlocked,
                static () => ModelDb.Event<XiaoSpecialGuestEvent>(),
                [
                    new SpecialGuestStageDefinition(
                        static () => ModelDb.Encounter<XiaoSpecialGuestStageOneEncounter>(),
                        AfterVictoryStory: firstStory,
                        BeforeVictoryStorySetup: static _ =>
                        {
                            XiaoSpecialGuestBgmController.Stop();
                            return Task.CompletedTask;
                        }),
                    new SpecialGuestStageDefinition(
                        static () => ModelDb.Encounter<XiaoSpecialGuestStageTwoEncounter>(),
                        BeforeCombatStory: secondStory,
                        RewardAugmenter: XiaoEmptyRewardAugmenter.Instance,
                        BeforeCombatStorySetup: static _ =>
                            XiaoSpecialGuestPresentation.WaitForStageTwoStoryAsync(),
                        AfterCombatStoryRelease: static _ =>
                            XiaoSpecialGuestBackgroundController.RevealStageTwoAsync(),
                        ExtraAssetPaths: LorexSceneTransitionAssetPaths.All
                            .Append(XiaoSpecialGuestIds.StageTwoBackground)
                            .Append(XiaoSpecialGuestIds.IronLotusBgm)
                            .Concat(secondStory.GetAssetPaths())
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()),
                ],
                AvailabilityCondition: IsThirdAct));
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
            SpecialGuestReceptionProgress.WasSuccessfullyReceived(
                runState,
                KaliSpecialGuestIds.Guest))
        && runState.ActFloor >= 3
        && HasNotEncounteredRnfmabj(runState);

    private static bool HasNotEncounteredRnfmabj(IRunState runState)
    {
        return runState is RunState concreteRunState 
            && !concreteRunState.VisitedEventIds.Contains(ModelDb.Event<RnfmabjSpecialGuestEvent>().Id);
    }

    internal static bool MeetsUnlockContract(
        int actIndex,
        IReadOnlyList<int> pageRelicCounts,
        bool kaliSuccessfullyReceived) =>
        actIndex == 2
        && pageRelicCounts.Count > 0
        && pageRelicCounts.All(static count => count >= 10)
        || kaliSuccessfullyReceived;

    private static bool IsThirdAct(IRunState runState) =>
        runState.CurrentActIndex == 2;

    private static SpecialGuestStorySequence CreateFirstStory()
    {
        var lines = new List<SpecialGuestStoryLine>(26);
        for (int index = 0; index < FirstStoryVoiceNames.Length; index++)
        {
            string art = GetFirstStoryArt(index);
            lines.Add(
                new SpecialGuestStoryLine(
                    new LocString("events", $"{FirstStoryId}.{index}"),
                    Speaker: new LocString("monsters", "XIAO_STAGE_ONE.name"),
                    CgTexturePath: art,
                    VoicePath: XiaoSpecialGuestIds.VoiceRoot
                               + "ch6_Liu_Sec1_ep2_Xiao_"
                               + FirstStoryVoiceNames[index]
                               + ".ogg"));
        }
        return new SpecialGuestStorySequence(FirstStoryId, lines);
    }

    private static SpecialGuestStorySequence CreateSecondStory()
    {
        string art = XiaoSpecialGuestIds.StoryRoot
                     + "xiao_ego_3_body_ego_2.png";
        var lines = new List<SpecialGuestStoryLine>(4);
        for (int index = 0; index < SecondStoryVoiceNames.Length; index++)
        {
            lines.Add(
                new SpecialGuestStoryLine(
                    new LocString("events", $"{SecondStoryId}.{index}"),
                    Speaker: new LocString("monsters", "XIAO_EGO.name"),
                    CgTexturePath: art,
                    VoicePath: XiaoSpecialGuestIds.VoiceRoot
                               + "ch6_Liu_Sec1_ep2_Xiao_"
                               + SecondStoryVoiceNames[index]
                               + ".ogg",
                    ArtLayout: SecondStoryArtLayout));
        }
        return new SpecialGuestStorySequence(SecondStoryId, lines);
    }

    private static string GetFirstStoryArt(int lineIndex)
    {
        string expression = lineIndex switch
        {
            >= 25 => "normal_2",
            >= 23 => "inner_peace",
            >= 22 => "dan_ho",
            >= 18 => "normal_2",
            >= 16 => "dan_ho",
            >= 15 => "inner_peace",
            >= 13 => "small_angry",
            >= 11 => "inner_peace",
            >= 7 => "normal_2",
            >= 4 => "dan_ho",
            _ => "inner_peace",
        };
        return XiaoSpecialGuestIds.StoryRoot
               + "xiao_ego_1_"
               + expression
               + ".png";
    }

    private sealed class XiaoEmptyRewardAugmenter : ISpecialGuestRewardAugmenter
    {
        public static XiaoEmptyRewardAugmenter Instance { get; } = new();

        public Task AugmentAsync(
            SpecialGuestStageContext context,
            RewardsSet rewards)
        {
            _ = context;
            _ = rewards;
            return Task.CompletedTask;
        }
    }
}
