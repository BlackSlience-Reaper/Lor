using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.SpeedDice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.interop;
using LibraryOfRuina.monsters;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests;

/// <summary>
/// Reusable combat contract for special guests.  It owns the standard
/// five-level emotion track, shared emotion UI, intent capacity and
/// deterministic five-slot plan carrier.  Guest-specific classes provide only
/// their initial capacity and move execution; presentation code is read-only
/// against this model.  This state lives for the current combat only: the game
/// never saves monster models and multiplayer syncs only HP, block and power
/// amounts, so none of it is a [SavedProperty].
/// </summary>
public abstract class SpecialGuestMonsterBase :
    LorMonsterModel,
    ILibraryEmotionBarSource
{
    protected const int StoredIntentSlots = 5;
    private static readonly int[] DefaultEmotionThresholds = [3, 3, 5, 7, 9];

    public int EmotionLevel { get; private set; }

    public int EmotionUnits { get; private set; }

    public int IntentCapacity { get; protected set; }

    public int LevelFiveRoundCounter { get; private set; }

    public int LastEmotionResolvedRound { get; private set; } = -1;

    public int UnblockedDamageDealtThisRound { get; private set; }

    public int PatternIndex { get; protected set; }

    public bool HasCompletedFirstTurn { get; protected set; }

    public int PlannedMoveOne { get; private set; } = -1;

    public int PlannedMoveTwo { get; private set; } = -1;

    public int PlannedMoveThree { get; private set; } = -1;

    public int PlannedMoveFour { get; private set; } = -1;

    public int PlannedMoveFive { get; private set; } = -1;

    public int CurrentEmotionThreshold =>
        !HasEmotionTrack || EmotionLevel >= EmotionThresholds.Count
            ? 0
            : EmotionThresholds[EmotionLevel];

    public IReadOnlyList<int> EmotionUnitThresholds => EmotionThresholds;

    /// <summary>
    /// Guests such as attached body parts can use the shared multi-intent
    /// carrier without owning an emotion track or emotion UI.
    /// </summary>
    public virtual bool HasEmotionTrack => true;

    public event Action? EmotionChanged;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        EmotionChanged = null;
    }

    public int StoredIntentCount => Enumerable.Range(0, StoredIntentSlots)
        .TakeWhile(slot => GetStoredIntent(slot) >= 0)
        .Count();

    protected virtual IReadOnlyList<int> EmotionThresholds =>
        DefaultEmotionThresholds;

    protected abstract int InitialIntentCapacity { get; }

    protected bool HasStoredIntentPlan => PlannedMoveOne >= 0;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (IntentCapacity <= 0)
        {
            IntentCapacity = InitialIntentCapacity;
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player
            && Creature.IsAlive
            && HasEmotionTrack
            && CombatManager.Instance.PlayersTakingExtraTurn.Count == 0
            && LastEmotionResolvedRound != combatState.RoundNumber)
        {
            LastEmotionResolvedRound = combatState.RoundNumber;
            await ResolveEmotionAtPlayerTurnStart();
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        if (target == Creature
            && Creature.IsAlive
            && dealer is { IsPlayer: true }
            && cardSource != null
            && ValuePropCompat.IsPoweredAttack(props)
            && ActualHpDamage(result) > 0)
        {
            GrantEmotionUnits(1);
        }

        return Task.CompletedTask;
    }

    protected void RecordDirectAttackDamageDealt(
        IEnumerable<DamageResult> results)
    {
        UnblockedDamageDealtThisRound += results.Sum(ActualHpDamage);
    }

    protected static int ActualHpDamage(DamageResult result) =>
        Math.Max(0, result.UnblockedDamage - result.OverkillDamage);

    protected int ScaleSpecialGuestAmount(int baseAmount) =>
        (int)Math.Ceiling(
            MultiplayerScalingPatchHelper.ScaleHpAmount(
                Creature.CombatState,
                this,
                baseAmount));

    protected int GetStoredIntent(int slot) => slot switch
    {
        0 => PlannedMoveOne,
        1 => PlannedMoveTwo,
        2 => PlannedMoveThree,
        3 => PlannedMoveFour,
        4 => PlannedMoveFive,
        _ => -1,
    };

    protected void SetStoredIntent(int slot, int value)
    {
        switch (slot)
        {
            case 0: PlannedMoveOne = value; break;
            case 1: PlannedMoveTwo = value; break;
            case 2: PlannedMoveThree = value; break;
            case 3: PlannedMoveFour = value; break;
            case 4: PlannedMoveFive = value; break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    protected void ClearStoredIntentPlan()
    {
        for (int slot = 0; slot < StoredIntentSlots; slot++)
        {
            SetStoredIntent(slot, -1);
        }
    }

    /// <summary>
    /// Restores the shared Special Guest state after an encounter-stage
    /// transition.  A new combat owns new Creature and Power instances, so
    /// only stable scalar state is carried here; guest-specific code restores
    /// its own stance, plans, and Power snapshot after this call.
    /// </summary>
    protected void RestoreSharedReceptionState(
        int emotionLevel,
        int emotionUnits,
        int intentCapacity,
        int levelFiveRoundCounter)
    {
        EmotionLevel = Math.Clamp(emotionLevel, 0, EmotionThresholds.Count);
        EmotionUnits = EmotionLevel >= EmotionThresholds.Count
            ? (EmotionThresholds.Count == 0 ? 0 : EmotionThresholds[^1])
            : Math.Clamp(
                emotionUnits,
                0,
                EmotionThresholds[EmotionLevel]);
        IntentCapacity = Math.Clamp(intentCapacity, 1, StoredIntentSlots);
        LevelFiveRoundCounter = Math.Max(0, levelFiveRoundCounter);

        // Round numbers restart in the next CombatRoom.  Reset every
        // per-combat latch so the restored model resolves exactly once on the
        // next player turn and never reuses a stage-one intent plan.
        LastEmotionResolvedRound = -1;
        UnblockedDamageDealtThisRound = 0;
        ClearStoredIntentPlan();
        EmotionChanged?.Invoke();
    }

    private async Task ResolveEmotionAtPlayerTurnStart()
    {
        bool wasLevelFive = EmotionLevel >= EmotionThresholds.Count;
        if (EmotionLevel < EmotionThresholds.Count)
        {
            int gained = 0;
            if (UnblockedDamageDealtThisRound >= ScaleSpecialGuestAmount(6))
            {
                gained += 2;
            }

            if (gained > 0)
            {
                GrantEmotionUnits(gained);
            }

            await ResolvePendingEmotionLevelUps();
        }
        else
        {
            LockEmotionAtMaximum();
        }

        UnblockedDamageDealtThisRound = 0;

        if (EmotionLevel >= EmotionThresholds.Count)
        {
            LevelFiveRoundCounter = ResolveLevelFiveRoundStep(
                LevelFiveRoundCounter,
                reachedLevelFiveThisTurn: !wasLevelFive);
        }
        else
        {
            LevelFiveRoundCounter = 0;
        }

        if (ShouldGrantLevelFiveIntangible(
                EmotionLevel,
                EmotionThresholds.Count,
                LevelFiveRoundCounter))
        {
            await PowerCmdCompat.Apply<IntangiblePower>(
                Creature,
                1,
                Creature,
                null);
        }

        EmotionChanged?.Invoke();
    }

    internal static int ResolveLevelFiveRoundStep(
        int currentStep,
        bool reachedLevelFiveThisTurn)
    {
        if (reachedLevelFiveThisTurn)
        {
            return 1;
        }

        return currentStep switch
        {
            1 => 2,
            2 => 3,
            _ => 1
        };
    }

    internal static bool ShouldGrantLevelFiveIntangible(
        int emotionLevel,
        int emotionLevelCount,
        int levelFiveRoundStep) =>
        emotionLevelCount > 0
        && emotionLevel >= emotionLevelCount
        && levelFiveRoundStep == 1;

    protected void GrantEmotionUnits(int units)
    {
        if (units <= 0)
        {
            return;
        }

        if (EmotionLevel >= EmotionThresholds.Count)
        {
            if (LockEmotionAtMaximum())
            {
                EmotionChanged?.Invoke();
            }
            return;
        }

        int threshold = CurrentEmotionThreshold;
        int nextUnits = Math.Clamp(EmotionUnits + units, 0, threshold);
        if (nextUnits == EmotionUnits)
        {
            return;
        }

        EmotionUnits = nextUnits;
        EmotionChanged?.Invoke();
    }

    private async Task ResolvePendingEmotionLevelUps()
    {
        if (TryAdvanceOneEmotionLevel(out int newLevel))
        {
            await ApplyEmotionLevelReward(newLevel);
        }
    }

    private bool TryAdvanceOneEmotionLevel(out int newLevel)
    {
        newLevel = EmotionLevel;
        if (EmotionLevel >= EmotionThresholds.Count)
        {
            LockEmotionAtMaximum();
            return false;
        }

        int threshold = CurrentEmotionThreshold;
        EmotionUnits = Math.Clamp(EmotionUnits, 0, threshold);
        if (threshold <= 0 || EmotionUnits < threshold)
        {
            return false;
        }

        EmotionLevel++;
        newLevel = EmotionLevel;
        EmotionUnits = EmotionLevel >= EmotionThresholds.Count
            ? EmotionThresholds[^1]
            : 0;
        return true;
    }

    private bool LockEmotionAtMaximum()
    {
        if (EmotionThresholds.Count == 0)
        {
            return false;
        }

        int lockedUnits = EmotionThresholds[^1];
        if (EmotionUnits == lockedUnits)
        {
            return false;
        }

        EmotionUnits = lockedUnits;
        return true;
    }

    protected virtual async Task ApplyEmotionLevelReward(int level)
    {
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Creature,
            level == 1 ? 3 : 1,
            -1,
            Creature,
            null);
        switch (level)
        {
            case 2:
                await PowerCmdCompat.Apply<RegenPower>(
                    Creature,
                    20,
                    Creature,
                    null);
                break;
            case 3:
                await PowerCmdCompat.Apply<PlatingPower>(
                    Creature,
                    50,
                    Creature,
                    null);
                break;
            case 4:
                IntentCapacity = Math.Min(StoredIntentSlots, IntentCapacity + 1);
                break;
        }
    }
}
