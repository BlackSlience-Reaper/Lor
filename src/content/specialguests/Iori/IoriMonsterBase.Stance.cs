using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.content.specialguests.Iori;

// 姿态切换：先撤掉旧姿态的贡献与标记能力，再加新姿态的。选择规则在 IoriStanceController。
public abstract partial class IoriMonsterBase
{
    private IoriStance[] SelectableStances =>
        IoriStanceController.SelectableStances(IsSecondStage);

    private IoriStance DrawInitialStance(Rng rng)
    {
        IoriStance[] candidates = SelectableStances;
        return candidates[rng.NextInt(candidates.Length)];
    }

    private IoriStance DrawNextStance(Rng rng)
    {
        IoriStance[] candidates = GetPreferredNextStances();
        return candidates[rng.NextInt(candidates.Length)];
    }

    private IoriStance ResolveFallbackNextStance() =>
        GetPreferredNextStances()[0];

    private IoriStance[] GetPreferredNextStances() =>
        IoriStanceController.GetPreferredNextStances(
            SelectableStances,
            CurrentStance,
            SelectedStanceMask);

    private void RecordStanceSelection(IoriStance stance)
    {
        SelectedStanceMask = IoriStanceController.RecordStanceSelection(
            SelectableStances,
            SelectedStanceMask,
            stance);
    }

    private async Task ChangeStance(
        IoriStance next,
        bool applyContributions)
    {
        if (next == IoriStance.None || next == CurrentStance)
        {
            return;
        }

        if (CurrentStance != IoriStance.None)
        {
            await RemoveStanceContributions(CurrentStance);
            await RemoveStancePower(CurrentStance);
        }

        CurrentStance = next;
        RecordStanceSelection(next);
        if (applyContributions)
        {
            await AddStanceContributions(next);
        }

        await ApplyStancePower(next);
        IoriCreatureVisuals.RefreshStance(Creature);
    }

    private async Task ApplyStancePower(IoriStance stance)
    {
        switch (stance)
        {
            case IoriStance.Slash:
                await PowerCmdCompat.Apply<IoriSlashStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            case IoriStance.Pierce:
                await PowerCmdCompat.Apply<IoriPierceStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            case IoriStance.Blunt:
                await PowerCmdCompat.Apply<IoriBluntStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            case IoriStance.Defense:
                await PowerCmdCompat.Apply<IoriDefenseStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            default:
                return;
        }
    }

    private async Task RemoveStancePower(IoriStance stance)
    {
        PowerModel? power = stance switch
        {
            IoriStance.Slash => Creature.GetPower<IoriSlashStancePower>(),
            IoriStance.Pierce => Creature.GetPower<IoriPierceStancePower>(),
            IoriStance.Blunt => Creature.GetPower<IoriBluntStancePower>(),
            IoriStance.Defense => Creature.GetPower<IoriDefenseStancePower>(),
            _ => null,
        };
        if (power != null)
        {
            await PowerCmd.Remove(power);
        }
    }

    private async Task AddStanceContributions(IoriStance stance)
    {
        switch (stance)
        {
            case IoriStance.Slash:
                await AddPermanent<LibraryStrongPower>(2);
                await AddPermanent<LibraryStrongSlashPower>(1);
                break;
            case IoriStance.Pierce:
                await AddPermanent<LibraryStrongPower>(2);
                await AddPermanent<LibraryStrongPiercePower>(1);
                await PowerCmdCompat.Apply<PainfulStabsPower>(
                    Creature,
                    1,
                    Creature,
                    null);
                break;
            case IoriStance.Blunt:
                await AddPermanent<LibraryStrongPower>(2);
                await AddPermanent<LibraryStrongBluntPower>(1);
                await AddChainsContribution(2);
                break;
            case IoriStance.Defense:
                await AddPermanent<LibraryDefensePowerUpPower>(1);
                await AddPermanent<LibraryEndurancePower>(2);
                await PowerCmdCompat.Apply<ThornsPower>(
                    Creature,
                    IoriStanceController.ResolveDefenseThorns(),
                    Creature,
                    null);
                await ClearDebuffs();
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(stance),
                    stance,
                    null);
        }
    }

    private async Task RemoveStanceContributions(IoriStance stance)
    {
        switch (stance)
        {
            case IoriStance.Slash:
                await RemovePermanent<LibraryStrongPower>(2);
                await RemovePermanent<LibraryStrongSlashPower>(1);
                break;
            case IoriStance.Pierce:
                await RemovePermanent<LibraryStrongPower>(2);
                await RemovePermanent<LibraryStrongPiercePower>(1);
                await RemoveNativeContribution<PainfulStabsPower>(1);
                break;
            case IoriStance.Blunt:
                await RemovePermanent<LibraryStrongPower>(2);
                await RemovePermanent<LibraryStrongBluntPower>(1);
                await RemoveChainsContribution(2);
                break;
            case IoriStance.Defense:
                await RemovePermanent<LibraryDefensePowerUpPower>(1);
                await RemovePermanent<LibraryEndurancePower>(2);
                await RemoveNativeContribution<ThornsPower>(
                    IoriStanceController.ResolveDefenseThorns());
                break;
        }
    }

    private Task<T?> AddPermanent<T>(int amount)
        where T : LibraryPowerModel =>
        LibraryPowerCmd.Apply<T>(
            Creature,
            amount,
            turns: -1,
            Creature,
            null);

    private async Task RemovePermanent<T>(int amount)
        where T : LibraryPowerModel
    {
        if (Creature.GetPower<T>() != null)
        {
            await LibraryPowerCmd.ModifyAmount<T>(
                Creature,
                -amount,
                turns: -1,
                Creature,
                null,
                silent: true);
        }
    }

    private async Task RemoveNativeContribution<T>(int amount)
        where T : PowerModel
    {
        if (Creature.GetPower<T>() is { } power)
        {
            await PowerCmdCompat.ModifyAmount(
                power,
                -amount,
                Creature,
                null,
                silent: true);
        }
    }

    private async Task AddChainsContribution(int amount)
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        var contributions = new Dictionary<ulong, int>();
        foreach (Creature player in Creature.CombatState.LivingPlayerCreatures()
                     .OrderBy(static player => player.CombatId))
        {
            int before = player.GetPower<ChainsOfBindingPower>()?.Amount ?? 0;
            await PowerCmdCompat.ApplyDebuff<ChainsOfBindingPower>(
                player,
                amount,
                Creature,
                null);
            int after = player.GetPower<ChainsOfBindingPower>()?.Amount ?? 0;
            int applied = Math.Max(0, after - before);
            if (applied > 0 && player.Player != null)
            {
                contributions[player.Player.NetId] = applied;
            }
        }

        ChainsContributionByPlayerNetId =
            IoriStanceController.SerializeContributions(contributions);
    }

    private async Task RemoveChainsContribution(int amount)
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        Dictionary<ulong, int> contributions =
            IoriStanceController.ParseContributions(
                ChainsContributionByPlayerNetId);
        foreach (Creature player in Creature.CombatState.PlayerCreatures
                     .OrderBy(static player => player.CombatId))
        {
            if (player.Player != null
                && contributions.TryGetValue(
                    player.Player.NetId,
                    out int contribution)
                && contribution > 0
                && player.GetPower<ChainsOfBindingPower>() is { } chains)
            {
                await PowerCmdCompat.ModifyAmount(
                    chains,
                    -Math.Min(amount, contribution),
                    Creature,
                    null,
                    silent: true);
            }
        }

        ChainsContributionByPlayerNetId = string.Empty;
    }

    private async Task ClearDebuffs()
    {
        PowerModel[] debuffs = Creature.Powers
            .Where(static power =>
                power.TypeForCurrentAmount == PowerType.Debuff)
            .ToArray();
        foreach (PowerModel debuff in debuffs)
        {
            await PowerCmd.Remove(debuff);
        }
    }
}
