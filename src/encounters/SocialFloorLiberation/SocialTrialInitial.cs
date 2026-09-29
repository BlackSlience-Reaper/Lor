using System.Threading.Tasks;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.encounters.SocialFloorLiberation;

/// <summary>开场：王座只执行一次初始序列，第一个敌方回合结束后进入樵夫试炼。</summary>
internal sealed class SocialTrialInitial : SocialTrial
{
    internal override FalseThroneMove ResolveDefaultMove(int round) =>
        FalseThroneMove.InitialSequence;

    internal override async Task AfterEnemyTurnAsync(
        SocialFloorLiberationEncounter encounter,
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        encounter.QueueTrial(SocialFloorTrial.Woodsman);
        await encounter.EnterPendingTrial(choiceContext, boss, combatState);
    }
}
