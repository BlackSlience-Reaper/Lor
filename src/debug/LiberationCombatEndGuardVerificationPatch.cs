using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
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
internal static class LiberationCombatEndGuardVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-liberation-combat-end-guards";
    private const string LogPrefix =
        "[LibraryOfRuina.LiberationCombatEndGuard.Verify] ";
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
            _ = TaskHelper.RunSafely(RunAsync());
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
            var failures = new List<string>();
            await VerifyHistoryFloor(failures);
            await VerifyHistoryFloorFinal(failures);
            await VerifyTechnologyFloor(failures);
            await VerifyTechnologyFloorFinal(failures);
            await VerifyArtFloor(failures);
            await VerifyArtFloorFinal(failures);
            Require(
                failures.Count == 0,
                "Pre-AfterDeath primary-boss lethal state was not held for: "
                + string.Join(", ", failures));

            Log.Info(LogPrefix + "LIBERATION_COMBAT_END_GUARDS_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix
                + "LIBERATION_COMBAT_END_GUARDS_FAIL\n"
                + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task VerifyHistoryFloor(List<string> failures)
    {
        var encounter = (HistoryFloorLiberationEncounter)ModelDb
            .Encounter<HistoryFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(CreatePhaseState(currentPhase: 2));
        (CombatState combatState, ILiberationPrimaryPhaseBoss boss) =
            await StartFight(
                encounter,
                "LIBERATIONENDGUARD_HISTORY",
                expectedPhase: 2);

        bool held = VerifyPendingDeathWindow(
            "history",
            combatState,
            boss,
            () => encounter.ShouldKeepCombatOpen(combatState),
            failures);
        if (held)
        {
            await encounter.OnPhaseBossDeath(
                boss,
                wasRemovalPrevented: false,
                deathAnimLength: 0f);
            Require(encounter.CurrentPhase == 3
                    && encounter.KilledBossCount == 2
                    && encounter.ShouldKeepCombatOpen(combatState),
                "History phase death did not enter a protected transition.");
            Require(!await CombatManager.Instance.CheckWinCondition()
                    && CombatManager.Instance.IsInProgress,
                "History transition incorrectly ended combat.");
        }
        await CleanupRunAndWait("history guard verifier cleanup");
    }

    private static async Task VerifyHistoryFloorFinal(
        List<string> failures)
    {
        var encounter = (HistoryFloorLiberationEncounter)ModelDb
            .Encounter<HistoryFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(CreatePhaseState(currentPhase: 5));
        (CombatState combatState, ILiberationPrimaryPhaseBoss boss) =
            await StartFight(
                encounter,
                "LIBERATIONENDGUARD_HISTORY_FINAL",
                expectedPhase: 5);

        bool held = VerifyPendingDeathWindow(
            "history-final",
            combatState,
            boss,
            () => encounter.ShouldKeepCombatOpen(combatState),
            failures);
        if (held)
        {
            await encounter.OnPhaseBossDeath(
                boss,
                wasRemovalPrevented: false,
                deathAnimLength: 0f);
            Require(encounter.SettlementTriggered
                    && encounter.KilledBossCount == 5
                    && !encounter.ShouldKeepCombatOpen(combatState),
                "History final death did not release combat for settlement.");
        }
        await CleanupRunAndWait("history final guard verifier cleanup");
    }

    private static async Task VerifyTechnologyFloor(List<string> failures)
    {
        var encounter = (TechnologyFloorLiberationEncounter)ModelDb
            .Encounter<TechnologyFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(CreatePhaseState(currentPhase: 1));
        (CombatState combatState, ILiberationPrimaryPhaseBoss boss) =
            await StartFight(
                encounter,
                "LIBERATIONENDGUARD_TECHNOLOGY",
                expectedPhase: 1);

        bool held = VerifyPendingDeathWindow(
            "technology",
            combatState,
            boss,
            () => encounter.ShouldKeepCombatOpen(combatState),
            failures);
        if (held)
        {
            await encounter.OnPhaseBossDeath(
                boss,
                wasRemovalPrevented: false,
                deathAnimLength: 0f);
            Require(encounter.CurrentPhase == 2
                    && encounter.KilledBossCount == 1
                    && encounter.ShouldKeepCombatOpen(combatState),
                "Technology phase death did not enter a protected transition.");
            Require(!await CombatManager.Instance.CheckWinCondition()
                    && CombatManager.Instance.IsInProgress,
                "Technology transition incorrectly ended combat.");
        }
        await CleanupRunAndWait("technology guard verifier cleanup");
    }

    private static async Task VerifyTechnologyFloorFinal(
        List<string> failures)
    {
        var encounter = (TechnologyFloorLiberationEncounter)ModelDb
            .Encounter<TechnologyFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(CreatePhaseState(currentPhase: 5));
        (CombatState combatState, ILiberationPrimaryPhaseBoss boss) =
            await StartFight(
                encounter,
                "LIBERATIONENDGUARD_TECHNOLOGY_FINAL",
                expectedPhase: 5);

        bool held = VerifyPendingDeathWindow(
            "technology-final",
            combatState,
            boss,
            () => encounter.ShouldKeepCombatOpen(combatState),
            failures);
        if (held)
        {
            await encounter.OnPhaseBossDeath(
                boss,
                wasRemovalPrevented: false,
                deathAnimLength: 0f);
            Require(encounter.SettlementTriggered
                    && encounter.KilledBossCount == 5
                    && !encounter.ShouldKeepCombatOpen(combatState),
                "Technology final death did not release combat for settlement.");
        }
        await CleanupRunAndWait("technology final guard verifier cleanup");
    }

    private static async Task VerifyArtFloor(List<string> failures)
    {
        var encounter = (ArtFloorLiberationEncounter)ModelDb
            .Encounter<ArtFloorLiberationEncounter>()
            .ToMutable();
        Dictionary<string, string> state = CreatePhaseState(
            currentPhase: 2);
        state["EndedByPlaceholder"] = "False";
        encounter.LoadCustomState(state);
        (CombatState combatState, ILiberationPrimaryPhaseBoss boss) =
            await StartFight(
                encounter,
                "LIBERATIONENDGUARD_ART",
                expectedPhase: 2);

        bool held = VerifyPendingDeathWindow(
            "art",
            combatState,
            boss,
            () => encounter.ShouldKeepCombatOpen(combatState),
            failures);
        if (held)
        {
            await encounter.OnPhaseBossDeath(
                boss,
                wasRemovalPrevented: false,
                deathAnimLength: 0f);
            Require(encounter.CurrentPhase == 3
                    && encounter.KilledBossCount == 2
                    && encounter.ShouldKeepCombatOpen(combatState),
                "Art phase death did not enter a protected transition.");
            Require(!await CombatManager.Instance.CheckWinCondition()
                    && CombatManager.Instance.IsInProgress,
                "Art transition incorrectly ended combat.");
        }
        await CleanupRunAndWait("art guard verifier cleanup");
    }

    private static async Task VerifyArtFloorFinal(List<string> failures)
    {
        var encounter = (ArtFloorLiberationEncounter)ModelDb
            .Encounter<ArtFloorLiberationEncounter>()
            .ToMutable();
        Dictionary<string, string> state = CreatePhaseState(
            currentPhase: 6);
        state["EndedByPlaceholder"] = "False";
        encounter.LoadCustomState(state);
        (CombatState combatState, ILiberationPrimaryPhaseBoss boss) =
            await StartFight(
                encounter,
                "LIBERATIONENDGUARD_ART_FINAL",
                expectedPhase: 6);

        bool held = VerifyPendingDeathWindow(
            "art-final",
            combatState,
            boss,
            () => encounter.ShouldKeepCombatOpen(combatState),
            failures);
        if (held)
        {
            await encounter.OnPhaseBossDeath(
                boss,
                wasRemovalPrevented: false,
                deathAnimLength: 0f);
            Require(encounter.SettlementTriggered
                    && encounter.KilledBossCount == 6
                    && !encounter.ShouldKeepCombatOpen(combatState),
                "Art final death did not release combat for settlement.");
        }
        await CleanupRunAndWait("art final guard verifier cleanup");
    }

    private static bool VerifyPendingDeathWindow(
        string floor,
        CombatState combatState,
        ILiberationPrimaryPhaseBoss boss,
        Func<bool> shouldKeepCombatOpen,
        ICollection<string> failures)
    {
        Require(!shouldKeepCombatOpen(),
            floor + " live phase boss incorrectly held combat open.");

        boss.Creature.SetCurrentHpInternal(0m);
        Require(boss.Creature.IsDead,
            floor + " verifier did not reach zero HP.");
        Require(combatState.Enemies.Contains(boss.Creature),
            floor + " zero-HP phase boss left the enemy roster too early.");

        bool held = shouldKeepCombatOpen();
        if (!held)
        {
            failures.Add(floor);
        }

        return held;
    }

    private static async Task<(
        CombatState CombatState,
        ILiberationPrimaryPhaseBoss Boss)> StartFight(
        EncounterModel encounter,
        string seed,
        int expectedPhase)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            seed + " combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        ILiberationPrimaryPhaseBoss boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ILiberationPrimaryPhaseBoss>()
            .SingleOrDefault(candidate =>
                candidate.LiberationPhase == expectedPhase)
            ?? throw new InvalidOperationException(
                "Could not find phase " + expectedPhase + " primary boss.");
        return (combatState, boss);
    }

    private static Dictionary<string, string> CreatePhaseState(
        int currentPhase) => new()
    {
        ["CurrentPhase"] = currentPhase.ToString(),
        ["KilledBossCount"] = Math.Max(0, currentPhase - 1).ToString(),
        ["TransitionPending"] = "False",
        ["SettlementTriggered"] = "False",
        ["EndedByLethalDamage"] = "False"
    };

    private static async Task CleanupRunAndWait(string description)
    {
        CleanupRun();
        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() == null
                && RunManager.Instance.DebugOnlyGetState() == null,
            description);
    }

    private static void CleanupRun()
    {
        if (RunManager.Instance.DebugOnlyGetState() != null)
        {
            RunManager.Instance.CleanUp(graceful: true);
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

            SceneTree tree = NGame.Instance?.GetTree()
                ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        throw new TimeoutException(
            "Timed out waiting for " + description + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
