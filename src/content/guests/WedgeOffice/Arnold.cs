using System.Threading.Tasks;
using LibraryOfRuina.content.guests.BrotherhoodOfIron;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.guests.WedgeOffice;

public sealed class Arnold : BrotherhoodOfIronMonster
{
    private const int ChargeUpStrength = 4;
    private const int ChopItOffHits = 3;
    private const int EndureBlock = 7;

    internal override SpriteVisualProfile SpriteProfile =>
        ArnoldCreatureVisuals.Profile;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 28, 26);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 31, 29);

    private int ChopItOffDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 1, 0);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var chargeUp = new MoveState(
            "CHARGE_UP",
            ChargeUpMove,
            new BuffIntent());

        var chopItOff = new MoveState(
            "CHOP_IT_OFF",
            ChopItOffMove,
            new MultiAttackIntent(ChopItOffDamage, ChopItOffHits));

        var endure = new MoveState(
            "ENDURE",
            EndureMove,
            new DefendIntent(),
            new DetailedStatusCardIntent<Dazed>(
                DazedCount,
                PileType.Discard,
                showSingleTargetMarker: false));

        endure.FollowUpState = chargeUp;
        chargeUp.FollowUpState = chopItOff;
        chopItOff.FollowUpState = endure;

        states.Add(chargeUp);
        states.Add(chopItOff);
        states.Add(endure);

        return new MonsterMoveStateMachine(states, endure);
    }

    private async Task ChargeUpMove(IReadOnlyList<Creature> targets)
    {
        await TriggerCast();
        await GainStrength(ChargeUpStrength);
    }

    private async Task ChopItOffMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(ChopItOffDamage, ChopItOffHits, targets);
    }

    private async Task EndureMove(IReadOnlyList<Creature> targets)
    {
        await TriggerCast();
        await GainMoveBlock(EndureBlock);
        await AddDazedToDiscard(targets);
    }
}
