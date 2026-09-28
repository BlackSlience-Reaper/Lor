using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.relics.LeopardPlush;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.patches.LeopardPlush;

internal static class LeopardPlushShopLockPatchHelper
{

    internal static bool TryBlockPurchase(MerchantEntry entry)
    {
        Player? player = VanillaPrivate.MerchantEntryPlayer.Get(entry) as Player;
        return LeopardPlushRelic.IsShopLocked(player);
    }
}

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper), typeof(MerchantInventory), typeof(bool))]
[LibraryPatch(Reason = "商店购买流程无取消 Hook；只拦截本模组豹豹玩偶的商店锁定。随 PR #4 删除八奈见内容一起移除。")]
public static class LeopardPlushMerchantPurchasePatch
{
    [HarmonyPrefix]
    public static bool Prefix(MerchantEntry __instance, bool ignoreCost, ref Task<bool> __result)
    {
        return true;
    }
}

[HarmonyPatch(typeof(MerchantCardRemovalEntry), nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper), typeof(MerchantInventory), typeof(bool), typeof(bool))]
[LibraryPatch(Reason = "商店删牌流程无取消 Hook；只拦截本模组豹豹玩偶的商店锁定。随 PR #4 删除八奈见内容一起移除。")]
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
