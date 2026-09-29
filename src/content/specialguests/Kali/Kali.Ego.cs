using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.specialguests.Kali;

// E.G.O.：首次血量跌破阈值时本回合锁血并排队显现，下一个玩家回合开始显现；混乱值归零或被眩晕时解除，
// 解除后数两个玩家回合再显现。强壮/忍耐按“基础 2 + 血雾层数”维护本模组自己加上的那一份。
public sealed partial class Kali
{

    private decimal ClampHpLossForFirstEgoTrigger(Creature target, decimal num)
    {
        if (target != Creature || num <= 0m)
        {
            return num;
        }

        decimal threshold = ScaledEgoHpThreshold;
        bool lockActive = IsEgoThresholdTurnLockActive();
        decimal resolved = ResolveThresholdTurnLockedHpLoss(
            Creature.CurrentHp,
            num,
            threshold,
            EgoThresholdTurnLockConsumed,
            lockActive,
            out bool activateLock);
        if (activateLock)
        {
            ActivateEgoThresholdTurnLock();
        }

        return resolved;
    }

    private bool IsEgoThresholdTurnLockActive()
    {
        CombatStateLike? combatState = Creature.CombatState;
        return combatState != null
               && EgoThresholdTurnLockRound == combatState.RoundNumber
               && EgoThresholdTurnLockSide == (int)combatState.CurrentSide;
    }

    private void ActivateEgoThresholdTurnLock()
    {
        EgoThresholdTurnLockConsumed = true;
        CombatStateLike? combatState = Creature.CombatState;
        EgoThresholdTurnLockRound = combatState?.RoundNumber ?? -1;
        EgoThresholdTurnLockSide = combatState == null
            ? -1
            : (int)combatState.CurrentSide;
    }

    internal static decimal ResolveThresholdTurnLockedHpLoss(
        decimal currentHp,
        decimal requestedLoss,
        decimal threshold,
        bool lockConsumed,
        bool lockActive,
        out bool activateLock)
    {
        activateLock = false;
        if (requestedLoss <= 0m)
        {
            return requestedLoss;
        }

        decimal hpAfter = currentHp - requestedLoss;
        if (!lockActive && !lockConsumed && hpAfter < threshold)
        {
            activateLock = true;
            lockActive = true;
        }

        return lockActive && hpAfter < threshold
            ? Math.Max(0m, currentHp - threshold)
            : requestedLoss;
    }

    private void QueueFirstEgoManifestation(Creature creature)
    {
        if (creature != Creature
            || !ShouldQueueFirstEgoManifestation(
                EgoTriggered,
                EgoManifestationPending,
                Creature.CurrentHp,
                ScaledEgoHpThreshold))
        {
            return;
        }

        EgoManifestationPending = true;
    }

    internal static bool ShouldQueueFirstEgoManifestation(
        bool egoTriggered,
        bool manifestationPending,
        decimal currentHp,
        decimal threshold) =>
        !egoTriggered && !manifestationPending && currentHp <= threshold;

    private async Task ResolvePendingEgoManifestation()
    {
        if (!EgoManifestationPending || EgoTriggered || Creature.IsDead)
        {
            return;
        }

        await TriggerEgoManifestation(firstTrigger: true);
    }

    private async Task TrackUnblockedPlayerDamage(
        Creature? dealer,
        DamageResult results,
        ValueProp props,
        Creature target)
    {
        if (dealer != Creature || target.Side != CombatSide.Player || results.UnblockedDamage <= 0m)
        {
            return;
        }

        if (ReferenceEquals(_lastProcessedDamageResult, results))
        {
            return;
        }

        _lastProcessedDamageResult = results;
        if (props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered))
        {
            _directUnblockedDamageThisEnemyTurn += results.UnblockedDamage;
        }

        if (EgoActive)
        {
            await GainBloodMistStack();
        }
    }

    public override async Task AfterCurrentChaoValueChanged(Creature target, decimal amount, LibraryDamageType type)
    {
        if (target == Creature && EgoActive && amount < 0m && target is LibraryCreature { CurrentChaoValue: <= 0 })
        {
            await DismissEgo();
        }
    }

    public override async Task AfterStun(Creature creature)
    {
        if (creature != Creature)
        {
            return;
        }

        if (EgoActive)
        {
            await DismissEgo();
        }

        // EGO dismissal mutates powers after StunInternal's first UI refresh.
        // Refresh once more so Fatal resistances and the native stun intent win visually.
        if (creature is LibraryCreature libraryCreature)
        {
            libraryCreature.HealthBar?.RefreshValues();
        }

        if (NCombatRoom.Instance?.GetCreatureNode(creature) is { } creatureNode)
        {
            await creatureNode.RefreshIntents();
        }
    }

    private async Task TriggerEgoManifestation(bool firstTrigger)
    {
        if (Creature.IsDead)
        {
            return;
        }

        if (firstTrigger)
        {
            EgoManifestationPending = false;
            EgoTriggered = true;
            decimal threshold = ScaledEgoHpThreshold;
            if (Creature.CurrentHp < threshold)
            {
                await CreatureCmd.SetCurrentHp(Creature, threshold);
            }
        }

        EgoReturnCountdown = 0;
        EgoActive = true;
        _forceManifestationCardsInNextPlan = true;
        ClearStoredIntentPlan();
        _enemyCards?.RefreshDefaultPlan();

        if (Creature is LibraryCreature lc)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(lc, lc.MaxChaoValue);
        }

        await PowerCmdCompat.Ensure<RedMistEgoPower>(
            Creature,
            1m,
            Creature,
            null);

        if (PersistedBloodMistStacks > 0)
        {
            RedMistBloodMistPower? bloodMistPower = Creature.GetPower<RedMistBloodMistPower>();
            if (bloodMistPower == null)
            {
                await PowerCmdCompat.Apply<RedMistBloodMistPower>(Creature, PersistedBloodMistStacks, Creature, null);
            }
            else
            {
                bloodMistPower.SetAmount(PersistedBloodMistStacks, silent: true);
            }
        }

        await RefreshRedMistStrongEnduranceContributions();
        KaliCreatureVisuals.SetEgoState(Creature, active: true);
        EncounterBgmController.ForceCurrentEncounterTrack(EgoBgmPath, "RedMistEgoBGM");
        NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents();
    }

    private async Task DismissEgo()
    {
        if (!EgoActive)
        {
            return;
        }

        EgoActive = false;
        EgoReturnCountdown = EgoReturnTurns;
        _forceManifestationCardsInNextPlan = false;
        await RefreshRedMistStrongEnduranceContributions();

        PowerModel? egoPower = Creature.GetPower<RedMistEgoPower>();
        if (egoPower != null)
        {
            await PowerCmd.Remove(egoPower);
        }

        PowerModel? bloodMistPower = Creature.GetPower<RedMistBloodMistPower>();
        if (bloodMistPower != null)
        {
            await PowerCmd.Remove(bloodMistPower);
        }

        KaliCreatureVisuals.SetEgoState(Creature, active: false);
    }

    private async Task TickEgoReturnCountdown()
    {
        if (EgoActive || !EgoTriggered || EgoReturnCountdown <= 0)
        {
            return;
        }

        EgoReturnCountdown = ResolveEgoReturnCountdownAfterPlayerTurnStart(
            EgoReturnCountdown);
        if (EgoReturnCountdown <= 0)
        {
            await TriggerEgoManifestation(firstTrigger: false);
        }
    }

    private async Task GainBloodMistStack()
    {
        if (PersistedBloodMistStacks >= BloodMistMaxStacks)
        {
            return;
        }

        PersistedBloodMistStacks++;
        await PowerCmdCompat.SetAmount<RedMistBloodMistPower>(Creature, PersistedBloodMistStacks, Creature, null);
        await QueueOrApplyEgoBuffRefresh();
    }

    private async Task QueueOrApplyEgoBuffRefresh()
    {
        if (_isExecutingEnemyCardSequence)
        {
            _pendingEgoBuffRefresh = true;
            return;
        }

        await RefreshRedMistStrongEnduranceContributions();
    }

    private async Task ApplyPendingPostSequenceBuffs()
    {
        bool changed = _pendingEgoBuffRefresh;
        _pendingEgoBuffRefresh = false;

        if (changed)
        {
            await RefreshRedMistStrongEnduranceContributions();
        }
    }

    private async Task RefreshRedMistStrongEnduranceContributions()
    {
        int egoBuffAmount = EgoActive ? EgoBaseBuffAmount + PersistedBloodMistStacks : 0;
        await SetRedMistStrongContribution(egoBuffAmount);
        await SetRedMistEnduranceContribution(egoBuffAmount);
        Log.Info(
            "[LibraryOfRuina.RedMist] Strong/Endurance refreshed: strong=" +
            _redMistStrongContribution +
            " endurance=" +
            _redMistEnduranceContribution +
            " bloodMist=" +
            PersistedBloodMistStacks);
    }

    private async Task SetRedMistStrongContribution(int targetContribution)
    {
        int clamped = Math.Max(0, targetContribution);
        int delta = clamped - _redMistStrongContribution;
        if (delta == 0)
        {
            return;
        }

        LibraryStrongPower? power = Creature.GetPower<LibraryStrongPower>();
        if (delta > 0 && power == null)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                delta,
                0,
                true,
                Creature,
                null);
        }
        else if (power != null)
        {
            await PowerCmdCompat.ModifyAmount(power, delta, Creature, null);
        }

        _redMistStrongContribution = clamped;
    }

    private async Task SetRedMistEnduranceContribution(int targetContribution)
    {
        int clamped = Math.Max(0, targetContribution);
        int delta = clamped - _redMistEnduranceContribution;
        if (delta == 0)
        {
            return;
        }

        LibraryEndurancePower? power = Creature.GetPower<LibraryEndurancePower>();
        if (delta > 0 && power == null)
        {
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                delta,
                0,
                true,
                Creature,
                null);
        }
        else if (power != null)
        {
            await PowerCmdCompat.ModifyAmount(power, delta, Creature, null);
        }

        _redMistEnduranceContribution = clamped;
    }

    private async Task ClearNegativePowers(PlayerChoiceContext choiceContext)
    {
        foreach (PowerModel power in Creature.Powers.ToArray())
        {
            if (power.TypeForCurrentAmount == PowerType.Debuff || power.Type == PowerType.Debuff)
            {
                await PowerCmd.Remove(power);
            }
        }
    }

    private async Task LoseCurrentChaoPercent()
    {
        if (Creature is not LibraryCreature lc || lc.CurrentChaoValue <= 0)
        {
            return;
        }

        decimal loss = Math.Ceiling(lc.CurrentChaoValue * ChaoLossPercent / 100m);
        await LibraryCreatureCmd.SetCurrentChaoValue(lc, Math.Max(0m, lc.CurrentChaoValue - loss));
    }

}
