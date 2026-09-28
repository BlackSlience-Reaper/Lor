using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.powers;

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy))]
[HarmonyPriority(Priority.Low)]
[LibraryPatch(Reason = "混乱阻止玩家侧额外获得能量；可改为能力覆写 ModifyEnergyGain（与原版 NoEnergyGainPower 同模式），暂缓。只在玩家带本模组混乱时生效。")]
public static class ConfusionEnergyPatch
{
    [HarmonyPrefix]
    public static bool Prefix(decimal amount, Player player, ref Task __result)
    {
        if (amount <= 0m || player?.Creature == null)
        {
            return true;
        }

        LibraryOfRuinaConfusionPower? confusion =
            player.Creature.GetPower<LibraryOfRuinaConfusionPower>();
        if (confusion == null || !confusion.ShouldBlockExtraEnergyGain(player))
        {
            return true;
        }

        confusion.NotifyExtraEnergyGainPrevented();
        __result = Task.CompletedTask;
        return false;
    }
}
