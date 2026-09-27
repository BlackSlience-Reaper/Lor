using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters.FairyFestival;
using LibraryOfRuina.monsters.FairyFestival;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class FairyMassCareVerificationPatch
{
    private const string VerifyArg = "lor-verify-fairy-mass-care";
    private const string LogPrefix = "[LibraryOfRuina.FairyMassCare.Verify] ";

    private static bool _started;

    private static void Postfix()
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
            Log.Info(LogPrefix + "FAIRY_MASS_CARE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "FAIRY_MASS_CARE_FAILED: " + exception);
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
            "FAIRYMASSCAREVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<FairyFestivalStrong>().ToMutable(),
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "Fairy Festival combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        Creature player = combatState.Players.Single().Creature;
        Creature queen = combatState.Enemies.Single(enemy => enemy.Monster is FairyQueen);
        Creature[] masses = combatState.Enemies
            .Where(enemy => enemy.Monster is FairyMass)
            .ToArray();

        Require(queen.IsAlive, "Fairy Queen was not alive at verification start.");
        Require(masses.Length == 2, "Expected exactly two Fairy Masses, found " + masses.Length + ".");

        Creature protectedMass = masses[0];
        LibraryVulnerablePower? vulnerable = await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
            protectedMass,
            amount: 2m,
            turns: -1,
            applier: player,
            cardSource: null,
            silent: true);
        Require(vulnerable != null, "Failed to apply LibraryVulnerablePower (易损).");

        await CreatureCmd.SetCurrentHp(protectedMass, 5m);
        DamageResult result = (await LibraryCreatureCmd.Damage(
                new ThrowingPlayerChoiceContext(),
                protectedMass,
                5m,
                ValueProp.Move,
                player,
                cardSource: null))
            .Single();
        await WaitFrames(10);

        Require(
            protectedMass.IsAlive && protectedMass.CurrentHp == 1,
            "Library 易损 bypassed Fairy Mass 1-HP preservation: "
            + $"hp={protectedMass.CurrentHp}, isDead={protectedMass.IsDead}, "
            + $"damage={result.UnblockedDamage}, overkill={result.OverkillDamage}.");

        Creature forceKilledMass = masses[1];
        await CreatureCmd.Kill(forceKilledMass, force: true);
        await WaitFrames(10);
        Require(forceKilledMass.IsDead, "Forced Fairy Mass death was incorrectly prevented.");

        Log.Info(
            LogPrefix
            + $"preservedHp={protectedMass.CurrentHp} vulnerable={vulnerable!.Amount} "
            + $"forcedDeath={forceKilledMass.IsDead}");
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
