using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.GalaxyChild;
using LibraryOfRuina.monsters.GalaxyChild;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class GalaxyDoomVerificationPatch
{
    private const string VerifyArg = "lor-verify-galaxy-doom";
    private const string LogPrefix = "[LibraryOfRuina.GalaxyDoom.Verify] ";

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
                   .Any(arg => string.Equals(arg.TrimStart('-'), VerifyArg, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunAsync()
    {
        try
        {
            await RunCoreAsync();
            Log.Info(LogPrefix + "GALAXY_DOOM_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "GALAXY_DOOM_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");

        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "GALAXYDOOMVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<GalaxyChildWeak>().ToMutable(),
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
            "GalaxyChildWeak combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        IReadOnlyList<Creature> friends = combatState.Enemies
            .Where(static enemy => enemy.Monster is GalaxyFriend)
            .ToArray();
        Require(friends.Count == 2, "Expected exactly two Galaxy Friends, found " + friends.Count + ".");

        var context = new ThrowingPlayerChoiceContext();
        foreach (Creature friend in friends)
        {
            await PowerCmdCompat.Apply<DoomPower>(
                context,
                friend,
                Math.Max(friend.CurrentHp, friend.MaxHp),
                applier: friend,
                cardSource: null,
                silent: true);
        }

        IReadOnlyList<Creature> doomedCreatures = DoomPower.GetDoomedCreatures(friends);
        Require(doomedCreatures.Count == 2, "Expected both Galaxy Friends to be doomed.");

        await DoomPower.DoomKill(doomedCreatures);
        await WaitFrames(20);

        bool ended = await CombatManager.Instance.CheckWinCondition();
        await WaitFrames(20);

        Require(ended || !CombatManager.Instance.IsInProgress, "Combat did not end after simultaneous Galaxy Friend Doom.");
        Require(friends.All(static friend => friend.IsDead), "Not all Galaxy Friends were force-killed after Parting Tears.");
        Log.Info(LogPrefix + "combatEnded=" + !CombatManager.Instance.IsInProgress);
    }

    private static async Task WaitUntil(Func<bool> predicate, string description, int maxFrames = 900)
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
        for (int i = 0; i < frames; i++)
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
