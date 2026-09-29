using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters.BurrowingHeaven;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.BurrowingHeaven;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.BurrowingHeaven;

public sealed class BurrowingHeavenPerfectFocusPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BURROWING_HEAVEN_PERFECT_FOCUS_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class BurrowingHeavenWingsTowardOldGodPassivePower : LibraryOfRuinaPowerModel
{
    private const int WeakStacks = 10;
    private const int WeakTurns = 1;

    protected override string LegacyPowerId => "BURROWING_HEAVEN_WINGS_TOWARD_OLD_GOD_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryWeakPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Weak", WeakStacks),
        new DynamicVar("Turns", WeakTurns),
        new DynamicVar("ReflectPercent", BurrowingHeavenEncounterHelper.CounterReflectPercent)
    ];

}

public sealed class BurrowingHeavenInCognitionPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BURROWING_HEAVEN_IN_COGNITION_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", BurrowingHeavenEncounterHelper.SleepTurns)
    ];
}

public sealed class HeavenThornDoNotShiftGazePassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "HEAVEN_THORN_DO_NOT_SHIFT_GAZE_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpPercent", BurrowingHeavenEncounterHelper.FriendlyGazeDamagePercent)
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (!BurrowingHeavenEncounterHelper.IsStageThornPassiveController(Owner)
            || !cardPlay.IsFirstInSeries
            || cardPlay.Card.Type != CardType.Attack
            || cardPlay.Target == null)
        {
            return;
        }

        Creature actor = cardPlay.Card.Owner.Creature;
        Creature target = cardPlay.Target;
        if (!actor.IsAlive
            || !target.IsAlive
            || target == Owner)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(BurrowingHeavenEncounterHelper.PickScreamSfx(Owner), -12f);
        foreach (Creature player in BurrowingHeavenEncounterHelper.LivingPlayers(Owner.CombatState))
        {
            decimal damage = Math.Max(1m, Math.Ceiling(player.MaxHp * BurrowingHeavenEncounterHelper.FriendlyGazeDamagePercent / 100m));
            await CreatureCmdCompat.Damage(
                context,
                player,
                damage,
                ValueProp.Unpowered | ValueProp.Move,
                Owner,
                null);
        }
    }
}

public sealed class HeavenThornInvisibleConnectionPassivePower : LibraryOfRuinaPowerModel
{
    private const int WeakStacks = 1;
    private const int WeakTurns = 1;

    protected override string LegacyPowerId => "HEAVEN_THORN_INVISIBLE_CONNECTION_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryWeakPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Weak", WeakStacks),
        new DynamicVar("Turns", WeakTurns)
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (!BurrowingHeavenEncounterHelper.IsStageThornPassiveController(Owner)
            || !cardPlay.IsFirstInSeries)
        {
            return;
        }

        Creature actor = cardPlay.Card.Owner.Creature;
        if (!actor.IsAlive)
        {
            return;
        }
        LibraryWeakPower? weakPower = actor.GetPowerInstances<LibraryWeakPower>()
        .FirstOrDefault(power => power.TurnsRemaining > 0);
        if (weakPower == null)
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                new ThrowingPlayerChoiceContext(),
                actor,
                WeakStacks,
                WeakTurns - 1,
                IsPermanent: false,
                Owner,
                cardPlay.Card);
        }
        else
        {
            await LibraryPowerCmd.ModifyAmount(
                new ThrowingPlayerChoiceContext(),
                weakPower,
                1,
                0,
                IsPermanent: false,
                Owner,
                cardPlay.Card);
        }
    }

    

}

public sealed class BurrowingHeavenSleepPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BURROWING_HEAVEN_SLEEP_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", BurrowingHeavenEncounterHelper.SleepTurns)
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner.IsDead)
        {
            return;
        }

        if (BurrowingHeavenEncounterHelper.IsBossStaggered(Owner.CombatState))
        {
            return;
        }

        if (Amount > 1)
        {
            await PowerCmdCompat.ModifyAmount(choiceContext, this, -1, Owner, null, silent: true);
            return;
        }

        await PowerCmd.Remove(this);
        await BurrowingHeavenEncounterHelper.SpawnOrWakeAwakeThorn(choiceContext, Owner.CombatState);
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(choiceContext, Owner.CombatState);
    }
}

public sealed class HeavenThornSleepPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "HEAVEN_THORN_SLEEP_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", BurrowingHeavenEncounterHelper.SleepTurns)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player || Owner.IsDead)
        {
            return;
        }

        // Initial setup happens immediately before the first player turn starts.
        if (combatState.RoundNumber == 1)
        {
            return;
        }

        if (BurrowingHeavenEncounterHelper.IsBossStaggered(Owner.CombatState))
        {
            return;
        }

        if (Amount > 1)
        {
            await PowerCmdCompat.ModifyAmount(choiceContext, this, -1, Owner, null, silent: true);
            return;
        }

        await PowerCmd.Remove(this);
        if (Owner.Monster is HeavenThorn thorn)
        {
            await thorn.WakeFromSleep(choiceContext);
        }
    }
}
