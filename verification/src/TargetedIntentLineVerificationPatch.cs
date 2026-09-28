using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.cards.PunishingBird;
using LibraryOfRuina.intents;
using LibraryOfRuina.intents.BigBadWolf;
using LibraryOfRuina.monsters.PunishingBird;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.PunishingBird;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;
using LittleRedEncounter = LibraryOfRuina.encounters.LittleRedMercenary.LittleRedMercenaryElite;
using LittleRedMonster = LibraryOfRuina.monsters.LittleRedMercenary.LittleRedRidingHoodedMercenary;
using PunishingBirdEncounter = LibraryOfRuina.encounters.PunishingBird.PunishingBirdStrong;
using PunishingBirdMonster = LibraryOfRuina.monsters.PunishingBird.PunishingBird;
using WolfMonster = LibraryOfRuina.monsters.LittleRedMercenary.WolfInHerNightmares;

namespace LibraryOfRuinaVerification;

internal static class TargetedIntentLineVerificationPatch
{
    private const string VerifyArg = "lor-verify-targeted-intent-lines";
    private const string LogPrefix = "[LibraryOfRuina.TargetedIntentLines.Verify] ";
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
            Log.Info(LogPrefix + "TARGETED_INTENT_LINES_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "TARGETED_INTENT_LINES_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        var failures = new List<Exception>();

        try
        {
            await VerifyWolfHowlLinesAsync();
        }
        catch (Exception ex)
        {
            failures.Add(new InvalidOperationException(
                "Wolf appended group-attack line verification failed.",
                ex));
        }
        finally
        {
            await CleanupRunAsync("Wolf target-line verifier cleanup");
        }

        try
        {
            await VerifyPunishingBirdLineAsync();
        }
        catch (Exception ex)
        {
            failures.Add(new InvalidOperationException(
                "Punishing Bird target-line verification failed.",
                ex));
        }
        finally
        {
            await CleanupRunAsync("Punishing Bird target-line verifier cleanup");
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }

    private static async Task VerifyPunishingBirdLineAsync()
    {
        CombatState combatState = await StartFightAsync<PunishingBirdEncounter>(
            RoomType.Monster,
            "Punishing Bird combat start");
        Creature bird = combatState.Enemies.Single(static creature =>
            creature.Monster is PunishingBirdMonster);
        Creature[] keepers = combatState.Enemies
            .Where(static creature => creature.Monster
                is ForestKeeperBirdBase)
            .ToArray();
        Require(keepers.Length == 2, "Punishing Bird fight did not contain two Forest Keepers.");
        Require(combatState.Players.Any(static player =>
                player.PlayerCombatState?.AllCards.Any(static card =>
                    card is ForestKeeperLockStatusCard) == true),
            "Punishing Bird fight did not create the Forest Keeper lock cards.");

        AbstractIntent targetedIntent = bird.Monster!.NextMove.Intents
            .FirstOrDefault(static intent => intent is IIntentTargetLineProvider)
            ?? throw new InvalidOperationException(
                "Punishing Bird next move did not expose a targeted intent provider.");
        var provider = (IIntentTargetLineProvider)targetedIntent;
        IReadOnlyList<IntentTargetLineTarget> lineTargets =
            provider.GetIntentTargetLineTargets(
                bird,
                combatState.PlayerCreatures);
        Creature keeperTarget = lineTargets.Single().Target;
        Require(keepers.Contains(keeperTarget),
            "Punishing Bird targeted intent did not resolve a Forest Keeper.");
        Require(!Hook.ShouldAllowTargeting(combatState, keeperTarget, out _),
            "Punishing Bird fixture no longer exercises the generic targeting filter.");
        Require(TargetedIntentIndicatorPatch.CanPointAtTarget(keeperTarget, bird),
            "Punishing Bird intent line was filtered from its Forest Keeper target.");

        PunishingBirdNoBadPower noBadPower = bird
            .GetPower<PunishingBirdNoBadPower>()
            ?? throw new InvalidOperationException(
                "Punishing Bird No Bad power was missing.");
        int strengthBefore = bird.GetPowerAmount<
            StrengthPower>();
        var nullDealerDamage = new DamageResult(bird, ValueProp.Move)
        {
            UnblockedDamage = 1
        };
        combatState.CurrentSide = CombatSide.Player;
        await noBadPower.AfterDamageReceived(
            new ThrowingPlayerChoiceContext(),
            bird,
            nullDealerDamage,
            ValueProp.Move,
            dealer: null,
            cardSource: null);
        Require(
            bird.GetPowerAmount<
                StrengthPower>()
            == strengthBefore,
            "Null-dealer damage incorrectly triggered No Bad.");

        ForestKeeperLockStatusCard lockCard = combatState.Players
            .SelectMany(static player =>
                player.PlayerCombatState?.AllCards
                ?? Array.Empty<CardModel>())
            .OfType<ForestKeeperLockStatusCard>()
            .First();
        var keeper = (ForestKeeperBirdBase)keepers[0].Monster!;
        await CreatureCmd.Kill(bird, force: true);
        Require(!lockCard.ShouldGlowGold && lockCard.ShouldGlowRed,
            "Forest Keeper lock card dereferenced a departed Punishing Bird.");

        MethodInfo chimeMove = typeof(ForestKeeperBirdBase)
            .GetMethod("ChimeMove", PrivateInstance)
            ?? throw new MissingMethodException(
                nameof(ForestKeeperBirdBase),
                "ChimeMove");
        Task chimeTask = chimeMove.Invoke(
                keeper,
                [Array.Empty<Creature>()]) as Task
            ?? throw new InvalidOperationException(
                "Forest Keeper ChimeMove did not return a Task.");
        await chimeTask;
    }

    private static async Task VerifyWolfHowlLinesAsync()
    {
        CombatState combatState = await StartFightAsync<LittleRedEncounter>(
            RoomType.Elite,
            "Little Red Mercenary combat start");
        Creature wolfCreature = combatState.Enemies.Single(static creature =>
            creature.Monster is WolfMonster);
        Creature littleRed = combatState.Enemies.Single(static creature =>
            creature.Monster is LittleRedMonster);
        var wolf = (WolfMonster)wolfCreature.Monster!;
        MoveState phaseTwoMove = typeof(WolfMonster)
                .GetField("_cruelClawsWithHowlState", PrivateInstance)?
                .GetValue(wolf) as MoveState
            ?? throw new InvalidOperationException(
                "Wolf phase-two move state was unavailable.");
        AbstractIntent howlIntent = phaseTwoMove.Intents
            .Single(static intent => intent is WolfHowlIntent);
        Require(howlIntent is IIntentTargetLineProvider,
            "Wolf's appended group-attack intent did not own its target-line resolver.");

        var provider = (IIntentTargetLineProvider)howlIntent;
        Creature[] expectedTargets = combatState.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .Append(littleRed)
            .Distinct()
            .ToArray();
        Creature[] actualTargets = provider
            .GetIntentTargetLineTargets(
                wolfCreature,
                combatState.PlayerCreatures)
            .Select(static target => target.Target)
            .Distinct()
            .ToArray();
        Require(actualTargets.Length == expectedTargets.Length
                && expectedTargets.All(actualTargets.Contains),
            "Wolf's appended group-attack intent did not point to Little Red and every living player. "
            + $"expected={expectedTargets.Length}, actual={actualTargets.Length}");
    }

    private static async Task<CombatState> StartFightAsync<TEncounter>(
        RoomType roomType,
        string description)
        where TEncounter : EncounterModel
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
            "TARGETEDINTENTLINESVERIFY");
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);

        MethodInfo startRun = typeof(NGame).GetMethod("StartRun", PrivateInstance)
            ?? throw new InvalidOperationException("NGame.StartRun was unavailable.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException("NGame.StartRun did not return a Task.");
        await startRunTask;
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            roomType,
            MapPointType.Unassigned,
            ModelDb.Encounter<TEncounter>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            description);
        return CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
    }

    private static async Task CleanupRunAsync(string description)
    {
        if (RunManager.Instance.DebugOnlyGetState() == null)
        {
            return;
        }

        RunManager.Instance.CleanUp(graceful: true);
        await WaitUntil(
            static () => RunManager.Instance.DebugOnlyGetState() == null,
            description);
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
