using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.guests.WedgeOffice;

public abstract class WedgeOfficeSpearTwinBase : MonsterModel
{
    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 77, 75);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 79, 78);

    public override IEnumerable<string> AssetPaths =>
        MonsterVisualCatalog.GetRequiredProfile(Id.Entry).AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    private int CollisionDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private int SpearedSweepDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private int HighSpeedStabbingDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private const int CollisionHits = 2;
    private const int CollisionStrength = 1;
    private const int SpearedSweepHits = 2;
    private const int SpearedSweepBlock = 4;
    private const int HighSpeedStabbingHits = 2;

    protected abstract bool OpensWithHighSpeedStabbing { get; }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
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

        var collision = new MoveState(
            "COLLISION",
            CollisionMove,
            new MultiAttackIntent(CollisionDamage, CollisionHits),
            new BuffIntent());

        var spearedSweep = new MoveState(
            "SPEARED_SWEEP",
            SpearedSweepMove,
            new MultiAttackIntent(SpearedSweepDamage, SpearedSweepHits),
            new DefendIntent());

        var highSpeedStabbing = new MoveState(
            "HIGH_SPEED_STABBING",
            HighSpeedStabbingMove,
            new MultiAttackIntent(HighSpeedStabbingDamage, HighSpeedStabbingHits));

        collision.FollowUpState = spearedSweep;
        spearedSweep.FollowUpState = highSpeedStabbing;
        highSpeedStabbing.FollowUpState = collision;

        states.Add(collision);
        states.Add(spearedSweep);
        states.Add(highSpeedStabbing);

        return new MonsterMoveStateMachine(
            states,
            OpensWithHighSpeedStabbing ? highSpeedStabbing : collision);
    }

    private async Task CollisionMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(CollisionDamage)
                .FromMonster(this)
                .WithHitCount(CollisionHits)
                .OnlyPlayAnimOnce()
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, CollisionStrength, Creature, null);
    }

    private async Task SpearedSweepMove(IReadOnlyList<Creature> targets)
    {
        using (new TargetedAttackLungeScope(this, targets))
        {
            await DamageCmd.Attack(SpearedSweepDamage)
                .FromMonster(this)
                .WithHitCount(SpearedSweepHits)
                .OnlyPlayAnimOnce()
                .WithAttackerAnim("Attack", 0.3f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await CreatureCmd.GainBlock(Creature, SpearedSweepBlock, ValueProp.Move, null);
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

public sealed class Pameli : WedgeOfficeSpearTwinBase
{
    protected override bool OpensWithHighSpeedStabbing => false;
}
