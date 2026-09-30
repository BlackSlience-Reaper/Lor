using Microsoft.CodeAnalysis.CSharp;

internal static class SelfTests
{
    private const string Prelude = """
        public abstract class MonsterModel
        {
            public abstract int MinInitialHp { get; }
            public abstract int MaxInitialHp { get; }
        }
        """;

    public static int Run()
    {
        var cases = new (string Name, string Source, bool Valid, string? Error)[]
        {
            ("equal bounds", Monster("10", "10"), true, null),
            ("fixed inversion", Monster("20", "10"), false, "MinInitialHp=20, MaxInitialHp=10"),
            ("ascension inversion", Monster(Ascension("21", "10"), Ascension("20", "15")), false, "=ascensionValue"),
            ("fallback inversion", Monster(Ascension("10", "21"), Ascension("15", "20")), false, "=fallbackValue"),
            ("independent branches", Monster(Ascension("10", "100"), Ascension("20", "110")), true, null),
            ("named arguments", Monster(
                "AscensionHelper.GetValueIfAscension(fallbackValue: 5, level: AscensionLevel.ToughEnemies, ascensionValue: 20)",
                "AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, fallbackValue: 10, ascensionValue: 15)"), false, "=ascensionValue"),
            ("comments and constants", Monster(Ascension("High /* , 999 */", "Low"), Ascension("22", "15"),
                "private const int High = 10 + 11, Low = 12;"), true, null),
            ("constant inversion", Monster(Ascension("High", "Low"), Ascension("20", "15"),
                "private const int High = 21, Low = 12;"), false, "=ascensionValue"),
            ("inherited helper", """
                public abstract class Base : MonsterModel
                {
                    protected const int Low = 10;
                    protected static int HpValue(int normal, int high) =>
                        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, high, normal);
                    public override int MinInitialHp => HpValue(Low, 25);
                    public override int MaxInitialHp => HpValue(15, 20);
                }
                public class Child : Base { }
                """, false, "=ascensionValue"),
            ("static import helper", """
                using static Rules;
                public static class Rules
                {
                    public const int Low = 25;
                    public static int HpValue(int normal, int high) =>
                        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, high, normal);
                }
                """ + Monster("HpValue(Low, 10)", "HpValue(20, 15)"), false, "=fallbackValue"),
            ("partial declarations", """
                public partial class Sample : MonsterModel { public override int MinInitialHp => 30; }
                public partial class Sample { public override int MaxInitialHp => 20; }
                """, false, "MinInitialHp=30"),
            ("inherited virtual property", """
                public abstract class Base : MonsterModel
                {
                    public override int MinInitialHp => 20;
                    public override int MaxInitialHp => MinInitialHp;
                }
                public class Child : Base { public override int MinInitialHp => 30; }
                """, true, null),
            ("inherited single override", """
                public class Base : MonsterModel
                {
                    public override int MinInitialHp => 10;
                    public override int MaxInitialHp => 20;
                }
                public class Child : Base { public override int MinInitialHp => 30; }
                """, false, "Child"),
            ("getter bodies", """
                public class Sample : MonsterModel
                {
                    public override int MinInitialHp { get { return 20; } }
                    public override int MaxInitialHp { get => 10; }
                }
                """, false, "MinInitialHp=20"),
            ("conditional branches", Monster("flag ? 10 : 30", "flag ? 20 : 40", "private bool flag;"), true, null),
            ("conditional inversion", Monster("flag ? 10 : 30", "flag ? 20 : 25", "private bool flag;"), false, "=false"),
            ("impure condition", Monster("CoinFlip() ? 10 : 30", "CoinFlip() ? 20 : 40"), false, "Unsupported HP branch condition"),
            ("property condition", Monster("Flag ? 10 : 30", "Flag ? 20 : 40",
                "private bool Flag => CoinFlip();"), false, "Unsupported HP branch property"),
            ("switch and alias", Monster("phase switch { 1 => 10, _ => 30 }", "MinInitialHp", "private int phase;"), true, null),
            ("switch inversion", Monster("phase switch { 1 => 10, _ => 30 }", "phase switch { 1 => 20, _ => 25 }",
                "private int phase;"), false, "MinInitialHp=30"),
            ("dynamic equal field", Monster("hp", "hp", "private int hp;"), true, null),
            ("dynamic different fields", Monster("low", "high", "private int low, high;"), false, "cannot satisfy"),
            ("source switch helper", Monster("Hp(phase)", "Hp(phase)",
                "private int phase; private static int Hp(int phase) => phase switch { 1 => 10, _ => 20 };"), true, null),
            ("helper body mutation", Monster("Hp(10, 30)", "Hp(20, 25)",
                "private static int Hp(int normal, int high) => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, normal, high);"),
                false, "=fallbackValue"),
            ("scaled monster reference", """
                public class Original : MonsterModel
                {
                    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 10, 20);
                    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 15, 25);
                }
                """ + Monster("ModelDb.Monster<Original>().MinInitialHp * 120 / 100",
                    "ModelDb.Monster<Original>().MaxInitialHp * 120 / 100"), true, null),
            ("scaled reference inversion", """
                public class Original : MonsterModel
                {
                    public override int MinInitialHp => 10;
                    public override int MaxInitialHp => MinInitialHp;
                }
                """ + Monster("ModelDb.Monster<Original>().MinInitialHp * 200 / 100",
                    "ModelDb.Monster<Original>().MaxInitialHp"), false, "MinInitialHp=20"),
            ("recursive monster references", """
                public class Original : MonsterModel
                {
                    public override int MinInitialHp => ModelDb.Monster<Sample>().MinInitialHp;
                    public override int MaxInitialHp => MinInitialHp;
                }
                """ + Monster("ModelDb.Monster<Original>().MinInitialHp", "10"), false, "Cyclic"),
            ("unresolved expression", Monster("Unknown()", "20"), false, "Unsupported/unresolved"),
            ("unresolved identical calls", Monster("Unknown()", "Unknown()"), false, "Unsupported/unresolved"),
            ("cyclic properties", Monster("MaxInitialHp", "MinInitialHp"), false, "Cyclic"),
            ("missing HP", "public class Sample : MonsterModel { }", false, "Missing concrete"),
            ("empty inventory", "public class Unrelated { }", false, "No monster"),
        };

        int failed = 0;
        foreach (var test in cases)
        {
            // Put imports before the base stub, as required by C#.
            var trees = new[] { CSharpSyntaxTree.ParseText(Prelude, path: "base.cs"), CSharpSyntaxTree.ParseText(test.Source, path: test.Name + ".cs") };
            var result = new HpChecker(trees).Check();
            bool passed = (result.Issues.Count == 0) == test.Valid
                && (test.Error is null || result.Issues.Any(issue => issue.Message.Contains(test.Error, StringComparison.Ordinal)));
            if (!passed)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {test.Name}: {string.Join("; ", result.Issues.Select(issue => issue.Message))}");
            }
        }

        Console.WriteLine($"Monster HP checker regression cases: {cases.Length - failed}/{cases.Length} passed.");
        return failed == 0 ? 0 : 1;
    }

    private static string Ascension(string high, string normal) =>
        $"AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, {high}, {normal})";

    private static string Monster(string min, string max, string members = "") =>
        $$"""
        public class Sample : MonsterModel
        {
            {{members}}
            public override int MinInitialHp => {{min}};
            public override int MaxInitialHp => {{max}};
        }
        """;
}
