using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

public sealed class LittleRedRidingHoodedMercenary : LorMonsterModel, ITargetedMonsterAttackProvider
{
    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.normal.0",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.normal.1",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.normal.2",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.normal.3",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.normal.4",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.normal.5"
    ];

    private static readonly string[] RageBackgroundTextLineKeys =
    [
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.rage.0",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.rage.1",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.rage.2"
    ];

    private static readonly string[] UnrelievedBackgroundTextLineKeys =
    [
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.unrelieved.0",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.unrelieved.1",
        "LITTLE_RED_RIDING_HOODED_MERCENARY.backgroundText.unrelieved.2"
    ];

    private const string BeastHuntMoveId = "BEAST_HUNT";
    private const string CatchBreathMoveId = "CATCH_BREATH";
    private const string HollowPointShellMoveId = "HOLLOW_POINT_SHELL";
    private const string StrikeWithoutHesitationMoveId = "STRIKE_WITHOUT_HESITATION";
    private const string BulletShowerMoveId = "BULLET_SHOWER";

    private const int BeastHuntFlaw = 5;
    private const int BeastHuntFlawTurns = 1;
    private const int BeastHuntBlock = 9;
    private const int CatchBreathHits = 2;
    private const int CatchBreathHeal = 6;
    private const int HollowPointConfusionTurns = 1;
    private const int HollowPointRapidWear = 3;
    private const int HollowPointRapidWearTurns = 3;
    private const int BulletShowerHits = 2;
    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private MoveState? _beastHuntState;
    private MoveState? _catchBreathState;
    private MoveState? _hollowPointState;
    private MoveState? _strikeState;
    private MoveState? _bulletShowerState;
    private int _normalStep;
    private int _rageStep;
    private bool _unrelievedAnger;
    private bool _isEnteringRage;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 270, 310);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 280, 320);

    public override int DefaultChaoResistance => 400;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    public bool IsUnrelievedAnger => _unrelievedAnger;

    public bool IsRaging => Creature?.GetPower<LittleRedRagePower>() != null || _isEnteringRage;

    private int BeastHuntDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 26, 22);

    private int CatchBreathDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

    private int HollowPointDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 18, 16);

    private int StrikeDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 19, 16);

    private int BulletShowerDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            IReadOnlyList<string> visualAssets =
                LittleRedMercenaryCreatureVisuals
                    .Profile.AssetPaths;
            var paths = new List<string>(visualAssets.Count + 16);
            paths.AddRange(visualAssets);

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

        _normalStep = 0;
        _rageStep = 0;
        _unrelievedAnger = false;
        _isEnteringRage = false;
        LittleRedDeathContext.Clear();

        await PowerCmdCompat.Apply<LittleRedNightmareEndPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LittleRedMercenaryDamageTrackerPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LittleRedAngerGaugePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LittleRedUnrelievedAngerPassivePower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead)
        {
            RefreshBackgroundMoonTextLoop();
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        _beastHuntState = new MoveState(
            BeastHuntMoveId,
            BeastHuntMove,
            new BadgedTargetedAttackIntent(
                () => BeastHuntDamage,
                () => 1,
                "LITTLE_RED_BEAST_HUNT.description",
                IntentBadge.Flaw(BeastHuntFlaw, BeastHuntFlawTurns)),
            new DebuffIntent(),
            new DefendIntent());

        _catchBreathState = new MoveState(
            CatchBreathMoveId,
            CatchBreathMove,
            new TargetedMonsterAttackIntent(() => CatchBreathDamage, () => CatchBreathHits, "LITTLE_RED_CATCH_BREATH.description"),
            new HealIntent());

        _hollowPointState = new MoveState(
            HollowPointShellMoveId,
            HollowPointShellMove,
            new BadgedTargetedAttackIntent(
                () => HollowPointDamage,
                () => 1,
                "LITTLE_RED_HOLLOW_POINT_SHELL.description",
                IntentBadge.RapidWear(HollowPointRapidWear, HollowPointRapidWearTurns),
                IntentBadge.Confusion(HollowPointConfusionTurns)));

        _strikeState = new MoveState(
            StrikeWithoutHesitationMoveId,
            StrikeWithoutHesitationMove,
            new IndiscriminateAttackIntent(
                () => StrikeDamage,
                () => 1,
                "LITTLE_RED_STRIKE_WITHOUT_HESITATION.description",
                IntentBadge.Confusion(1)),
            new DebuffIntent(true),
            new DetailedBuffIntent<LibraryOfRuinaNextTurnStrength>(
                () => LittleRedMercenaryEncounterHelper.GetIndiscriminateTargets(Creature).Count));

        _bulletShowerState = new MoveState(
            BulletShowerMoveId,
            BulletShowerMove,
            new IndiscriminateAttackIntent(() => BulletShowerDamage, () => BulletShowerHits, "LITTLE_RED_BULLET_SHOWER.description"));

        var chooser = new DelegatingMonsterRouterState(
            "LITTLE_RED_ROUTER",
            (_, rng) => ResolvePlannedMoveId(rng));

        foreach (MoveState move in new[] { _beastHuntState, _catchBreathState, _hollowPointState, _strikeState, _bulletShowerState })
        {
            move.FollowUpState = chooser;
            states.Add(move);
        }

        states.Add(chooser);
        return new MonsterMoveStateMachine(states, chooser);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new BadgedTargetedAttackIntent(
            () => BeastHuntDamage,
            () => 1,
            "LITTLE_RED_BEAST_HUNT.description",
            IntentBadge.Flaw(BeastHuntFlaw, BeastHuntFlawTurns));
        yield return new DebuffIntent();
        yield return new DefendIntent();
        yield return new TargetedMonsterAttackIntent(() => CatchBreathDamage, () => CatchBreathHits, "LITTLE_RED_CATCH_BREATH.description");
        yield return new HealIntent();
        yield return new BadgedTargetedAttackIntent(
            () => HollowPointDamage,
            () => 1,
            "LITTLE_RED_HOLLOW_POINT_SHELL.description",
            IntentBadge.RapidWear(HollowPointRapidWear, HollowPointRapidWearTurns),
            IntentBadge.Confusion(HollowPointConfusionTurns));
        yield return new IndiscriminateAttackIntent(
            () => StrikeDamage,
            () => 1,
            "LITTLE_RED_STRIKE_WITHOUT_HESITATION.description",
            IntentBadge.Confusion(1));
        yield return new DetailedBuffIntent<LibraryOfRuinaNextTurnStrength>(
            () => 1);
        yield return new IndiscriminateAttackIntent(() => BulletShowerDamage, () => BulletShowerHits, "LITTLE_RED_BULLET_SHOWER.description");
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        string moveId = NextMove.Id;
        return moveId is BeastHuntMoveId
            or CatchBreathMoveId
            or HollowPointShellMoveId
            or StrikeWithoutHesitationMoveId
            or BulletShowerMoveId;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        if (NextMove.Id is StrikeWithoutHesitationMoveId or BulletShowerMoveId)
        {
            return LittleRedMercenaryEncounterHelper.GetIndiscriminateTargets(owner);
        }

        Creature? wolf = LittleRedMercenaryEncounterHelper.FindWolf(owner.CombatState);
        if (wolf != null)
        {
            return [wolf];
        }

        return owner.CombatState?.Creatures
            .Where(creature => creature.IsAlive
                && creature != owner
                && creature.IsPlayer)
            .Take(1)
            .ToArray()
            ?? Array.Empty<Creature>();
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        return GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "Unknown Target";
    }

    public async Task ChangeAnger(int delta)
    {
        LittleRedAngerGaugePower? gauge = Creature.GetPower<LittleRedAngerGaugePower>();
        if (gauge != null)
        {
            await gauge.ChangeAnger(delta);
        }
    }

    public async Task EnterRage()
    {
        if (_isEnteringRage || _unrelievedAnger || Creature.IsDead || Creature.GetPower<LittleRedRagePower>() != null)
        {
            return;
        }

        _isEnteringRage = true;
        _rageStep = RunRng.MonsterAi.NextBool() ? 0 : 1;
        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_rage.ogg");
        RefreshBackgroundMoonTextLoop();
        await LittleRedRagePower.ApplyWithDuration(Creature, 1, LittleRedRagePower.DefaultTurns, Creature);
        await MultiplayerScalingPatchHelper.RescaleMonsterMaxHpAndRestoreDifference(
            Creature);
        LittleRedMercenaryBackgroundController.SetRageBackground(true);
        _isEnteringRage = false;
        await RefreshIntents();
    }

    public void OnRageEnded()
    {
        Creature.GetPower<LittleRedAngerGaugePower>()?.ResetGauge();
        LittleRedMercenaryBackgroundController.SetRageBackground(false);
        _rageStep = 0;
        if (!_unrelievedAnger)
        {
            RefreshBackgroundMoonTextLoop();
        }
    }

    public async Task EnterUnrelievedAnger(PlayerChoiceContext choiceContext)
    {
        if (_unrelievedAnger || Creature.IsDead)
        {
            return;
        }

        _unrelievedAnger = true;
        await MultiplayerScalingPatchHelper.RescaleMonsterMaxHpAndRestoreDifference(
            Creature);
        if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals is LittleRedMercenaryCreatureVisuals visuals)
        {
            visuals.FacePlayers();
        }
        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_unrelieved.ogg");

        PowerModel? passive = Creature.GetPower<LittleRedUnrelievedAngerPassivePower>();
        if (passive != null)
        {
            await PowerCmd.Remove(passive);
        }

        PowerModel? rage = Creature.GetPower<LittleRedRagePower>();
        if (rage != null)
        {
            await PowerCmd.Remove(rage);
        }

        int newMaxHp = (int)Math.Ceiling(Creature.MaxHp * (100 + LittleRedUnrelievedAngerPassivePower.MaxHpIncreasePercent) / 100m);
        await CreatureCmd.SetMaxHp(Creature, newMaxHp);
        await CreatureCmd.Heal(Creature, (int)Math.Floor(Creature.MaxHp * LittleRedUnrelievedAngerPassivePower.HealPercent / 100m));
        await PowerCmdCompat.Apply<LittleRedUnrelievedAngerPower>(Creature, 1m, Creature, null);
        LittleRedMercenaryBackgroundController.SetRageBackground(true);
        RefreshBackgroundMoonTextLoop();
        await RefreshIntents();
    }

    private async Task BeastHuntMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_attack.ogg");
        AttackCommand attack = await ExecuteTargetedAttackSegment(target, BeastHuntDamage, "Attack");
        if (target.IsAlive)
        {
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(target, BeastHuntFlaw, BeastHuntFlawTurns, Creature, null);
        }

        await CreatureCmd.GainBlock(Creature, BeastHuntBlock, ValueProp.Move, null);
        await HandleAngerAfterAttack(AttackCommandCompat.Results(attack).Any(result => result.Receiver == target), target);
    }

    private async Task CatchBreathMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_attack.ogg");
        bool hitTarget = false;
        for (int i = 0; i < CatchBreathHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_attack.ogg");
            AttackCommand segment = await ExecuteTargetedAttackSegment(target, CatchBreathDamage, "Attack");
            hitTarget |= AttackCommandCompat.Results(segment).Any(result => result.Receiver == target && result.TotalDamage > 0);
        }
        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, CatchBreathHeal));
        await HandleAngerAfterAttack(hitTarget, target);
    }

    private async Task HollowPointShellMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_fire.ogg");
        AttackCommand attack = await ExecuteTargetedAttackSegment(target, HollowPointDamage, "Fire");
        if (target.IsAlive)
        {
            if (target.IsPlayer)
            {
                await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(target, HollowPointConfusionTurns, Creature, null);
            }

            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                HollowPointRapidWear,
                HollowPointRapidWearTurns,
                Creature,
                null);
        }

        await HandleAngerAfterAttack(AttackCommandCompat.Results(attack).Any(result => result.Receiver == target && result.TotalDamage > 0), target);
    }

    private async Task StrikeWithoutHesitationMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> actualTargets = LittleRedMercenaryEncounterHelper.GetIndiscriminateTargets(Creature);
        if (actualTargets.Count == 0)
        {
            return;
        }

        await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, StrikeDamage, actualTargets);
        bool hitWolf = false;
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets))
        {
            LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_throw.ogg");
            AttackCommand segment = await DamageCmd.Attack(StrikeDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, StrikeDamage, actualTargets)
                .Execute(null);

            Creature? wolf = LittleRedMercenaryEncounterHelper.FindWolf(CombatState);
            hitWolf = wolf != null && AttackCommandCompat.Results(segment).Any(result => result.Receiver == wolf && result.TotalDamage > 0);
        }
        await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(actualTargets.Where(creature => creature.IsPlayer), 1, Creature, null);
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, actualTargets.Count, Creature, null);
        await HandleAngerAfterAttack(hitWolf, LittleRedMercenaryEncounterHelper.FindWolf(CombatState));
    }

    private async Task BulletShowerMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> actualTargets = LittleRedMercenaryEncounterHelper.GetIndiscriminateTargets(Creature);
        if (actualTargets.Count == 0)
        {
            return;
        }

        Creature? wolf = LittleRedMercenaryEncounterHelper.FindWolf(CombatState);
        bool hitWolf = false;
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets))
        {
            for (int i = 0; i < BulletShowerHits; i++)
            {
                if (Creature.IsDead) break;
                await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, BulletShowerDamage, actualTargets);
                LocalOggOneShotPlayer.Play(LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_fire.ogg");
                AttackCommand segment = await DamageCmd.Attack(BulletShowerDamage)
                    .FromMonster(this)
                    .WithAttackerAnim("Fire", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .SpawningHitVfxOnEachCreature()
                    .WithIndiscriminateBlockBreak(this, BulletShowerDamage, actualTargets)
                    .Execute(null);

                if (wolf != null)
                {
                    hitWolf |= AttackCommandCompat.Results(segment).Any(result => result.Receiver == wolf && result.TotalDamage > 0);
                }
            }
        }
        await HandleAngerAfterAttack(hitWolf, wolf);
    }

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

    private async Task HandleAngerAfterAttack(bool hitWolf, Creature? wolf)
    {
        if (wolf == null)
        {
            return;
        }

        if (hitWolf)
        {
            await ChangeAnger(-LittleRedAngerGaugePower.AngerLossOnWolfHit);
        }
    }

    private async Task RefreshIntents()
    {
        NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents();
        await Task.CompletedTask;
    }

    private string ResolvePlannedMoveId(Rng rng)
    {
        if (_unrelievedAnger)
        {
            return rng.NextBool() ? StrikeWithoutHesitationMoveId : BulletShowerMoveId;
        }

        if (IsRaging)
        {
            string[] rageMoves = _rageStep % 2 == 0
                ? [StrikeWithoutHesitationMoveId, BulletShowerMoveId]
                : [BulletShowerMoveId, StrikeWithoutHesitationMoveId];
            string result = rageMoves[Math.Clamp(_rageStep, 0, 1)];
            _rageStep = Math.Min(_rageStep + 1, 1);
            return result;
        }

        if (_normalStep == 0)
        {
            _normalStep = 1;
            return BeastHuntMoveId;
        }

        bool preferCatchBreath = _normalStep % 2 == 1;
        _normalStep++;
        if (preferCatchBreath)
        {
            return rng.NextBool() ? CatchBreathMoveId : HollowPointShellMoveId;
        }

        return rng.NextBool() ? HollowPointShellMoveId : CatchBreathMoveId;
    }

    private void RefreshBackgroundMoonTextLoop()
    {
        IReadOnlyList<string> lineKeys = _unrelievedAnger
            ? UnrelievedBackgroundTextLineKeys
            : IsRaging
                ? RageBackgroundTextLineKeys
                : NormalBackgroundTextLineKeys;

        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

}
