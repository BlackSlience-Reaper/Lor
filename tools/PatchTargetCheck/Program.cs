using LibraryOfRuina.Tools;

// 对一个目标的模组产物，在独立 AssemblyLoadContext 里执行每个会安装的补丁类的 Prepare 与 TargetMethod(s)，
// 用该目标的 Harmony 解析静态特性目标，再按确定的原方法核对参数注入。不安装补丁、不运行模组初始化、不启动 Godot。
// 任何异常、解析为空、Prepare 拒绝安装或注入不匹配都算失败，除非 exceptions.txt 按目标登记了原因；
// 登记了却没有失败的条目也算失败，免得例外表过期后继续掩盖问题。
if (args.Length < 5)
{
    Console.Error.WriteLine("用法：PatchTargetCheck <目标> <模组 dll> <exceptions.txt> <报告输出> <引用目录>...");
    return 2;
}

string target = args[0];
var resolver = new PatchResolver(args[1], args.Skip(4));
var exceptions = File.ReadLines(args[2])
    .Where(static line => line.Length > 0 && !line.StartsWith('#'))
    .Select(static line => line.Split('\t'))
    .Where(parts => parts.Length >= 3 && (parts[0] == target || parts[0] == "*"))
    .ToDictionary(static parts => parts[1], static parts => parts[2], StringComparer.Ordinal);

var report = new List<string> { "# 目标 " + target + "：离线执行 Prepare/TargetMethod(s) 并由该目标的 Harmony 解析；未安装补丁，不代表运行验证。" };
var failures = new List<string>();
foreach (string error in resolver.LoadErrors)
{
    failures.Add("类型加载失败：" + error);
}

int dynamicCount = 0, staticCount = 0, excepted = 0;
var seenExceptions = new HashSet<string>(StringComparer.Ordinal);
foreach (Type type in resolver.InstalledPatchClasses())
{
    ClassResult result = resolver.Resolve(type);
    string name = type.FullName!;
    string form = (result.Dynamic ? "dynamic" : "static") + (result.Optional ? ",optional" : "");
    if (result.Dynamic) dynamicCount++; else staticCount++;
    var problems = new List<string>();
    if (result.Error != null) problems.Add("异常 " + result.Error);
    if (result.Declined) problems.Add("Prepare 返回 false，整个类不安装");
    problems.AddRange(result.DeclinedOriginals.Select(static m => "Prepare(original) 返回 false：" + PatchResolver.GuardKey(m)));
    problems.AddRange(result.Problems.Select(static p => "注入 " + p));
    foreach (string original in result.Uses.Select(static use => PatchResolver.GuardKey(use.Original)).Distinct().Order(StringComparer.Ordinal))
    {
        report.Add(name + "\t" + form + "\t" + original);
    }

    foreach (string problem in problems)
    {
        report.Add(name + "\t" + form + "\tFAILED " + problem);
    }

    if (problems.Count == 0)
    {
        continue;
    }

    if (exceptions.TryGetValue(name, out string? reason))
    {
        seenExceptions.Add(name);
        excepted++;
        Console.WriteLine($"例外（{reason}）：{name}\n  " + string.Join("\n  ", problems));
        continue;
    }

    failures.Add(name + " [" + form + "]\n  " + string.Join("\n  ", problems));
}

failures.AddRange(exceptions.Keys.Where(name => !seenExceptions.Contains(name)).Select(name => "例外表条目已不再失败或不存在，删掉它：" + name));
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
File.WriteAllText(args[3], string.Join("\n", report) + "\n");
Console.WriteLine($"{resolver.Mod.GetName().Name} {target}：{dynamicCount} 个动态补丁类、{staticCount} 个静态补丁类，{failures.Count} 项失败，{excepted} 项登记例外。");
foreach (string failure in failures)
{
    Console.Error.WriteLine("FAILED " + target + " " + failure);
}

return failures.Count == 0 ? 0 : 1;
