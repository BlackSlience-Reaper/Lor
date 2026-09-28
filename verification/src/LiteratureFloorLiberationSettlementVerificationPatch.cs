using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.events.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class LiteratureFloorLiberationSettlementVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-literature-floor-settlement";
    private const string LogPrefix =
        "[LibraryOfRuina.LiteratureFloorSettlement.Verify] ";
    private const string EventId =
        "LITERATURE_FLOOR_LIBERATION_SETTLEMENT_EVENT";

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
            VerifyResourcesAndLocalization();
            await VerifyRuntimeContract();
            Log.Info(
                LogPrefix + "LITERATURE_FLOOR_SETTLEMENT_POLICY_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(
                LogPrefix
                + "LITERATURE_FLOOR_SETTLEMENT_POLICY_FAILED: "
                + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyResourcesAndLocalization()
    {
        Require(
            ResourceLoader.Exists(
                "res://images/events/literature_floor_liberation_settlement_event.png"),
            "Literature settlement background image was not imported.");
        Require(
            ResourceLoader.Exists(
                "res://scenes/events/background_scenes/literature_floor_liberation_settlement_event.tscn"),
            "Literature settlement background scene was not imported.");

        string[] requiredKeys =
        [
            EventId + ".title",
            EventId + ".epithet",
            EventId + ".loss",
            EventId + ".pages.INITIAL.description",
            EventId + ".pages.INITIAL.options.TIER_2.title",
            EventId + ".pages.INITIAL.options.TIER_2.description",
            EventId + ".pages.INITIAL.options.TIER_3.title",
            EventId + ".pages.INITIAL.options.TIER_4.title",
            EventId + ".pages.INITIAL.options.TIER_5.title",
            EventId + ".pages.DONE.description",
            EventId + ".talk.firstVisitEver.0-0.char",
            EventId + ".talk.firstVisitEver.0-1.char",
            EventId + ".talk.firstVisitEver.0-2.ancient",
            EventId + ".talk.ANY.0-0r.ancient"
        ];
        foreach (string key in requiredKeys)
        {
            Require(
                LocString.Exists("ancients", key),
                "Missing Literature settlement localization key: " + key);
        }
    }

    private static async Task VerifyRuntimeContract()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");

        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LITERATUREFLOORSETTLEMENTVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);

        var encounter = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "3",
            ["KilledBossCount"] = "2",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False",
            ["SettlementTriggered"] = "False",
            ["EndedByLethalDamage"] = "False"
        });

        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress,
            "Literature floor settlement combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var activeEncounter = combatState.Encounter
            as LiteratureFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "LiteratureFloorLiberationEncounter was not active.");
        Creature player = combatState.PlayerCreatures.Single();

        Require(
            activeEncounter.ShouldPreventPlayerDeath(player),
            "Two defeated bosses did not unlock settlement protection.");

        await activeEncounter.OnPreventingDeath(player);
        Require(
            player.IsAlive
            && activeEncounter.PhaseComplete
            && activeEncounter.SettlementTriggered
            && activeEncounter.EndedByLethalDamage
            && LiteratureFloorLiberationSettlementStore.PendingSettlement,
            "Lethal settlement did not preserve the player and queue the event: "
            + $"alive={player.IsAlive}, "
            + $"hp={player.CurrentHp}, "
            + $"phaseComplete={activeEncounter.PhaseComplete}, "
            + $"settlementTriggered={activeEncounter.SettlementTriggered}, "
            + $"endedByLethal={activeEncounter.EndedByLethalDamage}, "
            + "pending="
            + LiteratureFloorLiberationSettlementStore.PendingSettlement);

        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress,
            "Literature floor settlement combat victory");
        await RunManager.Instance.ProceedFromTerminalRewardsScreen();
        await WaitUntil(
            static () => RunManager.Instance.DebugOnlyGetState()?.CurrentRoom
                is EventRoom
                {
                    CanonicalEvent:
                        LiteratureFloorLiberationSettlementEvent
                },
            "Literature floor settlement event room");

        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom
            is not EventRoom eventRoom)
        {
            throw new InvalidOperationException(
                "Literature settlement event room was not active.");
        }

        var localEvent = eventRoom.LocalMutableEvent
            as LiteratureFloorLiberationSettlementEvent
            ?? throw new InvalidOperationException(
                "Literature settlement local event was not active.");
        Require(
            localEvent.DynamicVars["Kills"].IntValue == 2,
            "Settlement event did not preserve the defeated phase count.");
        Require(
            localEvent.CurrentOptions.Count == 4
            && !localEvent.CurrentOptions[0].IsLocked
            && localEvent.CurrentOptions.Skip(1).All(static option =>
                option.IsLocked),
            "Settlement reward tiers were not locked for two defeated phases.");
        Require(
            localEvent.DialogueSet.FirstVisitEverDialogue?.Lines.Count == 3
            && localEvent.DialogueSet.AgnosticDialogues.Single().Lines.Count
            == 1,
            "Settlement dialogue line contract was incomplete.");
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
