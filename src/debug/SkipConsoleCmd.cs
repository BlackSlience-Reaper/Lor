using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.debug;





public sealed class SkipConsoleCmd : AbstractConsoleCmd
{
    private const string LogPrefix = "[SkipCmd]";

    public override string CmdName => "skip";

    public override string Args => "";

    public override string Description => "Fallback: force-resolve stuck event/combat and continue.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!RunManager.Instance.IsInProgress)
        {
            return new CmdResult(success: false, "A run does not appear to be in progress.");
        }

        if (CombatManager.Instance.IsInProgress)
        {
            return new CmdResult(SkipCombatAsync(), success: true, "Skip requested: resolving combat.");
        }

        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is EventRoom)
        {
            return new CmdResult(SkipEventAsync(), success: true, "Skip requested: resolving event.");
        }

        return new CmdResult(success: false, "Skip can only be used in an active combat or event room.");
    }

    private static async Task SkipCombatAsync()
    {
        CombatStateLike? state = CombatManager.Instance.DebugOnlyGetState();
        if (!CombatManager.Instance.IsInProgress || state == null)
        {
            return;
        }

        List<Creature> enemies = state.Enemies.Where(static enemy => enemy.IsAlive).ToList();
        foreach (Creature enemy in enemies)
        {
            enemy.RemoveAllPowersInternalExcept();
            await CreatureCmd.Kill(enemy, force: true);
        }

        await CombatManager.Instance.CheckWinCondition();
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        Log.Warn($"{LogPrefix} Combat remained in progress after enemy cleanup; forcing EndCombatInternal().");
        await CombatManagerTurnMethodCompat.EndCombatAsync(CombatManager.Instance);
    }

    private static async Task SkipEventAsync()
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not EventRoom)
        {
            return;
        }

        NMapScreen? mapScreen = NMapScreen.Instance;
        if (mapScreen != null)
        {
            mapScreen.SetTravelEnabled(enabled: true);
            mapScreen.Open();
            if (mapScreen.IsTravelEnabled)
            {
                return;
            }
        }

        Log.Warn($"{LogPrefix} Failed to enable map travel from event room; forcing map room transition.");
        await RunManager.Instance.EnterRoom(new MapRoom());
    }
}
