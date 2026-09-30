using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.core.compat;

internal static class PowerCmdCompat
{
    private static PlayerChoiceContext DefaultChoiceContext() => new ThrowingPlayerChoiceContext();

    public static Task<IReadOnlyList<T>> Apply<T>(
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
        => Apply<T>(DefaultChoiceContext(), targets, amount, applier, cardSource, silent);

    public static Task<IReadOnlyList<T>> Apply<T>(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
        => PowerCmd.Apply<T>(choiceContext, targets, amount, applier, cardSource, silent);

    public static Task<T?> Apply<T>(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
        => Apply<T>(DefaultChoiceContext(), target, amount, applier, cardSource, silent);

    public static Task<T?> Apply<T>(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
        => PowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource, silent);

    public static Task<T?> Ensure<T>(
        Creature target,
        decimal amount = 1m)
        where T : PowerModel =>
        Ensure<T>(
            target,
            amount,
            target,
            null,
            silent: true);

    public static Task<T?> Ensure<T>(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
        => Ensure<T>(
            DefaultChoiceContext(),
            target,
            amount,
            applier,
            cardSource,
            silent);

    public static Task<T?> Ensure<T>(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        T? existingPower = target.GetPower<T>();
        return existingPower != null
            ? Task.FromResult<T?>(existingPower)
            : PowerCmd.Apply<T>(
                choiceContext,
                target,
                amount,
                applier,
                cardSource,
                silent);
    }

    public static Task RemoveIfPresent<T>(Creature target)
        where T : PowerModel =>
        target.GetPower<T>() is { } power
            ? PowerCmd.Remove(power)
            : Task.CompletedTask;

    public static Task Apply(
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        => Apply(DefaultChoiceContext(), power, target, amount, applier, cardSource, silent);

    public static Task Apply(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        => PowerCmd.Apply(choiceContext, power, target, amount, applier, cardSource, silent);

    public static Task<int> ModifyAmount(
        PowerModel power,
        decimal offset,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        => ModifyAmount(DefaultChoiceContext(), power, offset, applier, cardSource, silent);

    public static Task<int> ModifyAmount(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal offset,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        => PowerCmd.ModifyAmount(choiceContext, power, offset, applier, cardSource, silent);

    public static Task<T?> SetAmount<T>(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
        where T : PowerModel
        => SetAmount<T>(DefaultChoiceContext(), target, amount, applier, cardSource);

    public static async Task<T?> SetAmount<T>(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
        where T : PowerModel
    {
        T? existingPower = target.GetPower<T>();
        if (existingPower == null)
        {
            return await PowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource);
        }

        await PowerCmd.ModifyAmount(choiceContext, existingPower, amount - existingPower.Amount, applier, cardSource);
        return existingPower;
    }

    private static void CorrectDebuffSkipFlag(PowerModel? power, Creature target)
    {
        if (power == null || power.Type != PowerType.Debuff || target.Side != CombatSide.Player)
        {
            return;
        }

        power.SkipNextDurationTick = target.CombatState?.CurrentSide == CombatSide.Enemy;
    }

    public static async Task<T?> ApplyDebuff<T>(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        T? power = await Apply<T>(target, amount, applier, cardSource, silent);
        CorrectDebuffSkipFlag(power, target);
        return power;
    }

    public static Task<IReadOnlyList<T>> ApplyDebuff<T>(
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel =>
        ApplyDebuff<T>(DefaultChoiceContext(), targets, amount, applier, cardSource, silent);

    public static async Task<T?> ApplyDebuff<T>(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        T? power = await Apply<T>(choiceContext, target, amount, applier, cardSource, silent);
        CorrectDebuffSkipFlag(power, target);
        return power;
    }

    public static async Task<IReadOnlyList<T>> ApplyDebuff<T>(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent = false)
        where T : PowerModel
    {
        var powers = new List<T>();
        foreach (Creature target in targets.ToArray())
        {
            // Failed applications are omitted by PowerCmd; bind the duration flag to its actual target.
            T? power = await ApplyDebuff<T>(choiceContext, target, amount, applier, cardSource, silent);
            if (power != null)
            {
                powers.Add(power);
            }
        }
        return powers;
    }
}
