using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.BurrowingHeaven;

public sealed class HeavenThorn : CounterIntentMonsterModel
{
    private const string SleepMoveId = "SLEEP";
    private const string WitheringThornMoveId = "WITHERING_THORN";
    private const string BloodyThornMoveId = "BLOODY_THORN";
    private const string ExtendingThornMoveId = "EXTENDING_THORN";
    private const string RouterStateId = "HEAVEN_THORN_ROUTER";

    private const int WitheringThornMinDamage = 17;
    private const int WitheringThornMaxDamage = 21;
    private const int WitheringThornBleed = 3;
    private const int WitheringThornBlock = 6;
    private const int BloodyThornMinDamage = 12;
    private const int BloodyThornMaxDamage = 14;
    private const int BloodyThornHits = 2;
    private const int BloodyThornBleed = 6;
    private const int ExtendingThornBlock = 23;
    private const int ExtendingThornStrength = 3;

    internal const string TextureRoot = "res://images/monsters/heaven_thorn/";
    internal const string AwakeTexturePath = TextureRoot + "idle_awake.png";
    internal const string SleepTexturePath = TextureRoot + "idle_sleep.png";
    internal const string AttackTexturePath = TextureRoot + "attack.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string GuardTexturePath = TextureRoot + "guard.png";

    private static readonly string[] AdditionalAssetPaths =
    [
        BurrowingHeaven.AttackSfxPath,
        "res://images/powers/heaven_thorn_do_not_shift_gaze_passive_power.png",
        "res://images/powers/heaven_thorn_invisible_connection_passive_power.png",
        "res://images/powers/heaven_thorn_sleep_power.png"
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private bool _isAwake;
    private string? _lastMoveId;
    private int? _witheringDamageRoll;
    private int? _bloodyDamageRoll;
    private MoveState? _sleepState;

    public bool IsAwake => _isAwake;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 157, 120);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 160, 125);

    public override int DefaultChaoResistance => 90;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override IEnumerable<string> AssetPaths =>
        HeavenThornCreatureVisuals
            .Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public void SetInitialAwake(bool awake)
    {
        _isAwake = awake;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (_isAwake)
        {
            await ApplyAwakePassives();
        }
        else
        {
            await PowerCmdCompat.Apply<HeavenThornSleepPower>(
                Creature,
                BurrowingHeavenEncounterHelper.SleepTurns,
                BurrowingHeavenEncounterHelper.GetBoss(Creature.CombatState)?.Creature ?? Creature,
                null,
                silent: true);
        }
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1, Creature, null, silent: true);
        ForceRefreshMoveState();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
        _sleepState = null;
    }

    protected override bool ShouldQueueCounterIntentsForCurrentMove()
    {
        return _isAwake && base.ShouldQueueCounterIntentsForCurrentMove();
    }

    public async Task WakeFromSleep(PlayerChoiceContext choiceContext)
    {
        if (Creature.IsDead)
        {
            return;
        }

        _isAwake = true;
        await PowerCmdCompat.RemoveIfPresent<HeavenThornSleepPower>(
            Creature);

        await EnsureAwakePassives();
        await CreatureCmd.TriggerAnim(Creature, "Awake", 0f);
        ForceRefreshMoveState();
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(choiceContext, Creature.CombatState);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        _sleepState = Register(new MoveState(SleepMoveId, _ => Task.CompletedTask, new HiddenIntent()));
        MoveState witheringThorn = Register(new MoveState(
            WitheringThornMoveId,
            WitheringThornMove,
            new CombinedCounterAttackDefendIntent(
                () => GetWitheringDamageRoll(),
                () => 1,
                "HEAVEN_THORN_WITHERING_THORN.description",
                (choiceContext, _, target) => PerformCounterAttack(
                    choiceContext,
                    target,
                    GetWitheringDamageRoll(),
                    hitCount: 1,
                    bleed: WitheringThornBleed,
                    blockAfter: WitheringThornBlock),
                WitheringThornBlock,
                IntentBadge.Bleed(WitheringThornBleed))));
        MoveState bloodyThorn = Register(new MoveState(
            BloodyThornMoveId,
            BloodyThornMove,
            new CombinedCounterAttackDebuffIntent(
                () => GetBloodyDamageRoll(),
                () => BloodyThornHits,
                "HEAVEN_THORN_BLOODY_THORN.description",
                (choiceContext, _, target) => PerformCounterAttack(
                    choiceContext,
                    target,
                    GetBloodyDamageRoll(),
                    BloodyThornHits,
                    BloodyThornBleed),
                IntentBadge.Bleed(BloodyThornBleed))));
        MoveState extendingThorn = Register(new MoveState(
            ExtendingThornMoveId,
            ExtendingThornMove,
            new CombinedDefendBuffIntent(
                ExtendingThornBlock,
                "HEAVEN_THORN_EXTENDING_THORN.description",
                IntentBadge.Strength(ExtendingThornStrength))));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        _sleepState.FollowUpState = router;
        witheringThorn.FollowUpState = router;
        bloodyThorn.FollowUpState = router;
        extendingThorn.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [_sleepState, witheringThorn, bloodyThorn, extendingThorn, router],
            router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        if (!_isAwake)
        {
            return SleepMoveId;
        }

        string[] candidates =
        [
            WitheringThornMoveId,
            BloodyThornMoveId,
            ExtendingThornMoveId
        ];
        string[] filtered = candidates.Where(moveId => moveId != _lastMoveId).ToArray();
        string selected = rng.NextItem(filtered.Length > 0 ? filtered : candidates) ?? candidates[0];
        EnsureRollForMove(selected);
        return selected;
    }

    private async Task WitheringThornMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = WitheringThornMoveId;
        _witheringDamageRoll = null;
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(new ThrowingPlayerChoiceContext(), Creature.CombatState);
    }

    private async Task BloodyThornMove(IReadOnlyList<Creature> targets)
    {
        _lastMoveId = BloodyThornMoveId;
        _bloodyDamageRoll = null;
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(new ThrowingPlayerChoiceContext(), Creature.CombatState);
    }

    private async Task ExtendingThornMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
        await CreatureCmd.GainBlock(Creature, ExtendingThornBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, ExtendingThornStrength, Creature, null);
        _lastMoveId = ExtendingThornMoveId;
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(new ThrowingPlayerChoiceContext(), Creature.CombatState);
    }

    private async Task PerformCounterAttack(
        PlayerChoiceContext choiceContext,
        Creature target,
        int damage,
        int hitCount,
        int bleed,
        int blockAfter = 0)
    {
        if (CombatManager.Instance.IsOverOrEnding || Creature.IsDead || !target.IsAlive || !_isAwake)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(BurrowingHeaven.AttackSfxPath, -2f);
        IReadOnlyList<DamageResult> results;
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, [target]))
        {
            BurrowingHeavenEncounterHelper.TryPlayCounterHitVfx(
                target,
                "vfx/vfx_dramatic_stab",
                $"{nameof(HeavenThorn)}.{nameof(PerformCounterAttack)}");
            AttackCommand attack = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithHitCount(hitCount)
                .OnlyPlayAnimOnce()
                .WithAttackerAnim("Attack", 0.34f)
                .Execute(choiceContext);
            results = AttackCommandCompat.Results(attack);
        }

        foreach (Creature hitTarget in results
            .Select(static result => result.Receiver)
            .Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                choiceContext,
                hitTarget,
                bleed,
                Creature,
                null);
        }

        await BurrowingHeavenEncounterHelper.ReflectFullyBlockedCounter(
            choiceContext,
            Creature,
            results,
            BurrowingHeavenEncounterHelper.CounterReflectPercent);

        if (blockAfter > 0 && !Creature.IsDead && _isAwake)
        {
            await GainCounterBlock(Creature, blockAfter);
        }
    }

    private async Task GainCounterBlock(Creature owner, int block)
    {
        await CreatureCmd.TriggerAnim(owner, "Guard", 0f);
        await CreatureCmd.GainBlock(owner, block, ValueProp.Move, null);
    }

    private async Task ApplyAwakePassives()
    {
        await PowerCmdCompat.Apply<HeavenThornDoNotShiftGazePassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<HeavenThornInvisibleConnectionPassivePower>(Creature, 1, Creature, null, silent: true);
    }

    private async Task EnsureAwakePassives()
    {
        await PowerCmdCompat.Ensure<
            HeavenThornDoNotShiftGazePassivePower>(Creature);
        await PowerCmdCompat.Ensure<
            HeavenThornInvisibleConnectionPassivePower>(Creature);
    }

    private void ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }

        string moveId = ResolvePlannedMoveId(RunRng.MonsterAi);
        if (_statesById.TryGetValue(moveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
            // Direct placement before the first roll bypasses the state log.
            // Native Stun uses its last entry as the return move.
            if (MoveStateMachine.StateLog.Count == 0)
            {
                MoveStateMachine.StateLog.Add(state);
            }
            SyncCounterIntentsWithCurrentMove();
        }
    }

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private int GetWitheringDamageRoll() =>
        GetOrRollDamage(ref _witheringDamageRoll, WitheringThornMinDamage, WitheringThornMaxDamage);

    private int GetBloodyDamageRoll() =>
        GetOrRollDamage(ref _bloodyDamageRoll, BloodyThornMinDamage, BloodyThornMaxDamage);

    private void EnsureRollForMove(string moveId)
    {
        switch (moveId)
        {
            case WitheringThornMoveId:
                EnsureDamageRoll(ref _witheringDamageRoll, WitheringThornMinDamage, WitheringThornMaxDamage);
                break;
            case BloodyThornMoveId:
                EnsureDamageRoll(ref _bloodyDamageRoll, BloodyThornMinDamage, BloodyThornMaxDamage);
                break;
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState moveState)
            {
                foreach (AbstractIntent intent in moveState.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented
            && creature == Creature
            && Creature.CombatState?.CurrentSide == CombatSide.Player)
        {
            await BurrowingHeavenEncounterHelper.RefreshEncounterState(choiceContext, Creature.CombatState);
        }
    }


}
