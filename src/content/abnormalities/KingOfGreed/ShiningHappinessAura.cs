using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

internal static class ShiningHappinessAura
{
    internal static async Task Add<TPower>(Creature target, int amount, Creature source)
        where TPower : LibraryTurnsPowerModel
    {
        if (target.GetPower<TPower>() is { } power)
        {
            await PowerCmdCompat.ModifyAmount(power, amount, source, null, silent: true);
        }
        else
        {
            await LibraryPowerCmd.Apply<TPower>(new ThrowingPlayerChoiceContext(), target, amount, 0, true, source, null, silent: true);
        }
    }

    internal static Task Remove<TPower>(Creature target, int amount, Creature source)
        where TPower : LibraryTurnsPowerModel
    {
        if (target.GetPower<TPower>() is not { } power)
        {
            return Task.CompletedTask;
        }

        int delta = -Math.Min(amount, Math.Max(0, power.Amount));
        return delta == 0 ? Task.CompletedTask : PowerCmdCompat.ModifyAmount(power, delta, source, null, silent: true);
    }
}
