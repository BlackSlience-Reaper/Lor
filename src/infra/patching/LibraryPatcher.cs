using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Logging;
using Environment = System.Environment;

namespace LibraryOfRuina.infra.patching;

/// <summary>
/// 本模组唯一的补丁安装入口。按 <see cref="LibraryAssemblyTypes.Loadable"/> 的顺序逐类安装，
/// 与 Harmony.PatchAll 的遍历顺序相同，所以同目标、同优先级补丁的执行顺序不变。
/// 运行期不得另建 Harmony 实例，也不得扫描其他模组的程序集。
/// </summary>
internal static class LibraryPatcher
{
    public const string HarmonyId = "FYY.LibraryOfRuina";
    private const string LogPrefix = "[LibraryOfRuina.Patching] ";
    private const string DumpEnvironmentVariable = "LOR_DUMP_PATCHES";

    internal sealed record Result(
        IReadOnlyList<string> FailedRequired,
        IReadOnlyList<string> SkippedOptional,
        IReadOnlyList<string> NotApplied);

    public static Result ApplyAll(Harmony harmony)
    {
        var failedRequired = new List<string>();
        var skippedOptional = new List<string>();
        var notApplied = new List<string>();
        var optional = new List<Type>();

        foreach (Type type in LibraryAssemblyTypes.Loadable)
        {
            if (type.GetCustomAttribute<LibraryPatchAttribute>() is { Optional: true })
            {
                optional.Add(type);
                continue;
            }

            if (type.HasHarmonyAttribute())
            {
                Apply(harmony, type, optional: false, failedRequired, notApplied);
            }
        }

        foreach (Type type in optional)
        {
            Apply(harmony, type, optional: true, skippedOptional, notApplied);
        }

        return new Result(failedRequired, skippedOptional, notApplied);
    }

    private static void Apply(Harmony harmony, Type type, bool optional, List<string> failures, List<string> notApplied)
    {
        string name = Describe(type);
        try
        {
            // Patch() returns the replacement methods; an empty list means Prepare returned false
            // or TargetMethods yielded nothing.
            if (harmony.CreateClassProcessor(type).Patch() is not { Count: > 0 })
            {
                notApplied.Add(name);
            }
        }
        catch (Exception exception)
        {
            failures.Add(name);
            string message = LogPrefix + (optional ? "Optional" : "Required") + " patch class " + name
                             + " was skipped: " + (optional ? exception.Message : exception.ToString());
            if (optional)
            {
                Log.Info(message);
            }
            else
            {
                Log.Error(message);
            }
        }
    }

    /// <summary>补丁类的全名，前面加上所属功能（命名空间去掉 <c>LibraryOfRuina.</c>），便于在汇总里定位。</summary>
    private static string Describe(Type type)
    {
        string feature = type.Namespace?.StartsWith("LibraryOfRuina.", StringComparison.Ordinal) == true
            ? type.Namespace["LibraryOfRuina.".Length..]
            : type.Namespace ?? "";
        return "[" + feature + "] " + type.Name;
    }

    /// <summary>
    /// 在全部模组加载完之后调用：报告与其他模组共享的目标，单独点名排在本模组跳过型前缀之后的
    /// 第三方前缀，校验原版拷贝守卫；设置了 <c>LOR_DUMP_PATCHES</c> 时导出补丁表与守卫表。
    /// </summary>
    public static void ReportAfterAllModsLoaded(string harmonyId)
    {
        var shared = new List<string>();
        var guarded = new List<MethodBase>();
        var dump = new List<string>();

        foreach (MethodBase original in Harmony.GetAllPatchedMethods().OrderBy(Signature, StringComparer.Ordinal))
        {
            Patches? info = Harmony.GetPatchInfo(original);
            if (info == null || !info.Owners.Contains(harmonyId))
            {
                continue;
            }

            bool ownSkipPrefix = info.Prefixes.Any(patch => patch.owner == harmonyId && IsSkipPrefix(patch));
            bool ownTranspiler = info.Transpilers.Any(patch => patch.owner == harmonyId);
            if (ownSkipPrefix || ownTranspiler)
            {
                guarded.Add(original);
            }

            string[] others = info.Owners.Where(owner => owner != harmonyId).ToArray();
            if (others.Length > 0)
            {
                shared.Add(Signature(original) + " <- " + string.Join(", ", others));
                WarnThirdPartyPrefixesAfterOwnSkip(original, info, harmonyId);
            }

            dump.Add(Signature(original));
            AppendPatches(dump, "Prefix", info.Prefixes);
            AppendPatches(dump, "Postfix", info.Postfixes);
            AppendPatches(dump, "Transpiler", info.Transpilers);
            AppendPatches(dump, "Finalizer", info.Finalizers);
        }

        if (shared.Count > 0)
        {
            Log.Info(LogPrefix + shared.Count + " patched target(s) are shared with other mods:\n  "
                     + string.Join("\n  ", shared));
        }

        VanillaCopyGuard.Check(guarded);
        WriteDumpIfRequested(dump, guarded);
    }

    /// <summary>
    /// 近似 Harmony 的前缀执行顺序（优先级降序、安装序升序，未计入 before/after）。Harmony 2.4 在某个前缀
    /// 跳过原方法后，会整个跳过其后“影响原方法”的前缀（返回 bool，或注入参数/返回值引用），所以排在本模组
    /// 跳过型前缀之后的第三方前缀可能根本不执行。
    /// </summary>
    private static void WarnThirdPartyPrefixesAfterOwnSkip(MethodBase original, Patches info, string harmonyId)
    {
        Patch[] ordered = info.Prefixes
            .OrderByDescending(static patch => patch.priority)
            .ThenBy(static patch => patch.index)
            .ToArray();
        int firstOwnSkip = Array.FindIndex(ordered, patch => patch.owner == harmonyId && IsSkipPrefix(patch));
        if (firstOwnSkip < 0)
        {
            return;
        }

        string[] later = ordered.Skip(firstOwnSkip + 1)
            .Where(patch => patch.owner != harmonyId)
            .Select(static patch => patch.owner + ":" + patch.PatchMethod.DeclaringType?.FullName)
            .ToArray();
        if (later.Length > 0)
        {
            Log.Warn(LogPrefix + "Third-party prefixes run after a LibraryOfRuina prefix that can skip "
                     + Signature(original) + ": " + string.Join(", ", later));
        }
    }

    private static bool IsSkipPrefix(Patch patch) => patch.PatchMethod.ReturnType == typeof(bool);

    private static void AppendPatches(List<string> dump, string kind, IEnumerable<Patch> patches)
    {
        foreach (Patch patch in patches.OrderByDescending(static p => p.priority).ThenBy(static p => p.index))
        {
            dump.Add("  " + kind + " priority=" + patch.priority + " index=" + patch.index + " owner=" + patch.owner
                     + " " + patch.PatchMethod.DeclaringType?.FullName + "." + patch.PatchMethod.Name);
        }
    }

    internal static string Signature(MethodBase method) => method.FullDescription();

    internal static string IlHash(MethodBase method)
    {
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        return il == null ? "no-il" : Convert.ToHexString(SHA1.HashData(il)).ToLowerInvariant();
    }

    private static void WriteDumpIfRequested(List<string> dump, List<MethodBase> guarded)
    {
        string? directory = Environment.GetEnvironmentVariable(DumpEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "patch_table.txt"), string.Join("\n", dump) + "\n", new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(directory, "vanilla_copy_guard.txt"),
                VanillaCopyGuard.Header + string.Join("\n", guarded.Select(static m => Signature(m) + "\t" + IlHash(m)).Order(StringComparer.Ordinal)) + "\n",
                new UTF8Encoding(false));
            Log.Info(LogPrefix + "Patch table written to " + directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warn(LogPrefix + "Could not write the patch table to " + directory + ": " + exception.Message);
        }
    }
}
