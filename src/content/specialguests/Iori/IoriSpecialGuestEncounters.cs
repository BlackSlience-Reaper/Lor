using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.specialguests.Iori;

public sealed class IoriSpecialGuestStageOneEncounter :
    EncounterModel,
    ISpecialGuestEncounterStage
{
    public string SpecialGuestId => IoriSpecialGuestIds.Guest;

    public int SpecialGuestStageIndex => 0;

    public override RoomType RoomType => RoomType.Monster;

    public override bool ShouldGiveRewards => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => ["iori"];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<IoriStageOne>()];

    public override IEnumerable<string> ExtraAssetPaths =>
        GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths
            .Concat(IoriPresentationAssets.All)
            .Concat(IoriSpecialGuestBgmController.AllTracks)
            .Concat(IoriSpecialGuestAssets.PowerIcons)
            .Distinct(StringComparer.Ordinal);

    protected override IReadOnlyList<(MonsterModel, string?)>
        GenerateMonsters() =>
        [(ModelDb.Monster<IoriStageOne>().ToMutable(), "iori")];
}

public sealed class IoriSpecialGuestStageTwoEncounter :
    EncounterModel,
    ISpecialGuestEncounterStage
{
    public string SpecialGuestId => IoriSpecialGuestIds.Guest;

    public int SpecialGuestStageIndex => 1;

    public override RoomType RoomType => RoomType.Elite;

    public override bool ShouldGiveRewards => true;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => ["iori"];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<IoriStageTwo>()];

    public override IEnumerable<string> ExtraAssetPaths =>
        GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths
            .Concat(IoriPresentationAssets.All)
            .Concat(IoriSpecialGuestBgmController.AllTracks)
            .Concat(IoriSpecialGuestAssets.PowerIcons)
            .Distinct(StringComparer.Ordinal);

    protected override IReadOnlyList<(MonsterModel, string?)>
        GenerateMonsters() =>
        [(ModelDb.Monster<IoriStageTwo>().ToMutable(), "iori")];
}

[HarmonyPatch(typeof(EncounterModel), "CreateBackgroundAssetsForCustom")]
[HarmonyPriority(Priority.Low)]
[LibraryPatch(Reason = "原版背景标题只能是遭遇 id，伊织两阶段需要按种子抽取不同的接待层；只作用于伊织特殊来宾遭遇。")]
internal static class IoriSpecialGuestBackgroundAssetsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        EncounterModel __instance,
        Rng rng,
        ref BackgroundAssets __result)
    {
        int stageIndex = __instance switch
        {
            IoriSpecialGuestStageOneEncounter => 0,
            IoriSpecialGuestStageTwoEncounter => 1,
            _ => -1,
        };
        if (stageIndex < 0)
        {
            return true;
        }

        IRunState? runState = CurrentRun.State;
        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(runState);
        (string stageOne, string stageTwo) = ResolveFloorPair(
            runState?.Rng.Seed ?? 0UL,
            state);
        string selected = stageIndex == 0 ? stageOne : stageTwo;

        __result = new BackgroundAssets(
            GuestReceptionPoolRegistry.SharedBackgroundTitle,
            rng);
        __result.BgLayers.Clear();
        __result.BgLayers.Add(selected);
        return false;
    }

    internal static string ResolveLayer(
        CombatStateLike combatState)
    {
        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(combatState.RunState);
        (string stageOne, string stageTwo) = ResolveFloorPair(
            combatState.RunState.Rng.Seed,
            state);
        return combatState.Encounter is IoriSpecialGuestStageTwoEncounter
            ? stageTwo
            : stageOne;
    }

    private static (string stageOne, string stageTwo) ResolveFloorPair(
        ulong seed,
        SpecialGuestRunStateModifier? state)
    {
        string[] layers =
            GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths;
        string? savedOne = state?.GetValue(
            IoriSpecialGuestIds.StageOneLayerValueKey);
        string? savedTwo = state?.GetValue(
            IoriSpecialGuestIds.StageTwoLayerValueKey);
        bool valid = savedOne != null
                     && savedTwo != null
                     && !string.Equals(
                         savedOne,
                         savedTwo,
                         StringComparison.Ordinal)
                     && layers.Contains(savedOne, StringComparer.Ordinal)
                     && layers.Contains(savedTwo, StringComparer.Ordinal);
        if (valid)
        {
            return (savedOne!, savedTwo!);
        }

        ulong firstRoll = SpecialGuestRegistry.StableHash64(
            seed + "|" + IoriSpecialGuestIds.Guest + "|floor-one");
        int firstIndex = (int)(firstRoll % (ulong)layers.Length);
        ulong secondRoll = SpecialGuestRegistry.StableHash64(
            seed + "|" + IoriSpecialGuestIds.Guest + "|floor-two");
        int secondOffset = 1
                           + (int)(secondRoll
                                   % (ulong)(layers.Length - 1));
        int secondIndex = (firstIndex + secondOffset) % layers.Length;
        string stageOne = layers[firstIndex];
        string stageTwo = layers[secondIndex];
        state?.SetValue(
            IoriSpecialGuestIds.StageOneLayerValueKey,
            stageOne);
        state?.SetValue(
            IoriSpecialGuestIds.StageTwoLayerValueKey,
            stageTwo);
        return (stageOne, stageTwo);
    }
}

public static class IoriSpecialGuestBgmController
{
    private const string LogTag = "IoriSpecialGuestBGM";
    private static LocalOggLoopPlayer.LoopHandle? _loop;
    private static string? _activeTrack;

    internal static readonly string[] AllTracks =
        GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks
            .Concat(GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks)
            .Concat(GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static void Ensure(CombatStateLike combatState)
    {
        if (combatState.Encounter is not (
            IoriSpecialGuestStageOneEncounter
            or IoriSpecialGuestStageTwoEncounter))
        {
            return;
        }

        Start(ResolveTrack(combatState));
    }

    public static void RefreshForRound(CombatStateLike combatState)
    {
        if (combatState.Encounter is IoriSpecialGuestStageOneEncounter
            or IoriSpecialGuestStageTwoEncounter)
        {
            Start(ResolveTrack(combatState));
        }
    }

    public static void Stop(bool restoreRunMusic = false)
    {
        bool hadSession = _loop != null || _activeTrack != null;
        if (hadSession)
        {
            _loop?.Stop();
            _loop = null;
            _activeTrack = null;
            Log.Info("[" + LogTag + "] Stopped Iori reception BGM.");
        }

        if (restoreRunMusic)
        {
            RestoreRunMusicSafely();
        }
    }

    private static void Start(string track)
    {
        if (_loop != null
            && string.Equals(
                _activeTrack,
                track,
                StringComparison.Ordinal))
        {
            return;
        }

        Stop();
        NRunMusicController.Instance?.StopMusic();
        _loop = LocalOggLoopPlayer.StartLoop(track, -3f);
        _activeTrack = _loop == null ? null : track;
        if (_loop != null)
        {
            Log.Info("[" + LogTag + "] Started track: " + track);
        }
    }

    private static void RestoreRunMusicSafely()
    {
        if (MainMenuBgmController.TryResumeOwnedRunMusic())
        {
            return;
        }

        NRunMusicController? runMusicController =
            NRunMusicController.Instance;
        if (runMusicController == null)
        {
            Log.Warn(
                "[" + LogTag
                + "] Run music restore skipped: controller is null.");
            return;
        }

        try
        {
            runMusicController.UpdateMusic();
            runMusicController.UpdateTrack();
            Log.Info(
                "[" + LogTag
                + "] Restored run music after final Iori stage.");
        }
        catch (Exception exception)
        {
            Log.Error(
                "[" + LogTag
                + "] Failed to restore run music: "
                + exception);
        }
    }

    private static string ResolveTrack(CombatStateLike combatState)
    {
        string layer = IoriSpecialGuestBackgroundAssetsPatch.ResolveLayer(
            combatState);
        string[] tracks = layer switch
        {
            GuestReceptionPoolRegistry.ReligionReceptionFloorLayerScenePath =>
                GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
            GuestReceptionPoolRegistry.LiteratureReceptionFloorLayerScenePath =>
                GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            GuestReceptionPoolRegistry.NaturalReceptionFloorLayerScenePath =>
                GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
            GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath =>
                GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
            GuestReceptionPoolRegistry.YesodReceptionFloorLayerScenePath =>
                GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            _ => GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
        };
        int trackIndex = combatState.RoundNumber >= 7
            ? 2
            : combatState.RoundNumber >= 4
                ? 1
                : 0;
        return tracks[Math.Clamp(trackIndex, 0, tracks.Length - 1)];
    }
}

[HarmonyPatch]
internal static class IoriSpecialGuestBgmCombatEndPatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() =>
        typeof(CombatManager).GetMethods(
                BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly)
            .Single(method =>
                method.Name == "EndCombatInternal"
                && method.GetParameters().Length == 1
                && string.Equals(
                    method.GetParameters()[0].ParameterType.FullName,
                    "MegaCrit.Sts2.Core.Combat.CombatTurnState",
                    StringComparison.Ordinal));

    [HarmonyPrefix]
    private static void Prefix(
        CombatManager __instance,
        out bool __state)
    {
        __state = CurrentCombat.Of(__instance)?.Encounter
            is IoriSpecialGuestStageTwoEncounter;
        if (IsIoriEncounter(__instance))
        {
            // Stage one remains silent during its automatic one-second floor
            // transfer. Stage two restores run music after native teardown.
            IoriSpecialGuestBgmController.Stop();
        }
    }

    [HarmonyPostfix]
    private static void Postfix(
        bool __state,
        ref Task __result)
    {
        if (__state)
        {
            __result = RestoreAfterCombatAsync(__result);
        }
    }

    private static async Task RestoreAfterCombatAsync(Task original)
    {
        await original;
        IoriSpecialGuestBgmController.Stop(restoreRunMusic: true);
    }

    internal static bool IsIoriEncounter(CombatManager manager) =>
        CurrentCombat.Of(manager)?.Encounter
            is ISpecialGuestEncounterStage
            {
                SpecialGuestId: IoriSpecialGuestIds.Guest,
            };
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.LoseCombat),
    new Type[] { })]
internal static class IoriSpecialGuestBgmCombatLossPatch
{
    [HarmonyPrefix]
    private static void Prefix(CombatManager __instance)
    {
        if (IoriSpecialGuestBgmCombatEndPatch.IsIoriEncounter(__instance))
        {
            IoriSpecialGuestBgmController.Stop();
        }
    }
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.Reset), typeof(bool))]
internal static class IoriSpecialGuestBgmCombatResetPatch
{
    [HarmonyPrefix]
    private static void Prefix(CombatManager __instance)
    {
        if (IoriSpecialGuestBgmCombatEndPatch.IsIoriEncounter(__instance))
        {
            IoriSpecialGuestBgmController.Stop();
        }
    }
}
