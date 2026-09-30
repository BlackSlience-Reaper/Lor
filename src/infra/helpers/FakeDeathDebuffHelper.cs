using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.infra.helpers;

internal static class FakeDeathDebuffHelper
{
    public static async Task ClearDebuffs(Creature creature)
    {
        var debuffs = creature.Powers
            .Where(static power => power.TypeForCurrentAmount == PowerType.Debuff)
            .ToArray();

        foreach (var debuff in debuffs)
        {
            await PowerCmd.Remove(debuff);
        }
    }

    public static async Task ClearNonPassivePowers(
        Creature creature,
        Func<PowerModel, bool>? preserve = null)
    {
        var nonPassivePowers = creature.Powers
            .Where(power =>
                power.Type != PowerType.None
                && !(preserve?.Invoke(power) ?? false))
            .ToArray();

        foreach (var power in nonPassivePowers)
        {
            await PowerCmd.Remove(power);
        }
    }
}
