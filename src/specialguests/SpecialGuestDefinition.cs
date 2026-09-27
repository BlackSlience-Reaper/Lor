using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.specialguests;

/// <summary>
/// Identifies an encounter as one stage of a special-guest reception.  The
/// interface deliberately carries only stable IDs so encounter implementations
/// remain independent of the event-flow implementation.
/// </summary>
public interface ISpecialGuestEncounterStage
{
    string SpecialGuestId { get; }

    int SpecialGuestStageIndex { get; }
}

/// <summary>
/// Extends a stage's already-generated native combat rewards.  Implementations
/// add to <paramref name="rewards"/>; they must not replace the standard set.
/// </summary>
public interface ISpecialGuestRewardAugmenter
{
    Task AugmentAsync(SpecialGuestStageContext context, RewardsSet rewards);
}

public enum SpecialGuestStoryTiming
{
    BeforeCombat = 0,
    AfterVictory = 1,
}

/// <summary>
/// Per-line transform applied after fitting story art into the original
/// 1920-by-1080 Library of Ruina story canvas.
/// </summary>
public sealed record SpecialGuestStoryArtLayout(
    float Scale = 1f,
    float OffsetX = 0f,
    float OffsetY = 0f);

/// <summary>
/// Line-scoped control for an optional story-owned BGM stream.  A missing cue
/// means "continue" so existing special-guest stories retain their exact audio
/// behavior.
/// </summary>
public enum SpecialGuestStoryBgmAction
{
    Continue = 0,
    FadeIn = 1,
    FadeOut = 2,
    Stop = 3,
}

public sealed record SpecialGuestStoryBgmCue(
    SpecialGuestStoryBgmAction Action,
    string? TrackPath = null,
    float VolumeDb = 0f,
    float FadeSeconds = 1f);

/// <summary>
/// One localized story line.  Body and expression are separate so original
/// Library of Ruina expression tables can be reproduced without flattening the
/// source art.
/// </summary>
public sealed record SpecialGuestStoryLine(
    LocString Text,
    LocString? Speaker = null,
    string? CgTexturePath = null,
    string? ExpressionTexturePath = null,
    string? VoicePath = null,
    float VoiceVolumeDb = 0f,
    SpecialGuestStoryArtLayout? ArtLayout = null,
    string? SoundEffectPath = null,
    float SoundEffectVolumeDb = 0f,
    SpecialGuestStoryBgmCue? BgmCue = null);

public sealed record SpecialGuestStorySequence(
    string Id,
    IReadOnlyList<SpecialGuestStoryLine> Lines,
    bool HasBackgroundMusic = false)
{
    public IEnumerable<string> GetAssetPaths()
    {
        foreach (string path in SpecialGuestStoryResources.AssetPaths)
        {
            yield return path;
        }

        foreach (SpecialGuestStoryLine line in Lines)
        {
            if (!string.IsNullOrWhiteSpace(line.CgTexturePath))
            {
                yield return line.CgTexturePath;
            }

            if (!string.IsNullOrWhiteSpace(line.ExpressionTexturePath))
            {
                yield return line.ExpressionTexturePath;
            }

            if (!string.IsNullOrWhiteSpace(line.VoicePath))
            {
                yield return line.VoicePath;
            }

            if (!string.IsNullOrWhiteSpace(line.SoundEffectPath))
            {
                yield return line.SoundEffectPath;
            }

            if (!string.IsNullOrWhiteSpace(line.BgmCue?.TrackPath))
            {
                yield return line.BgmCue.TrackPath;
            }
        }
    }
}

public sealed record SpecialGuestStageContext(
    IRunState RunState,
    CombatRoom Room,
    SpecialGuestDefinition Guest,
    SpecialGuestStageDefinition Stage,
    SpecialGuestRunStateModifier State);

/// <summary>
/// Declarative stage data.  The two callback pairs bracket the reusable story
/// barrier, allowing a guest to prepare an old background before its story and
/// perform a synchronized reveal after every peer has finished reading.
/// </summary>
public sealed record SpecialGuestStageDefinition(
    Func<EncounterModel> EncounterFactory,
    SpecialGuestStorySequence? BeforeCombatStory = null,
    SpecialGuestStorySequence? AfterVictoryStory = null,
    ISpecialGuestRewardAugmenter? RewardAugmenter = null,
    Func<SpecialGuestStageContext, Task>? BeforeCombatStorySetup = null,
    Func<SpecialGuestStageContext, Task>? AfterCombatStoryRelease = null,
    Func<SpecialGuestStageContext, Task>? BeforeVictoryStorySetup = null,
    Func<SpecialGuestStageContext, Task>? AfterVictoryStoryRelease = null,
    IReadOnlyList<string>? ExtraAssetPaths = null)
{
    public IEnumerable<string> GetAssetPaths()
    {
        if (ExtraAssetPaths != null)
        {
            foreach (string path in ExtraAssetPaths.Where(static path => !string.IsNullOrWhiteSpace(path)))
            {
                yield return path;
            }
        }

        if (BeforeCombatStory != null)
        {
            foreach (string path in BeforeCombatStory.GetAssetPaths())
            {
                yield return path;
            }
        }

        if (AfterVictoryStory != null)
        {
            foreach (string path in AfterVictoryStory.GetAssetPaths())
            {
                yield return path;
            }
        }
    }
}

public sealed record SpecialGuestDefinition(
    string Id,
    LocString GuestName,
    Func<IRunState, bool> UnlockCondition,
    Func<EventModel> EventFactory,
    IReadOnlyList<SpecialGuestStageDefinition> Stages,
    Func<IRunState, bool>? AvailabilityCondition = null)
{
    public const int MinimumActFloor = 4;

    public bool CanAppear(IRunState runState) =>
        IsWithinActFloorWindow(runState)
        && (AvailabilityCondition?.Invoke(runState) ?? true);

    internal static bool IsWithinActFloorWindow(IRunState runState)
    {
        int lastActFloor = runState.Map.GetRowCount();
        if (lastActFloor <= 0
            && runState.CurrentActIndex >= 0
            && runState.CurrentActIndex < runState.Acts.Count)
        {
            lastActFloor = runState.Act.GetNumberOfRooms(
                runState.Players.Count > 1) + 1;
        }

        return runState.ActFloor >= MinimumActFloor
               && runState.ActFloor <= lastActFloor;
    }

    public SpecialGuestStageDefinition GetStage(int stageIndex)
    {
        if ((uint)stageIndex >= (uint)Stages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, $"Special guest {Id} has {Stages.Count} stages.");
        }

        return Stages[stageIndex];
    }
}
