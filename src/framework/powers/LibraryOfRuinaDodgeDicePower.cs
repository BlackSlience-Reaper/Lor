using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.powers;

public sealed class LibraryOfRuinaDodgeDicePower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public int PendingAvoidedDamage { get; set; }

        public bool Broken { get; set; }
    }

    protected override string LegacyPowerId => "DODGE_DICE_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    protected override object InitInternalData() => new Data();

    public static bool HasActiveDodge(Creature? creature) =>
        creature is { Block: > 0 } && creature.GetPower<LibraryOfRuinaDodgeDicePower>() is { } power && !power.GetData().Broken;

    public static bool ShouldUseDodgeBlockIcon(Creature? creature) =>
        creature?.GetPower<LibraryOfRuinaDodgeDicePower>() is { } power && !power.GetData().Broken;

    public static async Task ApplyDodge(
        PlayerChoiceContext choiceContext,
        Creature owner,
        int amount,
        Creature? applier,
        CardModel? cardSource)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        await ApplyDodgeInternal(choiceContext, owner, amount, applier, cardSource);
    }

    public static Task ApplyDodge(
        Creature owner,
        int amount,
        Creature? applier,
        CardModel? cardSource)
    {
        return ApplyDodgeInternal(null, owner, amount, applier, cardSource);
    }

    private static async Task ApplyDodgeInternal(
        PlayerChoiceContext? choiceContext,
        Creature owner,
        int amount,
        Creature? applier,
        CardModel? cardSource)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (owner.IsDead || amount <= 0)
        {
            return;
        }

        LibraryOfRuinaDodgeDicePower? power = owner.GetPower<LibraryOfRuinaDodgeDicePower>();
        if (power == null)
        {
            power = choiceContext == null
                ? await PowerCmdCompat.Apply<LibraryOfRuinaDodgeDicePower>(
                    owner,
                    amount,
                    applier,
                    cardSource,
                    silent: true)
                : await PowerCmdCompat.Apply<LibraryOfRuinaDodgeDicePower>(
                    choiceContext,
                    owner,
                    amount,
                    applier,
                    cardSource,
                    silent: true);
        }
        else
        {
            power.GetData().Broken = false;
            power.GetData().PendingAvoidedDamage = 0;
            if (choiceContext == null)
            {
                await PowerCmdCompat.ModifyAmount(
                    power,
                    amount - power.Amount,
                    applier,
                    cardSource,
                    silent: true);
            }
            else
            {
                await PowerCmdCompat.ModifyAmount(
                    choiceContext,
                    power,
                    amount - power.Amount,
                    applier,
                    cardSource,
                    silent: true);
            }
        }

        int blockDelta = amount - owner.Block;
        if (blockDelta > 0)
        {
            // 原版只在格挡从 0 变为非 0 时播放格挡淡入（NCreatureStateDisplay），其余情况入队会留下没人消耗的计数，
            // 之后吞掉一次普通格挡的淡入。
            if (owner.Block == 0)
            {
                DodgeDiceCombatFeedback.QueueDodgeBlockGain(owner);
            }

            owner.GainBlockInternal(blockDelta);
        }
        else if (blockDelta < 0)
        {
            owner.LoseBlockInternal(-blockDelta);
        }
    }

#if STS2_0_111_0
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (!ShouldHandleDamage(target, amount, props, dealer))
        {
            return 0m;
        }

        int dodgeValue = Owner.Block;
        Data data = GetData();
        bool isActualDamageResolution = IsActualDamageResolution(cardSource, dealer);
        if (dodgeValue <= 0)
        {
            if (isActualDamageResolution)
            {
                data.Broken = true;
            }
            return 0m;
        }

        if (amount <= dodgeValue)
        {
            if (isActualDamageResolution)
            {
                data.PendingAvoidedDamage += Math.Max(0, (int)amount);
                Flash();
            }

            return -amount;
        }

        if (isActualDamageResolution)
        {
            BreakDodge(data);
        }

        return 0m;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        Data data = GetData();
        int avoidedDamage = data.PendingAvoidedDamage;
        data.PendingAvoidedDamage = 0;

        if (target == Owner
            && avoidedDamage > 0
            && Owner is LibraryCreature { MaxChaoValue: > 0 } libraryCreature)
        {
            if (result.WasFullyBlocked && CombatManager.Instance.IsInProgress)
            {
                DodgeDiceCombatFeedback.QueueDodgeAvoidedHit(Owner);
            }

            await LibraryCreatureCmd.HealChaoValue(libraryCreature, avoidedDamage);
        }

        if (data.Broken)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (creature == Owner)
        {
            if (Owner.Block > 0)
            {
                Owner.LoseBlockInternal(Owner.Block);
            }

            await PowerCmd.Remove(this);
        }
    }

    private bool ShouldHandleDamage(Creature? target, decimal amount, ValueProp props, Creature? dealer) =>
        target == Owner
        && !GetData().Broken
        && amount > 0m
        && dealer != null
        && dealer.Side != Owner.Side
        && ValuePropCompat.IsPoweredAttack(props);

    private void BreakDodge(Data data)
    {
        data.Broken = true;
        data.PendingAvoidedDamage = 0;
        if (Owner.Block > 0)
        {
            Owner.LoseBlockInternal(Owner.Block);
        }
    }

    private bool IsActualDamageResolution(CardModel? cardSource, Creature? dealer)
    {
        if (!DodgeDiceCombatFeedback.IsResolvingActualDamage)
        {
            return false;
        }

        if (cardSource != null)
        {
            return cardSource.Pile?.Type == PileType.Play;
        }

        return dealer != null && Owner.CombatState?.CurrentSide == dealer.Side;
    }

    private Data GetData() => GetInternalData<Data>();
}
