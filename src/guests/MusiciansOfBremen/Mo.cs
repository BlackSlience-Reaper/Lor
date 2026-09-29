using System.Threading.Tasks;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.guests.BrotherhoodOfIron;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.BrotherhoodOfIron;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.guests.MusiciansOfBremen;

public sealed class Mo : BrotherhoodOfIronMonster
{
    private const int BlowItUpHits = 2;
    private const int EndureBlock = 7;
    private const int DodgeAndStrikeBlock = 4;

    internal override SpriteVisualProfile SpriteProfile =>
        MoCreatureVisuals.Profile;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 31, 29);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 34, 32);

    private int BlowItUpDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int DodgeAndStrikeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var blowItUp = new MoveState(
            "BLOW_IT_UP",
            BlowItUpMove,
            new MultiAttackIntent(BlowItUpDamage, BlowItUpHits),
            new DetailedStatusCardIntent<Dazed>(
                DazedCount,
                PileType.Discard,
                showSingleTargetMarker: false));

        var endure = new MoveState(
            "ENDURE",
            EndureMove,
            new DefendIntent(),
            new DetailedStatusCardIntent<Dazed>(
                DazedCount,
                PileType.Discard,
                showSingleTargetMarker: false));

        var dodgeAndStrike = new MoveState(
            "DODGE_AND_STRIKE",
            DodgeAndStrikeMove,
            new SingleAttackIntent(DodgeAndStrikeDamage),
            new DefendIntent());

        dodgeAndStrike.FollowUpState = endure;
        endure.FollowUpState = blowItUp;
        blowItUp.FollowUpState = dodgeAndStrike;

        states.Add(blowItUp);
        states.Add(endure);
        states.Add(dodgeAndStrike);

        return new MonsterMoveStateMachine(states, dodgeAndStrike);
    }

    private async Task BlowItUpMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(BlowItUpDamage, BlowItUpHits, targets);
        await AddDazedToDiscard(targets);
    }

    private async Task EndureMove(IReadOnlyList<Creature> targets)
    {
        await TriggerCast();
        await GainMoveBlock(EndureBlock);
        await AddDazedToDiscard(targets);
    }

    private async Task DodgeAndStrikeMove(IReadOnlyList<Creature> targets)
    {
        await PerformAttack(DodgeAndStrikeDamage, 1, targets);
        await GainMoveBlock(DodgeAndStrikeBlock);
    }
}
