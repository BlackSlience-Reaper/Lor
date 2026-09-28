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

// IlFingerprint (the vanilla copy guard): real changes must show, identical bodies must not.
int fingerprintFailures = 0;
void ExpectFingerprint(string a, string b, bool equal)
{
    Type calls = typeof(PatchRuleFixtures.Fingerprint.Calls);
    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
    bool same = IlFingerprint.Hash(calls.GetMethod(a, flags)!) == IlFingerprint.Hash(calls.GetMethod(b, flags)!);
    if (same != equal)
    {
        Console.Error.WriteLine($"IlFingerprint: {a} vs {b} expected {(equal ? "equal" : "different")}");
        fingerprintFailures++;
    }
}

ExpectFingerprint("Local", "Other", equal: false);
ExpectFingerprint("Local", "LocalTwin", equal: true);
ExpectFingerprint("ConstantA", "ConstantB", equal: false);
ExpectFingerprint("BranchA", "BranchB", equal: false);
Console.WriteLine($"il fingerprint: 4 cases, {fingerprintFailures} failure(s)");
return mismatches == 0 && fingerprintFailures == 0 ? 0 : 1;
