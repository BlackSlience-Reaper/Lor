using System;
using System.Linq;
using HarmonyLib;
using LibraryOfRuina.infra.patching;

// PatchClassRules re-implements Harmony's class check on metadata only; compare it with the real one.
int mismatches = 0;
Type[] fixtures = typeof(PatchRuleFixtures.LibraryPatchAttribute).Assembly.GetTypes()
    .Where(static type => type.Namespace == "PatchRuleFixtures")
    .ToArray();
foreach (Type type in fixtures)
{
    bool harmony = type.HasHarmonyAttribute();
    bool rules = PatchClassRules.HasHarmonyClassAttribute(type);
    if (harmony != rules)
    {
        Console.Error.WriteLine($"{type.FullName}: Harmony HasHarmonyAttribute={harmony}, PatchClassRules={rules}");
        mismatches++;
    }
}

Console.WriteLine($"patch class rule: {fixtures.Length} fixture types, {mismatches} mismatch(es) with Harmony");
return mismatches == 0 ? 0 : 1;
