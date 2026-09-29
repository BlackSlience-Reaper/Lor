using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.ArtFloorLiberation;
using LibraryOfRuina.visuals.ArtFloorLiberation;
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
using MegaCrit.Sts2.Core.ValueProps;
using VoidCard = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace LibraryOfRuina.monsters.ArtFloorLiberation;

public sealed class ArtFloorDaCapoBoss : LiberationPhaseBossMonster
{
    private const int Phase = 1;
    private const string FirstMovementMoveId = "FIRST_MOVEMENT";
    private const string SecondMovementMoveId = "SECOND_MOVEMENT";
    private const string ThirdMovementMoveId = "THIRD_MOVEMENT";
    private const string FourthMovementMoveId = "FOURTH_MOVEMENT";
    private const string FinaleMoveId = "FINALE";

    public const int StageTurnCount = 5;
    private const int StaggerResistanceMax = 200;
    private const int FirstMovementBlock = 20;
    private const int FirstMovementArtifact = 2;
    private const int FirstMovementBuffer = 2;
    private const int SecondMovementHits = 2;
    private const int ThirdMovementWeak = 3;
    private const int FourthMovementHits = 3;
    private const int FinaleChaosDamage = 1;

    public const string Root = "res://images/monsters/art_floor/dacapo/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string AttackTexturePath = Root + "attack.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string GuardTexturePath = Root + "guard.png";
    public const string SpecialTexturePath = Root + "special.png";

    public override int DefaultChaoResistance => StaggerResistanceMax;

    public override int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => ImmuneResistance();

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => ImmuneResistance();

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 296, 293);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 300, 295);

    private static int SecondMovementDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 9);

    private static int ThirdMovementDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 12);

    private static int FourthMovementDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 8, 6);

    private static int FinaleDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 30, 25);

    public override IEnumerable<string> AssetPaths =>
        ArtFloorDaCapoCreatureVisuals.Profile.AssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        if (CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<MostBeautifulPerformancePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ArtFloorEnsemblePower>(Creature, 1m, Creature, null, silent: true);

        Log.Info("[LibraryOfRuina.ArtFloor] Da Capo first-phase movement sequence ready.");
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
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var first = new MoveState(
            FirstMovementMoveId,
            FirstMovementMove,
            CreateMovementIntents(1));
        var second = new MoveState(
            SecondMovementMoveId,
            SecondMovementMove,
            CreateMovementIntents(2));
        var third = new MoveState(
            ThirdMovementMoveId,
            ThirdMovementMove,
            CreateMovementIntents(3));
        var fourth = new MoveState(
            FourthMovementMoveId,
            FourthMovementMove,
            CreateMovementIntents(4));
        var finale = new MoveState(
            FinaleMoveId,
            FinaleMove,
            CreateMovementIntents(5));

        first.FollowUpState = second;
        second.FollowUpState = third;
        third.FollowUpState = fourth;
        fourth.FollowUpState = finale;
        finale.FollowUpState = first;
        reviveAndEmpower.FollowUpState = first;

        return new MonsterMoveStateMachine(
            new MonsterState[] { reviveAndEmpower, first, second, third, fourth, finale },
            first);
    }

    private async Task FirstMovementMove(IReadOnlyList<Creature> targets)
    {
        await PlayFirstMovement(targets);
    }

    private async Task SecondMovementMove(IReadOnlyList<Creature> targets)
    {
        await PlaySecondMovement(targets);
    }

    private async Task ThirdMovementMove(IReadOnlyList<Creature> targets)
    {
        await PlayThirdMovement(targets);
    }

    private async Task FourthMovementMove(IReadOnlyList<Creature> targets)
    {
        await PlayFourthMovement(targets);
    }

    private async Task FinaleMove(IReadOnlyList<Creature> targets)
    {
        await PlayFinale(targets);
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter is ArtFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private async Task PlayFirstMovement(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
        await CreatureCmd.GainBlock(Creature, FirstMovementBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<ArtifactPower>(Creature, FirstMovementArtifact, Creature, null);
        await PowerCmdCompat.Apply<BufferPower>(Creature, FirstMovementBuffer, Creature, null);
    }

    private async Task PlaySecondMovement(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < SecondMovementHits; i++)
        {
            AttackCommand attack = await ExecuteGroupAttack(SecondMovementDamage);
            foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
            {
                await PowerCmdCompat.Apply<FanaticWorshipPower>(target, 1m, Creature, null);
            }
        }
    }

    private async Task PlayThirdMovement(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await ExecuteGroupAttack(ThirdMovementDamage);
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            await PowerCmdCompat.Apply<WeakPower>(target, ThirdMovementWeak, Creature, null);
        }
    }

    private async Task PlayFourthMovement(IReadOnlyList<Creature> targets)
    {
        HashSet<Creature> hitTargets = [];
        for (int i = 0; i < FourthMovementHits; i++)
        {
            AttackCommand attack = await ExecuteGroupAttack(FourthMovementDamage);
            foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
            {
                hitTargets.Add(target);
            }
        }

        foreach (Creature target in hitTargets)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<VoidCard>(
                target,
                PileType.Discard,
                6,
                addedByPlayer: false);
        }
    }

    private async Task PlayFinale(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await ExecuteGroupAttack(FinaleDamage, anim: "Special");
        foreach (Creature target in GetUnblockedPlayerHitTargets(attack))
        {
            FanaticWorshipPower? worship = target.GetPower<FanaticWorshipPower>();
            if (worship == null)
            {
                continue;
            }

            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
                target,
                FinaleChaosDamage,
                Creature,
                null);
            await PowerCmd.Remove(worship);
        }
    }

    private Task<AttackCommand> ExecuteGroupAttack(int damage, string anim = "Attack")
    {
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(anim, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private static IReadOnlyList<Creature> GetUnblockedPlayerHitTargets(AttackCommand attack)
    {
        return AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive && result.Receiver.IsPlayer && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
    }

    private static AbstractIntent[] CreateMovementIntents(int movement)
    {
        return movement switch
        {
            1 => [new CombinedDefendBuffIntent(FirstMovementBlock)],
            2 => [new CombinedAttackDebuffIntent(() => SecondMovementDamage, () => SecondMovementHits), new DebuffIntent()],
            3 => [new CombinedAttackDebuffIntent(() => ThirdMovementDamage), new DebuffIntent()],
            4 => [new CombinedAttackDebuffIntent(() => FourthMovementDamage, () => FourthMovementHits), new StatusIntent(6)],
            5 => [new CombinedAttackDebuffIntent(() => FinaleDamage), new DebuffIntent(strong: true)],
            6 => [new HealIntent(), new BuffIntent()],
            _ => [new UnknownIntent()]
        };
    }

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        for (int movement = 1; movement <= StageTurnCount; movement++)
        {
            foreach (AbstractIntent intent in CreateMovementIntents(movement))
            {
                yield return intent;
            }
        }

        foreach (AbstractIntent intent in CreateMovementIntents(6))
        {
            yield return intent;
        }
    }

    private static LibraryCreatureResistanceData.Resistance ImmuneResistance()
    {
        return new LibraryCreatureResistanceData.Resistance
        {
            Slash = LibraryResistanceLevel.Immune,
            Pierce = LibraryResistanceLevel.Immune,
            Blunt = LibraryResistanceLevel.Immune
        };
    }
}
