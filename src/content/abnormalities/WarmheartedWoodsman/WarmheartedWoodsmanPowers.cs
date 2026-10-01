using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

public sealed class WarmHeartPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WARM_HEART_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 规范模型不依赖持有者，显示值为 0；挂上后按当前层数同步。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(0)
    ];

    private int EnergyToRestore =>
        Math.Max(0, Amount) * WarmheartedWoodsman.WarmHeartEnergyPerStack;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncEnergyVar();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            SyncEnergyVar();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        int energy = EnergyToRestore;
        if (Owner.IsDead
            || energy <= 0
            || player.Creature?.IsAlive != true
            || player.Creature.CombatState != Owner.CombatState)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(energy, player);
    }

    /// <summary>直接设定层数且不触发增减结算；用于开局挂上 0 层作为机制提示。</summary>
    internal void SetStacksSilently(int stacks)
    {
        SetAmount(Math.Max(0, stacks), silent: true);
        SyncEnergyVar();
    }

    private void SyncEnergyVar()
    {
        DynamicVars[EnergyVar.defaultName].BaseValue = EnergyToRestore;
    }
}

public sealed class WarmheartedWoodsmanViolentHeartPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WARMHEARTED_WOODSMAN_VIOLENT_HEART_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TreeDamageMultiplier", WarmheartedWoodsman.ViolentHeartTreeDamageMultiplier),
        new DynamicVar("HealPercent", WarmheartedWoodsman.ViolentHeartHealPercent),
        new DynamicVar("Strength", WarmheartedWoodsman.ViolentHeartStrength),
        new DynamicVar("WarmHeart", WarmheartedWoodsman.ViolentHeartWarmHeartGain)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<WarmHeartPower>()
    ];

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner
            || target?.Monster is not WoodsmanTree
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }

        return WarmheartedWoodsman.ViolentHeartTreeDamageMultiplier;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner
            || !result.WasTargetKilled
            || target.Monster is not WoodsmanTree
            || Owner.Monster is not WarmheartedWoodsman woodsman)
        {
            return;
        }

        Flash();
        await woodsman.ApplyViolentHeartTreeKillRewards(choiceContext);
    }
}

public sealed class WoodsmanTreeHeartPassivePower : LibraryOfRuinaPowerModel
{
    private sealed class RevivesVar : DynamicVar
    {
        public RevivesVar() : base("Revives", WarmheartedWoodsman.InitialTreeReviveCharges)
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
        new RevivesVar(),
        new DynamicVar("ChargeCost", WarmheartedWoodsman.TreeRespawnChargeCost)
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
