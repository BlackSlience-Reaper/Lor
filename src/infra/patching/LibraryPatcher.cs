using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
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
            // 与 tools/ModSnapshot 共用 PatchClassRules，离线的跳过型前缀清单与实际安装范围一致。
            if (PatchClassRules.IsOptional(type))
            {
                optional.Add(type);
                continue;
            }

            if (PatchClassRules.HasHarmonyClassAttribute(type))
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
            // Patch() returns one replacement per original. Prepare returning false yields an empty list,
            // and a per-original Prepare that declines leaves a null entry (PatchClassProcessor.ProcessPatchJob).
            List<MethodInfo?> replacements = harmony.CreateClassProcessor(type).Patch() ?? [];
            int installed = replacements.Count(static replacement => replacement != null);
            if (installed == 0)
            {
                notApplied.Add(name);
            }
            else if (installed < replacements.Count)
            {
                notApplied.Add(name + " (" + (replacements.Count - installed) + "/" + replacements.Count + " targets skipped)");
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
    /// 在全部模组加载完之后调用：报告与其他模组共享的目标，点名可能被本模组跳过型前缀连带跳过的
    /// 第三方前缀，校验原版拷贝守卫；设置了 <c>LOR_DUMP_PATCHES</c> 时导出补丁表与守卫表。
    /// </summary>
    public static void ReportAfterAllModsLoaded(string harmonyId)
    {
        var shared = new List<string>();
        var guarded = new Dictionary<string, MethodBase>(StringComparer.Ordinal);
        var dump = new List<string>();
        Assembly ownAssembly = typeof(LibraryPatcher).Assembly;

        foreach (MethodBase original in Harmony.GetAllPatchedMethods().OrderBy(GuardKey, StringComparer.Ordinal))
        {
            Patches? info = Harmony.GetPatchInfo(original);
            if (info == null || !info.Owners.Contains(harmonyId))
            {
                continue;
            }

            bool ownSkipPrefix = info.Prefixes.Any(patch => patch.owner == harmonyId && IsSkipPrefix(patch));
            // 离线的 skip_prefixes.txt / hook_patches.txt 靠静态判定；这里用实际装上的补丁复核，漏判时能在日志里看到。
            bool hookTarget = original.DeclaringType?.FullName == PatchClassRules.HookTypeFullName;
            IEnumerable<Patch> needReason = hookTarget
                ? OwnPatches(info, harmonyId)
                : info.Prefixes.Where(patch => patch.owner == harmonyId && IsSkipPrefix(patch));
            foreach (Patch patch in needReason)
            {
                if (patch.PatchMethod.DeclaringType is { } patchClass && string.IsNullOrWhiteSpace(PatchClassRules.Reason(patchClass)))
                {
                    Log.Error(LogPrefix + (hookTarget ? "Hook patch" : "Skip prefix")
                              + " without [LibraryPatch(Reason = ...)]: " + patchClass.FullName);
                }
            }

            bool ownTranspiler = info.Transpilers.Any(patch => patch.owner == harmonyId);
            // 守卫只管别人的代码（游戏与前置库）；本模组自己的方法改动由自己的提交负责。
            if ((ownSkipPrefix || ownTranspiler) && original.DeclaringType?.Assembly != ownAssembly)
            {
                foreach (MethodBase method in GuardedBodies(original))
                {
                    AddGuarded(guarded, method);
                }
            }

            string[] others = info.Owners.Where(owner => owner != harmonyId).ToArray();
            if (others.Length > 0)
            {
                shared.Add(Signature(original) + " <- " + string.Join(", ", others));
                WarnThirdPartyPrefixesAfterOwnSkip(original, info, harmonyId);
            }

            dump.Add(GuardKey(original));
            AppendPatches(dump, original, "Prefix", info.Prefixes);
            AppendPatches(dump, original, "Postfix", info.Postfixes);
            AppendPatches(dump, original, "Transpiler", info.Transpilers);
            AppendPatches(dump, original, "Finalizer", info.Finalizers);
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
    /// 需要冻结 IL 的方法体。async/迭代器方法的入口只负责创建状态机，真正的逻辑在状态机的
    /// <c>MoveNext</c> 里，所以两者一起冻结。
    /// </summary>
    private static IEnumerable<MethodBase> GuardedBodies(MethodBase original)
    {
        yield return original;
        Type? stateMachine = original.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                             ?? original.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
        MethodInfo? moveNext = stateMachine?.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (moveNext != null)
        {
            yield return moveNext;
        }
    }

    private static void AddGuarded(Dictionary<string, MethodBase> guarded, MethodBase method)
    {
        string key = GuardKey(method);
        if (guarded.TryGetValue(key, out MethodBase? existing) && existing != method)
        {
            Log.Warn(LogPrefix + "Two guarded methods share the key " + key + "; only the first is checked.");
            return;
        }

        guarded[key] = method;
    }

    /// <summary>
    /// 用 Harmony 真实的排序（含 before/after）取前缀顺序。Harmony 2.4 在某个前缀跳过原方法后，会整个跳过
    /// 其后“影响原方法”的前缀（<c>MethodCreatorTools.AffectsOriginal</c>：返回 bool，或除 __instance、
    /// __originalMethod、__state 外有 out/ref/引用类型参数）。这里按同样规则筛出候选，实际是否冲突仍要逐个裁决。
    /// </summary>
    private static void WarnThirdPartyPrefixesAfterOwnSkip(MethodBase original, Patches info, string harmonyId)
    {
        Patch[] ordered = Sorted(original, info.Prefixes);
        int firstOwnSkip = Array.FindIndex(ordered, patch => patch.owner == harmonyId && IsSkipPrefix(patch));
        if (firstOwnSkip < 0)
        {
            return;
        }

        string[] later = ordered.Skip(firstOwnSkip + 1)
            .Where(patch => patch.owner != harmonyId && AffectsOriginal(patch.PatchMethod))
            .Select(static patch => patch.owner + ":" + patch.PatchMethod.DeclaringType?.FullName)
            .ToArray();
        if (later.Length > 0)
        {
            Log.Warn(LogPrefix + "Third-party prefixes are skipped whenever a LibraryOfRuina prefix skips "
                     + Signature(original) + ": " + string.Join(", ", later));
        }
    }

    private static bool AffectsOriginal(MethodInfo prefix)
    {
        if (prefix.ReturnType == typeof(bool))
        {
            return true;
        }

        return prefix.GetParameters().Any(static parameter =>
            parameter.Name is not ("__instance" or "__originalMethod" or "__state")
            && (parameter.IsOut || parameter.IsRetval || parameter.ParameterType.IsByRef || !parameter.ParameterType.IsValueType));
    }

    private static bool IsSkipPrefix(Patch patch) => patch.PatchMethod.ReturnType == typeof(bool);

    private static IEnumerable<Patch> OwnPatches(Patches info, string harmonyId) =>
        info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
            .Where(patch => patch.owner == harmonyId);

    /// <summary>Harmony 实际执行顺序（<see cref="PatchProcessor.GetSortedPatchMethods"/>，含 before/after）。</summary>
    private static Patch[] Sorted(MethodBase original, IEnumerable<Patch> patches)
    {
        Patch[] array = patches.ToArray();
        List<MethodInfo> order = PatchProcessor.GetSortedPatchMethods(original, array);
        return order.Select(method => array.First(patch => patch.PatchMethod == method)).ToArray();
    }

    private static void AppendPatches(List<string> dump, MethodBase original, string kind, IEnumerable<Patch> patches)
    {
        foreach (Patch patch in Sorted(original, patches))
        {
            dump.Add("  " + kind + " priority=" + patch.priority + " index=" + patch.index + " owner=" + patch.owner
                     + (patch.before is { Length: > 0 } ? " before=" + string.Join(",", patch.before) : "")
                     + (patch.after is { Length: > 0 } ? " after=" + string.Join(",", patch.after) : "")
                     + " " + patch.PatchMethod.DeclaringType?.FullName + "." + patch.PatchMethod.Name);
        }
    }

    /// <summary>日志里给人看的方法描述。</summary>
    internal static string Signature(MethodBase method) => method.FullDescription();

    /// <summary>
    /// 守卫表与补丁表的方法键：程序集、含外层类型的完整类型名、方法名、泛型元数与参数类型。
    /// <c>FullDescription()</c> 不带外层类型，不同卡牌的状态机 <c>&lt;OnPlay&gt;d__3</c> 会撞名。
    /// </summary>
    internal static string GuardKey(MethodBase method)
    {
        Type? declaring = method.DeclaringType;
        int arity = method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0;
        string parameters = string.Join(",", method.GetParameters().Select(static p => p.ParameterType.FullName ?? p.ParameterType.Name));
        return (declaring?.Assembly.GetName().Name ?? "?") + "|" + (declaring?.FullName ?? "?") + "::" + method.Name
               + (arity > 0 ? "`" + arity : "") + "(" + parameters + ")";
    }

    internal static string IlHash(MethodBase method) => IlFingerprint.Hash(method);

    private static void WriteDumpIfRequested(List<string> dump, Dictionary<string, MethodBase> guarded)
    {
        string? directory = Environment.GetEnvironmentVariable(DumpEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        // 补丁表依赖同时装了哪些模组，只能在相同组合下比较；表头记下这次的组合。
        string header = string.Concat(ModManager.GetLoadedMods()
            .Select(static mod => "# mod " + mod.manifest?.id + " " + mod.manifest?.version + "\n")
            .Order(StringComparer.Ordinal));
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "patch_table.txt"), header + string.Join("\n", dump) + "\n", new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(directory, "vanilla_copy_guard.txt"),
                VanillaCopyGuard.Header + string.Join("\n", guarded.Select(static pair => pair.Key + "\t" + IlHash(pair.Value)).Order(StringComparer.Ordinal)) + "\n",
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(directory, "vanilla_private.txt"),
                header + string.Join("\n", LibraryOfRuina.interop.VanillaPrivate.DumpLines().Order(StringComparer.Ordinal)) + "\n",
                new UTF8Encoding(false));
            Log.Info(LogPrefix + "Patch table written to " + directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warn(LogPrefix + "Could not write the patch table to " + directory + ": " + exception.Message);
        }
    }
}
