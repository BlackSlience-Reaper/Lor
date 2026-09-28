using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.infra.patching;

/// <summary>
/// 原版拷贝守卫：本模组用跳过型前缀或 Transpiler 修补的原版方法，冻结其原始 IL 的 SHA1。
/// 游戏更新改动了这些方法时，补丁多半仍能装上，却可能在替换一段已经变了的逻辑；这里在日志里点名。
/// 表由 <c>LOR_DUMP_PATCHES</c> 导出的 <c>vanilla_copy_guard.txt</c> 生成，见 README 的“重构护栏”。
/// </summary>
internal static class VanillaCopyGuard
{
    private const string LogPrefix = "[LibraryOfRuina.VanillaCopyGuard] ";
    private const string ResourceName = "LibraryOfRuina.vanilla_copy_guard.txt";

    internal const string Header =
        "# Original IL SHA1 of vanilla methods that LibraryOfRuina skips (bool prefix) or rewrites (transpiler).\n"
        + "# Regenerate with LOR_DUMP_PATCHES=<dir> on the target game version; see README.\n";

    public static void Check(IReadOnlyCollection<MethodBase> guarded)
    {
        Dictionary<string, string>? expected = LoadTable();
        if (expected == null)
        {
            Log.Warn(LogPrefix + "Guard table resource is missing; skipped.");
            return;
        }

        var drifted = new List<string>();
        var unlisted = new List<string>();
        foreach (MethodBase method in guarded)
        {
            string signature = LibraryPatcher.Signature(method);
            if (!expected.TryGetValue(signature, out string? hash))
            {
                unlisted.Add(signature);
            }
            else if (!string.Equals(hash, LibraryPatcher.IlHash(method), StringComparison.Ordinal))
            {
                drifted.Add(signature);
            }
        }

        if (drifted.Count > 0)
        {
            Log.Warn(LogPrefix + "DRIFT: " + drifted.Count + " skipped or rewritten vanilla method(s) changed since the "
                     + "guard table was generated; review these patches:\n  " + string.Join("\n  ", drifted));
        }

        if (unlisted.Count > 0)
        {
            Log.Warn(LogPrefix + unlisted.Count + " guarded target(s) are not in the table; regenerate it:\n  "
                     + string.Join("\n  ", unlisted));
        }

        if (drifted.Count == 0 && unlisted.Count == 0)
        {
            Log.Info(LogPrefix + guarded.Count + " guarded vanilla method(s) match the table.");
        }
    }

    private static Dictionary<string, string>? LoadTable()
    {
        using Stream? stream = typeof(VanillaCopyGuard).Assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd()
            .Split('\n')
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Select(static line => line.Split('\t'))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(static parts => parts[0], static parts => parts[1].Trim(), StringComparer.Ordinal);
    }
}
