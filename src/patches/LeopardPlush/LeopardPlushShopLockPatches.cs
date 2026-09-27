using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.relics.LeopardPlush;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;

namespace LibraryOfRuina.patches.LeopardPlush;

internal static class LeopardPlushShopLockPatchHelper
{
    private static readonly FieldInfo? PlayerField = AccessTools.Field(typeof(MerchantEntry), "_player");

    internal static bool TryBlockPurchase(MerchantEntry entry)
    {
        Player? player = PlayerField?.GetValue(entry) as Player;
        return LeopardPlushRelic.IsShopLocked(player);
    }
}

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper), typeof(MerchantInventory), typeof(bool))]
public static class LeopardPlushMerchantPurchasePatch
{
    [HarmonyPrefix]
    public static bool Prefix(MerchantEntry __instance, bool ignoreCost, ref Task<bool> __result)
    {
        return true;
    }
}

[HarmonyPatch(typeof(MerchantCardRemovalEntry), nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper), typeof(MerchantInventory), typeof(bool), typeof(bool))]
public static class LeopardPlushMerchantRemovalPatch
{
    [HarmonyPrefix]
    public static bool Prefix(MerchantCardRemovalEntry __instance, bool ignoreCost, ref Task<bool> __result)
    {
        if (ignoreCost)
        {
            return true;
        }

        if (!LeopardPlushShopLockPatchHelper.TryBlockPurchase(__instance))
        {
            return true;
        }

        __instance.InvokePurchaseFailed(PurchaseStatus.FailureForbidden);
        __result = Task.FromResult(false);
        return false;
    }
}
