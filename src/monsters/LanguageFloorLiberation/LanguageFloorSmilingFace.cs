using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.SmilingBodies;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

using BaseSmilingBodies =
    SmilingBodies.SmilingBodies;

public enum LanguageFloorSmilingFaceForm
{
    First,
    Second,
    Third
}

internal enum LanguageFloorSmilingFaceMove
{
    Devour,//吞噬
    Absorb,//吸收
    Sit,//瘫坐
    Scream,//惨叫
    Vomit//呕吐
}

public sealed class LanguageFloorSmilingFace :
    LibraryMonsterModel,
    ITargetedMonsterAttackProvider,
    ILiberationPrimaryPhaseBoss
{
    public const int MaxCorpseCount = 3;
    public const int MaxChaoResistance = 100;
    public const int FormOneEntryHp = 100;
    public const int FormTwoEntryHp = 200;
    public const int FormThreeEntryHp = 300;
    public const int CorpseSpawnThresholdPercent = 25;
    public const int EntryCorpseCount = 1;
    public const int TrialCorpseCount = 3;
    public const int TrialPlayerTurns = 2;
    public const int PermanentStrongPerPlayerTurn = 1;
    public const int DevourHits = 2;
    public const int DevourHealPercentPerHit = 5;
    public const int AbsorbNextTurnStrength = 1;
    public const int SitHits = 2;
    public const int SitVulnerable = 1;
    public const int ScreamHits = 2;
    public const int VomitDebuffAmount = 3;
    public const int VomitDebuffTurns = 2;
    public const int FormOneIntentCapacity = 3;
    public const int FormTwoIntentCapacity = 2;
    public const int FormThreeIntentCapacity = 1;
    private const int StoredIntentSlotCount = 4;

    public const string DevourMoveId = "DEVOUR";
    public const string AbsorbMoveId = "ABSORB";
    public const string SitMoveId = "SIT";
    public const string ScreamMoveId = "SCREAM";
    public const string VomitMoveId = "VOMIT";
    public const string ReviveMoveId = "REVIVE";
    public const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";
    private const string RouterMoveId = "LANGUAGE_FLOOR_SMILING_FACE_ROUTER";
    private const string FakeDeathHiddenMoveId = "FAKE_DEATH_HIDDEN";
    private const string FormOneCompositeMoveId = "FORM_ONE_COMPOSITE";
    private const string FormTwoCompositeMoveId = "FORM_TWO_COMPOSITE";
    private const string FormThreeCompositeMoveId = "FORM_THREE_COMPOSITE";

    public const string Root =
        "res://images/monsters/language_floor_liberation/smiling_face/";
    public const string IdleTexturePath = Root + "smiling_face_idle.png";
    public const string AttackThrustTexturePath =
        Root + "smiling_face_attack_thrust.png";
    public const string AttackSlashTexturePath =
        Root + "smiling_face_attack_slash.png";
    public const string HitTexturePath = Root + "smiling_face_hit.png";
    public const string ScreamTexturePath = Root + "smiling_face_scream.png";
    public const string VomitTexturePath = Root + "smiling_face_vomit.png";

    private const float SegmentDelaySeconds = 0.45f;

    [SavedProperty]
    public LanguageFloorSmilingFaceForm Form { get; private set; } =
        LanguageFloorSmilingFaceForm.Third;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool Initialized { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FormOneMaxHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FormTwoMaxHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FormThreeMaxHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FormTurnCount { get; private set; }

    [SavedProperty]
    public int PreviousNormalMove { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingCorpseSpawns { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HpAtLastSpawnThreshold { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool WaitingForDowngrade { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FakeDeathPlayerTurnsRemaining { get; private set; }

    [SavedProperty]
    public int PendingFormTransition { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool PendingFormTransitionIsPromotion { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool CorpseTrialPending { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool CorpseTrialActive { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CorpseTrialPlayerTurnsRemaining { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ForceKillable { get; private set; }

    [SavedProperty]
    public int PlannedMoveOne { get; private set; } = -1;

    [SavedProperty]
    public int PlannedMoveTwo { get; private set; } = -1;

    [SavedProperty]
    public int PlannedMoveThree { get; private set; } = -1;

    [SavedProperty]
    public int PlannedMoveFour { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetOne { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetTwo { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetThree { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetFour { get; private set; } = -1;

    private bool _isApplyingFormStats;
    private bool _isTransitioning;
    private int _lastCorpseTrialTickPlayerTurn = -1;
    private MoveState? _devourState;
    private MoveState? _absorbState;
    private MoveState? _sitState;
    private MoveState? _screamState;
    private MoveState? _vomitState;
    private MoveState? _reviveState;
    private MoveState? _reviveAndEmpowerState;
    private MoveState? _fakeDeathHiddenState;
    private MoveState? _formOneCompositeState;
    private MoveState? _formTwoCompositeState;
    private MoveState? _formThreeCompositeState;
    private AbstractIntent[]? _formOnePlannedIntents;
    private AbstractIntent[]? _formTwoPlannedIntents;
    private AbstractIntent[]? _formThreePlannedIntents;

    public static readonly string[] PowerIconPaths =
    [
        "res://images/powers/language_floor_smiling_face_find_corpses_power.png",
        "res://images/powers/language_floor_smiling_face_form_one_split_power.png",
        "res://images/powers/language_floor_smiling_face_fusion_power.png",
        "res://images/powers/language_floor_smiling_face_split_and_fusion_power.png",
        "res://images/powers/language_floor_smiling_face_scream_power.png",
        "res://images/powers/language_floor_smiling_face_form_three_split_power.png",
        "res://images/powers/language_floor_smiling_face_vomit_power.png"
    ];

    public static readonly string[] AssetPathsStatic =
        LanguageFloorSmilingFaceCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                BaseSmilingBodies.AbsorbHitSfxPath,
                BaseSmilingBodies.Phase2ScreamSfxPath,
                BaseSmilingBodies.Phase3SitHitSfxPath,
                BaseSmilingBodies.Phase3VomitSfxPath,
                BaseSmilingBodies.PhaseDownSfxPath,
                BaseSmilingBodies.PhaseUpSfxPath,
                MeltingCorpse.SpawnSfxPath
            ])
            .Concat(PowerIconPaths)
            .ToArray();

    public bool IsFakeDead =>
        !ForceKillable
        && (WaitingForDowngrade || CorpseTrialPending || CorpseTrialActive);

    public int LiberationPhase => 3;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            348,
            340);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            350,
            345);

    public override int DefaultChaoResistance => MaxChaoResistance;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Normal
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Normal
        };

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent =>
                intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        if (!Initialized)
        {
            Initialized = true;
            await ApplyFormStats(Form, resetMaxHp: true);
            ResetSpawnThreshold();
        }
        await RefreshFormPowers();
        LanguageFloorLiberationBackgroundController.SetPhaseThreeBackground();
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            int playerTurn = GetCurrentPlayerTurn(combatState);
            if (playerTurn >= 0 && playerTurn == _lastCorpseTrialTickPlayerTurn)
            {
                Log.Info(
                    "[LanguageFloorSmilingFace] player-turn start already handled "
                    + $"by fake-death power turn={playerTurn}");
            }
            else
            {
                if (playerTurn >= 0)
                {
                    _lastCorpseTrialTickPlayerTurn = playerTurn;
                }

                await ResolvePlayerTurnStart(choiceContext, combatState);
            }
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        if (creature != Creature || _isApplyingFormStats || ForceKillable)
        {
            return;
        }

        if (delta < 0m)
        {
            QueueCorpseSpawnsForThresholds();
        }
        else if (delta > 0m)
        {
            await TryPromoteAtFullHealth();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _formOnePlannedIntents = new AbstractIntent[FormOneIntentCapacity];
        _formTwoPlannedIntents = new AbstractIntent[FormTwoIntentCapacity];
        _formThreePlannedIntents = new AbstractIntent[FormThreeIntentCapacity];
        RefreshPlannedIntents();

        _devourState = new MoveState(
            DevourMoveId,
            DevourMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Devour));
        _absorbState = new MoveState(
            AbsorbMoveId,
            AbsorbMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Absorb));
        _sitState = new MoveState(
            SitMoveId,
            SitMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Sit));
        _screamState = new MoveState(
            ScreamMoveId,
            ScreamMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Scream));
        _vomitState = new MoveState(
            VomitMoveId,
            VomitMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Vomit));
        _reviveState = new LibraryPhaseTransitionMoveState(
            ReviveMoveId,
            ReviveMove,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };
        _reviveAndEmpowerState = new LibraryPhaseTransitionMoveState(
            ReviveAndEmpowerMoveId,
            ReviveAndEmpowerMove,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };
        _fakeDeathHiddenState = new MoveState(
            FakeDeathHiddenMoveId,
            static _ => Task.CompletedTask,
            new HiddenIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };
        _fakeDeathHiddenState.FollowUpState = _fakeDeathHiddenState;
        _formOneCompositeState = new MoveState(
            FormOneCompositeMoveId,
            PerformCompositeMove,
            _formOnePlannedIntents);
        _formTwoCompositeState = new MoveState(
            FormTwoCompositeMoveId,
            PerformCompositeMove,
            _formTwoPlannedIntents);
        _formThreeCompositeState = new MoveState(
            FormThreeCompositeMoveId,
            PerformCompositeMove,
            _formThreePlannedIntents);

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, rng) =>
            {
                PlanTurn(rng);
                return GetCurrentCompositeState().Id;
            });
        foreach (MoveState move in new[]
                 {
                     _devourState,
                     _absorbState,
                     _sitState,
                     _screamState,
                     _vomitState,
                     _reviveState,
                     _reviveAndEmpowerState,
                     _formOneCompositeState,
                     _formTwoCompositeState,
                     _formThreeCompositeState
                 })
        {
            move.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [
                _devourState,
                _absorbState,
                _sitState,
                _screamState,
                _vomitState,
                _reviveState,
                _reviveAndEmpowerState,
                _fakeDeathHiddenState,
                _formOneCompositeState,
                _formTwoCompositeState,
                _formThreeCompositeState,
                router
            ],
            IsFakeDead
                ? _fakeDeathHiddenState
                : HasPlannedTurn
                    ? GetCurrentCompositeState()
                    : router);
    }

    public bool UsesTargetedAttackContract(Creature owner) =>
        NextMove.Intents.Any(static intent =>
            intent is ITargetedIntentIndicator);

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        Creature? target = Enumerable.Range(0, GetIntentCapacity(Form))
            .Where(slot => IsTargetedMove(GetPlannedMove(slot)))
            .Select(GetPlannedTarget)
            .FirstOrDefault(static candidate => candidate is { IsAlive: true });
        return target is { IsAlive: true } ? [target] : [];
    }

    public string GetTargetedAttackTargetName(Creature owner) =>
        GetTargetedAttackTargets(owner).FirstOrDefault()?.Name
        ?? "Unknown Target";

    public bool CanEnterFakeDeath(Creature creature) =>
        creature == Creature
        && !ForceKillable
        && !IsFakeDead
        && Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter { PhaseComplete: false, CurrentPhase: 3 };

    public async Task EnterFakeDeathFromDeath()
    {
        if (IsFakeDead)
        {
            return;
        }

        if (!CanEnterFakeDeath(Creature))
        {
            ForceKillable = true;
            return;
        }

        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            PendingCorpseSpawns = 0;
            CorpseTrialPending = true;
            CorpseTrialActive = false;
            CorpseTrialPlayerTurnsRemaining = 0;
        }
        else
        {
            WaitingForDowngrade = true;
            FakeDeathPlayerTurnsRemaining = 0;
            QueueFormTransition(
                Form == LanguageFloorSmilingFaceForm.Third
                    ? LanguageFloorSmilingFaceForm.Second
                    : LanguageFloorSmilingFaceForm.First,
                isPromotion: false);
        }

        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            EnterHiddenFakeDeathIntent();
        }
        else
        {
            EnterReviveIntent();
        }

        Log.Info(
            "[LanguageFloorSmilingFace] fake-death enter "
            + $"form={Form} pending={CorpseTrialPending} "
            + $"active={CorpseTrialActive}");
    }

    public async Task OnAllyKilled()
    {
        if (Creature.IsDead || IsFakeDead)
        {
            return;
        }

        int percent = Form == LanguageFloorSmilingFaceForm.Third ? 30 : 40;
        int amount = Math.Max(
            1,
            (int)Math.Ceiling(Creature.MaxHp * percent / 100m));
        await CreatureCmd.Heal(Creature, amount);
    }

    public async Task OnCorpseDeath()
    {
        if (Creature.CombatState is not { } combatState)
        {
            return;
        }

        Log.Info(
            "[LanguageFloorSmilingFace] corpse death resolved "
            + $"trialActive={CorpseTrialActive} "
            + $"livingCorpses={CountLivingCorpses(combatState)}");
        await RefreshPlannedTargetsAfterRosterChanged(retargetAll: false);
        if (!CorpseTrialActive
            || CountLivingCorpses(combatState) > 0)
        {
            return;
        }

        Log.Info("[LanguageFloorSmilingFace] corpse trial won; completing phase three.");
        if (combatState.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseThree(this, combatState);
        }
    }

    public void MarkEncounterComplete()
    {
        ForceKillable = true;
        WaitingForDowngrade = false;
        CorpseTrialPending = false;
        CorpseTrialActive = false;
        CorpseTrialPlayerTurnsRemaining = 0;
        FakeDeathPlayerTurnsRemaining = 0;
        PendingFormTransition = -1;
        PendingFormTransitionIsPromotion = false;
    }

    public async Task TriggerReviveAndEmpowerState()
    {
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) != null)
        {
            await CreatureCmd.TriggerAnim(Creature, "Hit", 0f);
        }

        ForceReviveAndEmpowerState();
    }

    public void ForceReviveAndEmpowerState()
    {
        if (_reviveAndEmpowerState != null)
        {
            SetMoveImmediate(
                _reviveAndEmpowerState,
                forceTransition: true);
        }
    }

    private async Task ResolvePlayerTurnStart(
        PlayerChoiceContext choiceContext,
        CombatStateLike combatState)
    {
        await ResolvePendingFormTransition();

        if (await ResolveCorpseTrial(combatState))
        {
            return;
        }

        await ResolvePendingCorpseSpawns(combatState);

        if (Creature.IsAlive
            && Form is LanguageFloorSmilingFaceForm.Second
                or LanguageFloorSmilingFaceForm.Third)
        {
            await GainPermanentStrong(choiceContext);
        }

        if (!IsFakeDead && NextMove.Id == FakeDeathHiddenMoveId)
        {
            Log.Warn(
                "[LanguageFloorSmilingFace] stale FAKE_DEATH_HIDDEN after "
                + "trial resolution; resetting move state.");
            ResetMoveStateForCurrentForm();
            Creature.PrepareForNextTurn(combatState.PlayerCreatures);
        }
    }

    internal async Task TickCorpseTrialOnPlayerTurnStart(
        CombatStateLike combatState)
    {
        int playerTurn = GetCurrentPlayerTurn(combatState);
        if (playerTurn >= 0 && playerTurn == _lastCorpseTrialTickPlayerTurn)
        {
            return;
        }

        if (playerTurn >= 0)
        {
            _lastCorpseTrialTickPlayerTurn = playerTurn;
        }

        if (!CorpseTrialPending && !CorpseTrialActive)
        {
            return;
        }

        Log.Info(
            "[LanguageFloorSmilingFace] corpse trial tick "
            + $"turn={playerTurn} pending={CorpseTrialPending} "
            + $"active={CorpseTrialActive} "
            + $"remaining={CorpseTrialPlayerTurnsRemaining} "
            + $"corpses={CountLivingCorpses(combatState)}");
        await ResolveCorpseTrial(combatState);
    }

    private async Task<bool> ResolveCorpseTrial(CombatStateLike combatState)
    {
        if (CorpseTrialPending)
        {
            CorpseTrialPending = false;
            CorpseTrialActive = true;
            CorpseTrialPlayerTurnsRemaining = TrialPlayerTurns;
            PendingCorpseSpawns = 0;
            Log.Info(
                "[LanguageFloorSmilingFace] corpse trial activated "
                + $"turns={CorpseTrialPlayerTurnsRemaining} "
                + $"corpses={CountLivingCorpses(combatState)}");
            await FillCorpsesToCount(combatState, TrialCorpseCount);
            return true;
        }

        if (!CorpseTrialActive)
        {
            return false;
        }

        int livingCorpses = CountLivingCorpses(combatState);
        if (livingCorpses == 0)
        {
            await OnCorpseDeath();
            return true;
        }

        CorpseTrialPlayerTurnsRemaining--;
        Log.Info(
            "[LanguageFloorSmilingFace] corpse trial decrement "
            + $"remaining={CorpseTrialPlayerTurnsRemaining} "
            + $"corpses={livingCorpses}");
        if (CorpseTrialPlayerTurnsRemaining <= 0)
        {
            await FailCorpseTrial(combatState);
        }

        return true;
    }

    private static int GetCurrentPlayerTurn(CombatStateLike combatState)
    {
        return combatState.Players
            .FirstOrDefault(static player =>
                player.PlayerCombatState != null)
            ?.PlayerCombatState?.TurnNumber
            ?? -1;
    }

    private async Task GainPermanentStrong(PlayerChoiceContext choiceContext)
    {
        LibraryStrongPower? existing = Creature
            .GetPowerInstances<LibraryStrongPower>()
            .FirstOrDefault(static power => power.AmountPlan.Count == 0);
        if (existing == null)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(new ThrowingPlayerChoiceContext(),
                Creature,
                PermanentStrongPerPlayerTurn,
                0,
                true,
                Creature,
                null);
        }
        else
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                PermanentStrongPerPlayerTurn,
                Creature,
                null);
        }
    }

    private void QueueCorpseSpawnsForThresholds()
    {
        if (Creature.MaxHp <= 0 || HpAtLastSpawnThreshold <= 0)
        {
            return;
        }

        int threshold = Math.Max(
            1,
            (int)Math.Ceiling(
                Creature.MaxHp * CorpseSpawnThresholdPercent / 100m));
        while (HpAtLastSpawnThreshold - Creature.CurrentHp >= threshold)
        {
            PendingCorpseSpawns++;
            HpAtLastSpawnThreshold -= threshold;
        }
    }

    private async Task ResolvePendingCorpseSpawns(CombatStateLike combatState)
    {
        int available = MaxCorpseCount - CountLivingCorpses(combatState);
        int count = Math.Min(PendingCorpseSpawns, Math.Max(0, available));
        for (int i = 0; i < count; i++)
        {
            if (!await SpawnOneCorpse(combatState))
            {
                break;
            }

            PendingCorpseSpawns--;
        }

        combatState.SortEnemiesBySlotName();
        await RefreshPlannedTargetsAfterRosterChanged(retargetAll: true);
    }

    private async Task FillCorpsesToCount(
        CombatStateLike combatState,
        int targetCount)
    {
        while (CountLivingCorpses(combatState) < targetCount)
        {
            if (!await SpawnOneCorpse(combatState))
            {
                break;
            }
        }

        combatState.SortEnemiesBySlotName();
        await RefreshPlannedTargetsAfterRosterChanged(retargetAll: true);
    }

    internal async Task RefreshPlannedTargetsAfterRosterChanged(
        bool retargetAll)
    {
        int capacity = GetIntentCapacity(Form);
        for (int slot = 0; slot < capacity; slot++)
        {
            LanguageFloorSmilingFaceMove move = GetPlannedMove(slot);
            if (!IsTargetedMove(move))
            {
                SetPlannedTarget(slot, null);
                continue;
            }

            if (retargetAll || GetPlannedTarget(slot) == null)
            {
                SetPlannedTarget(slot, ChooseRandomTarget(RunRng.MonsterAi));
            }
        }

        RefreshPlannedIntents();
        await RefreshTargetedIntentDisplay();
    }

    internal Task RefreshTargetedIntentDisplay()
    {
        if (Creature.CombatState is not { } combatState)
        {
            return Task.CompletedTask;
        }

        return NCombatRoom.Instance?.GetCreatureNode(Creature)
                   ?.UpdateIntent(combatState.PlayerCreatures)
            ?? Task.CompletedTask;
    }

    private async Task<bool> SpawnOneCorpse(CombatStateLike combatState)
    {
        string? slot = NextOpenCorpseSlot(combatState);
        if (slot == null)
        {
            return false;
        }

        Creature corpse = await CreatureCmd.Add(
            ModelDb.Monster<LanguageFloorMeltingCorpse>().ToMutable(),
            combatState,
            CombatSide.Enemy,
            slot);
        corpse.PrepareForNextTurn(combatState.PlayerCreatures);
        return true;
    }

    private async Task FailCorpseTrial(CombatStateLike combatState)
    {
        Log.Info("[LanguageFloorSmilingFace] corpse trial failed; resetting to form one.");
        CorpseTrialActive = false;
        CorpseTrialPlayerTurnsRemaining = 0;
        foreach (Creature corpse in combatState.Enemies
                     .Where(static enemy =>
                         enemy.Monster is LanguageFloorMeltingCorpse)
                     .ToArray())
        {
            await LiberationPhaseCleanup.RemoveTransitionCreature(
                corpse,
                combatState);
        }

        LocalOggOneShotPlayer.Play(BaseSmilingBodies.PhaseUpSfxPath, -1.5f);
        PendingCorpseSpawns = EntryCorpseCount;
        await ApplyFormStats(LanguageFloorSmilingFaceForm.First);
        ResetFormMoves();
        ResetSpawnThreshold();
        ResetMoveStateForCurrentForm();
        await RefreshFormPowers();
        Creature.PrepareForNextTurn(combatState.PlayerCreatures);
        Log.Info(
            "[LanguageFloorSmilingFace] corpse trial fail complete "
            + $"nextMove={NextMove.Id}");
    }

    private Task TryPromoteAtFullHealth()
    {
        if (_isTransitioning
            || Creature.IsDead
            || IsFakeDead
            || PendingFormTransition >= 0
            || Creature.CurrentHp < Creature.MaxHp)
        {
            return Task.CompletedTask;
        }

        LanguageFloorSmilingFaceForm? next = Form switch
        {
            LanguageFloorSmilingFaceForm.First =>
                LanguageFloorSmilingFaceForm.Second,
            LanguageFloorSmilingFaceForm.Second =>
                LanguageFloorSmilingFaceForm.Third,
            _ => null
        };
        if (next != null)
        {
            QueueFormTransition(next.Value, isPromotion: true);
        }

        return Task.CompletedTask;
    }

    private void QueueFormTransition(
        LanguageFloorSmilingFaceForm target,
        bool isPromotion)
    {
        if (target == Form)
        {
            return;
        }

        PendingFormTransition = (int)target;
        PendingFormTransitionIsPromotion = isPromotion;
    }

    private async Task ResolvePendingFormTransition()
    {
        if (!Enum.IsDefined(
                typeof(LanguageFloorSmilingFaceForm),
                PendingFormTransition))
        {
            return;
        }

        var target =
            (LanguageFloorSmilingFaceForm)PendingFormTransition;
        string sfxPath = PendingFormTransitionIsPromotion
            ? BaseSmilingBodies.PhaseUpSfxPath
            : BaseSmilingBodies.PhaseDownSfxPath;
        PendingFormTransition = -1;
        PendingFormTransitionIsPromotion = false;
        WaitingForDowngrade = false;
        FakeDeathPlayerTurnsRemaining = 0;
        await TransitionToForm(target, sfxPath);
    }

    private async Task TransitionToForm(
        LanguageFloorSmilingFaceForm target,
        string sfxPath)
    {
        if (_isTransitioning || Form == target)
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            Form = target;
            PendingCorpseSpawns += target == LanguageFloorSmilingFaceForm.Third
                ? 0
                : EntryCorpseCount;
            ResetFormMoves();
            LocalOggOneShotPlayer.Play(sfxPath, -1.5f);
            await CreatureCmd.TriggerAnim(Creature, "Phase", 0.55f);
            if (!CombatManager.Instance.IsInProgress
                || Creature.CombatState is not { } combatState)
            {
                Log.Warn(
                    "[LanguageFloorSmilingFace] skipped transition tail because combat ended.");
                return;
            }

            await ApplyFormStats(target);
            ResetSpawnThreshold();
            ResetMoveStateForCurrentForm();
            await RefreshFormPowers();
            Creature.PrepareForNextTurn(combatState.PlayerCreatures);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private async Task ApplyFormStats(
        LanguageFloorSmilingFaceForm form,
        bool resetMaxHp = true)
    {
        _isApplyingFormStats = true;
        try
        {
            if (resetMaxHp)
            {
                int maxHp = await ResolveFormMaxHp(form);
                await CreatureCmd.SetMaxHp(Creature, maxHp);
            }

            decimal entryHp = MultiplayerScalingPatchHelper.ScaleHpAmount(
                Creature.CombatState,
                this,
                EntryHp(form));
            await CreatureCmd.SetCurrentHp(
                Creature,
                Math.Min(Creature.MaxHp, entryHp));

            if (Creature is LibraryCreature libraryCreature)
            {
                await MultiplayerScalingPatchHelper.SetMonsterBaseMaxAndCurrentChaoValue(
                    libraryCreature,
                    MaxChaoResistance);
            }
        }
        finally
        {
            _isApplyingFormStats = false;
        }
    }

    private async Task<int> ResolveFormMaxHp(
        LanguageFloorSmilingFaceForm form)
    {
        int saved = form switch
        {
            LanguageFloorSmilingFaceForm.First => FormOneMaxHp,
            LanguageFloorSmilingFaceForm.Second => FormTwoMaxHp,
            _ => FormThreeMaxHp
        };
        if (saved > 0)
        {
            return saved;
        }

        (int min, int max) = FormMaxHpRange(form);
        int rolled = RunRng.MonsterAi.NextInt(min, max + 1);
        decimal scaled = MultiplayerScalingPatchHelper.ScaleHpAmount(
            Creature.CombatState,
            this,
            rolled);
        await CreatureCmd.SetMaxHp(Creature, scaled);
        int actual = Creature.MaxHp;
        switch (form)
        {
            case LanguageFloorSmilingFaceForm.First:
                FormOneMaxHp = actual;
                break;
            case LanguageFloorSmilingFaceForm.Second:
                FormTwoMaxHp = actual;
                break;
            default:
                FormThreeMaxHp = actual;
                break;
        }

        return actual;
    }

    private async Task RefreshFormPowers()
    {
        await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceFindCorpsesPower>(
            Creature);
        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorSmilingFaceFormOneSplitPower>(Creature);
            await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceFusionPower>(
                Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceSplitAndFusionPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceScreamPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFormThreeSplitPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceVomitPower>(Creature);
            return;
        }

        if (Form == LanguageFloorSmilingFaceForm.Second)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorSmilingFaceSplitAndFusionPower>(Creature);
            await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceScreamPower>(
                Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFormOneSplitPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFusionPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFormThreeSplitPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceVomitPower>(Creature);
            return;
        }

        await PowerCmdCompat.Ensure<
            LanguageFloorSmilingFaceFormThreeSplitPower>(Creature);
        await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceVomitPower>(
            Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceFormOneSplitPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceFusionPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceSplitAndFusionPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceScreamPower>(Creature);
    }

    private void ResetFormMoves()
    {
        FormTurnCount = 0;
        PreviousNormalMove = -1;
        for (int slot = 0; slot < StoredIntentSlotCount; slot++)
        {
            SetPlannedMove(slot, null);
            SetPlannedTarget(slot, null);
        }

        RefreshPlannedIntents();
    }

    private void ResetSpawnThreshold()
    {
        HpAtLastSpawnThreshold = Creature.CurrentHp;
    }

    private void ResetMoveStateForCurrentForm()
    {
        ResetStateMachine();
        SetUpForCombat();
    }

    private void EnterHiddenFakeDeathIntent()
    {
        if (_fakeDeathHiddenState != null)
        {
            SetMoveImmediate(_fakeDeathHiddenState, forceTransition: true);
        }
    }

    private void EnterReviveIntent()
    {
        if (_reviveState != null)
        {
            SetMoveImmediate(_reviveState, forceTransition: true);
        }
    }

    private Task ReviveMove(IReadOnlyList<Creature> targets)
    {
        if (!WaitingForDowngrade || PendingFormTransition < 0)
        {
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private async Task ReviveAndEmpowerMove(
        IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await Cmd.CustomScaledWait(0.3f, 0.6f);
        if (Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    private async Task PerformCompositeMove(IReadOnlyList<Creature> targets)
    {
        int capacity = GetIntentCapacity(Form);
        for (int slot = 0; slot < capacity && Creature.IsAlive; slot++)
        {
            await PerformPlannedMove(GetPlannedMove(slot), slot);
            if (Creature.CombatState?.Encounter
                is LanguageFloorLiberationEncounter { PhaseComplete: true })
            {
                return;
            }
        }
    }

    private Task PerformPlannedMove(
        LanguageFloorSmilingFaceMove move,
        int slot) => move switch
    {
        LanguageFloorSmilingFaceMove.Devour => DevourMove([], slot),
        LanguageFloorSmilingFaceMove.Absorb => AbsorbMove([], slot),
        LanguageFloorSmilingFaceMove.Sit => SitMove([], slot),
        LanguageFloorSmilingFaceMove.Scream => ScreamMove([]),
        _ => VomitMove([])
    };

    private async Task DevourMove(IReadOnlyList<Creature> targets)
    {
        await DevourMove(targets, plannedSlot: null);
    }

    private async Task DevourMove(
        IReadOnlyList<Creature> targets,
        int? plannedSlot)
    {
        LanguageFloorSmilingFaceForm startingForm = Form;
        Creature? target = ResolveMoveTarget(plannedSlot);
        if (target == null)
        {
            return;
        }

        using var lunge = new TargetedAttackLungeScope(this, [target]);
        for (int hit = 0;
             hit < DevourHits
             && Creature.IsAlive
             && target.IsAlive;
             hit++)
        {
            LocalOggOneShotPlayer.Play(BaseSmilingBodies.AbsorbHitSfxPath, -2f);
            await CreatureCmd.TriggerAnim(
                Creature,
                GetNormalAttackAnimation(hit),
                SegmentDelaySeconds);
            await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                target,
                GetMoveDamage(LanguageFloorSmilingFaceMove.Devour),
                ValueProp.Move,
                Creature,
                null);

            if (Creature.IsAlive)
            {
                int heal = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        Creature.MaxHp * DevourHealPercentPerHit / 100m));
                await CreatureCmd.Heal(Creature, heal);
            }

            if (Form != startingForm)
            {
                return;
            }
        }
    }

    private async Task AbsorbMove(IReadOnlyList<Creature> targets)
    {
        await AbsorbMove(targets, plannedSlot: null);
    }

    private async Task AbsorbMove(
        IReadOnlyList<Creature> targets,
        int? plannedSlot)
    {
        Creature? target = ResolveMoveTarget(plannedSlot);
        if (target == null)
        {
            return;
        }

        await ExecuteTargetedHits(target, GetMoveDamage(
            LanguageFloorSmilingFaceMove.Absorb), 1);
        if (Creature.IsAlive)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(
                Creature,
                AbsorbNextTurnStrength,
                Creature,
                null);
        }
    }

    private async Task SitMove(IReadOnlyList<Creature> targets)
    {
        await SitMove(targets, plannedSlot: null);
    }

    private async Task SitMove(
        IReadOnlyList<Creature> targets,
        int? plannedSlot)
    {
        LocalOggOneShotPlayer.Play(BaseSmilingBodies.Phase3SitHitSfxPath, -2f);
        await ExecuteIndiscriminateAttack(
            GetMoveDamage(LanguageFloorSmilingFaceMove.Sit),
            SitHits,
            "Attack",
            "vfx/vfx_attack_blunt");
        foreach (Creature target in GetLivingIndiscriminateTargets())
        {
            await PowerCmdCompat.ApplyDebuff<FrailPower>(
                target,
                SitVulnerable,
                Creature,
                null);
        }
    }

    private async Task ExecuteIndiscriminateAttack(
        int damage,
        int hits,
        string animation,
        string hitFx)
    {
        for (int hit = 0; hit < hits && Creature.IsAlive; hit++)
        {
            IReadOnlyList<Creature> targets =
                GetLivingIndiscriminateTargets();
            if (targets.Count == 0)
            {
                return;
            }

            await IndiscriminateAttackExecutor.Execute(
                this,
                damage,
                targets,
                attack => attack
                    .WithAttackerAnim(animation, SegmentDelaySeconds)
                    .WithHitFx(hitFx),
                new ThrowingPlayerChoiceContext());
        }
    }

    private async Task ExecuteTargetedHits(
        Creature target,
        int damage,
        int hits)
    {
        using var lunge = new TargetedAttackLungeScope(this, [target]);
        for (int hit = 0;
             hit < hits && Creature.IsAlive && target.IsAlive;
             hit++)
        {
            LocalOggOneShotPlayer.Play(BaseSmilingBodies.AbsorbHitSfxPath, -2f);
            await CreatureCmd.TriggerAnim(
                Creature,
                GetNormalAttackAnimation(hit),
                SegmentDelaySeconds);
            await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                target,
                damage,
                ValueProp.Move,
                Creature,
                null);
        }
    }

    private async Task ScreamMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(BaseSmilingBodies.Phase2ScreamSfxPath, -2f);
        await ExecuteIndiscriminateAttack(
            GetMoveDamage(LanguageFloorSmilingFaceMove.Scream),
            ScreamHits,
            "Scream",
            "vfx/vfx_attack_blunt");
    }

    private async Task VomitMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(BaseSmilingBodies.Phase3VomitSfxPath, -2f);
        await ExecuteIndiscriminateAttack(
            GetMoveDamage(LanguageFloorSmilingFaceMove.Vomit),
            1,
            "Vomit",
            "vfx/vfx_bloody_impact");

        foreach (Creature target in GetLivingIndiscriminateTargets())
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                VomitDebuffAmount,
                VomitDebuffTurns,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                target,
                VomitDebuffAmount,
                VomitDebuffTurns,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(
                target,
                VomitDebuffAmount,
                VomitDebuffTurns,
                Creature,
            null);
        }
    }

    private static string GetNormalAttackAnimation(int hitIndex) =>
        hitIndex % 2 == 0 ? "AttackThrust" : "AttackSlash";

    private Creature? ResolveMoveTarget(int? plannedSlot)
    {
        if (plannedSlot is int slot)
        {
            Creature? planned = GetPlannedTarget(slot);
            if (planned != null)
            {
                return planned;
            }

            Creature? replacement = ChooseRandomTarget(RunRng.MonsterAi);
            SetPlannedTarget(slot, replacement);
            return replacement;
        }

        return ChooseRandomTarget(RunRng.MonsterAi);
    }

    private Creature? ChooseRandomTarget(Rng rng)
    {
        IReadOnlyList<Creature> candidates = GetLivingTargetCandidates();
        return candidates.Count == 0
            ? null
            : candidates[rng.NextInt(candidates.Count)];
    }

    private IReadOnlyList<Creature> GetLivingTargetCandidates()
    {
        if (Creature.CombatState is not { } combatState)
        {
            return [];
        }

        IEnumerable<Creature> players = combatState.PlayerCreatures
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.Player?.NetId ?? 0UL);
        IEnumerable<Creature> corpses = combatState.Enemies
            .Where(static target =>
                target.IsAlive
                && target.Monster is LanguageFloorMeltingCorpse)
            .OrderBy(static target => target.SlotName);
        return players.Concat(corpses).ToArray();
    }

    private IReadOnlyList<Creature> GetLivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray()
        ?? [];

    private IReadOnlyList<Creature> GetLivingIndiscriminateTargets() =>
        LanguageFloorLiberationCombatHelper
            .GetLivingPlayersAndCorpses(Creature);

    private string ChooseNextMoveId(Rng rng)
    {
        FormTurnCount++;
        if (ShouldUseSpecial(Form, FormTurnCount))
        {
            return MoveId(Form == LanguageFloorSmilingFaceForm.Second
                ? LanguageFloorSmilingFaceMove.Scream
                : LanguageFloorSmilingFaceMove.Vomit);
        }

        return MoveId(ChooseNormalMove(rng));
    }

    private void PlanTurn(Rng rng)
    {
        FormTurnCount++;
        int capacity = GetIntentCapacity(Form);
        bool useSpecial = ShouldUseSpecial(Form, FormTurnCount);
        for (int slot = 0; slot < capacity; slot++)
        {
            LanguageFloorSmilingFaceMove move = useSpecial && slot == 0
                ? Form == LanguageFloorSmilingFaceForm.Second
                    ? LanguageFloorSmilingFaceMove.Scream
                    : LanguageFloorSmilingFaceMove.Vomit
                : ChooseNormalMove(rng);
            SetPlannedMove(slot, move);
            SetPlannedTarget(
                slot,
                IsTargetedMove(move) ? ChooseRandomTarget(rng) : null);
        }

        for (int slot = capacity; slot < StoredIntentSlotCount; slot++)
        {
            SetPlannedMove(slot, null);
            SetPlannedTarget(slot, null);
        }

        RefreshPlannedIntents();
    }

    private LanguageFloorSmilingFaceMove ChooseNormalMove(Rng rng)
    {
        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            PreviousNormalMove = (int)LanguageFloorSmilingFaceMove.Devour;
            return LanguageFloorSmilingFaceMove.Devour;
        }

        LanguageFloorSmilingFaceMove[] candidates = Form switch
        {
            LanguageFloorSmilingFaceForm.Second =>
            [
                LanguageFloorSmilingFaceMove.Devour,
                LanguageFloorSmilingFaceMove.Absorb
            ],
            _ =>
            [
                LanguageFloorSmilingFaceMove.Devour,
                LanguageFloorSmilingFaceMove.Absorb,
                LanguageFloorSmilingFaceMove.Sit
            ]
        };
        LanguageFloorSmilingFaceMove[] filtered = candidates
            .Where(move => (int)move != PreviousNormalMove)
            .ToArray();
        LanguageFloorSmilingFaceMove selected = rng.NextItem(
            filtered.Length > 0 ? filtered : candidates);
        PreviousNormalMove = (int)selected;
        return selected;
    }

    internal static bool ShouldUseSpecial(
        LanguageFloorSmilingFaceForm form,
        int formTurn)
    {
        if (formTurn <= 0)
        {
            return false;
        }

        return form switch
        {
            LanguageFloorSmilingFaceForm.Second => (formTurn - 1) % 2 == 0,
            LanguageFloorSmilingFaceForm.Third => (formTurn - 1) % 3 == 0,
            _ => false
        };
    }

    internal static int GetMoveDamage(LanguageFloorSmilingFaceMove move) =>
        move switch
        {
            LanguageFloorSmilingFaceMove.Devour =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    5,
                    3),
            LanguageFloorSmilingFaceMove.Absorb =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    13,
                    11),
            LanguageFloorSmilingFaceMove.Sit =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    4,
                    3),
            LanguageFloorSmilingFaceMove.Scream =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    6,
                    5),
            _ => AscensionHelper.GetValueIfAscension(
                AscensionLevel.DeadlyEnemies,
                11,
                10)
        };

    internal async Task DebugTransitionToForm(
        LanguageFloorSmilingFaceForm form) =>
        await TransitionToForm(form, BaseSmilingBodies.PhaseDownSfxPath);

    internal Task DebugPerformMove(LanguageFloorSmilingFaceMove move) =>
        move switch
        {
            LanguageFloorSmilingFaceMove.Devour => DevourMove([]),
            LanguageFloorSmilingFaceMove.Absorb => AbsorbMove([]),
            LanguageFloorSmilingFaceMove.Sit => SitMove([]),
            LanguageFloorSmilingFaceMove.Scream => ScreamMove([]),
            _ => VomitMove([])
        };

    internal string DebugChooseNextMoveId(Rng rng) => ChooseNextMoveId(rng);

    internal string DebugChooseNextMoveId() =>
        ChooseNextMoveId(RunRng.MonsterAi);

    internal void DebugSetMovePlanState(
        LanguageFloorSmilingFaceForm form,
        int formTurnCount = 0,
        int previousNormalMove = -1)
    {
        Form = form;
        FormTurnCount = formTurnCount;
        PreviousNormalMove = previousNormalMove;
    }

    internal Creature? DebugChooseDevourTarget() => GetPlannedTarget(0);

    internal void DebugSetNextMove(LanguageFloorSmilingFaceMove move) =>
        SetMoveImmediate(GetMoveState(MoveId(move)), forceTransition: true);

    internal void DebugPlanTurn()
    {
        PlanTurn(RunRng.MonsterAi);
        if (_formOneCompositeState != null
            && _formTwoCompositeState != null
            && _formThreeCompositeState != null)
        {
            SetMoveImmediate(GetCurrentCompositeState(), forceTransition: true);
        }
    }

    internal void DebugSetPlan(
        LanguageFloorSmilingFaceForm form,
        int formTurnCount,
        IReadOnlyList<LanguageFloorSmilingFaceMove> moves,
        IReadOnlyList<Creature?> targets)
    {
        Form = form;
        FormTurnCount = formTurnCount;
        for (int slot = 0; slot < StoredIntentSlotCount; slot++)
        {
            SetPlannedMove(slot, slot < moves.Count ? moves[slot] : null);
            SetPlannedTarget(slot, slot < targets.Count ? targets[slot] : null);
        }

        RefreshPlannedIntents();
        if (_formOneCompositeState != null
            && _formTwoCompositeState != null
            && _formThreeCompositeState != null)
        {
            SetMoveImmediate(GetCurrentCompositeState(), forceTransition: true);
        }
    }

    internal Task DebugPerformPlannedMove(
        LanguageFloorSmilingFaceMove move,
        int slot) => PerformPlannedMove(move, slot);

    internal Task DebugResolvePlayerTurnStart(
        PlayerChoiceContext choiceContext,
        CombatStateLike combatState) =>
        ResolvePlayerTurnStart(choiceContext, combatState);

    internal async Task DebugRevive()
    {
        await ReviveMove([]);
        await ResolvePendingFormTransition();
    }

    private static string MoveId(LanguageFloorSmilingFaceMove move) =>
        move switch
        {
            LanguageFloorSmilingFaceMove.Devour => DevourMoveId,
            LanguageFloorSmilingFaceMove.Absorb => AbsorbMoveId,
            LanguageFloorSmilingFaceMove.Sit => SitMoveId,
            LanguageFloorSmilingFaceMove.Scream => ScreamMoveId,
            _ => VomitMoveId
        };

    private static int EntryHp(LanguageFloorSmilingFaceForm form) =>
        form switch
        {
            LanguageFloorSmilingFaceForm.First => FormOneEntryHp,
            LanguageFloorSmilingFaceForm.Second => FormTwoEntryHp,
            _ => FormThreeEntryHp
        };

    private static (int Min, int Max) FormMaxHpRange(
        LanguageFloorSmilingFaceForm form)
    {
        return form switch
        {
            LanguageFloorSmilingFaceForm.First =>
                (AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 197, 190),
                    AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 200, 193)),
            LanguageFloorSmilingFaceForm.Second =>
                (AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 297, 290),
                    AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 300, 293)),
            _ =>
                (AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 348, 340),
                    AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 350, 345))
        };
    }

    private static int CountLivingCorpses(CombatStateLike combatState) =>
        combatState.Enemies.Count(static enemy =>
            enemy.IsAlive && enemy.Monster is LanguageFloorMeltingCorpse);

    private static string? NextOpenCorpseSlot(CombatStateLike combatState) =>
        LanguageFloorLiberationEncounter.CorpseSlots.FirstOrDefault(slot =>
            combatState.Enemies.All(enemy =>
                enemy.SlotName != slot || !enemy.IsAlive));

    internal static int GetIntentCapacity(
        LanguageFloorSmilingFaceForm form) => form switch
    {
        LanguageFloorSmilingFaceForm.First => FormOneIntentCapacity,
        LanguageFloorSmilingFaceForm.Second => FormTwoIntentCapacity,
        _ => FormThreeIntentCapacity
    };

    private static bool IsTargetedMove(
        LanguageFloorSmilingFaceMove move) => move is
        LanguageFloorSmilingFaceMove.Devour
        or LanguageFloorSmilingFaceMove.Absorb;

    internal LanguageFloorSmilingFaceMove GetPlannedMove(int slot)
    {
        int value = slot switch
        {
            0 => PlannedMoveOne,
            1 => PlannedMoveTwo,
            2 => PlannedMoveThree,
            _ => PlannedMoveFour
        };
        return Enum.IsDefined(typeof(LanguageFloorSmilingFaceMove), value)
            ? (LanguageFloorSmilingFaceMove)value
            : LanguageFloorSmilingFaceMove.Devour;
    }

    internal Creature? GetPlannedTarget(int slot)
    {
        int combatId = slot switch
        {
            0 => PlannedTargetOne,
            1 => PlannedTargetTwo,
            2 => PlannedTargetThree,
            _ => PlannedTargetFour
        };
        if (combatId < 0 || Creature.CombatState is not { } combatState)
        {
            return null;
        }

        return combatState.Creatures.FirstOrDefault(target =>
            target.IsAlive
            && target.CombatId == (uint)combatId);
    }

    private void SetPlannedMove(
        int slot,
        LanguageFloorSmilingFaceMove? move)
    {
        int value = move == null ? -1 : (int)move.Value;
        switch (slot)
        {
            case 0:
                PlannedMoveOne = value;
                break;
            case 1:
                PlannedMoveTwo = value;
                break;
            case 2:
                PlannedMoveThree = value;
                break;
            default:
                PlannedMoveFour = value;
                break;
        }
    }

    private void SetPlannedTarget(int slot, Creature? target)
    {
        int value = target?.CombatId is uint combatId
            ? checked((int)combatId)
            : -1;
        switch (slot)
        {
            case 0:
                PlannedTargetOne = value;
                break;
            case 1:
                PlannedTargetTwo = value;
                break;
            case 2:
                PlannedTargetThree = value;
                break;
            default:
                PlannedTargetFour = value;
                break;
        }
    }

    private void RefreshPlannedIntents()
    {
        RefreshIntentArray(_formOnePlannedIntents);
        RefreshIntentArray(_formTwoPlannedIntents);
        RefreshIntentArray(_formThreePlannedIntents);
    }

    private void RefreshIntentArray(AbstractIntent[]? intents)
    {
        if (intents == null)
        {
            return;
        }

        for (int slot = 0; slot < intents.Length; slot++)
        {
            int plannedSlot = slot;
            intents[slot] = CreateIntent(
                GetPlannedMove(slot),
                _ => GetPlannedTarget(plannedSlot));
        }
    }

    private MoveState GetCurrentCompositeState() => Form switch
    {
        LanguageFloorSmilingFaceForm.First => _formOneCompositeState!,
        LanguageFloorSmilingFaceForm.Second => _formTwoCompositeState!,
        _ => _formThreeCompositeState!
    };

    private bool HasPlannedTurn => Enumerable.Range(0, GetIntentCapacity(Form))
        .All(slot => slot switch
        {
            0 => PlannedMoveOne >= 0,
            1 => PlannedMoveTwo >= 0,
            2 => PlannedMoveThree >= 0,
            _ => PlannedMoveFour >= 0
        });

    internal IReadOnlyList<LanguageFloorSmilingFaceMove> PlannedMoves =>
        Enumerable.Range(0, GetIntentCapacity(Form))
            .Select(GetPlannedMove)
            .ToArray();

    internal IReadOnlyList<Creature?> PlannedTargets =>
        Enumerable.Range(0, GetIntentCapacity(Form))
            .Select(GetPlannedTarget)
            .ToArray();

    private MoveState GetMoveState(string id) => id switch
    {
        DevourMoveId => _devourState!,
        AbsorbMoveId => _absorbState!,
        SitMoveId => _sitState!,
        ScreamMoveId => _screamState!,
        VomitMoveId => _vomitState!,
        _ => _devourState!
    };

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        foreach (LanguageFloorSmilingFaceMove move
                 in Enum.GetValues<LanguageFloorSmilingFaceMove>())
        {
            yield return CreateIntent(move);
        }
    }

    private static AbstractIntent CreateIntent(
        LanguageFloorSmilingFaceMove move,
        Func<Creature, Creature?>? targetResolver = null) => move switch
    {
        LanguageFloorSmilingFaceMove.Devour =>
            new BadgedTargetedAttackIntent(
                () => GetMoveDamage(move),
                () => DevourHits,
                "LANGUAGE_FLOOR_SMILING_FACE_DEVOUR.description",
                "LANGUAGE_FLOOR_SMILING_FACE_DEVOUR_PLAYER.description",
                true,
                targetResolver,
                IntentBadge.Heal(DevourHealPercentPerHit)),
        LanguageFloorSmilingFaceMove.Absorb =>
            new CombinedTargetedAttackBuffIntent(
                () => GetMoveDamage(move),
                () => 1,
                "LANGUAGE_FLOOR_SMILING_FACE_ABSORB.description",
                "LANGUAGE_FLOOR_SMILING_FACE_ABSORB_PLAYER.description",
                true,
                targetResolver,
                IntentBadge.NextTurnStrength(AbsorbNextTurnStrength)),
        LanguageFloorSmilingFaceMove.Sit =>
            new IndiscriminateAttackIntent(
                () => GetMoveDamage(move),
                () => SitHits,
                "LANGUAGE_FLOOR_SMILING_FACE_SIT.description",
                LanguageFloorLiberationCombatHelper.GetLivingPlayersAndCorpses,
                IntentBadge.FromPower<FrailPower>(SitVulnerable)),
        LanguageFloorSmilingFaceMove.Scream =>
            new MultiAttackIntent(GetMoveDamage(move), ScreamHits),
        _ => new IndiscriminateAttackIntent(
            () => GetMoveDamage(move),
            () => 1,
            "LANGUAGE_FLOOR_SMILING_FACE_VOMIT.description",
            LanguageFloorLiberationCombatHelper.GetLivingPlayersAndCorpses,
            IntentBadge.RapidWear(VomitDebuffAmount, VomitDebuffTurns),
            IntentBadge.FromPower<LibraryWeakPower>(
                VomitDebuffAmount,
                 VomitDebuffTurns.ToString(),
                 VomitDebuffAmount.ToString()),
            IntentBadge.Flaw(VomitDebuffAmount, VomitDebuffTurns))
    };

}
