using HarmonyLib;

// Stand-in for the game's hook bus; the scan matches it by full name only. It lives outside the fixture namespace
// so Program.cs does not compare it with Harmony.
namespace MegaCrit.Sts2.Core.Hooks
{
    internal static class Hook
    {
        internal static void AfterSomething()
        {
        }
    }
}

// Every form below targets Hook and must appear in expected_hook_patches.txt. The patch methods are void postfixes
// so these classes stay out of the skip-prefix list.
namespace PatchRuleFixtures
{
    using MegaCrit.Sts2.Core.Hooks;

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterSomething))]
    [LibraryPatch(Reason = "fixture hook reason")]
    internal static class HookWithReason
    {
        private static void Postfix()
        {
        }
    }

    /// <summary>Target on the method; the class-level [HarmonyPriority] is what makes Harmony install it.</summary>
    [HarmonyPriority(Priority.Normal)]
    internal static class HookOnMethod
    {
        [HarmonyPatch(typeof(Hook), nameof(Hook.AfterSomething))]
        [HarmonyPostfix]
        private static void After()
        {
        }
    }

    // Not listed: no class-level Harmony attribute and not optional, so Harmony never installs it.
    internal static class HookOnMethodNotInstalled
    {
        [HarmonyPatch(typeof(Hook), nameof(Hook.AfterSomething))]
        [HarmonyPostfix]
        private static void After()
        {
        }
    }

    /// <summary>Target given as a type-name string.</summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Hooks.Hook, PatchRuleFixtures", "AfterSomething")]
    internal static class HookByTypeName
    {
        private static void Postfix()
        {
        }
    }

    /// <summary>The base class only carries the target; it has no patch methods and is not listed itself.</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterSomething))]
    internal abstract class HookTargetBase;

    internal sealed class InheritedHookTarget : HookTargetBase
    {
        private static void Postfix()
        {
        }
    }

    // Not listed: targets another type.
    [HarmonyPatch(typeof(string), nameof(string.Trim), new System.Type[0])]
    internal static class NotHook
    {
        private static void Postfix()
        {
        }
    }
}
