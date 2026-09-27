using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.powers.WedgeOffice;
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

public sealed class Oscar : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 89, 87);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 92, 90);

    public override IEnumerable<string> AssetPaths =>
        OscarCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int TranspierceDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private int SparkingSpearDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int HighSpeedStabbingDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    private const int TranspierceBlock = 7;
    private const int SparkingSpearHits = 2;
    private const int SparkingSpearWeak = 1;
    private const int HighSpeedStabbingHits = 3;
    private const int PerseveranceGuardAmount = 25;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<WedgePerseverancePower>(Creature, PerseveranceGuardAmount, Creature, null, silent: true);
        await PowerCmdCompat.Apply<WedgePiercingPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var transpierce = new MoveState(
            "TRANSPIERCE",
            TranspierceMove,
            new SingleAttackIntent(TranspierceDamage),
            new DefendIntent());

        var sparkingSpear = new MoveState(
            "SPARKING_SPEAR",
            SparkingSpearMove,
            new MultiAttackIntent(SparkingSpearDamage, SparkingSpearHits),
            new DebuffIntent());

        var highSpeedStabbing = new MoveState(
            "HIGH_SPEED_STABBING",
            HighSpeedStabbingMove,
            new MultiAttackIntent(HighSpeedStabbingDamage, HighSpeedStabbingHits));

        sparkingSpear.FollowUpState = transpierce;
        transpierce.FollowUpState = highSpeedStabbing;
        highSpeedStabbing.FollowUpState = sparkingSpear;

        states.Add(transpierce);
        states.Add(sparkingSpear);
        states.Add(highSpeedStabbing);

        return new MonsterMoveStateMachine(states, sparkingSpear);
    }

    private async Task TranspierceMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(TranspierceDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await CreatureCmd.GainBlock(Creature, TranspierceBlock, ValueProp.Move, null);
    }

    private async Task SparkingSpearMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(SparkingSpearDamage)
                .FromMonster(this)
                .WithHitCount(SparkingSpearHits)
                .OnlyPlayAnimOnce()
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await PowerCmdCompat.Apply<WeakPower>(targets, SparkingSpearWeak, Creature, null);
    }

    private async Task HighSpeedStabbingMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(HighSpeedStabbingDamage)
                .FromMonster(this)
                .WithHitCount(HighSpeedStabbingHits)
                .OnlyPlayAnimOnce()
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
    }
}
