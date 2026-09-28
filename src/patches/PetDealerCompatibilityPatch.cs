using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches;

/// <summary>
/// Vanilla PersonalHivePower redirects only Osty to its owner and then creates Dazed for
/// dealer.Player, which is null for any other pet (this mod's allies included) and throws.
/// Other pets are redirected the same way; a pet without an owner has nobody to receive the cards.
/// Osty keeps the vanilla path untouched.
/// </summary>
[HarmonyPatch(typeof(PersonalHivePower), nameof(PersonalHivePower.AfterDamageReceived))]
[LibraryPatch(Reason = "PersonalHivePower 是 sealed 原版能力，无 Hook 能改伤害来源；把有主人的宠物伤害归到主人。本模组没有宠物，这是给第三方宠物模组的兼容，属于有意的例外。")]
public static class PetDealerCompatibilityPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(
        ref Creature? dealer,
        ref Task __result)
    {
        if (dealer is not { IsPet: true } || dealer.Monster is Osty)
        {
            return true;
        }

        if (dealer.PetOwner?.Creature is { } owner)
        {
            dealer = owner;
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
