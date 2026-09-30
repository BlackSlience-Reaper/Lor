using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.abnormalities.QueenBee;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Nosferatu;

public sealed class Nosferatu : LorMonsterModel
{
    private const string GracefulRestMoveId = "GRACEFUL_REST";
    private const string ElegantDinnerMoveId = "ELEGANT_DINNER";
    private const string ThirstMoveId = "THIRST";
    private const string CrimsonMealMoveId = "CRIMSON_MEAL";
    private const string BloodFeastMoveId = "BLOOD_FEAST";
    private const string OminousAuraMoveId = "OMINOUS_AURA";
    private const string ColdClawsMoveId = "COLD_CLAWS";
    private const string ViolentGestureMoveId = "VIOLENT_GESTURE";
    private const string UnbearableThirstMoveId = "UNBEARABLE_THIRST";
    private const string ExtremeBloodthirstMoveId = "EXTREME_BLOODTHIRST";

    internal const string TextureRoot = "res://images/monsters/nosferatu/";
    private const string SfxRoot = "res://audio/sfx/nosferatu/";
    private const float LocalSfxVolumeScale = 0.85f;
    private static readonly float LocalSfxVolumeDb = Mathf.LinearToDb(LocalSfxVolumeScale);

    private static readonly string[] NormalCycle =
    [
        ElegantDinnerMoveId,
        CrimsonMealMoveId,
        ThirstMoveId,
        BloodFeastMoveId
    ];

    private static readonly string[] TransformedCycle =
    [
        OminousAuraMoveId,
        ColdClawsMoveId,
        ViolentGestureMoveId,
        UnbearableThirstMoveId,
        ExtremeBloodthirstMoveId
    ];

    public static readonly string[] AssetPathsStatic =
        NosferatuCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                SfxRoot + "nosferatu_strong_attack.ogg",
                SfxRoot + "nosferatu_transform.ogg",
                SfxRoot + "nosferatu_transform_slash.ogg",
                SfxRoot + "nosferatu_transform_strike.ogg",
                SfxRoot + "nosferatu_transform_strong_attack_effect.ogg",
                SfxRoot + "nosferatu_transform_vampire.ogg",
                SfxRoot + "nosferatu_evade.ogg",
                "res://images/powers/nosferatu_blood_power.png",
                "res://images/powers/nosferatu_hydrophobia_passive_power.png",
                "res://images/powers/nosferatu_hydrophobia_power.png",
                "res://images/powers/nosferatu_transform_power.png",
                "res://images/powers/nosferatu_flowing_blood_power.png"
            ])
            .ToArray();

    private static readonly string NosferatuPageRelicTitleLocKey =
        $"{ModelDb.GetId<NosferatuPageRelic>().Entry}.title";

    private bool _isTransformed;
    private int _cycleIndex;

    public bool IsTransformed => _isTransformed;

    public override bool ShouldDisappearFromDoom => _isTransformed;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 545, 441);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 550, 444);

    public override int DefaultChaoResistance => 190;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(BloodBat.AssetPathsStatic)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _isTransformed = false;
        _cycleIndex = 0;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<NosferatuHydrophobiaPassivePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<NosferatuTransformPower>(Creature, 1m, Creature, null, silent: true);
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

        AddPageRewardsFromDeathHook();
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var gracefulRest = new MoveState(
            GracefulRestMoveId,
            GracefulRestMove,
            new SummonIntent(),
            new DefendIntent());
        var elegantDinner = new MoveState(
            ElegantDinnerMoveId,
            ElegantDinnerMove,
            new CombinedAttackBuffIntent(
                () => ElegantDinnerDamage,
                () => 2,
                "NOSFERATU_ELEGANT_DINNER.description",
                IntentBadge.FromPower<NosferatuBloodPower>(2)));
        var thirst = new MoveState(
            ThirstMoveId,
            ThirstMove,
            new CombinedAttackBuffIntent(
                () => ThirstDamage,
                () => 1,
                "NOSFERATU_THIRST.description",
                IntentBadge.FromPower<NosferatuBloodPower>(3)));
        var crimsonMeal = new MoveState(
            CrimsonMealMoveId,
            CrimsonMealMove,
            new CombinedDefendBuffIntent(
                24,
                "NOSFERATU_CRIMSON_MEAL.description",
                IntentBadge.FromPower<QueenBeeNextTurnQuicknessPower>(5),
                IntentBadge.FromPower<LibraryProtectionPower>(5)));
        var bloodFeast = new MoveState(
            BloodFeastMoveId,
            BloodFeastMove,
            new IndiscriminateAttackIntent(
                () => BloodFeastDamage,
                () => 3,
                "NOSFERATU_BLOOD_FEAST.description",
                IntentBadge.FromPower<NosferatuHydrophobiaPower>(2),
                IntentBadge.Heal(() => BloodFeastHealPreview)));

        var ominousAura = new MoveState(
            OminousAuraMoveId,
            OminousAuraMove,
            new CombinedDefendDebuffIntent(18, "NOSFERATU_OMINOUS_AURA.description", IntentBadge.Weak(2), IntentBadge.Frail(2)));
        var coldClaws = new MoveState(
            ColdClawsMoveId,
            ColdClawsMove,
            new CombinedAttackBuffIntent(
                () => ColdClawsDamage,
                () => 12,
                "NOSFERATU_COLD_CLAWS.description",
                IntentBadge.Strength(1)));
        var violentGesture = new MoveState(
            ViolentGestureMoveId,
            ViolentGestureMove,
            new CombinedAttackDebuffIntent(
                () => ViolentGestureDamage,
                () => 4,
                "NOSFERATU_VIOLENT_GESTURE.description",
                IntentBadge.StrengthDown(1),
                IntentBadge.FromPower<DexterityPower>(-1),
                IntentBadge.FromPower<StrengthPower>(-1),
                IntentBadge.RapidWear(8, 1)));
        var unbearableThirst = new MoveState(
            UnbearableThirstMoveId,
            UnbearableThirstMove,
            new BadgedBuffIntent(
                [
                    IntentBadge.FromPower<NosferatuBloodPower>(3),
                    IntentBadge.Heal(),
                ],
                amount: 3,
                "NOSFERATU_UNBEARABLE_THIRST.description"),
            new DebuffIntent());
        var extremeBloodthirst = new MoveState(
            ExtremeBloodthirstMoveId,
            ExtremeBloodthirstMove,
            new IndiscriminateAttackIntent(
                () => ExtremeBloodthirstDamage,
                () => 1,
                "NOSFERATU_EXTREME_BLOODTHIRST.description",
                IntentBadge.FromPower<NosferatuHydrophobiaPower>(2),
                IntentBadge.RapidWear(2, 2),
                IntentBadge.FromPower<LibraryWeakPower>(2,  "2",  "2")));

        var router = new DelegatingMonsterRouterState(
            "NOSFERATU_ROUTER",
            (_, _) => ResolvePlannedMoveId());
        foreach (MoveState state in new[]
                 {
                     gracefulRest, elegantDinner, thirst, crimsonMeal, bloodFeast,
                     ominousAura, coldClaws, violentGesture, unbearableThirst, extremeBloodthirst
                 })
        {
            state.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [
                gracefulRest, elegantDinner, thirst, crimsonMeal, bloodFeast,
                ominousAura, coldClaws, violentGesture, unbearableThirst, extremeBloodthirst, router
            ],
            router);
    }

    public async Task TransformToBloodfiend()
    {
        if (_isTransformed || Creature.IsDead)
        {
            return;
        }

        _isTransformed = true;
        _cycleIndex = 0;
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_transform.ogg", LocalSfxVolumeDb);

        if (CombatQueries.CreatureNodeOf(this)?.Visuals is NosferatuCreatureVisuals visuals)
        {
            visuals.SetBloodfiendForm(true);
        }

        await PowerCmdCompat.RemoveIfPresent<NosferatuTransformPower>(
            Creature);

        await PowerCmdCompat.Apply<NosferatuFlowingBloodPower>(Creature, 1m, Creature, null, silent: true);
    }

    internal string ResolvePlannedMoveId()
    {
        if (!_isTransformed && !HasLivingBloodBat() && TryGetBloodBatSlot(out _))
        {
            return GracefulRestMoveId;
        }

        string[] cycle = _isTransformed ? TransformedCycle : NormalCycle;
        return cycle[Math.Clamp(_cycleIndex, 0, cycle.Length - 1)];
    }

    private int ElegantDinnerDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 5);

    private int ThirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 19, 16);

    private int BloodFeastDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 7);

    private int ColdClawsDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private int ViolentGestureDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);

    private int ExtremeBloodthirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 23, 18);

    private int BloodFeastHealPreview => 12 + Math.Max(1, Creature.CombatState?.PlayerCreatures.Count(static c => c.IsAlive) ?? 1);

    private async Task GracefulRestMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_evade.ogg", LocalSfxVolumeDb);
        await AbnormalityAnimHelper.TriggerCast(Creature);
        if (!HasLivingBloodBat() && TryGetBloodBatSlot(out string slot))
        {
            await CreatureCmd.Add(ModelDb.Monster<BloodBat>().ToMutable(), CombatState, CombatSide.Enemy, slot);
        }

        foreach (Creature enemy in LivingEnemies())
        {
            await CreatureCmd.GainBlock(enemy, 12, ValueProp.Move, null);
        }

        AdvanceCycle();
    }

    private async Task ElegantDinnerMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_transform_strike.ogg", LocalSfxVolumeDb);
        GroupAttackOutcome attack = await ExecuteStandardAttackDetailed(ElegantDinnerDamage, hits: 2, "Attack");
        foreach (DamageResult _ in attack.UnblockedResults)
        {
            await NosferatuBloodPower.Change(Creature, 2, Creature);
        }

        AdvanceCycle();
    }

    private async Task ThirstMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_transform_strike.ogg", LocalSfxVolumeDb);
        GroupAttackOutcome attack = await ExecuteStandardAttackDetailed(ThirstDamage, hits: 1, "Attack");
        if (attack.AnyUnblocked)
        {
            await NosferatuBloodPower.Change(Creature, 3, Creature);
        }

        AdvanceCycle();
    }

    private async Task CrimsonMealMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);
        foreach (Creature enemy in LivingEnemies())
        {
            await CreatureCmd.GainBlock(enemy, 24, ValueProp.Move, null);
            // await LibraryPowerCmd.Apply<LibraryQuicknessPower>(enemy,
            //     5,
            //     Creature,
            //     null);
            await PowerCmdCompat.Apply<QueenBeeNextTurnQuicknessPower>(enemy,
                5,
                Creature, null);
            await LibraryPowerCmd.Apply<LibraryProtectionPower>(
                enemy,
                5,
                turns: 1,
                Creature,
                null);
        }

        AdvanceCycle();
    }

    private async Task BloodFeastMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_strong_attack.ogg", LocalSfxVolumeDb);
        GroupAttackOutcome attack = await ExecuteGroupAttackDetailed(BloodFeastDamage, hits: 3, "SpecialAttack");
        foreach (Creature target in attack.Targets.Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<NosferatuHydrophobiaPower>(target, 2, Creature, null);
        }

        int heal = 12 + attack.UnblockedTargets.Distinct().Count();
        foreach (Creature enemy in LivingEnemies())
        {
            await CreatureCmd.Heal(enemy, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(enemy, heal));
        }

        AdvanceCycle();
    }

    private async Task OminousAuraMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);
        await CreatureCmd.GainBlock(Creature, 18, ValueProp.Move, null);
        foreach (Creature player in LivingPlayers())
        {
            await PowerCmdCompat.ApplyDebuff<WeakPower>(player, 2, Creature, null);
            await PowerCmdCompat.ApplyDebuff<FrailPower>(player, 2, Creature, null);
        }

        AdvanceCycle();
    }

    private async Task ColdClawsMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_transform_strike.ogg", LocalSfxVolumeDb);
        await ExecuteStandardAttackDetailed(ColdClawsDamage, hits: 12, "Attack");
        await PowerCmdCompat.Apply<StrengthPower>(Creature, 1, Creature, null);
        AdvanceCycle();
    }

    private async Task ViolentGestureMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_transform_slash.ogg", LocalSfxVolumeDb);
        GroupAttackOutcome attack = await ExecuteStandardAttackDetailed(ViolentGestureDamage, hits: 4, "Attack");
        foreach (Creature target in attack.UnblockedTargets.Distinct())
        {
            await PowerCmdCompat.Apply<StrengthPower>(target, -1, Creature, null);
            await PowerCmdCompat.Apply<DexterityPower>(target, -1, Creature, null);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                8,
                turns: 1,
                Creature,
                null);
        }

        AdvanceCycle();
    }

    private async Task UnbearableThirstMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_transform_vampire.ogg", LocalSfxVolumeDb);
        await AbnormalityAnimHelper.TriggerCast(Creature);
        await NosferatuBloodPower.Change(Creature, 3, Creature);
        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, 16));
        await PowerCmdCompat.ApplyDebuff<VulnerablePower>(LivingPlayers(), 1, Creature, null);
        AdvanceCycle();
    }

    private async Task ExtremeBloodthirstMove(IReadOnlyList<Creature> targets)
    {
        GroupAttackOutcome attack = await ExecuteGroupAttackDetailed(ExtremeBloodthirstDamage, hits: 1, "SpecialAttack");
        LocalOggOneShotPlayer.Play(SfxRoot + "nosferatu_transform_strong_attack_effect.ogg", LocalSfxVolumeDb);
        foreach (Creature target in attack.UnblockedTargets.Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<NosferatuHydrophobiaPower>(target, 2, Creature, null);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                2,
                turns: 2,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                target,
                2,
                turns: 2,
                Creature,
                null);
        }

        AdvanceCycle();
    }

    private async Task<GroupAttackOutcome> ExecuteStandardAttackDetailed(int damage, int hits, string anim)
    {
        AttackCommand attack = await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithHitCount(hits)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim(anim, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        return GroupAttackOutcome.From(CombatState.LivingPlayerCreatures().ToArray(), AttackCommandCompat.Results(attack).ToArray());
    }

    private async Task<GroupAttackOutcome> ExecuteGroupAttackDetailed(int damage, int hits, string anim)
    {
        IReadOnlyList<Creature> players = LivingPlayers().ToArray();
        if (players.Count == 0)
        {
            return new GroupAttackOutcome(players, []);
        }

        List<DamageResult> results = [];
        for (int i = 0; i < hits; i++)
        {
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, players))
            {
                await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, damage, players);
                AttackCommand attack = await DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .WithAttackerAnim(anim, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .SpawningHitVfxOnEachCreature()
                    .WithIndiscriminateBlockBreak(this, damage, players)
                    .Execute(null);
                results.AddRange(AttackCommandCompat.Results(attack));
            }
        }

        return GroupAttackOutcome.From(players, results);
    }

    private IEnumerable<Creature> LivingPlayers() =>
        Creature.CombatState?.LivingPlayerCreatures().ToArray() ?? [];

    private IEnumerable<Creature> LivingEnemies() =>
        Creature.CombatState?.Enemies.Where(static creature => creature.IsAlive).ToArray() ?? [];

    private bool HasLivingBloodBat() =>
        Creature.CombatState?.Enemies.Any(static enemy => enemy.IsAlive && enemy.Monster is BloodBat) ?? false;

    private bool TryGetBloodBatSlot(out string slot)
    {
        slot = string.Empty;
        CombatStateLike? combatState = Creature.CombatState;
        if (combatState?.Encounter is not NosferatuElite)
        {
            return false;
        }

        foreach (string candidate in NosferatuElite.BatSlots)
        {
            bool occupied = combatState.Enemies.Any(enemy => enemy.IsAlive && enemy.SlotName == candidate);
            if (!occupied)
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private void AdvanceCycle()
    {
        string[] cycle = _isTransformed ? TransformedCycle : NormalCycle;
        _cycleIndex = (_cycleIndex + 1) % cycle.Length;
    }

    private void AddPageRewardsFromDeathHook()
    {
        if (Creature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || room.Encounter is not NosferatuElite)
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<NosferatuPageRelic>(room, NosferatuPageRelicTitleLocKey);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState moveState)
            {
                foreach (AbstractIntent intent in moveState.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

    private sealed record GroupAttackOutcome(
        IReadOnlyList<Creature> Targets,
        IReadOnlyList<DamageResult> Results)
    {
        public bool AnyUnblocked => Results.Any(static result => result.UnblockedDamage > 0);

        public IEnumerable<DamageResult> UnblockedResults =>
            Results.Where(static result => result.UnblockedDamage > 0);

        public IEnumerable<Creature> UnblockedTargets =>
            UnblockedResults.Select(static result => result.Receiver);

        public static GroupAttackOutcome From(
            IReadOnlyList<Creature> targets,
            IReadOnlyList<DamageResult> results) =>
            new(targets, results);
    }
}
