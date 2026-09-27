using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.events.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class TechnologyFloorLiberationSettlementVerificationPatch
{
    private const string VerifyArg = "lor-verify-technology-floor-settlement";
    private const string LogPrefix = "[LibraryOfRuina.TechnologyFloorSettlement.Verify] ";

    private static bool _started;

    private static void Postfix()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg()
    {
        return CommandLineHelper.HasArg(VerifyArg)
               || Environment.GetCommandLineArgs()
                   .Any(arg => string.Equals(
                       arg.TrimStart('-'),
                       VerifyArg,
                       StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunAsync()
    {
        try
        {
            await RunCoreAsync();
            Log.Info(LogPrefix + "TECHNOLOGY_FLOOR_SETTLEMENT_POLICY_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "TECHNOLOGY_FLOOR_SETTLEMENT_POLICY_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");

        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "TECHNOLOGYFLOORSETTLEMENTVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);

        var encounter = (TechnologyFloorLiberationEncounter)ModelDb
            .Encounter<TechnologyFloorLiberationEncounter>()
            .ToMutable();
        LoadEncounterState(encounter, killedBossCount: 1, settlementTriggered: false);

        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "Technology Floor liberation phase two combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var activeEncounter = combatState.Encounter as TechnologyFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "TechnologyFloorLiberationEncounter was not active.");
        Creature player = combatState.PlayerCreatures.Single();

        Require(
            !activeEncounter.ShouldPreventPlayerDeath(player),
            "One defeated boss incorrectly triggered lethal-damage settlement protection.");

        LoadEncounterState(activeEncounter, killedBossCount: 2, settlementTriggered: false);
        Require(
            activeEncounter.ShouldPreventPlayerDeath(player),
            "Two defeated bosses did not unlock lethal-damage settlement protection.");

        LoadEncounterState(activeEncounter, killedBossCount: 1, settlementTriggered: true);
        TechnologyFloorLiberationSettlementStore.Record(activeEncounter);
        Require(
            !TechnologyFloorLiberationSettlementStore.PendingSettlement,
            "One defeated boss incorrectly queued the settlement event.");
        Require(
            TechnologyFloorLiberationSettlementStore
                .RequiresLegacySingleKillDefeatRecovery(activeEncounter),
            "The legacy one-kill lethal-victory state was not recognized for defeat recovery.");

        LoadEncounterState(activeEncounter, killedBossCount: 2, settlementTriggered: true);
        TechnologyFloorLiberationSettlementStore.Record(activeEncounter);
        Require(
            TechnologyFloorLiberationSettlementStore.PendingSettlement,
            "Two defeated bosses did not queue the settlement event.");
        Require(
            !TechnologyFloorLiberationSettlementStore
                .RequiresLegacySingleKillDefeatRecovery(activeEncounter),
            "A valid two-kill settlement was incorrectly classified as legacy corruption.");
        TechnologyFloorLiberationSettlementStore.Consume();

        LoadEncounterState(activeEncounter, killedBossCount: 1, settlementTriggered: true);
        await RunManager.Instance.ProceedFromTerminalRewardsScreen();
        Require(
            RunManager.Instance.DebugOnlyGetState()?.IsGameOver == true,
            "The legacy one-kill lethal-victory state did not resolve as a normal defeat.");
    }

    private static void LoadEncounterState(
        TechnologyFloorLiberationEncounter encounter,
        int killedBossCount,
        bool settlementTriggered)
    {
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "2",
            ["KilledBossCount"] = killedBossCount.ToString(),
            ["TransitionPending"] = "False",
            ["SettlementTriggered"] = settlementTriggered.ToString(),
            ["EndedByLethalDamage"] = settlementTriggered.ToString()
        });
    }

    private static async Task WaitUntil(
        Func<bool> predicate,
        string description,
        int maxFrames = 900)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
            {
                return;
            }

            SceneTree tree = NGame.Instance?.GetTree()
                ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
