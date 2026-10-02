using LibraryOfRuina.infra.patching;
using LibraryOfRuina.Tools;

// 按目标离线生成原版拷贝守卫表。守卫范围取自补丁类实际解析出的原方法（PatchResolver 用该目标的 Harmony 执行
// Prepare/TargetMethod(s) 与静态特性解析），与运行时 LibraryPatcher 的取法相同；不再从基准表的键反查方法，
// 所以不会把本目标装不上的目标记进表里，也没有按方法名的回退。不安装补丁，不运行模组初始化或 Godot。
if (args.Length < 4)
{
    Console.Error.WriteLine("用法：GuardSnapshot <模组 dll> <基准表> <输出表> <引用目录>...");
    return 2;
}

var resolver = new PatchResolver(args[0], args.Skip(3));
var results = resolver.InstalledPatchClasses().Select(resolver.Resolve).ToArray();
var errors = resolver.LoadErrors.Select(static e => "类型加载失败：" + e).ToList();
// 有跳过型前缀或 Transpiler 的类解析失败时，守卫范围本身不可知，不能给出“完整”的表。
errors.AddRange(results.Where(static r => r.HasGuardKinds && (r.Error != null || r.Declined || r.DeclinedOriginals.Count > 0))
    .Select(static r => "守卫相关补丁类未能解析，守卫范围不完整：" + r.Type.FullName + (r.Error != null ? "：" + r.Error : "（Prepare 拒绝）")));

var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var method in resolver.GuardedBodies(results))
{
    string key = PatchResolver.GuardKey(method);
    string hash = IlFingerprint.Hash(method);
    if (lines.TryGetValue(key, out string? existing) && existing != hash)
    {
        errors.Add("两个守卫方法共用同一个键：" + key);
    }

    lines[key] = hash;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2], "# 按目标程序集离线计算的 IL 指纹；仅方法体证据，不代表运行验证。\n" + string.Join("\n", lines.Select(p => p.Key + "\t" + p.Value)) + "\n");

var baseline = File.ReadLines(args[1]).Where(static l => l.Length > 0 && !l.StartsWith('#')).Select(static l => l.Split('\t')[0]).ToHashSet(StringComparer.Ordinal);
foreach (string key in baseline.Where(k => !lines.ContainsKey(k)).Order(StringComparer.Ordinal))
{
    Console.WriteLine("守卫表有、本目标补丁类不会安装：" + key);
}

foreach (string key in lines.Keys.Where(k => !baseline.Contains(k)))
{
    Console.WriteLine("本目标会安装、守卫表没有：" + key);
}

Console.WriteLine($"已记录 {lines.Count} 个方法体。");
foreach (string error in errors)
{
    Console.Error.WriteLine("FAILED " + error);
}

return errors.Count == 0 ? 0 : 1;
