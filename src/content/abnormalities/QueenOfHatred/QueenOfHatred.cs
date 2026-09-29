using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.relics;
using LibraryOfRuina.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using QueenOfHatredCreatureVisuals = LibraryOfRuina.content.abnormalities.QueenOfHatred.QueenOfHatredCreatureVisuals;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

public sealed class QueenOfHatred : CounterIntentMonsterModel, ITargetedMonsterAttackProvider
{
    private const float LocalSfxVolumeScale = 0.85f;
    private static readonly float LocalSfxVolumeDb = Mathf.LinearToDb(LocalSfxVolumeScale);

    private const string QueenSfxRoot = "res://audio/sfx/queen_of_hatred/";
    private const string QueenAttackSfxPath = QueenSfxRoot + "queen_attack.ogg";
    private const string QueenMarkSfxPath = QueenSfxRoot + "queen_fire_mark.ogg";
    private const string QueenTransformEndSfxPath = QueenSfxRoot + "transform_end.ogg";
    private const string QueenSnakeAttackSfxPath = QueenSfxRoot + "snake_attack.ogg";
    private const string QueenSnakeFireSfxPath = QueenSfxRoot + "snake_fire.ogg";
    private const string QueenMagicSummonSfxPath = QueenSfxRoot + "magic_summon.ogg";
    private const string QueenMagicLoopSfxPath = QueenSfxRoot + "magic_loop.ogg";
    private const string QueenMagicEndSfxPath = QueenSfxRoot + "magic_end.ogg";

    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "QUEEN_OF_HATRED.backgroundText.normal.0",
        "QUEEN_OF_HATRED.backgroundText.normal.1",
        "QUEEN_OF_HATRED.backgroundText.normal.2",
        "QUEEN_OF_HATRED.backgroundText.normal.3",
        "QUEEN_OF_HATRED.backgroundText.normal.4",
        "QUEEN_OF_HATRED.backgroundText.normal.5"
    ];

    private static readonly string[] HysteriaBackgroundTextLineKeys =
    [
        "QUEEN_OF_HATRED.backgroundText.hysteria.0",
        "QUEEN_OF_HATRED.backgroundText.hysteria.1",
        "QUEEN_OF_HATRED.backgroundText.hysteria.2",
        "QUEEN_OF_HATRED.backgroundText.hysteria.3",
        "QUEEN_OF_HATRED.backgroundText.hysteria.4",
        "QUEEN_OF_HATRED.backgroundText.hysteria.5"
    ];

    private static readonly string[] LoveAndJusticeDamageKeys =
    [
        "FirstDamage",
        "SecondDamage",
        "ThirdDamage"
    ];

    private static readonly string[] LightOfHatredDamageKeys =
    [
        "FirstDamage",
        "SecondDamage"
    ];

    private static readonly string QueenOfHatredPageRelicTitleLocKey = $"{ModelDb.GetId<QueenOfHatredPageRelic>().Entry}.title";

    private const string ArcanaBeatsMoveId = "ARCANA_BEATS";
    private const string WithLoveMoveId = "WITH_LOVE";
    private const string InTheNameOfJusticeMoveId = "IN_THE_NAME_OF_JUSTICE";
    private const string InTheNameOfLoveAndJusticeMoveId = "IN_THE_NAME_OF_LOVE_AND_JUSTICE";
    private const string HumanArcanaSlaveMoveId = "ARCANA_SLAVE_HUMAN";
    private const string LightOfHatredMoveId = "LIGHT_OF_HATRED";
    private const string InTheNameOfLoveAndHateMoveId = "IN_THE_NAME_OF_LOVE_AND_HATE";
    private const string SnakeArcanaSlaveMoveId = "ARCANA_SLAVE_SNAKE";
    private const string QueenMoveRouterStateId = "QUEEN_ROUTER";

    private const int HumanArcanaSlaveTurn = 4;
    private const int RegularMoveLoopLength = 3;

    private const int SnakeArcanaSlaveTurn = 3;
    private const int HumanOpeningFormTurnCounter = -1;
    private const int SnakeOpeningFormTurnCounter = 0;
    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    public const int BindDamage = 3;
    public const int LoveJusticeNextTurnStrength = 2;
    public const int LoveAndHateVulnerable = 1;
    public const int LoveJusticeThirdHitReduction = 5;
    public const int SnakeStrength = 3;

    private bool _isSnakeForm;
    private bool _snakeArcanaSlaveUsed;
    private bool _snakeStunPending;
    private int _formTurnCounter;

    private Creature? _queuedArcanaTarget;
    private Creature? _markedTarget;
    private Creature? _forcedTarget;
    private HashSet<Creature>? _everMarkedPlayers;
    private bool _backgroundMoonTextLoopStarted;

    private MoveState? _arcanaBeatsState;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 440, 335);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 450, 340);

    public override int DefaultChaoResistance => 130;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        QueenOfHatredCreatureVisuals
            .Profile.AssetPaths
            .Concat(
            [
                QueenAttackSfxPath,
                QueenMarkSfxPath,
                QueenTransformEndSfxPath,
                QueenSnakeAttackSfxPath,
                QueenSnakeFireSfxPath,
                QueenMagicSummonSfxPath,
                QueenMagicLoopSfxPath,
                QueenMagicEndSfxPath
            ])
            .Distinct();

    public bool IsSnakeForm => _isSnakeForm;

    public int HumanLoveJusticeFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 11);

    public int HumanLoveJusticeSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 16);

    public int HumanLoveJusticeThirdDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 22, 21);

    public int SnakeLightOfHatredFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    public int SnakeLightOfHatredSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 21, 20);

    private int WithLoveDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 11, 10);

    private int WithLoveBlock => 19;

    private int JusticeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12);

    private int JusticeHits => 2;

    private int HumanArcanaSlaveDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 26, 25);

    private int LightOfHatredFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12);

    private int LightOfHatredSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 14);

    private int LoveAndHateDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 14);

    private int LoveAndHateHits => 2;

    private int SnakeArcanaSlaveDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 31, 30);

    private HashSet<Creature> EverMarkedPlayers => _everMarkedPlayers ??= new HashSet<Creature>();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        _isSnakeForm = false;
        _snakeArcanaSlaveUsed = false;
        _snakeStunPending = false;
        // The forced opening mark precedes the human-form move schedule.
        _formTurnCounter = HumanOpeningFormTurnCounter;
        _queuedArcanaTarget = null;
        _markedTarget = null;
        _forcedTarget = null;
        _everMarkedPlayers = new HashSet<Creature>();
        _backgroundMoonTextLoopStarted = false;

        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<LibraryOfRuinaQueenInversionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LibraryOfRuinaQueenInTheNameOfHatredPower>(Creature, 1m, Creature, null, silent: true);
        //await PowerCmdCompat.Apply<LibraryOfRuinaQueenMagicPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override Task BeforeCombatStart()
    {
        if (Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        if (!_backgroundMoonTextLoopStarted)
        {
            _backgroundMoonTextLoopStarted = true;
            StartBackgroundMoonTextLoop(NormalBackgroundTextLineKeys);
        }

        return Task.CompletedTask;
    }

    protected override bool ShouldQueueCounterIntentsForCurrentMove()
    {
        return !_isSnakeForm || NextMove.Id != ArcanaBeatsMoveId;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return Task.CompletedTask;
        }

        AddQueenOfHatredPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var arcanaBeats = new MoveState(
            ArcanaBeatsMoveId,
            ArcanaBeatsMove,
            new QueenArcanaBeatsIntent());

        var withLove = new MoveState(
            WithLoveMoveId,
            WithLoveMove,
            new TargetedMonsterAttackIntent(() => WithLoveDamage, () => 1, "QUEEN_WITH_LOVE.description"),
            new DefendIntent());

        var justice = new MoveState(
            InTheNameOfJusticeMoveId,
            InTheNameOfJusticeMove,
            new TargetedMonsterAttackIntent(() => JusticeDamage, () => JusticeHits, "QUEEN_IN_THE_NAME_OF_JUSTICE.description"));

        var loveJustice = new MoveState(
            InTheNameOfLoveAndJusticeMoveId,
            InTheNameOfLoveAndJusticeMove,
            new TargetedSegmentedAttackIntent(1, "QUEEN_LOVE_AND_JUSTICE.description", LoveAndJusticeDamageKeys, BuildLoveAndJusticePreview),
            new TargetedSegmentedAttackIntent(2, "QUEEN_LOVE_AND_JUSTICE.description", LoveAndJusticeDamageKeys, BuildLoveAndJusticePreview),
            new TargetedSegmentedAttackIntent(3, "QUEEN_LOVE_AND_JUSTICE.description", LoveAndJusticeDamageKeys, BuildLoveAndJusticePreview),
            new DebuffIntent(),
            new BuffIntent());

        var humanArcanaSlave = new MoveState(
            HumanArcanaSlaveMoveId,
            HumanArcanaSlaveMove,
            new SingleAttackIntent(HumanArcanaSlaveDamage));

        var lightOfHatred = new MoveState(
            LightOfHatredMoveId,
            LightOfHatredMove,
            new TargetedSegmentedAttackIntent(1, "QUEEN_LIGHT_OF_HATRED.description", LightOfHatredDamageKeys, BuildLightOfHatredPreview),
            new TargetedSegmentedAttackIntent(
                2,
                "QUEEN_LIGHT_OF_HATRED.description",
                LightOfHatredDamageKeys,
                BuildLightOfHatredPreview,
                IntentBadge.Heal()),
            new BuffIntent());

        var loveAndHate = new MoveState(
            InTheNameOfLoveAndHateMoveId,
            InTheNameOfLoveAndHateMove,
            new TargetedMonsterAttackIntent(() => LoveAndHateDamage, () => LoveAndHateHits, "QUEEN_IN_THE_NAME_OF_LOVE_AND_HATE.description"),
            new DebuffIntent(strong: true),
            new BuffIntent());

        var snakeArcanaSlave = new MoveState(
            SnakeArcanaSlaveMoveId,
            SnakeArcanaSlaveMove,
            new SingleAttackIntent(SnakeArcanaSlaveDamage));

        var stunned = new MoveState(
            stunnedMoveId,
            StunnedMove,
            new StunIntent());

        var chooser = new DelegatingMonsterRouterState(
            QueenMoveRouterStateId,
            (_, rng) =>
            {
                string nextMoveId = ResolvePlannedMoveId();
                if (nextMoveId == ArcanaBeatsMoveId)
                {
                    QueueArcanaTarget(rng);
                }

                return nextMoveId;
            });

        arcanaBeats.FollowUpState = chooser;
        withLove.FollowUpState = chooser;
        justice.FollowUpState = chooser;
        loveJustice.FollowUpState = chooser;
        humanArcanaSlave.FollowUpState = chooser;
        lightOfHatred.FollowUpState = chooser;
        loveAndHate.FollowUpState = chooser;
        snakeArcanaSlave.FollowUpState = chooser;
        stunned.FollowUpState = chooser;

        states.Add(arcanaBeats);
        states.Add(withLove);
        states.Add(justice);
        states.Add(loveJustice);
        states.Add(humanArcanaSlave);
        states.Add(lightOfHatred);
        states.Add(loveAndHate);
        states.Add(snakeArcanaSlave);
        states.Add(stunned);
        states.Add(chooser);

        _arcanaBeatsState = arcanaBeats;

        return new MonsterMoveStateMachine(states, chooser);
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        if (_forcedTarget != null)
        {
            return true;
        }

        string moveId = NextMove.Id;
        return moveId == ArcanaBeatsMoveId
            || moveId == WithLoveMoveId
            || moveId == InTheNameOfJusticeMoveId
            || moveId == InTheNameOfLoveAndJusticeMoveId
            || moveId == LightOfHatredMoveId
            || moveId == InTheNameOfLoveAndHateMoveId;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        if (_forcedTarget != null)
        {
            return [_forcedTarget];
        }

        string moveId = NextMove.Id;
        if (moveId == ArcanaBeatsMoveId)
        {
            Creature? target = ResolveQueuedArcanaTargetOrFallback();
            return target == null ? Array.Empty<Creature>() : [target];
        }

        Creature? markedTarget = ResolveMarkedAttackTarget();
        return markedTarget == null ? Array.Empty<Creature>() : [markedTarget];
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        return GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "Unknown Target";
    }

    public async Task TransformToSnake()
    {
        if (_isSnakeForm || Creature.IsDead)
        {
            return;
        }

        await PresentationGuard.RunAsync(
            () => QueenOfHatredInversionVideoController.PlayAsync(),
            "QueenOfHatred inversion video");

        _isSnakeForm = true;
        _formTurnCounter = SnakeOpeningFormTurnCounter;
        _snakeStunPending = false;
        _queuedArcanaTarget = null;
        _forcedTarget = null;

        PresentationGuard.Run(() =>
        {
            if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals is QueenOfHatredCreatureVisuals visuals)
            {
                visuals.SetSnakeForm(true);
            }

            LocalOggOneShotPlayer.Play(QueenTransformEndSfxPath, LocalSfxVolumeDb);
            StartBackgroundMoonTextLoop(HysteriaBackgroundTextLineKeys);
        }, "QueenOfHatred snake form visuals");

        if (_arcanaBeatsState != null)
        {
            QueueArcanaTarget(RunRng.MonsterAi);
            SetMoveImmediate(_arcanaBeatsState, forceTransition: true);
        }

        await PowerCmd.Remove(Creature.GetPower<LibraryOfRuinaQueenInversionPower>());

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    public async Task EnsurePersistentMarkPackages()
    {
        foreach (Creature creature in EverMarkedPlayers.ToArray())
        {
            if (creature == null || creature.IsDead || creature.CombatState != CombatState)
            {
                continue;
            }

            await EnsurePermanentMarkPackage(creature);
        }
    }

    private async Task ArcanaBeatsMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = ResolveQueuedArcanaTargetOrFallback();
        _queuedArcanaTarget = null;
        if (target == null)
        {
            AdvanceFormTurnCounter();
            return;
        }

        await ApplyMarkToTarget(target);
        AdvanceFormTurnCounter();
    }

    private async Task WithLoveMove(IReadOnlyList<Creature> targets)
    {
        using IDisposable forcedTargetScope = CreateForcedTargetScope(targets, out Creature? target);
        if (target == null)
        {
            AdvanceFormTurnCounter();
            return;
        }

        using (new TargetedAttackLungeScope(this, [target]))
        {
            await ExecuteTargetedAttack(WithLoveDamage);
        }

        await CreatureCmd.GainBlock(Creature, WithLoveBlock, ValueProp.Move, null);
        AdvanceFormTurnCounter();
    }

    private async Task InTheNameOfJusticeMove(IReadOnlyList<Creature> targets)
    {
        using IDisposable forcedTargetScope = CreateForcedTargetScope(targets, out Creature? target);
        if (target == null)
        {
            AdvanceFormTurnCounter();
            return;
        }

        using (new TargetedAttackLungeScope(this, [target]))
        {
            for (int i = 0; i < JusticeHits; i++)
            {
                await ExecuteTargetedAttack(JusticeDamage);
            }
        }

        AdvanceFormTurnCounter();
    }

    private async Task InTheNameOfLoveAndJusticeMove(IReadOnlyList<Creature> targets)
    {
        using IDisposable forcedTargetScope = CreateForcedTargetScope(targets, out Creature? target);
        if (target == null)
        {
            AdvanceFormTurnCounter();
            return;
        }

        int failedBlockBreakCount = 0;
        using (new TargetedAttackLungeScope(this, [target]))
        {
            DamageResult? firstResult = await ExecuteTargetedAttack(HumanLoveJusticeFirstDamage);
            if (HadUnbrokenBlockBeforeHit(firstResult))
            {
                failedBlockBreakCount++;
            }

            DamageResult? secondResult = await ExecuteTargetedAttack(HumanLoveJusticeSecondDamage);
            if (HadUnbrokenBlockBeforeHit(secondResult))
            {
                failedBlockBreakCount++;
            }

            int thirdDamage = Math.Max(0, HumanLoveJusticeThirdDamage - failedBlockBreakCount * LoveJusticeThirdHitReduction);
            await ExecuteTargetedAttack(thirdDamage);
        }

        if (target.IsAlive)
        {
            await LibraryPowerCmd.Apply<LibraryBindingPower>(new ThrowingPlayerChoiceContext(), target, BindDamage, 0, false, Creature, null);
        }

        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, LoveJusticeNextTurnStrength, Creature, null);
        AdvanceFormTurnCounter();
    }

    private async Task HumanArcanaSlaveMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteMagicMassAttackSegment(HumanArcanaSlaveDamage);

        AdvanceFormTurnCounter();
    }

    private async Task LightOfHatredMove(IReadOnlyList<Creature> targets)
    {
        using IDisposable forcedTargetScope = CreateForcedTargetScope(targets, out Creature? target);
        if (target == null)
        {
            AdvanceFormTurnCounter();
            return;
        }

        DamageResult? secondHitResult;
        using (new TargetedAttackLungeScope(this, [target]))
        {
            await ExecuteTargetedAttack(LightOfHatredFirstDamage, sfxPathOverride: QueenSnakeAttackSfxPath);
            secondHitResult = await ExecuteTargetedAttack(LightOfHatredSecondDamage, sfxPathOverride: QueenSnakeFireSfxPath);
        }

        int healAmount = secondHitResult?.UnblockedDamage ?? 0;
        if (healAmount > 0)
        {
            await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, healAmount));
        }
        await PowerCmdCompat.Apply<StrengthPower>(Creature, SnakeStrength, Creature, null);
        AdvanceFormTurnCounter();
    }

    private async Task InTheNameOfLoveAndHateMove(IReadOnlyList<Creature> targets)
    {
        using IDisposable forcedTargetScope = CreateForcedTargetScope(targets, out Creature? target);
        if (target == null)
        {
            AdvanceFormTurnCounter();
            return;
        }

        using (new TargetedAttackLungeScope(this, [target]))
        {
            for (int i = 0; i < LoveAndHateHits; i++)
            {
                string sfx = i == 0 ? QueenSnakeAttackSfxPath : QueenSnakeFireSfxPath;
                await ExecuteTargetedAttack(LoveAndHateDamage, sfxPathOverride: sfx);
            }
        }

        if (target.IsAlive)
        {
            await PowerCmdCompat.Apply<VulnerablePower>(target, LoveAndHateVulnerable, Creature, null);
        }
        await PowerCmdCompat.Apply<StrengthPower>(Creature, LoveJusticeNextTurnStrength, Creature, null);
        AdvanceFormTurnCounter();
    }

    private async Task SnakeArcanaSlaveMove(IReadOnlyList<Creature> targets)
    {
        _snakeArcanaSlaveUsed = true;
        await ExecuteMagicMassAttackSegment(SnakeArcanaSlaveDamage);

        _snakeStunPending = true;
        AdvanceFormTurnCounter();
    }

    private Task StunnedMove(IReadOnlyList<Creature> targets)
    {
        _snakeStunPending = false;
        return Task.CompletedTask;
    }

    private Creature? QueueArcanaTarget(Rng rng)
    {
        if (_queuedArcanaTarget != null && _queuedArcanaTarget.IsAlive && _queuedArcanaTarget.IsPlayer)
        {
            return _queuedArcanaTarget;
        }

        List<Creature> livingPlayers = GetLivingPlayers();
        if (livingPlayers.Count == 0)
        {
            _queuedArcanaTarget = null;
            return null;
        }

        _queuedArcanaTarget = rng.NextItem(livingPlayers);
        return _queuedArcanaTarget;
    }

    private Creature? ResolveQueuedArcanaTargetOrFallback()
    {
        if (_queuedArcanaTarget != null && _queuedArcanaTarget.IsAlive && _queuedArcanaTarget.IsPlayer)
        {
            return _queuedArcanaTarget;
        }

        return CombatState.PlayerCreatures.FirstOrDefault(creature => creature.IsAlive);
    }

    private Creature? ResolveMarkedAttackTarget()
    {
        if (_markedTarget != null
            && _markedTarget.IsAlive
            && _markedTarget.GetPower<LibraryOfRuinaMarkPower>() != null)
        {
            return _markedTarget;
        }

        return CombatState.PlayerCreatures
            .FirstOrDefault(creature => creature.IsAlive && creature.GetPower<LibraryOfRuinaMarkPower>() != null)
            ?? GetLivingPlayers().FirstOrDefault();
    }

    private async Task ApplyMarkToTarget(Creature target)
    {
        LocalOggOneShotPlayer.Play(QueenMarkSfxPath, LocalSfxVolumeDb);
        await QueenOfHatredMarkHelper.ApplyMarkPackage<LibraryOfRuinaMarkPower>(
            CombatState.PlayerCreatures,
            target,
            Creature);
        _markedTarget = target;
        EverMarkedPlayers.Add(target);
        await EnsurePermanentMarkPackage(target);
    }

    private async Task EnsurePermanentMarkPackage(Creature target)
    {
        await EnsurePermanentRapidWear(target);
    }

    private async Task EnsurePermanentRapidWear(Creature target)
    {
        await QueenOfHatredMarkHelper.EnsurePermanentRapidWear(target, Creature);
    }

    private static bool IsQueenOfHatredEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is QueenOfHatred);
    }

    private void AddQueenOfHatredPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsQueenOfHatredEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<QueenOfHatredPageRelic>(room, QueenOfHatredPageRelicTitleLocKey);
    }

    private List<Creature> GetLivingPlayers()
    {
        return CombatState.PlayerCreatures.Where(creature => creature.IsAlive).ToList();
    }

    private void AdvanceFormTurnCounter()
    {
        _formTurnCounter++;
        _queuedArcanaTarget = null;
    }

    private string ResolvePlannedMoveId()
    {
        if (ShouldForceMarkMove())
        {
            return ArcanaBeatsMoveId;
        }

        if (_snakeStunPending)
        {
            return stunnedMoveId;
        }

        string planned = _isSnakeForm
            ? ResolveSnakeMoveId()
            : ResolveHumanMoveId();

        if (ShouldRedirectToReMarkAfterMarkedDeath(planned))
        {
            return ArcanaBeatsMoveId;
        }

        return planned;
    }

    private bool ShouldForceMarkMove()
    {
        List<Creature> livingPlayers = GetLivingPlayers();
        return livingPlayers.Count > 0
            && !livingPlayers.Any(creature => creature.GetPower<LibraryOfRuinaMarkPower>() != null);
    }

    private bool ShouldRedirectToReMarkAfterMarkedDeath(string plannedMoveId)
    {
        if (plannedMoveId == ArcanaBeatsMoveId
            || plannedMoveId == HumanArcanaSlaveMoveId
            || plannedMoveId == SnakeArcanaSlaveMoveId
            || plannedMoveId == stunnedMoveId)
        {
            return false;
        }

        if (_markedTarget == null || _markedTarget.IsAlive)
        {
            return false;
        }

        return GetLivingPlayers().Count > 0;
    }

    private string ResolveHumanMoveId()
    {
        int turnNumber = _formTurnCounter + 1;
        if (turnNumber == HumanArcanaSlaveTurn)
        {
            return HumanArcanaSlaveMoveId;
        }

        if (turnNumber % 2 == 0)
        {
            return ArcanaBeatsMoveId;
        }

        int regularIndex = CountRegularHumanMovesBeforeTurn(turnNumber) % RegularMoveLoopLength;
        return regularIndex switch
        {
            0 => WithLoveMoveId,
            1 => InTheNameOfJusticeMoveId,
            _ => InTheNameOfLoveAndJusticeMoveId
        };
    }

    private static int CountRegularHumanMovesBeforeTurn(int turnNumber)
    {
        int count = 0;
        for (int turn = 1; turn < turnNumber; turn++)
        {
            if (turn != HumanArcanaSlaveTurn && turn % 2 != 0)
            {
                count++;
            }
        }

        return count;
    }

    private string ResolveSnakeMoveId()
    {
        if (!_snakeArcanaSlaveUsed)
        {
            return _formTurnCounter switch
            {
                0 => ArcanaBeatsMoveId,
                1 => LightOfHatredMoveId,
                2 => InTheNameOfLoveAndHateMoveId,
                SnakeArcanaSlaveTurn => SnakeArcanaSlaveMoveId,
                _ => ResolveSnakePostSlaveMoveId(),
            };
        }

        return ResolveSnakePostSlaveMoveId();
    }

    private string ResolveSnakePostSlaveMoveId()
    {
        int postSlaveTurn = Math.Max(0, _formTurnCounter - (SnakeArcanaSlaveTurn + 1));
        return (postSlaveTurn % 4) switch
        {
            0 => ArcanaBeatsMoveId,
            1 => LightOfHatredMoveId,
            2 => ArcanaBeatsMoveId,
            _ => InTheNameOfLoveAndHateMoveId,
        };
    }

    private IDisposable CreateForcedTargetScope(IReadOnlyList<Creature> targets, out Creature? target)
    {
        target = TargetedMonsterAttackHelper.GetPrimaryTarget(Creature, targets);
        return new ForcedTargetScope(this, target);
    }

    private async Task<DamageResult?> ExecuteTargetedAttack(int damage, string? sfxPathOverride = null)
    {
        if (damage <= 0)
        {
            return null;
        }

        string? resolvedSfx = sfxPathOverride ?? ResolveDefaultAttackSfxPath();
        if (!string.IsNullOrWhiteSpace(resolvedSfx))
        {
            LocalOggOneShotPlayer.Play(resolvedSfx!, LocalSfxVolumeDb);
        }

        AttackCommand attack = await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        return AttackCommandCompat.Results(attack).FirstOrDefault();
    }

    private string ResolveDefaultAttackSfxPath()
    {
        return _isSnakeForm ? QueenSnakeAttackSfxPath : QueenAttackSfxPath;
    }

    private async Task ExecuteMagicMassAttackSegment(int damage)
    {
        await PresentationGuard.RunAsync(
            () => LocalOggOneShotPlayer.PlayAsync(QueenMagicSummonSfxPath, LocalSfxVolumeDb),
            "QueenOfHatred magic summon sfx");
        using LocalOggLoopPlayer.LoopHandle? loop = LocalOggLoopPlayer.StartLoop(QueenMagicLoopSfxPath, LocalSfxVolumeDb);
        try
        {
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, damage);
        }
        finally
        {
            loop?.Stop();
            LocalOggOneShotPlayer.Play(QueenMagicEndSfxPath, LocalSfxVolumeDb);
        }
    }

    private static bool HadUnbrokenBlockBeforeHit(DamageResult? result)
    {
        if (result == null)
        {
            return false;
        }

        return result.BlockedDamage > 0 && !result.WasBlockBroken;
    }

    private static void StartBackgroundMoonTextLoop(IReadOnlyList<string> lineKeys)
    {
        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private static TargetedSegmentedAttackPreview BuildLoveAndJusticePreview(Creature owner, IReadOnlyList<Creature>? fallbackTargets)
    {
        if (owner.Monster is not QueenOfHatred queen)
        {
            return TargetedSegmentedAttackPreview.Empty(3);
        }

        Creature? target = TargetedMonsterAttackHelper.GetPrimaryTarget(owner, fallbackTargets);
        int firstDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, queen.HumanLoveJusticeFirstDamage);
        int secondDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, queen.HumanLoveJusticeSecondDamage);

        int remainingBlock = target?.Block ?? 0;
        int reductionSegments = 0;

        reductionSegments += CountFailedBlockBreakPreview(firstDamage, ref remainingBlock);
        reductionSegments += CountFailedBlockBreakPreview(secondDamage, ref remainingBlock);

        int thirdBaseDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, queen.HumanLoveJusticeThirdDamage);
        int thirdDamage = Math.Max(0, thirdBaseDamage - reductionSegments * LoveJusticeThirdHitReduction);

        Dictionary<string, object?> extraVariables = new Dictionary<string, object?>
        {
            ["BindAmount"] = BindDamage,
            ["StrengthAmount"] = LoveJusticeNextTurnStrength
        };

        return new TargetedSegmentedAttackPreview(
            [firstDamage, secondDamage, thirdDamage],
            target?.Name ?? "Unknown Target",
            extraVariables);
    }

    private static TargetedSegmentedAttackPreview BuildLightOfHatredPreview(Creature owner, IReadOnlyList<Creature>? fallbackTargets)
    {
        if (owner.Monster is not QueenOfHatred queen)
        {
            return TargetedSegmentedAttackPreview.Empty(2);
        }

        Creature? target = TargetedMonsterAttackHelper.GetPrimaryTarget(owner, fallbackTargets);
        int firstDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, queen.SnakeLightOfHatredFirstDamage);
        int secondDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, queen.SnakeLightOfHatredSecondDamage);
        int remainingBlock = target?.Block ?? 0;

        remainingBlock = Math.Max(0, remainingBlock - firstDamage);
        int healAmount = Math.Max(0, secondDamage - remainingBlock);

        Dictionary<string, object?> extraVariables = new Dictionary<string, object?>
        {
            ["HealAmount"] = healAmount
        };

        return new TargetedSegmentedAttackPreview(
            [firstDamage, secondDamage],
            target?.Name ?? "Unknown Target",
            extraVariables);
    }

    private static int CountFailedBlockBreakPreview(int damage, ref int remainingBlock)
    {
        if (remainingBlock <= 0)
        {
            return 0;
        }

        if (damage >= remainingBlock)
        {
            remainingBlock = 0;
            return 0;
        }

        remainingBlock -= damage;
        return 1;
    }

    private sealed class ForcedTargetScope : IDisposable
    {
        private readonly QueenOfHatred _owner;
        private readonly Creature? _previousForcedTarget;

        public ForcedTargetScope(QueenOfHatred owner, Creature? forcedTarget)
        {
            _owner = owner;
            _previousForcedTarget = owner._forcedTarget;
            owner._forcedTarget = forcedTarget;
        }

        public void Dispose()
        {
            _owner._forcedTarget = _previousForcedTarget;
        }
    }

}
