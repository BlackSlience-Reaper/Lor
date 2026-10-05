using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.BigBadWolf;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
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
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

public sealed class WolfInHerNightmares : CounterIntentMonsterModel, ITargetedMonsterAttackProvider
{
    private const string CruelClawsMoveId = "CRUEL_CLAWS";
    private const string BloodstainedHuntMoveId = "BLOODSTAINED_HUNT";
    private const string FerociousFangsMoveId = "FEROCIOUS_FANGS";
    private const string HowlMoveId = "HOWL";
    private const string CruelClawsWithHowlMoveId = "CRUEL_CLAWS_WITH_HOWL";
    private const string BloodstainedHuntWithHowlMoveId = "BLOODSTAINED_HUNT_WITH_HOWL";
    private const string FerociousFangsWithHowlMoveId = "FEROCIOUS_FANGS_WITH_HOWL";
    private const string LittleRedFinaleExplodeMoveId = "LITTLE_RED_FINALE_EXPLODE";
    private const string LittleRedFinaleStunnedMoveId = stunnedMoveId;
    private const int InfiniteFinaleHp = 999999999;

    private const int CruelClawsHits = 2;
    private const int CruelClawsVulnerable = 3;
    private const int BloodstainedHuntBaseHits = 2;
    private const int BloodstainedHuntPhaseTwoHits = 3;
    private const int BloodstainedHuntBleed = 4;
    private const int FerociousFangsBlock = 18;
    private const int FerociousFangsHeal = 22;
    private const int HowlFrail = 3;
    public const int PhaseTwoNextTurnStrength = 3;
    public const int LittleRedFinaleDamagePercent = 100;
    private const int PhaseTwoHpNumerator = WolfPhaseTwoThreshold.PhaseTwoHpNumerator;
    private const int PhaseTwoHpDenominator = 10;

    private MoveState? _cruelClawsState;
    private MoveState? _bloodstainedHuntState;
    private MoveState? _ferociousFangsState;
    private MoveState? _cruelClawsWithHowlState;
    private MoveState? _bloodstainedHuntWithHowlState;
    private MoveState? _ferociousFangsWithHowlState;
    private MoveState? _howlState;
    private MoveState? _littleRedFinaleExplodeState;
    private int _baseMoveStep;
    private int _baseMovesSinceHowl;
    private bool _phaseTwoEntered;
    private bool _forceHowl;
    private bool _littleRedDeathFinaleActive;
    private int _littleRedFinaleDamage;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 625, 620);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 630, 625);

    public override int DefaultChaoResistance => 290;

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

    public bool IsPhaseTwo =>
        _phaseTwoEntered || Creature.CurrentHp * PhaseTwoHpDenominator <= Creature.MaxHp * PhaseTwoHpNumerator;

    private int CruelClawsDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 16, 13);

    private int BloodstainedHuntDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 15, 11);

    private int FerociousFangsDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 32, 27);

    private int HowlDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 34, 29);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(16)
            {
                WolfInHerNightmaresCreatureVisuals
                    .ScenePath
            };
            paths.AddRange(CounterIntentVisuals.AssetPaths);

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        _baseMoveStep = 0;
        _baseMovesSinceHowl = 0;
        _phaseTwoEntered = false;
        _forceHowl = false;
        _littleRedDeathFinaleActive = false;
        _littleRedFinaleDamage = 0;
        LittleRedDeathContext.Clear();

        await PowerCmdCompat.Apply<LittleRedNightmareEndPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LittleRedMercenaryDamageTrackerPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<WolfHowlPassivePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LibraryOfRuinaFocusOfAttentionPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource);

        if (target == Creature)
        {
            await EnsurePhaseTwo();
        }
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        _cruelClawsState = new MoveState(
            CruelClawsMoveId,
            CruelClawsMove,
            CreateCruelClawsIntent(),
            new DebuffIntent());

        _bloodstainedHuntState = new MoveState(
            BloodstainedHuntMoveId,
            BloodstainedHuntMove,
            CreateBloodstainedHuntIntent(),
            new DebuffIntent());

        _ferociousFangsState = new MoveState(
            FerociousFangsMoveId,
            FerociousFangsMove,
            CreateFerociousFangsAttackIntent(),
            new DefendIntent(),
            new HealIntent());

        _cruelClawsWithHowlState = new MoveState(
            CruelClawsWithHowlMoveId,
            CruelClawsMove,
            CreateCruelClawsIntent(),
            CreateHowlIntent());

        _bloodstainedHuntWithHowlState = new MoveState(
            BloodstainedHuntWithHowlMoveId,
            BloodstainedHuntMove,
            CreateBloodstainedHuntIntent(),
            CreateHowlIntent());

        _ferociousFangsWithHowlState = new MoveState(
            FerociousFangsWithHowlMoveId,
            FerociousFangsMove,
            CreateFerociousFangsAttackIntent(),
            new DefendIntent(),
            new HealIntent(),
            CreateHowlIntent());

        _howlState = new MoveState(
            HowlMoveId,
            HowlMove,
            CreateHowlIntent());

        _littleRedFinaleExplodeState = new MoveState(
            LittleRedFinaleExplodeMoveId,
            LittleRedFinaleExplodeMove,
            CreateLittleRedFinaleIntent());

        var chooser = new DelegatingMonsterRouterState(
            "WOLF_IN_HER_NIGHTMARES_ROUTER",
            (_, _) => ResolvePlannedMoveId());

        foreach (MoveState move in new[]
        {
            _cruelClawsState,
            _bloodstainedHuntState,
            _ferociousFangsState,
            _cruelClawsWithHowlState,
            _bloodstainedHuntWithHowlState,
            _ferociousFangsWithHowlState,
            _howlState,
            _littleRedFinaleExplodeState
        })
        {
            move.FollowUpState = chooser;
            states.Add(move);
        }
        _littleRedFinaleExplodeState.FollowUpState = _littleRedFinaleExplodeState;

        states.Add(chooser);
        return new MonsterMoveStateMachine(states, chooser);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateCruelClawsIntent();
        yield return CreateBloodstainedHuntIntent();
        yield return CreateFerociousFangsAttackIntent();
        yield return new DefendIntent();
        yield return new HealIntent();
        yield return CreateHowlIntent();
        yield return CreateLittleRedFinaleIntent();
    }

    private AbstractIntent CreateCruelClawsIntent()
    {
        return new BadgedTargetedAttackIntent(
            () => CruelClawsDamage,
            () => CruelClawsHits,
            "WOLF_CRUEL_CLAWS.description",
            IntentBadge.Vulnerable(CruelClawsVulnerable));
    }

    private AbstractIntent CreateBloodstainedHuntIntent()
    {
        return new BadgedTargetedAttackIntent(
            () => BloodstainedHuntDamage,
            () => IsPhaseTwo ? BloodstainedHuntPhaseTwoHits : BloodstainedHuntBaseHits,
            "WOLF_BLOODSTAINED_HUNT.description",
            IntentBadge.Bleed(BloodstainedHuntBleed));
    }

    private AbstractIntent CreateFerociousFangsAttackIntent()
    {
        return new TargetedMonsterAttackIntent(() => FerociousFangsDamage, () => 1, "WOLF_FEROCIOUS_FANGS.description");
    }

    private AbstractIntent CreateHowlIntent()
    {
        return new WolfHowlIntent(
            () => HowlDamage,
            HowlFrail,
            GetHowlTargets);
    }

    private AbstractIntent CreateLittleRedFinaleIntent()
    {
        return new DeathBlowIntent(() => Math.Max(0, _littleRedFinaleDamage));
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        return true;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        if (NextMove.Id == LittleRedFinaleExplodeMoveId)
        {
            return owner.CombatState?.LivingPlayerCreatures()
                .ToArray()
                ?? Array.Empty<Creature>();
        }

        if (NextMove.Id == HowlMoveId)
        {
            return GetHowlTargets(owner);
        }

        Creature? littleRed = LittleRedMercenaryEncounterHelper.FindLittleRed(owner.CombatState);
        if (littleRed != null)
        {
            return [littleRed];
        }

        return owner.CombatState?.LivingPlayerCreatures().Take(1).ToArray()
            ?? Array.Empty<Creature>();
    }

    private static IReadOnlyList<Creature> GetHowlTargets(Creature owner)
    {
        return owner.CombatState?.Creatures
            .Where(creature => creature.IsAlive
                && creature != owner
                && (creature.IsPlayer || creature.Monster is LittleRedRidingHoodedMercenary))
            .ToArray()
            ?? Array.Empty<Creature>();
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        if (NextMove.Id == LittleRedFinaleExplodeMoveId)
        {
            return "All Players";
        }

        return GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "Unknown Target";
    }

    public async Task TriggerLittleRedDeathFinale()
    {
        if (_littleRedDeathFinaleActive || Creature.IsDead || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _littleRedDeathFinaleActive = true;
        foreach (PowerModel power in Creature.Powers.ToArray())
        {
            await PowerCmd.Remove(power);
        }
        int highestPlayerMaxHp = Creature.CombatState?.PlayerCreatures
            .Select(static creature => creature.MaxHp)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        _littleRedFinaleDamage = Math.Max(0, (int)Math.Ceiling(highestPlayerMaxHp * LittleRedFinaleDamagePercent / 100m));
        await CreatureCmd.SetMaxAndCurrentHp(Creature, InfiniteFinaleHp);
        Creature.HpDisplay = HpDisplay.InfiniteWithoutNumbers;
        ForceLittleRedFinaleStun();
        await RefreshIntents();
    }

    private void ForceLittleRedFinaleStun()
    {
        var stunned = new MoveState(
            LittleRedFinaleStunnedMoveId,
            static _ => Task.CompletedTask,
            new StunIntent())
        {
            FollowUpStateId = LittleRedFinaleExplodeMoveId,
            MustPerformOnceBeforeTransitioning = true
        };

        SetMoveImmediate(stunned, forceTransition: true);
        MoveStateMachine?.ForceCurrentState(stunned);
    }

    private async Task CruelClawsMove(IReadOnlyList<Creature> targets)
    {
        await EnsurePhaseTwo();
        Creature? target = ResolveTarget();
        if (!CanContinueNormalMove || target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_claw.ogg");
        int unblockedHitsOnLittleRed = 0;
        for (int i = 0; i < CruelClawsHits; i++)
        {
            LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_claw.ogg");
            AttackCommand segment = await ExecuteTargetedAttackSegment(target, CruelClawsDamage, "Slash");
            if (!CanContinueNormalMove)
            {
                return;
            }

            unblockedHitsOnLittleRed += CountUnblockedHitsOnLittleRed(AttackCommandCompat.Results(segment));
        }
        if (target.IsAlive)
        {
            await PowerCmdCompat.Apply<VulnerablePower>(target, CruelClawsVulnerable, Creature, null);
        }

        await HandleLittleRedAngerAfterWolfAttack(unblockedHitsOnLittleRed, isHowl: false);
        await AfterBaseMove();
    }

    private async Task BloodstainedHuntMove(IReadOnlyList<Creature> targets)
    {
        await EnsurePhaseTwo();
        Creature? target = ResolveTarget();
        if (!CanContinueNormalMove || target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_bite.ogg");
        int hits = IsPhaseTwo ? BloodstainedHuntPhaseTwoHits : BloodstainedHuntBaseHits;
        int unblockedHitsOnLittleRed = 0;
        bool anyUnblockedOnTarget = false;
        for (int i = 0; i < hits; i++)
        {
            LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_bite.ogg");
            AttackCommand segment = await ExecuteTargetedAttackSegment(target, BloodstainedHuntDamage, "Thrust");
            if (!CanContinueNormalMove)
            {
                return;
            }

            anyUnblockedOnTarget |= AttackCommandCompat.Results(segment).Any(result => result.Receiver == target && result.UnblockedDamage > 0);
            unblockedHitsOnLittleRed += CountUnblockedHitsOnLittleRed(AttackCommandCompat.Results(segment));
        }

        if (target.IsAlive && anyUnblockedOnTarget)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(target, BloodstainedHuntBleed, Creature, null);
        }

        await HandleLittleRedAngerAfterWolfAttack(unblockedHitsOnLittleRed, isHowl: false);
        await AfterBaseMove();
    }

    private async Task FerociousFangsMove(IReadOnlyList<Creature> targets)
    {
        await EnsurePhaseTwo();
        Creature? target = ResolveTarget();
        if (!CanContinueNormalMove || target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_bite.ogg");
        AttackCommand attack = await ExecuteTargetedAttackSegment(target, FerociousFangsDamage, "Thrust");
        if (!CanContinueNormalMove)
        {
            return;
        }

        await CreatureCmd.GainBlock(Creature, FerociousFangsBlock, ValueProp.Move, null);
        if (!CanContinueNormalMove)
        {
            return;
        }

        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, FerociousFangsHeal));
        await HandleLittleRedAngerAfterWolfAttack(CountUnblockedHitsOnLittleRed(AttackCommandCompat.Results(attack)), isHowl: false);
        await AfterBaseMove();
    }

    private async Task HowlMove(IReadOnlyList<Creature> targets)
    {
        await EnsurePhaseTwo();
        if (!CanContinueNormalMove)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_howl.ogg");
        IReadOnlyList<Creature> actualTargets = GetHowlTargets(Creature);
        if (actualTargets.Count == 0)
        {
            _forceHowl = false;
            return;
        }

        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, HowlDamage, actualTargets);
        using var forcedTargetScope = TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets);
        AttackCommand attack = await DamageCmd.Attack(HowlDamage)
            .FromMonster(this)
            .WithAttackerAnim("Howl", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .SpawningHitVfxOnEachCreature()
            .WithIndiscriminateBlockBreak(this, HowlDamage, actualTargets)
            .Execute(null);

        if (!CanContinueNormalMove)
        {
            return;
        }

        foreach (Creature target in actualTargets.Where(static creature => creature.IsAlive))
        {
            await PowerCmdCompat.Apply<FrailPower>(target, HowlFrail, Creature, null);
            if (!CanContinueNormalMove)
            {
                return;
            }
        }

        await HandleLittleRedAngerAfterWolfAttack(CountUnblockedHitsOnLittleRed(AttackCommandCompat.Results(attack)), isHowl: true);
        _baseMovesSinceHowl = 0;
        _forceHowl = false;
    }

    private async Task LittleRedFinaleExplodeMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> actualTargets = Creature.CombatState?.LivingPlayerCreatures()
            .ToArray()
            ?? Array.Empty<Creature>();

        int highestPlayerMaxHp = Creature.CombatState?.PlayerCreatures
            .Select(static creature => creature.MaxHp)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        if (highestPlayerMaxHp > 0)
        {
            _littleRedFinaleDamage = Math.Max(
                0,
                (int)Math.Ceiling(highestPlayerMaxHp * LittleRedFinaleDamagePercent / 100m));
        }

        if (actualTargets.Count > 0)
        {
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, _littleRedFinaleDamage, actualTargets);
            using var forcedTargetScope = TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets);
            await DamageCmd.Attack(_littleRedFinaleDamage)
                .FromMonster(this)
                .WithNoAttackerAnim()
                .WithHitFx("vfx/vfx_attack_slash")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, _littleRedFinaleDamage, actualTargets)
                .Execute(null);
        }

        if (Creature.CombatState?.Players.All(static player => player.Creature.IsDead) == true)
        {
            return;
        }

        await CreatureCmd.Kill(Creature, force: true);
    }

    // 普通招式中小红帽死亡会立即进入终幕；停止旧招式，保留终幕的清空能力与眩晕状态。
    private bool CanContinueNormalMove =>
        !_littleRedDeathFinaleActive
        && Creature.IsAlive
        && Creature.CombatState != null
        && !CombatManager.Instance.IsOverOrEnding;

    private Creature? ResolveTarget()
    {
        return TargetedMonsterAttackHelper.GetPrimaryTarget(Creature);
    }

    private async Task<AttackCommand> ExecuteTargetedAttackSegment(
        Creature target,
        int damage,
        string animation)
    {
        using var scope = new TargetedAttackLungeScope(this, [target]);
        using var forcedTargetScope = TargetedMonsterAttackHelper.ForceTargets(Creature, [target]);
        AttackCommand attack = await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(animation, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        return attack;
    }

    private async Task HandleLittleRedAngerAfterWolfAttack(int unblockedHitsOnLittleRed, bool isHowl)
    {
        if (!CanContinueNormalMove
            || LittleRedMercenaryEncounterHelper.FindLittleRed(CombatState)?.Monster is not LittleRedRidingHoodedMercenary littleRed)
        {
            return;
        }

        if (unblockedHitsOnLittleRed <= 0)
        {
            return;
        }

        int angerPerHit = isHowl
            ? LittleRedAngerGaugePower.WolfHowlAngerPerUnblockedHit
            : LittleRedAngerGaugePower.WolfAttackAngerPerUnblockedHit;
        await littleRed.ChangeAnger(angerPerHit * unblockedHitsOnLittleRed);
    }

    private int CountUnblockedHitsOnLittleRed(IEnumerable<DamageResult> results)
    {
        Creature? littleRed = LittleRedMercenaryEncounterHelper.FindLittleRed(CombatState);
        if (littleRed == null)
        {
            return 0;
        }

        return results.Count(result => result.Receiver == littleRed && result.UnblockedDamage > 0);
    }

    private async Task EnsurePhaseTwo()
    {
        if (_littleRedDeathFinaleActive
            || _phaseTwoEntered
            || Creature.CurrentHp * PhaseTwoHpDenominator > Creature.MaxHp * PhaseTwoHpNumerator)
        {
            return;
        }

        _phaseTwoEntered = true;
        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_phase_two.ogg");
        await PowerCmdCompat.Apply<WolfHowlingNightmarePower>(Creature, 1m, Creature, null);

        if (Creature.CombatState?.CurrentSide == CombatSide.Player)
        {
            string stunRecoveryMoveId = ToPhaseTwoMoveId(NextMove.Id);
            await CreatureCmd.Stun(Creature, stunRecoveryMoveId);
            await RefreshIntents();
            return;
        }

        await RefreshPhaseTwoIntentDisplay();
    }

    private async Task RefreshPhaseTwoIntentDisplay()
    {
        MoveState? replacement = NextMove.Id switch
        {
            CruelClawsMoveId => _cruelClawsWithHowlState,
            BloodstainedHuntMoveId => _bloodstainedHuntWithHowlState,
            FerociousFangsMoveId => _ferociousFangsWithHowlState,
            _ => null
        };

        if (replacement != null)
        {
            SetMoveImmediate(replacement, forceTransition: true);
        }

        NCreature? creatureNode = CombatQueries.CreatureNodeOf(this);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    private async Task AfterBaseMove()
    {
        if (!CanContinueNormalMove)
        {
            return;
        }

        if (IsPhaseTwo)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, PhaseTwoNextTurnStrength, Creature, null);
            await HowlMove([]);
            return;
        }

        _baseMovesSinceHowl++;
        if (_baseMovesSinceHowl >= WolfHowlPassivePower.BaseMovesPerHowl)
        {
            _forceHowl = true;
        }
    }

    private string ResolvePlannedMoveId()
    {
        if (_littleRedDeathFinaleActive)
        {
            return LittleRedFinaleExplodeMoveId;
        }

        if (IsPhaseTwo)
        {
            _forceHowl = false;
            return ToPhaseTwoMoveId(ResolveNextBaseMoveId());
        }

        if (_forceHowl)
        {
            return HowlMoveId;
        }

        return ResolveNextBaseMoveId();
    }

    private string ResolveNextBaseMoveId()
    {
        string moveId = (_baseMoveStep % 3) switch
        {
            0 => CruelClawsMoveId,
            1 => BloodstainedHuntMoveId,
            _ => FerociousFangsMoveId
        };
        _baseMoveStep++;
        return moveId;
    }

    private static string ToPhaseTwoMoveId(string baseMoveId)
    {
        return baseMoveId switch
        {
            CruelClawsMoveId => CruelClawsWithHowlMoveId,
            BloodstainedHuntMoveId => BloodstainedHuntWithHowlMoveId,
            FerociousFangsMoveId => FerociousFangsWithHowlMoveId,
            _ => baseMoveId
        };
    }

    private async Task RefreshIntents()
    {
        NCreature? creatureNode = CombatQueries.CreatureNodeOf(this);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

}
