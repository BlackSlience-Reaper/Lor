using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
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

internal static class LanguageFloorLiberationPhaseThreeVerificationPatch
{
    private const string VerifyArg = "lor-verify-language-floor-phase3";
    private const string VerifyTransitionArg =
        "lor-verify-language-floor-phase3-transition";
    private const string VerifyLethalArg =
        "lor-verify-language-floor-phase3-lethal";
    private const string LogPrefix = "[LibraryOfRuina.LanguageFloor.Phase3.Verify] ";
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
        || CommandLineHelper.HasArg(VerifyTransitionArg)
        || CommandLineHelper.HasArg(VerifyLethalArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyArg,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                arg.TrimStart('-'),
                VerifyTransitionArg,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                arg.TrimStart('-'),
                VerifyLethalArg,
                StringComparison.OrdinalIgnoreCase));

    private static bool HasLethalVerifyArg() =>
        CommandLineHelper.HasArg(VerifyLethalArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyLethalArg,
                StringComparison.OrdinalIgnoreCase));

    private static bool HasTransitionVerifyArg() =>
        CommandLineHelper.HasArg(VerifyTransitionArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyTransitionArg,
                StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            if (HasLethalVerifyArg())
            {
                PhaseThreeContext lethalFight =
                    await StartPhaseThreeFight(
                        "LANGUAGEFLOORVERIFY_SMILING_FACE_LETHAL",
                        expectedInitialForm:
                            LanguageFloorSmilingFaceForm.Third);
                await VerifyLethalFakeDeathHold(lethalFight);
                Log.Info(LogPrefix
                    + "LANGUAGE_FLOOR_PHASE3_LETHAL_OK");
                CleanupRun();
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasTransitionVerifyArg())
            {
                PhaseThreeContext transitionFight =
                    await StartPhaseThreeFight(
                        "LANGUAGEFLOORVERIFY_SMILING_FACE_TRANSITION",
                        expectedInitialForm: null);
                if (transitionFight.Boss.Form
                    != LanguageFloorSmilingFaceForm.First)
                {
                    await transitionFight.Boss.DebugTransitionToForm(
                        LanguageFloorSmilingFaceForm.First);
                }
                await VerifyCorpseTrialSuccess(transitionFight);
                Log.Info(LogPrefix
                    + "LANGUAGE_FLOOR_PHASE3_TRANSITION_OK");
                CleanupRun();
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyStaticDefinitionsAndSchema();
            await VerifyPhaseTwoTransition();
            await VerifyToughEnemyStats();
            PhaseThreeContext fight = await StartPhaseThreeFight(
                "LANGUAGEFLOORVERIFY_SMILING_FACE");
            VerifyOpeningStatsAndResources(fight);
            VerifyMovePlanning(fight);
            await VerifyMovesSpawnsAndBacklog(fight);
            await VerifyDowngradesPromotionsAndSave(fight);
            await VerifyHealingAndRot(fight);
            await VerifyCorpseTrialFailure(fight);
            await VerifyCorpseTrialSuccess(fight);
            Log.Info(LogPrefix + "LANGUAGE_FLOOR_PHASE3_OK");
            CleanupRun();
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LANGUAGE_FLOOR_PHASE3_FAIL\n" + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStaticDefinitionsAndSchema()
    {
        Require(LanguageFloorSmilingFace.GetMoveDamage(
                LanguageFloorSmilingFaceMove.Devour) == 8,
            "Devour damage was not 8 at A0.");
        Require(LanguageFloorSmilingFace.GetMoveDamage(
                LanguageFloorSmilingFaceMove.Absorb) == 17,
            "Absorb damage was not 17 at A0.");
        Require(LanguageFloorSmilingFace.GetMoveDamage(
                LanguageFloorSmilingFaceMove.Sit) == 6,
            "Sit damage was not 6 at A0.");
        Require(LanguageFloorSmilingFace.GetMoveDamage(
                LanguageFloorSmilingFaceMove.Scream) == 7,
            "Scream damage was not 7 at A0.");
        Require(LanguageFloorSmilingFace.GetMoveDamage(
                LanguageFloorSmilingFaceMove.Vomit) == 15,
            "Vomit damage was not 15 at A0.");
        Require(LanguageFloorSmilingFace.MaxCorpseCount == 3
                && LanguageFloorSmilingFace.TrialCorpseCount == 3,
            "Smiling Face corpse summon counts were not reduced to three.");
        Require(LanguageFloorMeltingCorpse.MoanDamage == 2,
            "Melting Corpse Moan damage was not 2 at A0.");
        Require(LanguageFloorSmilingFace.DevourHits == 2
                && LanguageFloorSmilingFace.ScreamHits == 2
                && LanguageFloorMeltingCorpse.MoanHits == 2,
            "Phase-three multi-hit counts were incorrect.");
        Require(LanguageFloorSmilingFace.GetIntentCapacity(
                    LanguageFloorSmilingFaceForm.First) == 3
                && LanguageFloorSmilingFace.GetIntentCapacity(
                    LanguageFloorSmilingFaceForm.Second) == 2
                && LanguageFloorSmilingFace.GetIntentCapacity(
                    LanguageFloorSmilingFaceForm.Third) == 1,
            "Smiling Face form intent capacities were not 3/2/1.");
        Require(LanguageFloorSmilingFace.VomitDebuffAmount == 3
                && LanguageFloorSmilingFace.VomitDebuffTurns == 2,
            "Vomit debuff values were incorrect.");
        Require(LanguageFloorMeltingCorpseRotPower.RetaliationDamage == 8
                && LanguageFloorMeltingCorpseRotPower.VulnerableAmount == 1
                && LanguageFloorMeltingCorpseRotPower.VulnerableTurns == 1,
            "Rot values were incorrect.");

        SpriteVisualProfile smilingProfile =
            LanguageFloorSmilingFaceCreatureVisuals.Profile;
        Require(smilingProfile.Variants[
                    SpriteVisualProfile.DefaultVariantKey]
                .IdleTexturePath
                    == LanguageFloorSmilingFace.IdleTexturePath
                && smilingProfile.Frames["thrust"].TexturePath
                    == LanguageFloorSmilingFace
                        .AttackThrustTexturePath
                && smilingProfile.Frames["thrust"].CharacterAnchorX
                    == 1150f
                && smilingProfile.Frames["slash"].TexturePath
                    == LanguageFloorSmilingFace
                        .AttackSlashTexturePath
                && smilingProfile.Frames["slash"].CharacterAnchorX
                    == 970f
                && smilingProfile.Frames["vomit"].CharacterAnchorX
                    == 1450f,
            "Smiling Face sprite profile did not preserve its frame anchors.");

        Require(LanguageFloorSmilingFace.ShouldUseSpecial(
                    LanguageFloorSmilingFaceForm.Second, 1)
                && !LanguageFloorSmilingFace.ShouldUseSpecial(
                    LanguageFloorSmilingFaceForm.Second, 2)
                && LanguageFloorSmilingFace.ShouldUseSpecial(
                    LanguageFloorSmilingFaceForm.Second, 3),
            "Scream was not scheduled on turns 1, 3, 5...");
        Require(LanguageFloorSmilingFace.ShouldUseSpecial(
                    LanguageFloorSmilingFaceForm.Third, 1)
                && !LanguageFloorSmilingFace.ShouldUseSpecial(
                    LanguageFloorSmilingFaceForm.Third, 2)
                && !LanguageFloorSmilingFace.ShouldUseSpecial(
                    LanguageFloorSmilingFaceForm.Third, 3)
                && LanguageFloorSmilingFace.ShouldUseSpecial(
                    LanguageFloorSmilingFaceForm.Third, 4),
            "Vomit was not scheduled on turns 1, 4, 7...");

        Require(CombatStateProperties.IsTransient(typeof(LanguageFloorSmilingFace))
                && CombatStateProperties.IsListed(
                    typeof(LanguageFloorSmilingFace),
                    nameof(LanguageFloorSmilingFace.Form)),
            "Smiling Face combat state is a SavedProperty again, or its Form is missing from the reload list.");

        VerifySceneLayoutContract();
    }

    private static async Task VerifyToughEnemyStats()
    {
        PhaseThreeContext fight = await StartPhaseThreeFight(
            "LANGUAGEFLOORVERIFY_SMILING_FACE_TOUGH",
            (int)AscensionLevel.ToughEnemies);
        Require(fight.BossCreature.MaxHp is >= 348 and <= 350,
            "ToughEnemies form-three max HP was outside 348..350: "
            + fight.BossCreature.MaxHp);
        Require(fight.BossCreature.CurrentHp == 300,
            "ToughEnemies form-three entry HP was not 300.");
        CleanupRun();
        await WaitForRunCleanup("phase-three ToughEnemies cleanup");
    }

    private static async Task VerifyPhaseTwoTransition()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LANGUAGEFLOORVERIFY_PHASE2_TO_PHASE3",
            GameMode.Standard,
            ascensionLevel: 0);
        Player runPlayer = RunManager.Instance.DebugOnlyGetState()?
            .Players.Single()
            ?? throw new InvalidOperationException("Run player was missing.");
        if (runPlayer.GetRelic<BurningBlood>() is { } burningBlood)
        {
            await RelicCmd.Remove(burningBlood);
        }

        await RunManager.Instance.EnterAct(1, doTransition: false);
        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "2",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
        });
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState()?.Enemies
                    .Select(enemy => enemy.Monster)
                    .OfType<LanguageFloorCobaltScar>()
                    .Any(static cobalt => cobalt.OpeningResolved) == true,
            "Cobalt Scar phase-two opening");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException(
                "Phase-two transition combat state was null.");
        LanguageFloorCobaltScar cobalt = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorCobaltScar>()
            .Single();
        await CreatureCmd.Kill(cobalt.Creature, force: true);
        await encounter.ResolvePhaseTwoDeath(cobalt.Creature);
        Require(encounter.CurrentPhase == 3 && !encounter.PhaseComplete,
            "Cobalt Scar death did not enter phase three idempotently.");
        await WaitUntil(
            () => combatState.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<LanguageFloorSmilingFace>()
                .Any()
                && !encounter.TransitionPending,
            "Smiling Face spawn after Cobalt Scar death");
        Require(encounter.CurrentPhase == 3
                && !encounter.PhaseComplete
                && !encounter.TransitionPending,
            "Phase-two death did not leave an active phase-three encounter.");
        RequireBackgroundTexture(
            LanguageFloorLiberationBackgroundController.PhaseThreeTexturePath);
        CleanupRun();
        await WaitForRunCleanup("phase-two to phase-three transition cleanup");
    }

    private static void VerifyOpeningStatsAndResources(PhaseThreeContext fight)
    {
        Require(fight.Encounter.CurrentPhase == 3
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.TransitionPending,
            "Direct phase-three load did not remain active.");
        Require(fight.Boss.Form == LanguageFloorSmilingFaceForm.Third,
            "Smiling Face did not start in form three.");
        Require(fight.BossCreature.MaxHp is >= 340 and <= 345,
            "Form-three max HP was outside 340..345: "
            + fight.BossCreature.MaxHp);
        Require(fight.BossCreature.CurrentHp == 300,
            "Form-three entry HP was not 300.");
        Require(fight.Boss.FormThreeMaxHp == fight.BossCreature.MaxHp,
            "Form-three max HP was not saved after its first roll.");
        Require(fight.BossCreature.Monster!.NextMove.Intents.Count
                == LanguageFloorSmilingFace.FormThreeIntentCapacity
                && fight.Boss.PlannedMoves[0]
                    == LanguageFloorSmilingFaceMove.Vomit,
            "Opening form-three turn did not expose one Vomit intent.");

        LibraryCreature bossCreature = RequireLibraryCreature(
            fight.BossCreature,
            "Smiling Face");
        Require(bossCreature.MaxChaoValue == 100
                && bossCreature.CurrentChaoValue == 100,
            "Smiling Face chao resistance was not 100/100.");
        Require(bossCreature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Slash) == LibraryResistanceLevel.Resist
                && bossCreature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Pierce) == LibraryResistanceLevel.Endure
                && bossCreature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Blunt) == LibraryResistanceLevel.Normal,
            "Smiling Face physical resistances did not match Smiling Bodies.");
        Require(bossCreature.GetChaosResistanceLevel(
                    LibraryDamageType.Slash) == LibraryResistanceLevel.Resist
                && bossCreature.GetChaosResistanceLevel(
                    LibraryDamageType.Pierce) == LibraryResistanceLevel.Endure
                && bossCreature.GetChaosResistanceLevel(
                    LibraryDamageType.Blunt) == LibraryResistanceLevel.Normal,
            "Smiling Face chao resistances did not match Smiling Bodies.");
        Require(fight.BossCreature.GetPower<
                    LanguageFloorSmilingFaceFindCorpsesPower>() != null
                && fight.BossCreature.GetPower<
                    LanguageFloorSmilingFaceFormThreeSplitPower>() != null
                && fight.BossCreature.GetPower<
                    LanguageFloorSmilingFaceVomitPower>() != null
                && fight.BossCreature.GetPower<
                    LanguageFloorSmilingFaceScreamPower>() == null,
            "Form-three passive set was incomplete or included Scream.");

        RequireBackgroundTexture(
            LanguageFloorLiberationBackgroundController.PhaseThreeTexturePath);
        Require(fight.BossNode.Visuals
                is LanguageFloorSmilingFaceCreatureVisuals,
            "Smiling Face did not use its scripted sprite visuals.");
        var visuals = (LanguageFloorSmilingFaceCreatureVisuals)
            fight.BossNode.Visuals!;
        RequireTriggerTexture(
            visuals,
            "AttackThrust",
            LanguageFloorSmilingFace.AttackThrustTexturePath);
        RequireTriggerTexture(
            visuals,
            "AttackSlash",
            LanguageFloorSmilingFace.AttackSlashTexturePath);
        RequireTriggerTexture(
            visuals,
            "Scream",
            LanguageFloorSmilingFace.ScreamTexturePath);
        RequireTriggerTexture(
            visuals,
            "Vomit",
            LanguageFloorSmilingFace.VomitTexturePath);
        RequireTriggerTexture(
            visuals,
            "Hit",
            LanguageFloorSmilingFace.HitTexturePath);

        foreach (string path in LanguageFloorSmilingFace.AssetPathsStatic
                     .Concat(LanguageFloorMeltingCorpse.AssetPathsStatic)
                     .Append(LanguageFloorLiberationBackgroundController
                         .PhaseThreeTexturePath))
        {
            Require(ResourceLoader.Exists(path),
                "Phase-three resource was missing: " + path);
        }
    }

    private static void VerifyMovePlanning(PhaseThreeContext fight)
    {
        LanguageFloorSmilingFace planner = fight.Boss;
        planner.DebugSetMovePlanState(LanguageFloorSmilingFaceForm.First);
        VerifyPlannedTurns(planner, LanguageFloorSmilingFaceForm.First, 2);
        planner.DebugSetMovePlanState(LanguageFloorSmilingFaceForm.Second);
        VerifyPlannedTurns(planner, LanguageFloorSmilingFaceForm.Second, 7);
        planner.DebugSetMovePlanState(LanguageFloorSmilingFaceForm.Third);
        VerifyPlannedTurns(planner, LanguageFloorSmilingFaceForm.Third, 8);

        planner.DebugSetMovePlanState(LanguageFloorSmilingFaceForm.Third);
        planner.DebugPlanTurn();
    }

    private static void VerifyPlannedTurns(
        LanguageFloorSmilingFace planner,
        LanguageFloorSmilingFaceForm form,
        int turns)
    {
        LanguageFloorSmilingFaceMove? previousNormal = null;
        int capacity = LanguageFloorSmilingFace.GetIntentCapacity(form);
        for (int turn = 1; turn <= turns; turn++)
        {
            planner.DebugPlanTurn();
            Require(planner.PlannedMoves.Count == capacity
                    && planner.Creature.Monster!.NextMove.Intents.Count
                        == capacity,
                form + " intent capacity mismatch on turn " + turn + ".");
            bool useSpecial = LanguageFloorSmilingFace.ShouldUseSpecial(
                form,
                turn);
            for (int slot = 0; slot < capacity; slot++)
            {
                LanguageFloorSmilingFaceMove move = planner.PlannedMoves[slot];
                if (useSpecial && slot == 0)
                {
                    Require(move == (form == LanguageFloorSmilingFaceForm.Second
                            ? LanguageFloorSmilingFaceMove.Scream
                            : LanguageFloorSmilingFaceMove.Vomit),
                        form + " special move mismatch on turn " + turn + ".");
                    Require(planner.PlannedTargets[slot] == null,
                        form + " group intent unexpectedly had a single target.");
                    continue;
                }

                bool allowed = form switch
                {
                    LanguageFloorSmilingFaceForm.First =>
                        move == LanguageFloorSmilingFaceMove.Devour,
                    LanguageFloorSmilingFaceForm.Second => move is
                        LanguageFloorSmilingFaceMove.Devour
                        or LanguageFloorSmilingFaceMove.Absorb,
                    _ => move is
                        LanguageFloorSmilingFaceMove.Devour
                        or LanguageFloorSmilingFaceMove.Absorb
                        or LanguageFloorSmilingFaceMove.Sit
                };
                Require(allowed,
                    form + " planned an invalid normal move " + move + ".");
                Require(form == LanguageFloorSmilingFaceForm.First
                        || previousNormal == null
                        || move != previousNormal,
                    form + " repeated normal move " + move + ".");
                Require(planner.PlannedTargets[slot] is { IsAlive: true },
                    form + " normal intent had no planned target.");
                previousNormal = move;
            }
        }
    }

    private static async Task VerifyMovesSpawnsAndBacklog(
        PhaseThreeContext fight)
    {
        await fight.Boss.DebugTransitionToForm(
            LanguageFloorSmilingFaceForm.Third);
        await StartPlayerTurnForBoss(fight);
        int formThreeThreshold = (int)Math.Ceiling(
            fight.BossCreature.MaxHp
            * LanguageFloorSmilingFace.CorpseSpawnThresholdPercent
            / 100m);
        await CreatureCmd.SetCurrentHp(
            fight.BossCreature,
            300 - formThreeThreshold * 2);
        Require(fight.Boss.PendingCorpseSpawns == 2
                && CountLivingCorpses(fight.CombatState) == 0,
            "Two crossed HP thresholds did not stay delayed.");
        decimal strongBeforeThresholdTurn = RequirePermanentStrong(
            fight.BossCreature,
            2).Amount;
        await StartPlayerTurnForBoss(fight);
        Require(fight.Boss.PendingCorpseSpawns == 0
                && CountLivingCorpses(fight.CombatState) == 2,
            "Delayed threshold corpses did not spawn next player turn.");
        decimal strongAfterThresholdTurn = RequirePermanentStrong(
            fight.BossCreature,
            strongBeforeThresholdTurn + 2).Amount;

        LanguageFloorMeltingCorpse[] corpses = LivingCorpses(fight.CombatState);
        Creature player = fight.CombatState.PlayerCreatures.Single();
        fight.Boss.DebugSetPlan(
            LanguageFloorSmilingFaceForm.Third,
            formTurnCount: 2,
            [
                LanguageFloorSmilingFaceMove.Devour
            ],
            [corpses[0].Creature]);
        await fight.Boss.RefreshTargetedIntentDisplay();
        await WaitFrames(1);
        Require(fight.BossCreature.Monster!.NextMove.Intents.Count == 1,
            "Form three did not expose one planned intent.");
        for (int slot = 0; slot < 1; slot++)
        {
            AbstractIntent intent = fight.BossCreature.Monster.NextMove
                .Intents[slot];
            Require(intent is IIntentTargetLineProvider provider
                    && provider.GetIntentTargetLineTargets(
                            fight.BossCreature,
                            [player])
                        .Single().Target
                        == fight.Boss.PlannedTargets[slot],
                "Planned intent target mismatch in slot " + slot + ".");
        }

        RequireMonsterTargetMarker(fight.BossNode, intentSlot: 0);

        await CreatureCmd.SetCurrentHp(fight.BossCreature, 100);
        int bossHpBeforeDevour = fight.BossCreature.CurrentHp;
        int corpseHpBeforeDevour = corpses[0].Creature.CurrentHp;
        await fight.Boss.DebugPerformPlannedMove(
            LanguageFloorSmilingFaceMove.Devour,
            slot: 0);
        int devourHeal = (int)Math.Ceiling(
            fight.BossCreature.MaxHp
            * LanguageFloorSmilingFace.DevourHealPercentPerHit
            / 100m);
        Require(fight.BossCreature.CurrentHp
                == bossHpBeforeDevour + devourHeal * 2,
            "Devour did not heal 5% max HP per hit.");
        Require(corpses[0].Creature.CurrentHp < corpseHpBeforeDevour,
            "Devour did not damage a corpse.");

        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await fight.Boss.DebugPerformPlannedMove(
            LanguageFloorSmilingFaceMove.Absorb,
            slot: 1);
        Require(fight.BossCreature
                .GetPower<LibraryOfRuinaNextTurnStrength>()?.Amount
                == LanguageFloorSmilingFace.AbsorbNextTurnStrength,
            "Absorb did not grant 2 next-turn Strength.");
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await fight.Boss.DebugPerformPlannedMove(
            LanguageFloorSmilingFaceMove.Sit,
            slot: 1);
        Require(player.GetPower<FrailPower>()?.Amount
                == LanguageFloorSmilingFace.SitVulnerable,
            "Sit did not apply 2 Vulnerable after both hits.");
        await RemovePowers<VulnerablePower>(player);
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        if (player.Block > 0)
        {
            await GameApi.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                player,
                player.Block,
                player);
        }

        int hpBeforeScream = player.CurrentHp;
        await fight.Boss.DebugPerformMove(LanguageFloorSmilingFaceMove.Scream);
        Require(player.CurrentHp < hpBeforeScream,
            "Scream did not perform group damage.");
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await fight.Boss.DebugPerformMove(LanguageFloorSmilingFaceMove.Vomit);
        RequireDurationPower<LibraryVulnerablePower>(
            player,
            LanguageFloorSmilingFace.VomitDebuffAmount,
            LanguageFloorSmilingFace.VomitDebuffTurns);
        RequireDurationPower<LibraryWeakPower>(
            player,
            LanguageFloorSmilingFace.VomitDebuffAmount,
            LanguageFloorSmilingFace.VomitDebuffTurns);
        RequireDurationPower<LibraryDisarmPower>(
            player,
            LanguageFloorSmilingFace.VomitDebuffAmount,
            LanguageFloorSmilingFace.VomitDebuffTurns);
        await RemovePowers<LibraryVulnerablePower>(player);
        await RemovePowers<LibraryWeakPower>(player);
        await RemovePowers<LibraryDisarmPower>(player);

        NCreature corpseNode = NCombatRoom.Instance!.GetCreatureNode(
                corpses[0].Creature)
            ?? throw new InvalidOperationException(
                "Melting Corpse visual node was missing.");
        Require(corpseNode.Visuals is LanguageFloorMeltingCorpseCreatureVisuals,
            "Melting Corpse did not use its scripted sprite visuals.");
        RequireTriggerTexture(
            (LanguageFloorMeltingCorpseCreatureVisuals)corpseNode.Visuals!,
            "Moan",
            LanguageFloorMeltingCorpse.AttackTexturePath);

        await fight.Boss.DebugTransitionToForm(
            LanguageFloorSmilingFaceForm.Second);
        Require(fight.BossCreature.MaxHp is >= 290 and <= 293
                && fight.BossCreature.CurrentHp == 200,
            "Form-two stats were outside the A0 contract.");
        Require(fight.Boss.PendingCorpseSpawns
                    == LanguageFloorSmilingFace.EntryCorpseCount,
            "Entering form two did not queue its entry corpses.");
        int formTwoThreshold = (int)Math.Ceiling(
            fight.BossCreature.MaxHp
            * LanguageFloorSmilingFace.CorpseSpawnThresholdPercent
            / 100m);
        await CreatureCmd.SetCurrentHp(
            fight.BossCreature,
            200 - formTwoThreshold * 2);
        Require(fight.Boss.PendingCorpseSpawns
                    == LanguageFloorSmilingFace.EntryCorpseCount + 2,
            "One HP loss crossing two thresholds did not accumulate both corpses.");
        decimal strongBeforeCapTurn = RequirePermanentStrong(
            fight.BossCreature,
            strongAfterThresholdTurn).Amount;
        await StartPlayerTurnForBoss(fight);
        Require(CountLivingCorpses(fight.CombatState)
                    == LanguageFloorSmilingFace.MaxCorpseCount
                && fight.Boss.PendingCorpseSpawns == 2,
            "Reduced corpse cap did not preserve excess pending spawns.");
        RequirePermanentStrong(
            fight.BossCreature,
            strongBeforeCapTurn + 2);
        await CreatureCmd.Kill(
            LivingCorpses(fight.CombatState)[0].Creature,
            force: true);
        decimal strongBeforeRefillTurn = RequirePermanentStrong(
            fight.BossCreature,
            strongBeforeCapTurn + 2).Amount;
        await StartPlayerTurnForBoss(fight);
        Require(CountLivingCorpses(fight.CombatState)
                    == LanguageFloorSmilingFace.MaxCorpseCount
                && fight.Boss.PendingCorpseSpawns == 1,
            "Pending corpses did not refill an opened slot on a later player turn.");
        RequirePermanentStrong(
            fight.BossCreature,
            strongBeforeRefillTurn + 2);
    }

    private static async Task VerifyDowngradesPromotionsAndSave(
        PhaseThreeContext fight)
    {
        await fight.Boss.DebugTransitionToForm(
            LanguageFloorSmilingFaceForm.Third);
        decimal strongBeforeFakeDeath = fight.BossCreature
            .GetPowerInstances<LibraryStrongPower>()
            .Single(static power => power.AmountPlan.Count == 0)
            .Amount;
        await CreatureCmd.Kill(fight.BossCreature);
        Require(fight.Boss.WaitingForDowngrade
                && fight.Boss.PendingFormTransition
                    == (int)LanguageFloorSmilingFaceForm.Second
                && fight.Boss.IsFakeDead,
            "Form three did not enter pending fake death.");
        VerifySavedPropertyRoundTrip(fight.Boss);
        await StartPlayerTurnForBoss(fight);
        Require(fight.Boss.Form == LanguageFloorSmilingFaceForm.Second
                && fight.BossCreature.CurrentHp == 200
                && fight.BossCreature.GetPowerInstances<LibraryStrongPower>()
                    .Single(static power => power.AmountPlan.Count == 0).Amount
                == strongBeforeFakeDeath + 2,
            "Pending form three transition did not resolve on player turn start.");

        await CreatureCmd.Kill(fight.BossCreature);
        Require(fight.Boss.WaitingForDowngrade
                && fight.Boss.PendingFormTransition
                    == (int)LanguageFloorSmilingFaceForm.First,
            "Form two did not enter pending fake death.");
        await StartPlayerTurnForBoss(fight);
        Require(fight.Boss.Form == LanguageFloorSmilingFaceForm.First
                && fight.BossCreature.CurrentHp == 100,
            "Pending form two transition did not resolve on player turn start.");
        Require(fight.Boss.FormOneMaxHp is >= 190 and <= 193
                && fight.Boss.FormTwoMaxHp is >= 290 and <= 293,
            "Form-one or form-two max HP roll was outside the A0 range.");

        int oneMax = fight.Boss.FormOneMaxHp;
        int twoMax = fight.Boss.FormTwoMaxHp;
        int threeMax = fight.Boss.FormThreeMaxHp;
        await CreatureCmd.SetCurrentHp(fight.BossCreature, oneMax);
        await StartPlayerTurnForBoss(fight);
        Require(fight.Boss.Form == LanguageFloorSmilingFaceForm.Second
                && fight.BossCreature.CurrentHp == 200,
            "Pending full form one did not promote on player turn start.");
        await CreatureCmd.SetCurrentHp(fight.BossCreature, twoMax);
        await StartPlayerTurnForBoss(fight);
        Require(fight.Boss.Form == LanguageFloorSmilingFaceForm.Third
                && fight.BossCreature.CurrentHp == 300,
            "Pending full form two did not promote on player turn start.");
        Require(fight.Boss.FormOneMaxHp == oneMax
                && fight.Boss.FormTwoMaxHp == twoMax
                && fight.Boss.FormThreeMaxHp == threeMax,
            "A form rerolled its fixed maximum HP.");
    }

    private static async Task VerifyLethalFakeDeathHold(
        PhaseThreeContext fight)
    {
        Require(fight.Boss.Form == LanguageFloorSmilingFaceForm.Third,
            "Lethal verifier did not start in Smiling Face form three.");
        Require(!fight.Encounter.ShouldKeepCombatOpen(fight.CombatState)
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.SettlementTriggered,
            "Live form-three Smiling Face incorrectly held or settled combat.");

        AccessTools.Method(
                typeof(Creature),
                "SetCurrentHpInternal",
                [typeof(decimal)])
            .Invoke(fight.BossCreature, [0m]);
        Require(fight.BossCreature.IsDead && !fight.Boss.IsFakeDead,
            "Lethal verifier did not reach the pre-AfterDeath zero-HP window.");

        bool shouldHold = fight.Encounter.ShouldKeepCombatOpen(
            fight.CombatState);
        Require(shouldHold,
            "Pre-AfterDeath form-three lethal state did not hold combat open.");
        Require(fight.Encounter.CurrentPhase == 3
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.SettlementTriggered,
            "Combat-end predicate prematurely completed phase three before fake death.");

        await fight.Encounter.ResolvePhaseThreeCreatureDeath(
            fight.BossCreature);
        Require(fight.Boss.IsFakeDead
                && fight.Boss.WaitingForDowngrade
                && fight.Boss.PendingFormTransition
                    == (int)LanguageFloorSmilingFaceForm.Second
                && fight.Boss.NextMove.Id
                    == LanguageFloorSmilingFace.ReviveMoveId,
            "Form-three lethal state did not schedule the form-two REVIVE transition.");
        bool combatEnded = await CombatManager.Instance.CheckWinCondition();
        Require(!combatEnded && CombatManager.Instance.IsInProgress,
            "Form-three fake death incorrectly ended the liberation combat.");
    }

    private static async Task VerifyHealingAndRot(PhaseThreeContext fight)
    {
        await CreatureCmd.SetCurrentHp(fight.BossCreature, 100);
        int thirdHeal = (int)Math.Ceiling(
            fight.BossCreature.MaxHp * 30m / 100m);
        await fight.Boss.OnAllyKilled();
        Require(fight.BossCreature.CurrentHp == 100 + thirdHeal,
            "Form three did not recover 30% max HP after an ally kill.");

        await fight.Boss.DebugTransitionToForm(
            LanguageFloorSmilingFaceForm.Second);
        await CreatureCmd.SetCurrentHp(fight.BossCreature, 10);
        LanguageFloorMeltingCorpse[] corpses = LivingCorpses(
            fight.CombatState);
        Require(corpses.Length > 0,
            "No corpse remained for ally-kill and Rot verification.");
        await CreatureCmd.SetCurrentHp(
            corpses[0].Creature,
            LanguageFloorSmilingFace.GetMoveDamage(
                LanguageFloorSmilingFaceMove.Devour) * 2 + 1);
        for (int i = 1; i < corpses.Length; i++)
        {
            await CreatureCmd.SetCurrentHp(
                corpses[i].Creature,
                corpses[i].Creature.MaxHp);
        }
        int formTwoMax = fight.BossCreature.MaxHp;
        int expectedHeal = (int)Math.Ceiling(formTwoMax * 40m / 100m)
            + (int)Math.Ceiling(
                formTwoMax
                * LanguageFloorSmilingFace.DevourHealPercentPerHit
                / 100m) * 2;
        Creature player = fight.CombatState.PlayerCreatures.Single();
        fight.Boss.DebugSetPlan(
            LanguageFloorSmilingFaceForm.Second,
            formTurnCount: 2,
            [
                LanguageFloorSmilingFaceMove.Devour,
                LanguageFloorSmilingFaceMove.Absorb,
                LanguageFloorSmilingFaceMove.Devour
            ],
            [corpses[0].Creature, player, corpses[^1].Creature]);
        await fight.Boss.DebugPerformPlannedMove(
            LanguageFloorSmilingFaceMove.Devour,
            slot: 0);
        Require(fight.BossCreature.CurrentHp == 10 + expectedHeal,
            "Devour kill did not combine 40% ally-kill recovery with both 5% heals.");

        LanguageFloorMeltingCorpse rotCorpse = LivingCorpses(
            fight.CombatState).First();
        LanguageFloorMeltingCorpseRotPower rot = rotCorpse.Creature
            .GetPower<LanguageFloorMeltingCorpseRotPower>()
            ?? throw new InvalidOperationException("Rot power was missing.");
        await RemovePowers<LibraryVulnerablePower>(player);
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        if (player.Block > 0)
        {
            await GameApi.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                player,
                player.Block,
                player);
        }
        await CreatureCmd.GainBlock(
            player,
            32,
            ValueProp.Unpowered,
            null,
            fast: true);
        int totalBefore = player.CurrentHp + player.Block;
        var choiceContext = new ThrowingPlayerChoiceContext();
        await rot.BeforeDamageReceived(
            choiceContext,
            rotCorpse.Creature,
            0m,
            ValueProp.Move,
            player,
            null);
        await rot.BeforeDamageReceived(
            choiceContext,
            rotCorpse.Creature,
            0m,
            ValueProp.Move,
            player,
            null);
        Require(totalBefore - (player.CurrentHp + player.Block) == 16,
            "Two fully blocked attack segments did not each trigger 8 Rot damage.");
        RequireDurationPower<LibraryVulnerablePower>(player, 2, 1);
    }

    private static async Task VerifyCorpseTrialFailure(PhaseThreeContext fight)
    {
        await fight.Boss.DebugTransitionToForm(
            LanguageFloorSmilingFaceForm.First);
        foreach (Creature enemy in fight.CombatState.Enemies.ToArray())
        {
            enemy.RemoveAllPowersInternalExcept();
            await CreatureCmd.Kill(enemy);
        }

        bool combatEnded = await CombatManager.Instance.CheckWinCondition();
        Require(fight.Boss.CorpseTrialPending
                && fight.CombatState.Enemies.Contains(fight.BossCreature),
            "Power-cleared form-one death did not retain the boss and queue the corpse trial.");
        Require(!combatEnded && CombatManager.Instance.IsInProgress,
            "Power-cleared form-one death incorrectly ended combat.");
        await StartPlayerTurnForBoss(fight);
        Require(fight.Boss.CorpseTrialActive
                && fight.Boss.CorpseTrialPlayerTurnsRemaining == 2
                && CountLivingCorpses(fight.CombatState)
                    == LanguageFloorSmilingFace.TrialCorpseCount,
            "Corpse trial did not begin with the reduced corpse count and two full turns.");
        await StartPlayerTurnForBoss(fight);
        Require(fight.Boss.CorpseTrialPlayerTurnsRemaining == 1,
            "Corpse trial did not preserve the second full player turn.");
        await StartPlayerTurnForBoss(fight);
        Require(!fight.Boss.CorpseTrialActive
                && fight.Boss.Form == LanguageFloorSmilingFaceForm.First
                && fight.BossCreature.CurrentHp == 100
                && CountLivingCorpses(fight.CombatState) == 0
                && fight.Boss.PendingCorpseSpawns
                    == LanguageFloorSmilingFace.EntryCorpseCount,
            "Failed corpse trial did not clear corpses and revive form one at 100 HP.");
    }

    private static async Task VerifyCorpseTrialSuccess(PhaseThreeContext fight)
    {
        await CreatureCmd.Kill(fight.BossCreature);
        await StartPlayerTurnForBoss(fight);
        LanguageFloorMeltingCorpse[] corpses = LivingCorpses(
            fight.CombatState);
        Require(corpses.Length == LanguageFloorSmilingFace.TrialCorpseCount,
            "Successful corpse-trial setup did not contain the reduced corpse count.");
        foreach (LanguageFloorMeltingCorpse corpse in corpses)
        {
            await CreatureCmd.Kill(corpse.Creature, force: true);
        }
        await WaitUntil(
            () => fight.Encounter.CurrentPhase == 4
                && fight.Encounter.TransitionPending,
            "phase-three completion after all trial corpses died");
        Require(!fight.Encounter.PhaseComplete
                && fight.CombatState.Enemies.Contains(fight.BossCreature)
                && fight.BossCreature.IsDead,
            "Smiling Face was not retained as the phase-three transition boss.");
        Require(fight.Boss.NextMove.Id
                == LanguageFloorSmilingFace.ReviveAndEmpowerMoveId,
            "Smiling Face did not expose the revive-and-empower intent.");
        Require(!fight.CombatState.Enemies.Any(
                static enemy => enemy.Monster is LanguageFloorDipsia),
            "Phase four spawned before Smiling Face performed its transition turn.");
        await fight.Boss.PerformMove();
        await WaitUntil(
            () => !fight.Encounter.TransitionPending
                && fight.CombatState.Enemies.Any(
                    static enemy =>
                        enemy.Monster is LanguageFloorDipsia),
            "phase-four spawn after Smiling Face transition turn");
    }

    private static void VerifySavedPropertyRoundTrip(
        LanguageFloorSmilingFace source)
    {
        SavedProperties props = CombatStateProperties.From(source)
            ?? throw new InvalidOperationException(
                "Smiling Face SavedProperties were empty.");
        var clone = (LanguageFloorSmilingFace)ModelDb
            .Monster<LanguageFloorSmilingFace>()
            .ToMutable();
        props.Fill(clone);
        Require(clone.Form == source.Form
                && clone.Initialized == source.Initialized
                && clone.FormOneMaxHp == source.FormOneMaxHp
                && clone.FormTwoMaxHp == source.FormTwoMaxHp
                && clone.FormThreeMaxHp == source.FormThreeMaxHp
                && clone.FormTurnCount == source.FormTurnCount
                && clone.PreviousNormalMove == source.PreviousNormalMove
                && clone.PendingCorpseSpawns == source.PendingCorpseSpawns
                && clone.HpAtLastSpawnThreshold
                    == source.HpAtLastSpawnThreshold
                && clone.WaitingForDowngrade == source.WaitingForDowngrade
                && clone.FakeDeathPlayerTurnsRemaining
                    == source.FakeDeathPlayerTurnsRemaining
                && clone.CorpseTrialPending == source.CorpseTrialPending
                && clone.CorpseTrialActive == source.CorpseTrialActive
                && clone.CorpseTrialPlayerTurnsRemaining
                    == source.CorpseTrialPlayerTurnsRemaining
                && clone.ForceKillable == source.ForceKillable
                && clone.PlannedMoveOne == source.PlannedMoveOne
                && clone.PlannedMoveTwo == source.PlannedMoveTwo
                && clone.PlannedMoveThree == source.PlannedMoveThree
                && clone.PlannedMoveFour == source.PlannedMoveFour
                && clone.PlannedTargetOne == source.PlannedTargetOne
                && clone.PlannedTargetTwo == source.PlannedTargetTwo
                && clone.PlannedTargetThree == source.PlannedTargetThree
                && clone.PlannedTargetFour == source.PlannedTargetFour,
            "Smiling Face saved state did not round-trip.");
    }

    private static async Task StartPlayerTurnForBoss(PhaseThreeContext fight)
    {
        await fight.Boss.DebugResolvePlayerTurnStart(
            new ThrowingPlayerChoiceContext(),
            fight.CombatState);
        await WaitFrames(1);
    }

    private static LanguageFloorMeltingCorpse[] LivingCorpses(
        CombatState combatState) =>
        combatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorMeltingCorpse>()
            .OrderBy(static corpse => corpse.Creature.SlotName)
            .ToArray();

    private static int CountLivingCorpses(CombatState combatState) =>
        LivingCorpses(combatState).Length;

    private static LibraryStrongPower RequirePermanentStrong(
        Creature creature,
        decimal expectedAmount)
    {
        LibraryStrongPower strong = creature
            .GetPowerInstances<LibraryStrongPower>()
            .Single(static power => power.AmountPlan.Count == 0);
        Require(strong.Amount == expectedAmount,
            "Permanent Strong amount mismatch: " + strong.Amount
            + " != " + expectedAmount + ".");
        return strong;
    }

    private static void VerifySceneLayoutContract()
    {
        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        Require(Mathf.IsEqualApprox(
                    encounter.GetCameraScaling(),
                    LanguageFloorLiberationEncounter.EncounterCameraScaling)
                && encounter.GetCameraOffset().IsEqualApprox(
                    LanguageFloorLiberationEncounter.EncounterCameraOffset),
            "Language Floor camera did not match the History Floor five-enemy view.");

        PackedScene encounterScene = ResourceLoader.Load<PackedScene>(
            LanguageFloorLiberationEncounter.EncounterScenePath)
            ?? throw new InvalidOperationException(
                "Language Floor encounter scene was missing.");
        Control root = encounterScene.Instantiate<Control>();
        try
        {
            RequireMarker(root, LanguageFloorLiberationEncounter.ScarletSlot,
                new Vector2(800f, 735f));
            RequireMarker(root, LanguageFloorLiberationEncounter.WolfSlot,
                new Vector2(1920f, 738f));
            Vector2[] corpsePositions =
            [
                new(750f, 735f),
                new(1010f, 738f),
                new(1270f, 738f),
                new(1530f, 735f)
            ];
            for (int i = 0; i < corpsePositions.Length; i++)
            {
                RequireMarker(
                    root,
                    LanguageFloorLiberationEncounter.CorpseSlots[i],
                    corpsePositions[i]);
            }
        }
        finally
        {
            root.Free();
        }

        const string backgroundLayerPath =
            "res://scenes/backgrounds/language_floor_liberation_encounter/layers/language_floor_liberation_encounter_bg_00_a.tscn";
        PackedScene backgroundScene = ResourceLoader.Load<PackedScene>(
            backgroundLayerPath)
            ?? throw new InvalidOperationException(
                "Language Floor background layer scene was missing.");
        TextureRect background = backgroundScene.Instantiate<TextureRect>();
        try
        {
            Require(Mathf.IsEqualApprox(background.OffsetLeft, -1280f)
                    && Mathf.IsEqualApprox(background.OffsetTop, -720f)
                    && Mathf.IsEqualApprox(background.OffsetRight, 1280f)
                    && Mathf.IsEqualApprox(background.OffsetBottom, 720f)
                    && background.ExpandMode
                        == TextureRect.ExpandModeEnum.IgnoreSize
                    && background.StretchMode
                        == TextureRect.StretchModeEnum.KeepAspectCentered,
                "Language Floor background did not match the History Floor 2560x1440 layout.");
        }
        finally
        {
            background.Free();
        }
    }

    private static void RequireMarker(
        Node root,
        string markerName,
        Vector2 expectedPosition)
    {
        Marker2D? marker = root.GetNodeOrNull<Marker2D>(markerName);
        Require(marker != null
                && marker.Position.IsEqualApprox(expectedPosition),
            markerName + " position mismatch: "
            + (marker?.Position.ToString() ?? "<missing>"));
    }

    private static void RequireMonsterTargetMarker(
        NCreature bossNode,
        int intentSlot)
    {
        NIntent intentNode = bossNode.IntentContainer
            .GetChildren()
            .OfType<NIntent>()
            .ElementAt(intentSlot);
        Control holder = intentNode.GetNode<Control>("%IntentHolder");
        Control? marker = holder.GetNodeOrNull<Control>(
            "LibraryOfRuinaDetailedIntentTargetMarker");
        Require(marker?.GetNodeOrNull<TextureRect>("MonsterHead") != null,
            "Monster TargetedIntent head marker was missing in slot "
            + intentSlot + ".");
    }

    private static void RequireDurationPower<TPower>(
        Creature creature,
        decimal expectedAmount,
        int expectedTurns)
        where TPower : LibraryTurnsPowerModel
    {
        TPower power = creature.GetPowerInstances<TPower>()
            .Single(static candidate => candidate.TurnsRemaining > 0);
        Require(power.Amount == expectedAmount
                && power.TurnsRemaining == expectedTurns,
            typeof(TPower).Name + " mismatch: amount=" + power.Amount
            + " turns=" + power.TurnsRemaining + ".");
    }

    private static async Task RemovePowers<TPower>(Creature creature)
        where TPower : PowerModel
    {
        foreach (TPower power in creature.GetPowerInstances<TPower>().ToArray())
        {
            await PowerCmd.Remove(power);
        }
    }

    private static LibraryCreature RequireLibraryCreature(
        Creature creature,
        string label) =>
        creature as LibraryCreature
        ?? throw new InvalidOperationException(
            label + " was not a LibraryCreature.");

    private static void RequireTriggerTexture(
        SpriteAttackCreatureVisuals visuals,
        string trigger,
        string expectedPath)
    {
        Require(visuals.TryPlayTrigger(trigger),
            "Visual trigger failed: " + trigger);
        Sprite2D attack = visuals.GetNode<Sprite2D>("%AttackVisuals");
        Require(attack.Texture?.ResourcePath == expectedPath,
            trigger + " texture mismatch: "
            + (attack.Texture?.ResourcePath ?? "<null>"));
    }

    private static void RequireBackgroundTexture(string expectedPath)
    {
        TextureRect? image = NCombatRoom.Instance?.Background
            .GetNodeOrNull<TextureRect>(
                "%LittleRedMercenaryBackgroundImage")
            ?? NCombatRoom.Instance?.Background.FindChild(
                "LittleRedMercenaryBackgroundImage",
                recursive: true,
                owned: false) as TextureRect;
        Require(image?.Texture?.ResourcePath == expectedPath,
            "Background texture mismatch: "
            + (image?.Texture?.ResourcePath ?? "<null>"));
    }

    private sealed record PhaseThreeContext(
        CombatState CombatState,
        LanguageFloorLiberationEncounter Encounter,
        LanguageFloorSmilingFace Boss,
        Creature BossCreature,
        NCreature BossNode);

    private static async Task<PhaseThreeContext> StartPhaseThreeFight(
        string seed,
        int ascensionLevel = 0,
        LanguageFloorSmilingFaceForm? expectedInitialForm =
            LanguageFloorSmilingFaceForm.First)
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
        Player runPlayer = RunManager.Instance.DebugOnlyGetState()?
            .Players.Single()
            ?? throw new InvalidOperationException("Run player was missing.");
        if (runPlayer.GetRelic<BurningBlood>() is { } burningBlood)
        {
            await RelicCmd.Remove(burningBlood);
        }

        await RunManager.Instance.EnterAct(1, doTransition: false);
        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "3",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
        });
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "Language Floor phase-three combat start");
        await WaitUntil(
            () => CombatManager.Instance.DebugOnlyGetState()?.Enemies
                .Select(enemy => enemy.Monster)
                .OfType<LanguageFloorSmilingFace>()
                .Any(boss => boss.Initialized
                    && (expectedInitialForm == null
                        || boss.Form == expectedInitialForm)) == true,
            "Smiling Face initialization");
        await WaitFrames(10);

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException(
                "Phase-three combat state is null.");
        var activeEncounter = combatState.Encounter
            as LanguageFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Language Floor encounter was not active.");
        LanguageFloorSmilingFace boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorSmilingFace>()
            .Single();
        NCreature bossNode = NCombatRoom.Instance!.GetCreatureNode(
                boss.Creature)
            ?? throw new InvalidOperationException(
                "Smiling Face visual node was missing.");
        return new PhaseThreeContext(
            combatState,
            activeEncounter,
            boss,
            boss.Creature,
            bossNode);
    }

    private static void CleanupRun()
    {
        if (RunManager.Instance.DebugOnlyGetState() != null)
        {
            RunManager.Instance.CleanUp(graceful: true);
        }
    }

    private static Task WaitForRunCleanup(string description) =>
        WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() == null
                && RunManager.Instance.DebugOnlyGetState() == null,
            description);

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

        throw new TimeoutException(
            "Timed out waiting for " + description + ".");
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
