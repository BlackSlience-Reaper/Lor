using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards;
using LibraryOfRuina.content.abnormalities.CosmicFragment;
using LibraryOfRuina.content.abnormalities.SpiderBud;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class ArtFloorLiberationVerificationPatch
{
    private const string VerifyArg = "lor-verify-art-floor";
    private const string VerifyPhaseSixArg = "lor-verify-art-floor-phase6";
    private const string VerifyPerformerStunArg =
        "lor-verify-art-floor-performer-stun";
    private const string VerifyPleasureCleanupArg = "lor-verify-pleasure-cleanup";
    private const string VerifySolemnEgoDeathCleanupArg = "lor-verify-solemn-ego-death";
    private const string VerifyNostalgicScentCounterArg = "lor-verify-nostalgic-scent-counter";
    private const string VerifyLittleGalaxySequenceArg = "lor-verify-art-floor-little-galaxy";
    private const string LogPrefix = "[LibraryOfRuina.ArtFloor.Verify] ";

    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasAnyVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasAnyVerifyArg()
    {
        return HasArg(VerifyArg)
               || HasArg(VerifyPhaseSixArg)
               || HasArg(VerifyPerformerStunArg)
               || HasArg(VerifyPleasureCleanupArg)
               || HasArg(VerifySolemnEgoDeathCleanupArg)
               || HasArg(VerifyNostalgicScentCounterArg)
               || HasArg(VerifyLittleGalaxySequenceArg);
    }

    private static bool HasArg(string arg)
    {
        return CommandLineHelper.HasArg(arg)
               || Environment.GetCommandLineArgs()
                   .Any(value => string.Equals(value.TrimStart('-'), arg, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunAsync()
    {
        try
        {
            if (HasArg(VerifyPerformerStunArg))
            {
                await VerifyFinalPerformerStunOnlyAsync();
                Log.Info(LogPrefix + "ART_FLOOR_PERFORMER_STUN_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasArg(VerifyPleasureCleanupArg))
            {
                await VerifyDaCapoDeathTransitionAsync(stopAfterPleasureCleanup: true);
                Log.Info(LogPrefix + "PLEASURE_CLEANUP_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasArg(VerifyPhaseSixArg))
            {
                await VerifyFinalDaCapoPhaseOnlyAsync();
                Log.Info(LogPrefix + "ART_FLOOR_PHASE6_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasArg(VerifySolemnEgoDeathCleanupArg))
            {
                await VerifySolemnMourningEgoDeathCleanupAsync();
                Log.Info(LogPrefix + "SOLEMN_EGO_DEATH_CLEANUP_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasArg(VerifyNostalgicScentCounterArg))
            {
                await VerifyNostalgicScentCounterAsync();
                Log.Info(LogPrefix + "NOSTALGIC_SCENT_COUNTER_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasArg(VerifyLittleGalaxySequenceArg))
            {
                await VerifyLittleGalaxySequenceOnlyAsync();
                Log.Info(LogPrefix + "LITTLE_GALAXY_SEQUENCE_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            await VerifyOpeningAndTurnLimitAsync();
            await VerifyDaCapoDeathTransitionAsync();
            await VerifyFanaticWorshipAsync();
            Log.Info(LogPrefix + "ART_FLOOR_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "ART_FLOOR_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task VerifyOpeningAndTurnLimitAsync()
    {
        ArtFloorCombatContext fight = await StartFight("ARTFLOORVERIFY_OPENING");

        VerifyRoster(fight);
        VerifyDaCapoStats(fight.DaCapoCreature);
        VerifyFirstPerformerStats(fight.FirstPerformerCreature);
        VerifyOpeningPowers(fight);
        await VerifyOpeningVisualsAndIntents(fight);

        fight.CombatState.RoundNumber = ArtFloorDaCapoBoss.StageTurnCount + 1;
        await fight.Encounter.TriggerPlaceholderPhaseEnd(fight.CombatState, fromTurnLimit: true);
        await WaitFrames(10);

        Require(!fight.Encounter.SettlementTriggered, "Turn-limit transition incorrectly triggered final settlement.");
        Require(fight.Encounter.EndedByPlaceholder, "Turn-limit transition did not preserve placeholder flag.");
        Require(fight.Encounter.CurrentPhase == 2, "Turn-limit transition did not advance to phase 2.");
        Require(fight.Encounter.KilledBossCount == 1, "Turn-limit transition did not record one defeated boss.");
        Require(fight.CombatState.Enemies.Any(static enemy => enemy.Monster is ArtFloorDaCapoBoss),
            "Turn-limit transition did not keep Da Capo as transition boss.");
        Require(fight.CombatState.Enemies.All(static enemy => enemy.Monster is ArtFloorDaCapoBoss),
            "Turn-limit transition did not remove first-phase support enemies.");
        Log.Info(LogPrefix + "TurnLimitTransition phase=2 pending=true");

        CleanupRun();
        await WaitForRunCleanup("opening verifier cleanup");
    }

    private static async Task VerifyDaCapoDeathTransitionAsync(bool stopAfterPleasureCleanup = false)
    {
        ArtFloorCombatContext fight = await StartFight("ARTFLOORVERIFY_DEATH");
        Creature player = fight.CombatState.PlayerCreatures.Single();
        await PowerCmdCompat.Apply<FanaticWorshipPower>(
            new ThrowingPlayerChoiceContext(),
            player,
            1m,
            fight.DaCapoCreature,
            null);
        Require(player.HasPower<FanaticWorshipPower>(), "Fanatic Worship was not applied before Da Capo death.");

        await CreatureCmd.Kill(fight.DaCapoCreature, force: true);
        await WaitFrames(10);

        Require(!fight.Encounter.SettlementTriggered, "Da Capo death incorrectly triggered final settlement.");
        Require(!fight.Encounter.EndedByPlaceholder, "Da Capo death incorrectly marked placeholder flag.");
        Require(fight.Encounter.CurrentPhase == 2, "Da Capo death did not advance to phase 2.");
        Require(fight.Encounter.KilledBossCount == 1, "Da Capo death did not record one defeated boss.");
        Require(fight.CombatState.Enemies.Count == 1, "Da Capo death transition did not leave exactly one transition enemy.");
        Require(fight.CombatState.Enemies.Single().Monster is ArtFloorDaCapoBoss,
            "Da Capo death transition did not keep Da Capo as transition boss.");
        Require(!fight.CombatState.PlayerCreatures.Any(static creature => creature.HasPower<FanaticWorshipPower>()),
            "Da Capo death did not clear Fanatic Worship.");

        await fight.DaCapo.PerformMove();
        await WaitFrames(10);

        ArtFloorBeyondFragmentBoss beyondFragment = fight.CombatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorBeyondFragmentBoss>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("Phase 2 Beyond Fragment was not spawned.");
        VerifyBeyondFragmentStats(beyondFragment.Creature);
        Require(!fight.CombatState.PlayerCreatures.Any(static creature => creature.HasPower<BoundaryThornPower>()),
            "Boundary Thorn was not cleared before phase 2.");
        Require(!fight.CombatState.PlayerCreatures.Any(static creature =>
                creature.Player?.PlayerCombatState?.AllCards.Any(static card => card is CosmicFragmentEpiphanyCard) == true),
            "Epiphany cards were not cleared before phase 2.");
        Log.Info(LogPrefix + "DaCapoDeathTransition phase=2 beyondFragment=true");

        await CreatureCmd.Kill(beyondFragment.Creature, force: true);
        await WaitFrames(10);

        Require(!fight.Encounter.SettlementTriggered, "Beyond Fragment death incorrectly triggered final settlement.");
        Require(fight.Encounter.CurrentPhase == 3, "Beyond Fragment death did not advance to phase 3.");
        Require(fight.Encounter.KilledBossCount == 2, "Beyond Fragment death did not record two defeated bosses.");
        Require(fight.CombatState.Enemies.Count == 1, "Beyond Fragment death transition did not leave exactly one transition enemy.");
        Require(fight.CombatState.Enemies.Single().Monster is ArtFloorBeyondFragmentBoss,
            "Beyond Fragment death transition did not keep Beyond Fragment as transition boss.");

        await fight.Encounter.CompletePhaseTransition(beyondFragment);
        await WaitFrames(10);

        VerifyLittleGalaxyPhase(fight);
        await VerifyLittleGalaxyFakeDeathAndEgoAsync(fight);
        Log.Info(LogPrefix + "BeyondFragmentDeathTransition phase=3 littleGalaxy=true");

        ArtFloorLittleGalaxyBoss littleGalaxy = fight.CombatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorLittleGalaxyBoss>()
            .Single();
        await CreatureCmd.Kill(littleGalaxy.Creature, force: true);
        await WaitFrames(10);

        Require(!fight.Encounter.SettlementTriggered, "Little Galaxy death incorrectly triggered final settlement.");
        Require(fight.Encounter.CurrentPhase == 4, "Little Galaxy death did not advance to phase 4.");
        Require(fight.Encounter.KilledBossCount == 3, "Little Galaxy death did not record three defeated bosses.");
        Require(fight.CombatState.Enemies.Any(static enemy => enemy.Monster is ArtFloorLittleGalaxyBoss),
            "Little Galaxy death transition did not keep Little Galaxy as transition boss.");

        await fight.Encounter.CompletePhaseTransition(littleGalaxy);
        await WaitFrames(10);

        ArtFloorPleasureBoss pleasure = VerifyPleasurePhase(fight);
        await CreatureCmd.Kill(pleasure.Creature, force: true);
        await WaitFrames(10);

        Require(!fight.Encounter.SettlementTriggered, "Pleasure death incorrectly triggered final settlement.");
        Require(fight.Encounter.CurrentPhase == 5, "Pleasure death did not advance to phase 5.");
        Require(fight.Encounter.KilledBossCount == 4, "Pleasure death did not record four defeated bosses.");
        Require(fight.CombatState.Enemies.Any(static enemy => enemy.Monster is ArtFloorPleasureBoss),
            "Pleasure death transition did not keep Pleasure as transition boss.");

        await fight.Encounter.CompletePhaseTransition(pleasure);
        await WaitFrames(10);

        VerifyNoPleasureCardsRemain(fight.CombatState);
        if (stopAfterPleasureCleanup)
        {
            CleanupRun();
            await WaitForRunCleanup("pleasure cleanup verifier cleanup");
            return;
        }

        ArtFloorNostalgicScentBoss nostalgicScent = VerifyNostalgicScentPhase(fight);
        await VerifyNostalgicScentMechanicsAsync(fight, nostalgicScent);
        await CreatureCmd.Kill(nostalgicScent.Creature, force: true);
        await WaitFrames(10);

        Require(!fight.Encounter.SettlementTriggered, "Nostalgic Scent death should transition into phase 6 first.");
        Require(fight.Encounter.CurrentPhase == 6, "Nostalgic Scent death did not advance to phase 6.");
        Require(fight.Encounter.KilledBossCount == 5, "Nostalgic Scent death did not record five defeated bosses.");
        Require(fight.CombatState.Enemies.Any(static enemy => enemy.Monster is ArtFloorNostalgicScentBoss),
            "Nostalgic Scent death transition did not keep Nostalgic Scent as transition boss.");

        await fight.Encounter.CompletePhaseTransition(nostalgicScent);
        await WaitFrames(10);

        VerifyFinalDaCapoPhase(fight.CombatState);
        await VerifyFinalDaCapoPresentationAsync(fight.CombatState);
        await VerifyFinalDaCapoMechanicsAsync(fight.CombatState);

        Creature finalDaCapoCreature = fight.CombatState.Enemies.Single(static enemy =>
            enemy.SlotName == ArtFloorLiberationEncounter.FinalDaCapoSlot);
        await CreatureCmd.Kill(finalDaCapoCreature, force: true);
        await WaitFrames(10);

        Require(fight.Encounter.SettlementTriggered, "Final Da Capo death did not trigger final settlement.");
        Require(fight.Encounter.CurrentPhase == 6, "Final Da Capo death changed phase unexpectedly.");
        Require(fight.Encounter.KilledBossCount == 6, "Final Da Capo death did not record six defeated bosses.");
        Log.Info(LogPrefix + "PleasureDeathTransition phase=6 finalDaCapo=true finalSettlement=true");

        CleanupRun();
        await WaitForRunCleanup("death verifier cleanup");
    }

    private static async Task VerifyLittleGalaxySequenceOnlyAsync()
    {
        ArtFloorCombatContext fight = await StartFight("ARTFLOORVERIFY_LITTLE_GALAXY");

        await CreatureCmd.Kill(fight.DaCapoCreature, force: true);
        await WaitFrames(10);
        await fight.DaCapo.PerformMove();
        await WaitFrames(10);

        ArtFloorBeyondFragmentBoss beyondFragment = fight.CombatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorBeyondFragmentBoss>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("Little Galaxy verifier could not reach Beyond Fragment.");

        await CreatureCmd.Kill(beyondFragment.Creature, force: true);
        await WaitFrames(10);
        await fight.Encounter.CompletePhaseTransition(beyondFragment);
        await WaitFrames(10);

        Require(fight.CombatState.Enemies.Count(static enemy => enemy.Monster is ArtFloorGalaxyFriend) == 2,
            "Little Galaxy verifier did not spawn both Galaxy Friends.");
        Require(fight.CombatState.Enemies.Count(static enemy => enemy.Monster is ArtFloorLittleGalaxyBoss) == 1,
            "Little Galaxy verifier did not spawn the phase boss.");

        await VerifyLittleGalaxyFakeDeathAndEgoAsync(fight);

        CleanupRun();
        await WaitForRunCleanup("Little Galaxy verifier cleanup");
    }

    private static async Task VerifyFanaticWorshipAsync()
    {
        ArtFloorCombatContext fight = await StartFight("ARTFLOORVERIFY_WORSHIP");
        Creature player = fight.CombatState.PlayerCreatures.Single();
        var context = new ThrowingPlayerChoiceContext();

        await PowerCmdCompat.Apply<FanaticWorshipPower>(context, player, 1m, fight.DaCapoCreature, null);
        Require(player.HasPower<FanaticWorshipPower>(), "Fanatic Worship was not applied to the player.");

        int maxHpBeforeFanaticHit = player.MaxHp;
        int hpBeforeFanaticHit = player.CurrentHp;
        await CreatureCmdCompat.Damage(
            context,
            player,
            3m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            dealer: player,
            cardSource: null);
        Require(player.MaxHp == maxHpBeforeFanaticHit, "Fanatic self-hit incorrectly changed max HP.");
        Require(player.CurrentHp == hpBeforeFanaticHit - 3, "Fanatic self-hit did not deal physical HP damage.");

        FanaticWorshipPower? worship = player.GetPower<FanaticWorshipPower>();
        Require(worship != null, "Fanatic Worship disappeared before Finale cleanup.");
        await PowerCmd.Remove(worship!);
        Require(!player.HasPower<FanaticWorshipPower>(), "Fanatic Worship did not remove cleanly.");
        Log.Info(LogPrefix + "FanaticWorship singlePlayerDamagePath=ok");

        CleanupRun();
        await WaitForRunCleanup("worship verifier cleanup");
    }

    private static async Task VerifyNostalgicScentCounterAsync()
    {
        ArtFloorCombatContext fight = await StartFight("ARTFLOORVERIFY_SCENT_COUNTER");
        for (int phase = 1; phase < 5; phase++)
        {
            ILiberationPrimaryPhaseBoss phaseBoss = fight.CombatState.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<ILiberationPrimaryPhaseBoss>()
                .Single(boss => boss.LiberationPhase == phase);

            await fight.Encounter.TriggerPlaceholderPhaseEnd(fight.CombatState, fromTurnLimit: false);
            await WaitFrames(5);
            await fight.Encounter.CompletePhaseTransition(phaseBoss);
            await WaitFrames(10);
        }

        ArtFloorNostalgicScentBoss nostalgicScent = fight.CombatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorNostalgicScentBoss>()
            .Single();
        await VerifyCounteredWinterGrantsCrownAsync(fight, nostalgicScent);

        CleanupRun();
        await WaitForRunCleanup("nostalgic scent counter verifier cleanup");
    }

    private static async Task VerifyFinalDaCapoPhaseOnlyAsync()
    {
        (CombatState combatState, ArtFloorLiberationEncounter _) = await StartPhaseSixFight("ARTFLOORVERIFY_PHASE6");
        VerifyFinalDaCapoPhase(combatState);
        await VerifyFinalDaCapoPresentationAsync(combatState);
        await VerifyFinalDaCapoMechanicsAsync(combatState);

        CleanupRun();
        await WaitForRunCleanup("phase 6 verifier cleanup");
    }

    private static async Task VerifyFinalPerformerStunOnlyAsync()
    {
        (CombatState combatState, ArtFloorLiberationEncounter _) =
            await StartPhaseSixFight("ARTFLOORVERIFY_PERFORMER_STUN");
        ArtFloorFinalDaCapoBoss boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorFinalDaCapoBoss>()
            .Single();

        await VerifyFinalPerformerStunSurvivesIntentSyncAsync(
            combatState,
            boss);

        CleanupRun();
        await WaitForRunCleanup("performer stun verifier cleanup");
    }

    private static async Task VerifySolemnMourningEgoDeathCleanupAsync()
    {
        (CombatState combatState, TechnologyFloorSolemnMourningBoss boss) =
            await StartSolemnMourningFight("SOLEMN_EGO_DEATH_VERIFY");
        Creature player = combatState.PlayerCreatures.Single();
        Creature bossCreature = boss.Creature;
        var context = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetCurrentHp(bossCreature, 2m);
        await PowerCmdCompat.Apply<ThornsPower>(context, player, 1m, player, null, silent: true);

        bool queued = await boss.QueueEgoSequence();
        Require(queued, "Solemn Mourning EGO did not queue.");
        Require(bossCreature.Monster?.NextMove?.Id == "SOLEMN_MOURNING_EGO",
            "Solemn Mourning next move was not EGO.");

        await boss.PerformMove();
        await WaitFrames(20);

        Require(bossCreature.IsDead, "Solemn Mourning did not die to thorns during EGO.");
        Require(!HasSolemnMourningEgoFlashLayer(),
            "Solemn Mourning EGO flash layer remained after thorns death.");
        Log.Info(LogPrefix + "SolemnEgoDeathCleanup bossDead=true flashLayer=false");

        CleanupRun();
        await WaitForRunCleanup("solemn EGO death cleanup verifier cleanup");
    }

    private sealed record ArtFloorCombatContext(
        CombatState CombatState,
        ArtFloorLiberationEncounter Encounter,
        ArtFloorDaCapoBoss DaCapo,
        Creature DaCapoCreature,
        ArtFloorFirstPerformer FirstPerformer,
        Creature FirstPerformerCreature);

    private static async Task<ArtFloorCombatContext> StartFight(string seed)
    {
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");

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
            ModelDb.Encounter<ArtFloorLiberationEncounter>().ToMutable(),
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
            "ArtFloorLiberation combat start");
        await WaitFrames(5);

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var encounter = combatState.Encounter as ArtFloorLiberationEncounter
            ?? throw new InvalidOperationException("ArtFloorLiberationEncounter was not active.");
        ArtFloorDaCapoBoss daCapo = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorDaCapoBoss>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("Da Capo was not found.");
        ArtFloorFirstPerformer firstPerformer = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorFirstPerformer>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("First Performer was not found.");

        return new ArtFloorCombatContext(
            combatState,
            encounter,
            daCapo,
            daCapo.Creature,
            firstPerformer,
            firstPerformer.Creature);
    }

    private static async Task<(CombatState, ArtFloorLiberationEncounter)> StartPhaseSixFight(string seed)
    {
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");

        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);

        var encounter = (ArtFloorLiberationEncounter)ModelDb.Encounter<ArtFloorLiberationEncounter>().ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "6",
            ["KilledBossCount"] = "5",
            ["TransitionPending"] = "False",
            ["SettlementTriggered"] = "False",
            ["EndedByPlaceholder"] = "False",
            ["EndedByLethalDamage"] = "False"
        });

        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
            "ArtFloorLiberation phase 6 combat start");
        await WaitFrames(5);

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var activeEncounter = combatState.Encounter as ArtFloorLiberationEncounter
            ?? throw new InvalidOperationException("ArtFloorLiberationEncounter was not active.");
        Require(activeEncounter.CurrentPhase == 6, "Phase 6 verifier did not start in phase 6.");
        return (combatState, activeEncounter);
    }

    private static async Task<(CombatState, TechnologyFloorSolemnMourningBoss)> StartSolemnMourningFight(string seed)
    {
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");

        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);

        var encounter = (TechnologyFloorLiberationEncounter)ModelDb.Encounter<TechnologyFloorLiberationEncounter>().ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "4",
            ["KilledBossCount"] = "3",
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
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
            "TechnologyFloorLiberation phase 4 combat start");
        await WaitFrames(5);

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<TechnologyFloorSolemnMourningBoss>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("Solemn Mourning boss was not found.");
        return (combatState, boss);
    }

    private static void VerifyRoster(ArtFloorCombatContext fight)
    {
        Require(fight.CombatState.Enemies.Count == 2, "Expected exactly two Art Floor enemies.");
        Require(fight.CombatState.Enemies[0].Monster is ArtFloorFirstPerformer, "First slot was not First Performer.");
        Require(fight.CombatState.Enemies[1].Monster is ArtFloorDaCapoBoss, "Second slot was not Da Capo.");
    }

    private static void VerifyDaCapoStats(Creature creature)
    {
        Require(creature.MaxHp is >= 293 and <= 295, "Da Capo max HP outside 293..295 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, "Da Capo");
        Require(libraryCreature.MaxChaoValue == 200, "Da Capo max chao was not 200: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 200, "Da Capo current chao was not 200: " + libraryCreature.CurrentChaoValue);
        RequireAllResistances(libraryCreature, LibraryResistanceLevel.Immune, "Da Capo");
    }

    private static void VerifyFirstPerformerStats(Creature creature)
    {
        Require(creature.MaxHp is >= 34 and <= 36, "First Performer max HP outside 34..36 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, "First Performer");
        Require(libraryCreature.MaxChaoValue == 40, "First Performer max chao was not 40: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 40, "First Performer current chao was not 40: " + libraryCreature.CurrentChaoValue);
    }

    private static void VerifyBeyondFragmentStats(Creature creature)
    {
        Require(creature.MaxHp is >= 173 and <= 175, "Beyond Fragment max HP outside 173..175 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, "Beyond Fragment");
        Require(libraryCreature.MaxChaoValue == 100, "Beyond Fragment max chao was not 100: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 100, "Beyond Fragment current chao was not 100: " + libraryCreature.CurrentChaoValue);
    }

    private static void VerifyLittleGalaxyPhase(ArtFloorCombatContext fight)
    {
        Require(fight.CombatState.Enemies.Count == 3, "Phase 3 did not spawn exactly three enemies.");

        Creature leftFriend = fight.CombatState.Enemies.SingleOrDefault(static enemy =>
                enemy.SlotName == ArtFloorLiberationEncounter.GalaxyFriendLeftSlot)
            ?? throw new InvalidOperationException("Phase 3 left Galaxy Friend missing.");
        Creature bossCreature = fight.CombatState.Enemies.SingleOrDefault(static enemy =>
                enemy.SlotName == ArtFloorLiberationEncounter.LittleGalaxySlot)
            ?? throw new InvalidOperationException("Phase 3 Little Galaxy boss missing.");
        Creature rightFriend = fight.CombatState.Enemies.SingleOrDefault(static enemy =>
                enemy.SlotName == ArtFloorLiberationEncounter.GalaxyFriendRightSlot)
            ?? throw new InvalidOperationException("Phase 3 right Galaxy Friend missing.");

        Require(leftFriend.Monster is ArtFloorGalaxyFriend, "Left phase 3 enemy was not ArtFloorGalaxyFriend.");
        Require(bossCreature.Monster is ArtFloorLittleGalaxyBoss boss, "Center phase 3 enemy was not ArtFloorLittleGalaxyBoss.");
        Require(rightFriend.Monster is ArtFloorGalaxyFriend, "Right phase 3 enemy was not ArtFloorGalaxyFriend.");

        VerifyLittleGalaxyBossStats(bossCreature);
        VerifyArtFloorGalaxyFriendStats(leftFriend, "Left Galaxy Friend");
        VerifyArtFloorGalaxyFriendStats(rightFriend, "Right Galaxy Friend");

        Require(leftFriend.Monster?.NextMove?.Id == "WAIT", "Left Galaxy Friend did not start with WAIT.");
        Require(rightFriend.Monster?.NextMove?.Id == "STARLIGHT_FALL", "Right Galaxy Friend did not start with STARLIGHT_FALL.");
        Require(bossCreature.Monster?.NextMove?.Id == "PARTING_TEARS", "Little Galaxy did not start with PARTING_TEARS.");

        string bossPowers = DescribePowers(bossCreature);
        string leftPowers = DescribePowers(leftFriend);
        string rightPowers = DescribePowers(rightFriend);
        Require(bossCreature.HasPower<ArtFloorErosionPower>(), "Little Galaxy missing ArtFloorErosionPower. powers=" + bossPowers);
        Require(bossCreature.HasPower<SpiderBudUntargetablePower>(), "Little Galaxy missing SpiderBudUntargetablePower. powers=" + bossPowers);
        SpiderBudUntargetablePower untargetablePower = bossCreature.GetPower<SpiderBudUntargetablePower>()
            ?? throw new InvalidOperationException("Little Galaxy untargetable power could not be read.");
        Require(!untargetablePower.ShouldAllowTargeting(bossCreature),
            "Little Galaxy untargetable power did not block targeting before friends died.");
        Require(bossCreature.HasPower<ArtFloorLittleGalaxyEternalFarewellPower>(), "Little Galaxy missing Eternal Farewell power. powers=" + bossPowers);
        Require(bossCreature.HasPower<ArtFloorLittleGalaxyPebblePower>(), "Little Galaxy missing Pebble power. powers=" + bossPowers);
        Require(leftFriend.HasPower<ArtFloorGalaxyDoNotLeaveMePower>(), "Left Galaxy Friend missing Do Not Leave Me power. powers=" + leftPowers);
        Require(rightFriend.HasPower<ArtFloorGalaxyDoNotLeaveMePower>(), "Right Galaxy Friend missing Do Not Leave Me power. powers=" + rightPowers);

        RequireResourceExists(ArtFloorLittleGalaxyBoss.IdleTexturePath, "Little Galaxy idle texture");
        RequireResourceExists(ArtFloorLittleGalaxyBoss.AttackFrameS4TexturePath, "Little Galaxy S4 texture");
        RequireResourceExists(ArtFloorLittleGalaxyBoss.AttackFrameS3TexturePath, "Little Galaxy S3 texture");
        RequireResourceExists(ArtFloorLittleGalaxyBoss.AttackFrameS2TexturePath, "Little Galaxy S2 texture");
        RequireResourceExists(ArtFloorLittleGalaxyBoss.AttackFrameS1TexturePath, "Little Galaxy S1 texture");
        RequireResourceExists(ModelDb.Card<OurLittleGalaxyEgoPreviewCard>().PortraitPath, "Little Galaxy EGO preview portrait");

        Require(CardPoolContains(ModelDb.CardPool<LibraryOfRuinaEgoCardPool>(), typeof(OurLittleGalaxyEgoPreviewCard)),
            "Our Little Galaxy preview card was not registered to LibraryOfRuinaEgoCardPool.");

        TextureRect? backgroundImage = NCombatRoom.Instance?.Background
            ?.FindChild("ArtFloorLiberationBackgroundImage", recursive: true, owned: false) as TextureRect;
        Require(backgroundImage?.Texture?.ResourcePath == ArtFloorLiberationBackgroundController.PhaseThreeTexturePath,
            "Phase 3 background was not the Galaxy background.");
    }

    private static async Task VerifyLittleGalaxyFakeDeathAndEgoAsync(ArtFloorCombatContext fight)
    {
        ArtFloorLittleGalaxyBoss boss = fight.CombatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorLittleGalaxyBoss>()
            .Single();
        ArtFloorGalaxyFriend leftFriend = (ArtFloorGalaxyFriend)fight.CombatState.Enemies
            .Single(static enemy => enemy.SlotName == ArtFloorLiberationEncounter.GalaxyFriendLeftSlot)
            .Monster!;
        ArtFloorGalaxyFriend rightFriend = (ArtFloorGalaxyFriend)fight.CombatState.Enemies
            .Single(static enemy => enemy.SlotName == ArtFloorLiberationEncounter.GalaxyFriendRightSlot)
            .Monster!;

        NCreature leftNode = NCombatRoom.Instance?.GetCreatureNode(leftFriend.Creature)
            ?? throw new InvalidOperationException("Left Galaxy Friend node was missing before fake death.");

        await CreatureCmd.Kill(leftFriend.Creature);
        await boss.RefreshTurnStartState();
        Require(leftFriend.IsFakeDead, "Left Galaxy Friend did not enter fake death.");
        Require(leftFriend.Creature.CurrentHp == ArtFloorGalaxyFriend.FakeDeathHp,
            "Fake-dead Galaxy Friend did not drop to 0 HP.");
        Require(!leftNode.IntentContainer.Visible,
            "Fake-dead Galaxy Friend still displayed its intent container.");
        Require(boss.Creature.Monster?.NextMove?.Id == "PARTING_TEARS",
            "One fake-dead friend did not force PARTING_TEARS.");

        await leftFriend.TickFakeDeathOnPlayerTurnStart();
        await leftFriend.TickFakeDeathOnPlayerTurnStart();
        int expectedRecoveredHp = Math.Max(
            1,
            (int)Math.Ceiling(leftFriend.Creature.MaxHp * 0.80m));
        Require(leftFriend.IsFakeDead, "Galaxy Friend left fake death before its revive intent.");
        await leftFriend.Creature.Monster!.NextMove.PerformMove(fight.CombatState.PlayerCreatures);
        Require(!leftFriend.IsFakeDead, "Galaxy Friend did not leave fake death after revive intent.");
        Require(leftFriend.Creature.CurrentHp == expectedRecoveredHp,
            "Galaxy Friend did not recover 80% max HP after fake death: " + leftFriend.Creature.CurrentHp);
        Require(leftNode.IntentContainer.Visible,
            "Recovered Galaxy Friend did not restore its intent container.");

        await CreatureCmd.Kill(leftFriend.Creature);
        Require(leftFriend.IsFakeDead && !leftNode.IntentContainer.Visible,
            "Galaxy Friend did not hide its intent after entering fake death a second time.");

        await CreatureCmd.Kill(rightFriend.Creature);

        Require(leftFriend.Creature.IsDead && rightFriend.Creature.IsDead,
            "Both fake-dead Galaxy Friends were not forced into true death immediately.");
        Require(!leftFriend.IsFakeDead && !rightFriend.IsFakeDead,
            "Galaxy Friends stayed fake-dead after both had died.");
        Require(fight.CombatState.Enemies.All(static enemy => enemy.Monster is not ArtFloorGalaxyFriend),
            "Galaxy Friends remained in combat after both died.");
        Require(NCombatRoom.Instance?.GetCreatureNode(leftFriend.Creature) == null
                && NCombatRoom.Instance?.GetCreatureNode(rightFriend.Creature) == null,
            "Galaxy Friend creature nodes were not cleared immediately after both died.");
        Require(boss.IsExposedAfterFriendsDead, "Little Galaxy was not exposed after all friends truly died.");
        Require(boss.Creature.CurrentHp <= 50, "Little Galaxy HP was not reduced to at most 50 after all friends died.");
        Require(boss.Creature.Monster?.NextMove?.Id == "OUR_LITTLE_GALAXY",
            "All friends true death did not queue OUR_LITTLE_GALAXY.");
        Require(boss.Creature.Monster?.NextMove?.Intents
                .Any(static intent => intent is PlayCardAttackIntent<OurLittleGalaxyEgoPreviewCard>) == true,
            "All friends true death did not display the Little Galaxy EGO intent.");
        Require(!boss.Creature.HasPower<SpiderBudUntargetablePower>(),
            "Little Galaxy still had SpiderBudUntargetablePower after all friends died.");

        LibraryCreature bossLibraryCreature = (LibraryCreature)boss.Creature;
        Require(bossLibraryCreature.CurrentChaoValue == 0,
            "Little Galaxy chao was not depleted after all friends died: " + bossLibraryCreature.CurrentChaoValue);
        Require(boss.Creature.IsHittable, "Little Galaxy was not hittable after all friends died.");

        await boss.PerformMove();
        await WaitFrames(5);

        Require(boss.Creature.Monster?.NextMove?.Id == MonsterModel.stunnedMoveId,
            "Little Galaxy did not force permanent STUNNED after OUR_LITTLE_GALAXY.");
        Require(bossLibraryCreature.CurrentChaoValue == 0,
            "Little Galaxy chao was not locked at 0 after OUR_LITTLE_GALAXY: " + bossLibraryCreature.CurrentChaoValue);
        Require(!bossLibraryCreature.RestoreChaoOnNextOwnerTurn,
            "Little Galaxy still had normal one-turn chao recovery after OUR_LITTLE_GALAXY.");
        Require(!boss.Creature.HasPower<SpiderBudUntargetablePower>(),
            "Little Galaxy regained SpiderBudUntargetablePower after OUR_LITTLE_GALAXY.");

        boss.Creature.PrepareForNextTurn(fight.CombatState.PlayerCreatures, rollNewMove: true);
        Require(boss.Creature.Monster?.NextMove?.Id == MonsterModel.stunnedMoveId,
            "Little Galaxy permanent stun did not survive rolling the next move.");
    }

    private static void VerifyLittleGalaxyBossStats(Creature creature)
    {
        Require(creature.MaxHp is >= 114 and <= 116, "Little Galaxy max HP outside 114..116 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, "Little Galaxy");
        Require(libraryCreature.MaxChaoValue == 60, "Little Galaxy max chao was not 60: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 60, "Little Galaxy current chao was not 60: " + libraryCreature.CurrentChaoValue);
    }

    private static ArtFloorPleasureBoss VerifyPleasurePhase(ArtFloorCombatContext fight)
    {
        Require(fight.CombatState.Enemies.Count == 1, "Phase 4 did not spawn exactly one enemy.");
        Creature bossCreature = fight.CombatState.Enemies.SingleOrDefault(static enemy =>
                enemy.SlotName == ArtFloorLiberationEncounter.PleasureSlot)
            ?? throw new InvalidOperationException("Phase 4 Pleasure boss missing from Pleasure slot.");
        ArtFloorPleasureBoss boss = bossCreature.Monster as ArtFloorPleasureBoss
            ?? throw new InvalidOperationException("Phase 4 enemy was not ArtFloorPleasureBoss.");

        Require(bossCreature.MaxHp is >= 190 and <= 194, "Pleasure max HP outside 190..194 at A0: " + bossCreature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(bossCreature, "Pleasure");
        Require(libraryCreature.MaxChaoValue == 100, "Pleasure max chao was not 100: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 100, "Pleasure current chao was not 100: " + libraryCreature.CurrentChaoValue);
        Require(bossCreature.Monster?.NextMove?.Id == "GRINNING", "Pleasure did not start with GRINNING.");

        string powers = DescribePowers(bossCreature);
        Require(bossCreature.HasPower<ArtFloorErosionPower>(), "Pleasure missing ArtFloorErosionPower. powers=" + powers);
        Require(bossCreature.HasPower<ArtFloorPleasureJoyThornsPower>(), "Pleasure missing Joy Thorns. powers=" + powers);
        Require(bossCreature.HasPower<ArtFloorPleasureSoftBodyPower>(), "Pleasure missing Soft Body. powers=" + powers);
        Require(bossCreature.HasPower<ArtFloorPleasureUnbearablePleasurePower>(), "Pleasure missing Unbearable Pleasure. powers=" + powers);
        Require(bossCreature.HasPower<ArtFloorPleasureExplodingHeadPower>(), "Pleasure missing Exploding Head. powers=" + powers);

        foreach (string path in
                 ArtFloorPleasureCreatureVisuals.Profile.AssetPaths)
        {
            RequireResourceExists(path, "Pleasure visual asset");
        }

        RequireResourceExists(ModelDb.Card<PleasureCard>().PortraitPath, "Pleasure status card portrait");
        RequireResourceExists(ModelDb.Card<PleasureEgoCard>().PortraitPath, "Pleasure EGO card portrait");
        Require(CardPoolContains(ModelDb.CardPool<StatusCardPool>(), typeof(PleasureCard)),
            "Pleasure status card was not registered to StatusCardPool.");
        Require(CardPoolContains(ModelDb.CardPool<LibraryOfRuinaEgoCardPool>(), typeof(PleasureEgoCard)),
            "Pleasure EGO preview card was not registered to LibraryOfRuinaEgoCardPool.");

        TextureRect? backgroundImage = NCombatRoom.Instance?.Background
            ?.FindChild("ArtFloorLiberationBackgroundImage", recursive: true, owned: false) as TextureRect;
        Require(backgroundImage?.Texture?.ResourcePath == ArtFloorLiberationBackgroundController.PhaseFourTexturePath,
            "Phase 4 background was not the Spiny Bus background.");

        TextureRect? filterImage = NCombatRoom.Instance?.Background
            ?.FindChild("GalaxyChildFilterImage", recursive: true, owned: false) as TextureRect;
        Require(filterImage == null || !filterImage.Visible, "Galaxy filter remained visible in phase 4.");

        return boss;
    }

    private static ArtFloorNostalgicScentBoss VerifyNostalgicScentPhase(ArtFloorCombatContext fight)
    {
        Require(fight.CombatState.Enemies.Count == 3, "Phase 5 did not spawn exactly three enemies.");

        Creature leftDustborn = fight.CombatState.Enemies.SingleOrDefault(static enemy =>
                enemy.SlotName == ArtFloorLiberationEncounter.DustbornLeftSlot)
            ?? throw new InvalidOperationException("Phase 5 left Dustborn missing.");
        Creature bossCreature = fight.CombatState.Enemies.SingleOrDefault(static enemy =>
                enemy.SlotName == ArtFloorLiberationEncounter.NostalgicScentSlot)
            ?? throw new InvalidOperationException("Phase 5 Nostalgic Scent boss missing.");
        Creature rightDustborn = fight.CombatState.Enemies.SingleOrDefault(static enemy =>
                enemy.SlotName == ArtFloorLiberationEncounter.DustbornRightSlot)
            ?? throw new InvalidOperationException("Phase 5 right Dustborn missing.");

        Require(leftDustborn.Monster is ArtFloorDustbornPerson, "Left phase 5 enemy was not Dustborn.");
        ArtFloorNostalgicScentBoss boss = bossCreature.Monster as ArtFloorNostalgicScentBoss
            ?? throw new InvalidOperationException("Center phase 5 enemy was not ArtFloorNostalgicScentBoss.");
        Require(rightDustborn.Monster is ArtFloorDustbornPerson, "Right phase 5 enemy was not Dustborn.");

        VerifyNostalgicScentStats(bossCreature);
        VerifyDustbornStats(leftDustborn, "Left Dustborn");
        VerifyDustbornStats(rightDustborn, "Right Dustborn");

        Require(bossCreature.Monster?.NextMove?.Id == ArtFloorNostalgicScentBoss.WinterBeginningMoveId,
            "Nostalgic Scent did not force WINTER_BEGINNING when no player had crown.");
        Require(leftDustborn.Monster?.NextMove?.Id == ArtFloorDustbornPerson.WinterStasisMoveId,
            "Left Dustborn did not start in WINTER_STASIS.");
        Require(rightDustborn.Monster?.NextMove?.Id == ArtFloorDustbornPerson.WinterStasisMoveId,
            "Right Dustborn did not start in WINTER_STASIS.");

        string bossPowers = DescribePowers(bossCreature);
        string leftPowers = DescribePowers(leftDustborn);
        string rightPowers = DescribePowers(rightDustborn);
        Require(bossCreature.HasPower<ArtFloorErosionPower>(), "Nostalgic Scent missing ArtFloorErosionPower. powers=" + bossPowers);
        Require(bossCreature.HasPower<ArtFloorSuffocatingAtonementPower>(), "Nostalgic Scent missing Suffocating Atonement. powers=" + bossPowers);
        Require(bossCreature.HasPower<ArtFloorUnfadingFlowerPower>(), "Nostalgic Scent missing Unfading Flower. powers=" + bossPowers);
        Require(bossCreature.HasPower<ArtFloorClayDollPower>(), "Nostalgic Scent missing Clay Doll. powers=" + bossPowers);
        Require(!bossCreature.HasPower<ArtFloorPetalPower>(), "Nostalgic Scent should not start with Petal. powers=" + bossPowers);
        Require(leftDustborn.HasPower<ArtFloorClayDollPower>(), "Left Dustborn missing Clay Doll. powers=" + leftPowers);
        Require(leftDustborn.HasPower<ArtFloorDustToDustPower>(), "Left Dustborn missing Dust to Dust. powers=" + leftPowers);
        Require(leftDustborn.HasPower<MinionPower>(), "Left Dustborn missing MinionPower. powers=" + leftPowers);
        Require(leftDustborn.HasPower<ArtFloorDustbornWinterStasisPower>(), "Left Dustborn missing winter stasis. powers=" + leftPowers);
        Require(rightDustborn.HasPower<ArtFloorClayDollPower>(), "Right Dustborn missing Clay Doll. powers=" + rightPowers);
        Require(rightDustborn.HasPower<ArtFloorDustToDustPower>(), "Right Dustborn missing Dust to Dust. powers=" + rightPowers);
        Require(rightDustborn.HasPower<MinionPower>(), "Right Dustborn missing MinionPower. powers=" + rightPowers);
        Require(rightDustborn.HasPower<ArtFloorDustbornWinterStasisPower>(), "Right Dustborn missing winter stasis. powers=" + rightPowers);

        foreach (string path in
                 ArtFloorNostalgicScentCreatureVisuals.Profile.AssetPaths)
        {
            RequireResourceExists(path, "Nostalgic Scent visual asset");
        }

        foreach (string path in
                 ArtFloorDustbornPersonCreatureVisuals.Profile.AssetPaths)
        {
            RequireResourceExists(path, "Dustborn visual asset");
        }

        RequireResourceExists(ArtFloorNostalgicScentBoss.AttackSfxPath, "Nostalgic Scent attack sfx");
        RequireResourceExists(ArtFloorNostalgicScentBoss.RangedSfxPath, "Nostalgic Scent ranged sfx");
        RequireResourceExists(ArtFloorNostalgicScentBoss.EgoSfxPath, "Nostalgic Scent EGO sfx");
        RequireResourceExists(ArtFloorNostalgicScentBoss.EgoFinishSfxPath, "Nostalgic Scent EGO finish sfx");
        RequireResourceExists(ArtFloorNostalgicScentBoss.GuardSfxPath, "Nostalgic Scent guard sfx");
        RequireResourceExists(ArtFloorDustbornPerson.AttackSfxPath, "Dustborn attack sfx");
        RequireResourceExists(ArtFloorDustbornPerson.DodgeSfxPath, "Dustborn dodge sfx");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_green_passive_power.png"), "Art Floor green passive power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_atonement_crown_power.png"), "Atonement Crown power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_petal_power.png"), "Petal power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_fragrance_power.png"), "Fragrance power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_collapse_power.png"), "Collapse power icon");
        RequireResourceExists(ModelDb.Card<NostalgicScentEgoCard>().PortraitPath, "Nostalgic Scent EGO card portrait");

        Require(CardPoolContains(ModelDb.CardPool<LibraryOfRuinaEgoCardPool>(), typeof(NostalgicScentEgoCard)),
            "Nostalgic Scent EGO preview card was not registered to LibraryOfRuinaEgoCardPool.");

        TextureRect? backgroundImage = NCombatRoom.Instance?.Background
            ?.FindChild("ArtFloorLiberationBackgroundImage", recursive: true, owned: false) as TextureRect;
        Require(backgroundImage?.Texture?.ResourcePath == ArtFloorLiberationBackgroundController.PhaseFiveTexturePath,
            "Phase 5 background was not the Nostalgic Scent background.");

        TextureRect? filterImage = NCombatRoom.Instance?.Background
            ?.FindChild("GalaxyChildFilterImage", recursive: true, owned: false) as TextureRect;
        Require(filterImage == null || !filterImage.Visible, "Galaxy filter remained visible in phase 5.");

        return boss;
    }

    private static async Task VerifyNostalgicScentMechanicsAsync(
        ArtFloorCombatContext fight,
        ArtFloorNostalgicScentBoss boss)
    {
        var context = new ThrowingPlayerChoiceContext();
        Creature player = fight.CombatState.PlayerCreatures.Single();
        Creature bossCreature = boss.Creature;
        Creature leftDustborn = fight.CombatState.Enemies.Single(static enemy =>
            enemy.SlotName == ArtFloorLiberationEncounter.DustbornLeftSlot);
        Creature rightDustborn = fight.CombatState.Enemies.Single(static enemy =>
            enemy.SlotName == ArtFloorLiberationEncounter.DustbornRightSlot);

        int leftHpBefore = leftDustborn.CurrentHp;
        await CreatureCmdCompat.Damage(
            context,
            leftDustborn,
            8m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            dealer: player,
            cardSource: null);
        Require(leftDustborn.CurrentHp == leftHpBefore,
            "Dustborn winter stasis did not block physical HP damage.");

        await VerifyCounteredWinterGrantsCrownAsync(fight, boss);

        int bossHpBeforeNoCrown = bossCreature.CurrentHp;
        await CreatureCmdCompat.Damage(
            context,
            bossCreature,
            8m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            dealer: player,
            cardSource: null);
        Require(bossCreature.CurrentHp == bossHpBeforeNoCrown,
            "Non-crown player damaged Nostalgic Scent.");

        await PowerCmdCompat.Apply<ArtFloorAtonementCrownPower>(context, player, 1m, bossCreature, null);
        Require(player.HasPower<ArtFloorAtonementCrownPower>(), "Atonement Crown was not applied to player.");
        int bossHpBeforeCrown = bossCreature.CurrentHp;
        await CreatureCmdCompat.Damage(
            context,
            bossCreature,
            8m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            dealer: player,
            cardSource: null);
        Require(bossCreature.CurrentHp < bossHpBeforeCrown,
            "Crown holder could not damage Nostalgic Scent.");

        await PowerCmdCompat.Apply<ArtFloorNextTurnCollapsePower>(context, player, 1m, rightDustborn, null);
        ArtFloorNextTurnCollapsePower nextTurnCollapse = player.GetPower<ArtFloorNextTurnCollapsePower>()
            ?? throw new InvalidOperationException("Next-turn Collapse was not applied.");
        Require(nextTurnCollapse.Amount == 1, "Next-turn Collapse should carry collapse stacks only: " + nextTurnCollapse.Amount);
        int hpBeforeDelayedCollapseDamage = player.CurrentHp;
        await CreatureCmdCompat.Damage(
            context,
            player,
            2m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            dealer: rightDustborn,
            cardSource: null);
        Require(player.CurrentHp == hpBeforeDelayedCollapseDamage - 2,
            "Next-turn Collapse affected HP damage before conversion.");
        await nextTurnCollapse.BeforeSideTurnStart(context, CombatSide.Player, fight.CombatState.PlayerCreatures, fight.CombatState);
        Require(!player.HasPower<ArtFloorNextTurnCollapsePower>(), "Next-turn Collapse did not remove itself on player turn start.");
        ArtFloorCollapsePower collapse = player.GetPower<ArtFloorCollapsePower>()
            ?? throw new InvalidOperationException("Next-turn Collapse did not convert into Collapse.");
        Require(collapse.Amount == 1, "Collapse should use Amount as duration only: " + collapse.Amount);
        int hpBeforeCollapseDamage = player.CurrentHp;
        await CreatureCmdCompat.Damage(
            context,
            player,
            2m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            dealer: rightDustborn,
            cardSource: null);
        Require(player.CurrentHp == hpBeforeCollapseDamage - 4,
            "Collapse did not double HP damage.");
        await collapse.AfterSideTurnEnd(context, CombatSide.Player, fight.CombatState.PlayerCreatures);
        Require(!player.HasPower<ArtFloorCollapsePower>(), "Collapse did not tick down after owner turn end.");

        fight.CombatState.RoundNumber = 2;
        ArtFloorUnfadingFlowerPower flower = bossCreature.GetPower<ArtFloorUnfadingFlowerPower>()
            ?? throw new InvalidOperationException("Unfading Flower was not applied.");
        await flower.BeforeSideTurnStart(context, CombatSide.Enemy, fight.CombatState.Enemies, fight.CombatState);
        ArtFloorPetalPower petal = bossCreature.GetPower<ArtFloorPetalPower>()
            ?? throw new InvalidOperationException("Petal was not gained on round 2 enemy turn start.");
        Require(petal.Amount == 1, "Petal first gain amount was not 1: " + petal.Amount);
        await PowerCmdCompat.ModifyAmount(context, petal, ArtFloorPetalPower.Threshold - 1 - petal.Amount, bossCreature, null, silent: true);
        Require(petal.Amount == ArtFloorPetalPower.Threshold - 1, "Petal threshold setup failed: " + petal.Amount);
        await flower.BeforeSideTurnStart(context, CombatSide.Enemy, fight.CombatState.Enemies, fight.CombatState);
        Require(bossCreature.GetPower<ArtFloorPetalPower>() is not { Amount: > 0 },
            "Petal did not clear at threshold.");
        Require(bossCreature.Monster?.NextMove?.Id == ArtFloorNostalgicScentBoss.NostalgicScentEgoMoveId,
            "Petal threshold did not queue Nostalgic Scent EGO.");

        int maxHpBeforeEgo = player.MaxHp;
        await CreatureCmd.GainBlock(player, 100m, ValueProp.Unpowered, null, fast: true);
        await boss.PerformMove();
        await WaitFrames(5);
        int expectedMaxHpAfterEgo = maxHpBeforeEgo
                                    - ArtFloorEgoNumbers.NostalgicScentHitCount * ArtFloorEgoNumbers.NostalgicScentMaxHpLossOnFullBlock;
        Require(player.MaxHp == expectedMaxHpAfterEgo,
            "Fully blocked EGO did not reduce max HP per segment: " + player.MaxHp + " expected=" + expectedMaxHpAfterEgo);

        LibraryCreature rightLibraryCreature = RequireLibraryCreature(rightDustborn, "Right Dustborn");
        await LibraryCreatureCmd.SetCurrentChaoValue(rightLibraryCreature, 0m);
        await WaitFrames(5);
        Require(rightDustborn.IsDead, "Dustborn did not die when chao reached 0.");
    }

    private static async Task VerifyCounteredWinterGrantsCrownAsync(
        ArtFloorCombatContext fight,
        ArtFloorNostalgicScentBoss boss)
    {
        var context = new ThrowingPlayerChoiceContext();
        Creature player = fight.CombatState.PlayerCreatures.Single();
        Creature bossCreature = boss.Creature;

        await PowerCmdCompat.Apply<ArtFloorDustbornWinterStasisPower>(
            context,
            player,
            1m,
            bossCreature,
            null,
            silent: true);
        int playerHpBeforeCounteredWinter = player.CurrentHp;
        await boss.PerformMove();
        await WaitFrames(5);
        Require(player.CurrentHp == playerHpBeforeCounteredWinter,
            "Zero-damage Winter Beginning unexpectedly reduced player HP.");
        Require(player.HasPower<ArtFloorAtonementCrownPower>(),
            "Zero-damage Winter Beginning did not grant Atonement Crown.");
        boss.Creature.PrepareForNextTurn(fight.CombatState.PlayerCreatures);
        await WaitFrames(2);
        Require(boss.NextMove.Id != ArtFloorNostalgicScentBoss.WinterBeginningMoveId,
            "Nostalgic Scent remained locked in Winter Beginning after granting Atonement Crown.");

        ArtFloorDustbornWinterStasisPower? playerWinterStasis =
            player.GetPower<ArtFloorDustbornWinterStasisPower>();
        if (playerWinterStasis != null)
        {
            await PowerCmd.Remove(playerWinterStasis);
        }

        ArtFloorAtonementCrownPower? counteredWinterCrown =
            player.GetPower<ArtFloorAtonementCrownPower>();
        if (counteredWinterCrown != null)
        {
            await PowerCmd.Remove(counteredWinterCrown);
        }
    }

    private static void VerifyNoPleasureCardsRemain(CombatStateLike combatState)
    {
        var localPlayer = LocalContextCompat.GetMe(combatState);
        foreach (Creature player in combatState.PlayerCreatures)
        {
            var pleasureCards = player.Player?.PlayerCombatState?.AllCards
                .Where(static card => card is PleasureCard)
                .ToArray()
                ?? Array.Empty<CardModel>();
            Require(pleasureCards.Length == 0,
                "Pleasure cards remained in combat state after phase transition: "
                + string.Join(", ", pleasureCards.Select(static card => card.Pile?.Type.ToString() ?? "<null>")));

            if (player.Player != localPlayer)
            {
                continue;
            }

            NPlayerHand hand = NCombatRoom.Instance?.Ui.Hand
                ?? throw new InvalidOperationException("Local player hand UI was not available.");
            Require(!hand.ActiveHolders.Any(static holder => holder.CardNode?.Model is PleasureCard),
                "Pleasure card holder remained in local hand UI after phase transition.");
        }
    }

    private static void VerifyNostalgicScentStats(Creature creature)
    {
        Require(creature.MaxHp is >= 173 and <= 175, "Nostalgic Scent max HP outside 173..175 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, "Nostalgic Scent");
        Require(libraryCreature.MaxChaoValue == 60, "Nostalgic Scent max chao was not 60: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 60, "Nostalgic Scent current chao was not 60: " + libraryCreature.CurrentChaoValue);
        RequireResistance(libraryCreature, LibraryDamageType.Slash, LibraryResistanceLevel.Resist, LibraryResistanceLevel.Endure, "Nostalgic Scent");
        RequireResistance(libraryCreature, LibraryDamageType.Pierce, LibraryResistanceLevel.Resist, LibraryResistanceLevel.Normal, "Nostalgic Scent");
        RequireResistance(libraryCreature, LibraryDamageType.Blunt, LibraryResistanceLevel.Resist, LibraryResistanceLevel.Normal, "Nostalgic Scent");
    }

    private static void VerifyDustbornStats(Creature creature, string label)
    {
        Require(creature.MaxHp is >= 62 and <= 64, label + " max HP outside 62..64 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, label);
        Require(libraryCreature.MaxChaoValue == 70, label + " max chao was not 70: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 70, label + " current chao was not 70: " + libraryCreature.CurrentChaoValue);
        RequireResistance(libraryCreature, LibraryDamageType.Slash, LibraryResistanceLevel.Endure, LibraryResistanceLevel.Endure, label);
        RequireResistance(libraryCreature, LibraryDamageType.Pierce, LibraryResistanceLevel.Endure, LibraryResistanceLevel.Normal, label);
        RequireResistance(libraryCreature, LibraryDamageType.Blunt, LibraryResistanceLevel.Endure, LibraryResistanceLevel.Vulnerable, label);
    }

    private static void RequireResistance(
        LibraryCreature creature,
        LibraryDamageType type,
        LibraryResistanceLevel physical,
        LibraryResistanceLevel chaos,
        string label)
    {
        Require(creature.GetPhysicalResistanceLevel(type) == physical,
            label + " physical resistance mismatch for " + type + ": " + creature.GetPhysicalResistanceLevel(type));
        Require(creature.GetChaosResistanceLevel(type) == chaos,
            label + " chao resistance mismatch for " + type + ": " + creature.GetChaosResistanceLevel(type));
    }

    private static void VerifyArtFloorGalaxyFriendStats(Creature creature, string label)
    {
        Require(creature.MaxHp is >= 84 and <= 86, label + " max HP outside 84..86 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, label);
        Require(libraryCreature.MaxChaoValue == 50, label + " max chao was not 50: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 50, label + " current chao was not 50: " + libraryCreature.CurrentChaoValue);
    }

    private static void VerifyOpeningPowers(ArtFloorCombatContext fight)
    {
        Require(fight.DaCapoCreature.HasPower<MostBeautifulPerformancePower>(), "Da Capo missing MostBeautifulPerformancePower.");
        Require(fight.DaCapoCreature.HasPower<ArtFloorEnsemblePower>(), "Da Capo missing ArtFloorEnsemblePower.");
        Require(fight.FirstPerformerCreature.HasPower<SilentPerformancePower>(), "First Performer missing SilentPerformancePower.");
        Require(fight.FirstPerformerCreature.HasPower<MinionPower>(), "First Performer missing MinionPower.");
        Require(fight.CombatState.PlayerCreatures.All(static player => player.HasPower<ArtFloorLiberationControllerPower>()),
            "Player missing hidden ArtFloorLiberationControllerPower.");
    }

    private static async Task VerifyOpeningVisualsAndIntents(ArtFloorCombatContext fight)
    {
        NCreature daCapoNode = await WaitFor(
            () => NCombatRoom.Instance?.GetCreatureNode(fight.DaCapoCreature),
            "Da Capo creature node");
        NCreature performerNode = await WaitFor(
            () => NCombatRoom.Instance?.GetCreatureNode(fight.FirstPerformerCreature),
            "First Performer creature node");

        RequireSpriteTexture(daCapoNode, ArtFloorDaCapoBoss.IdleTexturePath, "Da Capo");
        RequireSpriteTexture(performerNode, ArtFloorFirstPerformer.IdleTexturePath, "First Performer");
        Require(
            NCombatRoom.Instance?.GetNodeOrNull<TextureRect>("ArtFloorLiberationBackground") == null,
            "Art Floor background was attached to NCombatRoom root and can cover creature visuals.");

        await daCapoNode.RefreshIntents();
        await performerNode.RefreshIntents();
        await WaitFrames(3);

        AbstractIntent[] daCapoIntents = daCapoNode.IntentContainer
            .GetChildren()
            .OfType<NIntent>()
            .Select(GetIntentModel)
            .Where(static intent => intent != null)
            .Cast<AbstractIntent>()
            .ToArray();
        Require(daCapoIntents.Any(), "Da Capo has no visible opening intent.");
        Require(daCapoIntents.All(static intent => intent is not IEnemyCardIntent),
            "Da Capo opening intent still uses enemy-card/book-page presentation.");
        Require(daCapoIntents.Any(static intent => intent is CombinedDefendBuffIntent),
            "Da Capo opening intent was not ordinary defend+buff intent.");
        Require(performerNode.IntentContainer.GetChildren().OfType<NIntent>().Any(static intent => GetIntentModel(intent) is UnknownIntent),
            "First Performer opening intent was not Unknown.");
    }

    private static void VerifyFinalDaCapoPhase(CombatState combatState)
    {
        Require(combatState.Enemies.Count == 5, "Phase 6 did not spawn exactly five enemies.");
        Require(combatState.Enemies.Any(static enemy => enemy.Monster is ArtFloorFinalDaCapoBoss),
            "Phase 6 final Da Capo boss missing.");
        Require(combatState.Enemies.Count(static enemy => enemy.Monster is ArtFloorDaCapoPerformer) == 4,
            "Phase 6 performer count was not 4.");

        Creature daCapo = combatState.Enemies.Single(static enemy => enemy.SlotName == ArtFloorLiberationEncounter.FinalDaCapoSlot);
        VerifyFinalDaCapoStats(daCapo);

        foreach (ArtFloorDaCapoPerformerVariant variant in Enum.GetValues<ArtFloorDaCapoPerformerVariant>())
        {
            Creature performer = combatState.Enemies.Single(enemy =>
                enemy.Monster is ArtFloorDaCapoPerformer p && p.Id == enemy.Monster.Id && GetPerformerVariant(p) == variant);
            VerifyFinalPerformerStats(performer, variant);
        }

        VerifyFinalPerformerSlot(ArtFloorLiberationEncounter.FinalPerformerOneSlot, ArtFloorDaCapoPerformerVariant.First);
        VerifyFinalPerformerSlot(ArtFloorLiberationEncounter.FinalPerformerTwoSlot, ArtFloorDaCapoPerformerVariant.Second);
        VerifyFinalPerformerSlot(ArtFloorLiberationEncounter.FinalPerformerThreeSlot, ArtFloorDaCapoPerformerVariant.Third);
        VerifyFinalPerformerSlot(ArtFloorLiberationEncounter.FinalPerformerFourSlot, ArtFloorDaCapoPerformerVariant.Fourth);
        VerifyFinalDaCapoPowerIcons();

        void VerifyFinalPerformerSlot(string slot, ArtFloorDaCapoPerformerVariant expectedVariant)
        {
            Creature performerCreature = combatState.Enemies.Single(enemy => enemy.SlotName == slot);
            var performer = performerCreature.Monster as ArtFloorDaCapoPerformer
                ?? throw new InvalidOperationException(slot + " was not ArtFloorDaCapoPerformer.");
            Require(GetPerformerVariant(performer) == expectedVariant,
                slot + " variant mismatch: " + GetPerformerVariant(performer) + " expected=" + expectedVariant);
            RequirePerformerTitleNumber(performer, expectedVariant);
        }
    }

    private static async Task VerifyFinalDaCapoMechanicsAsync(CombatState combatState)
    {
        Creature daCapo = combatState.Enemies.Single(static enemy => enemy.SlotName == ArtFloorLiberationEncounter.FinalDaCapoSlot);
        NCreature daCapoNode = await WaitFor(
            () => NCombatRoom.Instance?.GetCreatureNode(daCapo),
            "Final Da Capo node");
        await daCapoNode.RefreshIntents();
        await WaitFrames(3);
        Require(daCapoNode.IntentContainer.GetChildCount() == 2, "Final Da Capo opening intent count was not 2.");

        await VerifyFinalDaCapoPowerMechanicsAsync(combatState, daCapo);

        ArtFloorFinalDaCapoBoss boss = (ArtFloorFinalDaCapoBoss)daCapo.Monster!;
        await VerifyFinalPerformerStunSurvivesIntentSyncAsync(combatState, boss);
        await boss.AdvanceMovement(new ThrowingPlayerChoiceContext());
        RequireLibraryCreature(daCapo, "Final Da Capo second movement");

        await boss.AdvanceMovement(new ThrowingPlayerChoiceContext());
        await boss.AdvanceMovement(new ThrowingPlayerChoiceContext());
        await boss.AdvanceMovement(new ThrowingPlayerChoiceContext());
        await VerifyFinalPerformerUnknownIntentsAsync(combatState, "fifth movement");

        await boss.AdvanceMovement(new ThrowingPlayerChoiceContext());
        await VerifyFinalPerformerUnknownIntentsAsync(combatState, "sixth movement");
        Log.Info(LogPrefix + "FinalDaCapoPhaseFixes performers=ok powers=ok");
    }

    private static async Task VerifyFinalPerformerStunSurvivesIntentSyncAsync(
        CombatState combatState,
        ArtFloorFinalDaCapoBoss boss)
    {
        Creature performerCreature = combatState.Enemies.Single(static enemy =>
            enemy.Monster is ArtFloorDaCapoPerformer performer
            && GetPerformerVariant(performer) == ArtFloorDaCapoPerformerVariant.First);
        var performer = (ArtFloorDaCapoPerformer)performerCreature.Monster!;
        LibraryCreature libraryCreature = RequireLibraryCreature(
            performerCreature,
            "Final Performer stun sync");
        var context = new ThrowingPlayerChoiceContext();
        int daCapoBlockBeforeStunnedMove = boss.Creature.Block;

        try
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
            Require(libraryCreature.IsStunPending && performerCreature.IsStunned,
                "Final Performer did not enter STUNNED after its chao reached 0.");

            await boss.BeforeSideTurnStart(
                context,
                CombatSide.Enemy,
                combatState.Enemies,
                combatState);
            Require(performerCreature.IsStunned,
                "Final Performer lost STUNNED when Da Capo synchronized performer intents.");

            await performer.PerformMove();
            Require(boss.Creature.Block == daCapoBlockBeforeStunnedMove,
                "Confused Final Performer executed PERFORM instead of its STUNNED move.");
        }
        finally
        {
            libraryCreature.RestorePreStunResistance();
            await LibraryCreatureCmd.SetCurrentChaoValue(
                libraryCreature,
                libraryCreature.MaxChaoValue);
            performerCreature.PrepareForNextTurn(combatState.PlayerCreatures);
        }
    }

    private static async Task VerifyFinalDaCapoPresentationAsync(CombatState combatState)
    {
        foreach (Creature creature in combatState.Enemies.Where(static enemy => enemy.Monster is ArtFloorDaCapoPerformer))
        {
            var performer = (ArtFloorDaCapoPerformer)creature.Monster!;
            ArtFloorDaCapoPerformerVariant variant = GetPerformerVariant(performer);
            NCreature node = await WaitFor(
                () => NCombatRoom.Instance?.GetCreatureNode(creature),
                "Final Performer " + (int)variant + " node");
            RequireSpriteTexture(node, performer.IdleTexturePath, "Final Performer " + (int)variant);
            Require(Math.Abs(node.Visuals.IntentPosition.Position.Y - -268f) < 0.01f,
                "Final Performer " + (int)variant + " intent Y was not lowered to -268: "
                + node.Visuals.IntentPosition.Position.Y);
        }
    }

    private static async Task VerifyFinalPerformerUnknownIntentsAsync(CombatState combatState, string label)
    {
        foreach (Creature creature in combatState.Enemies.Where(static enemy => enemy.Monster is ArtFloorDaCapoPerformer))
        {
            var performer = (ArtFloorDaCapoPerformer)creature.Monster!;
            ArtFloorDaCapoPerformerVariant variant = GetPerformerVariant(performer);
            NCreature node = await WaitFor(
                () => NCombatRoom.Instance?.GetCreatureNode(creature),
                "Final Performer " + (int)variant + " node for " + label);
            await node.RefreshIntents();
            await WaitFrames(3);
            Require(node.IntentContainer.GetChildren().OfType<NIntent>()
                    .Any(static intent => GetIntentModel(intent) is UnknownIntent),
                "Final Performer " + (int)variant + " did not show Unknown intent during " + label + ".");
            Require(Math.Abs(node.Visuals.IntentPosition.Position.Y - -268f) < 0.01f,
                "Final Performer " + (int)variant + " Unknown intent Y drifted during " + label + ": "
                + node.Visuals.IntentPosition.Position.Y);
        }
    }

    private static async Task VerifyFinalDaCapoPowerMechanicsAsync(CombatState combatState, Creature applier)
    {
        Creature playerCreature = combatState.PlayerCreatures.Single();
        var player = playerCreature.Player
            ?? throw new InvalidOperationException("Verifier player was missing.");
        var playerCombatState = player.PlayerCombatState
            ?? throw new InvalidOperationException("Verifier player combat state was missing.");
        CardModel testCard = playerCombatState.AllCards
            .FirstOrDefault(static card => ModelDb.Affliction<Bound>().CanAfflict(card))
            ?? throw new InvalidOperationException("Verifier player had no bindable combat card.");
        CombatSide previousSide = combatState.CurrentSide;
        var context = new ThrowingPlayerChoiceContext();

        try
        {
            CardCmd.ClearAffliction(testCard);
            combatState.CurrentSide = CombatSide.Player;
            await PlayerCmd.SetEnergy(3m, player);

            ArtFloorImbalancedPower imbalanced = await PowerCmdCompat.ApplyDebuff<ArtFloorImbalancedPower>(
                    context,
                    playerCreature,
                    1m,
                    applier,
                    null)
                ?? throw new InvalidOperationException("Imbalanced was not applied.");
            await imbalanced.AfterCardDrawn(context, testCard, fromHandDraw: true);
            Require(playerCombatState.Energy == 3,
                "Imbalanced counted start-of-turn hand draw. energy=" + playerCombatState.Energy);
            await imbalanced.AfterCardDrawn(context, testCard, fromHandDraw: false);
            Require(playerCombatState.Energy == 2,
                "Imbalanced did not count in-turn draw. energy=" + playerCombatState.Energy);
            await PowerCmd.Remove(imbalanced);

            ArtFloorDaCapoSoulBindingPower soulBinding = await PowerCmdCompat.ApplyDebuff<ArtFloorDaCapoSoulBindingPower>(
                    context,
                    playerCreature,
                    1m,
                    applier,
                    null)
                ?? throw new InvalidOperationException("Soul Binding was not applied.");
            await soulBinding.AfterCardDrawn(context, testCard, fromHandDraw: false);
            Require(testCard.Affliction is Bound, "Soul Binding did not afflict drawn card with Bound.");
            Require(!soulBinding.ShouldPlay(testCard, AutoPlayType.None),
                "Soul Binding allowed the first Bound card to be played.");
            await soulBinding.BeforeSideTurnEnd(
                context,
                CombatSide.Player,
                [playerCreature]);
            Require(!playerCreature.HasPower<ArtFloorDaCapoSoulBindingPower>(),
                "Soul Binding was not removed before its owner's side turn ended.");
            Require(testCard.Affliction is not Bound,
                "Soul Binding did not clear Bound before its owner's side turn ended.");
        }
        finally
        {
            combatState.CurrentSide = previousSide;
            CardCmd.ClearAffliction(testCard);
        }
    }

    private static void VerifyFinalDaCapoStats(Creature creature)
    {
        Require(creature.MaxHp is >= 194 and <= 196, "Final Da Capo max HP outside 194..196 at A0: " + creature.MaxHp);
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, "Final Da Capo");
        Require(libraryCreature.MaxChaoValue == 100, "Final Da Capo max chao was not 100: " + libraryCreature.MaxChaoValue);
        Require(libraryCreature.CurrentChaoValue == 100, "Final Da Capo current chao was not 100: " + libraryCreature.CurrentChaoValue);
        RequireResistance(libraryCreature, LibraryDamageType.Slash, LibraryResistanceLevel.Immune, LibraryResistanceLevel.Immune, "Final Da Capo");
        RequireResistance(libraryCreature, LibraryDamageType.Pierce, LibraryResistanceLevel.Immune, LibraryResistanceLevel.Immune, "Final Da Capo");
        RequireResistance(libraryCreature, LibraryDamageType.Blunt, LibraryResistanceLevel.Normal, LibraryResistanceLevel.Normal, "Final Da Capo");
    }

    private static void VerifyFinalPerformerStats(Creature creature, ArtFloorDaCapoPerformerVariant variant)
    {
        Require(creature.MaxHp is >= 20 and <= 22, $"Performer {variant} max HP outside 20..22 at A0: {creature.MaxHp}");
        LibraryCreature libraryCreature = RequireLibraryCreature(creature, $"Performer {variant}");
        Require(libraryCreature.MaxChaoValue == 30, $"Performer {variant} max chao was not 30: {libraryCreature.MaxChaoValue}");
        Require(libraryCreature.CurrentChaoValue == 30, $"Performer {variant} current chao was not 30: {libraryCreature.CurrentChaoValue}");
        string powers = DescribePowers(creature);
        Require(creature.HasPower<MinionPower>(), $"Performer {variant} missing MinionPower. powers={powers}");
        Require(creature.HasPower<ArtFloorFinalDaCapoPerformerPassivePower>(),
            $"Performer {variant} missing ArtFloorFinalDaCapoPerformerPassivePower. powers={powers}");
    }

    private static void RequirePerformerTitleNumber(
        ArtFloorDaCapoPerformer performer,
        ArtFloorDaCapoPerformerVariant variant)
    {
        LocString title = performer.Title;
        Require(title.LocEntryKey == "ART_FLOOR_DA_CAPO_PERFORMER.variant.name",
            "Performer " + (int)variant + " did not use numbered title loc key: " + title.LocEntryKey);
        Require(title.Variables.TryGetValue("Number", out object? number)
                && Convert.ToDecimal(number) == (int)variant,
            "Performer title number mismatch for " + variant + ": " + (number ?? "<null>"));
    }

    private static void VerifyFinalDaCapoPowerIcons()
    {
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_final_da_capo_cycle_power.png"), "Final Da Capo cycle power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_final_da_capo_aria_power.png"), "Final Da Capo aria power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_final_da_capo_performer_passive_power.png"), "Final Da Capo performer passive power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_adagio_cantabile_power.png"), "Final Da Capo adagio power icon");
        RequireResourceExists(ImageHelper.GetImagePath("powers/art_floor_da_capo_soul_binding_power.png"), "Final Da Capo Soul Binding power icon");
    }

    private static ArtFloorDaCapoPerformerVariant GetPerformerVariant(ArtFloorDaCapoPerformer performer)
    {
        return typeof(ArtFloorDaCapoPerformer)
            .GetField("_variant", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(performer) is ArtFloorDaCapoPerformerVariant variant
            ? variant
            : ArtFloorDaCapoPerformerVariant.First;
    }

    private static LibraryCreature RequireLibraryCreature(Creature creature, string label)
    {
        return creature as LibraryCreature
               ?? throw new InvalidOperationException(label + " creature was not a LibraryCreature.");
    }

    private static void RequireAllResistances(LibraryCreature creature, LibraryResistanceLevel expected, string label)
    {
        foreach (LibraryDamageType type in Enum.GetValues<LibraryDamageType>())
        {
            if (type == LibraryDamageType.None)
            {
                continue;
            }

            Require(creature.GetPhysicalResistanceLevel(type) == expected, label + " physical resistance mismatch: " + type);
            Require(creature.GetChaosResistanceLevel(type) == expected, label + " chao resistance mismatch: " + type);
        }
    }

    private static void RequireSpriteTexture(NCreature node, string expectedPath, string label)
    {
        Sprite2D sprite = node.Visuals.GetNode<Sprite2D>("%Visuals");
        string? actualPath = sprite.Texture?.ResourcePath;
        Require(actualPath == expectedPath, label + " texture mismatch: " + (actualPath ?? "<null>"));
    }

    private static void RequireResourceExists(string path, string label)
    {
        Require(ResourceLoader.Exists(path), label + " resource was not found: " + path);
    }

    private static bool CardPoolContains(CardPoolModel pool, Type cardType)
    {
        PropertyInfo? property = typeof(CardPoolModel).GetProperty("AllCards")
            ?? typeof(CardPoolModel).GetProperty("Cards");
        if (property?.GetValue(pool) is not IEnumerable cards)
        {
            return false;
        }

        foreach (object? card in cards)
        {
            if (card != null && cardType.IsInstanceOfType(card))
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribePowers(Creature creature)
    {
        return string.Join(",", creature.Powers.Select(static power => power.GetType().Name + ":" + power.Id.Entry));
    }

    private static bool HasSolemnMourningEgoFlashLayer()
    {
        Node? root = NGame.Instance?.GetTree().Root;
        return root?.FindChild(
            TechnologyFloorSolemnMourningBoss.EgoFlashLayerNodeName,
            recursive: true,
            owned: false) is Node layer
            && GodotObject.IsInstanceValid(layer);
    }

    private static AbstractIntent? GetIntentModel(NIntent intent)
    {
        return typeof(NIntent)
            .GetField("_intent", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(intent) as AbstractIntent;
    }

    private static void CleanupRun()
    {
        RunManager.Instance.CleanUp(graceful: true);
    }

    private static Task WaitForRunCleanup(string description)
    {
        return WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                         && CombatManager.Instance.DebugOnlyGetState() == null
                         && RunManager.Instance.DebugOnlyGetState() == null,
            description);
    }

    private static async Task<T> WaitFor<T>(Func<T?> resolve, string description, int maxFrames = 900)
        where T : class
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (resolve() is { } value)
            {
                return value;
            }

            await WaitFrame();
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
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
