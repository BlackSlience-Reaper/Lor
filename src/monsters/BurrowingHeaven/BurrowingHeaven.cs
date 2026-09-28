using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.BurrowingHeaven;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.BurrowingHeaven;
using LibraryOfRuina.powers;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.BurrowingHeaven;
using LibraryOfRuina.visuals.BurrowingHeaven;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.BurrowingHeaven;

public sealed class BurrowingHeaven : CounterIntentMonsterModel
{
    private const string SleepMoveId = "SLEEP";
    private const string DeepGazeMoveId = "DEEP_GAZE";
    private const string WitheringWingsMoveId = "WITHERING_WINGS";
    private const string BloodyWingsMoveId = "BLOODY_WINGS";
    private const string ExclusiveHeavenMoveId = "EXCLUSIVE_HEAVEN";
    private const string RouterStateId = "BURROWING_HEAVEN_ROUTER";

    private const int DeepGazeMinDamage = 16;
    private const int DeepGazeMaxDamage = 17;
    private const int DeepGazeBleed = 4;
    private const int DeepGazeBlock = 10;
    private const int WitheringWingsMinDamage = 8;
    private const int WitheringWingsMaxDamage = 9;
    private const int WitheringWingsBleed = 3;
    private const int WitheringWingsBlock = 20;
    private const int BloodyWingsMinDamage = 17;
    private const int BloodyWingsMaxDamage = 20;
    private const int BloodyWingsBleed = 6;
    private const int ExclusiveHeavenFirstMinDamage = 14;
    private const int ExclusiveHeavenFirstMaxDamage = 19;
    private const int ExclusiveHeavenVulnerable = 3;
    private const int ExclusiveHeavenVulnerableTurns = 1;
    private const int ExclusiveHeavenSecondMinDamage = 13;
    private const int ExclusiveHeavenSecondMaxDamage = 16;
    private const int ExclusiveHeavenHealPercent = 5;
    private const int OldGodWeakStacks = 10;
    private const int OldGodWeakTurns = 1;

    internal const string TextureRoot = "res://images/monsters/burrowing_heaven/";
    internal const string AwakeTexturePath = TextureRoot + "idle_awake.png";
    internal const string SleepTexturePath = TextureRoot + "idle_sleep.png";
    internal const string AttackTexturePath = TextureRoot + "attack.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string SpecialTexturePath = TextureRoot + "special.png";
    internal const string GuardTexturePath = TextureRoot + "guard.png";
    internal const string SfxRoot = "res://audio/sfx/burrowing_heaven/";
    internal const string AttackSfxPath = SfxRoot + "attack.ogg";
    internal const string AwakeSfxPath = SfxRoot + "awake_strong.ogg";
    internal const string SpecialFirstSfxPath = SfxRoot + "special_attack_1.ogg";
    internal const string SpecialSecondSfxPath = SfxRoot + "special_attack_2.ogg";
    public static readonly string[] ScreamSfxPaths =
    [
        SfxRoot + "gaze_scream_1.ogg",
        SfxRoot + "gaze_scream_2.ogg"
    ];

    private static readonly string[] AdditionalAssetPaths =
    [
        AttackSfxPath,
        AwakeSfxPath,
        SpecialFirstSfxPath,
        SpecialSecondSfxPath,
        ..ScreamSfxPaths,
        "res://images/powers/burrowing_heaven_perfect_focus_passive_power.png",
        "res://images/powers/burrowing_heaven_wings_toward_old_god_passive_power.png",
        "res://images/powers/burrowing_heaven_in_cognition_passive_power.png",
        "res://images/powers/burrowing_heaven_sleep_power.png"
    ];

    private static readonly string PageRelicTitleLocKey = $"{ModelDb.GetId<BurrowingHeavenPageRelic>().Entry}.title";

    private Dictionary<string, MoveState> _statesById = [];
    private bool _isAwake;
    private bool _forceExclusiveHeaven;
    private int _awakeRoundCounter;
    private bool _generateThornAtNextTurnStart;
    private string? _lastNormalMoveId;
    private int? _deepGazeDamageRoll;
    private int? _witheringWingsDamageRoll;
    private int? _bloodyWingsDamageRoll;
    private int? _exclusiveFirstDamageRoll;
    private int? _exclusiveSecondDamageRoll;
    private MoveState? _sleepState;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
        _sleepState = null;
    }

    public bool IsAwake => _isAwake;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 306, 252);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 310, 255);

    public override int DefaultChaoResistance => 120;

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
        BurrowingHeavenCreatureVisuals
            .Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public void SetInitialAwake(bool awake)
    {
        _isAwake = awake;
        if (!awake)
        {
            ResetCognitionCounter();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<BurrowingHeavenPerfectFocusPassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<BurrowingHeavenWingsTowardOldGodPassivePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<BurrowingHeavenInCognitionPassivePower>(Creature, 1, Creature, null, silent: true);
        ForceRefreshMoveState();
    }

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(new ThrowingPlayerChoiceContext(), Creature.CombatState);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            await BurrowingHeavenEncounterHelper.RefreshEncounterState(choiceContext, combatState);
            if (_isAwake && !Creature.IsStunned && Creature.IsAlive)
            {
                foreach (Creature player in BurrowingHeavenEncounterHelper.LivingPlayers(combatState))
                {
                    await LibraryPowerCmd.Apply<LibraryWeakPower>(
                        new ThrowingPlayerChoiceContext(),
                        player,
                        OldGodWeakStacks,
                        OldGodWeakTurns - 1,
                        IsPermanent: false,
                        Creature,
                        null);
                }

                if (_generateThornAtNextTurnStart)
                {
                    ResetCognitionCounter();
                    await BurrowingHeavenEncounterHelper.SpawnOrWakeAwakeThorn(choiceContext, combatState);
                    await BurrowingHeavenEncounterHelper.RefreshEncounterState(choiceContext, combatState);
                }
                else
                {
                    _awakeRoundCounter++;
                    if (_awakeRoundCounter >= BurrowingHeavenEncounterHelper.SleepTurns)
                    {
                        _generateThornAtNextTurnStart = true;
                    }
                }
            }
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    protected override bool ShouldQueueCounterIntentsForCurrentMove()
    {
        return _isAwake && base.ShouldQueueCounterIntentsForCurrentMove();
    }

    public async Task SetAwake(PlayerChoiceContext choiceContext, bool awake, bool forceExclusiveHeaven)
    {
        bool changed = _isAwake != awake;
        _isAwake = awake;
        if (awake)
        {
            if (forceExclusiveHeaven)
            {
                _forceExclusiveHeaven = true;
            }

            BurrowingHeavenSleepPower? sleepPower = Creature.GetPower<BurrowingHeavenSleepPower>();
            if (changed)
            {
                sleepPower?.SetAmount(BurrowingHeavenEncounterHelper.SleepTurns, silent: true);
            }

            await RemoveSleepUntargetable();

            if (changed)
            {
                LocalOggOneShotPlayer.Play(AwakeSfxPath, -2f);
                await CreatureCmd.TriggerAnim(Creature, "Awake", 0f);
            }
        }
        else
        {
            _forceExclusiveHeaven = false;
            ResetCognitionCounter();
            ClearCounterIntentQueueAndRefresh(forceRefresh: true);
            await ApplySleepUntargetable();
            if (changed)
            {
                await CreatureCmd.TriggerAnim(Creature, "Sleep", 0f);
            }
        }

        if (changed || forceExclusiveHeaven)
        {
            ForceRefreshMoveState();
        }
    }

    private void ResetCognitionCounter()
    {
        _awakeRoundCounter = 0;
        _generateThornAtNextTurnStart = false;
    }

    private async Task ApplySleepUntargetable()
    {
        await PowerCmdCompat.Ensure<UntargetablePower>(Creature);
    }

    private async Task RemoveSleepUntargetable()
    {
        PowerModel? untargetablePower = Creature.GetPower<UntargetablePower>();
        if (untargetablePower != null)
        {
            await PowerCmd.Remove(untargetablePower);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        _sleepState = Register(new MoveState(SleepMoveId, _ => Task.CompletedTask, new HiddenIntent()));
        MoveState deepGaze = Register(new MoveState(
            DeepGazeMoveId,
            DeepGazeMove,
            new CombinedCounterAttackDefendIntent(
                () => GetDeepGazeDamageRoll(),
                () => 1,
                "BURROWING_HEAVEN_DEEP_GAZE.description",
                (choiceContext, _, target) => PerformCounterAttack(
                    choiceContext,
                    target,
                    GetDeepGazeDamageRoll(),
                    hitCount: 1,
                    bleed: DeepGazeBleed,
                    blockAfter: DeepGazeBlock),
                DeepGazeBlock,
                IntentBadge.Bleed(DeepGazeBleed))));
        MoveState witheringWings = Register(new MoveState(
            WitheringWingsMoveId,
            WitheringWingsMove,
            new CombinedCounterAttackDebuffIntent(
                () => GetWitheringWingsDamageRoll(),
                () => 1,
                "BURROWING_HEAVEN_WITHERING_WINGS.description",
                (choiceContext, _, target) => PerformCounterAttack(
                    choiceContext,
                    target,
                    GetWitheringWingsDamageRoll(),
                    hitCount: 1,
                    bleed: WitheringWingsBleed),
                IntentBadge.Bleed(WitheringWingsBleed)),
            new CombinedCounterAttackDebuffIntent(
                () => GetWitheringWingsDamageRoll(),
                () => 1,
                "BURROWING_HEAVEN_WITHERING_WINGS.description",
                (choiceContext, _, target) => PerformCounterAttack(
                    choiceContext,
                    target,
                    GetWitheringWingsDamageRoll(),
                    hitCount: 1,
                    bleed: WitheringWingsBleed),
                IntentBadge.Bleed(WitheringWingsBleed)),
            new CounterDefendIntent(WitheringWingsBlock, (_, owner) => GainCounterBlock(owner, WitheringWingsBlock))));
        MoveState bloodyWings = Register(new MoveState(
            BloodyWingsMoveId,
            BloodyWingsMove,
            new CombinedCounterAttackDebuffIntent(
                () => GetBloodyWingsDamageRoll(),
                () => 1,
                "BURROWING_HEAVEN_BLOODY_WINGS.description",
                (choiceContext, _, target) => PerformCounterAttack(
                    choiceContext,
                    target,
                    GetBloodyWingsDamageRoll(),
                    hitCount: 1,
                    bleed: BloodyWingsBleed),
                IntentBadge.Bleed(BloodyWingsBleed))));
        MoveState exclusiveHeaven = Register(new MoveState(
            ExclusiveHeavenMoveId,
            ExclusiveHeavenMove,
            new IndiscriminateAttackIntent(
                () => GetExclusiveFirstDamageRoll(),
                null,
                "BURROWING_HEAVEN_EXCLUSIVE_HEAVEN_FIRST.description",
                IntentBadge.RapidWear(ExclusiveHeavenVulnerable, ExclusiveHeavenVulnerableTurns)),
            new IndiscriminateAttackIntent(
                () => GetExclusiveSecondDamageRoll(),
                null,
                "BURROWING_HEAVEN_EXCLUSIVE_HEAVEN_SECOND.description",
                IntentBadge.Heal(() => ExclusiveHeavenHealPercent))));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        _sleepState.FollowUpState = router;
        deepGaze.FollowUpState = router;
        witheringWings.FollowUpState = router;
        bloodyWings.FollowUpState = router;
        exclusiveHeaven.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [_sleepState, deepGaze, witheringWings, bloodyWings, exclusiveHeaven, router],
            router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        if (!_isAwake)
        {
            return SleepMoveId;
        }

        if (_forceExclusiveHeaven)
        {
            EnsureExclusiveFirstDamageRoll();
            EnsureExclusiveSecondDamageRoll();
            return ExclusiveHeavenMoveId;
        }

        string[] candidates =
        [
            DeepGazeMoveId,
            WitheringWingsMoveId,
            BloodyWingsMoveId
        ];
        string[] filtered = candidates
            .Where(moveId => moveId != _lastNormalMoveId)
            .ToArray();
        string selected = rng.NextItem(filtered.Length > 0 ? filtered : candidates) ?? candidates[0];
        EnsureRollForMove(selected);
        return selected;
    }

    private async Task DeepGazeMove(IReadOnlyList<Creature> targets)
    {
        _lastNormalMoveId = DeepGazeMoveId;
        _deepGazeDamageRoll = null;
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(new ThrowingPlayerChoiceContext(), Creature.CombatState);
    }

    private async Task WitheringWingsMove(IReadOnlyList<Creature> targets)
    {
        _lastNormalMoveId = WitheringWingsMoveId;
        _witheringWingsDamageRoll = null;
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(new ThrowingPlayerChoiceContext(), Creature.CombatState);
    }

    private async Task BloodyWingsMove(IReadOnlyList<Creature> targets)
    {
        _lastNormalMoveId = BloodyWingsMoveId;
        _bloodyWingsDamageRoll = null;
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(new ThrowingPlayerChoiceContext(), Creature.CombatState);
    }

    private async Task ExclusiveHeavenMove(IReadOnlyList<Creature> targets)
    {
        _forceExclusiveHeaven = false;
        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        IReadOnlyList<Creature> actualTargets = BurrowingHeavenEncounterHelper.LivingPlayers(Creature.CombatState);
        if (actualTargets.Count == 0)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(SpecialFirstSfxPath, -2f);
        int firstDamage = GetExclusiveFirstDamageRoll();
        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, firstDamage, actualTargets);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets))
        {
            AttackCommand attack = await DamageCmd.Attack(firstDamage)
                .FromMonster(this)
                .WithAttackerAnim("Special", 0.45f)
                .WithHitFx("vfx/vfx_attack_slash")
                .WithIndiscriminateBlockBreak(this, firstDamage, actualTargets)
                .Execute(null);

            foreach (Creature target in AttackCommandCompat.Results(attack)
                .Where(static result => result.UnblockedDamage > 0)
                .Select(static result => result.Receiver)
                .Distinct())
            {
                await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                    target,
                    ExclusiveHeavenVulnerable,
                    ExclusiveHeavenVulnerableTurns,
                    Creature,
                    null);
            }
        }

        LocalOggOneShotPlayer.Play(SpecialSecondSfxPath, -2f);
        int secondDamage = GetExclusiveSecondDamageRoll();
        actualTargets = BurrowingHeavenEncounterHelper.LivingPlayers(Creature.CombatState);
        if (actualTargets.Count > 0)
        {
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, secondDamage, actualTargets);
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets))
            {
                await DamageCmd.Attack(secondDamage)
                    .FromMonster(this)
                    .WithAttackerAnim("Special", 0.45f)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .WithIndiscriminateBlockBreak(this, secondDamage, actualTargets)
                    .Execute(null);
            }
        }

        decimal missingHp = Creature.MaxHp - Creature.CurrentHp;
        if (missingHp > 0m)
        {
            await CreatureCmd.Heal(
                Creature,
                Math.Min(missingHp, Math.Ceiling(Creature.MaxHp * ExclusiveHeavenHealPercent / 100m)));
        }

        _exclusiveFirstDamageRoll = null;
        _exclusiveSecondDamageRoll = null;
        await BurrowingHeavenEncounterHelper.RefreshEncounterState(choiceContext, Creature.CombatState);
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

        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        IReadOnlyList<DamageResult> results;
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, [target]))
        {
            BurrowingHeavenEncounterHelper.TryPlayCounterHitVfx(
                target,
                "vfx/vfx_attack_slash",
                $"{nameof(BurrowingHeaven)}.{nameof(PerformCounterAttack)}");
            AttackCommand attack = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithHitCount(hitCount)
                .OnlyPlayAnimOnce()
                .WithAttackerAnim("Attack", 0.35f)
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

    private static async Task GainCounterBlock(Creature owner, int block)
    {
        await CreatureCmd.TriggerAnim(owner, "Guard", 0f);
        await CreatureCmd.GainBlock(owner, block, ValueProp.Move, null);
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

    private int GetDeepGazeDamageRoll() =>
        GetOrRollDamage(ref _deepGazeDamageRoll, DeepGazeMinDamage, DeepGazeMaxDamage);

    private int GetWitheringWingsDamageRoll() =>
        GetOrRollDamage(ref _witheringWingsDamageRoll, WitheringWingsMinDamage, WitheringWingsMaxDamage);

    private int GetBloodyWingsDamageRoll() =>
        GetOrRollDamage(ref _bloodyWingsDamageRoll, BloodyWingsMinDamage, BloodyWingsMaxDamage);

    private int GetExclusiveFirstDamageRoll() =>
        GetOrRollDamage(ref _exclusiveFirstDamageRoll, ExclusiveHeavenFirstMinDamage, ExclusiveHeavenFirstMaxDamage);

    private int GetExclusiveSecondDamageRoll() =>
        GetOrRollDamage(ref _exclusiveSecondDamageRoll, ExclusiveHeavenSecondMinDamage, ExclusiveHeavenSecondMaxDamage);

    private void EnsureRollForMove(string moveId)
    {
        switch (moveId)
        {
            case DeepGazeMoveId:
                EnsureDamageRoll(ref _deepGazeDamageRoll, DeepGazeMinDamage, DeepGazeMaxDamage);
                break;
            case WitheringWingsMoveId:
                EnsureDamageRoll(ref _witheringWingsDamageRoll, WitheringWingsMinDamage, WitheringWingsMaxDamage);
                break;
            case BloodyWingsMoveId:
                EnsureDamageRoll(ref _bloodyWingsDamageRoll, BloodyWingsMinDamage, BloodyWingsMaxDamage);
                break;
        }
    }

    private int EnsureExclusiveFirstDamageRoll() =>
        EnsureDamageRoll(ref _exclusiveFirstDamageRoll, ExclusiveHeavenFirstMinDamage, ExclusiveHeavenFirstMaxDamage);

    private int EnsureExclusiveSecondDamageRoll() =>
        EnsureDamageRoll(ref _exclusiveSecondDamageRoll, ExclusiveHeavenSecondMinDamage, ExclusiveHeavenSecondMaxDamage);

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

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !IsBurrowingHeavenEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<BurrowingHeavenPageRelic>(room, PageRelicTitleLocKey);
    }

    private static bool IsBurrowingHeavenEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is BurrowingHeaven);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            AddPageRewardsFromDeathHook(creature);
        }

        return Task.CompletedTask;
    }

    

}
