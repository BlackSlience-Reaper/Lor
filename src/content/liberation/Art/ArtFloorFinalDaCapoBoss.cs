using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using VoidCard = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace LibraryOfRuina.content.liberation.Art;

internal enum ArtFloorFinalDaCapoMovement
{
    First,
    Second,
    Third,
    Fourth,
    Fifth,
    Sixth
}

internal enum ArtFloorFinalDaCapoSupportCard
{
    Moderato,
    PrestoPassionato,
    AdagioCantabile
}

public sealed class ArtFloorFinalDaCapoBoss : LiberationPhaseBossMonster
{
    private const int Phase = 6;

    private const string RouterStateId = "FINAL_DA_CAPO_ROUTER";
    private const string FirstMovementMoveId = "FIRST_MOVEMENT";
    private const string SecondMovementMoveId = "SECOND_MOVEMENT";
    private const string ThirdMovementMoveId = "THIRD_MOVEMENT";
    private const string FourthMovementMoveId = "FOURTH_MOVEMENT";
    private const string FinaleMoveId = "FINALE";
    private const string CurtainCallMoveId = "CURTAIN_CALL";

    private const string ModeratoDescriptionKey = "ART_FLOOR_FINAL_DA_CAPO_BOSS.special.moderato.description";
    private const string PrestoDescriptionKey = "ART_FLOOR_FINAL_DA_CAPO_BOSS.special.presto_passionato.description";
    private const string AdagioDescriptionKey = "ART_FLOOR_FINAL_DA_CAPO_BOSS.special.adagio_cantabile.description";

    private const int StaggerResistanceMax = 100;
    private const int ModeratoHits = 3;
    private const int ModeratoWeak = 2;
    private const int PrestoImbalanced = 1;
    private const int AdagioBlock = 33;
    private const int AdagioHeal = 6;

    private const int FirstMovementDamageA0 = 12;
    private const int FirstMovementDamageHighAsc = 13;
    private const int SecondMovementHits = 2;
    private const int SecondMovementDamageA0 = 12;
    private const int SecondMovementDamageHighAsc = 13;
    private const int ThirdMovementFrail = 3;
    private const int ThirdMovementWeak = 3;
    private const int ThirdMovementDamageA0 = 9;
    private const int ThirdMovementDamageHighAsc = 10;
    private const int FourthMovementHits = 3;
    private const int FourthMovementDamageA0 = 8;
    private const int FourthMovementDamageHighAsc = 9;
    private const int FourthMovementVoidCount = 2;
    private const int FinaleDamage = 35;
    private const int FinaleConfusion = 1;
    private const int CurtainCallBlock = 99;
    private const int CurtainCallStrength = 3;
    private const decimal CurtainCallHealPercent = 0.20m;
    private const float SegmentDelaySeconds = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;

    private Dictionary<string, MoveState> _movementStates = [];

    private ConditionalBranchState? _routerState;

    private ArtFloorFinalDaCapoMovement _currentMovement = ArtFloorFinalDaCapoMovement.First;
    private string? _currentStateKey;
    private IReadOnlyList<ArtFloorFinalDaCapoSupportCard> _currentSupportCards = Array.Empty<ArtFloorFinalDaCapoSupportCard>();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _movementStates = [];
        _routerState = null;
        ClearReviveAndEmpowerState();
        _currentSupportCards = [.. _currentSupportCards];
    }

    public override int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override float HpBarSizeReduction => 20f;

    public override int DefaultChaoResistance => StaggerResistanceMax;
    
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 497, 494);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 500, 496);

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => ResistanceForMovement(ArtFloorFinalDaCapoMovement.First);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => ResistanceForMovement(ArtFloorFinalDaCapoMovement.First);

    protected override string VisualsPath => SceneHelper.GetScenePath("creature_visuals/art_floor_final_da_capo_boss");

    public override IEnumerable<string> AssetPaths =>
        ArtFloorDaCapoCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Append(ImageHelper.GetImagePath("powers/art_floor_green_passive_power.png"))
            .Append(ImageHelper.GetImagePath("powers/fanatic_worship_power.png"))
            .Append(ImageHelper.GetImagePath("powers/art_floor_final_da_capo_cycle_power.png"))
            .Append(ImageHelper.GetImagePath("powers/art_floor_final_da_capo_aria_power.png"))
            .Append(ImageHelper.GetImagePath("powers/art_floor_adagio_cantabile_power.png"))
            .Append(ImageHelper.GetImagePath("powers/art_floor_imbalanced_power.png"))
            .Append(ImageHelper.GetImagePath("powers/art_floor_da_capo_soul_binding_power.png"))
            .Distinct();

    private int ModeratoDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private int PrestoDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private int FirstMovementDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, FirstMovementDamageHighAsc, FirstMovementDamageA0);

    private int SecondMovementDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, SecondMovementDamageHighAsc, SecondMovementDamageA0);

    private int ThirdMovementDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, ThirdMovementDamageHighAsc, ThirdMovementDamageA0);

    private int FourthMovementDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, FourthMovementDamageHighAsc, FourthMovementDamageA0);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        if (_currentStateKey == null)
        {
            SelectMovementStateForCurrentMovement();
        }

        SyncPerformerHiddenIntents();

        if (CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<ArtFloorFinalDaCapoCyclePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorFinalDaCapoAriaPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorEnsemblePower>(Creature, 1m, Creature, null, silent: true);
        await ApplyMovementResistance(new ThrowingPlayerChoiceContext(), _currentMovement);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not ArtFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _movementStates.Clear();
        _currentMovement = ArtFloorFinalDaCapoMovement.First;
        _currentStateKey = null;
        _currentSupportCards = Array.Empty<ArtFloorFinalDaCapoSupportCard>();

        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        CreateMovementStates(ArtFloorFinalDaCapoMovement.First, 1, FirstMovementMoveId, FirstMovementMove);
        CreateMovementStates(ArtFloorFinalDaCapoMovement.Second, 2, SecondMovementMoveId, SecondMovementMove);
        CreateMovementStates(ArtFloorFinalDaCapoMovement.Third, 2, ThirdMovementMoveId, ThirdMovementMove);
        CreateMovementStates(ArtFloorFinalDaCapoMovement.Fourth, 3, FourthMovementMoveId, FourthMovementMove);
        CreateMovementStates(ArtFloorFinalDaCapoMovement.Fifth, 0, FinaleMoveId, FinaleMove);
        CreateMovementStates(ArtFloorFinalDaCapoMovement.Sixth, 0, CurtainCallMoveId, CurtainCallMove);

        _routerState = new ConditionalBranchState(RouterStateId);
        foreach (KeyValuePair<string, MoveState> entry in _movementStates)
        {
            string stateKey = entry.Key;
            MoveState state = entry.Value;
            _routerState.AddState(state, () => string.Equals(_currentStateKey, stateKey, StringComparison.Ordinal));
            state.FollowUpState = _routerState;
        }

        reviveAndEmpower.FollowUpState = _routerState;
        SelectMovementStateForCurrentMovement();

        List<MonsterState> states =
        [
            reviveAndEmpower,
            _routerState
        ];
        states.AddRange(_movementStates.Values);

        return new MonsterMoveStateMachine(states, _routerState);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Enemy && OwnerIsCurrentPhaseBoss())
        {
            SyncPerformerHiddenIntents();
        }

        return base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    internal async Task AdvanceMovement(PlayerChoiceContext choiceContext)
    {
        if (!CanAdvanceMovement())
        {
            return;
        }

        _currentMovement = _currentMovement switch
        {
            ArtFloorFinalDaCapoMovement.First => ArtFloorFinalDaCapoMovement.Second,
            ArtFloorFinalDaCapoMovement.Second => ArtFloorFinalDaCapoMovement.Third,
            ArtFloorFinalDaCapoMovement.Third => ArtFloorFinalDaCapoMovement.Fourth,
            ArtFloorFinalDaCapoMovement.Fourth => ArtFloorFinalDaCapoMovement.Fifth,
            ArtFloorFinalDaCapoMovement.Fifth => ArtFloorFinalDaCapoMovement.Sixth,
            _ => ArtFloorFinalDaCapoMovement.First
        };

        SelectMovementStateForCurrentMovement();
        SyncPerformerHiddenIntents();
        await ApplyMovementResistance(choiceContext, _currentMovement);

        if (CanAdvanceMovement() && ResolveCurrentMoveState() is { } moveState)
        {
            SetMoveImmediate(moveState, forceTransition: true);
            await RefreshNodeIntents();
        }
    }

    internal async Task PerformSupportMoves(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        foreach (ArtFloorFinalDaCapoSupportCard supportCard in supportCards)
        {
            if (!HasLivingPlayerTargets())
            {
                Log.Warn("[LibraryOfRuina.FinalDaCapo] skipped support move because no living player targets were available.");
                return;
            }

            switch (supportCard)
            {
                case ArtFloorFinalDaCapoSupportCard.Moderato:
                    await PerformModerato(choiceContext);
                    break;
                case ArtFloorFinalDaCapoSupportCard.PrestoPassionato:
                    await PerformPrestoPassionato(choiceContext);
                    break;
                case ArtFloorFinalDaCapoSupportCard.AdagioCantabile:
                    await PerformAdagioCantabile(choiceContext);
                    break;
            }
        }
    }

    private void CreateMovementStates(
        ArtFloorFinalDaCapoMovement movement,
        int supportCount,
        string stateIdPrefix,
        Func<IReadOnlyList<Creature>, IReadOnlyList<ArtFloorFinalDaCapoSupportCard>, Task> onPerform)
    {
        if (supportCount == 0)
        {
            IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards =
                Array.Empty<ArtFloorFinalDaCapoSupportCard>();
            string key = BuildStateKey(movement, supportCards);
            _movementStates[key] = new MoveState(
                stateIdPrefix,
                targets => onPerform(targets, supportCards),
                CreateMovementIntents(movement, supportCards));
            return;
        }

        int index = 0;
        foreach (ArtFloorFinalDaCapoSupportCard[] supportCards in EnumerateSupportSequences(supportCount))
        {
            index++;
            IReadOnlyList<ArtFloorFinalDaCapoSupportCard> boundSupportCards = supportCards.ToArray();
            string key = BuildStateKey(movement, supportCards);
            _movementStates[key] = new MoveState(
                $"{stateIdPrefix}_{index}",
                targets => onPerform(targets, boundSupportCards),
                CreateMovementIntents(movement, boundSupportCards));
        }
    }

    private async Task FirstMovementMove(
        IReadOnlyList<Creature> targets,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        await ExecuteGroupAttack(FirstMovementDamage, "Attack");
        await PerformSupportMoves(new ThrowingPlayerChoiceContext(), supportCards);
        await AdvanceMovement(new ThrowingPlayerChoiceContext());
    }

    private async Task SecondMovementMove(
        IReadOnlyList<Creature> targets,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        for (int i = 0; i < SecondMovementHits; i++)
        {
            AttackCommand attack = await ExecuteGroupAttack(SecondMovementDamage, "Attack");
            foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
            {
                await PowerCmdCompat.Apply<FanaticWorshipPower>(target, 1m, Creature, null);
            }
        }

        await PerformSupportMoves(new ThrowingPlayerChoiceContext(), supportCards);
        await AdvanceMovement(new ThrowingPlayerChoiceContext());
    }

    private async Task ThirdMovementMove(
        IReadOnlyList<Creature> targets,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        AttackCommand attack = await ExecuteGroupAttack(ThirdMovementDamage, "Attack");
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            await PowerCmdCompat.Apply<FrailPower>(target, ThirdMovementFrail, Creature, null);
            await PowerCmdCompat.Apply<WeakPower>(target, ThirdMovementWeak, Creature, null);
        }

        await PerformSupportMoves(new ThrowingPlayerChoiceContext(), supportCards);
        await AdvanceMovement(new ThrowingPlayerChoiceContext());
    }

    private async Task FourthMovementMove(
        IReadOnlyList<Creature> targets,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        for (int i = 0; i < FourthMovementHits; i++)
        {
            await ExecuteGroupAttack(FourthMovementDamage, "Attack");
        }

        foreach (Creature player in CombatState.PlayerCreatures.Where(static player => player.IsAlive))
        {
            await CardPileCmdCompat.AddToCombatAndPreview<VoidCard>(
                player,
                PileType.Draw,
                FourthMovementVoidCount,
                addedByPlayer: false);
        }

        await PerformSupportMoves(new ThrowingPlayerChoiceContext(), supportCards);
        await AdvanceMovement(new ThrowingPlayerChoiceContext());
    }

    private async Task FinaleMove(
        IReadOnlyList<Creature> targets,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        AttackCommand attack = await ExecuteGroupAttack(FinaleDamage, "Special");
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
                target,
                FinaleConfusion,
                Creature,
                null);
        }

        await AdvanceMovement(new ThrowingPlayerChoiceContext());
    }

    private async Task CurtainCallMove(
        IReadOnlyList<Creature> targets,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
        await CreatureCmd.GainBlock(Creature, CurtainCallBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, CurtainCallStrength, Creature, null);

        foreach (Creature ally in CombatState.Enemies.Where(enemy => enemy != Creature && enemy.IsAlive))
        {
            int healAmount = Math.Max(1, (int)Math.Ceiling(ally.MaxHp * CurtainCallHealPercent));
            await CreatureCmd.Heal(ally, healAmount);
        }

        await AdvanceMovement(new ThrowingPlayerChoiceContext());
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is ArtFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private async Task PerformModerato(PlayerChoiceContext choiceContext)
    {
        for (int i = 0; i < ModeratoHits; i++)
        {
            AttackCommand attack = await ExecuteGroupAttack(ModeratoDamage, "Attack");
            foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
            {
                await PowerCmdCompat.Apply<WeakPower>(choiceContext, target, ModeratoWeak, Creature, null);
            }
        }
    }

    private async Task PerformPrestoPassionato(PlayerChoiceContext choiceContext)
    {
        AttackCommand attack = await ExecuteGroupAttack(PrestoDamage, "Attack");
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            await PowerCmdCompat.ApplyDebuff<ArtFloorImbalancedPower>(
                choiceContext,
                target,
                PrestoImbalanced,
                Creature,
                null);
        }
    }

    private async Task PerformAdagioCantabile(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
        await CreatureCmd.GainBlock(Creature, AdagioBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<ArtFloorAdagioCantabilePower>(Creature, 1m, Creature, null, silent: true);
    }

    private async Task<AttackCommand> ExecuteGroupAttack(int damage, string anim)
    {
        CombatStateLike? combatState = Creature.CombatState;
        if (combatState == null)
        {
            Log.Warn("[LibraryOfRuina.FinalDaCapo] skipped group attack because combat state is null.");
            throw new InvalidOperationException("Final Da Capo group attack cannot execute without a combat state.");
        }

        Creature[] players = combatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        if (players.Length == 0)
        {
            Log.Warn("[LibraryOfRuina.FinalDaCapo] skipped group attack because no living player targets were available.");
            throw new InvalidOperationException("Final Da Capo group attack cannot execute without living player targets.");
        }

        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, damage, players);

        return await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(anim, SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_blunt")
            .WithIndiscriminateBlockBreak(this, damage, players)
            .Execute(null);
    }

    private static Creature[] GetUnblockedPlayerHitTargets(AttackCommand attack)
    {
        return AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive && result.Receiver.IsPlayer && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
    }

    private bool HasLivingPlayerTargets()
    {
        return Creature.CombatState?.PlayerCreatures.Any(static player => player.IsAlive) == true;
    }

    private async Task ApplyMovementResistance(PlayerChoiceContext choiceContext, ArtFloorFinalDaCapoMovement movement)
    {
        if (Creature is not LibraryCreature creature)
        {
            return;
        }

        LibraryCreatureResistanceData.Resistance resistance = ResistanceForMovement(movement);
        await LibraryCreatureCmd.SetPhysicalResistance(choiceContext, creature, Creature, LibraryDamageType.Blunt, resistance.Blunt);
        await LibraryCreatureCmd.SetPhysicalResistance(choiceContext, creature, Creature, LibraryDamageType.Slash, resistance.Slash);
        await LibraryCreatureCmd.SetPhysicalResistance(choiceContext, creature, Creature, LibraryDamageType.Pierce, resistance.Pierce);
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, creature, Creature, LibraryDamageType.Blunt, resistance.Blunt);
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, creature, Creature, LibraryDamageType.Slash, resistance.Slash);
        await LibraryCreatureCmd.SetChaoResistance(choiceContext, creature, Creature, LibraryDamageType.Pierce, resistance.Pierce);
        creature.HealthBar?.RefreshValues();
    }

    private static LibraryCreatureResistanceData.Resistance ResistanceForMovement(ArtFloorFinalDaCapoMovement movement)
    {
        return movement switch
        {
            ArtFloorFinalDaCapoMovement.First => new LibraryCreatureResistanceData.Resistance
            {
                Slash = LibraryResistanceLevel.Immune,
                Pierce = LibraryResistanceLevel.Immune,
                Blunt = LibraryResistanceLevel.Normal
            },
            ArtFloorFinalDaCapoMovement.Second => new LibraryCreatureResistanceData.Resistance
            {
                Slash = LibraryResistanceLevel.Normal,
                Pierce = LibraryResistanceLevel.Immune,
                Blunt = LibraryResistanceLevel.Immune
            },
            ArtFloorFinalDaCapoMovement.Third => new LibraryCreatureResistanceData.Resistance
            {
                Slash = LibraryResistanceLevel.Immune,
                Pierce = LibraryResistanceLevel.Normal,
                Blunt = LibraryResistanceLevel.Immune
            },
            ArtFloorFinalDaCapoMovement.Fourth => new LibraryCreatureResistanceData.Resistance
            {
                Slash = LibraryResistanceLevel.Normal,
                Pierce = LibraryResistanceLevel.Normal,
                Blunt = LibraryResistanceLevel.Normal
            },
            ArtFloorFinalDaCapoMovement.Fifth => new LibraryCreatureResistanceData.Resistance
            {
                Slash = LibraryResistanceLevel.Immune,
                Pierce = LibraryResistanceLevel.Immune,
                Blunt = LibraryResistanceLevel.Immune
            },
            _ => new LibraryCreatureResistanceData.Resistance
            {
                Slash = LibraryResistanceLevel.Fatal,
                Pierce = LibraryResistanceLevel.Fatal,
                Blunt = LibraryResistanceLevel.Fatal
            }
        };
    }

    private AbstractIntent[] CreateMovementIntents(
        ArtFloorFinalDaCapoMovement movement,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        List<AbstractIntent> intents = movement switch
        {
            ArtFloorFinalDaCapoMovement.First =>
            [
                new IndiscriminateAttackIntent(() => FirstMovementDamage, null, null)
            ],
            ArtFloorFinalDaCapoMovement.Second =>
            [
                new CombinedAttackDebuffIntent(
                    () => SecondMovementDamage,
                    () => SecondMovementHits,
                    null,
                    IntentBadge.FromPower<FanaticWorshipPower>())
            ],
            ArtFloorFinalDaCapoMovement.Third =>
            [
                new CombinedAttackDebuffIntent(
                    () => ThirdMovementDamage,
                    null,
                    null,
                    IntentBadge.Frail(ThirdMovementFrail),
                    IntentBadge.Weak(ThirdMovementWeak))
            ],
            ArtFloorFinalDaCapoMovement.Fourth =>
            [
                new CombinedAttackDebuffIntent(
                    () => FourthMovementDamage,
                    () => FourthMovementHits,
                    null,
                    IntentBadge.StatusCard<VoidCard>(FourthMovementVoidCount))
            ],
            ArtFloorFinalDaCapoMovement.Fifth =>
            [
                new CombinedAttackDebuffIntent(
                    () => FinaleDamage,
                    null,
                    null,
                    IntentBadge.Confusion(FinaleConfusion))
            ],
            _ =>
            [
                new CombinedDefendBuffIntent(CurtainCallBlock, null, IntentBadge.Strength(CurtainCallStrength))
            ]
        };

        foreach (ArtFloorFinalDaCapoSupportCard supportCard in supportCards)
        {
            intents.Add(CreateSupportIntent(supportCard));
        }

        return [.. intents];
    }

    private static AbstractIntent CreateSupportIntent(ArtFloorFinalDaCapoSupportCard supportCard)
    {
        return supportCard switch
        {
            ArtFloorFinalDaCapoSupportCard.Moderato => new CombinedAttackDebuffIntent(
                () => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6),
                () => ModeratoHits,
                ModeratoDescriptionKey,
                IntentBadge.Weak(ModeratoWeak)),
            ArtFloorFinalDaCapoSupportCard.PrestoPassionato => new CombinedAttackDebuffIntent(
                () => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6),
                null,
                PrestoDescriptionKey,
                IntentBadge.FromPower<ArtFloorImbalancedPower>(PrestoImbalanced)),
            _ => new CombinedDefendBuffIntent(AdagioBlock, AdagioDescriptionKey)
        };
    }

    private static IEnumerable<ArtFloorFinalDaCapoSupportCard[]> EnumerateSupportSequences(int count)
    {
        ArtFloorFinalDaCapoSupportCard[] allCards =
        [
            ArtFloorFinalDaCapoSupportCard.Moderato,
            ArtFloorFinalDaCapoSupportCard.PrestoPassionato,
            ArtFloorFinalDaCapoSupportCard.AdagioCantabile
        ];

        IEnumerable<ArtFloorFinalDaCapoSupportCard[]> Expand(int remaining)
        {
            if (remaining == 0)
            {
                yield return Array.Empty<ArtFloorFinalDaCapoSupportCard>();
                yield break;
            }

            foreach (ArtFloorFinalDaCapoSupportCard card in allCards)
            {
                foreach (ArtFloorFinalDaCapoSupportCard[] tail in Expand(remaining - 1))
                {
                    ArtFloorFinalDaCapoSupportCard[] result = new ArtFloorFinalDaCapoSupportCard[tail.Length + 1];
                    result[0] = card;
                    Array.Copy(tail, 0, result, 1, tail.Length);
                    yield return result;
                }
            }
        }

        return Expand(count);
    }

    private void SelectMovementStateForCurrentMovement()
    {
        int supportCount = _currentMovement switch
        {
            ArtFloorFinalDaCapoMovement.First => 1,
            ArtFloorFinalDaCapoMovement.Second => 2,
            ArtFloorFinalDaCapoMovement.Third => 2,
            ArtFloorFinalDaCapoMovement.Fourth => 3,
            _ => 0
        };

        if (supportCount == 0)
        {
            _currentSupportCards = Array.Empty<ArtFloorFinalDaCapoSupportCard>();
            _currentStateKey = BuildStateKey(_currentMovement, _currentSupportCards);
            return;
        }

        // Asset preloading enumerates intents on mutable models before the
        // combat Creature and RunRng are bound. Use a deterministic preview
        // state there; live combat still rolls from the synchronized run RNG.
        if (!IsMutable || !HasBoundCreature())
        {
            _currentSupportCards = Enumerable
                .Repeat(ArtFloorFinalDaCapoSupportCard.Moderato, supportCount)
                .ToArray();
            _currentStateKey = BuildStateKey(_currentMovement, _currentSupportCards);
            return;
        }

        List<ArtFloorFinalDaCapoSupportCard> supportCards = [];
        for (int i = 0; i < supportCount; i++)
        {
            supportCards.Add((ArtFloorFinalDaCapoSupportCard)RunRng.MonsterAi.NextInt(0, 3));
        }

        _currentSupportCards = supportCards;
        _currentStateKey = BuildStateKey(_currentMovement, supportCards);
    }

    private bool HasBoundCreature()
    {
        try
        {
            _ = Creature;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string BuildStateKey(
        ArtFloorFinalDaCapoMovement movement,
        IReadOnlyList<ArtFloorFinalDaCapoSupportCard> supportCards)
    {
        if (supportCards.Count == 0)
        {
            return movement.ToString();
        }

        return movement + ":" + string.Join("-", supportCards.Select(static support => support.ToString()));
    }

    private MoveState? ResolveCurrentMoveState()
    {
        return _currentStateKey != null && _movementStates.TryGetValue(_currentStateKey, out MoveState? state)
            ? state
            : null;
    }

    private bool CanAdvanceMovement()
    {
        Creature creature = Creature;
        return creature.CombatState != null
            && !creature.IsDead
            && MoveStateMachine != null;
    }

    private bool OwnerIsCurrentPhaseBoss()
    {
        return Creature.CombatState?.Encounter is ArtFloorLiberationEncounter encounter
            && encounter.CurrentPhase == Phase;
    }

    private Task RefreshNodeIntents()
    {
        return NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents() ?? Task.CompletedTask;
    }

    private void SyncPerformerHiddenIntents()
    {
        if (CombatState == null)
        {
            return;
        }

        bool showUnknownIntent = _currentMovement is ArtFloorFinalDaCapoMovement.Fifth or ArtFloorFinalDaCapoMovement.Sixth;
        foreach (ArtFloorDaCapoPerformer performer in CombatState.Enemies
                     .Select(static enemy => enemy.Monster)
                     .OfType<ArtFloorDaCapoPerformer>())
        {
            if (performer.ConfigureHiddenIntent(showUnknownIntent))
            {
                performer.Creature.PrepareForNextTurn(CombatState.PlayerCreatures);
            }
        }
    }
}
