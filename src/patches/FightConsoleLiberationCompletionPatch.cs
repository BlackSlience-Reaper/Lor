using System;
using System.Linq;
using HarmonyLib;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.PhilosophyFloorLiberation;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(FightConsoleCmd), nameof(FightConsoleCmd.GetArgumentCompletions))]
public static class FightConsoleLiberationCompletionPatch
{
    [HarmonyPostfix]
    public static void Postfix(string[] args, ref CompletionResult __result)
    {
        if (args.Length > 1)
        {
            return;
        }

        string partial = args.FirstOrDefault() ?? string.Empty;
        IEnumerable<string> liberationIds =
        [
            ModelDb.Encounter<TechnologyFloorLiberationEncounter>().Id.Entry,
            ModelDb.Encounter<HistoryFloorLiberationEncounter>().Id.Entry,
            ModelDb.Encounter<LiteratureFloorLiberationEncounter>().Id.Entry,
            ModelDb.Encounter<ArtFloorLiberationEncounter>().Id.Entry,
            ModelDb.Encounter<LanguageFloorLiberationEncounter>().Id.Entry,
            ModelDb.Encounter<NaturalFloorLiberationEncounter>().Id.Entry,
            ModelDb.Encounter<PhilosophyFloorLiberationEncounter>().Id.Entry,
            ModelDb.Encounter<SocialFloorLiberationEncounter>().Id.Entry
        ];

        List<string> candidates = __result.Candidates
            .Concat(liberationIds.Where(id =>
                id.StartsWith(partial, StringComparison.OrdinalIgnoreCase)))
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
            return "fight " + candidates[0] + " ";
        }

        int commonLength = candidates.Min(static candidate => candidate.Length);
        string first = candidates[0];
        int matchedLength = 0;
        for (int i = 0; i < commonLength; i++)
        {
            char current = char.ToUpperInvariant(first[i]);
            if (candidates.Any(candidate => char.ToUpperInvariant(candidate[i]) != current))
            {
                break;
            }

            matchedLength = i + 1;
        }

        return matchedLength == 0
            ? string.Empty
            : "fight " + first[..matchedLength];
    }
}
