using System;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Rnfmabj;

internal static class RnfmabjBlockAudio
{
    internal const float ConsecutiveGainFastIntervalSeconds = 0.5f;
    private const float ConsecutiveGainStandardIntervalSeconds = 1.25f;

    private const string GainBlockSfx = "event:/sfx/block_gain";
    private const float GainBlockVolumeMultiplier = 0.5f;
    // The existing local guard sound is -2 dB; another -6.0206 dB halves its amplitude.
    private const float LocalBlockSfxVolumeDb = -10.0206f;
    // CreatureCmd.GainBlock(..., fast: true) already waits 0/0.03 seconds (fast/normal).
    private const float StandardFastGainBlockWaitSeconds = 0.03f;

    private static readonly AsyncLocal<GainBlockScope?> CurrentGainBlockScope = new();

    internal static void PlayLocalBlockSfx(string audioPath) =>
        LocalOggOneShotPlayer.Play(audioPath, LocalBlockSfxVolumeDb);

    internal static Task WaitForNextConsecutiveGain() =>
        Cmd.CustomScaledWait(
            ConsecutiveGainFastIntervalSeconds,
            ConsecutiveGainStandardIntervalSeconds - StandardFastGainBlockWaitSeconds);

    internal static GainBlockScope PushGainBlockScope(Creature creature)
    {
        var scope = new GainBlockScope(creature, CurrentGainBlockScope.Value);
        CurrentGainBlockScope.Value = scope;
        return scope;
    }

    internal static void PopGainBlockScope(GainBlockScope scope)
    {
        if (ReferenceEquals(CurrentGainBlockScope.Value, scope))
        {
            CurrentGainBlockScope.Value = scope.Parent;
        }
    }

    internal static bool ShouldReduceGainBlockSfx(string sfx) =>
        string.Equals(sfx, GainBlockSfx, StringComparison.Ordinal)
        && CurrentGainBlockScope.Value?.Creature.Monster is RnfmabjMonsterBase;

    internal sealed class GainBlockScope(Creature creature, GainBlockScope? parent)
    {
        internal Creature Creature { get; } = creature;

        internal GainBlockScope? Parent { get; } = parent;
    }

    internal static float ReduceGainBlockVolume(float volume) =>
        volume * GainBlockVolumeMultiplier;
}

[HarmonyPatch(
    typeof(CreatureCmd),
    nameof(CreatureCmd.GainBlock), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool))]
internal static class RnfmabjGainBlockSoundScopePatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature creature, out RnfmabjBlockAudio.GainBlockScope __state) =>
        __state = RnfmabjBlockAudio.PushGainBlockScope(creature);

    [HarmonyPostfix]
    private static void Postfix(RnfmabjBlockAudio.GainBlockScope __state) =>
        RnfmabjBlockAudio.PopGainBlockScope(__state);
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), typeof(string), typeof(float))]
internal static class RnfmabjGainBlockSoundVolumePatch
{
    [HarmonyPrefix]
    private static void Prefix(string sfx, ref float volume)
    {
        if (RnfmabjBlockAudio.ShouldReduceGainBlockSfx(sfx))
        {
            volume = RnfmabjBlockAudio.ReduceGainBlockVolume(volume);
        }
    }
}
