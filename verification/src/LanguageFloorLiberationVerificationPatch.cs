using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class LanguageFloorLiberationVerificationPatch
{
    private const string VerifyArg = "lor-verify-language-floor-phase1";
    private const string VerifyPhaseTwoArg = "lor-verify-language-floor-phase2";
    private const string VerifyPunishEvilArg =
        "lor-verify-language-floor-punish-evil";
    private const string VerifyShadowAmbushArg = "lor-verify-language-floor-shadow-ambush";
    private const string VerifyPhaseTransitionArg =
        "lor-verify-language-floor-phase-transition";
    private const string VerifyCobaltOpeningStunArg =
        "lor-verify-language-floor-cobalt-opening-stun";
    private const string LogPrefix = "[LibraryOfRuina.LanguageFloor.Verify] ";
    private static readonly HashSet<string> ScriptedVisualScenePaths = new(StringComparer.Ordinal)
    {
        "res://scenes/creature_visuals/language_floor_scarlet_scar.tscn",
        "res://scenes/creature_visuals/language_floor_lost_everything_wolf.tscn"
    };

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

    private static bool HasVerifyArg()
    {
        return HasArg(VerifyArg)
            || HasArg(VerifyPhaseTwoArg)
            || HasArg(VerifyPunishEvilArg)
            || HasArg(VerifyShadowAmbushArg)
            || HasArg(VerifyPhaseTransitionArg)
            || HasArg(VerifyCobaltOpeningStunArg);
    }

    private static bool HasPhaseTwoVerifyArg()
    {
        return HasArg(VerifyPhaseTwoArg);
    }

    private static bool HasPunishEvilVerifyArg()
    {
        return HasArg(VerifyPunishEvilArg);
    }

    private static bool HasShadowAmbushVerifyArg()
    {
        return HasArg(VerifyShadowAmbushArg);
    }

    private static bool HasPhaseTransitionVerifyArg() =>
        HasArg(VerifyPhaseTransitionArg);

    private static bool HasCobaltOpeningStunVerifyArg() =>
        HasArg(VerifyCobaltOpeningStunArg);

    private static bool HasArg(string argName)
    {
        return CommandLineHelper.HasArg(argName)
            || Environment.GetCommandLineArgs()
                .Any(arg => string.Equals(
                    arg.TrimStart('-'),
                    argName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunAsync()
    {
        bool phaseTwoOnly = HasPhaseTwoVerifyArg();
        bool punishEvilOnly = HasPunishEvilVerifyArg();
        bool shadowAmbushOnly = HasShadowAmbushVerifyArg();
        bool phaseTransitionOnly = HasPhaseTransitionVerifyArg();
        bool cobaltOpeningStunOnly = HasCobaltOpeningStunVerifyArg();

        try
        {
            if (cobaltOpeningStunOnly)
            {
                await VerifyCobaltOpeningStunOnly();
                Log.Info(LogPrefix
                    + "LANGUAGE_FLOOR_COBALT_OPENING_STUN_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (phaseTransitionOnly)
            {
                await VerifyScarletKillsWolf();
                await VerifyScarletDeath();
                await VerifyPhaseTwoDeath();
                Log.Info(LogPrefix
                    + "LANGUAGE_FLOOR_PHASE_TRANSITION_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (shadowAmbushOnly)
            {
                await VerifyShadowAmbushOnly();
                Log.Info(LogPrefix + "LANGUAGE_FLOOR_SHADOW_AMBUSH_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (punishEvilOnly)
            {
                await VerifyPunishEvilOnly();
                Log.Info(LogPrefix + "LANGUAGE_FLOOR_PUNISH_EVIL_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (phaseTwoOnly)
            {
                await VerifyPhaseTwoOpeningAndMechanics();
                await VerifyPhaseTwoDeath();
                Log.Info(LogPrefix + "LANGUAGE_FLOOR_PHASE2_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            await VerifySimultaneousDeath();
            await VerifyOpeningAndMechanics();
            await VerifyNonScarletWolfDeath();
            await VerifyScarletKillsWolf();
            await VerifyScarletDeath();
            await VerifyPhaseTwoOpeningAndMechanics();
            await VerifyPhaseTwoDeath();
            Log.Info(LogPrefix + "LANGUAGE_FLOOR_PHASE2_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix
                + (shadowAmbushOnly
                    ? "LANGUAGE_FLOOR_SHADOW_AMBUSH_FAILED: "
                    : cobaltOpeningStunOnly
                        ? "LANGUAGE_FLOOR_COBALT_OPENING_STUN_FAILED: "
                    : punishEvilOnly
                        ? "LANGUAGE_FLOOR_PUNISH_EVIL_FAILED: "
                    : phaseTransitionOnly
                        ? "LANGUAGE_FLOOR_PHASE_TRANSITION_FAILED: "
                    : phaseTwoOnly
                        ? "LANGUAGE_FLOOR_PHASE2_FAILED: "
                        : "LANGUAGE_FLOOR_PHASE1_FAILED: ")
                + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task VerifyCobaltOpeningStunOnly()
    {
        VerifyCobaltIntentDamageDefinitions();

        LanguageFloorPhaseTwoContext fight =
            await StartPhaseTwoFight("LANGUAGEFLOORVERIFY_COBALT_OPENING_STUN");
        LibraryCreature cobaltCreature =
            RequireLibraryCreature(fight.CobaltCreature, "Cobalt Scar opening stun");

        Require(fight.Cobalt.PlannedMoves.All(
                static move => move == LanguageFloorMoveKind.CobaltWolfComes)
            && fight.Cobalt.CounterIntentQueue.Count == 6,
            "Cobalt Scar did not begin with the first-turn counter plan.");

        await LibraryCreatureCmd.Stun(cobaltCreature);
        Require(cobaltCreature.IsStunned,
            "Opening Cobalt Scar did not enter STUNNED.");
        Require(fight.Cobalt.CounterIntentQueue.IsEmpty,
            "Opening counter intents remained queued after confusion.");

        await fight.Cobalt.PerformMove();
        fight.CobaltCreature.PrepareForNextTurn(
            fight.CombatState.PlayerCreatures);

        Require(fight.Cobalt.PlannedMoves.All(static move =>
                move is LanguageFloorMoveKind.CobaltDoNotProvoke
                    or LanguageFloorMoveKind.CobaltCough
                    or LanguageFloorMoveKind.CobaltSharpClaws)
            && fight.Cobalt.NextMove.Intents.All(
                static intent => intent is not ICounterIntent),
            "Cobalt Scar repeated its first-turn counter plan after confusion.");

        Creature player = fight.CombatState.PlayerCreatures.Single();
        if (player.GetPower<LanguageFloorScarPower>() is { } oldScar)
        {
            await PowerCmd.Remove(oldScar);
        }

        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await fight.Cobalt.DebugTransformToBigBadWolf();
        var instinctIntent = (AttackIntent)LanguageFloorCobaltScar.CreateIntent(
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct);
        int displayedDamage = instinctIntent.GetSingleDamage(
            [player],
            fight.CobaltCreature);
        List<int> instinctHitDamage = [];
        void RecordInstinctHit(int oldHp, int newHp)
        {
            if (newHp < oldHp)
            {
                instinctHitDamage.Add(oldHp - newHp);
            }
        }

        player.CurrentHpChanged += RecordInstinctHit;
        try
        {
            await fight.Cobalt.DebugPerformMove(
                LanguageFloorMoveKind.BigWolfUncontrollableInstinct);
        }
        finally
        {
            player.CurrentHpChanged -= RecordInstinctHit;
        }

        int expectedHitCount = LanguageFloorCobaltScar.GetMoveHitCount(
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct);
        Require(instinctHitDamage.Count == expectedHitCount,
            "Uncontrollable Instinct actual hit count did not match its intent. "
            + $"intent={expectedHitCount} actual={instinctHitDamage.Count}.");
        for (int hit = 0; hit < instinctHitDamage.Count; hit++)
        {
            int expectedDamage = displayedDamage
                + hit * LanguageFloorScarPower.DamagePerStack
                + (hit >= LanguageFloorScarPower.ExtraDamageThreshold
                    ? LanguageFloorScarPower.ExtraDamageAtFourStacks
                    : 0);
            Require(instinctHitDamage[hit] == expectedDamage,
                "Uncontrollable Instinct actual damage did not match its intent "
                + "and live Scar modifiers. "
                + $"hit={hit + 1} expected={expectedDamage} "
                + $"actual={instinctHitDamage[hit]}.");
        }

        Require(player.GetPower<LanguageFloorScarPower>()?.Amount
                == expectedHitCount,
            "Uncontrollable Instinct did not apply one Scar per displayed hit.");
        Log.Info(LogPrefix
            + "COBALT_INTENT_DAMAGE_OK "
            + $"instinctIntent={displayedDamage}x{expectedHitCount} "
            + $"actual=[{string.Join(',', instinctHitDamage)}]");

        CleanupRun();
        await WaitForRunCleanup("Cobalt opening-stun verifier cleanup");
    }

    private static async Task VerifyPunishEvilOnly()
    {
        VerifyPhaseTwoPowerDefinitions();

        LanguageFloorPhaseTwoContext durationFight =
            await StartPhaseTwoFight("LANGUAGEFLOORVERIFY_PUNISH_EVIL_DURATION");
        Player durationPlayer = durationFight.CombatState.Players.Single();
        int handCount = CardPile.GetCards(durationPlayer, PileType.Hand).Count();
        LibraryCreature durationCreature = RequireLibraryCreature(
            durationFight.CobaltCreature,
            "Punish Evil duration Cobalt Scar");
        Require(durationFight.Cobalt.SwallowWindowActive
                && durationFight.Cobalt.SwallowedCards.Count == 3
                && durationCreature.MaxChaoValue == 250
                && durationCreature.CurrentChaoValue == 250,
            "Punish Evil opening swallow state was invalid.");

        await durationFight.Cobalt.BeforeSideTurnStart(
            new BlockingPlayerChoiceContext(),
            CombatSide.Player,
            durationFight.CombatState.PlayerCreatures,
            durationFight.CombatState);
        Require(durationFight.Cobalt.SwallowWindowActive
                && durationFight.Cobalt.PlayerTurnsSinceSwallow == 1
                && durationFight.Cobalt.SwallowedCards.Count == 3,
            "Punish Evil did not remain active for the full player turn.");

        await durationFight.Cobalt.AfterSideTurnEnd(
            new BlockingPlayerChoiceContext(),
            CombatSide.Player,
            durationFight.CombatState.PlayerCreatures);
        Require(!durationFight.Cobalt.SwallowWindowActive
                && durationFight.Cobalt.SwallowedCards.Count == 0
                && durationCreature.MaxChaoValue == 150
                && durationCreature.CurrentChaoValue == 150,
            "Punish Evil did not consume swallowed cards at player-turn end.");
        Require(CardPile.GetCards(durationPlayer, PileType.Hand).Count() == handCount,
            "Punish Evil returned cards without a chao break.");

        CleanupRun();
        await WaitForRunCleanup("Punish Evil duration verifier cleanup");

        LanguageFloorPhaseTwoContext penaltyFight =
            await StartPhaseTwoFight("LANGUAGEFLOORVERIFY_PUNISH_EVIL_PENALTY");
        int hpBefore = penaltyFight.CobaltCreature.CurrentHp;
        int expectedHpLoss =
            LanguageFloorCobaltScar.CalculateSpitHpLoss(
                penaltyFight.CobaltCreature.MaxHp);
        await penaltyFight.Cobalt.DebugReturnSwallowedCards(applyHpLoss: true);
        Require(
            penaltyFight.CobaltCreature.CurrentHp == hpBefore - expectedHpLoss,
            "Punish Evil chao-break penalty did not remove 30% max HP.");
        Require(!penaltyFight.Cobalt.SwallowWindowActive
                && penaltyFight.Cobalt.SwallowedCards.Count == 0,
            "Punish Evil chao-break return did not clear swallowed-card state.");

        CleanupRun();
        await WaitForRunCleanup("Punish Evil penalty verifier cleanup");
    }

    private static async Task VerifyShadowAmbushOnly()
    {
        LanguageFloorPhaseTwoContext fight =
            await StartPhaseTwoFight("LANGUAGEFLOORVERIFY_SHADOW_AMBUSH");
        Creature player = fight.CombatState.PlayerCreatures.Single();
        await fight.Cobalt.DebugTransformToBigBadWolf();
        int threshold = fight.Cobalt.GetShadowDamageThreshold();
        fight.Cobalt.DebugSetState(
            LanguageFloorCobaltScarForm.BigBadWolf,
            fight.Cobalt.BigWolfEntryHp,
            0,
            0,
            LanguageFloorMoveKind.BigWolfHorrifyingClaws,
            LanguageFloorMoveKind.BigWolfBrutalFangs,
            LanguageFloorMoveKind.BigWolfBloodstainedHunt);
        await fight.Cobalt.AfterDamageReceived(
            new BlockingPlayerChoiceContext(),
            fight.CobaltCreature,
            new DamageResult(fight.CobaltCreature, ValueProp.Move)
            {
                UnblockedDamage = threshold
            },
            ValueProp.Move,
            player,
            null);
        await fight.Cobalt.AfterSideTurnEnd(
            new BlockingPlayerChoiceContext(),
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        fight.CobaltCreature.PrepareForNextTurn(
            fight.CombatState.PlayerCreatures);
        Require(fight.Cobalt.ShadowTurnsRemaining == 2
                && fight.Cobalt.PlannedMoves.SequenceEqual(
                    [
                        LanguageFloorMoveKind.BigWolfShadowAssault,
                        LanguageFloorMoveKind.BigWolfShadowAssault
                    ])
                && fight.CobaltCreature.Monster!.NextMove.Intents.Count == 2,
            "Shadow Ambush did not enter its two-intent Shadow Wolf state.");
        await VerifyShadowAmbushCardLimit(fight, player);
        CleanupRun();
        await WaitForRunCleanup("shadow ambush verifier cleanup");
    }

    private static async Task VerifyOpeningAndMechanics()
    {
        LanguageFloorCombatContext fight = await StartFight("LANGUAGEFLOORVERIFY_OPENING");
        VerifyStats(fight);
        VerifyPassivePowerUi(fight);
        Log.Info(LogPrefix + "LANGUAGE_FLOOR_PASSIVES_OK");
        VerifyPlansAndTargets(fight);
        VerifySavedPropertyRoundTrip();
        VerifyResourcesAndVisuals(fight);
        VerifyPhaseTwoPowerDefinitions();
        await VerifyDamageAndPowerRules(fight);
        Log.Info(LogPrefix + "OpeningAndMechanics OK");
        CleanupRun();
        await WaitForRunCleanup("opening verifier cleanup");
    }

    private static void VerifyStats(LanguageFloorCombatContext fight)
    {
        Require(
            fight.ScarletCreature.MaxHp is >= 190 and <= 194,
            "Scarlet max HP outside 190..194 at A0: " + fight.ScarletCreature.MaxHp);
        Require(
            fight.WolfCreature.MaxHp is >= 390 and <= 394,
            "Wolf max HP outside 390..394 at A0: " + fight.WolfCreature.MaxHp);
        Require(
            RequireLibraryCreature(fight.ScarletCreature, "Scarlet").MaxChaoValue == 400,
            "Scarlet max chao was not 400.");
        Require(
            RequireLibraryCreature(fight.WolfCreature, "Wolf").MaxChaoValue == 130,
            "Wolf max chao was not 130.");
        Require(fight.ScarletCreature.GetPower<LanguageFloorAngerGaugePower>() != null,
            "Scarlet anger gauge was missing.");
        Require(fight.ScarletCreature.GetPower<LanguageFloorRevengePassivePower>() != null,
            "Scarlet revenge passive was missing.");
        LanguageFloorDeathTrackerPower deathTracker =
            fight.ScarletCreature.GetPower<LanguageFloorDeathTrackerPower>()
            ?? throw new InvalidOperationException("Nightmare's End was missing.");
        Require(deathTracker.IsVisible, "Nightmare's End was not visible.");
        Require(fight.WolfCreature.GetPower<LanguageFloorDeathTrackerPower>() == null,
            "Wolf incorrectly owned a duplicate death tracker.");
        Require(fight.WolfCreature.GetPower<LanguageFloorWolfHowlPassivePower>() != null,
            "Phase-one wolf was missing Wolf Howl.");
        Require(fight.WolfCreature.GetPower<LanguageFloorWolfHowlingNightmarePassivePower>() != null,
            "Phase-one wolf was missing Howling Nightmare.");
        Require(fight.WolfCreature.GetPower<LanguageFloorRipOpenClawPassivePower>() != null,
            "Phase-one wolf was missing Rip Open Claw.");
        Require(fight.WolfCreature.GetPower<LanguageFloorPunishEvilPassivePower>() == null,
            "Phase-one wolf incorrectly retained Punish Evil.");
        Require(fight.WolfCreature.GetPower<LanguageFloorDestinedBigBadWolfPassivePower>() == null,
            "Phase-one wolf incorrectly retained Destined Big Bad Wolf.");
        Require(fight.WolfCreature.GetPower<LanguageFloorHideInDarknessPassivePower>() == null,
            "Cobalt Scar entered the Big Bad Wolf passive set before transforming.");
        Require(fight.WolfCreature.GetPower<LanguageFloorShadowAmbushPassivePower>() == null,
            "Cobalt Scar entered the Big Bad Wolf passive set before transforming.");
        Require(fight.WolfCreature.GetPower<LanguageFloorExhaustionPassivePower>() == null,
            "Cobalt Scar entered the Big Bad Wolf passive set before transforming.");
    }

    private static void VerifyPassivePowerUi(LanguageFloorCombatContext fight)
    {
        NPowerContainer powerContainer = GetPowerContainer(fight.WolfNode);
        PowerModel[] visiblePowers = powerContainer.GetChildren()
            .OfType<NPower>()
            .Select(static powerNode => powerNode.Model)
            .ToArray();
        Require(visiblePowers.OfType<LanguageFloorWolfHowlPassivePower>().Any(),
            "Wolf Howl had no visible NPower node.");
        Require(visiblePowers.OfType<LanguageFloorWolfHowlingNightmarePassivePower>().Any(),
            "Wolf Howling Nightmare had no visible NPower node.");
        Require(visiblePowers.OfType<LanguageFloorRipOpenClawPassivePower>().Any(),
            "Wolf Rip Open Claw had no visible NPower node.");
    }

    private static void VerifyPlansAndTargets(LanguageFloorCombatContext fight)
    {
        Require(fight.ScarletCreature.Monster!.NextMove.Intents.Count == 2,
            "Scarlet intent capacity was not 2.");
        Require(fight.WolfCreature.Monster!.NextMove.Intents.Count == 2,
            "Wolf opening intent capacity was not 2.");
        Require(
            fight.ScarletCreature.Monster.NextMove.Intents.All(IsOrdinaryLanguageFloorIntent),
            "Scarlet still used a dynamic intent implementation.");
        Require(
            fight.WolfCreature.Monster.NextMove.Intents.All(IsOrdinaryLanguageFloorIntent),
            "Wolf still used a dynamic intent implementation.");
        Require(!LanguageFloorScarletScar.GetNormalCandidates(1)
                .Contains(LanguageFloorMoveKind.DecisiveStrike),
            "Scarlet intent 5 was enabled on turn 1.");
        Require(!LanguageFloorScarletScar.GetNormalCandidates(2)
                .Contains(LanguageFloorMoveKind.DecisiveStrike),
            "Scarlet intent 5 was enabled on turn 2.");
        Require(LanguageFloorScarletScar.GetNormalCandidates(3)
                .Contains(LanguageFloorMoveKind.DecisiveStrike),
            "Scarlet intent 5 was not enabled on turn 3.");
        Require(LanguageFloorLostEverythingWolf.ShouldUseHowl(3, lowHealthMode: false),
            "High-health howl did not trigger on wolf turn 3.");
        Require(!LanguageFloorLostEverythingWolf.ShouldUseHowl(2, lowHealthMode: false),
            "High-health howl triggered before wolf turn 3.");
        Require(LanguageFloorLostEverythingWolf.ShouldUseHowl(2, lowHealthMode: true),
            "Low-health howl did not trigger on wolf turn 2.");
        Require(LanguageFloorLostEverythingWolf.HowlWeak == 2,
            "Wolf Howl did not apply two Weak.");

        Creature player = fight.CombatState.PlayerCreatures.Single();
        IReadOnlyList<Creature> scarletGroupTargets =
            LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner(
                fight.ScarletCreature);
        IReadOnlyList<Creature> wolfGroupTargets =
            LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner(
                fight.WolfCreature);
        Require(scarletGroupTargets.Contains(player)
                && scarletGroupTargets.Contains(fight.WolfCreature)
                && scarletGroupTargets.Count == 2,
            "Scarlet group targets did not contain player and wolf.");
        Require(wolfGroupTargets.Contains(player)
                && wolfGroupTargets.Contains(fight.ScarletCreature)
                && wolfGroupTargets.Count == 2,
            "Wolf group targets did not contain player and Scarlet.");
        Require(
            LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(
                fight.ScarletCreature) == fight.WolfCreature,
            "Scarlet did not prioritize the wolf.");
        Require(
            LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(
                fight.WolfCreature) == fight.ScarletCreature,
            "Wolf did not prioritize Scarlet.");
    }

    private static async Task VerifyDamageAndPowerRules(LanguageFloorCombatContext fight)
    {
        LanguageFloorAngerGaugePower gauge =
            fight.ScarletCreature.GetPower<LanguageFloorAngerGaugePower>()
            ?? throw new InvalidOperationException("Scarlet anger gauge disappeared.");
        LanguageFloorRipOpenClawPassivePower claw =
            fight.WolfCreature.GetPower<LanguageFloorRipOpenClawPassivePower>()
            ?? throw new InvalidOperationException("Phase-one Rip Open Claw disappeared.");
        var fullyBlocked = new DamageResult(fight.ScarletCreature, ValueProp.Move)
        {
            BlockedDamage = 10,
            UnblockedDamage = 0,
            WasFullyBlocked = true
        };
        await fight.Wolf.ApplyScarAndAnger(
            [fullyBlocked],
            LanguageFloorAngerGaugePower.WolfAttackAnger);
        Require(gauge.Amount == 0, "Fully blocked wolf hit changed anger.");
        await claw.AfterDamageGiven(
            new BlockingPlayerChoiceContext(),
            fight.WolfCreature,
            fullyBlocked,
            ValueProp.Move,
            fight.ScarletCreature,
            null);
        Require(fight.ScarletCreature.GetPower<LanguageFloorScarPower>()?.Amount == 1,
            "Fully blocked phase-one wolf hit did not apply Scar.");
        await PowerCmd.Remove(
            fight.ScarletCreature.GetPower<LanguageFloorScarPower>()
            ?? throw new InvalidOperationException("Blocked-hit Scar disappeared."));

        var hpDamage = new DamageResult(fight.ScarletCreature, ValueProp.Move)
        {
            UnblockedDamage = 1
        };
        await fight.Wolf.ApplyScarAndAnger(
            [hpDamage],
            LanguageFloorAngerGaugePower.WolfAttackAnger);
        Require(
            gauge.Amount == LanguageFloorAngerGaugePower.WolfAttackAnger,
            "Wolf HP damage did not add 45 anger.");
        await claw.AfterDamageGiven(
            new BlockingPlayerChoiceContext(),
            fight.WolfCreature,
            hpDamage,
            ValueProp.Move,
            fight.ScarletCreature,
            null);
        Require(fight.ScarletCreature.GetPower<LanguageFloorScarPower>()?.Amount == 1,
            "Phase-one wolf HP damage did not apply Scar.");

        LanguageFloorHuntMarkPower mark =
            await LibraryDurationPowerModel.ApplyWithDuration<LanguageFloorHuntMarkPower>(
                fight.WolfCreature,
                1,
                LanguageFloorHuntMarkPower.DefaultTurns,
                fight.ScarletCreature,
                null)
            ?? throw new InvalidOperationException("Hunt Mark was not applied.");
        decimal multiplier = mark.ModifyDamageMultiplicativeCompat(
            fight.WolfCreature,
            10,
            ValueProp.Move,
            fight.ScarletCreature,
            null,
            null);
        Require(multiplier == 1.5m, "Hunt Mark multiplier was not 1.5.");
        Require(
            mark.ModifyDamageMultiplicativeCompat(
                fight.WolfCreature,
                10,
                ValueProp.Move,
                fight.WolfCreature,
                null,
                null) == 1m,
            "Hunt Mark increased the marked wolf's own damage.");
        Require(
            mark.ModifyDamageMultiplicativeCompat(
                fight.ScarletCreature,
                10,
                ValueProp.Move,
                fight.WolfCreature,
                null,
                null) == 1m,
            "Hunt Mark increased wolf damage dealt to Scarlet Scar.");
        Creature previewPlayer = fight.CombatState.PlayerCreatures.Single();
        Require(
            mark.ModifyDamageMultiplicativeCompat(
                fight.WolfCreature,
                10,
                ValueProp.Move,
                previewPlayer,
                null,
                null) == 1m,
            "Hunt Mark increased player damage or card damage preview.");
        var groupIntent = new IndiscriminateAttackIntent(
            10,
            1,
            null,
            LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner);
        int expectedPlayerDamage = TargetedAttackIntentPreviewHelper.GetModifiedDamage(
            fight.ScarletCreature,
            previewPlayer,
            10);
        Require(
            groupIntent.GetTotalDamage(
                [previewPlayer, fight.WolfCreature],
                fight.ScarletCreature)
            == expectedPlayerDamage,
            "Indiscriminate attack preview did not use player damage.");
        await LibraryDurationPowerModel.ApplyWithDuration<LanguageFloorHuntMarkPower>(
            fight.WolfCreature,
            1,
            LanguageFloorHuntMarkPower.DefaultTurns,
            fight.ScarletCreature,
            null);
        Require(mark.Amount == 1, "Hunt Mark incorrectly stacked its multiplier amount.");
        Require(mark.TurnsRemaining == 4, "Hunt Mark duration did not stack to 4 turns.");
        Require(mark.IsVisible, "Hunt Mark was not visible.");
        await WaitFrames(2);
        NPower? huntMarkNode = GetPowerContainer(fight.WolfNode)
            .GetChildren()
            .OfType<NPower>()
            .FirstOrDefault(node => node.Model == mark);
        Require(huntMarkNode != null, "Hunt Mark had no visible NPower node.");
        Require(GodotTextureSafety.IsValid(mark.Icon), "Hunt Mark status icon was not loadable.");

        LanguageFloorScarPower scar =
            await PowerCmdCompat.SetAmount<LanguageFloorScarPower>(
                fight.ScarletCreature,
                3,
                fight.WolfCreature,
                null)
            ?? throw new InvalidOperationException("Scar power was not set to 3.");
        Require(
            scar.ModifyDamageAdditiveCompat(
                fight.ScarletCreature,
                10,
                ValueProp.Move,
                fight.WolfCreature,
                null,
                null) == 3,
            "Three Scar did not add three damage for the phase-one wolf.");
        scar = await PowerCmdCompat.SetAmount<LanguageFloorScarPower>(
            fight.ScarletCreature,
            4,
            fight.WolfCreature,
            null)
            ?? throw new InvalidOperationException("Scar power was not set to 4.");
        Require(
            scar.ModifyDamageAdditiveCompat(
                fight.ScarletCreature,
                10,
                ValueProp.Move,
                fight.WolfCreature,
                null,
                null) == 6,
            "Four Scar did not add six damage for the phase-one wolf.");

        Creature player = fight.CombatState.PlayerCreatures.Single();
        int hpBeforeBurst = player.CurrentHp;
        LanguageFloorScarPower playerScar =
            await PowerCmdCompat.SetAmount<LanguageFloorScarPower>(
                player,
                6,
                fight.WolfCreature,
                null)
            ?? throw new InvalidOperationException("Player Scar power was not set to 6.");
        await playerScar.AfterSideTurnStart(
            CombatSide.Player,
            [player],
            fight.CombatState);
        Require(
            player.CurrentHp == Math.Max(1, (int)Math.Ceiling(hpBeforeBurst * 0.8m)),
            "Scar 6-stack burst did not retain 80% current HP.");
        Require(player.GetPower<LanguageFloorScarPower>() == null,
            "Scar was not cleared after the 6-stack burst.");
        Require(player.GetPower<LibraryBleedingPower>()?.Amount == 6,
            "Scar did not apply 6 Bleed before bursting.");

        gauge.ResetAnger();
        await gauge.ChangeAnger(LanguageFloorAngerGaugePower.MaxAnger);
        LanguageFloorRagePower rage =
            fight.ScarletCreature.GetPower<LanguageFloorRagePower>()
            ?? throw new InvalidOperationException("Rage did not activate at 100 anger.");
        Require(
            fight.Scarlet.GetPlannedMove(0) == LanguageFloorMoveKind.IndiscriminateShot,
            "Rage did not force Scarlet slot 1 to Indiscriminate Shot.");
        Require(
            fight.ScarletCreature.Monster!.NextMove.Intents[0] is IndiscriminateAttackIntent,
            "Rage did not replace Scarlet slot 1 with an ordinary indiscriminate intent.");
        RequireBackgroundTexture(LanguageFloorLiberationBackgroundController.RageTexturePath);

        var context = new ThrowingPlayerChoiceContext();
        await rage.AfterSideTurnStart(
            CombatSide.Enemy,
            fight.CombatState.Enemies,
            fight.CombatState);
        LibraryStrongPower firstStrong = RequireTemporaryStrong(fight.ScarletCreature, 2);
        await firstStrong.AfterSideTurnEnd(context, CombatSide.Enemy, fight.CombatState.Enemies);
        await rage.AfterSideTurnEnd(context, CombatSide.Enemy, fight.CombatState.Enemies);
        Require(rage.TurnsRemaining == 1, "Rage did not have one turn remaining.");

        await rage.AfterSideTurnStart(
            CombatSide.Enemy,
            fight.CombatState.Enemies,
            fight.CombatState);
        LibraryStrongPower secondStrong = RequireTemporaryStrong(fight.ScarletCreature, 2);
        await secondStrong.AfterSideTurnEnd(context, CombatSide.Enemy, fight.CombatState.Enemies);
        await rage.AfterSideTurnEnd(context, CombatSide.Enemy, fight.CombatState.Enemies);
        Require(fight.ScarletCreature.GetPower<LanguageFloorRagePower>() == null,
            "Rage did not expire after 2 turns.");
        Require(gauge.Amount == 0, "Anger did not reset after Rage ended.");
        RequireBackgroundTexture(LanguageFloorLiberationBackgroundController.NormalTexturePath);
    }

    private static NPowerContainer GetPowerContainer(Node creatureNode)
    {
        return creatureNode.FindChild("PowerContainer", recursive: true, owned: false)
                   as NPowerContainer
               ?? throw new InvalidOperationException(
                   "PowerContainer was not found below " + creatureNode.GetPath() + ".");
    }

    private static void VerifySavedPropertyRoundTrip()
    {
        var scarletSource =
            (LanguageFloorScarletScar)ModelDb.Monster<LanguageFloorScarletScar>().ToMutable();
        scarletSource.DebugSetPlan(
            3,
            LanguageFloorMoveKind.DecisiveStrike,
            LanguageFloorMoveKind.ExplosiveShot,
            unrelievedAnger: true);
        SavedProperties scarletProps = CombatStateProperties.From(scarletSource)
            ?? throw new InvalidOperationException("Scarlet SavedProperties were empty.");
        var scarletClone =
            (LanguageFloorScarletScar)ModelDb.Monster<LanguageFloorScarletScar>().ToMutable();
        scarletProps.Fill(scarletClone);
        Require(
            scarletClone.EnemyTurnCount == 3
            && scarletClone.UnrelievedAnger
            && scarletClone.PlannedMoves.SequenceEqual(
                [
                    LanguageFloorMoveKind.DecisiveStrike,
                    LanguageFloorMoveKind.ExplosiveShot
                ]),
            "Scarlet saved plan did not round-trip.");

        var wolfSource =
            (LanguageFloorLostEverythingWolf)ModelDb
                .Monster<LanguageFloorLostEverythingWolf>()
                .ToMutable();
        wolfSource.DebugSetPlan(
            4,
            lowHealthMode: true,
            LanguageFloorMoveKind.Howl,
            LanguageFloorMoveKind.BrutalFangs,
            LanguageFloorMoveKind.HorrifyingClaws,
            LanguageFloorMoveKind.BloodstainedHunt);
        SavedProperties wolfProps = CombatStateProperties.From(wolfSource)
            ?? throw new InvalidOperationException("Wolf SavedProperties were empty.");
        var wolfClone =
            (LanguageFloorLostEverythingWolf)ModelDb
                .Monster<LanguageFloorLostEverythingWolf>()
                .ToMutable();
        wolfProps.Fill(wolfClone);
        Require(
            wolfClone.WolfTurnCount == 4
            && wolfClone.LowHealthMode
            && wolfClone.PlannedMoves.SequenceEqual(
                [
                    LanguageFloorMoveKind.Howl,
                    LanguageFloorMoveKind.BrutalFangs,
                    LanguageFloorMoveKind.HorrifyingClaws,
                    LanguageFloorMoveKind.BloodstainedHunt
                ]),
            "Wolf saved plan did not round-trip.");
    }

    private static void VerifyPhaseTwoPowerDefinitions()
    {
        Require(LanguageFloorPunishEvilPassivePower.ChaoResistanceIncrease == 60,
            "Punish Evil chao-resistance increase was not 100.");
        Require(LanguageFloorPunishEvilPassivePower.SpitHpLossPercent == 30
                && LanguageFloorCobaltScar.CalculateSpitHpLoss(100) == 30
                && LanguageFloorCobaltScar.CalculateSpitHpLoss(101) == 31,
            "Punish Evil spit penalty was not 30% max HP with ceiling rounding.");
        Require(LanguageFloorPunishEvilPassivePower.SpatCardCostIncrease == 1,
            "Punish Evil spat-card cost increase was not 1.");
        Require(
            LanguageFloorDestinedBigBadWolfPassivePower.TransformHpThreshold(100) == 50
            && LanguageFloorDestinedBigBadWolfPassivePower.TransformHpThreshold(101) == 51,
            "Destined Big Bad Wolf did not clamp to the ceiling of 50% max HP.");
        Require(
            LanguageFloorHideInDarknessPassivePower.ExceedsDamageThreshold(400, 100)
            && !LanguageFloorHideInDarknessPassivePower.ExceedsDamageThreshold(400, 99),
            "Hide in Darkness did not trigger at the exact 25% max HP boundary.");
        Require(LanguageFloorHideInDarknessPassivePower.ShadowTurns == 2,
            "Hide in Darkness shadow duration was not 2 player turns.");

        VerifyCobaltIntentDamageDefinitions();
    }

    private static void VerifyCobaltIntentDamageDefinitions()
    {
        LanguageFloorMoveKind[] attackMoves =
        [
            LanguageFloorMoveKind.CobaltCough,
            LanguageFloorMoveKind.CobaltSharpClaws,
            LanguageFloorMoveKind.CobaltWolfComes,
            LanguageFloorMoveKind.BigWolfHorrifyingClaws,
            LanguageFloorMoveKind.BigWolfBloodstainedHunt,
            LanguageFloorMoveKind.BigWolfShadowAssault,
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct,
            LanguageFloorMoveKind.BigWolfRoar
        ];
        foreach (LanguageFloorMoveKind move in attackMoves)
        {
            AbstractIntent intent = LanguageFloorCobaltScar.CreateIntent(move);
            Require(intent is AttackIntent,
                $"{move} did not create an attack intent.");
            var attackIntent = (AttackIntent)intent;
            Require(
                attackIntent.DamageCalc?.Invoke()
                == LanguageFloorCobaltScar.GetMoveDamage(move),
                $"{move} intent damage did not match execution damage.");
            Require(
                attackIntent.Repeats
                == LanguageFloorCobaltScar.GetMoveHitCount(move),
                $"{move} intent hit count did not match execution hit count.");
        }
    }

    private static void VerifyResourcesAndVisuals(LanguageFloorCombatContext fight)
    {
        foreach (string path in fight.Encounter.ExtraAssetPaths ?? [])
        {
            if (ScriptedVisualScenePaths.Contains(path))
            {
                continue;
            }

            Require(ResourceLoader.Exists(path), "Missing language floor resource: " + path);
        }

        string[] phaseTwoPowerIcons =
        [
            "res://images/powers/language_floor_scar_power.png",
            "res://images/powers/language_floor_rip_open_claw_passive_power.png",
            "res://images/powers/language_floor_punish_evil_passive_power.png",
            "res://images/powers/language_floor_destined_big_bad_wolf_passive_power.png",
            "res://images/powers/language_floor_hide_in_darkness_passive_power.png",
            "res://images/powers/language_floor_shadow_ambush_passive_power.png",
            "res://images/powers/language_floor_exhaustion_passive_power.png",
            "res://images/powers/language_floor_shadow_wolf_power.png"
        ];
        foreach (string path in phaseTwoPowerIcons)
        {
            Require(ResourceLoader.Exists(path), "Missing language floor phase-two power icon: " + path);
        }

        Require(LiberationBossRegistry.RegisterLanguageFloorLiberation,
            "Language floor liberation was not enabled in the formal boss pool by default.");
        Require(EncounterBgmController.ResolveLiberationPhaseTrackIndex(1) == 0
                && EncounterBgmController.ResolveLiberationPhaseTrackIndex(2) == 0
                && EncounterBgmController.ResolveLiberationPhaseTrackIndex(3) == 1
                && EncounterBgmController.ResolveLiberationPhaseTrackIndex(4) == 1
                && EncounterBgmController.ResolveLiberationPhaseTrackIndex(5) == 2
                && EncounterBgmController.ResolveLiberationPhaseTrackIndex(6) == 2,
            "Liberation phase BGM mapping was not 1-2/3-4/5-6.");

        var scarletVisuals = fight.ScarletNode.Visuals
            as LanguageFloorScarletScarCreatureVisuals
            ?? throw new InvalidOperationException("Scarlet scripted visuals were not active.");
        var wolfVisuals = fight.WolfNode.Visuals
            as LanguageFloorLostEverythingWolfCreatureVisuals
            ?? throw new InvalidOperationException("Wolf scripted visuals were not active.");

        RequireTriggerTexture(
            scarletVisuals,
            "ShootS1",
            "res://images/monsters/language_floor_liberation/scarlet_scar_s1.png");
        RequireTriggerTexture(
            scarletVisuals,
            "ShootS2",
            "res://images/monsters/language_floor_liberation/scarlet_scar_s2.png");
        RequireTriggerTexture(
            scarletVisuals,
            "ShootS3",
            "res://images/monsters/language_floor_liberation/scarlet_scar_s3.png");
        RequireTriggerTexture(
            scarletVisuals,
            "Hit",
            "res://images/monsters/language_floor_liberation/scarlet_scar_hit.png");
        RequireTriggerTexture(
            wolfVisuals,
            "WolfSlash",
            "res://images/monsters/language_floor_liberation/lost_everything_wolf_attack.png");
        RequireTriggerTexture(
            wolfVisuals,
            "WolfS2",
            "res://images/monsters/language_floor_liberation/lost_everything_wolf_howl.png");
        RequireTriggerTexture(
            wolfVisuals,
            "WolfHowl",
            "res://images/monsters/language_floor_liberation/lost_everything_wolf_howl.png");
        RequireTriggerTexture(
            wolfVisuals,
            "Hit",
            "res://images/monsters/language_floor_liberation/lost_everything_wolf_hit.png");
        RequireBackgroundTexture(LanguageFloorLiberationBackgroundController.NormalTexturePath);
    }

    private static bool IsOrdinaryLanguageFloorIntent(AbstractIntent intent)
    {
        return intent is CombinedTargetedAttackDefendIntent
            or CombinedTargetedAttackDebuffIntent
            or IndiscriminateAttackIntent;
    }

    private static async Task VerifyNonScarletWolfDeath()
    {
        LanguageFloorCombatContext fight = await StartFight("LANGUAGEFLOORVERIFY_UNRELIEVED");
        Creature player = fight.CombatState.PlayerCreatures.Single();
        int damagedHp = Math.Max(1, fight.ScarletCreature.MaxHp / 4);
        await CreatureCmd.SetCurrentHp(fight.ScarletCreature, damagedHp);
        LanguageFloorDeathContext.Record(fight.WolfCreature, player);
        await CreatureCmd.Kill(fight.WolfCreature, force: true);
        await WaitFrames(10);

        Require(!fight.Encounter.PhaseComplete,
            "Non-Scarlet wolf death incorrectly completed phase one.");
        Require(fight.Scarlet.UnrelievedAnger,
            "Non-Scarlet wolf death did not enter Unrelieved Anger.");
        Require(fight.ScarletCreature.CurrentHp
                == Math.Min(
                    fight.ScarletCreature.MaxHp,
                    damagedHp + (int)Math.Ceiling(fight.ScarletCreature.MaxHp * 0.5m)),
            "Unrelieved Anger did not heal 50% max HP.");
        Require(
            RequireLibraryCreature(fight.ScarletCreature, "Scarlet").CurrentChaoValue == 400,
            "Unrelieved Anger did not restore all chao resistance.");

        LanguageFloorUnrelievedAngerPower power =
            fight.ScarletCreature.GetPower<LanguageFloorUnrelievedAngerPower>()
            ?? throw new InvalidOperationException("Unrelieved Anger power was missing.");
        await power.AfterSideTurnStart(
            CombatSide.Enemy,
            fight.CombatState.Enemies,
            fight.CombatState);
        await power.AfterSideTurnStart(
            CombatSide.Enemy,
            fight.CombatState.Enemies,
            fight.CombatState);
        LibraryStrongPower permanentStrong = fight.ScarletCreature
            .GetPowerInstances<LibraryStrongPower>()
            .Single(static strong => strong.AmountPlan.Count == 0);
        Require(permanentStrong.Amount == 8,
            "Unrelieved Anger permanent Strong did not accumulate to 8.");
        Require(
            fight.Scarlet.GetPlannedMove(0) == LanguageFloorMoveKind.IndiscriminateShot,
            "Unrelieved Anger did not force Scarlet slot 1.");
        Log.Info(LogPrefix + "NonScarletWolfDeath phase=1 strong=8");

        CleanupRun();
        await WaitForRunCleanup("unrelieved verifier cleanup");
    }

    private static async Task VerifyScarletKillsWolf()
    {
        LanguageFloorCombatContext fight = await StartFight("LANGUAGEFLOORVERIFY_SCARLET_KILL");
        LanguageFloorDeathContext.Record(fight.WolfCreature, fight.ScarletCreature);
        await CreatureCmd.Kill(fight.WolfCreature, force: true);
        Require(fight.Encounter.CurrentPhase == 2
                && fight.Encounter.TransitionPending
                && fight.CombatState.Enemies.Contains(fight.WolfCreature)
                && fight.WolfCreature.IsDead,
            "Scarlet-killed wolf was not retained as the phase-one transition boss.");
        Require(fight.Wolf.NextMove.Id
                == LanguageFloorLostEverythingWolf.ReviveAndEmpowerMoveId,
            "Scarlet-killed wolf did not expose the revive-and-empower intent.");
        Require(!fight.CombatState.Enemies.Any(
                static enemy => enemy.Monster is LanguageFloorCobaltScar),
            "Phase two spawned before the transition boss performed its turn.");
        await fight.Wolf.PerformMove();
        await WaitUntil(
            () => fight.CombatState.Enemies.Any(
                static enemy => enemy.Monster is LanguageFloorCobaltScar),
            "phase two spawn after Scarlet killed the wolf");
        Require(!fight.Encounter.PhaseComplete && fight.Encounter.CurrentPhase == 2,
            "Scarlet killing the wolf did not enter phase 2.");
        Require(!fight.Encounter.ShouldGiveRewards,
            "Phase-one debug completion incorrectly enabled boss rewards.");
        Log.Info(LogPrefix + "ScarletKillsWolf phase=2");
        CleanupRun();
        await WaitForRunCleanup("Scarlet kill verifier cleanup");
    }

    private static async Task VerifyScarletDeath()
    {
        LanguageFloorCombatContext fight = await StartFight("LANGUAGEFLOORVERIFY_SCARLET_DEATH");
        Creature player = fight.CombatState.PlayerCreatures.Single();
        int hpBefore = player.CurrentHp;
        await CreatureCmd.Kill(fight.ScarletCreature, force: true);
        Require(fight.Encounter.CurrentPhase == 2
                && fight.Encounter.TransitionPending
                && fight.CombatState.Enemies.Contains(fight.ScarletCreature)
                && fight.ScarletCreature.IsDead,
            "Dead Scarlet was not retained as the phase-one transition boss.");
        Require(fight.Scarlet.NextMove.Id
                == LanguageFloorScarletScar.ReviveAndEmpowerMoveId,
            "Dead Scarlet did not expose the revive-and-empower intent.");
        Require(!fight.CombatState.Enemies.Any(
                static enemy => enemy.Monster is LanguageFloorCobaltScar),
            "Phase two spawned before Scarlet performed its transition turn.");
        await fight.Scarlet.PerformMove();
        await WaitUntil(
            () => fight.CombatState.Enemies.Any(
                static enemy => enemy.Monster is LanguageFloorCobaltScar),
            "phase two spawn after Scarlet death");
        Require(!fight.Encounter.PhaseComplete && fight.Encounter.CurrentPhase == 2,
            "Scarlet death did not enter phase 2.");
        Require(
            player.CurrentHp == Math.Max(1, (int)Math.Ceiling(hpBefore * 0.3m)),
            "Scarlet death did not retain 30% player current HP.");
        Log.Info(LogPrefix + "ScarletDeath phase=2 playerHp=" + player.CurrentHp);
        CleanupRun();
        await WaitForRunCleanup("Scarlet death verifier cleanup");
    }

    private static async Task VerifySimultaneousDeath()
    {
        LanguageFloorCombatContext fight = await StartFight("LANGUAGEFLOORVERIFY_SIMULTANEOUS");
        Creature player = fight.CombatState.PlayerCreatures.Single();
        int hpBefore = player.CurrentHp;
        fight.ScarletCreature.LoseHpInternal(
            fight.ScarletCreature.CurrentHp,
            ValueProp.Unblockable);
        fight.WolfCreature.LoseHpInternal(
            fight.WolfCreature.CurrentHp,
            ValueProp.Unblockable);
        await fight.Encounter.ResolvePhaseOneDeath(fight.ScarletCreature, dealer: null);
        Require(!fight.Encounter.PhaseComplete && fight.Encounter.CurrentPhase == 2,
            "Simultaneous death did not enter phase 2.");
        Require(player.CurrentHp == hpBefore,
            "Simultaneous death incorrectly reduced player HP.");
        Log.Info(LogPrefix + "SimultaneousDeath phase=2 playerHpUnchanged=true");
        CleanupRun();
        await WaitForRunCleanup("simultaneous death verifier cleanup");
    }

    private static async Task VerifyPhaseTwoOpeningAndMechanics()
    {
        LanguageFloorPhaseTwoContext fight =
            await StartPhaseTwoFight("LANGUAGEFLOORVERIFY_COBALT");
        Creature player = fight.CombatState.PlayerCreatures.Single();
        Player playerModel = player.Player
            ?? throw new InvalidOperationException("Phase-two player model was missing.");
        LibraryCreature cobaltCreature =
            RequireLibraryCreature(fight.CobaltCreature, "Cobalt Scar");

        Require(fight.Cobalt.Form == LanguageFloorCobaltScarForm.CobaltScar,
            "Cobalt Scar started in the wrong form.");
        Require(fight.CobaltCreature.MaxHp is >= 290 and <= 293,
            "Cobalt Scar max HP outside 290..293 at A0.");
        Require(fight.Cobalt.DefaultChaoResistance == 150,
            "Cobalt Scar base chao resistance was not 150.");
        Require(cobaltCreature.MaxChaoValue == 250
                && cobaltCreature.CurrentChaoValue == 250,
            "Opening swallow did not set and fill 250 chao resistance.");
        RequireAllChaoResistances(cobaltCreature, LibraryResistanceLevel.Fatal);
        Require(fight.Cobalt.SwallowedCards.Count == 3,
            "Opening swallow did not remove exactly three cards.");
        Require(fight.Cobalt.SwallowedOwnerIndexes.SequenceEqual([0, 0, 0]),
            "Opening swallow did not preserve player ownership.");
        Require(playerModel.Deck.Cards.Count == fight.PermanentDeckCount,
            "Opening swallow modified the permanent deck.");
        Require(fight.Cobalt.PlannedMoves.All(
                static move => move == LanguageFloorMoveKind.CobaltWolfComes)
            && fight.CobaltCreature.Monster!.NextMove.Intents.Count == 3,
            "Opening turn was not three Wolf Comes intents.");
        Require(fight.Cobalt.CounterIntentQueue.Count == 6,
            "Three Wolf Comes intents did not create six counter entries.");
        Require(fight.CobaltCreature.GetPower<LanguageFloorRipOpenClawPassivePower>() != null
                && fight.CobaltCreature.GetPower<LanguageFloorPunishEvilPassivePower>() != null
                && fight.CobaltCreature.GetPower<LanguageFloorDestinedBigBadWolfPassivePower>() != null,
            "Cobalt Scar form passives were incomplete.");
        Require(fight.CobaltCreature.GetPower<LanguageFloorHideInDarknessPassivePower>() == null,
            "Cobalt Scar started with Big Bad Wolf passives.");

        SavedProperties saved = CombatStateProperties.From(fight.Cobalt)
            ?? throw new InvalidOperationException("Cobalt SavedProperties were empty.");
        var clone = (LanguageFloorCobaltScar)ModelDb
            .Monster<LanguageFloorCobaltScar>()
            .ToMutable();
        saved.Fill(clone);
        Require(clone.Form == fight.Cobalt.Form
                && clone.PlannedMoves.SequenceEqual(fight.Cobalt.PlannedMoves)
                && clone.SwallowedCards.SequenceEqual(fight.Cobalt.SwallowedCards)
                && clone.SwallowedOwnerIndexes.SequenceEqual(
                    fight.Cobalt.SwallowedOwnerIndexes),
            "Cobalt saved plan or swallowed-card snapshot did not round-trip.");

        var visuals = fight.CobaltNode.Visuals
            as LanguageFloorCobaltScarCreatureVisuals
            ?? throw new InvalidOperationException(
                "Cobalt Scar scripted visuals were not active.");
        RequireTriggerTexture(
            visuals,
            "CobaltStrike",
            "res://images/monsters/language_floor_liberation/cobalt_scar_strike.png");
        RequireTriggerTexture(
            visuals,
            "CobaltSlash",
            "res://images/monsters/language_floor_liberation/cobalt_scar_slash.png");
        RequireTriggerTexture(
            visuals,
            "Hit",
            "res://images/monsters/language_floor_liberation/cobalt_scar_hit.png");

        SerializableCard[] swallowed = fight.Cobalt.SwallowedCards.ToArray();
        CardModel[] handBefore = CardPile.GetCards(playerModel, PileType.Hand).ToArray();
        int transformHp =
            LanguageFloorCobaltScar.TransformHpThreshold(fight.CobaltCreature.MaxHp);
        await CreatureCmd.SetCurrentHp(fight.CobaltCreature, transformHp);
        await fight.Cobalt.BeforeSideTurnStart(
            new BlockingPlayerChoiceContext(),
            CombatSide.Player,
            fight.CombatState.PlayerCreatures,
            fight.CombatState);
        await WaitUntil(
            () => fight.CobaltCreature
                    .GetPower<LanguageFloorPunishEvilPassivePower>() == null
                && fight.CobaltCreature
                    .GetPower<LanguageFloorDestinedBigBadWolfPassivePower>() == null
                && fight.CobaltCreature
                    .GetPower<LanguageFloorHideInDarknessPassivePower>() != null
                && fight.CobaltCreature
                    .GetPower<LanguageFloorShadowAmbushPassivePower>() != null
                && fight.CobaltCreature
                    .GetPower<LanguageFloorExhaustionPassivePower>() != null
                && visuals.CurrentSpriteVariantKey == "big_wolf",
            "Cobalt Scar Big Bad Wolf passive switch");
        if (fight.Cobalt.ForceInstinctNextTurn)
        {
            fight.Cobalt.DebugPlanTurn();
        }

        Require(fight.Cobalt.Form == LanguageFloorCobaltScarForm.BigBadWolf,
            "Cobalt Scar did not transform at 50% HP.");
        Require(fight.CobaltCreature.CurrentHp == transformHp
                && fight.Cobalt.BigWolfEntryHp == transformHp,
            "Spit/transform ordering did not preserve the 50% HP floor. "
            + $"expected={transformHp} current={fight.CobaltCreature.CurrentHp} "
            + $"entry={fight.Cobalt.BigWolfEntryHp}");
        Require(fight.Cobalt.SwallowedCards.Count == 0,
            "Transformation did not spit all swallowed cards.");
        Require(playerModel.Deck.Cards.Count == fight.PermanentDeckCount,
            "Spitting cards modified the permanent deck.");
        CardModel[] handAfter = CardPile.GetCards(playerModel, PileType.Hand).ToArray();
        List<CardModel> newlyPresent = handAfter
            .Where(card => !handBefore.Any(before => ReferenceEquals(before, card)))
            .ToList();
        List<CardModel> returned = [];
        foreach (SerializableCard snapshot in swallowed)
        {
            int matchIndex = newlyPresent.FindIndex(card =>
                card.ToSerializable().Equals(snapshot)
                && (card.EnergyCost.CostsX
                    || card.EnergyCost.GetWithModifiers(CostModifiers.Local)
                        == card.EnergyCost.Canonical + 1));
            Require(matchIndex >= 0,
                "Spitting cards did not return every swallowed card to hand. "
                + $"missing={snapshot.Id?.Entry ?? "<null>"} "
                + $"handBefore={handBefore.Length} handAfter={handAfter.Length} "
                + "swallowed=["
                + string.Join(',', swallowed.Select(static card =>
                    card.Id?.Entry ?? "<null>"))
                + "] handAfter=["
                + string.Join(',', handAfter.Select(static card => card.Id.Entry))
                + "]");
            returned.Add(newlyPresent[matchIndex]);
            newlyPresent.RemoveAt(matchIndex);
        }

        Require(returned.All(static card =>
                card.EnergyCost.CostsX
                || card.EnergyCost.GetWithModifiers(CostModifiers.Local)
                    == card.EnergyCost.Canonical + 1),
            "A spat card did not gain +1 cost for the rest of combat.");
        Require(cobaltCreature.MaxChaoValue == 250
                && cobaltCreature.CurrentChaoValue == 250,
            "Big Bad Wolf transformation did not set and fill 250 chao.");
        RequireAllChaoResistances(cobaltCreature, LibraryResistanceLevel.Resist);
        Require(fight.Cobalt.PlannedMoves[0]
                == LanguageFloorMoveKind.BigWolfUncontrollableInstinct,
            "Transformation turn did not force Uncontrollable Instinct.");
        Require(fight.CobaltCreature.GetPower<LanguageFloorPunishEvilPassivePower>() == null
                && fight.CobaltCreature.GetPower<LanguageFloorDestinedBigBadWolfPassivePower>() == null
                && fight.CobaltCreature.GetPower<LanguageFloorHideInDarknessPassivePower>() != null
                && fight.CobaltCreature.GetPower<LanguageFloorShadowAmbushPassivePower>() != null
                && fight.CobaltCreature.GetPower<LanguageFloorExhaustionPassivePower>() != null,
            "Transformation did not switch to the Big Bad Wolf passive set. powers=["
            + string.Join(
                ',',
                fight.CobaltCreature.Powers.Select(static power =>
                    power.GetType().Name))
            + "]");
        RequireTriggerTexture(
            visuals,
            "BigWolfStrike",
            "res://images/monsters/language_floor_liberation/cobalt_scar_big_wolf_strike.png");
        RequireTriggerTexture(
            visuals,
            "BigWolfSlash",
            "res://images/monsters/language_floor_liberation/cobalt_scar_big_wolf_slash.png");
        RequireTriggerTexture(
            visuals,
            "BigWolfS1",
            "res://images/monsters/language_floor_liberation/cobalt_scar_big_wolf_s1.png");
        RequireTriggerTexture(
            visuals,
            "BigWolfS2",
            "res://images/monsters/language_floor_liberation/cobalt_scar_big_wolf_s2.png");

        await VerifyPhaseTwoMoveDefinitions(fight, player);
        await VerifyPhaseTwoScarRules(fight, player);
        await VerifyPhaseTwoShadowCycle(fight, player);

        CleanupRun();
        await WaitForRunCleanup("phase-two mechanics verifier cleanup");

        LanguageFloorPhaseTwoContext ascended =
            await StartPhaseTwoFight(
                "LANGUAGEFLOORVERIFY_COBALT_ASCENDED",
                (int)AscensionLevel.ToughEnemies);
        Require(ascended.CobaltCreature.MaxHp is >= 297 and <= 300,
            "Cobalt Scar max HP outside 297..300 at Tough Enemies.");
        int handCount = CardPile.GetCards(
            ascended.CombatState.Players.Single(),
            PileType.Hand).Count();
        await ascended.Cobalt.BeforeSideTurnStart(
            new BlockingPlayerChoiceContext(),
            CombatSide.Player,
            ascended.CombatState.PlayerCreatures,
            ascended.CombatState);
        Require(ascended.Cobalt.SwallowWindowActive
                && ascended.Cobalt.PlayerTurnsSinceSwallow == 1
                && ascended.Cobalt.SwallowedCards.Count == 3,
            "Punish Evil did not remain active for the full player turn.");
        Require(
            RequireLibraryCreature(
                ascended.CobaltCreature,
                "Ascended Cobalt Scar during swallow window").MaxChaoValue == 250,
            "Punish Evil restored chao before the full player turn ended.");
        await ascended.Cobalt.AfterSideTurnEnd(
            new BlockingPlayerChoiceContext(),
            CombatSide.Player,
            ascended.CombatState.PlayerCreatures);
        Require(ascended.Cobalt.SwallowedCards.Count == 0,
            "Unstaggered swallowed cards were not consumed after the full player turn.");
        Require(CardPile.GetCards(
                ascended.CombatState.Players.Single(),
                PileType.Hand).Count() == handCount,
            "Consumed swallowed cards were incorrectly returned to hand.");
        LibraryCreature ascendedCreature =
            RequireLibraryCreature(ascended.CobaltCreature, "Ascended Cobalt Scar");
        Require(ascendedCreature.MaxChaoValue == 150
                && ascendedCreature.CurrentChaoValue == 150,
            "Card consumption did not restore 150 chao resistance.");
        Require(
            ascendedCreature.GetChaosResistanceLevel(LibraryDamageType.Slash)
                == LibraryResistanceLevel.Endure
            && ascendedCreature.GetChaosResistanceLevel(LibraryDamageType.Pierce)
                == LibraryResistanceLevel.Endure
            && ascendedCreature.GetChaosResistanceLevel(LibraryDamageType.Blunt)
                == LibraryResistanceLevel.Normal,
            "Card consumption did not restore Cobalt Scar base chao resistances.");

        CleanupRun();
        await WaitForRunCleanup("phase-two ascension verifier cleanup");
    }

    private static async Task VerifyPhaseTwoMoveDefinitions(
        LanguageFloorPhaseTwoContext fight,
        Creature player)
    {
        VerifyCombinedIntentContracts(fight.CobaltCreature, player);

        Require(LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.CobaltCough) == 4
                && LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.CobaltSharpClaws) == 4
                && LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.CobaltWolfComes) == 5
                && LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.BigWolfHorrifyingClaws) == 6
                && LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.BigWolfBloodstainedHunt) == 10
                && LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.BigWolfShadowAssault) == 11
                && LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.BigWolfUncontrollableInstinct) == 2
                && LanguageFloorCobaltScar.GetMoveDamage(
                    LanguageFloorMoveKind.BigWolfRoar) == 14,
            "A phase-two A0 attack value was incorrect.");

        if (player.GetPower<LibraryBleedingPower>() is { } oldBleed)
        {
            await PowerCmd.Remove(oldBleed);
        }
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        int horrifyingClawsHpBefore = player.CurrentHp;
        int horrifyingClawsBlockBefore = fight.CobaltCreature.Block;
        await fight.Cobalt.DebugPerformMove(
            LanguageFloorMoveKind.BigWolfHorrifyingClaws);
        Require(
            fight.CobaltCreature.Block
                == horrifyingClawsBlockBefore + 9,
            "Horrifying Claws did not grant nine Block.");
        Require(
            player.GetPower<LibraryBleedingPower>()?.Amount == 2,
            "Horrifying Claws did not apply two Bleed after unblocked damage. "
            + $"hpDelta={horrifyingClawsHpBefore - player.CurrentHp} "
            + $"block={player.Block} powers=["
            + string.Join(
                ',',
                player.Powers.Select(static power =>
                    $"{power.GetType().Name}:{power.Amount}"))
            + "]");
        if (player.GetPower<LibraryBleedingPower>() is { } horrifyingClawsBleed)
        {
            await PowerCmd.Remove(horrifyingClawsBleed);
        }

        int blockBefore = fight.CobaltCreature.Block;
        await fight.Cobalt.DebugPerformMove(
            LanguageFloorMoveKind.BigWolfBrutalFangs);
        Require(fight.CobaltCreature.Block == blockBefore + 18,
            "Brutal Fangs did not grant 18 Block.");
        Require(player.GetPower<StrengthPower>()?.Amount == -1
                && player.GetPower<DexterityPower>()?.Amount == -1,
            "Brutal Fangs did not permanently reduce Strength and Dexterity by 1.");

        if (player.GetPower<LanguageFloorScarPower>() is { } oldScar)
        {
            await PowerCmd.Remove(oldScar);
        }
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await fight.Cobalt.DebugPerformMove(
            LanguageFloorMoveKind.BigWolfBloodstainedHunt);
        Require(player.GetPower<LanguageFloorScarPower>()?.Amount == 3,
            "Bloodstained Hunt did not leave exactly three Scar.");
        Require(player.GetPower<LibraryOfRuinaDrawCardsNextTurnPower>()?.Amount == 1,
            "Bloodstained Hunt did not apply one fewer draw next turn.");

        if (player.GetPower<LanguageFloorScarPower>() is { } huntScar)
        {
            await PowerCmd.Remove(huntScar);
        }
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        int hpBeforeInstinct = player.CurrentHp;
        await fight.Cobalt.DebugPerformMove(
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct);
        Require(hpBeforeInstinct - player.CurrentHp == 22,
            "Uncontrollable Instinct did not execute five hits with live Scar scaling.");

        await PowerCmdCompat.Apply<StrengthPower>(
            player,
            10,
            fight.CobaltCreature,
            null);
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await fight.Cobalt.DebugPerformMove(LanguageFloorMoveKind.BigWolfRoar);
        Require(player.GetPower<StrengthPower>() == null,
            "Roar did not remove the target's visible positive power.");
        Require(player.GetPower<LanguageFloorLiberationControllerPower>() != null,
            "Roar removed the hidden liberation controller.");
    }

    private static void VerifyCombinedIntentContracts(
        Creature owner,
        Creature target)
    {
        IntentBadge bleed = IntentBadge.Bleed(2);
        IntentBadge weak = IntentBadge.Weak(1);
        var zeroEffects = new CombinedMagicIntent();
        var singleEffect = new CombinedMagicIntent(null, false, bleed);
        var multipleEffects = new CombinedMagicIntent(null, false, bleed, weak);
        var legacyBadged = new BadgedAttackIntent(1, bleed);

        Require(IntentEffectCollection.Get(zeroEffects).Count == 0,
            "Combined intent did not preserve zero effects.");
        Require(IntentEffectCollection.Get(singleEffect).SequenceEqual([bleed]),
            "Combined intent did not preserve one effect.");
        Require(IntentEffectCollection.Get(multipleEffects).SequenceEqual([bleed, weak]),
            "Combined intent did not preserve multiple effects in order.");
        Require(IntentEffectCollection.Get(legacyBadged).SequenceEqual([bleed]),
            "Legacy badged intent fallback did not expose its effects.");

        var ordinary = new CombinedAttackDefendIntent(6, 3, null, 9, bleed);
        var group = new CombinedAttackDefendIntent(
            6,
            3,
            null,
            9,
            IndiscriminateAttackIntent.CreateGroupAttackBadge());
        Require(!ordinary.IsGroupAttack && group.IsGroupAttack,
            "Combined group-attack semantics did not come from IGroupAttackIntent.");

        var targeted = new CombinedTargetedAttackDefendIntent(
            6,
            3,
            "LANGUAGE_FLOOR_BIG_WOLF_HORRIFYING_CLAWS.description",
            null,
            false,
            _ => target,
            9,
            bleed);
        IReadOnlyList<IntentTargetLineTarget> targetLines =
            targeted.GetIntentTargetLineTargets(owner, []);
        Require(targetLines.Count == 1
                && ReferenceEquals(targetLines[0].Target, target),
            "Combined targeted attack-defend intent did not resolve its explicit target.");

        string description = targeted.GetHoverTip([target], owner).Description;
        Require(!description.Contains("{Damage}", StringComparison.Ordinal)
                && !description.Contains("{Repeat}", StringComparison.Ordinal)
                && !description.Contains("{BlockAmount}", StringComparison.Ordinal)
                && !description.Contains("{BadgeAmount}", StringComparison.Ordinal),
            "Combined attack-defend description left an unresolved variable.");

        ICombinedIntentHoverIcon[] hoverIntents =
        [
            zeroEffects,
            new CombinedDefendDebuffIntent(),
            ordinary
        ];
        foreach (ICombinedIntentHoverIcon hoverIntent in hoverIntents)
        {
            Require(ResourceLoader.Exists(hoverIntent.HoverIconPath),
                "Missing combined intent hover icon: " + hoverIntent.HoverIconPath);
        }
    }

    private static async Task VerifyPhaseTwoScarRules(
        LanguageFloorPhaseTwoContext fight,
        Creature player)
    {
        if (player.GetPower<LanguageFloorScarPower>() is { } oldScar)
        {
            await PowerCmd.Remove(oldScar);
        }

        LanguageFloorRipOpenClawPassivePower claw =
            fight.CobaltCreature.GetPower<LanguageFloorRipOpenClawPassivePower>()
            ?? throw new InvalidOperationException("Rip Open Claw was missing.");
        var blocked = new DamageResult(player, ValueProp.Move)
        {
            BlockedDamage = 1,
            UnblockedDamage = 0,
            WasFullyBlocked = true
        };
        await claw.AfterDamageGiven(
            new BlockingPlayerChoiceContext(),
            fight.CobaltCreature,
            blocked,
            ValueProp.Move,
            player,
            null);
        Require(player.GetPower<LanguageFloorScarPower>() == null,
            "Rip Open Claw applied Scar on a fully blocked hit.");

        var hpDamage = new DamageResult(player, ValueProp.Move)
        {
            UnblockedDamage = 1
        };
        await claw.AfterDamageGiven(
            new BlockingPlayerChoiceContext(),
            fight.CobaltCreature,
            hpDamage,
            ValueProp.Move,
            player,
            null);
        Require(player.GetPower<LanguageFloorScarPower>()?.Amount == 1,
            "Rip Open Claw did not apply one Scar per hit.");

        LanguageFloorScarPower scar =
            await PowerCmdCompat.SetAmount<LanguageFloorScarPower>(
                player,
                3,
                fight.CobaltCreature,
                null)
            ?? throw new InvalidOperationException("Scar was not set to three.");
        Require(scar.ModifyDamageAdditiveCompat(
                player,
                10,
                ValueProp.Move,
                fight.CobaltCreature,
                null,
                null) == 3,
            "Three Scar did not add three damage for Cobalt Scar.");
        scar = await PowerCmdCompat.SetAmount<LanguageFloorScarPower>(
            player,
            4,
            fight.CobaltCreature,
            null)
            ?? throw new InvalidOperationException("Scar was not set to four.");
        Require(scar.ModifyDamageAdditiveCompat(
                player,
                10,
                ValueProp.Move,
                fight.CobaltCreature,
                null,
                null) == 6,
            "Four Scar did not add six damage for Cobalt Scar.");
    }

    private static async Task VerifyPhaseTwoShadowCycle(
        LanguageFloorPhaseTwoContext fight,
        Creature player)
    {
        if (player.GetPower<LanguageFloorScarPower>() is { } scar)
        {
            await PowerCmd.Remove(scar);
        }

        int threshold = fight.Cobalt.GetShadowDamageThreshold();
        Require(LanguageFloorCobaltScar.ReachesShadowThreshold(
                fight.Cobalt.BigWolfEntryHp,
                threshold)
            && !LanguageFloorCobaltScar.ReachesShadowThreshold(
                fight.Cobalt.BigWolfEntryHp,
                threshold - 1),
            "Shadow threshold did not trigger at the exact 25% boundary.");

        fight.Cobalt.DebugSetState(
            LanguageFloorCobaltScarForm.BigBadWolf,
            fight.Cobalt.BigWolfEntryHp,
            0,
            0,
            LanguageFloorMoveKind.BigWolfHorrifyingClaws,
            LanguageFloorMoveKind.BigWolfBrutalFangs,
            LanguageFloorMoveKind.BigWolfBloodstainedHunt);
        await fight.Cobalt.AfterDamageReceived(
            new BlockingPlayerChoiceContext(),
            fight.CobaltCreature,
            new DamageResult(fight.CobaltCreature, ValueProp.Move)
            {
                UnblockedDamage = threshold - 1
            },
            ValueProp.Move,
            player,
            null);
        await fight.Cobalt.AfterDamageReceived(
            new BlockingPlayerChoiceContext(),
            fight.CobaltCreature,
            new DamageResult(fight.CobaltCreature, ValueProp.Move)
            {
                UnblockedDamage = 1
            },
            ValueProp.Move,
            player,
            null);
        Require(fight.Cobalt.AccumulatedDamage == threshold,
            "Damage did not accumulate across the player/enemy turn window.");
        await fight.Cobalt.AfterSideTurnEnd(
            new BlockingPlayerChoiceContext(),
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        fight.CobaltCreature.PrepareForNextTurn(
            fight.CombatState.PlayerCreatures);
        Require(fight.Cobalt.ShadowTurnsRemaining == 2
                && fight.Cobalt.AccumulatedDamage == 0,
            "Exact threshold did not enter two-turn Shadow Wolf.");
        Require(fight.Cobalt.PlannedMoves.SequenceEqual(
                [
                    LanguageFloorMoveKind.BigWolfShadowAssault,
                    LanguageFloorMoveKind.BigWolfShadowAssault
                ])
            && fight.CobaltCreature.Monster!.NextMove.Intents.Count == 2,
            "Shadow Wolf did not use exactly two Shadow Assault intents. "
            + $"shadowTurns={fight.Cobalt.ShadowTurnsRemaining} "
            + $"planned=[{string.Join(',', fight.Cobalt.PlannedMoves)}] "
            + $"nextMove={fight.CobaltCreature.Monster!.NextMove.Id} "
            + $"intentCount={fight.CobaltCreature.Monster.NextMove.Intents.Count} "
            + $"shadowPower={fight.CobaltCreature.GetPower<LanguageFloorShadowWolfPower>()?.Amount}");
        Require(LanguageFloorShadowAmbushPassivePower.CurrentIntentCardLimit(
                fight.CobaltCreature) == 2,
            "Shadow Ambush did not limit each player to two cards.");
        await VerifyShadowAmbushCardLimit(fight, player);
        int accumulatedBefore = fight.Cobalt.AccumulatedDamage;
        await fight.Cobalt.AfterDamageReceived(
            new BlockingPlayerChoiceContext(),
            fight.CobaltCreature,
            new DamageResult(fight.CobaltCreature, ValueProp.Move)
            {
                UnblockedDamage = 50
            },
            ValueProp.Move,
            player,
            null);
        Require(fight.Cobalt.AccumulatedDamage == accumulatedBefore,
            "Shadow Wolf incorrectly accumulated incoming damage.");

        await fight.Cobalt.AfterSideTurnEnd(
            new BlockingPlayerChoiceContext(),
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        Require(fight.Cobalt.ShadowTurnsRemaining == 1,
            "Shadow Wolf did not retain one enemy turn after its first turn.");
        await fight.Cobalt.AfterSideTurnEnd(
            new BlockingPlayerChoiceContext(),
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        Require(fight.Cobalt.ShadowReleasePending,
            "Shadow Wolf did not schedule release after its second enemy turn.");
        await fight.Cobalt.BeforeSideTurnStart(
            new BlockingPlayerChoiceContext(),
            CombatSide.Player,
            fight.CombatState.PlayerCreatures,
            fight.CombatState);
        fight.Cobalt.DebugPlanTurn();
        Require(!fight.Cobalt.ShadowReleasePending
                && fight.Cobalt.ShadowTurnsRemaining == 0
                && fight.Cobalt.PlannedMoves[0]
                    == LanguageFloorMoveKind.BigWolfRoar,
            "Shadow release did not force Roar on the next enemy turn.");
        Require(fight.CobaltCreature.Monster!.NextMove.Intents[0]
                    is IndiscriminateAttackIntent roar
                && roar.Badges.Any(static badge =>
                    badge.Kind == IntentBadgeKind.Custom
                    && badge.IconPath.EndsWith(
                        IndiscriminateAttackIntent.GroupAttackBadgeImagePath,
                        StringComparison.Ordinal)),
            "Roar did not display its group-attack intent badge.");
        Require(fight.Cobalt.CounterIntentQueue.Count == 0,
            "Roar retained a stale counter intent from the prior move.");
        RequireAllChaoResistances(
            RequireLibraryCreature(fight.CobaltCreature, "Released Big Bad Wolf"),
            LibraryResistanceLevel.Fatal);

        var visuals = fight.CobaltNode.Visuals
            as LanguageFloorCobaltScarCreatureVisuals
            ?? throw new InvalidOperationException(
                "Cobalt Scar visuals disappeared.");
        fight.Cobalt.DebugSetState(
            LanguageFloorCobaltScarForm.BigBadWolf,
            fight.Cobalt.BigWolfEntryHp,
            0,
            1,
            LanguageFloorMoveKind.BigWolfShadowAssault,
            LanguageFloorMoveKind.BigWolfShadowAssault);
        visuals.SetForm(LanguageFloorCobaltScarForm.BigBadWolf, shadow: true);
        RequireTriggerTexture(
            visuals,
            "ShadowAssault",
            "res://images/monsters/language_floor_liberation/lost_everything_wolf_attack.png");
    }

    private static async Task VerifyShadowAmbushCardLimit(
        LanguageFloorPhaseTwoContext fight,
        Creature player)
    {
        LanguageFloorShadowAmbushPassivePower ambush = fight.CobaltCreature
            .GetPower<LanguageFloorShadowAmbushPassivePower>()
            ?? throw new InvalidOperationException(
                "Big Bad Wolf was missing Shadow Ambush.");
        ambush.RefreshCardLimit();
        int cardLimit = fight.CobaltCreature.Monster!.NextMove.Intents.Count;
        Require(ambush.DynamicVars["CardLimit"].IntValue == cardLimit,
            "Shadow Ambush CardLimit did not match the current intent count.");

        Player playerModel = player.Player
            ?? throw new InvalidOperationException(
                "Shadow Ambush verifier player model was missing.");
        CardModel testCard = CardPile.GetCards(
                playerModel,
                PileType.Hand,
                PileType.Draw,
                PileType.Discard)
            .First(card => !card.Keywords.Contains(CardKeyword.Unplayable));
        CombatSide previousSide = fight.CombatState.CurrentSide;
        CombatManager.Instance.History.Clear();
        fight.CombatState.CurrentSide = CombatSide.Player;
        fight.Cobalt.ResetShadowCardCounters();
        try
        {
            for (int i = 0; i < cardLimit; i++)
            {
                Require(Hook.ShouldPlay(
                        fight.CombatState,
                        testCard,
                        out _,
                        AutoPlayType.None),
                    "Shadow Ambush blocked a card before reaching its limit.");
                await Hook.BeforeCardPlayed(
                    fight.CombatState,
                    VerificationApi.CreateCardPlay(testCard, playerModel));
            }

            Require(!Hook.ShouldPlay(
                        fight.CombatState,
                        testCard,
                        out AbstractModel? preventer,
                        AutoPlayType.None)
                    && preventer is LanguageFloorShadowWolfPower,
                "Shadow Ambush did not block the next card at its limit. "
                + $"cardsPlayed={fight.Cobalt.GetShadowCardsPlayed(playerModel)} "
                + $"preventer={preventer?.GetType().Name}");
            Require(!testCard.CanPlay(
                        out UnplayableReason reason,
                        out AbstractModel? canPlayPreventer)
                    && reason.HasFlag(UnplayableReason.BlockedByHook)
                    && canPlayPreventer is LanguageFloorShadowWolfPower,
                "CardModel.CanPlay did not expose the active Shadow Wolf limiter "
                + "as the original BlockedByHook preventer.");

            LanguageFloorShadowWolfPower shadowWolf = fight.CobaltCreature
                .GetPower<LanguageFloorShadowWolfPower>()
                ?? throw new InvalidOperationException(
                    "Big Bad Wolf was missing the active Shadow Wolf limiter.");
            await shadowWolf.BeforeSideTurnStart(
                new BlockingPlayerChoiceContext(),
                CombatSide.Player,
                fight.CombatState.PlayerCreatures,
                fight.CombatState);
            Require(fight.Cobalt.GetShadowCardsPlayed(playerModel) == 0,
                "Shadow Ambush did not reset its per-player counter at player-turn start.");

            SavedProperties savedCounters = CombatStateProperties.From(fight.Cobalt)
                ?? throw new InvalidOperationException(
                    "Shadow Ambush counters produced no SavedProperties.");
            var counterClone = (LanguageFloorCobaltScar)ModelDb
                .Monster<LanguageFloorCobaltScar>()
                .ToMutable();
            savedCounters.Fill(counterClone);
            Require(counterClone.ShadowCardsPlayedByPlayer.SequenceEqual(
                    fight.Cobalt.ShadowCardsPlayedByPlayer),
                "Shadow Ambush per-player card counts did not survive save/load.");
        }
        finally
        {
            fight.Cobalt.ResetShadowCardCounters();
            CombatManager.Instance.History.Clear();
            fight.CombatState.CurrentSide = previousSide;
        }
    }

    private static async Task VerifyPhaseTwoDeath()
    {
        LanguageFloorPhaseTwoContext fight =
            await StartPhaseTwoFight("LANGUAGEFLOORVERIFY_COBALT_DEATH");
        await fight.Cobalt.DebugTransformToBigBadWolf();
        await fight.Encounter.OnBeforeSideTurnStart(
            CombatSide.Player,
            fight.CombatState);
        Require(fight.CombatState.PlayerCreatures.All(static player =>
                player.HasPower<LanguageFloorLiberationControllerPower>()),
            "Language-floor transition controller was missing before phase-two death.");
        Require(!Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(
                fight.CombatState,
                fight.CobaltCreature),
            "Language-floor transition controller did not retain the active phase-two boss.");
        await CreatureCmd.Kill(fight.CobaltCreature, force: true);
        Require(fight.Encounter.CurrentPhase == 3
                && fight.Encounter.TransitionPending
                && fight.CombatState.Enemies.Contains(fight.CobaltCreature)
                && fight.CobaltCreature.IsDead,
            "Cobalt Scar was not retained as the phase-two transition boss. "
            + $"phase={fight.Encounter.CurrentPhase} "
            + $"pending={fight.Encounter.TransitionPending} "
            + $"contained={fight.CombatState.Enemies.Contains(fight.CobaltCreature)} "
            + $"dead={fight.CobaltCreature.IsDead}");
        Require(fight.Cobalt.NextMove.Id
                == LanguageFloorCobaltScar.ReviveAndEmpowerMoveId,
            "Cobalt Scar did not expose the revive-and-empower intent.");
        Require(!fight.CombatState.Enemies.Any(
                static enemy => enemy.Monster is LanguageFloorSmilingFace),
            "Phase three spawned before Cobalt Scar performed its transition turn.");
        await fight.Cobalt.PerformMove();
        await WaitUntil(
            () => fight.CombatState.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<LanguageFloorSmilingFace>()
                .Any(),
            "Smiling Face phase-three spawn");
        Require(!fight.Encounter.PhaseComplete
                && fight.Encounter.CurrentPhase == 3
                && !fight.Encounter.TransitionPending,
            "Cobalt Scar death did not enter the active third phase.");
        CleanupRun();
        await WaitForRunCleanup("phase-two death verifier cleanup");
    }

    private static void RequireAllChaoResistances(
        LibraryCreature creature,
        LibraryResistanceLevel expected)
    {
        foreach (LibraryDamageType type in new[]
                 {
                     LibraryDamageType.Slash,
                     LibraryDamageType.Pierce,
                     LibraryDamageType.Blunt
                 })
        {
            Require(creature.GetChaosResistanceLevel(type) == expected,
                "Chao resistance mismatch for " + type + ".");
        }
    }

    private sealed record LanguageFloorCombatContext(
        CombatState CombatState,
        LanguageFloorLiberationEncounter Encounter,
        LanguageFloorScarletScar Scarlet,
        Creature ScarletCreature,
        NCreature ScarletNode,
        LanguageFloorLostEverythingWolf Wolf,
        Creature WolfCreature,
        NCreature WolfNode);

    private sealed record LanguageFloorPhaseTwoContext(
        CombatState CombatState,
        LanguageFloorLiberationEncounter Encounter,
        LanguageFloorCobaltScar Cobalt,
        Creature CobaltCreature,
        NCreature CobaltNode,
        int PermanentDeckCount);

    private static async Task<LanguageFloorCombatContext> StartFight(string seed)
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
        if (RunManager.Instance.DebugOnlyGetState()?.Players.Single().GetRelic<BurningBlood>()
            is { } burningBlood)
        {
            await RelicCmd.Remove(burningBlood);
        }

        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            ModelDb.Encounter<LanguageFloorLiberationEncounter>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "LanguageFloorLiberation combat start");
        await WaitFrames(5);

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var encounter = combatState.Encounter as LanguageFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "LanguageFloorLiberationEncounter was not active.");
        LanguageFloorScarletScar scarlet = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorScarletScar>()
            .Single();
        LanguageFloorLostEverythingWolf wolf = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorLostEverythingWolf>()
            .Single();
        NCreature scarletNode = NCombatRoom.Instance!.GetCreatureNode(scarlet.Creature)
            ?? throw new InvalidOperationException("Scarlet node was missing.");
        NCreature wolfNode = NCombatRoom.Instance.GetCreatureNode(wolf.Creature)
            ?? throw new InvalidOperationException("Wolf node was missing.");
        return new LanguageFloorCombatContext(
            combatState,
            encounter,
            scarlet,
            scarlet.Creature,
            scarletNode,
            wolf,
            wolf.Creature,
            wolfNode);
    }

    private static async Task<LanguageFloorPhaseTwoContext> StartPhaseTwoFight(
        string seed,
        int ascensionLevel = 0)
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
            ascensionLevel);
        Player runPlayer = RunManager.Instance.DebugOnlyGetState()?.Players.Single()
            ?? throw new InvalidOperationException("Run player was missing.");
        if (runPlayer.GetRelic<BurningBlood>() is { } burningBlood)
        {
            await RelicCmd.Remove(burningBlood);
        }

        int permanentDeckCount = runPlayer.Deck.Cards.Count;
        await RunManager.Instance.EnterAct(1, doTransition: false);
        var phaseTwoEncounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        phaseTwoEncounter.LoadCustomState(
            new Dictionary<string, string>
            {
                ["CurrentPhase"] = "2",
                ["PhaseComplete"] = "False",
                ["TransitionPending"] = "False"
            });
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            phaseTwoEncounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "LanguageFloorLiberation phase-two combat start");
        await WaitUntil(
            static () => CombatManager.Instance.DebugOnlyGetState()?.Enemies
                .Select(enemy => enemy.Monster)
                .OfType<LanguageFloorCobaltScar>()
                .Any(static cobalt => cobalt.OpeningResolved) == true,
            "Cobalt Scar opening swallow");
        await WaitUntil(
            static () => CombatManager.Instance.DebugOnlyGetState() is
                {
                    CurrentSide: CombatSide.Player
                } state
                && state.Players.All(static player =>
                    player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
            "LanguageFloorLiberation phase-two player turn ready");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Phase-two combat state is null.");
        var encounter = combatState.Encounter as LanguageFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "LanguageFloorLiberationEncounter phase two was not active.");
        LanguageFloorCobaltScar cobalt = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorCobaltScar>()
            .Single();
        NCreature cobaltNode = NCombatRoom.Instance!.GetCreatureNode(cobalt.Creature)
            ?? throw new InvalidOperationException("Cobalt Scar node was missing.");
        return new LanguageFloorPhaseTwoContext(
            combatState,
            encounter,
            cobalt,
            cobalt.Creature,
            cobaltNode,
            permanentDeckCount);
    }

    private static void RequireTriggerTexture(
        SpriteAttackCreatureVisuals visuals,
        string trigger,
        string expectedPath)
    {
        Require(visuals.TryPlayTrigger(trigger), "Visual trigger failed: " + trigger);
        Sprite2D attack = visuals.GetNode<Sprite2D>("%AttackVisuals");
        Require(
            attack.Texture?.ResourcePath == expectedPath,
            trigger + " texture mismatch: " + (attack.Texture?.ResourcePath ?? "<null>"));
    }

    private static void RequireBackgroundTexture(string expectedPath)
    {
        TextureRect? image = NCombatRoom.Instance?.Background
            .GetNodeOrNull<TextureRect>("%LittleRedMercenaryBackgroundImage")
            ?? NCombatRoom.Instance?.Background.FindChild(
                "LittleRedMercenaryBackgroundImage",
                recursive: true,
                owned: false) as TextureRect;
        Require(image?.Texture?.ResourcePath == expectedPath,
            "Background texture mismatch: " + (image?.Texture?.ResourcePath ?? "<null>"));
    }

    private static LibraryStrongPower RequireTemporaryStrong(
        Creature creature,
        int expectedAmount)
    {
        LibraryStrongPower power = creature
            .GetPowerInstances<LibraryStrongPower>()
            .Single(static strong => strong.TurnsRemaining > 0);
        Require(power.Amount == expectedAmount,
            "Temporary Strong amount mismatch: " + power.Amount);
        Require(power.TurnsRemaining == 1,
            "Temporary Strong duration was not 1.");
        return power;
    }

    private static LibraryCreature RequireLibraryCreature(
        Creature creature,
        string label)
    {
        return creature as LibraryCreature
            ?? throw new InvalidOperationException(label + " was not a LibraryCreature.");
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

            await WaitFrames(1);
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
    }

    private static async Task WaitFrames(int frameCount)
    {
        SceneTree tree = NGame.Instance?.GetTree()
            ?? throw new InvalidOperationException("SceneTree is unavailable.");
        for (int frame = 0; frame < frameCount; frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
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
