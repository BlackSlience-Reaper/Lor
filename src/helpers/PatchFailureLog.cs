using System;
using System.Collections.Concurrent;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.helpers;

internal static class PatchFailureLog
{
    private const int MaxLogsPerSurface = 3;
    private static readonly ConcurrentDictionary<string, int> Counts =
        new(StringComparer.Ordinal);

    public static void Warn(string surface, Exception exception)
    {
        int count = Counts.AddOrUpdate(surface, 1, static (_, value) => value + 1);
        if (count > MaxLogsPerSurface)
        {
            return;
        }

        Log.Warn(
            "[LibraryOfRuina.PatchFailure] "
            + surface
            + " failed ("
            + count
            + "/"
            + MaxLogsPerSurface
            + "): "
            + exception);
    }
}
