using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args is ["--source", var sourceRoot, var output])
{
    var excluded = new HashSet<string> { "build", "bin", "obj", "tools", "loader", "verification", ".git", ".godot", "tests", ".tests", "libs", "dist" };
    IEnumerable<string> Files(string dir) => Directory.EnumerateFiles(dir, "*.cs").Concat(Directory.EnumerateDirectories(dir).Where(d => !excluded.Contains(Path.GetFileName(d))).SelectMany(Files));
    foreach (string target in new[] { "0.107.1", "0.111.0" })
    {
        var result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var options = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["STS2_" + target.Replace('.', '_'), "GODOT", "NET9_0"]);
        foreach (string file in Files(sourceRoot).Order(StringComparer.Ordinal))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), options);
            foreach (var token in tree.GetRoot().DescendantTokens().Where(t => t.IsKind(SyntaxKind.NumericLiteralToken)))
            {
                var member = token.Parent!.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().First(m => m is not BaseTypeDeclarationSyntax);
                string name = member switch { MethodDeclarationSyntax m => m.Identifier.Text, ConstructorDeclarationSyntax c => ".ctor", PropertyDeclarationSyntax p => p.Identifier.Text, FieldDeclarationSyntax f => string.Join(",",f.Declaration.Variables.Select(v => v.Identifier.Text)), EnumMemberDeclarationSyntax e => e.Identifier.Text, _ => member.Kind().ToString() };
                string key = Path.GetRelativePath(sourceRoot, file).Replace(".Legacy.cs", ".cs").Replace(".Official.cs", ".cs") + "|" + string.Join(".", member.Ancestors().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.Text)) + "." + name;
                if (!result.TryGetValue(key, out var values)) result[key] = values = [];
                values.Add(token.Text);
            }
        }
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "source_numbers." + target + ".txt"), result.Select(p => p.Key + "\t" + string.Join(",",p.Value)));
    }
    return 0;
}

// 仅解析元数据和 IL，不执行模组、Harmony 的 Prepare/TargetMethod 或游戏初始化。
if (args.Length < 3) throw new ArgumentException("用法：DualTargetAudit <dll> <输出目录> <引用目录>...");
var resolver = new DefaultAssemblyResolver();
foreach (string directory in args.Skip(2).Prepend(Path.GetDirectoryName(Path.GetFullPath(args[0]))!).Append(Path.GetDirectoryName(typeof(object).Assembly.Location)!)) resolver.AddSearchDirectory(directory);
using var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
Directory.CreateDirectory(args[1]);
IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> roots) => roots.SelectMany(t => new[] { t }.Concat(Flatten(t.NestedTypes)));
TypeDefinition[] types = Flatten(module.Types).ToArray();
bool Model(TypeDefinition t)
{
    for (TypeReference? p = t.BaseType; p != null; p = p.Resolve()?.BaseType)
        if (p.FullName == "MegaCrit.Sts2.Core.Models.AbstractModel") return true;
    return false;
}
void Write(string name, IEnumerable<string> lines) => File.WriteAllText(Path.Combine(args[1], name + ".txt"), string.Join("\n", lines) + "\n", new UTF8Encoding(false));
string Value(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
Write("models", types.Where(t => !t.IsAbstract && Model(t)).Select(t => t.FullName).Order(StringComparer.Ordinal));
string AttributeValue(CustomAttributeArgument a) => a.Value is CustomAttributeArgument[] items ? "[" + string.Join(",", items.Select(AttributeValue)) + "]" : a.Type.FullName + ":" + Value(a.Value);
string SavedAttribute(PropertyDefinition p)
{
    var a = p.CustomAttributes.Single(a => a.AttributeType.Name == "SavedPropertyAttribute");
    return string.Join(",", a.ConstructorArguments.Select(AttributeValue)) + ";" + string.Join(",",a.Fields.Select(f => f.Name + "=" + AttributeValue(f.Argument)).Concat(a.Properties.Select(f => f.Name + "=" + AttributeValue(f.Argument))));
}
Write("saved_property_order", types.Where(t => !t.FullName.Contains('<')).SelectMany(t => t.Properties.Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "SavedPropertyAttribute")).Select((p, index) => $"{t.FullName}\t{index}\t{p.Name}\t{p.PropertyType.FullName}\t{SavedAttribute(p)}" )).Order(StringComparer.Ordinal));
Write("constant_fields", types.Where(t => !t.FullName.Contains('<')).SelectMany(t => t.Fields.Where(f => f.HasConstant).Select(f => $"{t.FullName}.{f.Name}\t{f.FieldType}\t{Value(f.Constant)}")).Order(StringComparer.Ordinal));
var patches = types.Where(t => t.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch") || t.Methods.Any(m => m.CustomAttributes.Any(a => a.AttributeType.FullName.StartsWith("HarmonyLib.Harmony") && a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyTranspiler" or "HarmonyFinalizer"))).ToArray();
Write("patch_ids", patches.Select(t => t.FullName).Order(StringComparer.Ordinal));
var targets = new List<string>();
var problems = new List<string>();
foreach (TypeDefinition patch in patches)
{
    var attrs = patch.CustomAttributes.Where(a => a.AttributeType.Name == "HarmonyPatch").ToArray();
    TypeReference? targetType = null; string? name = null; string[]? parameters = null; int kind = 0;
    foreach (var attr in attrs)
        foreach (var arg in attr.ConstructorArguments)
        {
            if (arg.Value is TypeReference tr) targetType = tr;
            else if (arg.Type.FullName == "System.String") name = (string)arg.Value;
            else if (arg.Type.FullName == "HarmonyLib.MethodType") kind = (int)arg.Value;
            else if (arg.Type.FullName == "System.Type[]" && arg.Value is CustomAttributeArgument[] array) parameters = array.Select(a => ((TypeReference)a.Value).FullName).ToArray();
        }
    if (targetType == null || (name == null && kind is not (3 or 4)))
    {
        targets.Add(patch.FullName + "\tdynamic/manual"); continue;
    }
    name = kind switch { 1 => "get_" + name, 2 => "set_" + name, 3 => ".ctor", 4 => ".cctor", _ => name };
    var candidates = new List<MethodDefinition>();
    for (TypeDefinition? t = targetType.Resolve(); t != null; t = t.BaseType?.Resolve())
    {
        candidates.AddRange(t.Methods.Where(m => m.Name == name && (parameters == null || m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters))));
        if (candidates.Count > 0) break;
    }
    if (candidates.Count == 0) problems.Add($"{patch.FullName}\tMISSING {targetType.FullName}.{name}({string.Join(",", parameters ?? [])})");
    else
    {
        targets.Add($"{patch.FullName}\t" + string.Join(" | ", candidates.Select(m => m.FullName)));
        foreach (var method in patch.Methods.Where(m => m.Name is "Prefix" or "Postfix" or "Finalizer" || m.CustomAttributes.Any(a => a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyFinalizer")))
            foreach (var parameter in method.Parameters.Where(p => !p.Name.StartsWith("__") && !p.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyArgument")))
                if (!candidates.Any(c => c.Parameters.Any(p => p.Name == parameter.Name)))
                    problems.Add($"{patch.FullName}.{method.Name}\tMISSING PARAMETER {parameter.Name} in {targetType.FullName}.{name}");
    }
}
Write("patch_targets", targets.Order(StringComparer.Ordinal));
Write("missing_patch_targets", problems.Order(StringComparer.Ordinal));
// 编译产物里的数值常量逐方法保留；变体差异另行人工核对，不把编译器状态机的整数误称为玩法变化。
Write("il_numbers", types.SelectMany(t => t.Methods.Where(m => m.HasBody).Select(m => new { Method=m, Numbers=m.Body.Instructions.Where(i => i.OpCode.Code.ToString().StartsWith("Ldc_")).Select(i => i.OpCode.Code + ":" + Value(i.Operand)).ToArray() })).Where(x => x.Numbers.Length > 0).Select(x => $"{x.Method.DeclaringType.FullName}.{x.Method.Name}\t{string.Join(",", x.Numbers)}").Order(StringComparer.Ordinal));
Console.WriteLine($"{module.Assembly.Name.Name}: {types.Count(t => !t.IsAbstract && Model(t))} models, {patches.Length} patch classes, {problems.Count} unresolved static targets");
return problems.Count == 0 ? 0 : 1;
