using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.Nosferatu;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.Nosferatu;
using LibraryOfRuina.visuals.Nosferatu;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.Nosferatu;

public abstract class NosferatuBloodBatBase : LorMonsterModel
{
    protected const string ThirstMoveId = "THIRST";
    protected const string VampirismMoveId = "VAMPIRISM";
    protected const string DeepFangsMoveId = "DEEP_FANGS";

    internal const string TextureRoot = Nosferatu.TextureRoot;
    private const string SfxRoot = "res://audio/sfx/nosferatu/";

    internal static readonly string[] SharedAssetPaths =
        BloodBatCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                SfxRoot + "blood_bat_attack.ogg",
                "res://images/powers/nosferatu_blood_power.png",
                "res://images/powers/nosferatu_hydrophobia_passive_power.png"
            ])
            .ToArray();

    protected abstract string RightBatSlotName { get; }

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure
        };

    public override IEnumerable<string> AssetPaths =>
        SharedAssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent =>
                intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await ApplyHydrophobiaPassive();
        await PowerCmdCompat.Apply<MinionPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
    }

    protected abstract Task ApplyHydrophobiaPassive();

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var thirst = new MoveState(
            ThirstMoveId,
            ThirstMove,
            new BadgedDebuffIntent(
                [
                    IntentBadge.RapidWear(3, 1),
                    IntentBadge.FromPower<NosferatuBloodPower>(2)
                ],
                amount: 4,
                "BLOOD_BAT_THIRST.description"));
        var vampirism = new MoveState(
            VampirismMoveId,
            VampirismMove,
            new CombinedAttackDebuffIntent(
                () => VampirismDamage,
                () => 1,
                "BLOOD_BAT_VAMPIRISM.description",
                IntentBadge.Bleed(4),
                IntentBadge.FromPower<NosferatuBloodPower>(1)));
        var deepFangs = new MoveState(
            DeepFangsMoveId,
            DeepFangsMove,
            new CombinedAttackDebuffIntent(
                () => DeepFangsDamage,
                () => 2,
                "BLOOD_BAT_DEEP_FANGS.description",
                IntentBadge.Bleed(6)));

        var router = new DelegatingMonsterRouterState(
            "BLOOD_BAT_ROUTER",
            (_, _) => ResolvePlannedMoveId());
        thirst.FollowUpState = router;
        vampirism.FollowUpState = router;
        deepFangs.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [thirst, vampirism, deepFangs, router],
            router);
    }

    internal string ResolvePlannedMoveId()
    {
        string[] cycle = [ThirstMoveId, VampirismMoveId, DeepFangsMoveId];
        int round = Math.Max(0, Creature.CombatState?.RoundNumber ?? 0);
        int offset = Creature.SlotName == RightBatSlotName ? 1 : 0;
        return cycle[(round + offset) % cycle.Length];
    }

    private int VampirismDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12,
            9);

    private int DeepFangsDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            5);

    private async Task ThirstMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);
        foreach (Creature player in LivingPlayers())
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                    player,
                    3,
                    turns: 1,
                    Creature,
                    null);
        }

        await NosferatuBloodPower.Change(Creature, 2, Creature);
    }

    private async Task VampirismMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(
            SfxRoot + "blood_bat_attack.ogg",
            -2f);
        GroupAttackOutcome attack = await ExecuteAttackDetailed(
            VampirismDamage,
            hits: 1,
            "Attack");
        foreach (Creature target in attack.UnblockedTargets.Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                target,
                4,
                Creature,
                null);
        }

        if (attack.AnyUnblocked)
        {
            await NosferatuBloodPower.Change(Creature, 1, Creature);
        }
    }

    private async Task DeepFangsMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(
            SfxRoot + "blood_bat_attack.ogg",
            -2f);
        GroupAttackOutcome attack = await ExecuteAttackDetailed(
            DeepFangsDamage,
            hits: 2,
            "Attack");
        foreach (Creature target in attack.UnblockedTargets.Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                target,
                6,
                Creature,
                null);
        }
    }

    private async Task<GroupAttackOutcome> ExecuteAttackDetailed(
        int damage,
        int hits,
        string anim)
    {
        AttackCommand attack = await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithHitCount(hits)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim(
                anim,
                AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        return GroupAttackOutcome.From(
            Creature.CombatState?.PlayerCreatures
                .Where(static creature => creature.IsAlive)
                .ToArray()
            ?? [],
            AttackCommandCompat.Results(attack).ToArray());
    }

    private IEnumerable<Creature> LivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .ToArray()
        ?? [];

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is not MoveState moveState)
            {
                continue;
            }

            foreach (AbstractIntent intent in moveState.Intents)
            {
                yield return intent;
            }
        }
    }

    private sealed record GroupAttackOutcome(
        IReadOnlyList<Creature> Targets,
        IReadOnlyList<DamageResult> Results)
    {
        public bool AnyUnblocked =>
            Results.Any(static result => result.UnblockedDamage > 0);

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

public sealed class BloodBat : NosferatuBloodBatBase
{
    public static readonly string[] AssetPathsStatic = SharedAssetPaths;

    protected override string RightBatSlotName =>
        NosferatuElite.RightBatSlot;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            110,
            85);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            115,
            90);

    public override int DefaultChaoResistance => 70;

    protected override Task ApplyHydrophobiaPassive() =>
        PowerCmdCompat.Apply<NosferatuHydrophobiaPassivePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
}
