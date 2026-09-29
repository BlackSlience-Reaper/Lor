using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.scene_transitions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.specialguests.Xiao;

internal static class XiaoSpecialGuestPresentation
{
    private static readonly TimeSpan TransitionWaitLimit = TimeSpan.FromSeconds(10);

    internal static async Task WaitForStageTwoStoryAsync()
    {
        XiaoSpecialGuestBgmController.Stop();
        NGame? game = NGame.Instance;
        if (game == null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TransitionWaitLimit
               && GodotObject.IsInstanceValid(game)
               && game.IsInsideTree()
               && game.Transition is { InTransition: true })
        {
            SceneTree tree;
            try
            {
                tree = game.GetTree();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            await game.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}

public static class XiaoSpecialGuestBgmController
{
    private const string LogTag = "XiaoSpecialGuestBGM";

    private static LocalOggLoopPlayer.LoopHandle? _loop;
    private static string? _activeTrack;
    private static bool _stageTwoForced;

    public static void EnsureStageOne(CombatStateLike combatState)
    {
        if (combatState.Encounter is not XiaoSpecialGuestStageOneEncounter)
        {
            return;
        }
        _stageTwoForced = false;
        Start(ResolveStageOneTrack(combatState, ResolveTrackIndex(combatState.RoundNumber)));
    }

    public static void RefreshForRound(CombatStateLike combatState)
    {
        if (_stageTwoForced)
        {
            Start(XiaoSpecialGuestIds.IronLotusBgm);
            return;
        }
        if (combatState.Encounter is XiaoSpecialGuestStageOneEncounter)
        {
            Start(ResolveStageOneTrack(combatState, ResolveTrackIndex(combatState.RoundNumber)));
        }
    }

    public static void ForceStageTwo()
    {
        _stageTwoForced = true;
        Start(XiaoSpecialGuestIds.IronLotusBgm);
    }

    public static void Stop()
    {
        if (_loop == null && _activeTrack == null)
        {
            return;
        }

        _loop?.Stop();
        _loop = null;
        _activeTrack = null;
        Log.Info("[" + LogTag + "] Stopped Xiao special guest BGM.");
    }

    private static void Start(string track)
    {
        if (string.Equals(_activeTrack, track, StringComparison.Ordinal)
            && _loop != null)
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

    private static int ResolveTrackIndex(int roundNumber) =>
        roundNumber >= 7 ? 2 : roundNumber >= 4 ? 1 : 0;

    private static string ResolveStageOneTrack(
        CombatStateLike combatState,
        int trackIndex)
    {
        string? layer = SpecialGuestRunStateModifier.TryGet(combatState.RunState)
            ?.GetValue(XiaoSpecialGuestBackgroundAssetsPatch.StageOneLayerValueKey);
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
        return tracks[Math.Clamp(trackIndex, 0, tracks.Length - 1)];
    }
}

[HarmonyPatch]
internal static class XiaoSpecialGuestBgmCombatEndPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(CombatManager).GetMethods(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly)
            .Where(static method =>
                method.Name == "EndCombatInternal");

    [HarmonyPrefix]
    private static void Prefix(CombatManager __instance)
    {
        if (__instance.DebugOnlyGetState()?.Encounter is ISpecialGuestEncounterStage
            {
                SpecialGuestId: XiaoSpecialGuestIds.Guest,
            })
        {
            XiaoSpecialGuestBgmController.Stop();
        }
    }
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.LoseCombat),
    new Type[] { })]
internal static class XiaoSpecialGuestBgmCombatLossPatch
{
    [HarmonyPrefix]
    private static void Prefix(CombatManager __instance)
    {
        if (__instance.DebugOnlyGetState()?.Encounter is ISpecialGuestEncounterStage
            {
                SpecialGuestId: XiaoSpecialGuestIds.Guest,
            })
        {
            XiaoSpecialGuestBgmController.Stop();
        }
    }
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.Reset), typeof(bool))]
internal static class XiaoSpecialGuestBgmCombatResetPatch
{
    // CombatRoom.Exit 直接调用 CombatManager.Reset(graceful: true) 放弃战斗，
    // 不经过 EndCombatInternal / LoseCombat：控制台 `event` 等命令切换离开
    // Xiao 战斗时走的就是这条路径，此前会漏停 BGM。
    [HarmonyPrefix]
    private static void Prefix(CombatManager __instance)
    {
        if (__instance.DebugOnlyGetState()?.Encounter is ISpecialGuestEncounterStage
            {
                SpecialGuestId: XiaoSpecialGuestIds.Guest,
            })
        {
            XiaoSpecialGuestBgmController.Stop();
        }
    }
}

internal static class XiaoSpecialGuestBackgroundController
{
    public static TextureRect? GetCurrentBackgroundImage()
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        return background?.FindChild("A", recursive: true, owned: false)
                   as TextureRect
               ?? background?.FindChildren("*", "TextureRect", recursive: true, owned: false)
                   .OfType<TextureRect>()
                   .FirstOrDefault();
    }

    public static async Task RevealStageTwoAsync()
    {
        // AfterCombatStoryRelease invokes this method at the exact frame the
        // story overlay is released. Start the stage-two track before any
        // resource load or reveal animation can yield.
        XiaoSpecialGuestBgmController.ForceStageTwo();

        TextureRect? image = GetCurrentBackgroundImage();
        Texture2D? next = ResourceLoader.Load<Texture2D>(
            XiaoSpecialGuestIds.StageTwoBackground);
        if (image == null || next == null || image.Texture == null)
        {
            if (image != null && next != null)
            {
                image.Texture = next;
            }
            return;
        }

        await LorexSceneTransitionController.PlayRevealAsync(
            image,
            image.Texture,
            next);
    }
}

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.SetEvent))]
internal static class XiaoSpecialGuestEventGradientPatch
{
    private const string OverlayName = "XiaoSpecialGuestRightGradient";

    [HarmonyPostfix]
    private static void Postfix(NEventLayout __instance, EventModel eventModel)
    {
        if (eventModel is not XiaoSpecialGuestEvent
            || __instance.GetNodeOrNull<TextureRect>("%Portrait") is not { } portrait
            || portrait.GetNodeOrNull<TextureRect>(OverlayName) != null)
        {
            return;
        }

        var gradient = new Gradient
        {
            Offsets = [0f, 0.45f, 1f],
            Colors =
            [
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0.72f),
            ],
        };
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Width = 256,
            Height = 2,
            FillFrom = new Vector2(0f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
        var overlay = new TextureRect
        {
            Name = OverlayName,
            LayoutMode = 1,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        portrait.AddChildSafely(overlay);
    }
}
