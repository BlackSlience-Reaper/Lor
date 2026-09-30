using System.Reflection;
using System.Text;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;

// usage: ModSnapshot <mod.dll> <out-dir> <reference-dir-or-dll>...
// Writes deterministic, sorted text snapshots so a refactor can be diffed against a baseline.
if (args.Length < 3)
{
    Console.Error.WriteLine("usage: ModSnapshot <mod.dll> <out-dir> <reference-dir-or-dll>...");
    return 2;
}

string modPath = Path.GetFullPath(args[0]);
string outDir = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outDir);

var referencePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
void AddReference(string path)
{
    // First occurrence wins, so pass the most specific directories first.
    referencePaths.TryAdd(Path.GetFileNameWithoutExtension(path), path);
}

AddReference(modPath);
foreach (string reference in args.Skip(2))
{
    if (Directory.Exists(reference))
    {
        foreach (string dll in Directory.EnumerateFiles(reference, "*.dll"))
        {
            AddReference(dll);
        }
    }
    else if (File.Exists(reference))
    {
        AddReference(reference);
    }
}

foreach (string dll in Directory.EnumerateFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"))
{
    AddReference(dll);
}

using var context = new MetadataLoadContext(new PathAssemblyResolver(referencePaths.Values), "System.Private.CoreLib");
var missing = new SortedSet<string>(StringComparer.Ordinal);
Assembly mod = context.LoadFromAssemblyPath(modPath);
// The order LibraryAssemblyTypes hands to every auto-discovery step (same shared source file).
IReadOnlyList<string> discoveryTable = TypeDiscoveryOrder.ReadTable(mod, required: false);
Type[] types = TypeDiscoveryOrder.Sort(LoadTypes(mod), discoveryTable);

WriteLines("models.txt", Models());
WriteLines("saved_properties.txt", SavedProperties());
WriteLines("patches.txt", Patches(out List<string> order));
WriteLines("patch_order.txt", order);
WriteLines("static_fields.txt", StaticFields());
WriteLines("skip_prefixes.txt", SkipPrefixes());
WriteLines("hook_patches.txt", HookPatches());
// Not sorted: the discovery order LibraryPatcher and the other auto-discovery steps iterate (types listed in
// type_discovery_order.txt first, the rest in TypeDef order, which moves when source files move).
WriteLines("type_order.txt", types
    .Where(static type => type != null && !type.FullName!.Contains('<'))
    .Select(static type => type!.FullName!));
// The types whose discovery order is behaviour, in that order; "unlisted" ones follow TypeDef order and move with files.
WriteLines("discovery_order.txt", DiscoveryOrder());
WriteLines("unresolved.txt", missing);
Console.WriteLine($"snapshot written to {outDir} ({types.Length} types, {missing.Count} unresolved)");
return 0;

Type[] LoadTypes(Assembly assembly)
{
    try
    {
        return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException exception)
    {
        foreach (Exception? loaderException in exception.LoaderExceptions)
        {
            if (loaderException != null)
            {
                missing.Add("type-load: " + loaderException.Message);
            }
        }

        return exception.Types.Where(static type => type != null).ToArray()!;
    }
}

void WriteLines(string name, IEnumerable<string> lines)
{
    File.WriteAllText(Path.Combine(outDir, name), string.Join("\n", lines) + "\n", new UTF8Encoding(false));
}

bool DerivesFrom(Type type, string baseFullName)
{
    try
    {
        for (Type? current = type.BaseType; current != null; current = current.BaseType)
        {
            if (current.FullName == baseFullName)
            {
                return true;
            }
        }
    }
    catch (FileNotFoundException exception)
    {
        missing.Add("base-type: " + type.FullName + " -> " + exception.Message);
    }

    return false;
}

IEnumerable<string> Models()
{
    // Model class names are ModelDb ids (Id.Entry is the class-name slug): any change here changes
    // localization keys, save data and network ids.
    return types
        .Where(type => type.IsClass && !type.IsAbstract && DerivesFrom(type, "MegaCrit.Sts2.Core.Models.AbstractModel"))
        .Select(type => type.FullName!)
        .Order(StringComparer.Ordinal);
}

// Same filters as the order-dependent consumers: LibraryPatcher, RegisterRuntimeCardPools, RegisterAllyTurnProviders,
// LibraryOfRuinaEventRelicPoolPatch. Table keys that match no type are listed as "stale".
IEnumerable<string> DiscoveryOrder()
{
    var listed = new HashSet<string>(discoveryTable, StringComparer.Ordinal);
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (Type type in types)
    {
        string? category;
        try
        {
            category = PatchClassRules.IsInstalled(type) ? "patch"
                : type.GetCustomAttributesData().Any(static data => data.AttributeType.Name == "CardPoolAttribute") ? "card-pool"
                : !type.IsAbstract && !type.IsInterface && type.GetInterfaces().Any(static i => i.Name == "IAllyTurnProvider") ? "ally-provider"
                : !type.IsAbstract && DerivesFrom(type, "MegaCrit.Sts2.Core.Models.RelicModel") ? "relic"
                : null;
        }
        catch (FileNotFoundException exception)
        {
            missing.Add("discovery-order: " + type.FullName + " -> " + exception.Message);
            continue;
        }

        string key = TypeDiscoveryOrder.Key(type);
        seen.Add(key);
        if (category != null)
        {
            yield return category + "\t" + type.FullName + (listed.Contains(key) ? "" : "\tunlisted");
        }
    }

    foreach (string key in discoveryTable.Where(key => !seen.Contains(key)))
    {
        yield return "stale\t" + key;
    }
}

IEnumerable<string> SavedProperties()
{
    var lines = new List<string>();
    foreach (Type type in types)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (property.GetCustomAttributesData().Any(static data => data.AttributeType.Name == "SavedPropertyAttribute"))
            {
                lines.Add($"{property.Name}\t{SafeTypeName(property.PropertyType)}\t{type.FullName}");
            }
        }
    }

    return lines.Order(StringComparer.Ordinal);
}

string SafeTypeName(Type type)
{
    try
    {
        return type.ToString();
    }
    catch (FileNotFoundException)
    {
        return type.Name;
    }
}

string FormatArgument(CustomAttributeTypedArgument argument)
{
    return argument.Value switch
    {
        null => "null",
        Type type => "typeof(" + type.FullName + ")",
        string text => "\"" + text + "\"",
        IReadOnlyCollection<CustomAttributeTypedArgument> items => "[" + string.Join(", ", items.Select(FormatArgument)) + "]",
        _ when argument.ArgumentType.IsEnum => EnumName(argument),
        _ => Convert.ToString(argument.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "?"
    };
}

string EnumName(CustomAttributeTypedArgument argument)
{
    foreach (FieldInfo field in argument.ArgumentType.GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        if (Equals(field.GetRawConstantValue(), argument.Value))
        {
            return argument.ArgumentType.Name + "." + field.Name;
        }
    }

    return argument.ArgumentType.Name + "(" + argument.Value + ")";
}

string FormatAttribute(CustomAttributeData data)
{
    string name = data.AttributeType.Name.Replace("Attribute", string.Empty, StringComparison.Ordinal);
    var parts = data.ConstructorArguments.Select(FormatArgument)
        .Concat(data.NamedArguments.Select(named => named.MemberName + "=" + FormatArgument(named.TypedValue)));
    return name + "(" + string.Join(", ", parts) + ")";
}

static string PatchKind(MethodInfo method)
{
    foreach (CustomAttributeData data in method.GetCustomAttributesData())
    {
        switch (data.AttributeType.Name)
        {
            case "HarmonyPrefix": return "Prefix";
            case "HarmonyPostfix": return "Postfix";
            case "HarmonyTranspiler": return "Transpiler";
            case "HarmonyFinalizer": return "Finalizer";
            case "HarmonyPrepare": return "Prepare";
            case "HarmonyTargetMethod": return "TargetMethod";
            case "HarmonyTargetMethods": return "TargetMethods";
            case "HarmonyCleanup": return "Cleanup";
        }
    }

    return method.Name switch
    {
        "Prefix" or "Postfix" or "Transpiler" or "Finalizer" or "Prepare" or "TargetMethod" or "TargetMethods" or "Cleanup" => method.Name,
        _ => string.Empty
    };
}

IEnumerable<string> Patches(out List<string> orderLines)
{
    var lines = new List<string>();
    var byTarget = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
    // Metadata order is the order the initializer applies patch classes in, which decides the
    // execution order of same-priority patches on a shared target.
    foreach (Type type in types)
    {
        List<CustomAttributeData> classAttributes;
        try
        {
            classAttributes = type.GetCustomAttributesData().ToList();
        }
        catch (FileNotFoundException exception)
        {
            missing.Add("attributes: " + type.FullName + " -> " + exception.Message);
            continue;
        }

        var harmonyClass = classAttributes.Where(static data => data.AttributeType.Name.StartsWith("Harmony", StringComparison.Ordinal)).ToList();
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var patchMethods = methods.Where(method => PatchKind(method) != string.Empty
            || method.GetCustomAttributesData().Any(static data => data.AttributeType.Name == "HarmonyPatch")).ToList();
        if (!harmonyClass.Any(static data => data.AttributeType.Name == "HarmonyPatch") && patchMethods.Count == 0)
        {
            continue;
        }

        string classTarget = string.Join(" ", harmonyClass.Where(static data => data.AttributeType.Name == "HarmonyPatch").Select(FormatAttribute));
        string classExtras = string.Join(" ", harmonyClass.Where(static data => data.AttributeType.Name != "HarmonyPatch").Select(FormatAttribute));
        foreach (MethodInfo method in patchMethods.OrderBy(static method => method.Name, StringComparer.Ordinal))
        {
            string kind = PatchKind(method);
            var methodAttributes = method.GetCustomAttributesData()
                .Where(static data => data.AttributeType.Name.StartsWith("Harmony", StringComparison.Ordinal)
                    && data.AttributeType.Name is not ("HarmonyPrefix" or "HarmonyPostfix" or "HarmonyTranspiler" or "HarmonyFinalizer"))
                .Select(FormatAttribute);
            string returns = SafeTypeName(method.ReturnType);
            string line = $"{type.FullName}\t{kind}\t{method.Name}\treturns={returns}\ttarget={classTarget}\t{classExtras}\t{string.Join(" ", methodAttributes)}";
            lines.Add(line.TrimEnd());
            if (kind is "Prefix" or "Postfix" or "Transpiler" or "Finalizer")
            {
                string target = classTarget + " " + string.Join(" ", methodAttributes.Where(static text => text.StartsWith("HarmonyPatch", StringComparison.Ordinal)));
                if (!byTarget.TryGetValue(target.Trim(), out List<string>? ordered))
                {
                    byTarget[target.Trim()] = ordered = new List<string>();
                }

                ordered.Add($"{kind} {type.FullName}.{method.Name}");
            }
        }
    }

    orderLines = byTarget
        .Where(static pair => pair.Value.Count > 1)
        .Select(static pair => pair.Key + "\n  " + string.Join("\n  ", pair.Value))
        .ToList();
    return lines.Order(StringComparer.Ordinal);
}

// Every bool prefix (it can skip the original) must state why in [LibraryPatch(Reason = ...)] on its class;
// check.sh fails on MISSING. The reason text is part of the snapshot so a changed rationale shows in review.
IEnumerable<string> SkipPrefixes()
{
    var lines = new List<string>();
    foreach (Type type in types)
    {
        MethodInfo[] methods;
        try
        {
            methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        }
        catch (FileNotFoundException)
        {
            continue;
        }

        // Same candidate rule as LibraryPatcher (shared source file), so a class the installer would patch
        // cannot slip past the reason check because of how its targets are declared.
        bool isSkipPrefix = methods.Any(PatchClassRules.IsSkipPrefix) && PatchClassRules.IsInstalled(type);
        if (!isSkipPrefix)
        {
            continue;
        }

        string? reason = PatchClassRules.Reason(type);
        lines.Add($"{type.FullName}\t{(string.IsNullOrWhiteSpace(reason) ? "MISSING" : reason)}");
    }

    return lines.Order(StringComparer.Ordinal);
}

// Every patch class that targets the vanilla Hook bus by attribute must also state why it is not a model override.
// Class-level targets count from base classes too (Harmony reads them with inherit: true), and the target type may
// be given as typeof(...) or as a type-name string. Classes that pick Hook methods in TargetMethod(s) are caught at
// runtime by LibraryPatcher's report instead.
IEnumerable<string> HookPatches()
{
    var lines = new List<string>();
    foreach (Type type in types)
    {
        bool targetsHook;
        try
        {
            IEnumerable<CustomAttributeData> classAttributes = [];
            for (Type? current = type; current != null; current = current.BaseType)
            {
                classAttributes = classAttributes.Concat(current.GetCustomAttributesData());
            }

            MethodInfo[] patchMethods = PatchClassRules.PatchMethods(type).ToArray();
            targetsHook = patchMethods.Length > 0
                && PatchClassRules.IsInstalled(type)
                && patchMethods.SelectMany(static method => method.GetCustomAttributesData())
                    .Concat(classAttributes)
                    .Any(static data => data.AttributeType.Name == "HarmonyPatch" && TargetsHookType(data));
        }
        catch (FileNotFoundException)
        {
            continue;
        }

        if (targetsHook)
        {
            string? reason = PatchClassRules.Reason(type);
            lines.Add($"{type.FullName}\t{(string.IsNullOrWhiteSpace(reason) ? "MISSING" : reason)}");
        }
    }

    return lines.Order(StringComparer.Ordinal);
}

static bool TargetsHookType(CustomAttributeData data)
{
    object? first = data.ConstructorArguments.FirstOrDefault().Value;
    return first switch
    {
        Type type => type.FullName == PatchClassRules.HookTypeFullName,
        // [HarmonyPatch("Namespace.Type, Assembly", "Method")]: the assembly part is optional.
        string typeName => typeName.Split(',')[0].Trim() == PatchClassRules.HookTypeFullName,
        _ => false
    };
}

IEnumerable<string> StaticFields()
{
    var lines = new List<string>();
    foreach (Type type in types)
    {
        if (type.Name.Contains('<', StringComparison.Ordinal))
        {
            continue;
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.IsLiteral || field.Name.Contains('<', StringComparison.Ordinal))
            {
                continue;
            }

            string mutability = field.IsInitOnly ? "readonly" : "mutable";
            lines.Add($"{mutability}\t{type.FullName}.{field.Name}\t{SafeTypeName(field.FieldType)}");
        }
    }

    return lines.Order(StringComparer.Ordinal);
}
