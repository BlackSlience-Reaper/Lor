using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.helpers;

/// <summary>
/// Runs presentation (video, animation, SFX, scene transitions, node lookups) that sits in front of
/// synchronized state writes. A presentation failure is local to one client; if it propagated, that
/// client would skip the writes that follow while its peers perform them — a permanent desync
/// (design philosophy §4). Timing on the success path is unchanged: the task is still awaited.
/// </summary>
internal static class PresentationGuard
{
    private static readonly HashSet<string> ReportedSurfaces = new(StringComparer.Ordinal);

    internal static async Task RunAsync(Func<Task> presentation, string surface)
    {
        try
        {
            await presentation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Report(surface, exception);
        }
    }

    /// <summary>Node or resource lookup for presentation; returns null when the lookup throws.</summary>
    internal static T? Get<T>(Func<T?> lookup, string surface) where T : class
    {
        try
        {
            return lookup();
        }
        catch (Exception exception)
        {
            Report(surface, exception);
            return null;
        }
    }

    internal static void Run(Action presentation, string surface)
    {
        try
        {
            presentation();
        }
        catch (Exception exception)
        {
            Report(surface, exception);
        }
    }

    private static void Report(string surface, Exception exception)
    {
        bool first;
        lock (ReportedSurfaces)
        {
            first = ReportedSurfaces.Add(surface);
        }

        if (first)
        {
            Log.Warn("[LibraryOfRuina.Presentation] " + surface + " failed; state updates continue: " + exception);
        }
    }
}
