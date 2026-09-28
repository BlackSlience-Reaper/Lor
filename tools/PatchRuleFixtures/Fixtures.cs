using System;
using System.Reflection;
using HarmonyLib;

namespace PatchRuleFixtures;

// tools/check.sh scans this assembly with ModSnapshot and diffs skip_prefixes.txt against
// expected_skip_prefixes.txt. Each class below is a form LibraryPatcher installs (or must not report).

/// <summary>Stand-in matched by name, like the mod's own attribute.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class LibraryPatchAttribute : Attribute
{
    public bool Optional { get; init; }

    public string? Reason { get; init; }
}

/// <summary>Only a class-level [HarmonyPriority]; the target is on the method. Harmony installs it.</summary>
[HarmonyPriority(Priority.Low)]
internal static class MethodLevelTarget
{
    [HarmonyPatch(typeof(string), nameof(string.Trim), new Type[0])]
    [HarmonyPrefix]
    private static bool Skip() => true;
}

[HarmonyPatch(typeof(string), nameof(string.Trim), new Type[0])]
[LibraryPatch(Reason = "fixture reason")]
internal static class ClassLevelTargetWithReason
{
    private static bool Prefix() => true;
}

/// <summary>Optional classes are installed without any class-level Harmony attribute.</summary>
[LibraryPatch(Optional = true)]
internal static class OptionalTargetMethod
{
    [HarmonyTargetMethod]
    private static MethodBase Target() => AccessTools.Method(typeof(string), nameof(string.Trim), Type.EmptyTypes);

    [HarmonyPrefix]
    private static bool Skip() => true;
}

/// <summary>Harmony checks Prefix before Postfix, so [HarmonyPrefix] wins over the method name.</summary>
[HarmonyPatch(typeof(string), nameof(string.Trim), new Type[0])]
internal static class PrefixAttributeOnOtherName
{
    [HarmonyPrefix]
    private static bool Postfix() => true;
}

[HarmonyPatch(typeof(string), nameof(string.Trim), new Type[0])]
internal abstract class InheritedTargetBase;

/// <summary>HasHarmonyAttribute reads attributes with inherit: true.</summary>
internal sealed class InheritedTarget : InheritedTargetBase
{
    private static bool Prefix() => true;
}

internal static class Outer
{
    [HarmonyPatch(typeof(string), nameof(string.Trim), new Type[0])]
    internal static class Nested
    {
        private static bool Prefix() => true;
    }
}

// Not reported: cannot skip the original, or not installed at all.

[HarmonyPatch(typeof(string), nameof(string.Trim), new Type[0])]
internal static class VoidPrefix
{
    private static void Prefix()
    {
    }
}

internal static class NoHarmonyClassAttribute
{
    [HarmonyPrefix]
    private static bool Prefix() => true;
}

[HarmonyPatch(typeof(string), nameof(string.Trim), new Type[0])]
internal static class BoolPostfix
{
    private static bool Postfix(bool __result) => __result;
}
