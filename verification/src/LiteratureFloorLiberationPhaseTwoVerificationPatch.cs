using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.SpiderBud;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace LibraryOfRuinaVerification;

internal static class LiteratureFloorLiberationPhaseTwoVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-literature-floor-phase2";
    private const string LogPrefix =
        "[LibraryOfRuina.LiteratureFloor.Phase2.Verify] ";
    private static string LocalProjectRoot => VerificationPaths.ProjectRoot;

    private static bool _started;

    private sealed record PhaseTwoContext(
        CombatState State,
        LiteratureFloorLiberationEncounter Encounter,
        LiteratureFloorRedEyesBoss RedEyes,
        Creature RedEyesCreature,
        LiteratureFloorEnhancedSmallSpider LeftSpider,
        LiteratureFloorEnhancedSmallSpider RightSpider,
        Creature Player);

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
            VerifyStaticContract();
            VerifyResourceAndLocalizationContract();
            await VerifyPhaseTwoRuntime();
            Log.Info(LogPrefix + "LITERATURE_FLOOR_PHASE2_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(
                LogPrefix + "LITERATURE_FLOOR_PHASE2_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStaticContract()
    {
        MonsterVisualCatalog.Validate();
        Require(MonsterVisualCatalog.Contains(
                    "LITERATURE_FLOOR_RED_EYES_BOSS")
                && MonsterVisualCatalog.Contains(
                    "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER"),
            "Phase-two monster visuals were omitted from the catalog baseline.");

        Require(LiteratureFloorRedEyesBoss.DebugHpRange(false)
                    == (120, 127)
                && LiteratureFloorRedEyesBoss.DebugHpRange(true)
                    == (133, 140)
                && LiteratureFloorRedEyesBoss.DebugScreechDamage(false)
                    == 5
                && LiteratureFloorRedEyesBoss.DebugScreechDamage(true)
                    == 7
                && LiteratureFloorRedEyesBoss.ScreechHits == 3
                && LiteratureFloorRedEyesAnimationContract
                    .ScreechHitFrameTimesSeconds.SequenceEqual(
                        [0.50f, 1.10f, 2.10f])
                && LiteratureFloorRedEyesBoss
                    .DebugFlickeringEyesStrength(false) == 1
                && LiteratureFloorRedEyesBoss
                    .DebugFlickeringEyesStrength(true) == 2,
            "Red Eyes normal/ascended values changed.");
        Require(LiteratureFloorEnhancedSmallSpider.DebugHpRange(false)
                    == (50, 54)
                && LiteratureFloorEnhancedSmallSpider.DebugHpRange(true)
                    == (57, 60)
                && LiteratureFloorEnhancedSmallSpider
                    .DebugSharpFangsDamage(false) == 2
                && LiteratureFloorEnhancedSmallSpider
                    .DebugSharpFangsDamage(true) == 3
                && LiteratureFloorEnhancedSmallSpider
                    .DebugSharpFangsFlaw(false) == 2
                && LiteratureFloorEnhancedSmallSpider
                    .DebugSharpFangsFlaw(true) == 3
                && LiteratureFloorEnhancedSmallSpider
                    .DebugSlenderWebBinding(false) == 6
                && LiteratureFloorEnhancedSmallSpider
                    .DebugSlenderWebBinding(true) == 9,
            "Enhanced Small Spider normal/ascended values changed.");

        LiteratureFloorRedEyesBoss redEyes =
            ModelDb.Monster<LiteratureFloorRedEyesBoss>();
        LiteratureFloorEnhancedSmallSpider spider =
            ModelDb.Monster<LiteratureFloorEnhancedSmallSpider>();
        Require(redEyes.DefaultChaoResistance == 100
                && spider.DefaultChaoResistance == 40,
            "Phase-two Chao maxima changed.");
        RequireResistance(
            redEyes.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            "Red Eyes physical");
        RequireResistance(
            redEyes.DefaultChaoResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            "Red Eyes Chao");
        RequireResistance(
            spider.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Vulnerable,
            "Small Spider physical");
        RequireResistance(
            spider.DefaultChaoResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Vulnerable,
            "Small Spider Chao");

        MonsterMoveStateMachine redEyesMoves = GenerateStateMachine(
            ModelDb.Monster<LiteratureFloorRedEyesBoss>().ToMutable());
        RequireIntentTypes(
            redEyesMoves,
            LiteratureFloorRedEyesBoss.FlickeringEyesMoveId,
            typeof(CombinedDefendBuffIntent));
        RequireIntentTypes(
            redEyesMoves,
            LiteratureFloorRedEyesBoss.UnknownMoveId,
            typeof(UnknownIntent));
        RequireIntentTypes(
            redEyesMoves,
            LiteratureFloorRedEyesBoss.ScreechMoveId,
            typeof(MultiAttackIntent),
            typeof(BadgedDebuffIntent));
        RequireIntentTypes(
            redEyesMoves,
            LiteratureFloorRedEyesBoss.ReviveAndEmpowerMoveId,
            typeof(HealIntent),
            typeof(BuffIntent));

        var left = (LiteratureFloorEnhancedSmallSpider)ModelDb
            .Monster<LiteratureFloorEnhancedSmallSpider>()
            .ToMutable();
        left.ConfigureOpeningMove(startsWithSharpFangs: true);
        MonsterMoveStateMachine spiderMoves = GenerateStateMachine(left);
        RequireIntentTypes(
            spiderMoves,
            LiteratureFloorEnhancedSmallSpider.SharpFangsMoveId,
            typeof(CombinedAttackDebuffIntent));
        RequireIntentTypes(
            spiderMoves,
            LiteratureFloorEnhancedSmallSpider.SlenderWebMoveId,
            typeof(CombinedDefendDebuffIntent));

        Require(LiteratureFloorCocoonBindPower.PresenceOffset == 1
                && LiteratureFloorCocoonBindPower
                    .CalculateDamageIncreasePercent(0) == 0
                && LiteratureFloorCocoonBindPower
                    .CalculateDamageIncreasePercent(1) == 50
                && LiteratureFloorCocoonBindPower
                    .CalculateDamageIncreasePercent(2) == 100,
            "Cocoon Bind count/percentage formula changed.");

        RequireGreenPassive<
            LiteratureFloorRedEyesStartHuntingPassivePower>();
        RequireGreenPassive<
            LiteratureFloorRedEyesVigilancePassivePower>();
    }

    private static void VerifyResourceAndLocalizationContract()
    {
        string[] resources =
        [
            LiteratureFloorLiberationEncounter.RedEyesEncounterScenePath,
            LiteratureFloorRedEyesCreatureVisuals.ScenePath,
            LiteratureFloorEnhancedSmallSpiderCreatureVisuals.ScenePath,
            LiteratureFloorRedEyesBoss.IdleTexturePath,
            LiteratureFloorRedEyesBoss.StrikeTexturePath,
            LiteratureFloorRedEyesBoss.S1TexturePath,
            LiteratureFloorRedEyesBoss.SpecialTexturePath,
            LiteratureFloorRedEyesBoss.SlashTexturePath,
            LiteratureFloorRedEyesBoss.HitTexturePath,
            LiteratureFloorEnhancedSmallSpider.IdleTexturePath,
            LiteratureFloorEnhancedSmallSpider.MoveTexturePath,
            LiteratureFloorEnhancedSmallSpider.AttackTexturePath,
            LiteratureFloorEnhancedSmallSpider.GuardTexturePath,
            LiteratureFloorEnhancedSmallSpider.HitTexturePath,
            LiteratureFloorRedEyesBoss.ScreechSfxPath,
            LiteratureFloorRedEyesBoss.FlickeringEyesSfxPath,
            LiteratureFloorRedEyesBoss.VigilanceSfxPath,
            LiteratureFloorRedEyesBoss.HitSfxPath,
            LiteratureFloorRedEyesBoss.HuntStartSfxPath,
            LiteratureFloorEnhancedSmallSpider.AttackSfxPath,
            LiteratureFloorEnhancedSmallSpider.WebSfxPath,
            "res://images/powers/literature_floor_cocoon_bind_power.png"
        ];
        foreach (string resource in resources)
        {
            Require(ResourceLoader.Exists(resource),
                "Missing phase-two resource: " + resource);
        }

        VerifyScene(
            LiteratureFloorLiberationEncounter.RedEyesEncounterScenePath,
            [
                LiteratureFloorLiberationEncounter
                    .EnhancedSpiderLeftSlot,
                LiteratureFloorLiberationEncounter
                    .EnhancedSpiderRightSlot,
                LiteratureFloorLiberationEncounter.RedEyesSlot
            ]);
        VerifyScene(
            LiteratureFloorRedEyesCreatureVisuals.ScenePath,
            [
                "MotionRoot/Visuals",
                "MotionRoot/AttackVisuals",
                "AnimationPlayer",
                "Bounds",
                "CenterPos",
                "IntentPos",
                "TalkPos"
            ]);
        VerifyScene(
            LiteratureFloorEnhancedSmallSpiderCreatureVisuals.ScenePath,
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
            "res://scenes/creature_visuals/literature_floor_red_eyes_boss_animations.tres",
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["FlickeringEyes"] = LiteratureFloorRedEyesAnimationContract
                    .FlickeringEyesDurationSeconds,
                ["Unknown"] = LiteratureFloorRedEyesAnimationContract
                    .UnknownDurationSeconds,
                ["Screech"] = LiteratureFloorRedEyesAnimationContract
                    .ScreechDurationSeconds,
                ["Hit"] = LiteratureFloorRedEyesAnimationContract
                    .HitDurationSeconds
            });
        VerifyAnimationLibrary(
            "res://scenes/creature_visuals/literature_floor_enhanced_small_spider_animations.tres",
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["Attack"] =
                    LiteratureFloorEnhancedSmallSpiderAnimationContract
                        .AttackDurationSeconds,
                ["Cast"] =
                    LiteratureFloorEnhancedSmallSpiderAnimationContract
                        .CastDurationSeconds,
                ["Hit"] =
                    LiteratureFloorEnhancedSmallSpiderAnimationContract
                        .HitDurationSeconds
            });
        VerifyLocalizationKeys();
    }

    private static async Task VerifyPhaseTwoRuntime()
    {
        PhaseTwoContext fight = await StartPhaseTwoFight();
        Require(fight.Encounter.CurrentPhase == 2
                && fight.Encounter.KilledBossCount == 1
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.TransitionPending,
            "Direct phase-two combat state was incorrect.");
        Require(fight.RedEyesCreature.MaxHp is >= 120 and <= 127
                && ((LibraryCreature)fight.RedEyesCreature)
                    .MaxChaoValue == 100
                && fight.LeftSpider.Creature.MaxHp is >= 50 and <= 54
                && fight.RightSpider.Creature.MaxHp is >= 50 and <= 54
                && ((LibraryCreature)fight.LeftSpider.Creature)
                    .MaxChaoValue == 40,
            "Phase-two runtime HP/Chao values were incorrect.");
        Require(fight.RedEyes.NextMove.StateId
                    == LiteratureFloorRedEyesBoss.FlickeringEyesMoveId
                && fight.LeftSpider.NextMove.StateId
                    == LiteratureFloorEnhancedSmallSpider
                        .SharpFangsMoveId
                && fight.RightSpider.NextMove.StateId
                    == LiteratureFloorEnhancedSmallSpider
                        .SlenderWebMoveId,
            "Phase-two opening move order was incorrect.");

        Require(fight.RedEyesCreature.HasPower<HistoryFloorCorrosionPower>()
                && fight.RedEyesCreature.HasPower<
                    LiteratureFloorRedEyesStartHuntingPassivePower>()
                && fight.RedEyesCreature.HasPower<
                    LiteratureFloorRedEyesVigilancePassivePower>()
                && fight.RedEyesCreature.HasPower<
                    SpiderBudUntargetablePower>(),
            "Red Eyes opening passives were incomplete.");
        Require(!UntargetableInteractionFilter.CanBeHit(
                    fight.RedEyesCreature)
                && !UntargetableInteractionFilter.CanBeSelected(
                    fight.RedEyesCreature,
                    fight.Player),
            "Red Eyes was targetable before a spider died.");

        int lockedHp = fight.RedEyesCreature.CurrentHp;
        await CreatureCmdCompat.Damage(
            new ThrowingPlayerChoiceContext(),
            [fight.RedEyesCreature],
            5m,
            ValueProp.Unpowered,
            fight.Player,
            null,
            null);
        Require(fight.RedEyesCreature.CurrentHp == lockedHp,
            "Direct damage bypassed Red Eyes untargetability.");
        await LibraryCreatureCmd.Stun(
            (LibraryCreature)fight.RedEyesCreature);
        Require(!fight.RedEyesCreature.IsStunned,
            "Stun bypassed Red Eyes untargetability.");

        // The verifier invokes enemy models directly, so mirror the real
        // execution side before testing duration and refresh semantics.
        fight.State.CurrentSide = CombatSide.Enemy;

        Dictionary<Creature, int> blockBefore = LivingSpiders(fight)
            .ToDictionary(static spider => spider, static spider =>
                spider.Block);
        await fight.RedEyes.PerformMove();
        foreach (Creature spider in LivingSpiders(fight))
        {
            bool blockRecorded = CombatManager.Instance.History.Entries
                .OfType<BlockGainedEntry>()
                .Any(entry => entry.Receiver == spider
                    && entry.Amount
                        == LiteratureFloorRedEyesBoss
                            .FlickeringEyesBlock);
            Require((spider.Block == blockBefore[spider]
                        + LiteratureFloorRedEyesBoss.FlickeringEyesBlock
                        || blockRecorded)
                    && spider.GetPower<StrengthPower>()?.Amount == 1,
                "Flickering Eyes did not grant 8 Block and 1 Strength to "
                + spider.SlotName
                + "; beforeBlock=" + blockBefore[spider]
                + ", afterBlock=" + spider.Block
                + ", strength="
                + (spider.GetPower<StrengthPower>()?.Amount ?? 0)
                + ", blockRecorded=" + blockRecorded
                + ".");
        }
        fight.RedEyes.RollMove(fight.State.PlayerCreatures);
        Require(fight.RedEyes.NextMove.StateId
                    == LiteratureFloorRedEyesBoss.UnknownMoveId,
            "Flickering Eyes did not transition to Unknown.");
        await fight.RedEyes.PerformMove();
        fight.RedEyes.RollMove(fight.State.PlayerCreatures);
        Require(fight.RedEyes.NextMove.StateId
                    == LiteratureFloorRedEyesBoss.FlickeringEyesMoveId,
            "Unknown did not return to Flickering Eyes.");

        LiteratureFloorRedEyesVigilancePassivePower vigilance =
            fight.RedEyesCreature.GetPower<
                LiteratureFloorRedEyesVigilancePassivePower>()
            ?? throw new InvalidOperationException(
                "Vigilance power was missing.");
        var context = new ThrowingPlayerChoiceContext();
        await vigilance.BeforeSideTurnStart(
            context,
            CombatSide.Enemy,
            fight.State.Enemies,
            fight.State);
        Require(LivingSpiders(fight).All(static spider =>
                spider.GetPower<LibraryProtectionPower>() == null),
            "Vigilance triggered on its first counted enemy turn.");
        await vigilance.BeforeSideTurnStart(
            context,
            CombatSide.Enemy,
            fight.State.Enemies,
            fight.State);
        Require(LivingSpiders(fight).All(static spider =>
                spider.GetPower<LibraryProtectionPower>()?.Amount
                    == LiteratureFloorRedEyesVigilancePassivePower
                        .Protection),
            "Vigilance did not grant 4 Protection on its second turn.");

        int rightBlockBefore = fight.RightSpider.Creature.Block;
        await fight.LeftSpider.PerformMove();
        await fight.RightSpider.PerformMove();
        Require(fight.Player.GetPower<LibraryDisarmPower>()?.Amount == 2
                && fight.Player.GetPower<LibraryBindingPower>()?.Amount == 6
                && fight.RightSpider.Creature.Block
                    == rightBlockBefore
                    + LiteratureFloorEnhancedSmallSpider
                        .SlenderWebBlock,
            "Small Spider moves did not apply Flaw/Bind/Block values.");
        fight.LeftSpider.RollMove(fight.State.PlayerCreatures);
        fight.RightSpider.RollMove(fight.State.PlayerCreatures);
        Require(fight.LeftSpider.NextMove.StateId
                    == LiteratureFloorEnhancedSmallSpider
                        .SlenderWebMoveId
                && fight.RightSpider.NextMove.StateId
                    == LiteratureFloorEnhancedSmallSpider
                        .SharpFangsMoveId,
            "Small Spider move alternation changed.");

        await CreatureCmd.Kill(
            fight.LeftSpider.Creature,
            force: true);
        await WaitFrames(3);
        Require(!fight.RedEyesCreature.HasPower<
                    SpiderBudUntargetablePower>()
                && fight.RedEyes.DebugHuntingActionsRemaining == 2
                && fight.RedEyes.NextMove.StateId
                    == LiteratureFloorRedEyesBoss.ScreechMoveId
                && UntargetableInteractionFilter.CanBeHit(
                    fight.RedEyesCreature),
            "First spider death did not open a two-action hunt window.");

        await CreatureCmd.SetCurrentHp(
            fight.Player,
            fight.Player.MaxHp);
        await fight.RedEyes.PerformMove();
        LiteratureFloorCocoonBindPower cocoon =
            fight.Player.GetPower<LiteratureFloorCocoonBindPower>()
            ?? throw new InvalidOperationException(
                "Screech did not apply Cocoon Bind.");
        Require(fight.RedEyes.DebugHuntingActionsRemaining == 1
                && cocoon.DisplayAmount == 0
                && cocoon.CurrentDamageIncreasePercent == 0,
            "First Screech did not consume one hunt action or start Cocoon at zero.");

        IEnumerable<CardModel> playerCards = fight.Player.Player
            ?.PlayerCombatState?.AllCards
            ?? throw new InvalidOperationException(
                "Phase-two verifier player cards were unavailable.");
        CardModel attackCard = playerCards
            .First(static card => card.Type == CardType.Attack);
        CardPlay cardPlay = CreateCardPlay(attackCard);
        await cocoon.AfterCardPlayed(context, cardPlay);
        await cocoon.AfterCardPlayed(context, cardPlay);
        Require(cocoon.DisplayAmount == 2
                && cocoon.CurrentDamageIncreasePercent == 100
                && cocoon.ModifyDamageMultiplicativeCompat(
                    fight.Player,
                    10m,
                    ValueProp.Move,
                    fight.RedEyesCreature,
                    null,
                    null) == 2m
                && cocoon.ModifyDamageMultiplicativeCompat(
                    fight.Player,
                    10m,
                    ValueProp.Unpowered,
                    fight.RedEyesCreature,
                    null,
                    null) == 1m
                && cocoon.ModifyDamageMultiplicativeCompat(
                    fight.Player,
                    10m,
                    ValueProp.Move,
                    fight.Player,
                    null,
                    null) == 1m,
            "Cocoon Bind multiplier/source filtering was incorrect.");

        fight.RedEyes.RollMove(fight.State.PlayerCreatures);
        Require(fight.RedEyes.NextMove.StateId
                    == LiteratureFloorRedEyesBoss.ScreechMoveId,
            "Red Eyes did not retain Screech for the second hunt action.");
        await CreatureCmd.SetCurrentHp(
            fight.Player,
            fight.Player.MaxHp);
        await fight.RedEyes.PerformMove();
        Require(fight.RedEyes.DebugHuntingActionsRemaining == 0
                && fight.RedEyesCreature.HasPower<
                    SpiderBudUntargetablePower>(),
            "Second Screech did not close the temporary hunt window.");

        await cocoon.AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            fight.State.Enemies);
        LiteratureFloorCocoonBindPower? afterFirstDecay =
            fight.Player.GetPower<LiteratureFloorCocoonBindPower>();
        Require(afterFirstDecay == null
                || afterFirstDecay.DisplayAmount == 0,
            "Refreshed Cocoon Bind did not reset at enemy turn end.");
        if (afterFirstDecay != null)
        {
            await afterFirstDecay.AfterSideTurnEnd(
                context,
                CombatSide.Enemy,
                fight.State.Enemies);
        }
        Require(fight.Player.GetPower<LiteratureFloorCocoonBindPower>()
                    == null,
            "Unrefreshed Cocoon Bind did not expire next enemy turn.");

        await CreatureCmd.Kill(
            fight.RightSpider.Creature,
            force: true);
        await WaitFrames(3);
        Require(!fight.RedEyesCreature.HasPower<
                    SpiderBudUntargetablePower>()
                && fight.RedEyes.DebugHuntingActionsRemaining == 0
                && fight.RedEyes.NextMove.StateId
                    == LiteratureFloorRedEyesBoss.ScreechMoveId,
            "All-spider death did not make Screech/targetability permanent.");

        int hpBeforeTransition = Math.Max(
            1,
            fight.Player.MaxHp - 20);
        await CreatureCmd.SetCurrentHp(
            fight.Player,
            hpBeforeTransition);
        await LiteratureFloorCocoonBindPower.ApplyOrRefresh(
            fight.Player,
            fight.RedEyesCreature);

        await CreatureCmd.Kill(fight.RedEyesCreature, force: true);
        await WaitFrames(8);
        Require(fight.Encounter.CurrentPhase == 3
                && fight.Encounter.KilledBossCount == 2
                && !fight.Encounter.PhaseComplete
                && fight.Encounter.TransitionPending
                && fight.Player.CurrentHp
                    == Math.Min(
                        fight.Player.MaxHp,
                        hpBeforeTransition
                        + LiteratureFloorLiberationEncounter
                            .PhaseTransitionHealAmount)
                && fight.Player.GetPower<
                    LiteratureFloorCocoonBindPower>() == null
                && !fight.Encounter.IsFullyLiberated,
            "Red Eyes death did not enter the controlled phase-three transition.");
        Require(fight.RedEyes.NextMove.StateId
                    == LiteratureFloorRedEyesBoss
                        .ReviveAndEmpowerMoveId,
            "Red Eyes did not expose REVIVE_AND_EMPOWER after death.");

        await fight.RedEyes.PerformMove();
        await WaitFrames(8);
        Require(!fight.Encounter.TransitionPending
                && !fight.Encounter.PhaseComplete
                && fight.State.Enemies.Count(static enemy =>
                    enemy.IsAlive
                    && enemy.Monster
                        is LiteratureFloorBloodlustBoss) == 1
                && fight.State.Enemies.Count(static enemy =>
                    enemy.IsAlive
                    && enemy.Monster
                        is LiteratureFloorEnhancedLeftShoe) == 1,
            "Red Eyes transition did not spawn Bloodlust and the enhanced Left Shoe.");

        CleanupRun();
        await WaitForRunCleanup("phase-two verifier cleanup");
    }

    private static async Task<PhaseTwoContext> StartPhaseTwoFight()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LITERATUREFLOORVERIFY_PHASE2",
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);

        var encounter = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "2",
            ["KilledBossCount"] = "1",
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
            "Literature floor phase-two combat start");
        await WaitFrames(8);

        CombatState state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var activeEncounter = state.Encounter
                as LiteratureFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Literature floor encounter was not active.");
        LiteratureFloorRedEyesBoss redEyes = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorRedEyesBoss>()
            .Single();
        LiteratureFloorEnhancedSmallSpider[] spiders = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorEnhancedSmallSpider>()
            .OrderBy(static spider => spider.Creature.SlotName)
            .ToArray();
        Require(spiders.Length == 2,
            "Direct phase-two combat did not create two spiders.");
        return new PhaseTwoContext(
            state,
            activeEncounter,
            redEyes,
            redEyes.Creature,
            spiders.Single(static spider => spider.StartsWithSharpFangs),
            spiders.Single(static spider => !spider.StartsWithSharpFangs),
            state.PlayerCreatures.Single());
    }

    private static CardPlay CreateCardPlay(CardModel card)
    {
        return VerificationApi.CreateCardPlay(card, card.Owner);
    }

    private static Creature[] LivingSpiders(PhaseTwoContext fight) =>
        fight.State.Enemies.Where(static enemy =>
            enemy.IsAlive
            && enemy.Monster
                is LiteratureFloorEnhancedSmallSpider).ToArray();

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
        string[] requiredMonsterKeys =
        [
            "LITERATURE_FLOOR_RED_EYES_BOSS.name",
            "LITERATURE_FLOOR_RED_EYES_BOSS.moves.FLICKERING_EYES.title",
            "LITERATURE_FLOOR_RED_EYES_BOSS.moves.UNKNOWN.title",
            "LITERATURE_FLOOR_RED_EYES_BOSS.moves.SCREECH.title",
            "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER.name",
            "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER.moves.SHARP_FANGS.title",
            "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER.moves.SLENDER_WEB.title"
        ];
        string[] requiredPowerKeys =
        [
            "LITERATURE_FLOOR_RED_EYES_START_HUNTING_PASSIVE_POWER.smartDescription",
            "LITERATURE_FLOOR_RED_EYES_VIGILANCE_PASSIVE_POWER.smartDescription",
            "LITERATURE_FLOOR_COCOON_BIND_POWER.title",
            "LITERATURE_FLOOR_COCOON_BIND_POWER.smartDescription"
        ];
        string[] requiredIntentKeys =
        [
            "LITERATURE_FLOOR_RED_EYES_FLICKERING_EYES.description",
            "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SHARP_FANGS.description",
            "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SLENDER_WEB.description"
        ];

        foreach (string language in new[] { "zhs", "eng", "jpn", "kor" })
        {
            VerifyJsonKeys(language, "monsters.json", requiredMonsterKeys);
            VerifyJsonKeys(language, "powers.json", requiredPowerKeys);
            VerifyJsonKeys(language, "intents.json", requiredIntentKeys);
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
