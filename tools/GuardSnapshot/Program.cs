using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using LibraryOfRuina.infra.patching;

// 离线读取指定目标的方法体；不调用游戏或模组初始化，不安装补丁。
if (args.Length < 3) throw new ArgumentException("GuardSnapshot <基准表> <输出表> <引用目录>...");
var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (string directory in args.Skip(2))
    foreach (string file in Directory.EnumerateFiles(directory, "*.dll")) files.TryAdd(Path.GetFileNameWithoutExtension(file), Path.GetFullPath(file));
AssemblyLoadContext.Default.Resolving += (_, name) => files.TryGetValue(name.Name!, out string? file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
string Key(MethodBase method) => method.DeclaringType!.Assembly.GetName().Name + "|" + method.DeclaringType.FullName + "::" + method.Name + (method.IsGenericMethodDefinition ? "`" + method.GetGenericArguments().Length : "") + "(" + string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name)) + ")";
var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (string line in File.ReadLines(args[0]).Where(l => l.Length > 0 && !l.StartsWith('#')))
{
    string key = line.Split('\t')[0];
    string assemblyName = key[..key.IndexOf('|')];
    Assembly assembly = Assembly.Load(assemblyName);
    string typeName = key[(key.IndexOf('|') + 1)..key.IndexOf("::", StringComparison.Ordinal)];
    string methodName = key[(key.IndexOf("::", StringComparison.Ordinal) + 2)..key.IndexOf('(')];
    methodName = methodName.Split('`')[0];
    Type? type = assembly.GetType(typeName);
    MethodBase? found = type?.GetMethods(Flags).FirstOrDefault(m => Key(m) == key);
    if (found == null && typeName.Contains("+<", StringComparison.Ordinal) && methodName == "MoveNext")
    {
        string parentName = typeName[..typeName.LastIndexOf("+<", StringComparison.Ordinal)];
        string ownerName = typeName[(parentName.Length + 2)..typeName.LastIndexOf('>')];
        var owners = assembly.GetType(parentName)!.GetMethods(Flags).Where(m => m.Name == ownerName).ToArray();
        if (owners.Length != 1) throw new InvalidOperationException("状态机所属方法不唯一：" + key);
        Type? state = owners[0].GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType ?? owners[0].GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
        found = state?.GetMethod("MoveNext", Flags);
    }
    if (found == null && type != null)
    {
        var candidates = type.GetMethods(Flags).Where(m => m.Name == methodName).ToArray();
        if (candidates.Length == 1) found = candidates[0];
    }
    if (found == null) throw new MissingMethodException("需要人工核对版本映射：" + key);
    string actual = Key(found);
    if (actual != key) Console.WriteLine("目标变化：" + key + "\n  -> " + actual);
    lines.Add(actual, IlFingerprint.Hash(found));
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], "# 按目标程序集离线计算的 IL 指纹；仅方法体证据，不代表运行验证。\n" + string.Join("\n", lines.Select(p => p.Key + "\t" + p.Value)) + "\n");
Console.WriteLine($"已记录 {lines.Count} 个方法体。");
