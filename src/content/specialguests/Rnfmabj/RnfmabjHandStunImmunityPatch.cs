using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

/// <summary>
/// The hands have no confusion gauge and cannot enter the engine's stunned
/// move even when another mod invokes CreatureCmd.Stun directly.
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.StunInternal))]
[LibraryPatch(Reason = "原版 Creature.StunInternal 非虚且无眩晕否决 Hook；仅让本模组特邀 Boss 的手部免疫任何来源的眩晕。")]
internal static class RnfmabjHandStunImmunityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __instance) =>
        __instance.Monster is not RnfmabjHandBase;
}

[HarmonyPatch(typeof(LibraryCreature), nameof(LibraryCreature.StunInternal))]
[LibraryPatch(Reason = "基础库 LibraryCreature.StunInternal 非虚且无否决钩子；仅让本模组特邀 Boss 的手部免疫混乱眩晕。")]
internal static class RnfmabjHandLibraryStunImmunityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(LibraryCreature __instance) =>
        __instance.Monster is not RnfmabjHandBase;
}
