using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.QueenOfHatred;
using LibraryOfRuina.powers.BigBadWolf;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class BigBadWolfTargetingVerificationPatch
{
    private const string VerifyArg = "lor-verify-big-bad-wolf-targeting";
    private const string LogPrefix =
        "[LibraryOfRuina.BigBadWolfTargeting.Verify] ";
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

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
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            await RunCoreAsync();
            Log.Info(LogPrefix + "BIG_BAD_WOLF_TARGETING_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "BIG_BAD_WOLF_TARGETING_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        Player[] players = await StartFakeMultiplayerFight();
        try
        {
            CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Combat state is null.");
            Creature protectedPlayer = players[0].Creature;
            Creature friendlyPlayer = players[1].Creature;
            Creature enemy = combatState.Enemies.First(static creature => creature.IsAlive);

            BigBadWolfUntargetablePower? protection =
                await PowerCmdCompat.Apply<BigBadWolfUntargetablePower>(
                    protectedPlayer,
                    1m,
                    protectedPlayer,
                    null,
                    silent: true);
            Require(protection != null, "Failed to apply Big Bad Wolf protection.");
            Require(protectedPlayer.IsHittable,
                "Protected player was not hittable during the player side.");
            Require(UntargetableInteractionFilter.CanBeSelected(
                    protectedPlayer,
                    protectedPlayer),
                "The protected player could not select itself.");
            Require(UntargetableInteractionFilter.CanBeSelected(
                    protectedPlayer,
                    friendlyPlayer),
                "A friendly player could not select the protected player.");
            Require(!UntargetableInteractionFilter.CanBeSelected(
                    protectedPlayer,
                    enemy),
                "An enemy could select the protected player.");
            Require(UntargetableInteractionFilter.FilterPlayerAttackTargets(
                        [protectedPlayer],
                        enemy)
                    .Count == 0,
                "Enemy attack target filtering retained the protected player.");
            Require(!TargetedIntentIndicatorPatch.CanPointAtTarget(
                    protectedPlayer,
                    enemy),
                "An enemy intent line could point at the protected player.");
            Require(TargetedIntentIndicatorPatch.CanPointAtTarget(
                    protectedPlayer,
                    friendlyPlayer),
                "A friendly intent line could not point at the protected player.");

            Coordinate friendlyCard = combatState.CreateCard<Coordinate>(players[1]);
            Require(friendlyCard.IsValidTarget(protectedPlayer),
                "A friendly AnyAlly card could not select the protected player.");

            StrengthPower? strength = await PowerCmdCompat.Apply<StrengthPower>(
                protectedPlayer,
                1m,
                friendlyPlayer,
                friendlyCard,
                silent: true);
            Require(strength?.Amount == 1m,
                "A friendly power could not be applied to the protected player.");

            combatState.CurrentSide = CombatSide.Enemy;
            Require(!protectedPlayer.IsHittable,
                "Protected player remained hittable during the enemy side.");
            Require(friendlyCard.IsValidTarget(protectedPlayer),
                "Friendly source-aware card targeting failed outside the player side.");
            combatState.CurrentSide = CombatSide.Player;
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Big Bad Wolf targeting verifier cleanup");
        }
    }

    private static async Task<Player[]> StartFakeMultiplayerFight()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        Player[] players =
        [
            Player.CreateForNewRun(
                ModelDb.Character<Ironclad>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                1uL),
            Player.CreateForNewRun(
                ModelDb.Character<Silent>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                2uL)
        ];
        RunState runState = RunState.CreateForNewRun(
            players,
            ActModel.GetDefaultList()
                .Select(static act => act.ToMutable())
                .ToList(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            ascensionLevel: 0,
            "BIGBADWOLFTARGETINGVERIFY");
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);

        MethodInfo startRun = typeof(NGame).GetMethod(
                "StartRun",
                PrivateInstance)
            ?? throw new InvalidOperationException(
                "NGame.StartRun was unavailable for fake multiplayer.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException(
                "NGame.StartRun did not return a Task.");
        await startRunTask;

        foreach (Player player in players)
        {
            foreach (RelicModel relic in player.Relics.ToArray())
            {
                await RelicCmd.Remove(relic);
            }
        }

        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<SlimesWeak>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "Big Bad Wolf targeting fake multiplayer combat start");
        return players;
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
