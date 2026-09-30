using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.specialguests.Xiao;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

public sealed class RnfmabjSpecialGuestEvent : SpecialGuestEventBase
{
    private const string TakeDirectiveResolution = "took_directive";
    private const string FledWithGuiltResolution = "fled_with_guilt";
    private const string OptionRoot =
        "RNFMABJ_SPECIAL_GUEST_EVENT.pages.INITIAL.options.";

    private const int HplossPercentage = 20;
    private const string HpLossVarName = "Hploss";

    protected override string GuestDefinitionId => RnfmabjSpecialGuestIds.Guest;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        .. base.CanonicalVars,
        new HpLossVar(HpLossVarName, 0m)
    ];

    public override void CalculateVars()
    {
        base.CalculateVars();
        var maxHp = Owner!.Creature.MaxHp;
        var hploss = Math.Max(0, (maxHp * HplossPercentage / 100));
        DynamicVars[HpLossVarName].BaseValue = hploss;
    }

    protected override string SecondOptionLocKey =>
        OptionRoot + "TAKE_DIRECTIVE";

    protected override string ThirdOptionLocKey =>
        OptionRoot + "FLEE_WITH_GUILT";

    protected override EventOption CreateCustomSecondOption() =>
        CreateOption(
            SecondOptionLocKey,
            TakeDirective,
            HoverTipFactory.FromCardWithCardHoverTips<
                RnfmabjWillOfThePrescriptCard>())
            .ThatDoesDamage(DynamicVars[HpLossVarName].BaseValue);

    protected override EventOption CreateCustomThirdOption() =>
        CreateOption(
            ThirdOptionLocKey,
            FleeWithGuilt,
            HoverTipFactory.FromCardWithCardHoverTips<Guilty>());

    protected override LocString? ResolveCustomResolutionDescription(
        string resolution) => resolution switch
    {
        TakeDirectiveResolution => new LocString(
            "events",
            "RNFMABJ_SPECIAL_GUEST_EVENT.pages.TOOK_DIRECTIVE.description"),
        FledWithGuiltResolution => new LocString(
            "events",
            "RNFMABJ_SPECIAL_GUEST_EVENT.pages.FLED_WITH_GUILT.description"),
        _ => null,
    };

    private async Task TakeDirective()
    {
        if (Owner is { } owner)
        {
            await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                owner.Creature,
                DynamicVars[HpLossVarName].BaseValue,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null);

            CardModel card = owner.RunState
                .CreateCard<RnfmabjWillOfThePrescriptCard>(owner);
            SaveManager.Instance.MarkCardAsSeen(card);
            
            CardCmd.PreviewCardPileAdd(
                await CardPileCmd.Add(card, PileType.Deck));
        }

        await FinishWithoutReceptionAsync(
            TakeDirectiveResolution,
            new LocString(
                "events",
                "RNFMABJ_SPECIAL_GUEST_EVENT.pages.TOOK_DIRECTIVE.description"));
    }

    private async Task FleeWithGuilt()
    {
        if (Owner is { } owner)
        {
            decimal missingHp = owner.Creature.MaxHp - owner.Creature.CurrentHp;
            if (missingHp > 0m)
            {
                await CreatureCmd.Heal(owner.Creature, missingHp);
            }

            CardModel guilty = owner.RunState.CreateCard<Guilty>(owner);
            SaveManager.Instance.MarkCardAsSeen(guilty);
            CardCmd.PreviewCardPileAdd(
                await CardPileCmd.Add(guilty, PileType.Deck));
        }

        await FinishWithoutReceptionAsync(
            FledWithGuiltResolution,
            new LocString(
                "events",
                "RNFMABJ_SPECIAL_GUEST_EVENT.pages.FLED_WITH_GUILT.description"));
    }
}

public static class RnfmabjSpecialGuestRegistration
{
    private const int RequiredActIndex = 2;
    private const int RequiredPageRelicCount = 9;
    private const int RequiredEliteVictories = 2;
    private static bool _initialized;

    private static readonly string?[] VoiceNames =
    [
        "ch6_Index_ep3_Yan_1-01", "ch6_Index_ep3_Yan_2-01", "ch6_Index_ep3_Yan_3-01", "ch6_Index_ep3_Yan_4", "ch6_Index_ep3_Yan_5-01", "ch6_Index_ep3_Yan_6", "ch6_Index_ep3_Yan_7-01", "ch6_Index_ep3_Yan_8-01",
        "ch6_Index_ep3_Yan_9", "ch6_Index_ep3_Yan_10-01", "ch6_Index_ep3_Moirai_1", "ch6_Index_ep3_Yan_11-01", "ch6_Index_ep3_Moirai_2", "ch6_Index_ep3_Moirai_3", "ch6_Index_ep3_Yan_12-01", "ch6_Index_ep3_Moirai_4_a",
        "ch6_Index_ep3_Moirai_5-01", "ch6_Index_ep3_Yan_13-01", "ch6_Index_ep3_Moirai_6-01", "ch6_Index_ep3_Moirai_7", "ch6_Index_ep3_Yan_14-01", "ch6_Index_ep3_Moirai_8", "ch6_Index_ep3_Yan_15", "ch6_Index_ep3_Moirai_9-02",
        "ch6_Index_ep3_Moirai_10", "ch6_Index_ep3_Moirai_11-02", "ch6_Index_ep3_Yan_16-01", "ch6_Index_ep3_Yan_17", "ch6_Index_ep3_Moirai_12", "ch6_Index_ep3_Yan_18", "ch6_Index_ep3_Moirai_13", "ch6_Index_ep3_Yan_19-01",
        "ch6_Index_ep3_Yan_20-01", "ch6_Index_ep3_Moirai_14", "ch6_Index_ep3_Moirai_15", "ch6_Index_ep3_Moirai_16", "ch6_Index_ep3_Yan_21", "ch6_Index_ep3_Moirai_17-01", "ch6_Index_ep3_Moirai_18-04", "ch6_Index_ep3_Yan_22-01",
        "ch6_Index_ep3_Moirai_19", "ch6_Index_ep3_Yan_23", "ch6_Index_ep3_Moirai_20", "ch6_Index_ep3_Moirai_21", "ch6_Index_ep3_Moirai_22", "ch6_Index_ep3_Moirai_23", "ch6_Index_ep3_Yan_24-01", "ch6_Index_ep3_Moirai_24",
        "ch6_Index_ep3_Moirai_25-01", "ch6_Index_ep3_Yan_25", "ch6_Index_ep3_Moirai_26", "ch6_Index_ep3_Yan_26-01", "ch6_Index_ep3_Moirai_27", "ch6_Index_ep3_Moirai_28", "ch6_Index_ep3_Moirai_29", "ch6_Index_ep3_Moirai_30_a",
        "ch6_Index_ep3_Moirai_31", "ch6_Index_ep3_Moirai_32", "ch6_Index_ep3_Yan_27", "ch6_Index_ep3_Moirai_33", "ch6_Index_ep3_Yan_28-03", "ch6_Index_ep3_Yan_29-01", "ch6_Index_ep3_Yan_30", "ch6_Index_ep3_Moirai_34",
        "ch6_Index_ep3_Moirai_35", "ch6_Index_ep3_Moirai_36", "ch6_Index_ep3_Moirai_37", "ch6_Index_ep3_Moirai_38", "ch6_Index_ep3_Yan_31-01", "ch6_Index_ep3_Yan_32-02", "ch6_Index_ep3_Moirai_39", "ch6_Index_ep3_Moirai_40",
        "ch6_Index_ep3_Yan_33", "ch6_Index_ep3_Moirai_41-01", "ch6_Index_ep3_Yan_34", "ch6_Index_ep3_Yan_35-02", "ch6_Index_ep3_Moirai_42-02", "ch6_Index_ep3_Moirai_43-02", "ch6_Index_ep3_Yan_36-02", "ch6_Index_ep3_Moirai_44",
        "ch6_Index_ep3_Moirai_45", "ch6_Index_ep3_Yan_37-01", "ch6_Index_ep3_Moirai_46", "ch6_Index_ep3_Moirai_47", "ch6_Index_ep3_Moirai_48", "ch6_Index_ep3_Moirai_49", "ch6_Index_ep3_Moirai_50", "ch6_Index_ep3_Moirai_51",
        "ch6_Index_ep3_Moirai_52", "ch6_Index_ep3_Yan_38", "ch6_Index_ep3_Yan_39-03", "ch6_Index_ep3_Moirai_53", "ch6_Index_ep3_Moirai_54-01", "ch6_Index_ep3_Moirai_55-01", "ch6_Index_ep3_Yan_40-01", "ch6_Index_ep3_Moirai_56",
        "ch6_Index_ep3_Yan_41-01", "ch6_Index_ep3_Yan_42-01", "ch6_Index_ep3_Moirai_57", "ch6_Index_ep3_Moirai_58", "ch6_Index_ep3_Moirai_59", "ch6_Index_ep3_Moirai_60", "ch6_Index_ep3_Moirai_61", "ch6_Index_ep3_Moirai_62_b",
        "ch6_Index_ep3_Moirai_63-01", "ch6_Index_ep3_Moirai_64", "ch6_Index_ep3_Moirai_65-01", "ch6_Index_ep3_Moirai_66", "ch6_Index_ep3_Moirai_67", "ch6_Index_ep3_Moirai_68", "ch6_Index_ep3_Moirai_69", "ch6_Index_ep3_Moirai_70-01",
        "ch6_Index_ep3_Moirai_71_a", "ch6_Index_ep3_Moirai_72", "ch6_Index_ep3_Moirai_73", "ch6_Index_ep3_Yan_43-01", "ch6_Index_ep3_Yan_44", "ch6_Index_ep3_Yan_45", "ch6_Index_ep3_Yan_46", "ch6_Index_ep3_Yan_47-01",
        "ch6_Index_ep3_Yan_48-01", "ch6_Index_ep3_Yan_49-01", "ch6_Index_ep3_Yan_50-01", "ch6_Index_ep3_Yan_51-01", "ch6_Index_ep3_Yan_52", "ch6_Index_ep3_Yan_53-01", "ch6_Index_ep3_Yan_54-01", "ch6_Index_ep3_Yan_55",
        "ch6_Index_ep3_Yan_56", "ch6_Index_ep3_Yan_57-01", "ch6_Index_ep3_Yan_58", "ch6_Index_ep3_Yan_59", "ch6_Index_ep3_Yan_60-01", "ch6_Index_ep3_Yan_61-02", "ch6_Index_ep3_Moirai_74-02", null,
        "ch6_Index_ep3_Moirai_75", "ch6_Index_ep3_Moirai_76_b", "ch6_Index_ep3_Yan_62-01",
    ];

    private static readonly string[] FirstCharacterLayers =
    [
        "state_00.png", "state_01.png", "state_02.png", "state_01.png", "state_03.png", "state_04.png", "state_03.png", "state_05.png", "state_06.png", "state_05.png", "state_03.png", "state_07.png", "state_08.png",
        "state_00.png", "state_01.png", "state_01.png", "state_01.png", "state_02.png", "state_02.png", "state_09.png", "state_02.png", "state_10.png", "state_11.png", "state_12.png", "state_13.png", "state_13.png",
        "state_14.png", "state_07.png", "state_15.png", "state_16.png", "state_00.png", "state_17.png", "state_06.png", "state_18.png", "state_18.png", "state_17.png", "state_17.png", "state_19.png", "state_20.png",
    ];

    private static readonly string[] SecondCharacterLayers =
    [
        "state_21.png", "state_22.png", "state_22.png", "state_23.png", "state_23.png", "state_24.png", "state_23.png", "state_23.png", "state_06.png", "state_06.png", "state_25.png", "state_25.png",
        "state_25.png", "state_26.png", "state_09.png", "state_27.png", "state_27.png", "state_28.png", "state_29.png", "state_23.png", "state_24.png", "state_24.png", "state_24.png", "state_28.png",
        "state_28.png", "state_23.png", "state_23.png", "state_23.png", "state_23.png", "state_23.png", "state_24.png", "state_24.png", "state_24.png", "state_23.png",
    ];

    private static readonly string[] FinalCharacterLayers =
    [
        "state_30.png", "state_31.png", "state_32.png",
        "state_33.png", "state_34.png", "state_30.png",
    ];

    private static readonly HashSet<int> UnknownSpeakerLines =
    [
        10, 12, 13, 15, 16, 18, 19, 21, 23, 24, 25,
    ];

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        SpecialGuestStorySequence story = CreateBeforeCombatStory();
        SpecialGuestRegistry.Register(
            new SpecialGuestDefinition(
                RnfmabjSpecialGuestIds.Guest,
                new LocString("events", "RNFMABJ_SPECIAL_GUEST.name"),
                IsUnlocked,
                static () => ModelDb.Event<RnfmabjSpecialGuestEvent>(),
                [
                    new SpecialGuestStageDefinition(
                        static () => ModelDb.Encounter<RnfmabjSpecialGuestEncounter>(),
                        BeforeCombatStory: story,
                        BeforeCombatStorySetup: PauseCombatBgmForStory,
                        AfterCombatStoryRelease: ResumeCombatBgmAfterStory,
                        ExtraAssetPaths:
                        [
                            RnfmabjSpecialGuestIds.EventImage,
                            RnfmabjSpecialGuestIds.EncounterScene,
                            RnfmabjSpecialGuestIds.BattleBackground,
                            RnfmabjSpecialGuestIds.BattleBgm,
                            RnfmabjWillOfThePrescriptCard.PortraitAssetPath,
                            RnfmabjWillOfThePrescriptCard.CardHoverTipScenePath,
                        ])
                ],
                AvailabilityCondition: CanAppear));
        _initialized = true;
    }

    [SpecialGuestRegistration]
    public static void Register() => Initialize();

    internal static bool MeetsUnlockContract(
        int actIndex,
        IReadOnlyList<int> pageRelicCounts,
        bool secondActFullyLiberated,
        int eliteVictories,
        bool hasNotEncounteredXiao) =>
        actIndex == RequiredActIndex
        && hasNotEncounteredXiao
        && eliteVictories >= RequiredEliteVictories
        && pageRelicCounts.Count > 0
        && pageRelicCounts.All(static count => count >= RequiredPageRelicCount)
        || secondActFullyLiberated;

    private static bool IsUnlocked(IRunState runState) =>
        MeetsUnlockContract(
            runState.CurrentActIndex,
            runState.Players
                .Select(static player => player.Relics.Count(
                    AbnormalityPageRewardPreselection.IsPageRelic))
                .ToArray(),
            LiberationFloorIds.SecondAct.All(
                floorId => FloorLiberationProgress.IsFullyLiberated(
                    runState,
                    floorId)),
            ScoreUtility.GetElitesKilledCount(runState.MapPointHistory),
            HasNotEncounteredXiao(runState));

    private static bool CanAppear(IRunState runState) =>
        runState.CurrentActIndex == RequiredActIndex
        && HasNotEncounteredXiao(runState)
        && runState.ActFloor >= 3;

    private static bool HasNotEncounteredXiao(IRunState runState) =>
        runState is RunState concreteRunState
        && !concreteRunState.VisitedEventIds.Contains(
            ModelDb.Event<XiaoSpecialGuestEvent>().Id);

    private static Task PauseCombatBgmForStory(
        SpecialGuestStageContext context)
    {
        if (!context.State.IsStoryCompleted(RnfmabjSpecialGuestIds.BeforeCombatStory))
        {
            EncounterBgmController.StopRuntimeSession();
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

        foreach (var enemy in context.Room.Enemies)
        {
            if (enemy.IsAlive)
            {
                EncounterBgmController.RegisterMonster(enemy);
            }
        }

        return Task.CompletedTask;
    }

    private static SpecialGuestStorySequence CreateBeforeCombatStory()
    {
        var lines = new List<SpecialGuestStoryLine>(VoiceNames.Length);
        for (int index = 0; index < VoiceNames.Length; index++)
        {
            string? voice = VoiceNames[index];
            lines.Add(
                new SpecialGuestStoryLine(
                    new LocString("events", $"{RnfmabjSpecialGuestIds.BeforeCombatStory}.{index}"),
                    Speaker: new LocString("events", GetSpeakerKey(index, voice)),
                    CgTexturePath: GetBackgroundPath(index),
                    ExpressionTexturePath: GetCharacterLayerPath(index),
                    VoicePath: voice == null
                        ? null
                        : RnfmabjSpecialGuestIds.StoryVoiceRoot + voice + ".ogg",
                    SoundEffectPath: GetSoundEffectPath(index),
                    BgmCue: GetBgmCue(index)));
        }

        return new SpecialGuestStorySequence(
            RnfmabjSpecialGuestIds.BeforeCombatStory,
            lines);
    }

    private static string GetSpeakerKey(int index, string? voice)
    {
        const string root = "RNFMABJ_SPECIAL_GUEST_STORY.speakers.";
        if (index == 25)
        {
            // The shipped English XML reveals Moirai one line earlier than
            // the other three languages; keep that original teller text.
            return root + "line_25";
        }

        if (UnknownSpeakerLines.Contains(index))
        {
            return root + "unknown";
        }

        if (index is 133 or 138)
        {
            return root + "distorted_yan";
        }

        if (index == 135 || voice?.Contains("_Moirai_", StringComparison.Ordinal) == true)
        {
            return root + "moirai";
        }

        return root + "yan";
    }

    private static string GetBackgroundPath(int index)
    {
        string fileName = index switch
        {
            <= 8 => "검은화면.png",
            <= 47 => "ch6_Index_10.png",
            <= 54 => "ch6_The Index_5.png",
            <= 64 => "ch6_The Index_6.png",
            <= 71 => "ch6_The Index_7.png",
            <= 80 => "ch6_The Index_8.png",
            <= 114 => "ch6_Index_10.png",
            <= 132 => "ch6_The Index_9.png",
            _ => "ch6_Index_10.png",
        };
        return RnfmabjSpecialGuestIds.StoryBackgroundRoot + fileName;
    }

    private static string? GetCharacterLayerPath(int index)
    {
        string? fileName = index switch
        {
            >= 9 and <= 47 => FirstCharacterLayers[index - 9],
            >= 81 and <= 114 => SecondCharacterLayers[index - 81],
            >= 133 and <= 138 => FinalCharacterLayers[index - 133],
            _ => null,
        };
        return fileName == null
            ? null
            : RnfmabjSpecialGuestIds.StoryCharacterRoot + fileName;
    }

    private static string? GetSoundEffectPath(int index) => index switch
    {
        4 => RnfmabjSpecialGuestIds.StorySfxRoot + "CH6_yan_stairDown.ogg",
        5 => RnfmabjSpecialGuestIds.StorySfxRoot + "CH6_yan_sewingMachine_far.ogg",
        65 => RnfmabjSpecialGuestIds.StorySfxRoot + "CH6_yan_moiraiWalk.ogg",
        _ => null,
    };

    private static SpecialGuestStoryBgmCue? GetBgmCue(int index) => index switch
    {
        0 => new SpecialGuestStoryBgmCue(
            SpecialGuestStoryBgmAction.FadeOut,
            FadeSeconds: 1f),
        9 or 133 => new SpecialGuestStoryBgmCue(
            SpecialGuestStoryBgmAction.FadeIn,
            RnfmabjSpecialGuestIds.StoryBgm,
            VolumeDb: 0f,
            FadeSeconds: 1f),
        115 => new SpecialGuestStoryBgmCue(
            SpecialGuestStoryBgmAction.FadeOut,
            FadeSeconds: 1f),
        _ => null,
    };
}
