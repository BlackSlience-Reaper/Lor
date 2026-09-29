using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.SmilingBodies;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.SmilingBodies;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.SmilingBodies;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.SmilingBodies;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.SmilingBodies;

public enum SmilingBodiesPhase
{
    First,
    Second,
    Third
}

public sealed class SmilingBodies : LorMonsterModel, ITargetedMonsterAttackProvider
{
    public const int MaxCorpseCount = 1;

    public const string AbsorbMoveId = "ABSORB";
    public const string ScreamMoveId = "SCREAM";
    public const string SitMoveId = "SIT";
    public const string VomitMoveId = "VOMIT";
    public const string ReviveMoveId = "REVIVE";

    public const string Root = "res://images/monsters/smiling_bodies/";
    public const string SfxRoot = "res://audio/sfx/smiling_bodies/";

    public const string Phase1IdleTexturePath = Root + "phase_1_idle.png";
    public const string Phase1AbsorbTexturePath = Root + "phase_1_absorb.png";
    public const string Phase1HitTexturePath = Root + "phase_1_hit.png";

    public const string Phase2IdleTexturePath = Root + "phase_2_idle.png";
    public const string Phase2AbsorbTexturePath = Root + "phase_2_absorb.png";
    public const string Phase2ScreamTexturePath = Root + "phase_2_scream.png";
    public const string Phase2HitTexturePath = Root + "phase_2_hit.png";

    public const string Phase3IdleTexturePath = Root + "phase_3_idle.png";
    public const string Phase3AbsorbTexturePath = Root + "phase_3_absorb.png";
    public const string Phase3SitTexturePath = Root + "phase_3_sit.png";
    public const string Phase3VomitTexturePath = Root + "phase_3_vomit.png";
    public const string Phase3HitTexturePath = Root + "phase_3_hit.png";

    public const string AbsorbHitSfxPath = SfxRoot + "absorb_hit.ogg";
    public const string Phase2ScreamSfxPath = SfxRoot + "phase_2_scream.ogg";
    public const string Phase3SitHitSfxPath = SfxRoot + "phase_3_sit_hit.ogg";
    public const string Phase3VomitSfxPath = SfxRoot + "phase_3_vomit.ogg";
    public const string PhaseDownSfxPath = SfxRoot + "phase_down.ogg";
    public const string PhaseUpSfxPath = SfxRoot + "phase_up.ogg";

    private const int AbsorbBaseDamage = 8;
    private const int AbsorbHighAscensionDamage = 10;
    private const int AbsorbHits = 2;
    private const int AbsorbHealPerUnblockedHit = 12;
    private const decimal AbsorbCorpseKillHealRatio = 0.50m;

    private const int ScreamBaseDamage = 10;
    private const int ScreamHighAscensionDamage = 13;
    private const int ScreamHits = 2;
    private const int ScreamWeak = 2;

    private const int SitBaseDamage = 10;
    private const int SitHighAscensionDamage = 12;
    private const int SitConfusion = 1;
    private const int SitHeal = 40;

    private const int VomitBaseDamage = 13;
    private const int VomitHighAscensionDamage = 16;
    private const int VomitVulnerable = 3;
    private const int VomitWeak = 2;
    private const int VomitDisarm = 4;
    private const int VomitTurns = 1;

    private static readonly string[] CorpseSlots =
    [
        SmilingBodiesStrong.CorpseSlotOne,
        SmilingBodiesStrong.CorpseSlotTwo,
        SmilingBodiesStrong.CorpseSlotThree,
        SmilingBodiesStrong.CorpseSlotFour
    ];

    private SmilingBodiesPhase _phase = SmilingBodiesPhase.Second;
    private int _pendingCorpseSpawns;
    private int _hpAtLastSpawnThreshold;
    private bool _isTransitioningAfterPreventedDeath;
    private bool _isApplyingPhaseHp;
    private bool _isPromoting;
    private bool _isWaitingForRevive;
    private bool _isForceKillable;
    private int _preventDeathCount;
    private MoveState? _reviveState;

    private static readonly string PageRelicTitleLocKey =
        $"{ModelDb.GetId<SmilingBodiesPageRelic>().Entry}.title";

    public static readonly string[] PowerIconPaths =
    [
        "res://images/powers/smiling_bodies_find_corpses_power.png",
        "res://images/powers/smiling_bodies_dissolving_corpses_power.png",
        "res://images/powers/smiling_bodies_split_power.png",
        "res://images/powers/smiling_bodies_split_and_fusion_power.png",
        "res://images/powers/smiling_bodies_fusion_power.png",
        "res://images/powers/smiling_bodies_scream_power.png",
        "res://images/powers/smiling_bodies_vomit_power.png"
    ];

    public static readonly string[] AssetPathsStatic =
        new[]
            {
                SmilingBodiesCreatureVisuals.ScenePath,
                Phase1IdleTexturePath,
                Phase1AbsorbTexturePath,
                Phase1HitTexturePath,
                Phase2IdleTexturePath,
                Phase2AbsorbTexturePath,
                Phase2ScreamTexturePath,
                Phase2HitTexturePath,
                Phase3IdleTexturePath,
                Phase3AbsorbTexturePath,
                Phase3SitTexturePath,
                Phase3VomitTexturePath,
                Phase3HitTexturePath
            }
            .Concat(
            [
                AbsorbHitSfxPath,
                Phase2ScreamSfxPath,
                Phase3SitHitSfxPath,
                Phase3VomitSfxPath,
                PhaseDownSfxPath,
                PhaseUpSfxPath
            ])
            .Concat(PowerIconPaths)
            .ToArray();

    public SmilingBodiesPhase Phase => _phase;

    public bool IsTransitioningAfterPreventedDeath => _isTransitioningAfterPreventedDeath;

    public bool IsWaitingForRevive => _isWaitingForRevive;

    public bool IsForceKillable => _isForceKillable;

    public bool IsFakeDead => !_isForceKillable && _isWaitingForRevive;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp => PhaseMaxHp(_phase);

    public override int MaxInitialHp => PhaseMaxHp(_phase);

    public override int DefaultChaoResistance => PhaseChao(_phase);

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Normal
    };

    private static int AbsorbDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            AbsorbHighAscensionDamage,
            AbsorbBaseDamage);

    private static int ScreamDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            ScreamHighAscensionDamage,
            ScreamBaseDamage);

    private static int SitDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, SitHighAscensionDamage, SitBaseDamage);

    private static int VomitDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, VomitHighAscensionDamage, VomitBaseDamage);

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public static string IdleTexturePathForPhase(SmilingBodiesPhase phase) => phase switch
    {
        SmilingBodiesPhase.First => Phase1IdleTexturePath,
        SmilingBodiesPhase.Second => Phase2IdleTexturePath,
        _ => Phase3IdleTexturePath
    };

    public static string AbsorbTexturePathForPhase(SmilingBodiesPhase phase) => phase switch
    {
        SmilingBodiesPhase.First => Phase1AbsorbTexturePath,
        SmilingBodiesPhase.Second => Phase2AbsorbTexturePath,
        _ => Phase3AbsorbTexturePath
    };

    public static string HitTexturePathForPhase(SmilingBodiesPhase phase) => phase switch
    {
        SmilingBodiesPhase.First => Phase1HitTexturePath,
        SmilingBodiesPhase.Second => Phase2HitTexturePath,
        _ => Phase3HitTexturePath
    };

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await ApplyPhaseStats(_phase);
        ResetCorpseSpawnThreshold();
        await RefreshPhasePowers();
        EncounterBgmController.RegisterMonster(Creature);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _reviveState = new LibraryPhaseTransitionMoveState(
            ReviveMoveId,
            ReviveMove,
            new HealIntent(),
            new SummonIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var absorb = new MoveState(
            AbsorbMoveId,
            AbsorbMove,
            CreateAbsorbIntent());

        if (_phase == SmilingBodiesPhase.First)
        {
            absorb.FollowUpState = absorb;
            _reviveState.FollowUpState = absorb;
            return new MonsterMoveStateMachine([absorb, _reviveState], absorb);
        }

        if (_phase == SmilingBodiesPhase.Second)
        {
            var scream = new MoveState(
                ScreamMoveId,
                ScreamMove,
                CreateScreamIntent());

            var chooser = new RandomBranchState("PHASE_2_RANDOM");
            chooser.AddBranch(absorb, MoveRepeatType.CannotRepeat);
            chooser.AddBranch(scream, MoveRepeatType.CannotRepeat);
            absorb.FollowUpState = chooser;
            scream.FollowUpState = chooser;
            _reviveState.FollowUpState = chooser;

            return new MonsterMoveStateMachine([absorb, scream, chooser, _reviveState], chooser);
        }

        var sit = new MoveState(
            SitMoveId,
            SitMove,
            CreateSitIntent());

        var vomit = new MoveState(
            VomitMoveId,
            VomitMove,
            CreateVomitIntent());

        var phase3Chooser = new RandomBranchState("PHASE_3_RANDOM");
        phase3Chooser.AddBranch(absorb, MoveRepeatType.CannotRepeat);
        phase3Chooser.AddBranch(sit, MoveRepeatType.CannotRepeat);
        phase3Chooser.AddBranch(vomit, MoveRepeatType.CannotRepeat);
        absorb.FollowUpState = phase3Chooser;
        sit.FollowUpState = phase3Chooser;
        vomit.FollowUpState = phase3Chooser;
        _reviveState.FollowUpState = phase3Chooser;

        return new MonsterMoveStateMachine([absorb, sit, vomit, phase3Chooser, _reviveState], phase3Chooser);
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        return NextMove.Id is AbsorbMoveId or SitMoveId;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        Creature? target = NextMove.Id switch
        {
            AbsorbMoveId => ChooseAbsorbTarget(owner.CombatState?.PlayerCreatures ?? []),
            SitMoveId => ChoosePlayerTarget(owner.CombatState?.PlayerCreatures ?? []),
            _ => null
        };

        return target is { IsAlive: true }
            ? [target]
            : Array.Empty<Creature>();
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        return GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "Unknown Target";
    }

    public void QueueCorpseSpawnForThresholds()
    {
        if (_isApplyingPhaseHp || Creature.IsDead || Creature.MaxHp <= 0)
        {
            return;
        }

        int threshold = Math.Max(1, (int)Math.Ceiling(Creature.MaxHp * 0.25m));
        while (_hpAtLastSpawnThreshold - Creature.CurrentHp >= threshold)
        {
            _pendingCorpseSpawns++;
            _hpAtLastSpawnThreshold -= threshold;
        }
    }

    public async Task ResolvePendingCorpseSpawn(PlayerChoiceContext choiceContext, CombatStateLike combatState)
    {
        if (_pendingCorpseSpawns <= 0 || Creature.IsDead)
        {
            return;
        }

        int availableSlots = MaxCorpseCount - CountLivingCorpses(combatState);
        int spawnCount = Math.Min(_pendingCorpseSpawns, Math.Max(0, availableSlots));
        if (spawnCount <= 0)
        {
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            string? slot = NextOpenCorpseSlot(combatState);
            if (slot == null)
            {
                break;
            }

            Creature spawned = await CreatureCmd.Add(
                ModelDb.Monster<MeltingCorpse>().ToMutable(),
                combatState,
                CombatSide.Enemy,
                slot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);
            _pendingCorpseSpawns--;
        }

        combatState.SortEnemiesBySlotName();
    }

    public bool CanEnterFakeDeath(Creature creature)
    {
        return creature == Creature
            && !_isForceKillable
            && !_isWaitingForRevive
            && _phase != SmilingBodiesPhase.First
            && _preventDeathCount < 5;
    }

    public async Task EnterFakeDeathFromDeath()
    {
        if (!CanEnterFakeDeath(Creature))
        {
            _isForceKillable = true;
            return;
        }

        _preventDeathCount++;
        await EnterFakeDeath();
    }

    private async Task EnterFakeDeath()
    {
        if (!_isWaitingForRevive)
        {
            _isWaitingForRevive = true;
            _isTransitioningAfterPreventedDeath = true;
        }

        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        EnterReviveAndEmpowerIntent();
    }

    private void EnterReviveAndEmpowerIntent()
    {
        if (_reviveState != null)
        {
            SetMoveImmediate(_reviveState, forceTransition: true);
        }

        _isTransitioningAfterPreventedDeath = false;
    }

    public async Task TryPromoteAtFullHealth()
    {
        if (_isApplyingPhaseHp || _isPromoting || Creature.IsDead || Creature.CurrentHp < Creature.MaxHp)
        {
            return;
        }

        SmilingBodiesPhase? targetPhase = _phase switch
        {
            SmilingBodiesPhase.First => SmilingBodiesPhase.Second,
            SmilingBodiesPhase.Second => SmilingBodiesPhase.Third,
            _ => null
        };

        if (targetPhase == null)
        {
            return;
        }

        _isPromoting = true;
        try
        {
            await TransitionToPhase(targetPhase.Value, PhaseUpSfxPath);
        }
        finally
        {
            _isPromoting = false;
        }
    }

    public Task GainPhaseStrength(PlayerChoiceContext choiceContext, int amount)
    {
        return PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(choiceContext, Creature, amount, Creature, null);
    }

    private async Task TransitionToPhase(SmilingBodiesPhase targetPhase, string sfxPath)
    {
        if (_phase == targetPhase)
        {
            return;
        }

        SmilingBodiesPhase previousPhase = _phase;
        _phase = targetPhase;
        _pendingCorpseSpawns = 0;

        LocalOggOneShotPlayer.Play(sfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Phase",
            SmilingBodiesAnimationContract.PhaseDurationSeconds);

        CombatStateLike? activeCombatState = TryGetActiveCombatState();
        if (activeCombatState == null)
        {
            Log.Warn("[SmilingBodies] skipped phase transition follow-up because combat is no longer active.");
            return;
        }

        await KillOtherEnemies(activeCombatState);
        await ApplyPhaseStats(targetPhase);
        ResetMoveStateForCurrentPhase();
        ResetCorpseSpawnThreshold();
        await RefreshPhasePowers();
        await SpawnCorpsesForPhase(targetPhase);

        if (previousPhase != _phase && TryGetActiveCombatState() is { } prepareCombatState)
        {
            Creature.PrepareForNextTurn(prepareCombatState.PlayerCreatures);
        }
    }

    private async Task ApplyPhaseStats(SmilingBodiesPhase phase)
    {
        _isApplyingPhaseHp = true;
        try
        {
            decimal maxHp = MultiplayerScalingPatchHelper.ScaleHpAmount(
                Creature.CombatState,
                this,
                PhaseMaxHp(phase));
            decimal entryHp = MultiplayerScalingPatchHelper.ScaleHpAmount(
                Creature.CombatState,
                this,
                PhaseEntryHp(phase));
            await CreatureCmd.SetMaxHp(Creature, maxHp);
            await CreatureCmd.SetCurrentHp(Creature, Math.Min(Creature.MaxHp, entryHp));

            if (Creature is LibraryCreature libraryCreature)
            {
                await MultiplayerScalingPatchHelper.SetMonsterBaseMaxAndCurrentChaoValue(
                    libraryCreature,
                    PhaseChao(phase));
            }
        }
        finally
        {
            _isApplyingPhaseHp = false;
        }
    }

    private async Task RefreshPhasePowers()
    {
        await PowerCmdCompat.Ensure<SmilingBodiesFindCorpsesPower>(Creature);
        await PowerCmdCompat.Ensure<SmilingBodiesDissolvingCorpsesPower>(
            Creature);

        if (_phase == SmilingBodiesPhase.First)
        {
            await PowerCmdCompat.Ensure<SmilingBodiesSplitPower>(Creature);
            await PowerCmdCompat.Ensure<SmilingBodiesFusionPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                SmilingBodiesSplitAndFusionPower>(Creature);
        }
        else if (_phase == SmilingBodiesPhase.Second)
        {
            await PowerCmdCompat.Ensure<SmilingBodiesSplitAndFusionPower>(
                Creature);
            await PowerCmdCompat.RemoveIfPresent<SmilingBodiesSplitPower>(
                Creature);
            await PowerCmdCompat.RemoveIfPresent<SmilingBodiesFusionPower>(
                Creature);
        }
        else
        {
            await PowerCmdCompat.Ensure<SmilingBodiesSplitPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                SmilingBodiesSplitAndFusionPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<SmilingBodiesFusionPower>(
                Creature);
        }

        if (_phase == SmilingBodiesPhase.Second)
        {
            await PowerCmdCompat.Ensure<SmilingBodiesScreamPower>(Creature);
        }
        else
        {
            await PowerCmdCompat.RemoveIfPresent<SmilingBodiesScreamPower>(
                Creature);
        }

        if (_phase == SmilingBodiesPhase.Third)
        {
            await PowerCmdCompat.Ensure<SmilingBodiesVomitPower>(Creature);
        }
        else
        {
            await PowerCmdCompat.RemoveIfPresent<SmilingBodiesVomitPower>(
                Creature);
        }
    }

    private void ResetMoveStateForCurrentPhase()
    {
        ResetStateMachine();
        SetUpForCombat();
    }

    private void ResetCorpseSpawnThreshold()
    {
        _hpAtLastSpawnThreshold = Creature.CurrentHp;
    }

    private async Task KillOtherEnemies(CombatStateLike combatState)
    {
        IReadOnlyList<Creature> others = combatState.Enemies
            .Where(enemy => enemy != Creature && enemy.IsAlive)
            .ToArray();

        foreach (Creature enemy in others)
        {
            await CreatureCmd.Kill(enemy, force: true);
        }
    }

    private async Task SpawnCorpsesForPhase(SmilingBodiesPhase phase)
    {
        CombatStateLike? combatState = TryGetActiveCombatState();
        if (combatState == null)
        {
            Log.Warn("[SmilingBodies] skipped corpse spawn because combat is no longer active.");
            return;
        }

        int targetCount = phase == SmilingBodiesPhase.Third ? 0 : MaxCorpseCount;
        for (int i = 0; i < targetCount; i++)
        {
            string? slot = NextOpenCorpseSlot(combatState);
            if (slot == null)
            {
                break;
            }

            Creature spawned = await CreatureCmd.Add(
                ModelDb.Monster<MeltingCorpse>().ToMutable(),
                combatState,
                CombatSide.Enemy,
                slot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        }

        combatState.SortEnemiesBySlotName();
    }

    private CombatStateLike? TryGetActiveCombatState()
    {
        try
        {
            return CombatManager.Instance.IsInProgress && Creature.CombatState != null
                ? Creature.CombatState
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task AbsorbMove(IReadOnlyList<Creature> targets)
    {
        SmilingBodiesPhase startingPhase = _phase;
        Creature? target = TargetedMonsterAttackHelper.GetPrimaryTarget(Creature, LivingPlayers(targets));
        if (target == null)
        {
            return;
        }

        using var lungeScope = new TargetedAttackLungeScope(this, [target]);
        for (int i = 0; i < AbsorbHits; i++)
        {
            if (Creature.IsDead || target.IsDead)
            {
                return;
            }

            LocalOggOneShotPlayer.Play(AbsorbHitSfxPath, -2f);
            await CreatureCmd.TriggerAnim(
                Creature,
                "Absorb",
                SmilingBodiesAnimationContract.ActionDurationSeconds);

            IReadOnlyList<DamageResult> results = (await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                target,
                AbsorbDamage,
                ValueProp.Move,
                Creature,
                null)).ToArray();

            if (results.Any(static result => result.UnblockedDamage > 0))
            {
                await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, AbsorbHealPerUnblockedHit));
            }

            if (target.Monster is MeltingCorpse && results.Any(static result => result.WasTargetKilled))
            {
                int healAmount = Math.Max(1, (int)Math.Ceiling(Creature.MaxHp * AbsorbCorpseKillHealRatio));
                await CreatureCmd.Heal(Creature, healAmount);
                return;
            }

            if (_phase != startingPhase)
            {
                return;
            }
        }
    }

    private async Task ScreamMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(Phase2ScreamSfxPath, -2f);

        for (int i = 0; i < ScreamHits; i++)
        {
            if (Creature.IsDead) return;
            await DamageCmd.Attack(ScreamDamage)
                .FromMonster(this)
                .WithAttackerAnim(
                    "Scream",
                    SmilingBodiesAnimationContract.ActionDurationSeconds)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);
        }

        await PowerCmdCompat.ApplyDebuff<WeakPower>(
            LivingPlayers(targets),
            ScreamWeak,
            Creature,
            null);
    }

    private async Task SitMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = TargetedMonsterAttackHelper.GetPrimaryTarget(Creature, LivingPlayers(targets));
        if (target == null)
        {
            return;
        }

        using var lungeScope = new TargetedAttackLungeScope(this, [target]);
        LocalOggOneShotPlayer.Play(Phase3SitHitSfxPath, -2f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Sit",
            SmilingBodiesAnimationContract.ActionDurationSeconds);

        await CreatureCmdCompat.Damage(
            new ThrowingPlayerChoiceContext(),
            target,
            SitDamage,
            ValueProp.Move,
            Creature,
            null);
        await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaConfusionPower>(
            target,
            SitConfusion,
            Creature,
            null);
        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, SitHeal));
    }

    private async Task VomitMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(Phase3VomitSfxPath, -2f);

        await DamageCmd.Attack(VomitDamage)
            .FromMonster(this)
            .WithAttackerAnim(
                "Vomit",
                SmilingBodiesAnimationContract.ActionDurationSeconds)
            .WithHitFx("vfx/vfx_bloody_impact")
            .Execute(null);

        IReadOnlyList<Creature> livingPlayers = LivingPlayers(targets);
        foreach (Creature target in livingPlayers)
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                VomitVulnerable,
                VomitTurns,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                target,
                VomitWeak,
                VomitTurns,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(
                target,
                VomitDisarm,
                VomitTurns,
                Creature,
                null);
        }
    }

    private async Task ReviveMove(IReadOnlyList<Creature> targets)
    {
        _isWaitingForRevive = false;

        SmilingBodiesPhase targetPhase = _phase == SmilingBodiesPhase.Third
            ? SmilingBodiesPhase.Second
            : SmilingBodiesPhase.First;

        _isTransitioningAfterPreventedDeath = true;
        try
        {
            await TransitionToPhase(targetPhase, PhaseDownSfxPath);
        }
        finally
        {
            _isTransitioningAfterPreventedDeath = false;
        }
    }

    private Creature? ChooseAbsorbTarget(IReadOnlyList<Creature> targets)
    {
        return CombatState.Enemies
            .Where(enemy => enemy != Creature && enemy.IsAlive && enemy.Monster is MeltingCorpse)
            .OrderBy(static enemy => enemy.CurrentHp)
            .ThenBy(static enemy => enemy.SlotName)
            .FirstOrDefault()
            ?? ChoosePlayerTarget(targets);
    }

    private Creature? ChoosePlayerTarget(IReadOnlyList<Creature> targets)
    {
        return LivingPlayers(targets)
            .OrderBy(static target => target.Player?.NetId ?? 0UL)
            .FirstOrDefault();
    }

    private static IReadOnlyList<Creature> LivingPlayers(IEnumerable<Creature> targets)
    {
        return targets.Where(static target => target.IsAlive && target.IsPlayer).ToArray();
    }

    private static int PhaseMaxHp(SmilingBodiesPhase phase) => phase switch
    {
        SmilingBodiesPhase.First => 200,
        SmilingBodiesPhase.Second => 250,
        _ => 300
    };

    private static int PhaseChao(SmilingBodiesPhase phase) => phase switch
    {
        SmilingBodiesPhase.First => 70,
        SmilingBodiesPhase.Second => 90,
        _ => 100
    };

    private static int PhaseEntryHp(SmilingBodiesPhase phase) => phase switch
    {
        SmilingBodiesPhase.First => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 160, 155),
        SmilingBodiesPhase.Second => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 190, 185),
        _ => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 210, 205)
    };

    private static int CountLivingCorpses(CombatStateLike combatState)
    {
        return combatState.Enemies.Count(static enemy => enemy.IsAlive && enemy.Monster is MeltingCorpse);
    }

    private static string? NextOpenCorpseSlot(CombatStateLike combatState)
    {
        return CorpseSlots.FirstOrDefault(slot =>
            combatState.Enemies.All(enemy => enemy.SlotName != slot || !enemy.IsAlive));
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateAbsorbIntent();
        yield return CreateScreamIntent();
        yield return CreateSitIntent();
        yield return CreateVomitIntent();
    }

    private AbstractIntent CreateAbsorbIntent()
    {
        return new BadgedTargetedAttackIntent(
            () => AbsorbDamage,
            () => AbsorbHits,
            "SMILING_BODIES_ABSORB.description",
            "SMILING_BODIES_ABSORB_PLAYER.description",
            true,
            IntentBadge.Heal(() => ScaledHealBadgeAmount(AbsorbHealPerUnblockedHit)));
    }

    private static AbstractIntent CreateScreamIntent()
    {
        return new BadgedAttackIntent(
            () => ScreamDamage,
            () => ScreamHits,
            "SMILING_BODIES_SCREAM.description",
            IntentBadge.Weak(ScreamWeak));
    }

    private AbstractIntent CreateSitIntent()
    {
        return new BadgedTargetedAttackIntent(
            () => SitDamage,
            () => 1,
            "SMILING_BODIES_SIT.description",
            "SMILING_BODIES_SIT_PLAYER.description",
            true,
            IntentBadge.Confusion(SitConfusion),
            IntentBadge.Heal(() => ScaledHealBadgeAmount(SitHeal)));
    }

    private int ScaledHealBadgeAmount(int amount) =>
        (int)MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, amount);

    private static AbstractIntent CreateVomitIntent()
    {
        return new BadgedAttackIntent(
            () => VomitDamage,
            null,
            "SMILING_BODIES_VOMIT.description",
            IntentBadge.RapidWear(VomitVulnerable, VomitTurns),
            IntentBadge.FromPower<LibraryWeakPower>(
                VomitWeak,
                 VomitTurns.ToString(),
                 VomitWeak.ToString()),
            IntentBadge.Flaw(VomitDisarm, VomitTurns));
    }

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !SmilingBodiesEncounterHelper.IsSmilingBodiesEncounter(deadCreature.CombatState))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<SmilingBodiesPageRelic>(room, PageRelicTitleLocKey);
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
