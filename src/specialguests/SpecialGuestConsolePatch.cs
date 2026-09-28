using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.specialguests;

/// <summary>
/// Keeps special guests out of the ordinary event pool while still allowing
/// the native `event` developer command to open their event pages directly.
/// </summary>
[HarmonyPatch(typeof(EventConsoleCmd), nameof(EventConsoleCmd.Process))]
[LibraryPatch(Reason = "原版 event 命令只在 ModelDb 事件表里查找，特邀嘉宾事件不在其中；只在参数是特邀嘉宾事件 ID 时接管，属于开发者控制台命令。")]
internal static class SpecialGuestEventConsoleProcessPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Player? issuingPlayer,
        string[] args,
        ref CmdResult __result)
    {
        if (args.Length == 0
            || !TryFindEvent(args[0], out EventModel specialGuestEvent))
        {
            return true;
        }

        if (!RunManager.Instance.IsInProgress || issuingPlayer == null)
        {
            __result = new CmdResult(success: false, "A run is currently not in progress!");
            return false;
        }

        specialGuestEvent.AssertCanonical();
        issuingPlayer.RunState.AppendToMapPointHistory(
            MapPointType.Unknown,
            RoomType.Event,
            specialGuestEvent.Id);
        Task task = RunManager.Instance.EnterRoom(new EventRoom(specialGuestEvent));
        __result = new CmdResult(
            task,
            success: true,
            "Jumped to event: '" + specialGuestEvent.Id.Entry + "'");
        return false;
    }

    internal static bool TryFindEvent(string requestedId, out EventModel eventModel)
    {
        string normalized = requestedId.ToUpperInvariant();
        foreach (SpecialGuestDefinition definition in SpecialGuestRegistry.All)
        {
            EventModel candidate = definition.EventFactory();
            if (string.Equals(candidate.Id.Entry, normalized, StringComparison.Ordinal))
            {
                eventModel = candidate;
                return true;
            }
        }

        eventModel = null!;
        return false;
    }
}

[HarmonyPatch(typeof(EventConsoleCmd), nameof(EventConsoleCmd.GetArgumentCompletions))]
internal static class SpecialGuestEventConsoleCompletionPatch
{
    [HarmonyPostfix]
    private static void Postfix(string[] args, ref CompletionResult __result)
    {
        if (args.Length > 1)
        {
            return;
        }

        string partial = args.FirstOrDefault() ?? string.Empty;
        List<string> candidates = __result.Candidates
            .Concat(SpecialGuestRegistry.All
                .Select(static definition => definition.EventFactory().Id.Entry)
                .Where(id => id.StartsWith(partial, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        __result.Candidates = candidates;
        __result.CommonPrefix = CalculateCommonPrefix(candidates);
    }

    private static string CalculateCommonPrefix(IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return string.Empty;
        }

        if (candidates.Count == 1)
        {
            return "event " + candidates[0] + " ";
        }

        int commonLength = candidates.Min(static candidate => candidate.Length);
        string first = candidates[0];
        int matchedLength = 0;
        for (int index = 0; index < commonLength; index++)
        {
            char current = char.ToUpperInvariant(first[index]);
            if (candidates.Any(candidate =>
                    char.ToUpperInvariant(candidate[index]) != current))
            {
                break;
            }

            matchedLength = index + 1;
        }

        return matchedLength == 0
            ? string.Empty
            : "event " + first[..matchedLength];
    }
}
