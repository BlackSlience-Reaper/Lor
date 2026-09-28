using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Fails when code outside src/interop/ reaches members by name through reflection. Vanilla non-public members go through
// src/interop/VanillaPrivate.cs, so a game update shows every missing member in one startup summary.
//
// Any call to a by-name reflection API counts, whatever its argument looks like (literal, constant, variable, wrapped
// over many lines). Exempt: src/interop/ and src/compat/ (version shims), and Harmony target resolution — code whose
// enclosing method is TargetMethod(s)/Prepare/Cleanup or carries the matching Harmony attribute (LibraryPatcher reports
// those failures at install time). Everything else must be listed in the allowlist with a reason.
//
// usage: PrivateAccessCheck <src-dir> <allowlist>
//        PrivateAccessCheck --self-test <fixtures-dir>     (fixtures/expected.txt lists the findings)
if (args is ["--self-test", var fixtures])
{
    List<string> found = Scan(fixtures, exemptDirectories: []).Select(static f => f.ToString()).Order(StringComparer.Ordinal).ToList();
    List<string> expected = File.ReadAllLines(Path.Combine(fixtures, "expected.txt"))
        .Where(static line => line.Length > 0 && !line.StartsWith('#')).Order(StringComparer.Ordinal).ToList();
    if (found.SequenceEqual(expected))
    {
        Console.WriteLine($"private access check self-test: {found.Count} expected finding(s)");
        return 0;
    }

    Console.WriteLine("private access check self-test failed.\n  expected:\n    " + string.Join("\n    ", expected)
                      + "\n  found:\n    " + string.Join("\n    ", found));
    return 1;
}

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: PrivateAccessCheck <src-dir> <allowlist> | --self-test <fixtures-dir>");
    return 2;
}

List<AllowEntry> allow = LoadAllowlist(args[1]);
var used = new HashSet<AllowEntry>();
var violations = new List<Finding>();
foreach (Finding finding in Scan(args[0], exemptDirectories: ["interop/", "compat/"]))
{
    AllowEntry? entry = allow.FirstOrDefault(e => e.Path == finding.Path && e.Member == finding.Member && e.Api == finding.Api);
    if (entry != null)
    {
        used.Add(entry);
    }
    else
    {
        violations.Add(finding);
    }
}

List<AllowEntry> stale = allow.Where(e => !used.Contains(e)).ToList();
if (violations.Count > 0)
{
    Console.WriteLine("reflection by name outside src/interop/ (add a VanillaPrivate accessor, or allowlist it with a reason):");
    violations.ForEach(v => Console.WriteLine("  src/" + v));
}

if (stale.Count > 0)
{
    Console.WriteLine("allowlist entries that no longer match anything:");
    stale.ForEach(e => Console.WriteLine($"  {e.Path}\t{e.Member}\t{e.Api}"));
}

if (violations.Count > 0 || stale.Count > 0)
{
    return 1;
}

Console.WriteLine($"no reflection by name outside src/interop/ ({allow.Count} allowlisted)");
return 0;

static List<AllowEntry> LoadAllowlist(string path)
{
    var entries = new List<AllowEntry>();
    foreach (string line in File.ReadAllLines(path))
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
        {
            continue;
        }

        string[] parts = line.Split('\t');
        if (parts.Length < 4 || string.IsNullOrWhiteSpace(parts[3]))
        {
            throw new InvalidDataException("allowlist entry needs path, member, api and a reason: " + line);
        }

        entries.Add(new AllowEntry(parts[0].Trim(), parts[1].Trim(), parts[2].Trim()));
    }

    return entries;
}

static IEnumerable<Finding> Scan(string root, string[] exemptDirectories)
{
    foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        if (exemptDirectories.Any(relative.StartsWith))
        {
            continue;
        }

        SyntaxNode tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
        HashSet<string> stringConstants = tree.DescendantNodes().OfType<VariableDeclarationSyntax>()
            .Where(IsStringConstant)
            .SelectMany(static d => d.Variables).Select(static v => v.Identifier.Text).ToHashSet();

        foreach (InvocationExpressionSyntax call in tree.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string? api = ReflectionApi(call, stringConstants);
            if (api == null || IsTargetResolution(call))
            {
                continue;
            }

            int line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new Finding(relative, line, api, EnclosingMember(call));
        }
    }
}

// const string locals and const/readonly string fields: identifiers that carry a fixed member name.
static bool IsStringConstant(VariableDeclarationSyntax declaration)
{
    if (declaration.Type is not PredefinedTypeSyntax { Keyword.Text: "string" })
    {
        return false;
    }

    return declaration.Parent switch
    {
        LocalDeclarationStatementSyntax local => local.IsConst,
        FieldDeclarationSyntax field => field.Modifiers.Any(static m => m.Text is "const" or "readonly"),
        _ => false,
    };
}

// The by-name reflection API this call uses, or null.
static string? ReflectionApi(InvocationExpressionSyntax call, HashSet<string> stringConstants)
{
    string name;
    string receiver;
    switch (call.Expression)
    {
        case MemberAccessExpressionSyntax access:
            name = access.Name.Identifier.Text;
            receiver = access.Expression.ToString();
            break;
        // receiver?.Method(...): the receiver is on the enclosing conditional access.
        case MemberBindingExpressionSyntax binding:
            name = binding.Name.Identifier.Text;
            receiver = call.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression.ToString() ?? "";
            break;
        default:
            return null;
    }

    int argumentCount = call.ArgumentList.Arguments.Count;
    if (receiver is "AccessTools" or "HarmonyLib.AccessTools"
        && name is "Field" or "DeclaredField" or "Property" or "DeclaredProperty" or "PropertyGetter" or "PropertySetter"
            or "DeclaredPropertyGetter" or "DeclaredPropertySetter" or "Method" or "DeclaredMethod" or "FieldRefAccess"
            or "StaticFieldRefAccess" or "TypeByName" or "Inner" or "EventInfo")
    {
        return "AccessTools." + name;
    }

    if (receiver is "Traverse" or "HarmonyLib.Traverse" && name == "Create")
    {
        return "Traverse.Create";
    }

    // Type/Assembly lookups by name. GetType() without arguments is object.GetType and not a lookup.
    if (name is "GetField" or "GetProperty" or "GetMethod" or "GetMember" or "GetNestedType" or "GetEvent" or "InvokeMember"
        && argumentCount > 0
        || name == "GetType" && argumentCount > 0)
    {
        return "." + name;
    }

    // Expression trees that bind a member by name: Property/Field/PropertyOrField(expr, "Name"), Call(expr, "Name", ...).
    if (receiver is "Expression" or "System.Linq.Expressions.Expression"
        && name is "Property" or "Field" or "PropertyOrField" or "Call"
        && argumentCount >= 2
        && IsString(call.ArgumentList.Arguments[1].Expression, stringConstants))
    {
        return "Expression." + name;
    }

    return null;
}

static bool IsString(ExpressionSyntax expression, HashSet<string> stringConstants) => expression switch
{
    LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
    InterpolatedStringExpressionSyntax => true,
    InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } => true,
    IdentifierNameSyntax identifier => stringConstants.Contains(identifier.Identifier.Text),
    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
        IsString(binary.Left, stringConstants) || IsString(binary.Right, stringConstants),
    _ => false,
};

// Harmony target resolution: the call sits (possibly inside a lambda) in a method Harmony calls to pick targets.
static bool IsTargetResolution(SyntaxNode node)
{
    MethodDeclarationSyntax? method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
    if (method == null)
    {
        return false;
    }

    if (method.Identifier.Text is "TargetMethod" or "TargetMethods" or "Prepare" or "Cleanup")
    {
        return true;
    }

    return method.AttributeLists.SelectMany(static list => list.Attributes)
        .Any(static attribute => attribute.Name.ToString() is "HarmonyTargetMethod" or "HarmonyTargetMethods" or "HarmonyPrepare"
            or "HarmonyCleanup");
}

// "Type.Member" of the declaration that contains the call (method, local function's method, accessor, field, ctor).
static string EnclosingMember(SyntaxNode node)
{
    string member = "?";
    foreach (SyntaxNode ancestor in node.Ancestors())
    {
        string? name = ancestor switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            ConstructorDeclarationSyntax => ".ctor",
            PropertyDeclarationSyntax property => property.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            FieldDeclarationSyntax field => field.Declaration.Variables.First().Identifier.Text,
            EventFieldDeclarationSyntax @event => @event.Declaration.Variables.First().Identifier.Text,
            _ => null,
        };
        if (name != null)
        {
            member = name;
        }

        if (ancestor is BaseTypeDeclarationSyntax type)
        {
            return type.Identifier.Text + "." + member;
        }
    }

    return member;
}

internal sealed record Finding(string Path, int Line, string Api, string Member)
{
    public override string ToString() => $"{Path}:{Line}: {Api} in {Member}";
}

internal sealed record AllowEntry(string Path, string Member, string Api);
