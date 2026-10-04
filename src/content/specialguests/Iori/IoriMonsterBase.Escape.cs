using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.specialguests.Iori;

// 一阶段：血量压到一半就锁住并排队撤离，撤离前把接待状态存成快照；二阶段进场时读回快照。
public abstract partial class IoriMonsterBase
{
    private decimal ClampStageOneHpLoss(
        Creature target,
        decimal amount)
    {
        if (IsSecondStage
            || target != Creature
            || amount <= 0m
            || EscapeCompleted)
        {
            return amount;
        }

        decimal threshold = ResolveEscapeThreshold();
        decimal maxLoss = Math.Max(0m, target.CurrentHp - threshold);
        return Math.Min(amount, maxLoss);
    }

    internal decimal ClampFinalStageOneHpLoss(decimal amount) =>
        ClampStageOneHpLoss(Creature, amount);

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        ClampStageOneHpLoss(target, amount);

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        TryQueueEscapeAfterHpChange(creature, delta);
    }

    public virtual async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta,
        LibraryDamageType type)
    {
        TryQueueEscapeAfterHpChange(creature, delta);
    }

    private void TryQueueEscapeAfterHpChange(
        Creature creature,
        decimal delta)
    {
        if (!IsSecondStage
            && creature == Creature
            && delta < 0m
            && creature.CurrentHp <= ResolveEscapeThreshold())
        {
            QueueEscape();
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        await base.AfterDamageReceived(
            choiceContext,
            target,
            result,
            props,
            dealer,
            cardSource,
            type);

        if (target != Creature)
        {
            return;
        }

        if (!IsSecondStage
            && Creature.CurrentHp <= ResolveEscapeThreshold())
        {
            QueueEscape();
            _shouldLockHealthBar = true;
        }

        if (result.WasFullyBlocked)
        {
            LocalOggOneShotPlayer.Play(
                IoriSpecialGuestIds.CombatAudioRoot + "Purple_Warp.ogg",
                -2f);
            await CreatureCmd.TriggerAnim(Creature, "Evade", 0f);
            await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
        }
        else if (result.TotalDamage > 0m)
        {
            await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
        }
    }

    private void QueueEscape()
    {
        if (IsSecondStage || EscapeQueued || EscapeCompleted)
        {
            return;
        }

        EscapeQueued = true;
        RevealEscapeIntent();
    }

    private decimal ResolveEscapeThreshold() =>
        Math.Ceiling(Creature.MaxHp * 0.5m);

    private async Task PerformEscapeMove(
        IReadOnlyList<Creature> targets)
    {
        _shouldLockHealthBar = false;
        _ = targets;
        if (!EscapeQueued || EscapeCompleted || IsSecondStage)
        {
            return;
        }

        await PresentationGuard.RunAsync(PlayGuardAnimation, "Iori guard animation");
        await CompleteStageOneEscape();
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEndLate(
            choiceContext,
            side,
            participants);
        if (side == CombatSide.Enemy
            && EscapeQueued
            && !EscapeCompleted
            && !IsSecondStage)
        {
            await PresentationGuard.RunAsync(PlayGuardAnimation, "Iori guard animation");
            await CompleteStageOneEscape();
        }
    }

    private async Task CompleteStageOneEscape()
    {
        if (_escapeCompleting || EscapeCompleted || IsSecondStage)
        {
            return;
        }

        _escapeCompleting = true;
        try
        {
            IoriReceptionSnapshotStore.Save(this);
            await CreatureCmd.Escape(Creature);
            EscapeCompleted = true;
        }
        finally
        {
            _escapeCompleting = false;
        }
    }

    private async Task<bool> TryRestoreReceptionSnapshot()
    {
        if (IoriReceptionSnapshotStore.TryLoad(this) is not { } snapshot)
        {
            return false;
        }

        CurrentStance = snapshot.CurrentStance;
        HasExpandedRoundTwoCapacity = snapshot.HasExpandedRoundTwoCapacity;
        ReceptionRoundOffset = Math.Max(0, snapshot.ReceptionRound);
        LastRegularMove = snapshot.LastRegularMove;
        SelectedStanceMask = snapshot.SelectedStanceMask;
        RestoreSharedReceptionState(
            snapshot.EmotionLevel,
            snapshot.EmotionUnits,
            snapshot.IntentCapacity,
            snapshot.LevelFiveRoundCounter);

        await CreatureCmd.SetCurrentHp(
            Creature,
            ResolveEscapeThreshold());
        if (Creature is LibraryCreature libraryCreature)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(
                libraryCreature,
                libraryCreature.MaxChaoValue);
        }

        foreach (IoriPowerSnapshot power in snapshot.PositivePowers)
        {
            await IoriReceptionSnapshotStore.RestorePower(Creature, power);
        }

        if (CurrentStance == IoriStance.Blunt)
        {
            // Player combat state is rebuilt between stages; restore the
            // stance-owned Chains contribution on the new player creatures.
            await AddChainsContribution(2);
        }

        if (CurrentStance != IoriStance.None)
        {
            // The stance marker power is a neutral state and is not part of the
            // positive-power snapshot; restore it for the recovered stance.
            await ApplyStancePower(CurrentStance);
        }

        IoriCreatureVisuals.RefreshStance(Creature);
        return true;
    }
}
