using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.RoadHome;

public sealed class RoadHome : LorMonsterModel
{
    internal const string HideAndSeekMoveId = "ROAD_HOME_HIDE_AND_SEEK";
    internal const string PatternOneMoveId = "ROAD_HOME_PATTERN_ONE";
    internal const string PatternTwoMoveId = "ROAD_HOME_PATTERN_TWO";
    internal const string PatternThreeMoveId = "ROAD_HOME_PATTERN_THREE";
    private const string RouterStateId = "ROAD_HOME_ROUTER";
    private const int PatternOneHomeIntentCount = 2;
    private const int PatternTwoHomeIntentCount = 3;

    public const int FriendFullyInterruptedTurnsForModeThree = 2;
    public const int TotalInterruptsForBadWizard = 3;
    public const int HideAndSeekBlock = 24;
    public const int HideAndSeekFrail = 2;
    public const int WalkTogetherLowDamage = 22;
    public const int WalkTogetherHighDamage = 29;
    public const int FriendHomeLowDamage = 15;
    public const int FriendHomeHighDamage = 19;
    public const int FriendHomeConfusion = 1;
    public const int BadWizardGroupLowDamage = 24;
    public const int BadWizardGroupHighDamage = 33;

    public const string IdleTexturePath = RoadHomeEncounterHelper.TextureRoot + "idle.png";
    public const string AttackTexturePath = RoadHomeEncounterHelper.TextureRoot + "attack.png";
    public const string Attack2TexturePath = RoadHomeEncounterHelper.TextureRoot + "attack_2.png";
    public const string Attack3TexturePath = RoadHomeEncounterHelper.TextureRoot + "attack_3.png";
    public const string HitTexturePath = RoadHomeEncounterHelper.TextureRoot + "hit.png";
    public const string DodgeTexturePath = RoadHomeEncounterHelper.TextureRoot + "dodge.png";
    public const string ConfusedTexturePath = RoadHomeEncounterHelper.TextureRoot + "confused.png";
    public const string AttackSfxPath = RoadHomeEncounterHelper.SfxRoot + "lion_attack.ogg";
    public const string HouseAttackSfxPath = RoadHomeEncounterHelper.SfxRoot + "house_attack.ogg";
    public const string HouseExplosionSfxPath = RoadHomeEncounterHelper.SfxRoot + "house_explosion.ogg";
    public const string NormalAttackSfxPath = RoadHomeEncounterHelper.SfxRoot + "normal_attack.ogg";
    public const string YellowBrickRoadSfxPath = RoadHomeEncounterHelper.SfxRoot + "yellow_brick_road.ogg";

    public static readonly string[] StaticAssetPaths =
        RoadHomeCreatureVisuals
            .Profile.AssetPaths.ToArray();

    private Dictionary<string, MoveState> _statesById = [];
    private int _normalPatternCursor;
    private int _pendingHomeInterruptsThisTurn;
    private int _fullyInterruptedFriendTurns;
    private bool _modeThreePending;

    private bool _pendingModeThreeConfusion;

    public RoadHomeActionPattern CurrentActionPattern { get; private set; } = RoadHomeActionPattern.ModeOne;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 573, 458);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 575, 461);

    public override int DefaultChaoResistance => 300;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => NormalResistance();

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => NormalResistance();

    public override IEnumerable<string> AssetPaths =>
        StaticAssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _normalPatternCursor = 0;
        _pendingHomeInterruptsThisTurn = 0;
        _fullyInterruptedFriendTurns = 0;
        _modeThreePending = false;
        CurrentActionPattern = RoadHomeActionPattern.ModeOne;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<RoadHomeFriendPassivePower>(Creature, 1, Creature, null, silent: true);
        ForceRefreshMoveState();
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!_pendingModeThreeConfusion) return;
        SetMoveImmediate(NextMove, true);
        await LibraryCreatureCmd.SetCurrentChaoValue(Creature as LibraryCreature ?? throw new InvalidOperationException(), 0);
        _pendingModeThreeConfusion = false;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (cardSource == null || target != Creature || result.UnblockedDamage <= 0 || cardSource.Type != CardType.Attack)
        {
            return;
        }

        await RoadHomeEncounterHelper.OnRoadHomeTookDamage(Creature, result.UnblockedDamage);
        _pendingHomeInterruptsThisTurn++;
        ForceRefreshMoveState();
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
        {
            await node.RefreshIntents();
        }
    }

  
    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            return RoadHomeEncounterHelper.OnRoadHomeDefeated(choiceContext, creature);
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        IReadOnlyList<MoveState> patternOneStates = RegisterInterruptVariants(
            PatternOneMoveId,
            PatternOneMove,
            [
                new CombinedDefendDebuffIntent(
                    HideAndSeekBlock,
                    "ROAD_HOME_HIDE_AND_SEEK.description",
                    HideAndSeekFrail)
            ],
            [
                CreateWalkTogetherIntent(),
                CreateWalkTogetherIntent()
            ]);

        IReadOnlyList<MoveState> patternTwoStates = RegisterInterruptVariants(
            PatternTwoMoveId,
            PatternTwoMove,
            [
                new CombinedAttackDebuffIntent(
                    () => GetFriendHomeDamage(),
                    null,
                    "ROAD_HOME_FRIEND_HOME.description",
                    IntentBadge.Confusion(FriendHomeConfusion))
            ],
            [
                CreateWalkTogetherIntent(),
                CreateWalkTogetherIntent(),
                CreateWalkTogetherIntent()
            ]);

        IReadOnlyList<MoveState> patternThreeStates = RegisterInterruptVariants(
            PatternThreeMoveId,
            PatternThreeMove,
            [
                new CombinedDefendDebuffIntent(
                    HideAndSeekBlock,
                    "ROAD_HOME_HIDE_AND_SEEK.description",
                    HideAndSeekFrail),
                new CombinedAttackDebuffIntent(
                    () => GetFriendHomeDamage(),
                    null,
                    "ROAD_HOME_FRIEND_HOME.description",
                    IntentBadge.Confusion(FriendHomeConfusion)),
                new IndiscriminateAttackIntent(
                    () => GetBadWizardGroupDamage(),
                    null,
                    "ROAD_HOME_BAD_WIZARD_GROUP.description")
            ],
            []);

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        foreach (MoveState state in patternOneStates.Concat(patternTwoStates).Concat(patternThreeStates))
        {
            state.FollowUpState = router;
        }

        List<MonsterState> states = [];
        states.AddRange(patternOneStates);
        states.AddRange(patternTwoStates);
        states.AddRange(patternThreeStates);
        states.Add(router);
        return new MonsterMoveStateMachine(states, router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        if (_modeThreePending)
        {
            CurrentActionPattern = RoadHomeActionPattern.ModeThree;
            return PatternThreeMoveId;
        }

        if (_normalPatternCursor == 0)
        {
            CurrentActionPattern = RoadHomeActionPattern.ModeOne;
            return GetInterruptedMoveId(PatternOneMoveId, PatternOneHomeIntentCount);
        }

        CurrentActionPattern = RoadHomeActionPattern.ModeTwo;
        return GetInterruptedMoveId(PatternTwoMoveId, PatternTwoHomeIntentCount);
    }

    private IReadOnlyList<MoveState> RegisterInterruptVariants(
        string baseMoveId,
        Func<IReadOnlyList<Creature>, Task> onPerform,
        IReadOnlyList<AbstractIntent> fixedIntents,
        IReadOnlyList<AbstractIntent> homeIntents)
    {
        List<MoveState> states = [];
        for (int interrupted = 0; interrupted <= homeIntents.Count; interrupted++)
        {
            AbstractIntent[] visibleIntents =
            [
                ..fixedIntents,
                ..homeIntents.Take(homeIntents.Count - interrupted)
            ];
            states.Add(Register(new MoveState(
                GetInterruptVariantMoveId(baseMoveId, interrupted),
                onPerform,
                visibleIntents)));
        }

        return states;
    }

    private string GetInterruptedMoveId(string baseMoveId, int homeIntentCount) =>
        GetInterruptVariantMoveId(
            baseMoveId,
            Math.Clamp(_pendingHomeInterruptsThisTurn, 0, homeIntentCount));

    private static string GetInterruptVariantMoveId(string baseMoveId, int interrupted) =>
        interrupted <= 0 ? baseMoveId : $"{baseMoveId}_INTERRUPTED_{interrupted}";

    private RoadHomeHouseAttackIntent CreateWalkTogetherIntent() =>
        new(
            GetWalkTogetherDamage,
            () => 1,
            "ROAD_HOME_WALK_TOGETHER.description");

    private async Task PatternOneMove(IReadOnlyList<Creature> targets)
    {
        await HideAndSeek(targets);
        bool firstInterrupted = await TryResolveHouseAttack(WalkTogetherAttack);
        bool secondInterrupted =await TryResolveHouseAttack(WalkTogetherAttack);
        if (firstInterrupted && secondInterrupted )
        {
            RegisterFullyInterruptedFriendTurn();
        }
        FinishPattern(RoadHomeActionPattern.ModeOne);
    }

    private async Task PatternTwoMove(IReadOnlyList<Creature> targets)
    {
        await FriendHomeAttack();
        bool firstInterrupted = await TryResolveHouseAttack(WalkTogetherAttack);
        bool secondInterrupted = await TryResolveHouseAttack(WalkTogetherAttack);
        bool thirdInterrupted = await TryResolveHouseAttack(WalkTogetherAttack);
        if (firstInterrupted && secondInterrupted && thirdInterrupted)
        {
            RegisterFullyInterruptedFriendTurn();
        }

        FinishPattern(RoadHomeActionPattern.ModeTwo);
    }

    private async Task PatternThreeMove(IReadOnlyList<Creature> targets)
    {
        await HideAndSeek(targets);
        await FriendHomeAttack();
        await BadWizardGroupAttack();
        FinishPattern(RoadHomeActionPattern.ModeThree);
    }

    private async Task BadWizardGroupAttack()
    {
        LocalOggOneShotPlayer.Play(HouseExplosionSfxPath, -1f);
        int damage = GetBadWizardGroupDamage();
        IReadOnlyList<Creature> targetList = GetIndiscriminateTargets();
        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, damage, targetList);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, targetList))
        {
            await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim("BadWizard", 0.36f)
                .WithHitFx("vfx/vfx_attack_blunt")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, damage, targetList)
                .Execute(null);
        }
    }

    private async Task HideAndSeek(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(YellowBrickRoadSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Hide", 0.2f);
        await CreatureCmd.GainBlock(Creature, HideAndSeekBlock, ValueProp.Move, null);
        IReadOnlyList<Creature> livingPlayers = targets
            .Where(static target => target is { IsAlive: true, IsPlayer: true })
            .ToArray();
        if (livingPlayers.Count > 0)
        {
            await PowerCmdCompat.ApplyDebuff<FrailPower>(livingPlayers, HideAndSeekFrail, Creature, null);
        }
    }

    private async Task FriendHomeAttack()
    {
        LocalOggOneShotPlayer.Play(HouseAttackSfxPath, -2f);
        IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(
            await DamageCmd.Attack(GetFriendHomeDamage())
                .FromMonster(this)
                .WithAttackerAnim("Attack2", 0.32f)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null));
        foreach (Creature receiver in results
            .Select(static result => result.Receiver)
            .Where(static creature => creature.IsPlayer)
            .Distinct())
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
                receiver,
                FriendHomeConfusion,
                Creature,
                null);
        }
    }

    private async Task<bool> TryResolveHouseAttack(Func<Task> attack)
    {
        if (_pendingHomeInterruptsThisTurn > 0)
        {
            _pendingHomeInterruptsThisTurn--;
            return true;
        }

        await attack();
        return false;
    }

    private async Task WalkTogetherAttack()
    {
        Creature? house = GetHouseTarget();
        if (house == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(NormalAttackSfxPath, -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, [house]))
        {
            await DamageCmd.Attack(GetWalkTogetherDamage())
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.32f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
    }

    private void RegisterFullyInterruptedFriendTurn()
    {
        _fullyInterruptedFriendTurns++;
        if (_fullyInterruptedFriendTurns >= FriendFullyInterruptedTurnsForModeThree)
        {
            _fullyInterruptedFriendTurns = 0;
            _modeThreePending = true;
        }
    }

    private void FinishPattern(RoadHomeActionPattern completedPattern)
    {
        _pendingHomeInterruptsThisTurn = 0;
        if (completedPattern == RoadHomeActionPattern.ModeThree)
        {
            _modeThreePending = false;
            _pendingModeThreeConfusion = true;
        }

        if (completedPattern == RoadHomeActionPattern.ModeOne)
        {
            _normalPatternCursor = 1;
        }
        else if (completedPattern is RoadHomeActionPattern.ModeTwo or RoadHomeActionPattern.ModeThree)
        {
            _normalPatternCursor = 0;
        }
    }

    private void ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }
        if (Creature.IsStunned
            || Creature is LibraryCreature { IsChaoed: true })
        {
            return;
        }

        string moveId = ResolvePlannedMoveId(RunRng.MonsterAi);
        if (_statesById.TryGetValue(moveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
        }
    }

    private Creature? GetHouseTarget() => RoadHomeEncounterHelper.FindHouse(Creature.CombatState);

    private IReadOnlyList<Creature> GetIndiscriminateTargets() =>
        CombatTargets.DeterministicLiving(
            Creature.CombatState?.Creatures,
            Creature);

    private int GetWalkTogetherDamage() => AscDamage(WalkTogetherLowDamage, WalkTogetherHighDamage);

    private int GetFriendHomeDamage() => AscDamage(FriendHomeLowDamage, FriendHomeHighDamage);

    private int GetBadWizardGroupDamage() => AscDamage(BadWizardGroupLowDamage, BadWizardGroupHighDamage);

    private static int AscDamage(int low, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, high, low);

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState move)
            {
                foreach (AbstractIntent intent in move.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

    private static LibraryCreatureResistanceData.Resistance NormalResistance() => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

}
