using HarmonyLib;
using LibraryLib.Entities.Creatures;

namespace LibraryOfRuina.specialguests.Rnfmabj;

/// <summary>
/// The hands have no confusion gauge and cannot enter the engine's stunned
/// move even when another mod invokes CreatureCmd.Stun directly.
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.StunInternal))]
internal static class RnfmabjHandStunImmunityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __instance) =>
        __instance.Monster is not RnfmabjHandBase;
}

[HarmonyPatch(typeof(LibraryCreature), nameof(LibraryCreature.StunInternal))]
internal static class RnfmabjHandLibraryStunImmunityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(LibraryCreature __instance) =>
        __instance.Monster is not RnfmabjHandBase;
}
