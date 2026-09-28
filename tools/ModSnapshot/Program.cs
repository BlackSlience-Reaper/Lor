using System.Reflection;
using System.Text;

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
Type[] types = LoadTypes(mod);

WriteLines("models.txt", Models());
WriteLines("saved_properties.txt", SavedProperties());
WriteLines("patches.txt", Patches(out List<string> order));
WriteLines("patch_order.txt", order);
WriteLines("static_fields.txt", StaticFields());
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
