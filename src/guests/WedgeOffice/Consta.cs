using System.Threading.Tasks;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.BrotherhoodOfIron;
using LibraryOfRuina.intents;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.BrotherhoodOfIron;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.guests.WedgeOffice;

public sealed class Consta : BrotherhoodOfIronMonster
{
    private const int DriedUpStrength = 2;
    private const int EndureBlock = 8;

    internal override SpriteVisualProfile SpriteProfile =>
        ConstaCreatureVisuals.Profile;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 28, 26);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 31, 29);

    private int YouOnlyLiveOnceDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var youOnlyLiveOnce = new MoveState(
            "YOU_ONLY_LIVE_ONCE",
            YouOnlyLiveOnceMove,
            new SingleAttackIntent(YouOnlyLiveOnceDamage),
            new DetailedStatusCardIntent<Dazed>(
                DazedCount,
                PileType.Discard,
                showSingleTargetMarker: false));

        var driedUp = new MoveState(
            "DRIED_UP",
            DriedUpMove,
            new BuffIntent());

        var endure = new MoveState(
            "ENDURE",
            EndureMove,
            new DefendIntent());

        driedUp.FollowUpState = youOnlyLiveOnce;
        youOnlyLiveOnce.FollowUpState = endure;
        endure.FollowUpState = driedUp;

        states.Add(youOnlyLiveOnce);
        states.Add(driedUp);
        states.Add(endure);

        return new MonsterMoveStateMachine(states, driedUp);
    }

    private async Task YouOnlyLiveOnceMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(YouOnlyLiveOnceDamage, 1, targets);
        await AddDazedToDiscard(targets);
    }

    private async Task DriedUpMove(IReadOnlyList<Creature> targets)
    {
        await TriggerCast();
        await GainStrength(DriedUpStrength);
    }

    private async Task EndureMove(IReadOnlyList<Creature> targets)
    {
        await TriggerCast();
        await GainMoveBlock(EndureBlock);
    }
}
