using System.Linq;
using MegaCrit.Sts2.Core.Combat;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

internal static class SpiderBudHuntTiming
{
    internal static bool HasPlayerResponseWindow(Creature creature)
    {
        CombatManager manager = CombatManager.Instance;
        // AllPlayersReadyToEndTurn 在单人模式下恒为 true，必须检查各玩家真实的结束回合状态。
        // 使用同步的玩家状态，确保玩家回合内立即切换，所有玩家结束回合后才延后。
        return manager.IsInProgress
            && creature.CombatState?.CurrentSide == CombatSide.Player
            && !manager.EndingPlayerTurnPhaseOne
            && !manager.EndingPlayerTurnPhaseTwo
            && creature.CombatState.Players.Any(player =>
                !manager.IsPlayerReadyToEndTurn(player));
    }
}
