using System;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.powers.WarmheartedWoodsman;

public sealed class WarmHeartPower : LibraryOfRuinaPowerModel
{
    private sealed class SyncedBuffData
    {
        public int StrongContribution;

        public int EnduranceContribution;

        public bool IsSyncing;
    }

    protected override string LegacyPowerId => "WARM_HEART_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => true;

    protected override object InitInternalData()
    {
        return new SyncedBuffData();
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await SyncPermanentBuffs(new ThrowingPlayerChoiceContext(), applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (!ReferenceEquals(power, this) && !IsTrackedPermanentBuff(power))
        {
            return;
        }

        await SyncPermanentBuffs(choiceContext, applier, cardSource);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        SyncedBuffData data = GetInternalData<SyncedBuffData>();
        await RemovePermanentBuffContribution<LibraryStrongPower>(oldOwner, data.StrongContribution);
        await RemovePermanentBuffContribution<LibraryEndurancePower>(oldOwner, data.EnduranceContribution);
        data.StrongContribution = 0;
        data.EnduranceContribution = 0;
    }

    private async Task SyncPermanentBuffs(
        PlayerChoiceContext choiceContext,
        Creature? applier,
        CardModel? cardSource)
    {
        if (Owner.IsDead)
        {
            return;
        }

        SyncedBuffData data = GetInternalData<SyncedBuffData>();
        if (data.IsSyncing)
        {
            return;
        }

        data.IsSyncing = true;
        try
        {
            int targetContribution = Math.Max(0, Amount);
            data.StrongContribution = await SyncPermanentBuffContribution<LibraryStrongPower>(
                choiceContext,
                Owner,
                data.StrongContribution,
                targetContribution,
                applier ?? Owner,
                cardSource);
            data.EnduranceContribution = await SyncPermanentBuffContribution<LibraryEndurancePower>(
                choiceContext,
                Owner,
                data.EnduranceContribution,
                targetContribution,
                applier ?? Owner,
                cardSource);
        }
        finally
        {
            data.IsSyncing = false;
        }
    }

    private static async Task<int> SyncPermanentBuffContribution<TPower>(
        PlayerChoiceContext choiceContext,
        Creature owner,
        int currentContribution,
        int targetContribution,
        Creature? applier,
        CardModel? cardSource)
        where TPower : LibraryTurnsPowerModel
    {
        TPower? existing = FindPermanentBuff<TPower>(owner);
        if (currentContribution == targetContribution
            && (targetContribution <= 0 || existing is { Amount: var amount } && amount >= currentContribution))
        {
            return currentContribution;
        }

        if (existing == null)
        {
            if (targetContribution > 0)
            {
                await LibraryPowerCmd.Apply<TPower>(
                    new ThrowingPlayerChoiceContext(),
                    owner,
                    targetContribution,
                    0,
                    true,
                    applier,
                    cardSource);
            }

            return targetContribution;
        }

        int delta = targetContribution - currentContribution;
        if (delta == 0 && existing.Amount < currentContribution)
        {
            delta = currentContribution - existing.Amount;
        }

        if (delta < 0)
        {
            delta = -Math.Min(-delta, existing.Amount);
        }

        if (delta != 0)
        {
            await PowerCmdCompat.ModifyAmount(choiceContext, existing, delta, applier, cardSource);
        }

        return targetContribution;
    }

    private static async Task RemovePermanentBuffContribution<TPower>(Creature owner, int contribution)
        where TPower : LibraryTurnsPowerModel
    {
        if (contribution <= 0)
        {
            return;
        }

        TPower? existing = FindPermanentBuff<TPower>(owner);
        if (existing == null)
        {
            return;
        }

        int amountToRemove = Math.Min(contribution, existing.Amount);
        if (amountToRemove > 0)
        {
            await PowerCmdCompat.ModifyAmount(existing, -amountToRemove, owner, null);
        }
    }

    private static TPower? FindPermanentBuff<TPower>(Creature owner)
        where TPower : LibraryTurnsPowerModel =>
        owner.GetPower<TPower>();

    private bool IsTrackedPermanentBuff(PowerModel power)
    {
        if (power.Owner != Owner)
        {
            return false;
        }

        return power switch
        {
            LibraryStrongPower strong => strong.TurnsRemaining <= 0,
            LibraryEndurancePower endurance => endurance.TurnsRemaining <= 0,
            _ => false
        };
    }
}

public sealed class WarmheartedWoodsmanVerdantForestPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WARMHEARTED_WOODSMAN_VERDANT_FOREST_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(monsters.WarmheartedWoodsman.WarmheartedWoodsman.VerdantForestEnergy)
    ];

    public override async Task AfterEnergyReset(Player player)
    {
        if (Owner.IsDead
            || Owner.GetPowerAmount<WarmHeartPower>() <= 0
            || player.Creature?.IsAlive != true
            || player.Creature.CombatState != Owner.CombatState)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(monsters.WarmheartedWoodsman.WarmheartedWoodsman.VerdantForestEnergy, player);
    }
}

public sealed class WarmheartedWoodsmanWantAHeartPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WARMHEARTED_WOODSMAN_WANT_A_HEART_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", monsters.WarmheartedWoodsman.WarmheartedWoodsman.WantAHeartBuffPerEnergy),
        new DynamicVar("Endurance", monsters.WarmheartedWoodsman.WarmheartedWoodsman.WantAHeartBuffPerEnergy),
        new DynamicVar("Turns", monsters.WarmheartedWoodsman.WarmheartedWoodsman.OneTurnBuffDuration),
        new EnergyVar("Energy", 1)
    ];
}

public sealed class WarmheartedWoodsmanViolentHeartPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WARMHEARTED_WOODSMAN_VIOLENT_HEART_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", monsters.WarmheartedWoodsman.WarmheartedWoodsman.ViolentHeartHealPercent),
        new DynamicVar("Strength", monsters.WarmheartedWoodsman.WarmheartedWoodsman.ViolentHeartStrong)
    ];
}

public sealed class WarmheartedWoodsmanEmptyHeartPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WARMHEARTED_WOODSMAN_EMPTY_HEART_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class WoodsmanTreeHeartPassivePower : LibraryOfRuinaPowerModel
{
    private sealed class RevivesVar : DynamicVar
    {
        public RevivesVar() : base("Revives", monsters.WarmheartedWoodsman.WarmheartedWoodsman.InitialTreeReviveCharges)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is WoodsmanTreeHeartPassivePower power
                ? power.Amount
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }

    protected override string LegacyPowerId => "WOODSMAN_TREE_HEART_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new RevivesVar()
    ];

    public void SetRevives(int revives)
    {
        SetAmount(Math.Max(0, revives), silent: true);
    }
}

public sealed class WarmheartedWoodsmanTemporaryThornsPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WARMHEARTED_WOODSMAN_TEMPORARY_THORNS_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Side || Amount <= 0)
        {
            return;
        }

        ThornsPower? thorns = Owner.GetPower<ThornsPower>();
        if (thorns != null)
        {
            await PowerCmdCompat.ModifyAmount(choiceContext, thorns, -Amount, Owner, null, silent: true);
        }

        await PowerCmd.Remove(this);
    }
}
