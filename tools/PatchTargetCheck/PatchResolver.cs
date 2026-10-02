using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.Tools;

/// <summary>
/// 在独立的 AssemblyLoadContext 里加载某个目标的模组产物和该目标的引用，用同一上下文里加载的
/// Harmony 2.4.2 本身解析每个会被安装的补丁类的原方法：构造 PatchClassProcessor，执行 Prepare、
/// TargetMethod(s)（GetBulkMethods），对静态特性调用 PatchTools.GetOriginalMethod，逐个原方法再执行一次
/// Prepare(original)。不调用 Patch()，不生成替换方法，不运行模组初始化，不启动 Godot。
/// 这些都是 Harmony 的私有/内部成员，名字变了会直接报错，不会退回到自写的近似规则。
/// </summary>
internal sealed class PatchResolver
{
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags AnyDeclared = AnyInstance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private readonly object harmony;
    private readonly Type processorType;
    private readonly MethodInfo runPrepare;
    private readonly MethodInfo getBulkMethods;
    private readonly FieldInfo patchMethodsField;
    private readonly FieldInfo attributeInfoField;
    private readonly FieldInfo attributeTypeField;
    private readonly MethodInfo getOriginalMethod;
    private readonly Type harmonyMethodType;

    public Assembly Mod { get; }
    public List<string> LoadErrors { get; } = [];
    public Type[] Types { get; }

    public PatchResolver(string modPath, IEnumerable<string> referenceDirectories)
    {
        modPath = Path.GetFullPath(modPath);
        // 先到先得：调用方按优先级传目录（目标专用目录在共享目录之前）。模组本身固定指向给定路径。
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFileNameWithoutExtension(modPath)] = modPath,
        };
        foreach (string directory in referenceDirectories)
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.Ordinal))
            {
                files.TryAdd(Path.GetFileNameWithoutExtension(file), Path.GetFullPath(file));
            }
        }

        var context = new AssemblyLoadContext("PatchTargets:" + Path.GetFileName(modPath));
        context.Resolving += (ctx, name) => files.TryGetValue(name.Name!, out string? path) ? ctx.LoadFromAssemblyPath(path) : null;
        Mod = context.LoadFromAssemblyPath(modPath);
        try
        {
            Types = Mod.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // 加载失败的类型可能正是补丁类；不能当作没有，交给调用方报错。
            Types = exception.Types.Where(static type => type != null).ToArray()!;
            LoadErrors.AddRange(exception.LoaderExceptions.Select(static error => error?.Message ?? "?").Distinct());
        }

        Assembly harmonyAssembly = context.LoadFromAssemblyName(new AssemblyName("0Harmony"));
        Type Harmony(string name) => harmonyAssembly.GetType("HarmonyLib." + name, throwOnError: true)!;
        harmony = Activator.CreateInstance(Harmony("Harmony"), "LibraryOfRuina.PatchTargetCheck")!;
        processorType = Harmony("PatchClassProcessor");
        harmonyMethodType = Harmony("HarmonyMethod");
        runPrepare = processorType.GetMethods(AnyInstance)
            .Single(static method => method.Name == "RunMethod" && method.GetGenericArguments().Length == 2)
            .MakeGenericMethod(Harmony("HarmonyPrepare"), typeof(bool));
        getBulkMethods = Required(processorType.GetMethod("GetBulkMethods", AnyInstance), "PatchClassProcessor.GetBulkMethods");
        patchMethodsField = Required(processorType.GetField("patchMethods", AnyInstance), "PatchClassProcessor.patchMethods");
        Type attributePatch = Harmony("AttributePatch");
        attributeInfoField = Required(attributePatch.GetField("info", AnyInstance), "AttributePatch.info");
        attributeTypeField = Required(attributePatch.GetField("type", AnyInstance), "AttributePatch.type");
        getOriginalMethod = Required(Harmony("PatchTools").GetMethod("GetOriginalMethod", AnyStatic, [harmonyMethodType]), "PatchTools.GetOriginalMethod");
    }

    private static T Required<T>(T? member, string name) where T : class =>
        member ?? throw new MissingMemberException("Harmony 内部成员不存在（Harmony 版本变了？）：" + name);

    /// <summary>LibraryPatcher.ApplyAll 会交给 Harmony 的类，顺序与安装无关，这里按全名排序。</summary>
    public IEnumerable<Type> InstalledPatchClasses() =>
        Types.Where(PatchClassRules.IsInstalled).OrderBy(static type => type.FullName, StringComparer.Ordinal);

    public ClassResult Resolve(Type type)
    {
        var result = new ClassResult(type, PatchClassRules.IsOptional(type), IsDynamic(type));
        try
        {
            object processor = Activator.CreateInstance(processorType, harmony, type)!;
            if (!(bool)runPrepare.Invoke(processor, [true, false, null, Array.Empty<object>()])!)
            {
                result.Declined = true;
                return result;
            }

            var bulk = (List<MethodBase>)getBulkMethods.Invoke(processor, null)!;
            var patches = new List<(MethodInfo Method, string Kind, object Info)>();
            foreach (object attributePatch in (IEnumerable)patchMethodsField.GetValue(processor)!)
            {
                object info = attributeInfoField.GetValue(attributePatch)!;
                var method = (MethodInfo)harmonyMethodType.GetField("method")!.GetValue(info)!;
                string kind = attributeTypeField.GetValue(attributePatch)?.ToString() ?? "?";
                if (kind is not ("Prefix" or "Postfix" or "Transpiler" or "Finalizer"))
                {
                    throw new NotSupportedException("离线检查不支持补丁种类 " + kind + "：" + method.Name);
                }

                patches.Add((method, kind, info));
            }

            if (bulk.Count > 0)
            {
                // PatchClassProcessor.BulkPatch：TargetMethod(s) 不能与目标特性混用。
                foreach ((MethodInfo method, _, object info) in patches)
                {
                    object? methodType = harmonyMethodType.GetField("methodType")!.GetValue(info);
                    if (harmonyMethodType.GetField("methodName")!.GetValue(info) != null
                        || (methodType != null && methodType.ToString() != "Normal")
                        || harmonyMethodType.GetField("argumentTypes")!.GetValue(info) != null)
                    {
                        throw new ArgumentException("TargetMethod(s) 与目标特性混用：" + method.Name);
                    }
                }

                foreach (MethodBase original in bulk)
                {
                    foreach ((MethodInfo method, string kind, _) in patches)
                    {
                        result.Uses.Add(new PatchUse(method, kind, original));
                    }
                }
            }
            else
            {
                foreach ((MethodInfo method, string kind, object info) in patches)
                {
                    MethodBase original = (MethodBase?)getOriginalMethod.Invoke(null, [info])
                        ?? throw new ArgumentException("补丁方法没有可解析的目标：" + method.Name);
                    result.Uses.Add(new PatchUse(method, kind, original));
                }
            }

            if (result.Uses.Count == 0)
            {
                throw new InvalidOperationException("没有解析出任何原方法");
            }

            foreach (MethodBase original in result.Uses.Select(static use => use.Original).Distinct().ToArray())
            {
                // ProcessPatchJob：逐个原方法再跑一次 Prepare(original)，返回 false 时这个原方法不打补丁。
                if (!(bool)runPrepare.Invoke(processor, [true, false, null, new object[] { original }])!)
                {
                    result.DeclinedOriginals.Add(original);
                }
            }

            result.Uses.RemoveAll(use => result.DeclinedOriginals.Contains(use.Original));
            foreach (PatchUse use in result.Uses.Where(static use => use.Kind != "Transpiler"))
            {
                foreach (string problem in Injection.Check(use.Method, use.Kind, use.Original))
                {
                    result.Problems.Add(use.Method.Name + " -> " + GuardKey(use.Original) + ": " + problem);
                }
            }
        }
        catch (Exception exception)
        {
            result.Error = Describe(exception);
        }

        return result;
    }

    private static bool IsDynamic(Type type) =>
        type.GetMethods(AnyDeclared).Any(static method => method.Name is "TargetMethod" or "TargetMethods"
            || method.CustomAttributes.Any(static data => data.AttributeType.FullName is "HarmonyLib.HarmonyTargetMethod" or "HarmonyLib.HarmonyTargetMethods"));

    /// <summary>沿 InnerException 展开，保留每层类型；反射调用与 HarmonyException 的包装层不带信息，跳过。</summary>
    private static string Describe(Exception exception)
    {
        var parts = new List<string>();
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is TargetInvocationException || current.GetType().FullName == "HarmonyLib.HarmonyException" && current.InnerException != null)
            {
                continue;
            }

            parts.Add(current.GetType().Name + ": " + current.Message.Split('\n')[0].Trim());
        }

        return string.Join(" <- ", parts);
    }

    /// <summary>与 LibraryPatcher.GuardKey 相同：程序集|含外层类型的完整类型名::方法名`泛型元数(参数类型)。</summary>
    public static string GuardKey(MethodBase method)
    {
        Type? declaring = method.DeclaringType;
        int arity = method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0;
        string parameters = string.Join(",", method.GetParameters().Select(static p => p.ParameterType.FullName ?? p.ParameterType.Name));
        return (declaring?.Assembly.GetName().Name ?? "?") + "|" + (declaring?.FullName ?? "?") + "::" + method.Name
               + (arity > 0 ? "`" + arity : "") + "(" + parameters + ")";
    }

    /// <summary>
    /// 与 LibraryPatcher.ReportAfterAllModsLoaded 相同的守卫范围：本模组有跳过型前缀（返回 bool）或 Transpiler、
    /// 且不属于本模组程序集的原方法；async/迭代器方法连同状态机 MoveNext。
    /// </summary>
    public IEnumerable<MethodBase> GuardedBodies(IEnumerable<ClassResult> results)
    {
        foreach (var group in results.Where(static r => r.Error == null && !r.Declined).SelectMany(static r => r.Uses).GroupBy(static use => use.Original))
        {
            MethodBase original = group.Key;
            bool guarded = group.Any(static use => use.Kind == "Transpiler" || use.Kind == "Prefix" && use.Method.ReturnType == typeof(bool));
            if (!guarded || original.DeclaringType?.Assembly == Mod)
            {
                continue;
            }

            yield return original;
            Type? stateMachine = original.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                                 ?? original.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
            MethodInfo? moveNext = stateMachine?.GetMethod("MoveNext", AnyInstance);
            if (moveNext != null)
            {
                yield return moveNext;
            }
        }
    }
}

internal sealed record PatchUse(MethodInfo Method, string Kind, MethodBase Original);

internal sealed class ClassResult(Type type, bool optional, bool dynamic)
{
    public Type Type { get; } = type;
    public bool Optional { get; } = optional;
    public bool Dynamic { get; } = dynamic;
    public bool Declined { get; set; }
    public string? Error { get; set; }
    public List<PatchUse> Uses { get; } = [];
    public List<MethodBase> DeclinedOriginals { get; } = [];
    public List<string> Problems { get; } = [];
    public bool HasGuardKinds => PatchClassRules.SkipPrefixes(Type).Any() || PatchClassRules.PatchMethods(Type).Any(static m =>
        m.Name == "Transpiler" || m.CustomAttributes.Any(static a => a.AttributeType.FullName == "HarmonyLib.HarmonyTranspiler"));
}

/// <summary>
/// Harmony 2.4.2 MethodCreatorTools.EmitCallParameter 的参数注入规则，针对一个确定的原方法逐个核对。
/// Harmony 对按名注入的参数不查类型，类型不兼容时生成的 IL 在调用时才出错；这里一并报出。
/// </summary>
internal static class Injection
{
    private static readonly HashSet<string> Special =
        ["__instance", "__originalMethod", "__args", "__result", "__resultRef", "__state", "__exception", "__runOriginal"];

    public static IEnumerable<string> Check(MethodInfo patch, string kind, MethodBase original)
    {
        ParameterInfo[] originalParameters = original.GetParameters();
        string[] originalNames = originalParameters.Select(static p => p.Name ?? "").ToArray();
        Type returnType = original is MethodInfo info ? info.ReturnType : typeof(void);
        IEnumerable<ParameterInfo> parameters = patch.GetParameters();
        // 后缀返回值类型与首个参数相同时是“传递式后缀”，首个参数接收结果，不参与注入。
        if (kind == "Postfix" && patch.ReturnType != typeof(void) && patch.GetParameters() is [var first, ..] && first.ParameterType == patch.ReturnType)
        {
            parameters = parameters.Skip(1);
        }

        foreach (ParameterInfo parameter in parameters)
        {
            string realName = RealName(patch, parameter);
            Type type = parameter.ParameterType;
            Type element = type.IsByRef ? type.GetElementType()! : type;
            if (Special.Contains(realName))
            {
                switch (realName)
                {
                    case "__instance" when !original.IsStatic && !Compatible(element, original.DeclaringType!):
                        yield return $"__instance 类型 {element.FullName} 不兼容 {original.DeclaringType!.FullName}";
                        break;
                    case "__instance" when original.IsStatic:
                        yield return "__instance 用在静态原方法上，恒为 null";
                        break;
                    case "__result" when returnType == typeof(void):
                        yield return "__result 用在无返回值的原方法上";
                        break;
                    case "__result" when !Compatible(type.IsByRef && !returnType.IsByRef ? element : type, returnType):
                        yield return $"__result 类型 {type.FullName} 不能接收 {returnType.FullName}";
                        break;
                    case "__resultRef" when !returnType.IsByRef:
                        yield return "__resultRef 用在非 ref 返回的原方法上";
                        break;
                }

                continue;
            }

            if (realName.StartsWith("___", StringComparison.Ordinal))
            {
                string name = realName[3..];
                FieldInfo? field = name.All(char.IsDigit)
                    ? original.DeclaringType?.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).ElementAtOrDefault(int.Parse(name))
                    : FindField(original.DeclaringType, name);
                if (field == null)
                {
                    yield return "字段不存在 " + realName;
                }
                else if (!Compatible(element, field.FieldType))
                {
                    yield return $"字段 {realName} 类型 {element.FullName} 不兼容 {field.FieldType.FullName}";
                }

                continue;
            }

            int index;
            if (realName.StartsWith("__", StringComparison.Ordinal))
            {
                if (!int.TryParse(realName[2..], out index) || index < 0 || index >= originalParameters.Length)
                {
                    yield return "无效的参数序号 " + realName;
                    continue;
                }
            }
            else
            {
                index = ArgumentIndex(patch, originalNames, parameter);
                if (index < 0)
                {
                    yield return "原方法没有参数 " + realName;
                    continue;
                }
            }

            Type originalType = originalParameters[index].ParameterType;
            Type originalElement = originalType.IsByRef ? originalType.GetElementType()! : originalType;
            if (!Compatible(element, originalElement))
            {
                yield return $"参数 {parameter.Name} 类型 {element.FullName} 不兼容原方法第 {index} 个参数 {originalElement.FullName}";
            }
            else if (type.IsByRef && !originalType.IsByRef && element.IsValueType && !originalElement.IsValueType)
            {
                yield return $"参数 {parameter.Name} 按引用接收引用类型参数 {originalElement.FullName}";
            }
        }
    }

    private static bool Compatible(Type patchType, Type originalType) =>
        patchType == originalType || patchType.IsAssignableFrom(originalType) || patchType == typeof(object);

    private static FieldInfo? FindField(Type? type, string name)
    {
        // AccessTools.Field：沿继承链找声明字段。
        for (; type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    private static IEnumerable<CustomAttributeData> Arguments(MemberInfo member) =>
        member.CustomAttributes.Where(static data => data.AttributeType.Name == "HarmonyArgument");

    private static (string? OriginalName, int Index, string? NewName) Parse(CustomAttributeData data)
    {
        // HarmonyArgument(string originalName) / (int index) / (string originalName, string newName) / (int index, string name)
        IList<CustomAttributeTypedArgument> args = data.ConstructorArguments;
        return args switch
        {
            [{ Value: string name }] => (name, -1, null),
            [{ Value: int index }] => (null, index, null),
            [{ Value: string name }, { Value: string newName }] => (name, -1, newName),
            [{ Value: int index }, { Value: string name }] => (null, index, name),
            _ => (null, -1, null),
        };
    }

    /// <summary>InjectedParameter.CalculateRealName：参数级 HarmonyArgument 的原名，或方法/类级的改名。</summary>
    private static string RealName(MethodInfo patch, ParameterInfo parameter)
    {
        CustomAttributeData? own = parameter.GetCustomAttributesData().FirstOrDefault(static data => data.AttributeType.Name == "HarmonyArgument");
        if (own != null)
        {
            return Parse(own).OriginalName ?? parameter.Name!;
        }

        foreach (CustomAttributeData data in Arguments(patch).Concat(patch.DeclaringType != null ? Arguments(patch.DeclaringType) : []))
        {
            var (originalName, _, newName) = Parse(data);
            if (originalName == parameter.Name)
            {
                return string.IsNullOrEmpty(newName) ? parameter.Name! : newName;
            }
        }

        return parameter.Name!;
    }

    /// <summary>PatchArgumentExtensions.GetArgumentIndex。</summary>
    private static int ArgumentIndex(MethodInfo patch, string[] originalNames, ParameterInfo parameter)
    {
        CustomAttributeData? own = parameter.GetCustomAttributesData().FirstOrDefault(static data => data.AttributeType.Name == "HarmonyArgument");
        if (own != null)
        {
            var (originalName, index, _) = Parse(own);
            string? name = !string.IsNullOrEmpty(originalName) ? originalName : index >= 0 && index < originalNames.Length ? originalNames[index] : null;
            if (name != null)
            {
                return Array.IndexOf(originalNames, name);
            }
        }

        foreach (MemberInfo member in new MemberInfo?[] { patch, patch.DeclaringType }.OfType<MemberInfo>())
        {
            foreach (CustomAttributeData data in Arguments(member))
            {
                var (originalName, index, newName) = Parse(data);
                if (originalName != parameter.Name)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(newName))
                {
                    return Array.IndexOf(originalNames, newName);
                }

                if (index >= 0 && index < originalNames.Length)
                {
                    return Array.IndexOf(originalNames, originalNames[index]);
                }
            }
        }

        return Array.IndexOf(originalNames, parameter.Name);
    }
}
