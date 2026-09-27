using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace LibraryOfRuina.patches;

/// <summary>
/// Canonical power hover tips must not call <see cref="LibraryTurnsPowerModel.Prompt"/>,
/// which reads mutable-only state such as <see cref="MegaCrit.Sts2.Core.Models.PowerModel.Owner"/>.
/// Older deployed LibraryOfRuinaLib builds may still route through the unsafe path.
/// </summary>
[HarmonyPatch(typeof(LibraryTurnsPowerModel), nameof(LibraryTurnsPowerModel.AddVariablesToDescription))]
internal static class LibraryTurnsPowerHoverTipPatch
{
    private static bool Prefix(LibraryTurnsPowerModel __instance, LocString description, int? amountOverride)
    {
        description.Add("Prompt", __instance.IsMutable ? __instance.Prompt() : "");
        return false;
    }
}
