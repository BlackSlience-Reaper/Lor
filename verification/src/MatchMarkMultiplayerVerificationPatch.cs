using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.powers;
using LibraryOfRuina.relics.HistoryFloorLiberation;
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

namespace LibraryOfRuinaVerification;

internal static class MatchMarkMultiplayerVerificationPatch
{
    private const string VerifyArg = "lor-verify-match-mark-multiplayer";
    private const string LogPrefix = "[LibraryOfRuina.MatchMarkMultiplayer.Verify] ";
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
            Log.Info(LogPrefix + "MATCH_MARK_MULTIPLAYER_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "MATCH_MARK_MULTIPLAYER_FAILED: " + ex);
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
            MatchMarkRelic relic = (MatchMarkRelic)ModelDb.Relic<MatchMarkRelic>().ToMutable();
            owner.AddRelicInternal(relic, silent: true);
            SetMode(relic, MatchMarkMode.Footsteps);
            await relic.BeforeCombatStart();

            decimal previewResult = ModifyHpLost(
                owner.RunState,
                combatState,
                owner.Creature,
                6m,
                enemy,
                preview: true,
                out _);

            Require(previewResult == 0m, "Damage preview was not prevented.");
            Require(!relic.FootstepsSpent,
                "Damage preview committed FootstepsSpent.");
            Require(relic.PendingDamage == 0,
                "Damage preview committed PendingDamage=" + relic.PendingDamage + ".");
            Require(!relic.ResolveAtNextPlayerTurnEnd,
                "Damage preview committed ResolveAtNextPlayerTurnEnd.");
            Require(!owner.Creature.GetPowerInstances<PreservedDamagePower>().Any(),
                "Damage preview applied Preserved Damage.");
            Require(combatState.Enemies.All(static target =>
                    !target.GetPowerInstances<LibraryBurnPower>().Any()),
                "Damage preview applied Burn.");

            decimal actualResult = ModifyHpLost(
                owner.RunState,
                combatState,
                owner.Creature,
                6m,
                enemy,
                preview: false,
                out IEnumerable<AbstractModel> actualModifiers);
            Require(actualResult == 0m, "Actual HP loss was not prevented.");
            Require(actualModifiers.Contains(relic),
                "Match Mark was absent from the actual modifier callback list.");

            await Hook.AfterModifyingHpLostAfterOsty(
                owner.RunState,
                combatState,
                actualModifiers);

            Require(relic.FootstepsSpent,
                "Actual HP loss did not commit FootstepsSpent.");
            Require(relic.PendingDamage == 6,
                "Actual HP loss committed PendingDamage=" + relic.PendingDamage + ".");
            Require(relic.ResolveAtNextPlayerTurnEnd,
                "Actual HP loss did not schedule turn-end resolution.");
            Require(owner.Creature.GetPowerInstances<PreservedDamagePower>()
                    .SingleOrDefault()?.Amount == 6m,
                "Actual HP loss did not apply 6 Preserved Damage.");
            Require(combatState.Enemies
                    .Where(static target => target.IsAlive)
                    .All(static target => target.GetPowerInstances<LibraryBurnPower>()
                        .Sum(static power => power.Amount) == MatchMarkRelic.FootstepsBurnStacks),
                "Actual HP loss did not apply the expected Burn to every living enemy.");
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Match Mark multiplayer verifier cleanup");
        }
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

    private static void SetMode(MatchMarkRelic relic, MatchMarkMode mode)
    {
        PropertyInfo modeProperty = typeof(MatchMarkRelic).GetProperty(
                nameof(MatchMarkRelic.Mode),
                BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("MatchMarkRelic.Mode was unavailable.");
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
            "MATCHMARKMULTIPLAYERVERIFY");
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
            "Match Mark fake multiplayer combat start");
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
