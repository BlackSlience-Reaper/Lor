using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.WedgeOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.guests.WedgeOffice;

public sealed class Eri : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 41, 39);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 42);

    public override IEnumerable<string> AssetPaths =>
        EriCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int WallopDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private int TestFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private int TestSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private const int WallopHits = 2;
    private const int FeelinGoodDamage = 5;
    private const int FeelinGoodWeak = 1;
    private const int TestBlock = 6;
    private const int NextTurnStrength = 1;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var wallop = new MoveState(
            "WALLOP",
            WallopMove,
            new MultiAttackIntent(WallopDamage, WallopHits),
            new BuffIntent());

        var feelinGood = new MoveState(
            "FEELIN_GOOD",
            FeelinGoodMove,
            new SingleAttackIntent(FeelinGoodDamage),
            new DebuffIntent());

        var timeForALittleTest = new MoveState(
            "TIME_FOR_A_LITTLE_TEST",
            TimeForALittleTestMove,
            new SingleAttackIntent(TestFirstDamage),
            new SingleAttackIntent(TestSecondDamage),
            new DefendIntent());

        wallop.FollowUpState = feelinGood;
        feelinGood.FollowUpState = timeForALittleTest;
        timeForALittleTest.FollowUpState = wallop;

        states.Add(wallop);
        states.Add(feelinGood);
        states.Add(timeForALittleTest);

        return new MonsterMoveStateMachine(states, wallop);
    }

    private async Task WallopMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(WallopDamage)
                .FromMonster(this)
                .WithHitCount(WallopHits)
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
        await ApplyNextTurnStrengthToLivingEnemies(NextTurnStrength);
        
    }

    private async Task ApplyNextTurnStrengthToLivingEnemies(decimal amount)
    {
        IReadOnlyList<Creature> livingEnemies = CombatState.Enemies
        .Where(enemies => enemies.IsAlive)
        .ToList();
        foreach (var enemy in livingEnemies)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(livingEnemies, amount, Creature, null);
        }
    }

    private async Task FeelinGoodMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(FeelinGoodDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await PowerCmdCompat.Apply<WeakPower>(targets, FeelinGoodWeak, Creature, null);
    }

    private async Task TimeForALittleTestMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        await CreatureCmd.GainBlock(Creature, TestBlock, ValueProp.Move, null);

        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(TestFirstDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(TestSecondDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
    }
}
