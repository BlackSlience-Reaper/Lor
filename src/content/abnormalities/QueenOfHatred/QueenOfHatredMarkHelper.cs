using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

public static class QueenOfHatredMarkHelper
{
    public const int PermanentRapidWearAmount = 1;
    public const int WeakAmount = 1;
    public const int VulnerableAmount = 0;

    public static async Task ApplyMarkPackage<TMarkPower>(
        IEnumerable<Creature> candidates,
        Creature target,
        Creature applier,
        int weakAmount = WeakAmount,
        int permanentRapidWearAmount = PermanentRapidWearAmount,
        int vulnerableAmount = VulnerableAmount,
        bool ensureMinimumRapidWear = true)
        where TMarkPower : PowerModel
    {
        foreach (Creature creature in candidates)
        {
            if (creature == target)
            {
                continue;
            }

            PowerModel? staleMark = creature.GetPower<TMarkPower>();
            if (staleMark != null)
            {
                await PowerCmd.Remove(staleMark);
            }
        }

        PowerModel? oldMark = target.GetPower<TMarkPower>();
        if (oldMark != null)
        {
            await PowerCmd.Remove(oldMark);
        }

        await PowerCmdCompat.Apply<TMarkPower>(target, 1m, applier, null);
        if (weakAmount > 0)
        {
            await PowerCmdCompat.Apply<WeakPower>(target, weakAmount, applier, null);
        }

        if (permanentRapidWearAmount > 0)
        {
            if (ensureMinimumRapidWear)
            {
                await EnsurePermanentRapidWear(target, applier, permanentRapidWearAmount);
            }
            else
            {
                await ApplyPermanentRapidWear(target, permanentRapidWearAmount, applier);
            }
        }

        if (vulnerableAmount > 0)
        {
            await PowerCmdCompat.Apply<VulnerablePower>(target, vulnerableAmount, applier, null);
        }
    }

    public static async Task ClearMarks<TMarkPower>(IEnumerable<Creature> creatures)
        where TMarkPower : PowerModel
    {
        foreach (Creature creature in creatures)
        {
            PowerModel? mark = creature.GetPower<TMarkPower>();
            if (mark != null)
            {
                await PowerCmd.Remove(mark);
            }
        }
    }

    public static async Task EnsurePermanentRapidWear(Creature target, Creature applier)
    {
        await EnsurePermanentRapidWear(target, applier, PermanentRapidWearAmount);
    }

    private static async Task EnsurePermanentRapidWear(Creature target, Creature applier, int minimumAmount)
    {
        LibraryVulnerablePower? rapidWear = target.GetPower<LibraryVulnerablePower>();
        if (rapidWear == null)
        {
            await ApplyPermanentRapidWear(target, minimumAmount, applier);
            return;
        }

        if (rapidWear.Amount < minimumAmount)
        {
            await ApplyPermanentRapidWear(target, minimumAmount - rapidWear.Amount, applier);
        }
    }

    private static async Task ApplyPermanentRapidWear(Creature target, int amount, Creature applier)
    {
        await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
            new ThrowingPlayerChoiceContext(),
            target,
            amount,
            0,
            true,
            applier,
            null,
            silent: true);
    }
}
