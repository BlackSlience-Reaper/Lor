using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

/// <summary>
/// Restores the dynamic description selector after a relic is deserialized.
/// SavedProperties restores the concrete Mode property, but DynamicVars is a
/// separate runtime cache and otherwise remains at Mode = 0 in run history.
/// </summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.FromSerializable))]
internal static class RelicSavedModeDescriptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(RelicModel __result)
    {
        PropertyInfo? modeProperty = AccessTools.Property(__result.GetType(), "Mode");
        if (modeProperty?.PropertyType.IsEnum != true
            || modeProperty.GetValue(__result) is not object mode
            || !__result.DynamicVars.TryGetValue("Mode", out var modeVar))
        {
            return;
        }

        modeVar.BaseValue = Convert.ToInt32(mode);
    }
}
