using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.content.liberation.Social;

/// <summary>
/// 家园试炼：王座开始承受混乱伤害。第 0 回合是眩晕试炼、第 1 回合友好问候，之后每回合用 MonsterAi 随机数抽一个行动。
/// 王座排入愤怒试炼后，不在敌方回合开始时进入，而是留到这个敌方回合结束。
/// </summary>
internal sealed class SocialTrialHome : SocialTrial
{
    internal override FalseThroneMove ResolveDefaultMove(int round) =>
        ResolveHomeMove(
            round,
            static () =>
                FalseThroneMove.HomeOverflowingLightInsolence);

    internal override async Task SetupAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        await boss.OpenHomeChaoGate();
    }

    internal override async Task ResumeAsync(FalseThrone boss)
    {
        await boss.OpenHomeChaoGate();
    }

    internal override bool DefersPendingTrialToEnemyTurnEnd(
        SocialFloorLiberationEncounter encounter) =>
        encounter.HasPendingTrial
        && encounter.PendingTrial == SocialFloorTrial.Rage;

    internal override async Task AfterEnemyTurnAsync(
        SocialFloorLiberationEncounter encounter,
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (DefersPendingTrialToEnemyTurnEnd(encounter))
        {
            await encounter.EnterPendingTrial(choiceContext, boss, combatState);
            return;
        }

        encounter.PlannedMove = ResolveHomeMove(
            encounter.TrialRound,
            boss.RollHomeMove);
        boss.ForcePlannedMove(encounter.PlannedMove);
    }

    /// <summary>第 2 回合起才调用 <paramref name="normalMoveFactory"/>，所以前两回合不消耗随机数。</summary>
    private static FalseThroneMove ResolveHomeMove(
        int round,
        Func<FalseThroneMove> normalMoveFactory) => round switch
    {
        <= 0 => FalseThroneMove.StunTrial,
        1 => FalseThroneMove.FriendlyGreeting,
        _ => normalMoveFactory()
    };
}
