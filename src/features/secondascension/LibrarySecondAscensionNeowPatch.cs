using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.specialguests;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.secondascension;

[HarmonyPatch(typeof(Neow), nameof(Neow.InitialDescription), MethodType.Getter)]
internal static class LibrarySecondAscensionNeowDescriptionPatch
{
    [HarmonyPrefix]
    private static void Prefix(Neow __instance, ref IReadOnlyList<ModifierModel>? __state)
    {
        LibrarySecondAscensionNeowModifierFilter.HideInternalCarriers(__instance, out __state);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, Neow __instance, IReadOnlyList<ModifierModel>? __state)
    {
        LibrarySecondAscensionNeowModifierFilter.RestoreModifiers(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class LibrarySecondAscensionNeowOptionsPatch
{
    [HarmonyPrefix]
    private static void Prefix(Neow __instance, ref IReadOnlyList<ModifierModel>? __state)
    {
        LibrarySecondAscensionNeowModifierFilter.HideInternalCarriers(__instance, out __state);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, Neow __instance, IReadOnlyList<ModifierModel>? __state)
    {
        LibrarySecondAscensionNeowModifierFilter.RestoreModifiers(__instance, __state);
        return __exception;
    }
}

internal static class LibrarySecondAscensionNeowModifierFilter
{
    private static readonly FieldInfo? RunStateModifiersField =
        AccessTools.Field(typeof(RunState), "<Modifiers>k__BackingField");

    public static void HideInternalCarriers(Neow neow, out IReadOnlyList<ModifierModel>? original)
    {
        original = null;

        if (RunStateModifiersField == null || neow.Owner?.RunState is not RunState runState)
        {
            return;
        }

        IReadOnlyList<ModifierModel> modifiers = runState.Modifiers;
        IReadOnlyList<ModifierModel> filtered = FilterForNeow(modifiers);

        if (filtered.Count == modifiers.Count)
        {
            return;
        }

        original = modifiers;
        RunStateModifiersField.SetValue(runState, filtered);
    }

    internal static IReadOnlyList<ModifierModel> FilterForNeow(
        IEnumerable<ModifierModel> modifiers) =>
        modifiers.Where(static modifier =>
                modifier is not LibrarySecondAscensionModifier
                && modifier is not SpecialGuestRunStateModifier)
            .ToList();

    public static void RestoreModifiers(Neow neow, IReadOnlyList<ModifierModel>? original)
    {
        if (original == null || RunStateModifiersField == null || neow.Owner?.RunState is not RunState runState)
        {
            return;
        }

        RunStateModifiersField.SetValue(runState, original);
    }
}
