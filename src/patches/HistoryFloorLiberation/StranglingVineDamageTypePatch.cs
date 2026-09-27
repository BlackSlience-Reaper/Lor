using System;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.enchantments.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches.HistoryFloorLiberation;

[HarmonyPatch]
internal static class StranglingVineVanillaDamageTypePatch
{
    [HarmonyPrepare]
    private static bool Prepare() => FindTargetMethod() != null;

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() =>
        FindTargetMethod()
        ?? throw new MissingMethodException(
            "LibraryLib.Patches.LibraryDamagePreviewFeedback",
            "ResolveVanillaPreviewDamageType");

    private static MethodInfo? FindTargetMethod()
    {
        Type? type = AccessTools.TypeByName(
            "LibraryLib.Patches.LibraryDamagePreviewFeedback");
        return type == null
            ? null
            : AccessTools.DeclaredMethod(
                type,
                "ResolveVanillaPreviewDamageType",
                [typeof(CardModel), typeof(Creature)]);
    }

    [HarmonyPostfix]
    private static void Postfix(
        CardModel card,
        ref LibraryDamageType __result)
    {
        if (card.Enchantment is StranglingVineEnchantment)
        {
            __result = LibraryDamageType.Pierce;
        }
    }
}

[HarmonyPatch(typeof(LibraryAttackCommand), nameof(LibraryAttackCommand.Execute))]
internal static class StranglingVineLibraryAttackDamageTypePatch
{
    private static readonly FieldInfo? DamageTypeField =
        AccessTools.Field(typeof(LibraryAttackCommand), "_damageType");

    [HarmonyPrefix]
    private static void Prefix(LibraryAttackCommand __instance)
    {
        if (__instance.ModelSource is CardModel
            {
                Enchantment: StranglingVineEnchantment
            })
        {
            DamageTypeField?.SetValue(
                __instance,
                LibraryDamageType.Pierce);
        }
    }
}
