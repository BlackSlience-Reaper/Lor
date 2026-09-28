using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters.SmilingBodies;
using LibraryOfRuina.monsters.SmilingBodies;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
using SmilingBodiesMonster = LibraryOfRuina.monsters.SmilingBodies.SmilingBodies;

namespace LibraryOfRuinaVerification;

internal static class SmilingBodiesDeathVerificationPatch
{
    private const string VerifyArg = "lor-verify-smiling-bodies-death";
    private const string LogPrefix = "[LibraryOfRuina.SmilingBodiesDeath.Verify] ";

    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() => TaskHelper.RunSafely(RunAsync())).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyArg,
                StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            await RunCoreAsync();
            Log.Info(LogPrefix + "SMILING_BODIES_DEATH_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "SMILING_BODIES_DEATH_FAILED: " + exception);
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
            "SMILINGBODIESDEATHVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<SmilingBodiesStrong>().ToMutable(),
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "Smiling Bodies combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        Creature bossCreature = combatState.Enemies.Single(enemy =>
            enemy.Monster is SmilingBodiesMonster);
        var boss = (SmilingBodiesMonster)bossCreature.Monster!;

        Require(
            boss.Phase == SmilingBodiesPhase.Second,
            "Expected the encounter to start in Phase 2.");

        await CreatureCmd.Kill(bossCreature);
        await WaitFrames(10);

        Require(bossCreature.IsDead, "Lethal damage did not leave the boss at 0 HP.");
        Require(boss.IsFakeDead, "Phase 2 lethal damage did not enter fake death.");
        Require(
            boss.NextMove.Id == SmilingBodiesMonster.ReviveMoveId,
            "Phase 2 lethal damage left the boss on " + boss.NextMove.Id
            + " instead of scheduling REVIVE for the next enemy action.");

        await boss.PerformMove();
        await WaitFrames(10);

        Require(
            boss.Phase == SmilingBodiesPhase.First,
            "REVIVE did not downgrade the boss to Phase 1.");
        Require(
            bossCreature.CurrentHp > 0 && bossCreature.IsAlive,
            "REVIVE completed without restoring positive HP.");
        Require(!boss.IsFakeDead, "REVIVE left the boss in fake death.");

        Log.Info(
            LogPrefix
            + $"phase={boss.Phase} hp={bossCreature.CurrentHp}/{bossCreature.MaxHp} "
            + $"nextMove={boss.NextMove.Id}");

        await CreatureCmd.Kill(bossCreature);
        await WaitFrames(10);

        Require(bossCreature.IsDead, "Phase 1 lethal damage did not kill the boss.");
        bool combatEnded = !CombatManager.Instance.IsInProgress
                           || await CombatManager.Instance.CheckWinCondition();
        await WaitFrames(10);
        Require(
            combatEnded || !CombatManager.Instance.IsInProgress,
            "Phase 1 lethal damage did not settle the encounter.");
        Log.Info(LogPrefix + "finalDeathSettled=True");
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

            await WaitFrame();
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
    }

    private static async Task WaitFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            await WaitFrame();
        }
    }

    private static async Task WaitFrame()
    {
        SceneTree tree = NGame.Instance?.GetTree()
            ?? (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
