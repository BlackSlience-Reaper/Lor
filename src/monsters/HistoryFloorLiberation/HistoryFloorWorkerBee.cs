using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.visuals.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.HistoryFloorLiberation;

public sealed class HistoryFloorWorkerBee : LibraryMonsterModel
{
    private const string GuardQueenMoveId = "GUARD_QUEEN";
    private const string CarryLarvaMoveId = "CARRY_LARVA";
    private const string NutrientMixMoveId = "NUTRIENT_MIX";
    private const string PromoteGrowthMoveId = "PROMOTE_GROWTH";
    internal const string ChooserStateId = "CHOOSER";

    private const int GuardQueenParalysisAmount = 2;
    private const int GuardQueenParalysisTurns = 1;
    private const int CarryLarvaBlock = 12;
    private const int NutrientMixHeal = 10;
    private const int NutrientMixBlock = 10;
    private const float SegmentDelaySeconds = 1.15f;

    public const int StaggerResistance = 40;

    public override int DefaultChaoResistance => StaggerResistance;

    public const string Root = "res://images/monsters/history_floor/";
    public const string IdleTexturePath = Root + "worker_bee_idle.png";
    public const string AttackTexturePath = Root + "worker_bee_attack.png";
    public const string Attack2TexturePath = Root + "worker_bee_attack2.png";
    public const string HitTexturePath = Root + "worker_bee_hit.png";
    public const string DodgeTexturePath = Root + "worker_bee_dodge.png";

    public const string AttackThrustSfxPath = "res://audio/sfx/history_floor/wasp/worker_attack_thrust.ogg";
    public const string AttackSlashSfxPath = "res://audio/sfx/history_floor/wasp/worker_attack_slash.ogg";
    public const string DodgeSfxPath = "res://audio/sfx/history_floor/wasp/worker_dodge.ogg";
    public const string SpawnSfxPath = "res://audio/sfx/history_floor/wasp/worker_spawn.ogg";
    public const string SporeSfxPath = "res://audio/sfx/history_floor/wasp/spore_apply.ogg";

    private int _cadenceIndex;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 61, 60);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 63, 62);

    private static int GuardQueenDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);

    private static int NutrientMixDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private static int PromoteGrowthDamageA =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int PromoteGrowthDamageC =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    public override IEnumerable<string> AssetPaths =>
        HistoryFloorWorkerBeeCreatureVisuals.Profile.AssetPaths
        .Concat(
        [
            AttackThrustSfxPath,
            AttackSlashSfxPath,
            DodgeSfxPath,
            SpawnSfxPath,
            SporeSfxPath
        ])
        .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
        .Distinct();

    public void ConfigurePattern(int startIndex)
    {
        AssertMutable();
        _cadenceIndex = startIndex;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        LocalOggOneShotPlayer.Play(SpawnSfxPath, -2f);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var guardQueen = new MoveState(
            GuardQueenMoveId,
            GuardQueenMove,
            CreateSporeAttackIntent(GuardQueenDamage),
            new BadgedDefendIntent(
                GuardQueenParalysisAmount,
                "WASP_PARALYSIS_DEFEND.description",
                IntentBadge.FromPower<HistoryFloorWaspParalysisPower>(GuardQueenParalysisAmount)),
            CreateSporeAttackIntent(GuardQueenDamage));

        var carryLarva = new MoveState(
            CarryLarvaMoveId,
            CarryLarvaMove,
            new DefendIntent());

        var nutrientMix = new MoveState(
            NutrientMixMoveId,
            NutrientMixMove,
            new SingleAttackIntent(NutrientMixDamage),
            new HealIntent(),
            new DefendIntent());

        var promoteGrowth = new MoveState(
            PromoteGrowthMoveId,
            PromoteGrowthMove,
            new BadgedAttackIntent(PromoteGrowthDamageA, "WORKER_BEE_GROWTH_ATTACK.description", IntentBadge.FromPower<HistoryFloorWaspSporePower>(downText: "+X")),
            new BadgedAttackIntent(PromoteGrowthDamageA, "WORKER_BEE_GROWTH_ATTACK.description", IntentBadge.FromPower<HistoryFloorWaspSporePower>(downText: "+X")),
            new BadgedAttackIntent(PromoteGrowthDamageC, "WORKER_BEE_GROWTH_ATTACK.description", IntentBadge.FromPower<HistoryFloorWaspSporePower>(downText: "+X")));

        var chooser = new ConditionalBranchState(ChooserStateId);
        chooser.AddState(guardQueen, () => _cadenceIndex % 4 == 0);
        chooser.AddState(carryLarva, () => _cadenceIndex % 4 == 1);
        chooser.AddState(nutrientMix, () => _cadenceIndex % 4 == 2);
        chooser.AddState(promoteGrowth, () => true);

        guardQueen.FollowUpState = chooser;
        carryLarva.FollowUpState = chooser;
        nutrientMix.FollowUpState = chooser;
        promoteGrowth.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { guardQueen, carryLarva, nutrientMix, promoteGrowth, chooser },
            chooser);
    }

    private async Task GuardQueenMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> firstResults = await ExecuteAttackSegment("Attack", GuardQueenDamage, AttackThrustSfxPath);
        await ApplySporeFromResults(firstResults);
        await PowerCmdCompat.Apply<HistoryFloorWaspParalysisPower>(targets.Where(static target => target.IsAlive), GuardQueenParalysisAmount, Creature, null);
        foreach (HistoryFloorWaspParalysisPower paralysis in targets.Select(static target => target.GetPower<HistoryFloorWaspParalysisPower>()).OfType<HistoryFloorWaspParalysisPower>())
        {
            paralysis.SetTurnsRemaining(GuardQueenParalysisTurns);
        }

        IReadOnlyList<DamageResult> secondResults = await ExecuteAttackSegment("Attack2", GuardQueenDamage, AttackSlashSfxPath);
        await ApplySporeFromResults(secondResults);
        AdvanceCadence();
    }

    private async Task CarryLarvaMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(DodgeSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Defend", 0.45f);
        await CreatureCmd.GainBlock(Creature, CarryLarvaBlock, ValueProp.Move, null);
        AdvanceCadence();
    }

    private async Task NutrientMixMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("Attack", NutrientMixDamage, AttackThrustSfxPath);

        Creature? waspBoss = CombatState.Enemies
            .FirstOrDefault(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorWaspBoss);
        if (waspBoss != null)
        {
            await CreatureCmd.Heal(waspBoss, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(waspBoss, NutrientMixHeal));
            await CreatureCmd.GainBlock(waspBoss, NutrientMixBlock, ValueProp.Move, null);
        }

        AdvanceCadence();
    }

    private async Task PromoteGrowthMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> first = await ExecuteAttackSegment("Attack2", PromoteGrowthDamageA, AttackSlashSfxPath);
        await ApplyGrowthBonus(first);
        IReadOnlyList<DamageResult> second = await ExecuteAttackSegment("Attack2", PromoteGrowthDamageA, AttackSlashSfxPath);
        await ApplyGrowthBonus(second);
        IReadOnlyList<DamageResult> third = await ExecuteAttackSegment("Attack", PromoteGrowthDamageC, AttackThrustSfxPath);
        await ApplyGrowthBonus(third);
        AdvanceCadence();
    }

    private Task<AttackCommand> ExecuteSegmentAttack(string trigger, int damage)
    {
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(trigger, SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttackSegment(string trigger, int damage, string sfxPath)
    {
        LocalOggOneShotPlayer.Play(sfxPath, -2f);
        AttackCommand attack = await ExecuteSegmentAttack(trigger, damage);
        return AttackCommandCompat.Results(attack);
    }

    private async Task ApplySporeFromResults(IReadOnlyList<DamageResult> results)
    {
        IReadOnlyList<Creature> sporeTargets = results
            .Where(static result => result.Receiver.IsPlayer && result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
        if (sporeTargets.Count == 0)
        {
            return;
        }

        await PowerCmdCompat.Apply<HistoryFloorWaspSporePower>(sporeTargets, 1m, Creature, null);
    }

    private async Task ApplyGrowthBonus(IReadOnlyList<DamageResult> results)
    {
        foreach (DamageResult result in results.Where(static result => result.Receiver.IsPlayer && result.Receiver.IsAlive && result.UnblockedDamage > 0))
        {
            int spore = result.Receiver.GetPower<HistoryFloorWaspSporePower>()?.Amount ?? 0;
            if (spore <= 0)
            {
                continue;
            }

            await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                result.Receiver,
                spore,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Creature,
                null);
        }
    }

    private void AdvanceCadence()
    {
        _cadenceIndex++;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateSporeAttackIntent(GuardQueenDamage);
        yield return new BadgedDefendIntent(
            GuardQueenParalysisAmount,
            "WASP_PARALYSIS_DEFEND.description",
            IntentBadge.FromPower<HistoryFloorWaspParalysisPower>(GuardQueenParalysisAmount));
        yield return new DefendIntent();
        yield return new SingleAttackIntent(NutrientMixDamage);
        yield return new HealIntent();
        yield return new BadgedAttackIntent(PromoteGrowthDamageA, "WORKER_BEE_GROWTH_ATTACK.description", IntentBadge.FromPower<HistoryFloorWaspSporePower>(downText: "+X"));
        yield return new BadgedAttackIntent(PromoteGrowthDamageC, "WORKER_BEE_GROWTH_ATTACK.description", IntentBadge.FromPower<HistoryFloorWaspSporePower>(downText: "+X"));
    }

    private static BadgedAttackIntent CreateSporeAttackIntent(int damage) =>
        new(damage, "WASP_UNBLOCKED_SPORE_ATTACK.description", IntentBadge.FromPower<HistoryFloorWaspSporePower>(1));
}
