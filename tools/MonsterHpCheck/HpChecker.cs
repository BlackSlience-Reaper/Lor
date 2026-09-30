using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed record Issue(Location Location, string Message);

internal sealed record CheckResult(int Monsters, int Pairs, List<Issue> Issues);

internal sealed class HpChecker
{
    private readonly CSharpCompilation _compilation;

    private readonly SyntaxTree[] _trees;

    public HpChecker(SyntaxTree[] trees)
    {
        _trees = trees;
        // Binding source symbols/constants does not require compiling the mod or loading its dependencies.
        _compilation = CSharpCompilation.Create("MonsterHpSourceCheck", trees,
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public CheckResult Check()
    {
        var issues = new List<Issue>();
        foreach (var diagnostic in _trees.SelectMany(tree => tree.GetDiagnostics())
                     .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            issues.Add(new Issue(diagnostic.Location, diagnostic.GetMessage()));
        }

        var types = _trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Select(node => (INamedTypeSymbol)Model(node).GetDeclaredSymbol(node)!)
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .Where(IsMonster)
            .OrderBy(type => type.ToDisplayString(), StringComparer.Ordinal);
        int monsters = 0;
        int pairs = 0;
        foreach (var type in types)
        {
            var min = FindProperty(type, "MinInitialHp");
            var max = FindProperty(type, "MaxInitialHp");
            if (!type.IsAbstract)
            {
                monsters++;
            }
            else if (min is null || max is null || min.IsAbstract || max.IsAbstract)
            {
                continue;
            }

            pairs++;
            try
            {
                if (min is null || max is null || min.IsAbstract || max.IsAbstract)
                {
                    throw new InvalidOperationException("Missing concrete MinInitialHp/MaxInitialHp definition.");
                }

                var evaluator = new Evaluator(this, type);
                var minima = evaluator.Property(min);
                var maxima = evaluator.Property(max);
                int comparisons = 0;
                foreach (var low in minima)
                {
                    foreach (var high in maxima)
                    {
                        var conditions = Merge(low.Conditions, high.Conditions);
                        if (conditions is null)
                        {
                            continue;
                        }

                        comparisons++;
                        bool valid = low.Number is { } a && high.Number is { } b
                            ? a <= b
                            : low.Atom is not null && low.Atom == high.Atom;
                        if (!valid)
                        {
                            string branch = conditions.Count == 0 ? "all ascensions" : string.Join(", ", conditions.Select(p => $"{p.Key}={p.Value}"));
                            issues.Add(new Issue(min.Locations[0],
                                $"{type.ToDisplayString()} [{branch}]: MinInitialHp={low.Display}, MaxInitialHp={high.Display}; "
                                + "cannot satisfy MinInitialHp <= MaxInitialHp."));
                        }
                    }
                }

                if (comparisons == 0)
                {
                    throw new InvalidOperationException("No comparable HP branches.");
                }
            }
            catch (InvalidOperationException error)
            {
                issues.Add(new Issue(type.Locations[0], $"{type.ToDisplayString()}: {error.Message}"));
            }
        }

        if (monsters == 0 || pairs == 0)
        {
            issues.Add(new Issue(Location.None, "No monster HP definitions found; refusing an empty check."));
        }

        return new CheckResult(monsters, pairs, issues);
    }

    private SemanticModel Model(SyntaxNode node) => _compilation.GetSemanticModel(node.SyntaxTree);

    private static bool IsMonster(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.Name is "MonsterModel" or "LibraryMonsterModel"
                || current.GetMembers("MinInitialHp").OfType<IPropertySymbol>().Any()
                || current.GetMembers("MaxInitialHp").OfType<IPropertySymbol>().Any())
            {
                return true;
            }
        }

        return false;
    }

    private static IPropertySymbol? FindProperty(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault() is { } property)
            {
                return property;
            }
        }

        return null;
    }

    private static Dictionary<string, string>? Merge(Dictionary<string, string> left, Dictionary<string, string> right)
    {
        var result = new Dictionary<string, string>(left);
        foreach (var (key, value) in right)
        {
            if (result.TryGetValue(key, out var previous) && previous != value)
            {
                return null;
            }

            result[key] = value;
        }

        return result;
    }

    private sealed record Value(int? Number, string? Atom, Dictionary<string, string> Conditions)
    {
        public string Display => Number?.ToString() ?? Atom!;
    }

    private sealed class Evaluator(HpChecker checker, INamedTypeSymbol monster, HashSet<ISymbol>? active = null)
    {
        private readonly HashSet<ISymbol> _active = active ?? new(SymbolEqualityComparer.Default);

        private readonly Dictionary<ISymbol, List<Value>> _arguments = new(SymbolEqualityComparer.Default);

        public List<Value> Property(IPropertySymbol property)
        {
            if (!_active.Add(property))
            {
                throw new InvalidOperationException($"Cyclic HP reference: {property}.");
            }

            try
            {
                var syntax = property.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as PropertyDeclarationSyntax;
                var getter = syntax?.AccessorList?.Accessors.SingleOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
                return Evaluate(syntax?.ExpressionBody?.Expression ?? getter?.ExpressionBody?.Expression
                    ?? ReturnExpression(getter?.Body));
            }
            finally
            {
                _active.Remove(property);
            }
        }

        private List<Value> Evaluate(ExpressionSyntax? expression)
        {
            if (expression is null)
            {
                throw new InvalidOperationException("Unsupported HP getter/helper body; expected an expression or a single return.");
            }

            var model = checker.Model(expression);
            if (model.GetConstantValue(expression) is { HasValue: true, Value: int constant })
            {
                return [new Value(constant, null, [])];
            }

            switch (expression)
            {
                case ParenthesizedExpressionSyntax parentheses:
                    return Evaluate(parentheses.Expression);
                case IdentifierNameSyntax:
                case MemberAccessExpressionSyntax:
                {
                    // Resolve canonical monster HP references from source, without invoking ModelDb.
                    if (expression is MemberAccessExpressionSyntax
                        {
                            Expression: InvocationExpressionSyntax
                            {
                                Expression: MemberAccessExpressionSyntax
                                {
                                    Expression: IdentifierNameSyntax { Identifier.ValueText: "ModelDb" },
                                    Name: GenericNameSyntax { Identifier.ValueText: "Monster" } generic
                                },
                                ArgumentList.Arguments.Count: 0
                            },
                            Name.Identifier.ValueText: "MinInitialHp" or "MaxInitialHp"
                        } reference && generic.TypeArgumentList.Arguments.Count == 1
                        && model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type is INamedTypeSymbol referencedType
                        && FindProperty(referencedType, reference.Name.Identifier.ValueText) is { } referencedProperty)
                    {
                        return new Evaluator(checker, referencedType, _active).Property(referencedProperty);
                    }

                    var symbol = model.GetSymbolInfo(expression).Symbol;
                    if (symbol is not null && _arguments.TryGetValue(symbol, out var argument))
                    {
                        return argument;
                    }

                    if (symbol is IPropertySymbol property)
                    {
                        if (!property.IsStatic && expression is MemberAccessExpressionSyntax
                            { Expression: not (ThisExpressionSyntax or BaseExpressionSyntax) })
                        {
                            throw new InvalidOperationException($"Unsupported HP property receiver: {expression}.");
                        }

                        if (expression is not MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }
                            && (property.IsVirtual || property.IsOverride || property.IsAbstract))
                        {
                            property = FindProperty(monster, property.Name) ?? property;
                        }

                        return Property(property);
                    }

                    // A mutable field has an unknown value, but the same field on both sides proves equality.
                    if (symbol is IFieldSymbol { IsConst: false } field
                        && (expression is IdentifierNameSyntax || expression is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax }))
                    {
                        return [new Value(null, FieldKey(field), [])];
                    }

                    break;
                }
                case InvocationExpressionSyntax call:
                    return Invoke(call);
                case ConditionalExpressionSyntax conditional:
                {
                    if (model.GetConstantValue(conditional.Condition) is { HasValue: true, Value: bool condition })
                    {
                        return Evaluate(condition ? conditional.WhenTrue : conditional.WhenFalse);
                    }

                    string key = Key(conditional.Condition);
                    return Branch(Evaluate(conditional.WhenTrue), key, "true")
                        .Concat(Branch(Evaluate(conditional.WhenFalse), key, "false")).ToList();
                }
                case SwitchExpressionSyntax choice:
                {
                    // Include the full partition in the key: different pattern sets must never be paired by arm index.
                    if (choice.Arms.Any(arm => arm.WhenClause is not null
                            || arm.Pattern is not (ConstantPatternSyntax or DiscardPatternSyntax)))
                    {
                        break;
                    }

                    string key = Key(choice.GoverningExpression) + " switch ["
                        + string.Join(",", choice.Arms.Select(arm => arm.Pattern.WithoutTrivia().ToString())) + "]";
                    return choice.Arms.SelectMany(arm => Branch(Evaluate(arm.Expression), key, arm.Pattern.ToString())).ToList();
                }
                case BinaryExpressionSyntax binary when binary.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression
                    or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression:
                {
                    var result = new List<Value>();
                    foreach (var left in Evaluate(binary.Left))
                    {
                        foreach (var right in Evaluate(binary.Right))
                        {
                            if (Merge(left.Conditions, right.Conditions) is not { } conditions)
                            {
                                continue;
                            }

                            if (left.Number is not { } a || right.Number is not { } b)
                            {
                                throw new InvalidOperationException($"Nonconstant HP arithmetic: {expression}.");
                            }

                            try
                            {
                                int number = binary.Kind() switch
                                {
                                    SyntaxKind.AddExpression => checked(a + b),
                                    SyntaxKind.SubtractExpression => checked(a - b),
                                    SyntaxKind.MultiplyExpression => checked(a * b),
                                    SyntaxKind.DivideExpression => a / b,
                                    _ => a % b
                                };
                                result.Add(new Value(number, null, conditions));
                            }
                            catch (ArithmeticException error)
                            {
                                throw new InvalidOperationException($"Invalid HP arithmetic: {expression} ({error.Message}).");
                            }
                        }
                    }

                    return result;
                }
            }

            throw new InvalidOperationException($"Unsupported/unresolved HP expression: {expression}. Extend the checker before merging.");
        }

        private List<Value> Invoke(InvocationExpressionSyntax call)
        {
            if (call.Expression is MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == "GetValueIfAscension"
                && access.Expression.ToString().Split('.').Last() == "AscensionHelper"
                && checker.Model(call).GetSymbolInfo(call).Symbol?.DeclaringSyntaxReferences.Length is not > 0)
            {
                var arguments = Bind(call, ["level", "ascensionValue", "fallbackValue"]);
                string key = "ascension " + Key(arguments[0]);
                return Branch(Evaluate(arguments[1]), key, "ascensionValue")
                    .Concat(Branch(Evaluate(arguments[2]), key, "fallbackValue")).ToList();
            }

            var symbol = checker.Model(call).GetSymbolInfo(call).Symbol as IMethodSymbol;
            var syntax = symbol?.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
            if (symbol is null || syntax is null || !symbol.IsStatic || !_active.Add(symbol))
            {
                throw new InvalidOperationException($"Unsupported/unresolved or recursive HP helper: {call}.");
            }

            try
            {
                var arguments = Bind(call, symbol.Parameters.Select(parameter => parameter.Name).ToArray());
                var values = arguments.Select(Evaluate).ToArray();
                for (int i = 0; i < symbol.Parameters.Length; i++)
                {
                    _arguments.Add(symbol.Parameters[i], values[i]);
                }

                return Evaluate(syntax.ExpressionBody?.Expression ?? ReturnExpression(syntax.Body));
            }
            finally
            {
                foreach (var parameter in symbol.Parameters)
                {
                    _arguments.Remove(parameter);
                }

                _active.Remove(symbol);
            }
        }

        private string FieldKey(IFieldSymbol field) =>
            field.IsStatic ? field.ToDisplayString() : $"{monster.ToDisplayString()}::{field.ToDisplayString()}";

        private string Key(ExpressionSyntax expression)
        {
            // Unknown calls/property getters may vary between reads. Only correlate side-effect-free conditions.
            if (expression.DescendantNodesAndSelf().Any(node => node is InvocationExpressionSyntax
                    or AssignmentExpressionSyntax or PostfixUnaryExpressionSyntax or ObjectCreationExpressionSyntax
                    or ImplicitObjectCreationExpressionSyntax or AwaitExpressionSyntax
                    || node.IsKind(SyntaxKind.PreIncrementExpression) || node.IsKind(SyntaxKind.PreDecrementExpression)))
            {
                throw new InvalidOperationException($"Unsupported HP branch condition: {expression}.");
            }

            return string.Join(" ", expression.DescendantTokens().Select(token =>
            {
                if (token.Parent is IdentifierNameSyntax identifier)
                {
                    var symbol = checker.Model(identifier).GetSymbolInfo(identifier).Symbol;
                    if (symbol is IPropertySymbol)
                    {
                        throw new InvalidOperationException($"Unsupported HP branch property: {expression}.");
                    }

                    if (symbol is not null && _arguments.TryGetValue(symbol, out var values))
                    {
                        return string.Join("|", values.Select(value => value.Display + string.Join(",", value.Conditions)));
                    }

                    if (symbol is IFieldSymbol field)
                    {
                        return FieldKey(field);
                    }

                    return symbol?.ToDisplayString() ?? token.Text;
                }

                return token.Text;
            }));
        }

        private static ExpressionSyntax? ReturnExpression(BlockSyntax? body) =>
            body?.Statements is [ReturnStatementSyntax statement] ? statement.Expression : null;

        private static ExpressionSyntax[] Bind(InvocationExpressionSyntax call, string[] names)
        {
            var result = new ExpressionSyntax?[names.Length];
            for (int i = 0; i < call.ArgumentList.Arguments.Count; i++)
            {
                var argument = call.ArgumentList.Arguments[i];
                int index = argument.NameColon is null ? i : Array.IndexOf(names, argument.NameColon.Name.Identifier.ValueText);
                if (index < 0 || index >= result.Length || result[index] is not null || argument.RefKindKeyword.RawKind != 0)
                {
                    throw new InvalidOperationException($"Unsupported HP arguments: {call}.");
                }

                result[index] = argument.Expression;
            }

            if (result.Any(expression => expression is null))
            {
                throw new InvalidOperationException($"Missing HP arguments: {call}.");
            }

            return result.Select(expression => expression!).ToArray();
        }

        private static IEnumerable<Value> Branch(List<Value> values, string key, string branch) =>
            values.Select(value => (value, conditions: Merge(value.Conditions, new() { [key] = branch })))
                .Where(pair => pair.conditions is not null)
                .Select(pair => pair.value with { Conditions = pair.conditions! });
    }
}
