using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters.Nosferatu;
using LibraryOfRuina.monsters.Nosferatu;
using LibraryOfRuina.powers.Nosferatu;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class NosferatuDoomLockVerificationPatch
{
    private const string VerifyArg = "lor-verify-nosferatu-doom-lock";
    private const string LogPrefix = "[LibraryOfRuina.NosferatuDoomLock.Verify] ";

    private static bool _started;

    internal static void Start()
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
            Log.Info(LogPrefix + "NOSFERATU_DOOM_LOCK_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "NOSFERATU_DOOM_LOCK_FAIL\n" + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");

        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "NOSFERATUDOOMLOCKVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Elite,
            MapPointType.Unassigned,
            ModelDb.Encounter<NosferatuElite>().ToMutable(),
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "Nosferatu combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        Creature creature = combatState.Enemies.Single(static enemy =>
            enemy.Monster is Nosferatu);
        Nosferatu boss = (Nosferatu)creature.Monster!;
        NosferatuTransformPower transformPower = creature
            .GetPower<NosferatuTransformPower>()
            ?? throw new InvalidOperationException(
                "Nosferatu transform lock was missing.");
        NCombatRoom room = NCombatRoom.Instance!;
        NCreature originalNode = room.GetCreatureNode(creature)
            ?? throw new InvalidOperationException(
                "Nosferatu creature node was missing before Doom.");

        await PowerCmdCompat.Apply<DoomPower>(
            new ThrowingPlayerChoiceContext(),
            creature,
            creature.MaxHp,
            applier: creature,
            cardSource: null,
            silent: true);
        Require(DoomPower.GetDoomedCreatures([creature]).Count == 1,
            "Nosferatu was not marked for Doom.");

        await DoomPower.DoomKill([creature]);

        int threshold = Math.Max(1, (int)Math.Ceiling(creature.MaxHp * 0.5m));
        NCreature? retainedNode = room.GetCreatureNode(creature);
        Require(creature.CurrentHp >= threshold
                && (boss.IsTransformed || transformPower.IsPendingTransform),
            "Doom did not leave Nosferatu locked at the transform threshold.");
        Require(combatState.Enemies.Contains(creature),
            "Doom removed Nosferatu from combat despite the transform lock.");
        Require(retainedNode != null
                && ReferenceEquals(retainedNode, originalNode)
                && GodotObject.IsInstanceValid(retainedNode)
                && retainedNode.IsInsideTree(),
            "Doom removed Nosferatu's creature node despite the transform lock.");

        if (!boss.IsTransformed)
        {
            Require(!boss.ShouldDisappearFromDoom,
                "The untransformed Nosferatu still allowed Doom node removal.");
            await boss.TransformToBloodfiend();
        }

        Require(boss.IsTransformed && boss.ShouldDisappearFromDoom,
            "Transformed Nosferatu did not restore normal Doom death visuals.");
    }

    private static void ArmActLikeIt2OneShotVanillaEntry()
    {
        Type? gateType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(
                "ActLikeIt2.Runtime.ActSelectionGate",
                throwOnError: false))
            .FirstOrDefault(static type => type != null);
        PropertyInfo? skipNextFork = gateType?.GetProperty(
            "SkipNextFork",
            BindingFlags.Static | BindingFlags.Public
                                | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
        }
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
