using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args is ["--source", var sourceRoot, var output])
{
    // 点开头的目录（.git、.godot、.claude/worktrees 里的旧副本等）和构建/工具目录都不是模组源码。
    var excluded = new HashSet<string> { "build", "bin", "obj", "tools", "loader", "verification", "tests", "libs", "dist" };
    IEnumerable<string> Files(string dir) => Directory.EnumerateFiles(dir, "*.cs").Concat(Directory.EnumerateDirectories(dir)
        .Where(d => !excluded.Contains(Path.GetFileName(d)) && !Path.GetFileName(d).StartsWith('.')).SelectMany(Files));
    string[] files = Files(sourceRoot).Order(StringComparer.Ordinal).ToArray();
    foreach (string target in new[] { "0.107.1", "0.111.0" })
    {
        var numbers = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var tokens = new SortedDictionary<string, StringBuilder>(StringComparer.Ordinal);
        var options = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["STS2_" + target.Replace('.', '_'), "GODOT", "NET9_0"]);
        foreach (string file in files)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), options);
            string fileKey = Path.GetRelativePath(sourceRoot, file).Replace(".Legacy.cs", ".cs").Replace(".Official.cs", ".cs");
            foreach (var token in tree.GetRoot().DescendantTokens())
            {
                var member = token.Parent!.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault(m => m is not BaseTypeDeclarationSyntax);
                string name = member switch { null => "<file>", MethodDeclarationSyntax m => m.Identifier.Text, ConstructorDeclarationSyntax => ".ctor", PropertyDeclarationSyntax p => p.Identifier.Text, FieldDeclarationSyntax f => string.Join(",", f.Declaration.Variables.Select(v => v.Identifier.Text)), EnumMemberDeclarationSyntax e => e.Identifier.Text, _ => member.Kind().ToString() };
                var owner = (SyntaxNode?)member ?? token.Parent!;
                string key = fileKey + "|" + string.Join(".", owner.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().Where(t => t != owner).Reverse().Select(t => t.Identifier.Text)) + "." + name;
                // 完整 token 流：数值以外的差异（例如 (uint) 截断、换了调用的 API）也要能按成员看出来。
                if (!tokens.TryGetValue(key, out var text)) tokens[key] = text = new StringBuilder();
                text.Append(token.Text).Append(' ');
                if (!token.IsKind(SyntaxKind.NumericLiteralToken)) continue;
                if (!numbers.TryGetValue(key, out var values)) numbers[key] = values = [];
                values.Add(token.Text);
            }
        }
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "source_numbers." + target + ".txt"), numbers.Select(p => p.Key + "\t" + string.Join(",", p.Value)));
        File.WriteAllLines(Path.Combine(output, "source_tokens." + target + ".txt"), tokens.Select(p => p.Key + "\t" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Value.ToString()))).ToLowerInvariant()));
    }
    return 0;
}

// 仅解析元数据和 IL，不执行模组、Harmony 的 Prepare/TargetMethod 或游戏初始化；动态目标由 tools/PatchTargetCheck 执行解析。
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
// 转义字符串和字符，确保换行、制表符等常量不破坏一字段一行的清单边界。
string ConstantValue(object? value) => value is string or char ? System.Text.Json.JsonSerializer.Serialize(Value(value)) : Value(value);
Write("constant_fields", types.Where(t => !t.FullName.Contains('<')).SelectMany(t => t.Fields.Where(f => f.HasConstant).Select(f => $"{t.FullName}.{f.Name}\t{f.FieldType}\t{ConstantValue(f.Constant)}")).Order(StringComparer.Ordinal));

// ---- Harmony 2.4.2 的静态目标解析（PatchClassProcessor + AttributePatch.Create + PatchTools.GetOriginalMethod）----
string[] patchKinds = ["Prefix", "Postfix", "Transpiler", "Finalizer", "ReversePatch", "InnerPrefix", "InnerPostfix"];
string? PatchKind(MethodDefinition m) => patchKinds.FirstOrDefault(k => m.Name == k || m.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.Harmony" + k));
bool IsAux(MethodDefinition m, string kind) => m.Name == kind || m.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.Harmony" + kind);
bool IsHarmonyAttribute(TypeReference attributeType)
{
    for (TypeReference? b = attributeType.Resolve()?.BaseType; b != null; b = b.Resolve()?.BaseType)
        if (b.FullName == "HarmonyLib.HarmonyAttribute") return true;
    return false;
}
// 与 src/infra/patching/PatchClassRules 相同的安装范围：类（含基类）上有 Harmony 特性，或 [LibraryPatch(Optional = true)]。
bool Installed(TypeDefinition t)
{
    for (TypeDefinition? c = t; c != null; c = c.BaseType?.Resolve())
        if (c.CustomAttributes.Any(a => IsHarmonyAttribute(a.AttributeType))) return true;
    return t.CustomAttributes.Any(a => a.AttributeType.Name == "LibraryPatchAttribute" && a.Properties.Any(p => p.Name == "Optional" && p.Argument.Value is true));
}
var patches = types.Where(t => t.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch") || t.Methods.Any(m => PatchKind(m) is "Prefix" or "Postfix" or "Transpiler" or "Finalizer")).ToArray();
Write("patch_ids", patches.Select(t => t.FullName).Order(StringComparer.Ordinal));

TypeDefinition? TypeByName(string name)
{
    string full = name.Split(',')[0].Trim().Replace('+', '/');
    foreach (var m in new[] { module }.Concat(module.AssemblyReferences.Select(r => { try { return resolver.Resolve(r).MainModule; } catch { return null; } }).OfType<ModuleDefinition>()))
        if (m.GetType(full) is { } found) return found;
    return null;
}
// HarmonyPatch 各构造重载按参数类型区分：Type、string、MethodType、Type[]、ArgumentType[]；(string, string[, MethodType]) 是“类型名、方法名”。
Info Parse(CustomAttribute attribute)
{
    var info = new Info();
    string[] strings = attribute.ConstructorArguments.Where(a => a.Type.FullName == "System.String").Select(a => (string)a.Value).ToArray();
    int[]? variations = null;
    foreach (var a in attribute.ConstructorArguments)
    {
        switch (a.Type.FullName)
        {
            case "System.Type": info.Type = (TypeReference)a.Value; break;
            case "HarmonyLib.MethodType": info.MethodType = Convert.ToInt32(a.Value); break;
            case "System.Type[]": info.Arguments = a.Value is CustomAttributeArgument[] items ? items.Select(i => ((TypeReference)i.Value).FullName).ToArray() : null; break;
            case "HarmonyLib.ArgumentType[]": variations = a.Value is CustomAttributeArgument[] v ? v.Select(i => Convert.ToInt32(i.Value)).ToArray() : null; break;
        }
    }
    if (strings.Length == 2) { info.Type = TypeByName(strings[0]); info.TypeName = strings[0]; info.Name = strings[1]; }
    else if (strings.Length == 1) info.Name = strings[0];
    if (info.Arguments != null && variations != null)
        for (int i = 0; i < info.Arguments.Length && i < variations.Length; i++)
            info.Arguments[i] += variations[i] switch { 1 or 2 => "&", 3 => "*", _ => "" };
    return info;
}
Info Merge(Info master, Info detail) => new() { Type = detail.Type ?? master.Type, TypeName = detail.TypeName ?? master.TypeName, Name = detail.Name ?? master.Name, MethodType = detail.MethodType ?? master.MethodType, Arguments = detail.Arguments ?? master.Arguments };
Info FromAttributes(IEnumerable<CustomAttribute> attributes) => attributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").Select(Parse).Aggregate(new Info(), Merge);
string Signature(Info i) => $"{i.Type?.FullName ?? i.TypeName ?? "?"}.{i.Name}({(i.Arguments == null ? "未给参数表" : string.Join(",", i.Arguments))}) MethodType={i.MethodType ?? 0}";
// AccessTools.DeclaredMethod：只看声明类型本身（不沿基类找）；没给参数表时有重载即 AmbiguousMatchException。
// 给了参数表时按完整类型名精确匹配；DefaultBinder 能接受的可赋值匹配也当作错误，免得目标随重载变化而漂移。
MethodDefinition DeclaredMethod(TypeDefinition type, string name, string[]? arguments)
{
    var candidates = type.Methods.Where(m => m.Name == name).ToArray();
    if (arguments == null)
        return candidates.Length switch { 0 => throw new MissingMemberException("找不到"), 1 => candidates[0], _ => throw new AmbiguousMatchException($"未给参数表而有 {candidates.Length} 个重载，Harmony 会抛 AmbiguousMatchException") };
    var exact = candidates.Where(m => m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(arguments)).ToArray();
    return exact.Length == 1 ? exact[0] : throw new MissingMemberException(candidates.Length == 0 ? "找不到" : $"{candidates.Length} 个同名重载都不精确匹配参数表");
}
MethodDefinition StateMachineMoveNext(MethodDefinition method, string attribute)
{
    var a = method.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == attribute) ?? throw new MissingMemberException("不是 " + attribute + " 方法");
    return ((TypeReference)a.ConstructorArguments[0].Value).Resolve().Methods.Single(m => m.Name == "MoveNext");
}
MethodDefinition Original(Info i)
{
    if (i.Type == null) throw new MissingMemberException(i.TypeName != null ? "找不到类型 " + i.TypeName : "没有目标类型");
    TypeDefinition type = i.Type.Resolve() ?? throw new MissingMemberException("无法解析类型");
    switch (i.MethodType ?? 0)
    {
        case 0 or 5 or 6:
            if (string.IsNullOrEmpty(i.Name)) throw new MissingMemberException("没有方法名");
            var method = DeclaredMethod(type, i.Name, i.Arguments);
            return (i.MethodType ?? 0) switch { 5 => StateMachineMoveNext(method, "System.Runtime.CompilerServices.IteratorStateMachineAttribute"), 6 => StateMachineMoveNext(method, "System.Runtime.CompilerServices.AsyncStateMachineAttribute"), _ => method };
        case 1 or 2:
            if (string.IsNullOrEmpty(i.Name)) throw new NotSupportedException("索引器目标未实现离线解析");
            var properties = type.Properties.Where(p => p.Name == i.Name).ToArray();
            if (properties.Length > 1) throw new AmbiguousMatchException("同名属性不唯一");
            var accessor = properties.Length == 0 ? null : i.MethodType == 1 ? properties[0].GetMethod : properties[0].SetMethod;
            return accessor ?? throw new MissingMemberException("找不到" + (i.MethodType == 1 ? "取值器" : "赋值器"));
        case 3:
            return type.Methods.SingleOrDefault(m => m.Name == ".ctor" && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(i.Arguments ?? [])) ?? throw new MissingMemberException("找不到构造函数");
        case 4:
            return type.Methods.SingleOrDefault(m => m.Name == ".cctor") ?? throw new MissingMemberException("找不到静态构造函数");
        default:
            throw new NotSupportedException("MethodType " + i.MethodType + " 未实现离线解析");
    }
}

// ---- 参数注入（MethodCreatorTools.EmitCallParameter）：对精确目标逐个核对 ----
TypeReference Substitute(TypeReference type, GenericInstanceType? context)
{
    if (context == null) return type;
    if (type is GenericParameter { Type: GenericParameterType.Type } p && p.Position < context.GenericArguments.Count) return context.GenericArguments[p.Position];
    if (type is GenericInstanceType g)
    {
        var copy = new GenericInstanceType(g.ElementType);
        foreach (var a in g.GenericArguments) copy.GenericArguments.Add(Substitute(a, context));
        return copy;
    }
    return type;
}
bool Assignable(TypeReference to, TypeReference from)
{
    if (to.FullName == from.FullName || to.FullName == "System.Object") return true;
    var queue = new Queue<TypeReference>([from]);
    var seen = new HashSet<string>();
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!seen.Add(current.FullName)) continue;
        if (current.FullName == to.FullName) return true;
        TypeDefinition? definition;
        try { definition = current.Resolve(); } catch { definition = null; }
        if (definition == null) continue;
        var context = current as GenericInstanceType;
        if (definition.BaseType != null) queue.Enqueue(Substitute(definition.BaseType, context));
        foreach (var i in definition.Interfaces) queue.Enqueue(Substitute(i.InterfaceType, context));
    }
    return false;
}
TypeReference Element(TypeReference t) => t is ByReferenceType r ? r.ElementType : t;
(string? Original, int Index, string? NewName) ArgumentAttribute(CustomAttribute a) => a.ConstructorArguments.Select(x => x.Value).ToArray() switch
{
    [string name] => (name, -1, null),
    [int index] => (null, index, null),
    [string name, string newName] => (name, -1, newName),
    [int index, string name] => (null, index, name),
    _ => (null, -1, null),
};
IEnumerable<CustomAttribute> HarmonyArguments(Mono.Cecil.ICustomAttributeProvider p) => p.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyArgument");
FieldDefinition? FindField(TypeDefinition? type, string name)
{
    for (; type != null; type = type.BaseType?.Resolve())
        if (type.Fields.FirstOrDefault(f => f.Name == name) is { } field) return field;
    return null;
}
IEnumerable<string> Injection(MethodDefinition patch, string kind, MethodDefinition original)
{
    string[] names = original.Parameters.Select(p => p.Name).ToArray();
    var classAndMethod = HarmonyArguments(patch).Concat(HarmonyArguments(patch.DeclaringType)).Select(ArgumentAttribute).ToArray();
    IEnumerable<ParameterDefinition> parameters = patch.Parameters;
    if (kind == "Postfix" && patch.ReturnType.FullName != "System.Void" && patch.Parameters.Count > 0 && patch.Parameters[0].ParameterType.FullName == patch.ReturnType.FullName) parameters = parameters.Skip(1);
    foreach (var parameter in parameters)
    {
        var own = HarmonyArguments(parameter).Select(ArgumentAttribute).Cast<(string? Original, int Index, string? NewName)?>().FirstOrDefault();
        string realName = own != null ? own.Value.Original ?? parameter.Name : classAndMethod.FirstOrDefault(a => a.Original == parameter.Name) is { Original: not null } m && !string.IsNullOrEmpty(m.NewName) ? m.NewName! : parameter.Name;
        TypeReference element = Element(parameter.ParameterType);
        switch (realName)
        {
            case "__instance":
                if (original.IsStatic) yield return "__instance 用在静态原方法上，恒为 null";
                else if (!Assignable(element, original.DeclaringType)) yield return $"__instance 类型 {element.FullName} 不兼容 {original.DeclaringType.FullName}";
                continue;
            case "__result":
                if (original.ReturnType.FullName == "System.Void") yield return "__result 用在无返回值的原方法上";
                else if (!Assignable(original.ReturnType is ByReferenceType ? parameter.ParameterType : element, original.ReturnType)) yield return $"__result 类型 {parameter.ParameterType.FullName} 不能接收 {original.ReturnType.FullName}";
                continue;
            case "__resultRef":
                if (original.ReturnType is not ByReferenceType) yield return "__resultRef 用在非 ref 返回的原方法上";
                continue;
            case "__originalMethod" or "__args" or "__state" or "__exception" or "__runOriginal":
                continue;
        }
        if (realName.StartsWith("___", StringComparison.Ordinal))
        {
            string fieldName = realName[3..];
            var field = fieldName.All(char.IsDigit) ? original.DeclaringType.Fields.ElementAtOrDefault(int.Parse(fieldName)) : FindField(original.DeclaringType, fieldName);
            if (field == null) yield return "字段不存在 " + realName;
            else if (!Assignable(element, field.FieldType)) yield return $"字段 {realName} 类型 {element.FullName} 不兼容 {field.FieldType.FullName}";
            continue;
        }
        int index;
        if (realName.StartsWith("__", StringComparison.Ordinal))
        {
            if (!int.TryParse(realName[2..], out index) || index < 0 || index >= names.Length) { yield return "无效的参数序号 " + realName; continue; }
        }
        else
        {
            string? lookup = own is { } o ? (!string.IsNullOrEmpty(o.Original) ? o.Original : o.Index >= 0 && o.Index < names.Length ? names[o.Index] : null) : null;
            if (lookup == null && classAndMethod.FirstOrDefault(a => a.Original == parameter.Name) is { Original: not null } renamed)
                lookup = !string.IsNullOrEmpty(renamed.NewName) ? renamed.NewName : renamed.Index >= 0 && renamed.Index < names.Length ? names[renamed.Index] : null;
            index = Array.IndexOf(names, lookup ?? parameter.Name);
            if (index < 0) { yield return "原方法没有参数 " + realName; continue; }
        }
        var originalElement = Element(original.Parameters[index].ParameterType);
        if (!Assignable(element, originalElement)) yield return $"参数 {parameter.Name} 类型 {element.FullName} 不兼容原方法第 {index} 个参数 {originalElement.FullName}";
    }
}

var targets = new List<string>();
var problems = new List<string>();
int dynamicClasses = 0;
foreach (TypeDefinition patch in patches.Where(Installed))
{
    var patchMethods = patch.Methods.Select(m => (Method: m, Kind: PatchKind(m)!)).Where(x => x.Kind != null).ToArray();
    if (patch.Methods.Any(m => IsAux(m, "TargetMethod") || IsAux(m, "TargetMethods")) || patch.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatchAll"))
    {
        targets.Add(patch.FullName + "\tdynamic/manual");
        dynamicClasses++;
        continue;
    }
    // GetFromType 用 GetCustomAttributes(true)：先本类、再基类，依次合并；方法级特性再覆盖类级。
    var classAttributes = new List<CustomAttribute>();
    for (TypeDefinition? c = patch; c != null; c = c.BaseType?.Resolve()) classAttributes.AddRange(c.CustomAttributes);
    Info container = FromAttributes(classAttributes);
    foreach (var (method, kind) in patchMethods)
    {
        if (kind is "ReversePatch" or "InnerPrefix" or "InnerPostfix") { problems.Add($"{patch.FullName}.{method.Name}\t不支持的补丁种类 {kind}"); continue; }
        if (!method.IsStatic) { problems.Add($"{patch.FullName}.{method.Name}\t补丁方法必须是静态的"); continue; }
        Info info = Merge(container, FromAttributes(method.CustomAttributes));
        MethodDefinition original;
        try { original = Original(info); }
        catch (Exception e) { problems.Add($"{patch.FullName}.{method.Name}\tMISSING {Signature(info)}：{e.Message}"); continue; }
        targets.Add($"{patch.FullName}.{method.Name}\t{kind}\t{original.FullName}");
        if (kind == "Transpiler") continue;
        foreach (string problem in Injection(method, kind, original))
            problems.Add($"{patch.FullName}.{method.Name}\tINJECTION {problem} in {original.FullName}");
    }
}
Write("patch_targets", targets.Order(StringComparer.Ordinal));
Write("missing_patch_targets", problems.Order(StringComparer.Ordinal));
// 编译产物里的数值常量逐方法保留。源码相同时也可能不同：原版枚举或常量在两个版本取值不同，会按目标编进 IL。
// compare_dual.py 归一编译器生成名里的序号后比较，差异要么落在源码按目标不同的类型里，要么登记审查理由。
Write("il_numbers", types.SelectMany(t => t.Methods.Where(m => m.HasBody).Select(m => new { Method=m, Numbers=m.Body.Instructions.Where(i => i.OpCode.Code.ToString().StartsWith("Ldc_")).Select(i => i.OpCode.Code + ":" + Value(i.Operand)).ToArray() })).Where(x => x.Numbers.Length > 0).Select(x => $"{x.Method.DeclaringType.FullName}::{x.Method.Name}\t{string.Join(",", x.Numbers)}").Order(StringComparer.Ordinal));
Console.WriteLine($"{module.Assembly.Name.Name}: {types.Count(t => !t.IsAbstract && Model(t))} models, {patches.Length} patch classes ({dynamicClasses} dynamic, see PatchTargetCheck), {problems.Count} unresolved static targets or injection problems");
foreach (string problem in problems.Order(StringComparer.Ordinal)) Console.Error.WriteLine("  " + problem);
return problems.Count == 0 ? 0 : 1;

sealed class Info { public TypeReference? Type; public string? TypeName; public string? Name; public int? MethodType; public string[]? Arguments; }
sealed class AmbiguousMatchException(string message) : Exception(message);
