using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace LibraryOfRuina.powers;

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy))]
[HarmonyPriority(Priority.Low)]
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
