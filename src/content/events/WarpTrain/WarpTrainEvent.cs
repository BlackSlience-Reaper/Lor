using System;
using System.Threading.Tasks;
using LibraryOfRuina.content.acts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.events.WarpTrain;

public enum WarpTrainPage
{
    Page1 = 1,
    Page2 = 2,
    Page3 = 3,
    Page4 = 4,
    Page5 = 5,
    Page6 = 6,
    Page7 = 7,
    Page8 = 8,
    Page9 = 9,
    Page10 = 10,
    Page11 = 11,
    Page12 = 12
}

public sealed class WarpTrainEvent : EventModel
{
    private const int EscapeHealAmount = 12;

    public const string WarpBackgroundPath = WarpTrainAssets.WarpTrainBackgroundTexture;
    public const string BlackBackgroundPath = WarpTrainAssets.BlackScreenTexture;
    public const string LoveTownBackgroundPath = WarpTrainAssets.LoveTownBackgroundTexture;

    public const string WarpTrainBgmPath = WarpTrainAssets.WarpTrainBgm;
    public const string LoveTownEventBgmPath = WarpTrainAssets.LoveTownEventBgm;

    
    public const string MaryVoicePath1 = WarpTrainAssets.MaryDialogueSfx;
    public const string TommyVoicePath1 = WarpTrainAssets.TommyDialogueSfx;
    public const string MaryVoicePath2 = WarpTrainAssets.TownsfolkDialogue1Sfx;
    public const string TommyVoicePath2 = WarpTrainAssets.TownsfolkDialogue2Sfx;
    public const string TownsfolkCheerPath1 = WarpTrainAssets.CrowdDialogue1Sfx;
    public const string TownsfolkCheerPath2 = WarpTrainAssets.CrowdDialogue2Sfx;

    private static readonly IReadOnlyList<string> PreloadAssetPaths =
    [
        WarpBackgroundPath,
        BlackBackgroundPath,
        LoveTownBackgroundPath,
        WarpTrainBgmPath,
        LoveTownEventBgmPath,
        MaryVoicePath1,
        TommyVoicePath1,
        MaryVoicePath2,
        TommyVoicePath2,
        TownsfolkCheerPath1,
        TownsfolkCheerPath2
    ];

    public override bool IsShared => true;

    public override bool IsAllowed(IRunState runState)
    {
        if (runState is RunState concreteRunState)
        {
            return !concreteRunState.VisitedEventIds.Contains(Id)
                && LibraryOfRuinaActModel.IsSecondFamily(runState)
                && runState.ActFloor is <= 10 and >= 2;
        }
        return false;
    }

    public override EventLayoutType LayoutType => EventLayoutType.Default;

    public override LocString InitialDescription =>
        L10NLookup("WARP_TRAIN_EVENT.pages.PAGE_1.description");

    public WarpTrainPage CurrentPage { get; private set; } = WarpTrainPage.Page1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(EscapeHealAmount)
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        CurrentPage = WarpTrainPage.Page1;
        return
        [
            ContinueOption(WarpTrainPage.Page1, ToPage2),
            Option(WarpTrainPage.Page1, "SKIP", ToPage10)
        ];
    }

    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        HashSet<string> paths = new(base.GetAssetPaths(runState));
        paths.UnionWith(PreloadAssetPaths);
        return paths;
    }

    public override Task Resume(AbstractRoom exitedRoom)
    {
        if (IsFinished)
        {
            return Task.CompletedTask;
        }

        if (exitedRoom is not CombatRoom combatRoom || combatRoom.Encounter is not TomerryEncounter)
        {
            return Task.CompletedTask;
        }

        CurrentPage = WarpTrainPage.Page12;
        SetEventFinished(DescriptionFor(WarpTrainPage.Page12));
        ApplyPresentationForCurrentPage(playVoiceWhenPageChanges: false);
        return Task.CompletedTask;
    }

    public override void OnRoomEnter()
    {
        ApplyPresentationForCurrentPage(playVoiceWhenPageChanges: true);
    }

    protected override void OnEventFinished()
    {
        StopPresentationAudio(restoreRunMusic: true);
    }

    private Task ToPage2()
    {
        SetPage(
            WarpTrainPage.Page2,
            ContinueOption(WarpTrainPage.Page2, ToPage3));
        return Task.CompletedTask;
    }

    private Task ToPage3()
    {
        SetPage(
            WarpTrainPage.Page3,
            ContinueOption(WarpTrainPage.Page3, ToPage4));
        return Task.CompletedTask;
    }

    private Task ToPage4()
    {
        SetPage(
            WarpTrainPage.Page4,
            ContinueOption(WarpTrainPage.Page4, ToPage5));
        return Task.CompletedTask;
    }

    private Task ToPage5()
    {
        SetPage(
            WarpTrainPage.Page5,
            ContinueOption(WarpTrainPage.Page5, ToPage6));
        return Task.CompletedTask;
    }

    private Task ToPage6()
    {
        SetPage(
            WarpTrainPage.Page6,
            ContinueOption(WarpTrainPage.Page6, ToPage7));
        return Task.CompletedTask;
    }

    private Task ToPage7()
    {
        SetPage(
            WarpTrainPage.Page7,
            Option(WarpTrainPage.Page7, "LOOK", ToPage8),
            Option(WarpTrainPage.Page7, "LEAVE", LeaveAtPage7));
        return Task.CompletedTask;
    }

    private Task LeaveAtPage7()
    {
        SetEventFinished(L10NLookup("WARP_TRAIN_EVENT.pages.PAGE_7_LEAVE.description"));
        ApplyPresentationForCurrentPage(playVoiceWhenPageChanges: false);
        return Task.CompletedTask;
    }

    private Task ToPage8()
    {
        SetPage(
            WarpTrainPage.Page8,
            ContinueOption(WarpTrainPage.Page8, ToPage9));
        return Task.CompletedTask;
    }

    private Task ToPage9()
    {
        SetPage(
            WarpTrainPage.Page9,
            ContinueOption(WarpTrainPage.Page9, ToPage10));
        return Task.CompletedTask;
    }

    private Task ToPage10()
    {
        SetPage(
            WarpTrainPage.Page10,
            Option(WarpTrainPage.Page10, "FIGHT", EnterTomerryEncounter),
            Option(
                WarpTrainPage.Page10,
                "ESCAPE",
                EscapeToPage11,
                HoverTipFactory.FromCardWithCardHoverTips<Injury>()));
        return Task.CompletedTask;
    }

    private Task EnterTomerryEncounter()
    {
        StopPresentationAudio(restoreRunMusic: false);
        EnterCombatWithoutExitingEvent<TomerryEncounter>([], shouldResumeAfterCombat: true);
        return Task.CompletedTask;
    }

    private async Task EscapeToPage11()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        await CardPileCmd.AddCurseToDeck<Injury>(owner);
        await CreatureCmd.Heal(owner.Creature, DynamicVars.Heal.IntValue);
        CurrentPage = WarpTrainPage.Page11;
        SetEventFinished(DescriptionFor(WarpTrainPage.Page11));
        ApplyPresentationForCurrentPage(playVoiceWhenPageChanges: false);
    }

    private void SetPage(WarpTrainPage page, params EventOption[] options)
    {
        CurrentPage = page;
        SetEventState(DescriptionFor(page), options);
        ApplyPresentationForCurrentPage(playVoiceWhenPageChanges: true);
    }

    private LocString DescriptionFor(WarpTrainPage page)
    {
        return L10NLookup($"WARP_TRAIN_EVENT.pages.{PageLocKey(page)}.description");
    }

    private EventOption ContinueOption(WarpTrainPage page, Func<Task> onChosen)
    {
        return Option(page, "CONTINUE", onChosen);
    }

    private EventOption Option(
        WarpTrainPage page,
        string optionKey,
        Func<Task> onChosen,
        IEnumerable<IHoverTip>? hoverTips = null)
    {
        return new EventOption(
            this,
            onChosen,
            $"WARP_TRAIN_EVENT.pages.{PageLocKey(page)}.options.{optionKey}",
            hoverTips ?? Array.Empty<IHoverTip>());
    }

    private void ApplyPresentationForCurrentPage(bool playVoiceWhenPageChanges)
    {
        NWarpTrainEventPresentationController? controller =
            NWarpTrainEventPresentationController.GetFromCurrentRoom(createIfMissing: true);

        controller?.ApplyPresentationForPage(CurrentPage, playVoiceWhenPageChanges);

        if (IsFinished)
        {
            controller?.StopAllAudio(restoreRunMusic: true);
        }
    }

    private static void StopPresentationAudio(bool restoreRunMusic)
    {
        NWarpTrainEventPresentationController
            .GetFromCurrentRoom(createIfMissing: false)?
            .StopAllAudio(restoreRunMusic);
    }

    private static string PageLocKey(WarpTrainPage page)
    {
        return page switch
        {
            WarpTrainPage.Page1 => "PAGE_1",
            WarpTrainPage.Page2 => "PAGE_2",
            WarpTrainPage.Page3 => "PAGE_3",
            WarpTrainPage.Page4 => "PAGE_4",
            WarpTrainPage.Page5 => "PAGE_5",
            WarpTrainPage.Page6 => "PAGE_6",
            WarpTrainPage.Page7 => "PAGE_7",
            WarpTrainPage.Page8 => "PAGE_8",
            WarpTrainPage.Page9 => "PAGE_9",
            WarpTrainPage.Page10 => "PAGE_10",
            WarpTrainPage.Page11 => "PAGE_11",
            WarpTrainPage.Page12 => "PAGE_12",
            _ => "PAGE_1"
        };
    }
}
