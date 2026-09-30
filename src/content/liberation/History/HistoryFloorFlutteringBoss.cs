using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.History;

public sealed class HistoryFloorFlutteringBoss : LiberationPhaseBossMonster
{
    private const int Phase = 3;
    internal const int StaggerResistance = 60;

    public override int DefaultChaoResistance => StaggerResistance;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    private const string BreathMoveId = "BREATH";
    private const string HungerWingsMoveId = "HUNGER_WINGS";
    private const string PredationMoveId = "PREDATION";
    private const string HungerFrenzyMoveId = "HUNGER_FRENZY";

    private const int BreathBlock = 10;
    private const int PredationHeal = 10;
    private const int PredationBlock = 8;
    private const int HungerWingsBleed = 1;
    private const float SegmentDelaySeconds = 1.25f;
    internal const string BackgroundTextScope = "history_floor_liberation_phase_3";
    private const float BackgroundTextIntervalSeconds = 5f;

    public const string Root = "res://images/monsters/history_floor/fluttering/";
    public const string IdleTexturePath = Root + "idle_s1.png";
    public const string HungerFrenzyS2TexturePath = Root + "idle_s2.png";
    public const string HungerFrenzyS3TexturePath = Root + "idle_s3.png";
    public const string HungerFrenzyS4TexturePath = Root + "idle_s4.png";
    public const string AttackStrikeTexturePath = Root + "attack_strike.png";
    public const string AttackSlashTexturePath = Root + "attack_slash.png";
    public const string HitTexturePath = Root + "hit.png";

    public const string BossAttackSfxPath = "res://audio/sfx/history_floor/fluttering/boss_attack.ogg";
    public const string ChangeSfxPath = "res://audio/sfx/history_floor/fluttering/boss_change.ogg";
    public const string DevourSfxPath = "res://audio/sfx/history_floor/fluttering/boss_devour.ogg";
    public const string SpecialSfxPath = "res://audio/sfx/history_floor/fluttering/special.ogg";

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);
    private static readonly string[] BackgroundTextLineKeys =
    [
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.0",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.1",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.2",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.3",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.4",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.5",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.6",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.7",
        "HISTORY_FLOOR_FLUTTERING_BOSS.backgroundText.normal.8"
    ];

    private static readonly string[] SfxAssetPaths =
    [
        BossAttackSfxPath,
        ChangeSfxPath,
        DevourSfxPath,
        SpecialSfxPath
    ];

    private int _baseCadenceIndex;
    private int _attackGroupSerial;
    private int _currentFreshMeatAttackGroupId;
    private bool _hungerFrenzyQueued;
    private bool _backgroundMoonTextLoopStarted;
    private MoveState? _hungerFrenzyState;

    public override int LiberationPhase => Phase;

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    protected override string? ReviveAndEmpowerAnimation => "Cast";

    internal int CurrentFreshMeatAttackGroupId => _currentFreshMeatAttackGroupId;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 133, 130);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 135, 132);

    private static int BreathDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private static int HungerWingsDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private static int PredationDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private static int HungerFrenzyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, HistoryFloorEgoNumbers.HungerFrenzyBossDeadlyMultiHitDamage, HistoryFloorEgoNumbers.HungerFrenzyBossMultiHitDamage);

    private static int HungerFrenzyFinalDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, HistoryFloorEgoNumbers.HungerFrenzyBossDeadlyFinalDamage, HistoryFloorEgoNumbers.HungerFrenzyBossFinalDamage);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorFlutteringBossCreatureVisuals.Profile.AssetPaths
            .Concat(SfxAssetPaths)
            .Concat(new[] { HistoryFloorLiberationBackgroundController.FlutteringPredationOverlayTexturePath })
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _baseCadenceIndex = 0;
        _attackGroupSerial = 0;
        _currentFreshMeatAttackGroupId = 0;
        _hungerFrenzyQueued = false;
        _backgroundMoonTextLoopStarted = false;
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<FlutteringHungerFrenzyEgoCard>());

        if (CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        HistoryFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<FlutteringHungerPower>(Creature, 40m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<HistoryFloorCorrosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<FlutteringMomentarySatietyPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<FlutteringHungerFrenzyPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<FlutteringFreshMeatPassivePower>(Creature, 1m, Creature, null, silent: true);
        StartBackgroundMoonTextLoop();
    }

    public override Task BeforeCombatStart()
    {
        HistoryFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        StartBackgroundMoonTextLoop();
        return Task.CompletedTask;
    }

    public override void BeforeRemovedFromRoom()
    {
        StopBackgroundMoonTextLoop();
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not HistoryFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    public async Task QueueHungerFrenzy()
    {
        if (_hungerFrenzyQueued || Creature.IsDead || _hungerFrenzyState == null)
        {
            return;
        }

        _hungerFrenzyQueued = true;
        SetMoveImmediate(_hungerFrenzyState, forceTransition: true);

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    public async Task QueueHungerFrenzyAfterStun()
    {
        if (_hungerFrenzyQueued || Creature.IsDead || _hungerFrenzyState == null)
        {
            return;
        }

        _hungerFrenzyQueued = true;
        await CreatureCmd.Stun(Creature, HungerFrenzyMoveId);
    }

    public async Task Devour(Creature food)
    {
        if (Creature.IsDead || food.IsDead)
        {
            return;
        }

        HistoryFloorLiberationBackgroundController.PlayFlutteringPredationOverlay();
        LocalOggOneShotPlayer.Play(DevourSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", SegmentDelaySeconds);
        await CreatureCmd.Kill(food, force: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var breath = new MoveState(
            BreathMoveId,
            BreathMove,
            new SingleAttackIntent(BreathDamage),
            new DefendIntent());

        var hungerWings = new MoveState(
            HungerWingsMoveId,
            HungerWingsMove,
            CreateBleedIntent(HungerWingsDamage, HungerWingsBleed),
            CreateBleedIntent(HungerWingsDamage, HungerWingsBleed),
            CreateBleedIntent(HungerWingsDamage, HungerWingsBleed));

        var predation = new MoveState(
            PredationMoveId,
            PredationMove,
            new SingleAttackIntent(PredationDamage),
            new HealIntent(),
            new DefendIntent());

        var hungerFrenzy = new MoveState(
            HungerFrenzyMoveId,
            HungerFrenzyMove,
            new PlayCardAttackIntent<FlutteringHungerFrenzyEgoCard>(
                "FLUTTERING_HUNGER_FRENZY_EGO_CARD",
                () => HungerFrenzyDamage,
                (card, damages) => { card.SetPreviewDamage(damages[0], damages[1]); card.UpgradePreview(); },
                () => HistoryFloorEgoNumbers.HungerFrenzyHitCount, () => HungerFrenzyFinalDamage),
            new HealIntent(),
            CreateBleedIntent(HungerFrenzyFinalDamage, HistoryFloorEgoNumbers.HungerFrenzyBleed));

        _hungerFrenzyState = hungerFrenzy;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(hungerFrenzy, () => _hungerFrenzyQueued);
        chooser.AddState(breath, () => _baseCadenceIndex == 0);
        chooser.AddState(hungerWings, () => _baseCadenceIndex == 1);
        chooser.AddState(predation, () => true);

        breath.FollowUpState = chooser;
        hungerWings.FollowUpState = chooser;
        predation.FollowUpState = chooser;
        hungerFrenzy.FollowUpState = chooser;
        reviveAndEmpower.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { reviveAndEmpower, breath, hungerWings, predation, hungerFrenzy, chooser },
            chooser);
    }

    private async Task BreathMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("AttackStrike", BreathDamage);
        await CreatureCmd.GainBlock(Creature, BreathBlock, ValueProp.Move, null);
        AdvanceBaseCadence();
    }

    private async Task HungerWingsMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 3; i++)
        {
            if (Creature.IsDead) return;
            IReadOnlyList<DamageResult> results = await ExecuteAttackSegment(i % 2 == 0 ? "AttackSlash" : "AttackStrike", HungerWingsDamage);
            await ApplyBleedFromResults(results, HungerWingsBleed);
        }

        AdvanceBaseCadence();
    }

    private async Task PredationMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("AttackStrike", PredationDamage);
        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, PredationHeal));
        await CreatureCmd.GainBlock(Creature, PredationBlock, ValueProp.Move, null);
        AdvanceBaseCadence();
    }

    private async Task HungerFrenzyMove(IReadOnlyList<Creature> targets)
    {
        BeginFreshMeatAttackGroup();
        try
        {
            for (int i = 0; i < HistoryFloorEgoNumbers.HungerFrenzyHitCount; i++)
            {
                if (Creature.IsDead) return;
                await ExecuteAttackSegment("HungerFrenzy", HungerFrenzyDamage);
            }
        }
        finally
        {
            EndFreshMeatAttackGroup();
        }

        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, HistoryFloorEgoNumbers.HungerFrenzyHeal));
        IReadOnlyList<DamageResult> finalResults = await ExecuteAttackSegment("HungerFrenzy", HungerFrenzyFinalDamage);
        await ApplyBleedFromResults(finalResults, HistoryFloorEgoNumbers.HungerFrenzyBleed);

        _hungerFrenzyQueued = false;
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttackSegment(string animation, int damage)
    {
        if (Creature == null || Creature.CombatState == null)
        {
            return Array.Empty<DamageResult>();
        }

        bool ownsAttackGroup = _currentFreshMeatAttackGroupId <= 0;
        if (ownsAttackGroup)
        {
            BeginFreshMeatAttackGroup();
        }

        AttackCommand attack;
        try
        {
            LocalOggOneShotPlayer.Play(BossAttackSfxPath, -2f);
            attack = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim(animation, SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
        finally
        {
            if (ownsAttackGroup)
            {
                EndFreshMeatAttackGroup();
            }
        }

        return AttackCommandCompat.Results(attack);
    }

    private async Task ApplyBleedFromResults(IEnumerable<DamageResult> results, int bleedAmount)
    {
        IReadOnlyList<Creature> bleedTargets = results
            .Where(static result => result.Receiver.IsPlayer && result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToList();
        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, bleedAmount, Creature, null);
        }
    }

    private static BadgedAttackIntent CreateBleedIntent(int damage, int bleedAmount) =>
        new(
            damage,
            "FLUTTERING_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(bleedAmount));

    private void AdvanceBaseCadence()
    {
        _baseCadenceIndex = (_baseCadenceIndex + 1) % 3;
    }

    private void StartBackgroundMoonTextLoop()
    {
        if (_backgroundMoonTextLoopStarted || Creature.IsDead)
        {
            return;
        }

        _backgroundMoonTextLoopStarted = true;
        MoonTextService.StartRandomLoop(
            Creature,
            BackgroundTextScope,
            BackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    internal void StopBackgroundMoonTextLoop()
    {
        if (Creature != null)
        {
            MoonTextService.StopRandomLoop(Creature, BackgroundTextScope);
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(BreathDamage);
        yield return new DefendIntent();
        yield return CreateBleedIntent(HungerWingsDamage, HungerWingsBleed);
        yield return new SingleAttackIntent(PredationDamage);
        yield return new HealIntent();
        yield return new PlayCardAttackIntent<FlutteringHungerFrenzyEgoCard>(
            "FLUTTERING_HUNGER_FRENZY_EGO_CARD",
            () => HungerFrenzyDamage,
            (card, damages) => { card.SetPreviewDamage(damages[0], damages[1]); card.UpgradePreview(); },
            () => HistoryFloorEgoNumbers.HungerFrenzyHitCount, () => HungerFrenzyFinalDamage);
        yield return CreateBleedIntent(HungerFrenzyFinalDamage, HistoryFloorEgoNumbers.HungerFrenzyBleed);
        yield return new BuffIntent();
    }

    private void BeginFreshMeatAttackGroup()
    {
        _currentFreshMeatAttackGroupId = ++_attackGroupSerial;
    }

    private void EndFreshMeatAttackGroup()
    {
        _currentFreshMeatAttackGroupId = 0;
    }
}
