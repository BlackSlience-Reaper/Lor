using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.relics.QueenOfHatred;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class QueenOfHatredPageMultiplayerVerificationPatch
{
    private const string VerifyArg = "lor-verify-queen-hatred-multiplayer";
    private const string LogPrefix =
        "[LibraryOfRuina.QueenOfHatredPageMultiplayer.Verify] ";
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

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
            Log.Info(LogPrefix + "QUEEN_OF_HATRED_MULTIPLAYER_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "QUEEN_OF_HATRED_MULTIPLAYER_FAILED: " + ex);
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
            Player owner = players[0];
            Creature enemy = combatState.Enemies.First(static creature => creature.IsAlive);
            QueenOfHatredPageRelic relic =
                (QueenOfHatredPageRelic)ModelDb.Relic<QueenOfHatredPageRelic>().ToMutable();
            owner.AddRelicInternal(relic, silent: true);
            SetMode(relic, QueenOfHatredPageMode.Hatred);
            await relic.BeforeCombatStart();

            Require(
                relic.HatredTriggersRemainingThisTurn
                    == QueenOfHatredPageRelic.HatredTriggersPerTurn,
                "Hatred counter did not initialize to two.");

            for (int previewIndex = 0; previewIndex < 3; previewIndex++)
            {
                decimal previewResult = ModifyHpLost(
                    owner.RunState,
                    combatState,
                    owner.Creature,
                    6m,
                    enemy,
                    preview: true,
                    out _);

                Require(previewResult == 7m,
                    "Damage preview did not include the one HP Hatred bonus.");
                Require(
                    relic.HatredTriggersRemainingThisTurn
                        == QueenOfHatredPageRelic.HatredTriggersPerTurn,
                    "Damage preview consumed Hatred trigger; remaining="
                    + relic.HatredTriggersRemainingThisTurn
                    + ".");
                Require(NextTurnStrengthAmount(owner) == 0m,
                    "Damage preview applied Next-Turn Strength.");
            }

            await ResolveActualHpLoss(owner, combatState, enemy, relic);
            Require(relic.HatredTriggersRemainingThisTurn == 1,
                "First actual HP loss did not consume exactly one Hatred trigger.");
            Require(
                NextTurnStrengthAmount(owner)
                    == QueenOfHatredPageRelic.HatredNextTurnStrengthGain,
                "First actual HP loss did not grant exactly two Next-Turn Strength.");

            await ResolveActualHpLoss(owner, combatState, enemy, relic);
            Require(relic.HatredTriggersRemainingThisTurn == 0,
                "Second actual HP loss did not consume the final Hatred trigger.");
            Require(
                NextTurnStrengthAmount(owner)
                    == QueenOfHatredPageRelic.HatredNextTurnStrengthGain * 2m,
                "Second actual HP loss did not stack Next-Turn Strength to four.");

            decimal exhaustedResult = ModifyHpLost(
                owner.RunState,
                combatState,
                owner.Creature,
                6m,
                enemy,
                preview: true,
                out IEnumerable<AbstractModel> exhaustedModifiers);
            Require(exhaustedResult == 6m,
                "Exhausted Hatred counter still modified HP loss.");
            Require(!exhaustedModifiers.Contains(relic),
                "Exhausted Hatred relic was still reported as an HP-loss modifier.");
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Queen of Hatred multiplayer verifier cleanup");
        }
    }

    private static async Task ResolveActualHpLoss(
        Player owner,
        CombatState combatState,
        Creature enemy,
        QueenOfHatredPageRelic relic)
    {
        decimal actualResult = ModifyHpLost(
            owner.RunState,
            combatState,
            owner.Creature,
            6m,
            enemy,
            preview: false,
            out IEnumerable<AbstractModel> actualModifiers);
        Require(actualResult == 7m,
            "Actual HP loss did not include the one HP Hatred bonus.");
        Require(actualModifiers.Contains(relic),
            "Queen of Hatred Page was absent from actual modifier callbacks.");

        await Hook.AfterModifyingHpLostAfterOsty(
            owner.RunState,
            combatState,
            actualModifiers);
    }

    private static decimal ModifyHpLost(
        IRunState runState,
        CombatState combatState,
        Creature target,
        decimal amount,
        Creature dealer,
        bool preview,
        out IEnumerable<AbstractModel> modifiers)
    {
#if STS2_BETA
        return Hook.ModifyHpLost(
            runState,
            combatState,
            target,
            amount,
            ValueProp.Unpowered,
            dealer,
            cardSource: null,
            preview ? HpLossHookPhase.All : HpLossHookPhase.AfterOsty,
            out modifiers);
#else
        return Hook.ModifyHpLostAfterOsty(
            runState,
            combatState,
            target,
            amount,
            ValueProp.Unpowered,
            dealer,
            cardSource: null,
            out modifiers);
#endif
    }

    private static decimal NextTurnStrengthAmount(Player owner) =>
        owner.Creature.GetPowerInstances<LibraryOfRuinaNextTurnStrength>()
            .Sum(static power => power.Amount);

    private static void SetMode(
        QueenOfHatredPageRelic relic,
        QueenOfHatredPageMode mode)
    {
        PropertyInfo modeProperty = typeof(QueenOfHatredPageRelic).GetProperty(
                nameof(QueenOfHatredPageRelic.Mode),
                BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException(
                "QueenOfHatredPageRelic.Mode was unavailable.");
        modeProperty.SetValue(relic, mode);
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
            "QUEENOFHATREDMULTIPLAYERVERIFY");
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);

        MethodInfo startRun = typeof(NGame).GetMethod("StartRun", PrivateInstance)
            ?? throw new InvalidOperationException(
                "NGame.StartRun was unavailable for fake multiplayer.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException("NGame.StartRun did not return a Task.");
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
            "Queen of Hatred fake multiplayer combat start");
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
