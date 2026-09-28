using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.PriceOfSilence;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.PriceOfSilence;
using LibraryOfRuina.visuals.PriceOfSilence;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.PriceOfSilence;

public sealed class TimeTrace : LorMonsterModel
{
    internal const string TimeRecoilMoveId = "TIME_RECOIL";
    internal const string TimeTorrentMoveId = "TIME_TORRENT";
    internal const string CopyOffenseSingleMoveId = "COPY_OFFENSE_SINGLE";
    internal const string CopyOffenseTripleMoveId = "COPY_OFFENSE_TRIPLE";
    internal const string CopyDefenseSingleMoveId = "COPY_DEFENSE_SINGLE";
    internal const string CopyDefenseStrengthMoveId = "COPY_DEFENSE_STRENGTH";
    internal const string TimeRestoreMoveId = "TIME_RESTORE";
    private const string RouterStateId = "TIME_TRACE_ROUTER";

    internal const int TimeRecoilMinDamage = 11;
    internal const int TimeRecoilMaxDamage = 14;
    internal const int TimeRecoilBlock = 24;
    internal const int TimeTorrentMinDamage = 3;
    internal const int TimeTorrentMaxDamage = 6;
    internal const int TimeTorrentHits = 3;
    internal const int TimeTorrentDebuffs = 1;
    internal const int CopyOffenseTripleHits = 3;
    internal const int CopyDefenseStrength = 3;
    private const int MinimumRecoveryHp = 1;

    internal const string TextureRoot = "res://images/monsters/time_trace/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string AttackBluntTexturePath = TextureRoot + "attack_blunt.png";
    internal const string AttackThrustTexturePath = TextureRoot + "attack_thrust.png";
    internal const string AttackSlashTexturePath = TextureRoot + "attack_slash.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string GuardTexturePath = TextureRoot + "guard.png";

    private static readonly string[] AdditionalAssetPaths =
    [
        "res://images/powers/price_of_silence_your_time_passive_power.png",
        "res://images/powers/time_trace_restoration_power.png"
    ];

    private static readonly string[] CopyMoveIds =
    [
        CopyOffenseSingleMoveId,
        CopyOffenseTripleMoveId,
        CopyDefenseSingleMoveId,
        CopyDefenseStrengthMoveId
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private TimeTraceSnapshot _snapshot = TimeTraceSnapshot.Empty;
    private TimeTraceMarkedPlayerPower? _markedPower;
    private bool _spawnTurn = true;
    private bool _useBasePageThisTurn = true;
    private bool _isFakeDead;
    private string? _plannedMoveId;
    private int? _timeRecoilDamageRoll;
    private int? _timeTorrentDamageRoll;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 296, 200);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 300, 204);

    public override int DefaultChaoResistance => 100;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Immune,
        Pierce = LibraryResistanceLevel.Immune,
        Blunt = LibraryResistanceLevel.Immune
    };

    public override bool ShouldDisappearFromDoom => false;

    public bool IsFakeDead => _isFakeDead;

    public override IEnumerable<string> AssetPaths =>
        TimeTraceCreatureVisuals
            .Profile.AssetPaths
            .Concat(
                PriceOfSilenceCreatureVisuals
                    .Profile.AssetPaths)
            .Concat(PriceOfSilence.AdditionalAssetPaths)
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        _spawnTurn = true;
        _useBasePageThisTurn = true;
        _isFakeDead = false;
        _snapshot = TimeTraceSnapshot.Empty;
        _markedPower = null;
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<PriceOfSilenceYourTimePassivePower>(Creature, 1, Creature, null);
        await PowerCmdCompat.Apply<TimeTraceRestorationPower>(Creature, 1, Creature, null, silent: true);
        ForceRefreshMoveState();
        await RegiveTickingPowerForMove(ResolveCurrentMoveId());
        ForceRefreshMoveState();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById =[];
        _snapshot = TimeTraceSnapshot.Empty;
        _markedPower = null;
        _spawnTurn = true;
        _useBasePageThisTurn = true;
        _isFakeDead = false;
        _plannedMoveId = null;
        _timeRecoilDamageRoll = null;
        _timeTorrentDamageRoll = null;
    }

    public override void BeforeRemovedFromRoom()
    {
        _markedPower = null;
        base.BeforeRemovedFromRoom();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player && _isFakeDead)
        {
            ForceFakeDeathRestoreIntent();
        }
        else if (side == CombatSide.Player && Creature.IsAlive)
        {
            PrepareSnapshotForIntent();
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player && Creature.IsAlive && !_isFakeDead)
        {
            await CaptureSnapshot();
        }

        await base.AfterSideTurnStart(side, participants, combatState);
    }

    protected override async Task AfterSideTurnEndInternal(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && Creature.IsAlive)
        {
            _spawnTurn = false;
            PrepareSnapshotForIntent();
            if (_snapshot.Player is { Creature.IsAlive: false })
            {
                _snapshot = TimeTraceSnapshot.Empty;
            }

            string moveId = ResolvePlannedMoveId(RunRng.MonsterAi);
            await RegiveTickingPowerForMove(moveId);
        }

        await base.AfterSideTurnEndInternal(choiceContext, side, participants);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        return CreateMoveStateMachine(registerRuntimeStates: true);
    }

    private MonsterMoveStateMachine CreateMoveStateMachine(bool registerRuntimeStates)
    {
        if (registerRuntimeStates)
        {
            _statesById.Clear();
        }

        // 假死恢复属于复活行动：假死前已被打出混乱时，混乱锁不能拦下强制恢复，
        // 下一次行动必须恢复全部生命。
        MoveState restore = Register(new LibraryPhaseTransitionMoveState(
            TimeRestoreMoveId,
            TimeRestoreMove,
            new HealIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        }, registerRuntimeStates);

        MoveState recoil = Register(new MoveState(
            TimeRecoilMoveId,
            TimeRecoilMove,
            new CombinedAttackDefendIntent(
                () => GetTimeRecoilDamageRoll(),
                null,
                "TIME_TRACE_TIME_RECOIL.description",
                TimeRecoilBlock)), registerRuntimeStates);

        MoveState torrent = Register(new MoveState(
            TimeTorrentMoveId,
            TimeTorrentMove,
            new CombinedAttackDebuffIntent(
                () => GetTimeTorrentDamageRoll(),
                () => TimeTorrentHits,
                "TIME_TRACE_TIME_TORRENT.description",
                IntentBadge.Weak(TimeTorrentDebuffs),
                IntentBadge.Frail(TimeTorrentDebuffs),
                IntentBadge.Vulnerable(TimeTorrentDebuffs))), registerRuntimeStates);

        MoveState copyOffenseSingle = Register(new MoveState(
            CopyOffenseSingleMoveId,
            CopyOffenseSingleMove,
            new IndiscriminateAttackIntent(() => Math.Max(9, _snapshot.Damage),
            () => 1,
            "TIME_TRACE_COPY_OFFENSE_SINGLE.description")), registerRuntimeStates);

        MoveState copyOffenseTriple = Register(new MoveState(
            CopyOffenseTripleMoveId,
            CopyOffenseTripleMove,
            new IndiscriminateAttackIntent(
                () => Math.Max(9, _snapshot.Damage),
                () => CopyOffenseTripleHits,
                "TIME_TRACE_COPY_OFFENSE_TRIPLE.description")), registerRuntimeStates);

        MoveState copyDefenseSingle = Register(new MoveState(
            CopyDefenseSingleMoveId,
            CopyDefenseSingleMove,
            new TimeTraceCopyDefendIntent(
                () => Math.Max(15, _snapshot.Block),
                "TIME_TRACE_COPY_DEFENSE_SINGLE.description")), registerRuntimeStates);

        MoveState copyDefenseStrength = Register(new MoveState(
            CopyDefenseStrengthMoveId,
            CopyDefenseStrengthMove,
            new CombinedDefendBuffIntent(
                 Math.Max(15,_snapshot.Block / 2),
                "TIME_TRACE_COPY_DEFENSE_STRENGTH.description",
                IntentBadge.Strength(CopyDefenseStrength))), registerRuntimeStates);

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        recoil.FollowUpState = router;
        torrent.FollowUpState = router;
        copyOffenseSingle.FollowUpState = router;
        copyOffenseTriple.FollowUpState = router;
        copyDefenseSingle.FollowUpState = router;
        copyDefenseStrength.FollowUpState = router;
        restore.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [restore, recoil, torrent, copyOffenseSingle, copyOffenseTriple, copyDefenseSingle, copyDefenseStrength, router],
            router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        if (_isFakeDead)
        {
            _plannedMoveId = TimeRestoreMoveId;
            return TimeRestoreMoveId;
        }

        if (!string.IsNullOrEmpty(_plannedMoveId)
            && _statesById.ContainsKey(_plannedMoveId))
        {
            EnsureRollForMove(_plannedMoveId);
            return _plannedMoveId;
        }

        if (_useBasePageThisTurn || _spawnTurn || _snapshot.Player == null || _snapshot.UsedSilence)
        {
            _plannedMoveId = rng.NextInt(0, 2) == 0 ? TimeRecoilMoveId : TimeTorrentMoveId;
        }
        else
        {
            _plannedMoveId = rng.NextItem(CopyMoveIds) ?? CopyOffenseSingleMoveId;
        }

        EnsureRollForMove(_plannedMoveId);
        return _plannedMoveId;
    }

    public bool CanEnterFakeDeath(Creature creature) =>
        creature == Creature
        && !_isFakeDead
        && PriceOfSilenceEncounterHelper.FindBoss(Creature?.CombatState) != null;

    public async Task EnterFakeDeath()
    {
        if (_isFakeDead)
        {
            return;
        }

        _isFakeDead = true;
        ResetMoveStateForRevival();
        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        await ForceFakeDeathChaoZero();
        ForceFakeDeathRestoreIntent();
        await NotifyBossTraceDestroyed();
        await CreatureCmd.TriggerAnim(Creature, "StunTrigger", 0f);
    }

    public async Task RecoverFromFakeDeath()
    {
        if (!_isFakeDead || PriceOfSilenceEncounterHelper.FindBoss(Creature.CombatState) == null)
        {
            return;
        }

        _isFakeDead = false;
        await CreatureCmd.TriggerAnim(Creature, "WakeUpTrigger", 0f);
        await CreatureCmd.SetCurrentHp(Creature, Math.Max(MinimumRecoveryHp, Creature.MaxHp));
        if (Creature is LibraryCreature libraryCreature && libraryCreature.HasChaoResistance)
        {
            if (libraryCreature.IsChaoed)
            {
                libraryCreature.RestoreChaoOnNextOwnerTurn = false;
                libraryCreature.RestorePreStunResistance();
            }

            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, libraryCreature.MaxChaoValue);
            libraryCreature.HealthBar?.RefreshValues();
        }
    }

    private async Task TimeRecoilMove(IReadOnlyList<Creature> targets)
    {
        ClearPlannedMove(TimeRecoilMoveId);
        if (_isFakeDead)
        {
            return;
        }

        _useBasePageThisTurn = false;
        LocalOggOneShotPlayer.Play(PriceOfSilence.MassAttackSfxPath, -5f);
        IReadOnlyList<Creature> actualTargets = PriceOfSilenceEncounterHelper.LivingPlayers(Creature.CombatState);
        int damage = GetTimeRecoilDamageRoll();
        await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim("AttackBlunt", 0.36f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        _timeRecoilDamageRoll = null;

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.15f);
        await CreatureCmd.GainBlock(Creature, TimeRecoilBlock, ValueProp.Move, null);
    }

    private async Task TimeTorrentMove(IReadOnlyList<Creature> targets)
    {
        ClearPlannedMove(TimeTorrentMoveId);
        if (_isFakeDead)
        {
            return;
        }

        _useBasePageThisTurn = false;
        LocalOggOneShotPlayer.Play(PriceOfSilence.MassAttackSfxPath, -5f);
        IReadOnlyList<Creature> actualTargets = PriceOfSilenceEncounterHelper.LivingPlayers(Creature.CombatState);
        int damage = GetTimeTorrentDamageRoll();
        IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(await DamageCmd.Attack(damage)
        .FromMonster(this)
        .WithHitCount(TimeTorrentHits)
        .WithAttackerAnim("AttackSlash", 0.36f)
        .WithHitFx("vfx/vfx_attack_slash")
        .Execute(null));
        
        _timeTorrentDamageRoll = null;

        foreach (Creature hitTarget in results
            .Where(static result => result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<WeakPower>(hitTarget, TimeTorrentDebuffs, Creature, null);
            await PowerCmdCompat.ApplyDebuff<FrailPower>(hitTarget, TimeTorrentDebuffs, Creature, null);
            await PowerCmdCompat.ApplyDebuff<VulnerablePower>(hitTarget, TimeTorrentDebuffs, Creature, null);
        }
    }

    private async Task CopyOffenseSingleMove(IReadOnlyList<Creature> targets)
    {
        ClearPlannedMove(CopyOffenseSingleMoveId);
        if (_isFakeDead)
        {
            return;
        }

        await CopyAttack(_snapshot.Damage, 1, "AttackThrust", "vfx/vfx_attack_slash");
    }

    private async Task CopyOffenseTripleMove(IReadOnlyList<Creature> targets)
    {
        ClearPlannedMove(CopyOffenseTripleMoveId);
        if (_isFakeDead)
        {
            return;
        }

        await CopyAttack(_snapshot.Damage, CopyOffenseTripleHits, "AttackSlash", "vfx/vfx_attack_slash");
    }

    private async Task CopyDefenseSingleMove(IReadOnlyList<Creature> targets)
    {
        ClearPlannedMove(CopyDefenseSingleMoveId);
        if (_isFakeDead)
        {
            return;
        }

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
        await CreatureCmd.GainBlock(Creature, Math.Max(0, _snapshot.Block), ValueProp.Move, null);
    }

    private async Task CopyDefenseStrengthMove(IReadOnlyList<Creature> targets)
    {
        ClearPlannedMove(CopyDefenseStrengthMoveId);
        if (_isFakeDead)
        {
            return;
        }

        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
        await CreatureCmd.GainBlock(Creature, Math.Max(0, _snapshot.Block / 2), ValueProp.Move, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, CopyDefenseStrength, Creature, null);
    }

    private Task TimeRestoreMove(IReadOnlyList<Creature> targets)
    {
        ClearPlannedMove(TimeRestoreMoveId);
        return RecoverFromFakeDeath();
    }

    private async Task CopyAttack(int damage, int hits, string anim, string hitFx)
    {
        IReadOnlyList<Creature> actualTargets = PriceOfSilenceEncounterHelper.LivingPlayers(Creature.CombatState);
        if (actualTargets.Count == 0)
        {
            return;
        }

        await IndiscriminateAttackExecutor.Execute(
            this,
            Math.Max(0, damage),
            actualTargets,
            attack => attack
                .WithHitCount(hits)
                .WithAttackerAnim(anim, 0.36f)
                .WithHitFx(hitFx));
    }

    private async Task CaptureSnapshot()
    {
        IReadOnlyList<Creature> livingPlayers = PriceOfSilenceEncounterHelper.LivingPlayers(Creature.CombatState);
        Creature? selected = livingPlayers.Count > 0 ? RunRng.MonsterAi.NextItem(livingPlayers) : null;
        await ClearStaleMarkedPowers(selected: selected);

        Player? player = selected?.Player ?? selected?.PetOwner;
        if (selected == null || player == null)
        {
            _markedPower = null;
            return;
        }

        TimeTraceMarkedPlayerPower? power = selected.GetPower<TimeTraceMarkedPlayerPower>();
        power ??= await PowerCmdCompat.Apply<TimeTraceMarkedPlayerPower>(
            target: selected,
            amount: 1,
            applier: Creature,
            cardSource: null,
            silent: true);
        if (power == null)
        {
            _markedPower = null;
            return;
        }

        _markedPower = power;
        power.ResetCounters();
        PriceOfSilenceFilterOverlay.FlashFor(player);
    }

    private void PrepareSnapshotForIntent()
    {
        TimeTraceMarkedPlayerPower? power = _markedPower;
        Creature? owner = power?.Owner;
        if (power != null && owner?.CombatState == Creature.CombatState)
        {
            _snapshot = power.FreezeForIntent();
            return;
        }

        _snapshot = TimeTraceSnapshot.Empty;
    }

    private async Task ClearStaleMarkedPowers(Creature? selected)
    {
        foreach (TimeTraceMarkedPlayerPower power in Creature.CombatState?.PlayerCreatures
            .Select(static creature => creature.GetPower<TimeTraceMarkedPlayerPower>())
            .OfType<TimeTraceMarkedPlayerPower>()
            .ToArray()
            ?? [])
        {
            if (power.Owner != selected)
            {
                await PowerCmd.Remove(power);
            }
        }
    }

    private async Task RegiveTickingPowerForMove(string? moveId)
    {
        PowerModel? attack = Creature.GetPower<TickingAttackPower>();
        if (attack != null)
        {
            await PowerCmd.Remove(attack);
        }

        PowerModel? guard = Creature.GetPower<TickingGuardPower>();
        if (guard != null)
        {
            await PowerCmd.Remove(guard);
        }

        switch (GetTickingPowerKind(moveId))
        {
            case TimeTraceTickingPowerKind.Attack:
                await PowerCmdCompat.Apply<TickingAttackPower>(Creature, 1, Creature, null, silent: false);
                break;
            case TimeTraceTickingPowerKind.Guard:
                await PowerCmdCompat.Apply<TickingGuardPower>(Creature, 1, Creature, null, silent: false);
                break;
        }
    }

    private string? ResolveCurrentMoveId() =>
        NextMove.Id == "UNSET_MOVE" ? _plannedMoveId : NextMove.Id;

    private static TimeTraceTickingPowerKind GetTickingPowerKind(string? moveId)
    {
        return moveId switch
        {
            CopyDefenseSingleMoveId or CopyDefenseStrengthMoveId => TimeTraceTickingPowerKind.Guard,
            TimeRecoilMoveId or TimeTorrentMoveId or CopyOffenseSingleMoveId or CopyOffenseTripleMoveId =>
                TimeTraceTickingPowerKind.Attack,
            _ => TimeTraceTickingPowerKind.None
        };
    }

    private void ClearPlannedMove(string moveId)
    {
        if (string.Equals(_plannedMoveId, moveId, StringComparison.Ordinal))
        {
            _plannedMoveId = null;
        }
    }

    private void ResetMoveStateForRevival()
    {
        _spawnTurn = true;
        _useBasePageThisTurn = true;
        _plannedMoveId = null;
        _timeRecoilDamageRoll = null;
        _timeTorrentDamageRoll = null;
        ResetStateMachine();
        SetUpForCombat();
    }

    private int GetTimeRecoilDamageRoll() =>
        GetOrRollDamage(ref _timeRecoilDamageRoll, TimeRecoilMinDamage, TimeRecoilMaxDamage);

    private int GetTimeTorrentDamageRoll() =>
        GetOrRollDamage(ref _timeTorrentDamageRoll, TimeTorrentMinDamage, TimeTorrentMaxDamage);

    private int GetOrRollDamage(ref int? cachedRoll, int minInclusive, int maxInclusive)
    {
        if (!IsMutable)
        {
            return maxInclusive;
        }

        return cachedRoll ?? maxInclusive;
    }

    private int EnsureDamageRoll(ref int? cachedRoll, int minInclusive, int maxInclusive)
    {
        if (!IsMutable)
        {
            return maxInclusive;
        }

        cachedRoll ??= RunRng.MonsterAi.NextInt(minInclusive, maxInclusive + 1);
        return cachedRoll.Value;
    }

    private void EnsureRollForMove(string moveId)
    {
        switch (moveId)
        {
            case TimeRecoilMoveId:
                EnsureDamageRoll(ref _timeRecoilDamageRoll, TimeRecoilMinDamage, TimeRecoilMaxDamage);
                break;
            case TimeTorrentMoveId:
                EnsureDamageRoll(ref _timeTorrentDamageRoll, TimeTorrentMinDamage, TimeTorrentMaxDamage);
                break;
        }
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
        }
    }

    private void ForceFakeDeathRestoreIntent()
    {
        if (_statesById.TryGetValue(TimeRestoreMoveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
        }
    }

    private async Task ForceFakeDeathChaoZero()
    {
        if (Creature is not LibraryCreature libraryCreature || !libraryCreature.HasChaoResistance)
        {
            return;
        }

        if (libraryCreature.CurrentChaoValue != 0)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
        }

        libraryCreature.HealthBar?.RefreshValues();
    }

    private async Task NotifyBossTraceDestroyed()
    {
        PriceOfSilence? boss = Creature.CombatState?.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<PriceOfSilence>()
            .FirstOrDefault(static candidate => candidate.Creature.IsAlive);
        if (boss != null)
        {
            await boss.NotifyTraceDestroyed();
        }
    }

    private MoveState Register(MoveState state, bool registerRuntimeStates)
    {
        if (registerRuntimeStates)
        {
            _statesById[state.Id] = state;
        }

        return state;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = CreateMoveStateMachine(registerRuntimeStates: false);
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

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        return Task.CompletedTask;
    }

    private enum TimeTraceTickingPowerKind
    {
        None,
        Attack,
        Guard
    }
}

internal sealed class TimeTraceCopyDefendIntent(
    Func<int> blockCalc,
    string descriptionKey) : DefendIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        int block = Math.Max(0, blockCalc());
        var desc = new LocString("intents", descriptionKey);
        desc.Add("Amount", block);
        desc.Add("BlockAmount", block);
        desc.Add("IsMultiplayer", owner.CombatState?.RunState.Players.Count > 1);
        return desc;
    }
}
