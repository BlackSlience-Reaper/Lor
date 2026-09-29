using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.TechnologyFloorLiberation;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.ui;
using LibraryOfRuina.visuals.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.TechnologyFloorLiberation;

public sealed class TechnologyFloorMagicBulletBoss : LiberationPhaseBossMonster, IEnemyCardRuntimeOwner
{
    private const int Phase = 5;
    private const int MaxInternalPhase = 7;
    public const int HpLossOnTransition = 25;
    public const int PhaseTransitionStrengthGain = 1;
    public const int DeadlyPhaseTransitionStrengthGain = 2;
    private const int StaggerResistanceMax = 40;

    public override int DefaultChaoResistance => StaggerResistanceMax;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Vulnerable,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    private const string Phase1MoveId = "PHASE_1_PIERCE";
    private const string Phase2MoveId = "PHASE_2_PRECISION";
    private const string Phase3MoveId = "PHASE_3_SOUL_STEAL";
    private const string Phase4MoveId = "PHASE_4_CRUELTY";
    private const string Phase5MoveId = "PHASE_5_SILENCE";
    private const string Phase6MoveId = "PHASE_6_TORRENT";
    private const string Phase7MoveId = "PHASE_7_DESPAIR";
    private const string PierceCardId = "MAGIC_BULLET_PIERCE";
    private const string PrecisionCardId = "MAGIC_BULLET_PRECISION";
    private const string BaseBulletCardId = "MAGIC_BULLET_BASE";
    private const string SoulStealCardId = "MAGIC_BULLET_SOUL_STEAL";
    private const string CrueltyCardId = "MAGIC_BULLET_CRUELTY";
    private const string SilenceCardId = "MAGIC_BULLET_SILENCE";
    private const string TorrentCardId = "MAGIC_BULLET_TORRENT";
    private const string DespairCardId = "MAGIC_BULLET_DESPAIR";

    internal const string BackgroundTextScope = "technology_floor_liberation_phase_5";

    public const string Root = "res://images/monsters/technology_floor/magic_bullet/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string AttackTexturePath = Root + "attack.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string SpecialTexturePath = Root + "special.png";
    public const string AttackSfxPath = "res://audio/sfx/technology_floor/magic_bullet/attack.ogg";

    private int _internalPhase = 1;
    private bool _bypassClamp;
    private bool _internalPhaseTransitionPending;
    private LibraryCreatureResistanceData? _savedResistanceData;
    private EnemyCardRuntime? _enemyCards;
    private Dictionary<string, EnemyCardSpec>? _enemyCardSpecs;

    public override int LiberationPhase => Phase;

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    protected override string? ReviveAndEmpowerAnimation => "Hit";

    public bool IsBypassingStaggerClamp => _bypassClamp;

    public EnemyCardRuntime? EnemyCards => _enemyCards;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 495, 490);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 501, 598);

    private static int MagicBulletBaseDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    public static int InternalPhaseTransitionStrengthGain =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            DeadlyPhaseTransitionStrengthGain,
            PhaseTransitionStrengthGain);

    private static int PierceDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

    private static int PrecisionDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 12);

    private static int SoulStealDamageA =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private static int SoulStealDamageB =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 7);

    private static int CrueltyDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private static int SilenceDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12);

    private static int TorrentDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private decimal DespairDamage => MaxInitialHp * 0.3m;

    private const int TorrentHits = 3;
    private const int TorrentChaosHealPerHit = 5;

    public override IEnumerable<string> AssetPaths =>
        TechnologyFloorMagicBulletBossCreatureVisuals.Profile.AssetPaths
            .Concat(new[] { AttackSfxPath })
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _internalPhase = 1;
        _bypassClamp = false;
        _internalPhaseTransitionPending = false;
        _savedResistanceData = null;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletBaseEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletPierceEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletPrecisionEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletSoulStealEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletCrueltyEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletSilenceEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletTorrentEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<MagicBulletDespairEgoCard>());

        _enemyCards = CreateEnemyCardRuntime();
        _enemyCards.RefreshDefaultPlan();
        Log.Info("[LibraryOfRuina.EnemyCards] Magic Bullet enemy card runtime ready: phase="
                 + _internalPhase
                 + " cards="
                 + string.Join(",", _enemyCards.CurrentPlan.Select(static card => card.Id)));

        if (CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<TechnologyFloorErosionPower>(
            Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<TechnologyFloorMagicBulletPower>(
            Creature, 1m, Creature, null, silent: true);

        AdjustIntentSeparation();
    }

    public override Task BeforeCombatStart()
    {
        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _enemyCards = null;
        _enemyCardSpecs = null;
        ClearReviveAndEmpowerState();
        _savedResistanceData = null;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not TechnologyFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    public Task TriggerInternalPhaseTransition()
    {
        if (Creature.IsDead || _internalPhaseTransitionPending)
        {
            return Task.CompletedTask;
        }

        _internalPhaseTransitionPending = true;
        return Task.CompletedTask;
    }

    private async Task ApplyPendingInternalPhaseTransition()
    {
        decimal newHp = Math.Max(1m, Creature.CurrentHp - HpLossOnTransition);
        await CreatureCmd.SetCurrentHp(Creature, newHp);

        if (Creature is LibraryCreature lc)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(lc, StaggerResistanceMax);
        }

        RestoreNormalResistance();
        await ClearNegativeStrengthAndGainTransitionStrength();
        _internalPhase = (_internalPhase % MaxInternalPhase) + 1;

        if (_internalPhase == 7)
        {
            SetFatalResistance();
        }

        _enemyCards?.RefreshDefaultPlan();
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        creatureNode?.RefreshIntents();
    }

    protected override async Task AfterSideTurnEndInternal(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy
            && _internalPhaseTransitionPending
            && !Creature.IsDead)
        {
            _internalPhaseTransitionPending = false;
            await ApplyPendingInternalPhaseTransition();
        }

        await base.AfterSideTurnEndInternal(choiceContext, side, participants);
    }

    private async Task ClearNegativeStrengthAndGainTransitionStrength()
    {
        StrengthPower? strength = Creature.GetPower<StrengthPower>();
        if (strength is { Amount: < 0 })
        {
            await PowerCmdCompat.ModifyAmount(strength, -strength.Amount, Creature, null, silent: true);
        }

        await PowerCmdCompat.Apply<StrengthPower>(
            Creature,
            InternalPhaseTransitionStrengthGain,
            Creature,
            null);
    }

    private void SetFatalResistance()
    {
        if (Creature is LibraryCreature lc)
        {
            _savedResistanceData = new LibraryCreatureResistanceData
            {
                ChaosResistance =
                {
                    Slash = lc.ResistanceData.ChaosResistance.Slash,
                    Pierce = lc.ResistanceData.ChaosResistance.Pierce,
                    Blunt = lc.ResistanceData.ChaosResistance.Blunt
                }
            };

            lc.ResistanceData.ChaosResistance.Slash = LibraryResistanceLevel.Fatal;
            lc.ResistanceData.ChaosResistance.Pierce = LibraryResistanceLevel.Fatal;
            lc.ResistanceData.ChaosResistance.Blunt = LibraryResistanceLevel.Fatal;
        }
    }

    private void RestoreNormalResistance()
    {
        if (_savedResistanceData == null)
        {
            return;
        }

        if (Creature is LibraryCreature lc)
        {
            lc.ResistanceData.ChaosResistance.Slash = _savedResistanceData.ChaosResistance.Slash;
            lc.ResistanceData.ChaosResistance.Pierce = _savedResistanceData.ChaosResistance.Pierce;
            lc.ResistanceData.ChaosResistance.Blunt = _savedResistanceData.ChaosResistance.Blunt;
        }

        _savedResistanceData = null;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var phase1 = new MoveState(
            Phase1MoveId,
            Phase1PierceMove,
            CreatePhaseIntents(1));

        var phase2 = new MoveState(
            Phase2MoveId,
            Phase2PrecisionMove,
            CreatePhaseIntents(2));

        var phase3 = new MoveState(
            Phase3MoveId,
            Phase3SoulStealMove,
            CreatePhaseIntents(3));

        var phase4 = new MoveState(
            Phase4MoveId,
            Phase4CrueltyMove,
            CreatePhaseIntents(4));

        var phase5 = new MoveState(
            Phase5MoveId,
            Phase5SilenceMove,
            CreatePhaseIntents(5));

        var phase6 = new MoveState(
            Phase6MoveId,
            Phase6TorrentMove,
            CreatePhaseIntents(6));

        var phase7 = new MoveState(
            Phase7MoveId,
            Phase7DespairMove,
            CreatePhaseIntents(7));

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(phase7, () => _internalPhase == 7);
        chooser.AddState(phase1, () => _internalPhase == 1);
        chooser.AddState(phase2, () => _internalPhase == 2);
        chooser.AddState(phase3, () => _internalPhase == 3);
        chooser.AddState(phase4, () => _internalPhase == 4);
        chooser.AddState(phase5, () => _internalPhase == 5);
        chooser.AddState(phase6, () => _internalPhase == 6);
        chooser.AddState(phase1, () => true);

        phase1.FollowUpState = chooser;
        phase2.FollowUpState = chooser;
        phase3.FollowUpState = chooser;
        phase4.FollowUpState = chooser;
        phase5.FollowUpState = chooser;
        phase6.FollowUpState = chooser;
        phase7.FollowUpState = chooser;
        reviveAndEmpower.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[]
            {
                reviveAndEmpower, phase1, phase2, phase3, phase4, phase5, phase6, phase7, chooser
            },
            chooser);
    }

    private async Task Phase1PierceMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteEnemyCardPlan(targets);
    }

    private async Task Phase2PrecisionMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteEnemyCardPlan(targets);
    }

    private async Task Phase3SoulStealMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteEnemyCardPlan(targets);
    }

    private async Task Phase4CrueltyMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteEnemyCardPlan(targets);
    }

    private async Task Phase5SilenceMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteEnemyCardPlan(targets);
    }

    private async Task Phase6TorrentMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteEnemyCardPlan(targets);
    }

    private async Task Phase7DespairMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteEnemyCardPlan(targets);
    }

    private async Task PlayPierceCard(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment(PierceDamage);
    }

    private async Task PlayBaseBulletCard(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment(MagicBulletBaseDamage);
    }

    private async Task PlayPrecisionCard(IReadOnlyList<Creature> targets)
    {
        AttackCommand? precisionResult = await ExecuteAttackSegment(PrecisionDamage);
        foreach (Creature target in targets)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaParalysisPower>(
                target, 3m, Creature, null);
        }
    }

    private async Task PlaySoulStealCard(IReadOnlyList<Creature> targets)
    {
        AttackCommand? hit1 = await ExecuteAttackSegment(SoulStealDamageA);
        foreach (Creature target in targets)
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(target, 5m, Creature, null);
            await PowerCmdCompat.Apply<WeakPower>(target, 1m, Creature, null);
        }

        AttackCommand? hit2 = await ExecuteAttackSegment(SoulStealDamageB);
        foreach (Creature target in targets)
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(target, 5m, Creature, null);
            await PowerCmdCompat.Apply<WeakPower>(target, 1m, Creature, null);
        }
    }

    private async Task PlayCrueltyCard(IReadOnlyList<Creature> targets)
    {
        AttackCommand? crueltyResult = await ExecuteAttackSegment(CrueltyDamage);
        foreach (Creature target in targets)
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target, 5, turns: 1, Creature, null);
        }
    }

    private async Task PlaySilenceCard(IReadOnlyList<Creature> targets)
    {
        AttackCommand? silenceResult = await ExecuteAttackSegment(SilenceDamage);
        foreach (Creature target in targets)
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(target, 10m, Creature, null);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(target, 3, 1, Creature, null);
            await PowerCmdCompat.Apply<WeakPower>(target, 3m, Creature, null);
        }
    }

    private async Task PlayTorrentCard(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < TorrentHits; i++)
        {
            AttackCommand? hitResult = await ExecuteAttackSegment(TorrentDamage);
            if (GetUnblockedPlayerHitTargets(hitResult).Count > 0)
            {
                if (Creature is LibraryCreature lc
                    && lc.CurrentChaoValue < lc.MaxChaoValue)
                {
                    int newChao = Math.Min(lc.MaxChaoValue, lc.CurrentChaoValue + TorrentChaosHealPerHit);
                    await LibraryCreatureCmd.SetCurrentChaoValue(lc, newChao);
                }
            }
        }
    }

    private async Task PlayDespairCard(IReadOnlyList<Creature> targets)
    {
        RestoreNormalResistance();
        _internalPhase = 1;

        await CreatureCmd.TriggerAnim(Creature, "Special", 3.0f);
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);

        await CreatureCmdCompat.Damage(new ThrowingPlayerChoiceContext(), Creature, DespairDamage,
            ValueProp.Unpowered | ValueProp.Unblockable, Creature, null);

        if (Creature is LibraryCreature lc)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(lc, StaggerResistanceMax);
        }

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        creatureNode?.RefreshIntents();
    }

    private async Task ExecuteEnemyCardPlan(IReadOnlyList<Creature> targets)
    {
        EnemyCardRuntime runtime = _enemyCards ??= CreateEnemyCardRuntime();
        runtime.RefreshDefaultPlan();
        await runtime.PlayPlannedHandLikeDownfall(targets);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player && !Creature.IsDead)
        {
            (_enemyCards ??= CreateEnemyCardRuntime()).RefreshDefaultPlan();
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    private EnemyCardRuntime CreateEnemyCardRuntime()
    {
        var runtime = new EnemyCardRuntime(this, 0, GetCurrentPhasePlan);
        runtime.InitializeDeck(AllEnemyCardSpecs());
        return runtime;
    }

    private IReadOnlyList<EnemyCardSpec> GetCurrentPhasePlan()
    {
        return GetPhasePlan(_internalPhase);
    }

    private AbstractIntent[] CreatePhaseIntents(int phase)
    {
        return GetPhasePlan(phase)
            .SelectMany(CreateIntentSequenceForCard)
            .ToArray();
    }

    public IReadOnlyList<AbstractIntent> CreateIntentSequenceForCard(EnemyCardSpec card)
    {
        AbstractIntent primaryIntent = card.CreateIntentInstance();
        return card.Id == SoulStealCardId
            ? [primaryIntent, new SingleAttackIntent(() => SoulStealDamageB)]
            : [primaryIntent];
    }

    private IReadOnlyList<EnemyCardSpec> GetPhasePlan(int phase)
    {
        Dictionary<string, EnemyCardSpec> specs = EnemyCardSpecs;
        return phase switch
        {
            1 => [specs[PierceCardId], specs[PierceCardId], specs[PierceCardId]],
            2 => [specs[PrecisionCardId], specs[BaseBulletCardId], specs[BaseBulletCardId]],
            3 => [specs[SoulStealCardId]],
            4 => [specs[CrueltyCardId], specs[BaseBulletCardId], specs[BaseBulletCardId]],
            5 => [specs[SilenceCardId]],
            6 => [specs[TorrentCardId]],
            7 => [specs[DespairCardId]],
            _ => [specs[PierceCardId], specs[PierceCardId], specs[PierceCardId]]
        };
    }

    private IReadOnlyList<EnemyCardSpec> AllEnemyCardSpecs()
    {
        return EnemyCardSpecs.Values.ToArray();
    }

    private Dictionary<string, EnemyCardSpec> EnemyCardSpecs =>
        _enemyCardSpecs ??= CreateEnemyCardSpecs();

    private Dictionary<string, EnemyCardSpec> CreateEnemyCardSpecs()
    {
        return new Dictionary<string, EnemyCardSpec>
        {
            [PierceCardId] = new(
                PierceCardId,
                CreatePierceDisplayCard,
                cost: 1,
                priority: 10,
                execute: (_, targets) => PlayPierceCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => PierceDamage)),
            [PrecisionCardId] = new(
                PrecisionCardId,
                CreatePrecisionDisplayCard,
                cost: 1,
                priority: 20,
                execute: (_, targets) => PlayPrecisionCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => PrecisionDamage)),
            [BaseBulletCardId] = new(
                BaseBulletCardId,
                CreateBaseBulletDisplayCard,
                cost: 1,
                priority: 30,
                execute: (_, targets) => PlayBaseBulletCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => MagicBulletBaseDamage)),
            [SoulStealCardId] = new(
                SoulStealCardId,
                CreateSoulStealDisplayCard,
                cost: 2,
                priority: 40,
                execute: (_, targets) => PlaySoulStealCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(
                    spec,
                    () => SoulStealDamageA,
                    additionalDamageCalcs: [() => SoulStealDamageB])),
            [CrueltyCardId] = new(
                CrueltyCardId,
                CreateCrueltyDisplayCard,
                cost: 2,
                priority: 50,
                execute: (_, targets) => PlayCrueltyCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => CrueltyDamage)),
            [SilenceCardId] = new(
                SilenceCardId,
                CreateSilenceDisplayCard,
                cost: 2,
                priority: 60,
                execute: (_, targets) => PlaySilenceCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => SilenceDamage)),
            [TorrentCardId] = new(
                TorrentCardId,
                CreateTorrentDisplayCard,
                cost: 1,
                priority: 70,
                execute: (_, targets) => PlayTorrentCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => TorrentDamage, () => TorrentHits)),
            [DespairCardId] = new(
                DespairCardId,
                CreateDespairDisplayCard,
                cost: 1,
                priority: 80,
                execute: (_, targets) => PlayDespairCard(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => DespairDamage))
        };
    }

    private static CardModel CreatePierceDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletPierceEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(PierceDamage); });
    }

    private static CardModel CreatePrecisionDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletPrecisionEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(PrecisionDamage); });
    }

    private static CardModel CreateBaseBulletDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletBaseEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(MagicBulletBaseDamage); });
    }

    private static CardModel CreateSoulStealDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletSoulStealEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(SoulStealDamageA, SoulStealDamageB); });
    }

    private static CardModel CreateCrueltyDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletCrueltyEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(CrueltyDamage); });
    }

    private static CardModel CreateSilenceDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletSilenceEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(SilenceDamage); });
    }

    private static CardModel CreateTorrentDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletTorrentEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(TorrentDamage); });
    }

    private CardModel CreateDespairDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<MagicBulletDespairEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage((int) DespairDamage); });
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private Task<AttackCommand> ExecuteAttackSegment(int damage)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_slash")
            .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .Execute(null);
    }

    private static IReadOnlyList<Creature> GetUnblockedPlayerHitTargets(AttackCommand? attack)
    {
        if (attack == null)
        {
            return Array.Empty<Creature>();
        }

        return AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive && result.Receiver.IsPlayer && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
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
        foreach (EnemyCardSpec spec in AllEnemyCardSpecs())
        {
            foreach (AbstractIntent intent in CreateIntentSequenceForCard(spec))
            {
                yield return intent;
            }
        }

        yield return new HealIntent();
        yield return new BuffIntent();
    }

    private void AdjustIntentSeparation()
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode == null) return;

        if (creatureNode.IntentContainer is HBoxContainer intentContainer)
        {
            intentContainer.AddThemeConstantOverride("separation", 55);
            intentContainer.Alignment = BoxContainer.AlignmentMode.Begin;
        }
    }
}
