using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args is ["--self-test"])
{
    return SelfTests.Run();
}

if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: MonsterHpCheck [repository-root] | --self-test");
    return 2;
}

string root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
string sourceRoot = Path.Combine(root, "src");
if (!Directory.Exists(sourceRoot))
{
    Console.Error.WriteLine($"Source directory missing: {sourceRoot}");
    return 2;
}

// Match the runtime source exclusions in LibraryOfRuina.csproj.
var paths = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
    .Where(path =>
    {
        string relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
        return !relative.StartsWith("debug/", StringComparison.Ordinal)
            && !relative.StartsWith("encounters/debug/", StringComparison.Ordinal);
    })
    .Order(StringComparer.Ordinal)
    .ToArray();
var results = new List<CheckResult>();
foreach (string flavor in new[] { "Beta", "Public" })
{
    var options = new CSharpParseOptions(preprocessorSymbols: flavor == "Beta" ? ["STS2_BETA"] : []);
    var trees = paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options,
        Path.GetRelativePath(root, path).Replace('\\', '/'))).ToArray();
    var result = new HpChecker(trees).Check();
    results.Add(result);
    foreach (var issue in result.Issues)
    {
        var location = issue.Location.GetLineSpan();
        string message = $"{flavor}: {issue.Message}";
        Console.Error.WriteLine($"{location.Path}:{location.StartLinePosition.Line + 1}: {message}");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            Console.WriteLine($"::error file={Escape(location.Path)},line={location.StartLinePosition.Line + 1}::{Escape(message)}");
        }
    }

    string summary = $"{flavor}: checked {result.Monsters} concrete monsters and {result.Pairs} HP pairs; "
        + $"{result.Issues.Count} error(s). ascensionValue and fallbackValue each require MinInitialHp <= MaxInitialHp.";
    Console.WriteLine(summary);
    if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summaryPath)
    {
        File.AppendAllText(summaryPath, summary + Environment.NewLine);
    }
}

return results.Any(result => result.Issues.Count > 0) ? 1 : 0;

static string Escape(string value) =>
    value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A").Replace(",", "%2C").Replace(":", "%3A");
