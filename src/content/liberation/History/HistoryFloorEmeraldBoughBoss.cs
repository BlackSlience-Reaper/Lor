using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.History;

public sealed class HistoryFloorEmeraldBoughBoss : LiberationPhaseBossMonster
{
    private const int Phase = 5;
    internal const int StaggerResistance = 120;

    public override int DefaultChaoResistance => StaggerResistance;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    private const string SummonVineBarriersMoveId = "SUMMON_VINE_BARRIERS";
    private const string EmeraldBoughMoveId = "EMERALD_BOUGH";
    private const string DelusionalVineMoveId = "DELUSIONAL_VINE";
    private const string GrudgeVineMoveId = "GRUDGE_VINE";
    private const string ExtendedMaliceMoveId = "EXTENDED_MALICE";
    private const string ShatteredLifeMoveId = "SHATTERED_LIFE";

    private const int EmeraldBoughRapidWearAmount = 9;
    private const int EmeraldBoughRapidWearTurns = 3;
    private const int DelusionalVineBindAmount = 1;
    private const int GrudgeVineWoundCount = 2;
    private const int ExtendedMaliceBlock = 17;
    private const int ExtendedMaliceWeak = 3;
    private const float SegmentDelaySeconds = 0.95f;
    private const float BackgroundTextIntervalSeconds = 5f;

    internal const string BackgroundTextScope = "history_floor_liberation_phase_5";

    public const string Root = "res://images/monsters/history_floor/emerald_bough/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string AttackTexturePath = Root + "attack.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string ParryTexturePath = Root + "parry.png";
    public const string EgoTexturePath = Root + "ego_s1.png";

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);
    private static readonly string[] BackgroundTextLineKeys =
    [
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.0",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.1",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.2",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.3",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.4",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.5",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.6",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.7",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.8",
        "HISTORY_FLOOR_EMERALD_BOUGH_BOSS.backgroundText.normal.9"
    ];

    private int _turnIndex;
    private bool _firstTurn = true;
    private bool _egoQueued;
    private bool _backgroundMoonTextLoopStarted;
    private MoveState? _shatteredLifeState;
    private MoveState? _emeraldBoughState;
    private ConditionalBranchState? _chooserState;

    public override int LiberationPhase => Phase;

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    protected override string? ReviveAndEmpowerAnimation => "Cast";

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 252, 250);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 253, 251);

    private static int EmeraldBoughDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

    private static int DelusionalVineHitADamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    private static int DelusionalVineHitBDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    private static int DelusionalVineHitCDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    private static int GrudgeVineDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 10);

    private static int ShatteredLifeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, HistoryFloorEgoNumbers.ShatteredLifeAscensionDamage, HistoryFloorEgoNumbers.ShatteredLifeBaseDamage);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorEmeraldBoughCreatureVisuals.Profile.AssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _turnIndex = 0;
        _firstTurn = true;
        _egoQueued = false;
        _backgroundMoonTextLoopStarted = false;
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<ShatteredLifeEgoCard>());

        if (CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        HistoryFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);
        
        await PowerCmdCompat.Apply<HistoryFloorCorrosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<EmeraldBoughVineBarrierPower>(Creature, 1m, Creature, null, silent: true);
        Creature.GetPower<EmeraldBoughVineBarrierPower>()?.SetAmount(0, silent: true);
        await PowerCmdCompat.Apply<EmeraldBoughForestApplePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<EmeraldBoughWhereAreYouPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<UntargetablePower>(Creature);

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

    public async Task QueueShatteredLife()
    {
        if (_egoQueued || Creature.IsDead || _shatteredLifeState == null)
        {
            return;
        }

        _egoQueued = true;
        SetMoveImmediate(_shatteredLifeState, forceTransition: true);

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    public async Task OnVineBarrierDestroyed()
    {
        EmeraldBoughVineBarrierPower? vineBarrierPower = Creature.GetPower<EmeraldBoughVineBarrierPower>();
        if (vineBarrierPower != null)
        {
            await vineBarrierPower.RegisterVineBarrierKill();
        }

        EmeraldBoughForestApplePower? forestApple = Creature.GetPower<EmeraldBoughForestApplePower>();
        if (forestApple != null)
        {
            await forestApple.RefreshVineState();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var summonVineBarriers = new MoveState(
            SummonVineBarriersMoveId,
            SummonVineBarriersMove,
            new SummonIntent());

        var emeraldBough = new MoveState(
            EmeraldBoughMoveId,
            EmeraldBoughMove,
            new SingleAttackIntent(EmeraldBoughDamage),
            new BadgedDebuffIntent(
                IntentBadge.FromPower<LibraryVulnerablePower>(
                    EmeraldBoughRapidWearAmount,
                    $"{EmeraldBoughRapidWearAmount}",
                    $"{EmeraldBoughRapidWearTurns}"),
                EmeraldBoughRapidWearAmount));

        var delusionalVine = new MoveState(
            DelusionalVineMoveId,
            DelusionalVineMove,
            new SingleAttackIntent(DelusionalVineHitADamage),
            new SingleAttackIntent(DelusionalVineHitBDamage),
            new SingleAttackIntent(DelusionalVineHitCDamage),
            new DebuffIntent());

        var grudgeVine = new MoveState(
            GrudgeVineMoveId,
            GrudgeVineMove,
            new SingleAttackIntent(GrudgeVineDamage),
            new SingleAttackIntent(GrudgeVineDamage),
            new DetailedStatusCardIntent<Wound>(GrudgeVineWoundCount, PileType.Draw, showSingleTargetMarker: false));

        var extendedMalice = new MoveState(
            ExtendedMaliceMoveId,
            ExtendedMaliceMove,
            new DefendIntent(),
            new DebuffIntent(true));

        var shatteredLife = new MoveState(
            ShatteredLifeMoveId,
            ShatteredLifeMove,
            new PlayCardAttackIntent<ShatteredLifeEgoCard>(
                "SHATTERED_LIFE_EGO_CARD",
                () => ShatteredLifeDamage,
                static (card, damages) => { card.UpgradePreview(); card.SetPreviewDamage(damages[0]); },
                () => HistoryFloorEgoNumbers.ShatteredLifeHitCount));

        _shatteredLifeState = shatteredLife;
        _emeraldBoughState = emeraldBough;

        var withVineBranch = new RandomBranchState("WITH_VINE_RAND");
        withVineBranch.AddBranch(emeraldBough, MoveRepeatType.CannotRepeat);
        withVineBranch.AddBranch(delusionalVine, MoveRepeatType.CannotRepeat);
        withVineBranch.AddBranch(grudgeVine, MoveRepeatType.CannotRepeat);

        var noVineBranch = new RandomBranchState("NO_VINE_RAND");
        noVineBranch.AddBranch(emeraldBough, MoveRepeatType.CannotRepeat);
        noVineBranch.AddBranch(extendedMalice, MoveRepeatType.CannotRepeat);

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(shatteredLife, () => _egoQueued);
        chooser.AddState(summonVineBarriers, () => _firstTurn);
        chooser.AddState(withVineBranch, () => HasLivingVineBarriers());
        chooser.AddState(noVineBranch, () => true);
        _chooserState = chooser;

        var shatteredLifeFollowUp = new ConditionalBranchState("SHATTERED_FOLLOWUP");
        shatteredLifeFollowUp.AddState(emeraldBough, HasLivingVineBarriers);
        shatteredLifeFollowUp.AddState(chooser, () => true);

        summonVineBarriers.FollowUpState = chooser;
        emeraldBough.FollowUpState = chooser;
        delusionalVine.FollowUpState = chooser;
        grudgeVine.FollowUpState = chooser;
        extendedMalice.FollowUpState = chooser;
        shatteredLife.FollowUpState = shatteredLifeFollowUp;
        reviveAndEmpower.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                reviveAndEmpower,
                summonVineBarriers,
                emeraldBough,
                delusionalVine,
                grudgeVine,
                extendedMalice,
                shatteredLife,
                withVineBranch,
                noVineBranch,
                chooser,
                shatteredLifeFollowUp
            },
            chooser);
    }

    private bool HasLivingVineBarriers()
    {
        return CombatState?.Enemies.Any(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorVineBarrier) == true;
    }

    private bool CanContinueCombatMove()
    {
        return !Creature.IsDead
            && CombatManager.Instance.IsInProgress
            && CombatState != null;
    }

    private async Task SummonVineBarriersMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        _firstTurn = false;
        if (CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            await encounter.TrySpawnEmeraldBoughVineBarriers(CombatState);
        }
        AdvanceTurn();
    }

    private async Task EmeraldBoughMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await DamageCmd.Attack(EmeraldBoughDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        IReadOnlyList<Creature> liveTargets = AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
        foreach (Creature target in liveTargets)
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                EmeraldBoughRapidWearAmount,
                EmeraldBoughRapidWearTurns,
                Creature,
                null);
        }

        AdvanceTurn();
    }

    private async Task DelusionalVineMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(DelusionalVineHitADamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        if (Creature.IsDead) return;
        await DamageCmd.Attack(DelusionalVineHitBDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        if (Creature.IsDead) return;
        AttackCommand finalAttack = await DamageCmd.Attack(DelusionalVineHitCDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        IReadOnlyList<Creature> bindTargets = AttackCommandCompat.Results(finalAttack)
            .Where(static result => result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
        if (bindTargets.Count > 0)
        {
            await LibraryPowerCmd.Apply<LibraryBindingPower>(new ThrowingPlayerChoiceContext(), bindTargets, DelusionalVineBindAmount, 0, false, Creature, null);
        }

        AdvanceTurn();
    }

    private async Task GrudgeVineMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(GrudgeVineDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        if (!CanContinueCombatMove()) return;

        await DamageCmd.Attack(GrudgeVineDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        if (!CanContinueCombatMove()) return;

        IReadOnlyList<Creature> alivePlayers = CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        foreach (Creature target in alivePlayers)
        {
            if (!CanContinueCombatMove()) return;

            await CardPileCmdCompat.AddToCombatAndPreview<Wound>(target, PileType.Draw, GrudgeVineWoundCount, addedByPlayer: false, CardPilePosition.Random);
        }

        AdvanceTurn();
    }

    private async Task ExtendedMaliceMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Parry", 0.5f);
        await CreatureCmd.GainBlock(Creature, ExtendedMaliceBlock, ValueProp.Move, null);

        IReadOnlyList<Creature> alivePlayers = CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        if (alivePlayers.Count > 0)
        {
            await PowerCmdCompat.Apply<WeakPower>(alivePlayers, ExtendedMaliceWeak, Creature, null);
        }

        AdvanceTurn();
    }

    private async Task ShatteredLifeMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Ego", 0.6f);

        for (int i = 0; i < HistoryFloorEgoNumbers.ShatteredLifeHitCount; i++)
        {
            if (Creature.IsDead) return;
            await DamageCmd.Attack(ShatteredLifeDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", SegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        ResetStranglingVineCounts();
        _egoQueued = false;
        AdvanceTurn();
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private void ResetStranglingVineCounts()
    {
        if (CombatState == null)
        {
            return;
        }

        foreach (Creature enemy in CombatState.Enemies)
        {
            if (!enemy.IsAlive || enemy.Monster is not HistoryFloorVineBarrier)
            {
                continue;
            }

            EmeraldBoughStranglingVinePower? vine = enemy.GetPower<EmeraldBoughStranglingVinePower>();
            if (vine != null && vine.Amount > 0)
            {
                vine.SetAmount(0, silent: true);
            }
        }
    }

    private void AdvanceTurn()
    {
        _turnIndex++;
        _firstTurn = false;
    }

    private void StartBackgroundMoonTextLoop()
    {
        if (_backgroundMoonTextLoopStarted || Creature == null || Creature.IsDead)
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
        yield return new BuffIntent();
        yield return new SingleAttackIntent(EmeraldBoughDamage);
        yield return new BadgedDebuffIntent(
            IntentBadge.FromPower<LibraryVulnerablePower>(
                EmeraldBoughRapidWearAmount,
                downText: $"x{EmeraldBoughRapidWearTurns}"),
            EmeraldBoughRapidWearAmount);
        yield return new SingleAttackIntent(DelusionalVineHitADamage);
        yield return new SingleAttackIntent(DelusionalVineHitBDamage);
        yield return new SingleAttackIntent(DelusionalVineHitCDamage);
        yield return new DetailedBuffIntent<LibraryBindingPower>(DelusionalVineBindAmount, DetailedBuffTargetScope.Target);
        yield return new SingleAttackIntent(GrudgeVineDamage);
        yield return new DetailedStatusCardIntent<Wound>(GrudgeVineWoundCount, PileType.Draw, showSingleTargetMarker: false);
        yield return new DefendIntent();
        yield return new DetailedBuffIntent<WeakPower>(ExtendedMaliceWeak, DetailedBuffTargetScope.Target);
        yield return new PlayCardAttackIntent<ShatteredLifeEgoCard>(
            "SHATTERED_LIFE_EGO_CARD",
            () => ShatteredLifeDamage,
            static (card, damages) => { card.UpgradePreview(); card.SetPreviewDamage(damages[0]); });
        yield return new HealIntent();
    }
}
