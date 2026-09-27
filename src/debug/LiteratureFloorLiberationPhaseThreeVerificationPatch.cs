using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.backgrounds.LiteratureFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.LiteratureFloorLiberation;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.powers.LiteratureFloorLiberation;
using LibraryOfRuina.visuals.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class LiteratureFloorLiberationPhaseThreeVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-literature-floor-phase3";
    private const string VerifyGlitterArg =
        "lor-verify-literature-floor-glitter";
    private const string VerifySoulBindingArg =
        "lor-verify-literature-floor-soul-binding";
    private const string LogPrefix =
        "[LibraryOfRuina.LiteratureFloor.Phase3.Verify] ";
    private const string LocalProjectRoot =
        @"D:\sls2-resource\sls2-better-extension-mod";

    private static bool _started;

    private sealed record PhaseThreeContext(
        CombatState State,
        LiteratureFloorLiberationEncounter Encounter,
        LiteratureFloorBloodlustBoss Bloodlust,
        Creature BloodlustCreature,
        LiteratureFloorEnhancedLeftShoe LeftShoe,
        Creature Player);

    private static void Postfix()
    {
        if (_started || (!HasVerifyArg()
                         && !HasGlitterVerifyArg()
                         && !HasSoulBindingVerifyArg()))
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
        HasArg(VerifyArg);

    private static bool HasGlitterVerifyArg() =>
        HasArg(VerifyGlitterArg);

    private static bool HasSoulBindingVerifyArg() =>
        HasArg(VerifySoulBindingArg);

    private static bool HasArg(string argument) =>
        CommandLineHelper.HasArg(argument)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                argument,
                StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            if (HasSoulBindingVerifyArg())
            {
                await VerifySoulBindingRuntime();
                Log.Info(LogPrefix + "LITERATURE_FLOOR_SOUL_BINDING_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasGlitterVerifyArg())
            {
                await VerifyGlitterRuntime();
                Log.Info(LogPrefix + "LITERATURE_FLOOR_GLITTER_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyStaticContract();
            VerifyResourceAndLocalizationContract();
            await VerifyPhaseThreeRuntime();
            Log.Info(LogPrefix + "LITERATURE_FLOOR_PHASE3_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(
                LogPrefix + "LITERATURE_FLOOR_PHASE3_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task VerifySoulBindingRuntime()
    {
        PhaseThreeContext fight = await StartPhaseThreeFight();
        try
        {
            Player player = fight.Player.Player
                ?? throw new InvalidOperationException(
                    "Soul Binding verifier player was missing.");
            var playerCombatState = player.PlayerCombatState
                ?? throw new InvalidOperationException(
                    "Soul Binding verifier combat state was missing.");
            CardModel card = playerCombatState.AllCards
                .FirstOrDefault(static candidate =>
                    ModelDb.Affliction<Bound>().CanAfflict(candidate))
                ?? throw new InvalidOperationException(
                    "Soul Binding verifier had no bindable combat card.");

            await PowerCmdCompat.ApplyDebuff<ChainsOfBindingPower>(
                fight.Player,
                LiteratureFloorBloodlustBoss.ChainsApplied,
                fight.BloodlustCreature,
                null);
            await CardCmd.AfflictAndPreview<Bound>(
                [card],
                1m,
                CardPreviewStyle.None);
            Require(card.Affliction is Bound,
                "Soul Binding verifier did not bind the combat card.");

            Bound bound = card.Affliction as Bound
                ?? throw new InvalidOperationException(
                    "Soul Binding verifier lost the Bound affliction.");
            IEnumerable<AbstractModel> hookListeners =
                fight.State.IterateHookListeners();
            CardCmd.ClearAffliction(card);
            Require(!hookListeners.Contains(bound),
                "Detached Bound remained in the combat hook listener stream.");
            await CardCmd.AfflictAndPreview<Bound>(
                [card],
                1m,
                CardPreviewStyle.None);

            await Hook.BeforeSideTurnEnd(
                fight.State,
                CombatSide.Player,
                [fight.Player]);

            Require(card.Affliction is not Bound,
                "Soul Binding remained on a participant's card before turn end.");
            Require(fight.Player.HasPower<ChainsOfBindingPower>(),
                "Soul Binding cleanup removed the persistent Chains power.");
        }
        finally
        {
            CleanupRun();
            await WaitForRunCleanup("soul-binding verifier cleanup");
        }
    }

    private static async Task VerifyGlitterRuntime()
    {
        PhaseThreeContext fight = await StartPhaseThreeFight();
        try
        {
            LiteratureFloorBloodlustGlitterPassivePower glitter =
                fight.BloodlustCreature.GetPower<
                    LiteratureFloorBloodlustGlitterPassivePower>()
                ?? throw new InvalidOperationException(
                    "Glitter power was missing.");
            Require(LiteratureFloorBloodlustGlitterPassivePower
                        .BleedThreshold == 6,
                "Glitter Bleed threshold was not six.");

            glitter.SetAmount(
                LiteratureFloorBloodlustGlitterPassivePower.Interval,
                silent: true);
            ForceMove(
                fight.Bloodlust,
                LiteratureFloorBloodlustBoss.ObsessionMoveId);
            await RemovePower<LibraryBleedingPower>(fight.Player);
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                fight.Player,
                5m,
                fight.BloodlustCreature,
                null);
            fight.Bloodlust.RollMove(fight.State.PlayerCreatures);
            await glitter.AfterSideTurnStart(
                CombatSide.Player,
                fight.State.PlayerCreatures,
                fight.State);
            Require(fight.Bloodlust.NextMove.StateId
                        != LiteratureFloorBloodlustBoss.UnbearableMoveId,
                "Glitter triggered at five Bleed instead of six.");

            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                fight.Player,
                1m,
                fight.BloodlustCreature,
                null);
            fight.Bloodlust.RollMove(fight.State.PlayerCreatures);
            string rolledMoveId = fight.Bloodlust.NextMove.StateId;
            await glitter.AfterSideTurnStart(
                CombatSide.Player,
                fight.State.PlayerCreatures,
                fight.State);
            Require(rolledMoveId
                        != LiteratureFloorBloodlustBoss.UnbearableMoveId
                    && fight.Bloodlust.NextMove.StateId
                        == LiteratureFloorBloodlustBoss.UnbearableMoveId,
                "Glitter did not replace the normal RollMove at six Bleed.");
        }
        finally
        {
            CleanupRun();
            await WaitForRunCleanup("glitter verifier cleanup");
        }
    }

    private static void VerifyStaticContract()
    {
        MonsterVisualCatalog.Validate();
        Require(MonsterVisualCatalog.Contains(
                    "LITERATURE_FLOOR_BLOODLUST_BOSS")
                && MonsterVisualCatalog.Contains(
                    "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE"),
            "Phase-three monster visuals were omitted from the catalog baseline.");

        Require(LiteratureFloorLiberationEncounter.ImplementedMaxPhase == 5,
            "Literature floor implemented max phase is not five.");
        Require(LiteratureFloorBloodlustBoss.DebugHpRange(false)
                    == (230, 238)
                && LiteratureFloorBloodlustBoss.DebugHpRange(true)
                    == (244, 250)
                && LiteratureFloorBloodlustBoss
                    .DebugPersistenceDamage(false) == 8
                && LiteratureFloorBloodlustBoss
                    .DebugPersistenceDamage(true) == 10
                && LiteratureFloorBloodlustBoss
                    .DebugObsessionDamage(false) == 20
                && LiteratureFloorBloodlustBoss
                    .DebugObsessionDamage(true) == 24
                && LiteratureFloorBloodlustBoss
                    .DebugDesireBurstDamage(false) == 5
                && LiteratureFloorBloodlustBoss
                    .DebugDesireBurstDamage(true) == 7
                && LiteratureFloorBloodlustBoss
                    .DebugUnbearableDamage(false) == 3
                && LiteratureFloorBloodlustBoss
                    .DebugUnbearableDamage(true) == 5
                && LiteratureFloorBloodlustBoss
                    .DebugUnbearableFinisherBaseDamage(false) == 14
                && LiteratureFloorBloodlustBoss
                    .DebugUnbearableFinisherBaseDamage(true) == 16
                && LiteratureFloorBloodlustBoss.ChainsApplied == 1
                && LiteratureFloorBloodlustGlitterPassivePower
                    .BleedThreshold == 6,
            "Bloodlust normal/ascended values changed.");
        Require(LiteratureFloorEnhancedLeftShoe.DebugHpRange(false)
                    == (100, 110)
                && LiteratureFloorEnhancedLeftShoe.DebugHpRange(true)
                    == (115, 120)
                && LiteratureFloorEnhancedLeftShoe
                    .DebugWhisperingDesireDamage(false) == 6
                && LiteratureFloorEnhancedLeftShoe
                    .DebugWhisperingDesireDamage(true) == 9,
            "Enhanced Left Shoe normal/ascended values changed.");

        LiteratureFloorBloodlustBoss bloodlust =
            ModelDb.Monster<LiteratureFloorBloodlustBoss>();
        LiteratureFloorEnhancedLeftShoe leftShoe =
            ModelDb.Monster<LiteratureFloorEnhancedLeftShoe>();
        Require(bloodlust.DefaultChaoResistance == 80
                && leftShoe.DefaultChaoResistance == 50,
            "Phase-three Chao maxima changed.");
        RequireResistance(
            bloodlust.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            "Bloodlust physical");
        RequireResistance(
            bloodlust.DefaultChaoResistanceData,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Endure,
            "Bloodlust Chao");
        RequireResistance(
            leftShoe.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Vulnerable,
            "Enhanced Left Shoe physical");
        RequireResistance(
            leftShoe.DefaultChaoResistanceData,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Vulnerable,
            "Enhanced Left Shoe Chao");

        MonsterMoveStateMachine bossMoves = GenerateStateMachine(
            ModelDb.Monster<LiteratureFloorBloodlustBoss>().ToMutable());
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBloodlustBoss.PersistenceMoveId,
            typeof(MultiAttackIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBloodlustBoss.ObsessionMoveId,
            typeof(CombinedAttackDebuffIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBloodlustBoss.DesireBurstMoveId,
            typeof(MultiAttackIntent),
            typeof(HealIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBloodlustBoss.UnbearableMoveId,
            typeof(IndiscriminateAttackIntent),
            typeof(LocalPreviewAttackIntent));
        Require(
            ((IndiscriminateAttackIntent)((MoveState)bossMoves.States[
                LiteratureFloorBloodlustBoss.UnbearableMoveId]).Intents[0])
            .Badges.Count == 1,
            "Unbearable still previews a Bleed application.");
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBloodlustBoss.ReviveAndEmpowerMoveId,
            typeof(HealIntent),
            typeof(BuffIntent));

        RandomBranchState normalRandom = bossMoves.States.Values
            .OfType<RandomBranchState>()
            .Single(static state => state.Id == "NORMAL_RANDOM");
        Require(normalRandom.States.Count == 3
                && normalRandom.States.All(static branch =>
                    branch.repeatType == MoveRepeatType.CannotRepeat)
                && normalRandom.States.Select(static branch =>
                        branch.stateId)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(
                    [
                        LiteratureFloorBloodlustBoss.PersistenceMoveId,
                        LiteratureFloorBloodlustBoss.ObsessionMoveId,
                        LiteratureFloorBloodlustBoss.DesireBurstMoveId
                    ]),
            "Bloodlust normal move randomization lost its no-repeat contract.");

        MonsterMoveStateMachine leftMoves = GenerateStateMachine(
            ModelDb.Monster<LiteratureFloorEnhancedLeftShoe>()
                .ToMutable());
        RequireIntentTypes(
            leftMoves,
            LiteratureFloorEnhancedLeftShoe.WhisperingDesireMoveId,
            typeof(CombinedAttackDebuffIntent));
        RequireIntentTypes(
            leftMoves,
            LiteratureFloorEnhancedLeftShoe.HiddenDesireMoveId,
            typeof(CombinedDefendDebuffIntent));
        Require(((MoveState)leftMoves.States[
                        LiteratureFloorEnhancedLeftShoe
                            .WhisperingDesireMoveId])
                    .FollowUpState?.Id
                == LiteratureFloorEnhancedLeftShoe.HiddenDesireMoveId
                && ((MoveState)leftMoves.States[
                        LiteratureFloorEnhancedLeftShoe.HiddenDesireMoveId])
                    .FollowUpState?.Id
                == LiteratureFloorEnhancedLeftShoe
                    .WhisperingDesireMoveId,
            "Enhanced Left Shoe move alternation changed.");

        Require(LiteratureFloorBloodlustBoss
                    .CalculateUnbearableFinisherDamage(14, 3) == 26,
            "Bloodlust finisher formula changed.");
        Require(EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(1)
                    == 0
                && EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(2) == 0
                && EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(3) == 1
                && EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(4) == 1
                && EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(5) == 2,
            "Literature liberation phase BGM routing changed.");

        RequireGreenPassive<
            LiteratureFloorBloodlustGiantAxePassivePower>();
        RequireGreenPassive<
            LiteratureFloorBloodlustGlitterPassivePower>();
        LiteratureFloorDeepWoundPower deepWound =
            ModelDb.Power<LiteratureFloorDeepWoundPower>();
        Require(deepWound.Type == PowerType.Debuff
                && deepWound.StackType == PowerStackType.Counter
                && !deepWound.AllowNegative,
            "Deep Wound power metadata changed.");
    }

    private static void VerifyResourceAndLocalizationContract()
    {
        string animationPath =
            "res://scenes/creature_visuals/literature_floor_bloodlust_boss_animations.tres";
        string[] resources =
        [
            LiteratureFloorLiberationEncounter.BloodlustEncounterScenePath,
            LiteratureFloorBloodlustCreatureVisuals.ScenePath,
            animationPath,
            LiteratureFloorBloodlustBoss.IdleTexturePath,
            LiteratureFloorBloodlustBoss.StrikeTexturePath,
            LiteratureFloorBloodlustBoss.SlashTexturePath,
            LiteratureFloorBloodlustBoss.S1TexturePath,
            LiteratureFloorBloodlustBoss.S2TexturePath,
            LiteratureFloorBloodlustBoss.EvadeTexturePath,
            LiteratureFloorBloodlustBoss.HitTexturePath,
            "res://images/powers/literature_floor_deep_wound_power.png",
            LiteratureFloorLiberationBackgroundController
                .PhaseThreeTexturePath
        ];
        foreach (string resource in resources)
        {
            Require(ResourceLoader.Exists(resource),
                "Missing phase-three resource: " + resource);
        }

        VerifyScene(
            LiteratureFloorLiberationEncounter.BloodlustEncounterScenePath,
            [
                LiteratureFloorLiberationEncounter.EnhancedLeftShoeSlot,
                LiteratureFloorLiberationEncounter.BloodlustSlot
            ]);
        VerifyScene(
            LiteratureFloorBloodlustCreatureVisuals.ScenePath,
            [
                "MotionRoot/Visuals",
                "MotionRoot/AttackVisuals",
                "AnimationPlayer",
                "Bounds",
                "CenterPos",
                "IntentPos",
                "TalkPos"
            ]);
        VerifyAnimationLibrary(
            animationPath,
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["Persistence"] =
                    LiteratureFloorBloodlustAnimationContract
                        .PersistenceDurationSeconds,
                ["Obsession"] =
                    LiteratureFloorBloodlustAnimationContract
                        .ObsessionDurationSeconds,
                ["DesireBurst"] =
                    LiteratureFloorBloodlustAnimationContract
                        .DesireBurstDurationSeconds,
                ["Unbearable"] =
                    LiteratureFloorBloodlustAnimationContract
                        .UnbearableDurationSeconds,
                ["Cast"] = LiteratureFloorBloodlustAnimationContract
                    .CastDurationSeconds,
                ["Hit"] = LiteratureFloorBloodlustAnimationContract
                    .HitDurationSeconds
            });
        VerifyLocalizationKeys();
    }

    private static async Task VerifyPhaseThreeRuntime()
    {
        PhaseThreeContext fight = await StartPhaseThreeFight();
        fight.State.CurrentSide = CombatSide.Enemy;

        Require(fight.Encounter.CurrentPhase == 3
                && fight.Encounter.KilledBossCount == 2
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.TransitionPending,
            "Direct phase-three combat state was incorrect.");
        Require(fight.BloodlustCreature.MaxHp is >= 230 and <= 238
                && ((LibraryCreature)fight.BloodlustCreature)
                    .MaxChaoValue == 80
                && fight.LeftShoe.Creature.MaxHp is >= 100 and <= 110
                && ((LibraryCreature)fight.LeftShoe.Creature)
                    .MaxChaoValue == 50,
            "Phase-three runtime HP/Chao values were incorrect.");
        Require(fight.BloodlustCreature.HasPower<
                    HistoryFloorCorrosionPower>()
                && fight.BloodlustCreature.HasPower<
                    LiteratureFloorBloodlustGiantAxePassivePower>()
                && fight.BloodlustCreature.HasPower<
                    LiteratureFloorBloodlustGlitterPassivePower>()
                && fight.LeftShoe.Creature.HasPower<MinionPower>(),
            "Phase-three opening passives were incomplete.");

        await SetPlayerToFullHpAndClearBlock(fight.Player);
        await RemovePower<LibraryBleedingPower>(fight.Player);
        await fight.LeftShoe.PerformMove();
        Require(fight.Player.GetPower<LibraryBleedingPower>()?.Amount == 4,
            "Whispering Desire did not apply four Bleed.");
        fight.LeftShoe.RollMove(fight.State.PlayerCreatures);
        Require(fight.LeftShoe.NextMove.StateId
                    == LiteratureFloorEnhancedLeftShoe.HiddenDesireMoveId,
            "Enhanced Left Shoe did not alternate to Hidden Desire.");
        await RemovePower<LibraryBleedingPower>(fight.Player);
        int leftBlockBefore = fight.LeftShoe.Creature.Block;
        await fight.LeftShoe.PerformMove();
        Require(fight.LeftShoe.Creature.Block
                    == leftBlockBefore
                    + LiteratureFloorEnhancedLeftShoe.HiddenDesireBlock
                && fight.Player.GetPower<LibraryBleedingPower>()?.Amount
                    == 4,
            "Hidden Desire did not grant 14 Block and four Bleed.");
        fight.LeftShoe.RollMove(fight.State.PlayerCreatures);
        string[] leftMoveLog = fight.LeftShoe.MoveStateMachine!.StateLog
            .Where(static state => state.Id
                    == LiteratureFloorEnhancedLeftShoe
                        .WhisperingDesireMoveId
                || state.Id
                    == LiteratureFloorEnhancedLeftShoe.HiddenDesireMoveId)
            .Select(static state => state.Id)
            .ToArray();
        Require(leftMoveLog.Contains(
                    LiteratureFloorEnhancedLeftShoe
                        .WhisperingDesireMoveId)
                && leftMoveLog.Contains(
                    LiteratureFloorEnhancedLeftShoe.HiddenDesireMoveId)
                && leftMoveLog.Zip(leftMoveLog.Skip(1))
                    .All(static pair => pair.First != pair.Second),
            "Enhanced Left Shoe runtime move log lost strict alternation: "
            + string.Join(">", leftMoveLog) + ".");

        await RemovePower<LibraryBleedingPower>(fight.Player);
        LiteratureFloorDeepWoundPower deepWound =
            await PowerCmdCompat.ApplyDebuff<
                    LiteratureFloorDeepWoundPower>(
                    fight.Player,
                    2m,
                    fight.BloodlustCreature,
                    null)
                ?? throw new InvalidOperationException(
                    "Deep Wound could not be applied.");
        IEnumerable<CardModel> playerCards = fight.Player.Player
            ?.PlayerCombatState?.AllCards
            ?? throw new InvalidOperationException(
                "Phase-three verifier player cards were unavailable.");
        CardModel attackCard = playerCards
            .First(static card => card.Type == CardType.Attack);
        await deepWound.AfterCardPlayed(
            new ThrowingPlayerChoiceContext(),
            CreateCardPlay(attackCard));
        Require(fight.Player.GetPower<LibraryBleedingPower>()?.Amount == 2,
            "Deep Wound did not apply its amount as Bleed after a card play.");
        await deepWound.AfterSideTurnEnd(
            new ThrowingPlayerChoiceContext(),
            CombatSide.Player,
            fight.State.PlayerCreatures);
        Require(fight.Player.GetPower<LiteratureFloorDeepWoundPower>()
                    ?.Amount == 1,
            "Deep Wound did not lose one stack at player turn end.");
        await RemovePower<LiteratureFloorDeepWoundPower>(fight.Player);
        await RemovePower<LibraryBleedingPower>(fight.Player);

        await SetPlayerToFullHpAndClearBlock(fight.Player);
        ForceMove(
            fight.Bloodlust,
            LiteratureFloorBloodlustBoss.PersistenceMoveId);
        await fight.Bloodlust.PerformMove();
        Require(fight.Player.GetPower<LiteratureFloorDeepWoundPower>()
                    ?.Amount == LiteratureFloorBloodlustBoss
                    .PersistenceHits,
            "Great Axe did not apply one Deep Wound per unblocked hit.");
        await RemovePower<LiteratureFloorDeepWoundPower>(fight.Player);

        await CreatureCmd.GainBlock(
            fight.Player,
            999m,
            ValueProp.Unpowered,
            null);
        int blockedHp = fight.Player.CurrentHp;
        ForceMove(
            fight.Bloodlust,
            LiteratureFloorBloodlustBoss.PersistenceMoveId);
        await fight.Bloodlust.PerformMove();
        Require(fight.Player.CurrentHp == blockedHp
                && fight.Player.GetPower<
                    LiteratureFloorDeepWoundPower>() == null,
            "Great Axe applied Deep Wound through fully blocked hits.");
        await ClearBlock(fight.Player);

        LiteratureFloorBloodlustGlitterPassivePower glitter =
            fight.BloodlustCreature.GetPower<
                LiteratureFloorBloodlustGlitterPassivePower>()
            ?? throw new InvalidOperationException(
                "Glitter power was missing.");
        ForceMove(
            fight.Bloodlust,
            LiteratureFloorBloodlustBoss.ObsessionMoveId);
        await RemovePower<LibraryBleedingPower>(fight.Player);
        await glitter.AfterSideTurnStart(
            CombatSide.Player,
            fight.State.PlayerCreatures,
            fight.State);
        Require(fight.Bloodlust.NextMove.StateId
                    == LiteratureFloorBloodlustBoss.ObsessionMoveId,
            "Glitter triggered below its Bleed threshold.");
        await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
            fight.Player,
            5m,
            fight.BloodlustCreature,
            null);
        Require(fight.Bloodlust.NextMove.StateId
                    == LiteratureFloorBloodlustBoss.ObsessionMoveId,
            "Bleed gained during the player turn changed intent immediately.");
        await glitter.AfterSideTurnStart(
            CombatSide.Player,
            fight.State.PlayerCreatures,
            fight.State);
        Require(fight.Bloodlust.NextMove.StateId
                    == LiteratureFloorBloodlustBoss.ObsessionMoveId,
            "Glitter triggered at five Bleed instead of six.");
        await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
            fight.Player,
            1m,
            fight.BloodlustCreature,
            null);
        Require(fight.Bloodlust.NextMove.StateId
                    == LiteratureFloorBloodlustBoss.ObsessionMoveId,
            "The sixth Bleed changed intent before the next turn start.");
        fight.Bloodlust.RollMove(fight.State.PlayerCreatures);
        Require(fight.Bloodlust.NextMove.StateId
                    != LiteratureFloorBloodlustBoss.UnbearableMoveId,
            "The normal player-turn RollMove selected Unbearable directly.");
        await glitter.AfterSideTurnStart(
            CombatSide.Player,
            fight.State.PlayerCreatures,
            fight.State);
        Require(fight.Bloodlust.NextMove.StateId
                    == LiteratureFloorBloodlustBoss.UnbearableMoveId,
            "Glitter did not replace the rolled move at player-turn start.");

        glitter.MarkSpecialUsed();
        ForceMove(
            fight.Bloodlust,
            LiteratureFloorBloodlustBoss.ObsessionMoveId);
        for (int turn = 1; turn <= 2; turn++)
        {
            fight.Bloodlust.RollMove(fight.State.PlayerCreatures);
            await glitter.AfterSideTurnStart(
                CombatSide.Player,
                fight.State.PlayerCreatures,
                fight.State);
            Require(fight.Bloodlust.NextMove.StateId
                        == LiteratureFloorBloodlustBoss.ObsessionMoveId,
                "Glitter ignored its two intervening normal turns.");
        }
        fight.Bloodlust.RollMove(fight.State.PlayerCreatures);
        await glitter.AfterSideTurnStart(
            CombatSide.Player,
            fight.State.PlayerCreatures,
            fight.State);
        Require(fight.Bloodlust.NextMove.StateId
                    == LiteratureFloorBloodlustBoss.UnbearableMoveId,
            "Glitter did not become available on the third subsequent turn.");

        glitter.MarkSpecialUsed();
        await RemovePower<LiteratureFloorDeepWoundPower>(fight.Player);
        await RemovePower<LibraryBleedingPower>(fight.Player);
        await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
            fight.Player,
            5m,
            fight.BloodlustCreature,
            null);
        await CreatureCmd.SetCurrentHp(
            fight.BloodlustCreature,
            Math.Max(1, fight.BloodlustCreature.MaxHp - 50));
        int bossHpBeforeHeal = fight.BloodlustCreature.CurrentHp;
        int expectedHeal = (int)MultiplayerScalingPatchHelper
            .ScaleMonsterHealAmount(
                fight.BloodlustCreature,
                5 * LiteratureFloorBloodlustBoss
                    .DesireBurstHealMultiplier);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        ForceMove(
            fight.Bloodlust,
            LiteratureFloorBloodlustBoss.DesireBurstMoveId);
        await fight.Bloodlust.PerformMove();
        Require(fight.BloodlustCreature.CurrentHp
                    == Math.Min(
                        fight.BloodlustCreature.MaxHp,
                        bossHpBeforeHeal + expectedHeal),
            "Desire Burst did not heal from the highest player Bleed.");

        await RemovePower<LiteratureFloorDeepWoundPower>(fight.Player);
        await RemovePower<LibraryBleedingPower>(fight.Player);
        await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
            fight.Player,
            3m,
            fight.BloodlustCreature,
            null);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        ForceMove(
            fight.Bloodlust,
            LiteratureFloorBloodlustBoss.UnbearableMoveId);
        LocalPreviewAttackIntent finisherIntent = fight.Bloodlust.NextMove
            .Intents.OfType<LocalPreviewAttackIntent>().Single();
        Require(finisherIntent.GetTotalDamage(
                    fight.State.PlayerCreatures,
                    fight.BloodlustCreature) == 26,
            "Unbearable finisher intent did not read the target's existing Bleed.");
        IndiscriminateAttackIntent groupIntent = fight.Bloodlust.NextMove
            .Intents.OfType<IndiscriminateAttackIntent>().Single();
        Require(groupIntent.GetIntentTargetLineTargets(
                    fight.BloodlustCreature,
                    fight.State.PlayerCreatures).Count == 1,
            "Unbearable group intent omitted a living player target.");
        int hpBeforeSpecial = fight.Player.CurrentHp;
        await fight.Bloodlust.PerformMove();
        Require(hpBeforeSpecial - fight.Player.CurrentHp == 38,
            "Unbearable did not deal four opening hits plus the Bleed finisher.");
        Require(fight.Player.GetPower<LibraryBleedingPower>() == null
                && fight.Player.GetPower<
                    LiteratureFloorDeepWoundPower>()?.Amount == 5
                && glitter.Amount == 0,
            "Unbearable did not clear Bleed, retain Deep Wound, and start cooldown.");

        await PowerCmdCompat.ApplyDebuff<ChainsOfBindingPower>(
            fight.Player,
            LiteratureFloorBloodlustBoss.ChainsApplied,
            fight.BloodlustCreature,
            null);
        int hpBeforeTransition = fight.Player.CurrentHp;
        await CreatureCmd.Kill(fight.BloodlustCreature, force: true);
        Require(fight.Encounter.CurrentPhase == 4
                && fight.Encounter.KilledBossCount == 3
                && !fight.Encounter.IsFullyLiberated
                && !fight.State.Enemies.Any(static enemy =>
                    enemy.IsAlive
                    && enemy.Monster
                        is LiteratureFloorEnhancedLeftShoe),
            "Bloodlust death did not advance phase progress and remove the Left Shoe.");

        Require(!fight.Encounter.PhaseComplete
                && fight.Encounter.TransitionPending
                && fight.Bloodlust.NextMove.StateId
                    == LiteratureFloorBloodlustBoss
                        .ReviveAndEmpowerMoveId
                && fight.Player.GetPower<
                    LiteratureFloorDeepWoundPower>() == null
                && fight.Player.GetPower<LibraryBleedingPower>() == null
                && fight.Player.GetPower<ChainsOfBindingPower>()?.Amount
                    == LiteratureFloorBloodlustBoss.ChainsApplied
                && fight.Player.CurrentHp
                    == Math.Min(
                        fight.Player.MaxHp,
                        hpBeforeTransition
                        + LiteratureFloorLiberationEncounter
                            .PhaseTransitionHealAmount),
            "Bloodlust did not enter the controlled phase-four transition.");

        await fight.Bloodlust.PerformMove();
        await WaitFrames(8);
        Creature expression = fight.State.Enemies.Single(static enemy =>
            enemy.IsAlive
            && enemy.Monster
                is LiteratureFloorTodaysExpressionBoss);
        Require(!fight.Encounter.TransitionPending
                && !fight.Encounter.PhaseComplete
                && expression.SlotName
                    == LiteratureFloorLiberationEncounter
                        .TodaysExpressionSlot
                && !fight.State.Enemies.Contains(
                    fight.BloodlustCreature),
            "Bloodlust revive did not spawn the implemented phase four.");

        CleanupRun();
        await WaitForRunCleanup("phase-three verifier cleanup");
    }

    private static async Task<PhaseThreeContext> StartPhaseThreeFight()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LITERATUREFLOORVERIFY_PHASE3",
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
            ["TransitionPending"] = "False"
        });
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress,
            "Literature floor phase-three combat start");
        await WaitFrames(8);

        CombatState state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var activeEncounter = state.Encounter
                as LiteratureFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Literature floor encounter was not active.");
        LiteratureFloorBloodlustBoss boss = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorBloodlustBoss>()
            .Single();
        LiteratureFloorEnhancedLeftShoe leftShoe = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorEnhancedLeftShoe>()
            .Single();
        return new PhaseThreeContext(
            state,
            activeEncounter,
            boss,
            boss.Creature,
            leftShoe,
            state.PlayerCreatures.Single());
    }

    private static void ForceMove(
        MonsterModel monster,
        string moveId)
    {
        MoveState move = monster.MoveStateMachine?.States[moveId]
            as MoveState
            ?? throw new InvalidOperationException(
                "Missing move state " + moveId + ".");
        monster.SetMoveImmediate(move, forceTransition: true);
    }

    private static async Task SetPlayerToFullHpAndClearBlock(
        Creature player)
    {
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await ClearBlock(player);
    }

    private static Task ClearBlock(Creature creature) =>
        creature.Block > 0
            ? CreatureCmd.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                creature,
                creature.Block,
                null)
            : Task.CompletedTask;

    private static Task RemovePower<T>(Creature creature)
        where T : PowerModel =>
        creature.GetPower<T>() is { } power
            ? PowerCmd.Remove(power)
            : Task.CompletedTask;

    private static CardPlay CreateCardPlay(CardModel card) =>
        new()
        {
            Card = card,
            Player = card.Owner,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo
            {
                EnergySpent = 0,
                EnergyValue = 0,
                StarsSpent = 0,
                StarValue = 0
            },
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 1
        };

    private static MonsterMoveStateMachine GenerateStateMachine(
        MonsterModel monster)
    {
        MethodInfo method = monster.GetType().GetMethod(
                "GenerateMoveStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "GenerateMoveStateMachine missing for "
                + monster.GetType().Name + ".");
        return method.Invoke(monster, null) as MonsterMoveStateMachine
            ?? throw new InvalidOperationException(
                "GenerateMoveStateMachine returned null for "
                + monster.GetType().Name + ".");
    }

    private static void RequireIntentTypes(
        MonsterMoveStateMachine machine,
        string moveId,
        params Type[] expectedTypes)
    {
        Require(machine.States.TryGetValue(moveId, out MonsterState? state)
                && state is MoveState,
            "Missing move state " + moveId + ".");
        Type[] actual = ((MoveState)state!).Intents
            .Select(static intent => intent.GetType())
            .ToArray();
        Require(actual.SequenceEqual(expectedTypes),
            moveId + " intent types changed: "
            + string.Join(",", actual.Select(static type => type.Name)));
    }

    private static void RequireGreenPassive<T>()
        where T : LiteratureFloorGreenPassivePower
    {
        T power = ModelDb.Power<T>();
        Require(power.Type == PowerType.None
                && power.StackType == PowerStackType.Single
                && power.PackedIconPath.EndsWith(
                    "library_passive_green.png",
                    StringComparison.Ordinal),
            typeof(T).Name + " lost the green passive icon.");
    }

    private static void VerifyScene(
        string path,
        IEnumerable<string> requiredNodes)
    {
        string text = FileAccess.GetFileAsString(path);
        Require(!text.Contains(
                "ext_resource type=\"Script\"",
                StringComparison.Ordinal),
            path + " contains an attached script.");
        PackedScene scene = ResourceLoader.Load<PackedScene>(path)
            ?? throw new InvalidOperationException(
                "Unable to load scene " + path + ".");
        Node root = scene.Instantiate();
        try
        {
            foreach (string node in requiredNodes)
            {
                Require(root.GetNodeOrNull(node) != null,
                    path + " is missing " + node + ".");
            }
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyAnimationLibrary(
        string path,
        IReadOnlyDictionary<string, double> expectedLengths)
    {
        AnimationLibrary library =
            ResourceLoader.Load<AnimationLibrary>(path)
            ?? throw new InvalidOperationException(
                "Unable to load animation library " + path + ".");
        HashSet<string> names = library.GetAnimationList()
            .Select(static name => name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        Require(names.SetEquals(expectedLengths.Keys),
            path + " animation names changed: "
            + string.Join(",", names));
        foreach ((string name, double expected) in expectedLengths)
        {
            Animation animation = library.GetAnimation(name)
                ?? throw new InvalidOperationException(
                    path + " is missing animation " + name + ".");
            Require(Math.Abs(animation.Length - expected) < 0.001d,
                path + " animation " + name + " length changed.");
        }
    }

    private static void VerifyLocalizationKeys()
    {
        string[] monsterKeys =
        [
            "LITERATURE_FLOOR_BLOODLUST_BOSS.name",
            "LITERATURE_FLOOR_BLOODLUST_BOSS.moves.PERSISTENCE.title",
            "LITERATURE_FLOOR_BLOODLUST_BOSS.moves.OBSESSION.title",
            "LITERATURE_FLOOR_BLOODLUST_BOSS.moves.DESIRE_BURST.title",
            "LITERATURE_FLOOR_BLOODLUST_BOSS.moves.UNBEARABLE.title",
            "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE.name",
            "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE.moves.WHISPERING_DESIRE.title",
            "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE.moves.HIDDEN_DESIRE.title"
        ];
        string[] powerKeys =
        [
            "LITERATURE_FLOOR_DEEP_WOUND_POWER.title",
            "LITERATURE_FLOOR_DEEP_WOUND_POWER.smartDescription",
            "LITERATURE_FLOOR_BLOODLUST_GIANT_AXE_PASSIVE_POWER.smartDescription",
            "LITERATURE_FLOOR_BLOODLUST_GLITTER_PASSIVE_POWER.smartDescription"
        ];
        string[] intentKeys =
        [
            "LITERATURE_FLOOR_BLOODLUST_OBSESSION.description",
            "LITERATURE_FLOOR_BLOODLUST_UNBEARABLE.description",
            "LITERATURE_FLOOR_BLOODLUST_UNBEARABLE_FINISHER.description",
            "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE_WHISPERING_DESIRE.description",
            "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE_HIDDEN_DESIRE.description"
        ];

        foreach (string language in new[] { "zhs", "eng", "jpn", "kor" })
        {
            VerifyJsonKeys(language, "monsters.json", monsterKeys);
            VerifyJsonKeys(language, "powers.json", powerKeys);
            VerifyJsonKeys(language, "intents.json", intentKeys);
        }
    }

    private static void VerifyJsonKeys(
        string language,
        string table,
        IEnumerable<string> requiredKeys)
    {
        string path = Path.Combine(
            LocalProjectRoot,
            "LibraryOfRuina",
            "localization",
            language,
            table);
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(path));
        JsonElement root = document.RootElement;
        foreach (string key in requiredKeys)
        {
            Require(root.TryGetProperty(key, out _),
                language + "/" + table + " is missing " + key + ".");
        }
    }

    private static void RequireResistance(
        LibraryCreatureResistanceData.Resistance? resistance,
        LibraryResistanceLevel slash,
        LibraryResistanceLevel pierce,
        LibraryResistanceLevel blunt,
        string label)
    {
        Require(resistance != null
                && resistance.Slash == slash
                && resistance.Pierce == pierce
                && resistance.Blunt == blunt,
            label + " resistance mismatch.");
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

    private static void CleanupRun()
    {
        RunManager.Instance.CleanUp(graceful: true);
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

            await WaitFrame();
        }

        throw new TimeoutException(
            "Timed out waiting for " + description + ".");
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
