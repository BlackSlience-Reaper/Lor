using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.JudgementBird;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.JudgementBird;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents.JudgementBird;
using LibraryOfRuina.monsters;
using LibraryOfRuina.monsters.JudgementBird;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.JudgementBird;
using LibraryOfRuina.powers.JudgementBird;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.JudgementBird;
using LibraryOfRuina.visuals.JudgementBird;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.HealthBars;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class JudgementBirdVerificationPatch
{
    private const string VerifyArg = "lor-verify-judgement-bird";
    private const string FullOfEvilVerifyArg =
        "lor-verify-judgement-bird-full-of-evil";
    private const string LogPrefix =
        "[LibraryOfRuina.JudgementBird.Verify] ";

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PrivateStatic =
        BindingFlags.Static | BindingFlags.NonPublic;

    private static bool _started;

    internal static void Start()
    {
        if (_started || (!HasVerifyArg(VerifyArg)
            && !HasVerifyArg(FullOfEvilVerifyArg)))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg(string argName) =>
        CommandLineHelper.HasArg(argName)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            argName,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            if (HasVerifyArg(FullOfEvilVerifyArg))
            {
                VerifySavedPropertySchema();
                JudgementBirdFight focusedFight = await StartFight();
                await VerifyFullOfEvilDelay(
                    focusedFight,
                    new ThrowingPlayerChoiceContext());
                RunManager.Instance.CleanUp(graceful: true);
                await WaitUntil(
                    static () => RunManager.Instance.DebugOnlyGetState()
                        == null,
                    "Judgement Bird Full of Evil verifier cleanup");
                Log.Info(LogPrefix + "FULL_OF_EVIL_DELAY_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyStaticContracts();
            JudgementBirdFight fight = await StartFight();
            await VerifyCombatContracts(fight);
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Judgement Bird verifier cleanup");
            Log.Info(LogPrefix + "JUDGEMENT_BIRD_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(
                LogPrefix + "JUDGEMENT_BIRD_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStaticContracts()
    {
        Require(
            ModelDb.GetId<JudgementBirdElite>().Entry
                == "JUDGEMENT_BIRD_ELITE",
            "Encounter ID drifted.");
        Require(
            ModelDb.GetId<JudgementBird>().Entry == "JUDGEMENT_BIRD"
            && ModelDb.GetId<EscapedBird>().Entry == "ESCAPED_BIRD",
            "Monster IDs drifted.");

        JudgementBirdElite encounter = ModelDb
            .Encounter<JudgementBirdElite>();
        Require(encounter.RoomType == RoomType.Elite,
            "Encounter is not an elite.");
        Require(encounter.HasScene,
            "Encounter scene is disabled.");
        Require(encounter.Slots.SequenceEqual(
            [
                JudgementBirdElite.LeftEscapedBirdSlot,
                JudgementBirdElite.RightEscapedBirdSlot,
                JudgementBirdElite.BossSlot
            ]),
            "Encounter slots drifted.");
        Require(
            encounter.AllPossibleMonsters.Count() == 2
            && encounter.AllPossibleMonsters.Any(
                static monster => monster is JudgementBird)
            && encounter.AllPossibleMonsters.Any(
                static monster => monster is EscapedBird),
            "Encounter bestiary monster set is incomplete.");

        List<EncounterModel> weighted = [];
        LibraryEncounterWeighting
            .AddWeightedCopies<JudgementBirdElite>(weighted);
        Require(weighted.Count == 25,
            "Encounter did not receive priority pool weighting.");
        Require(
            LibraryEncounterWeighting.IsPriorityEncounter(encounter),
            "Encounter is missing from the priority abnormality set.");

        VerifyGazeStateMachine();
        VerifyPageModels();
        VerifySavedPropertySchema();
        VerifyBgmRegistration();
        VerifyPassiveIcons();
        VerifyForecastOrigin();
        VerifyCreatureVisualScenes();
        VerifyLocalizationKeys();
        VerifyResourcePaths();

        Require(
            JudgementBird.LowMinHp == 370
                && JudgementBird.LowMaxHp == 380
                && JudgementBird.HighMinHp == 390
                && JudgementBird.HighMaxHp == 400,
            "Judgement Bird HP contract drifted.");
        Require(
            EscapedBird.LowMinHp == 190
                && EscapedBird.LowMaxHp == 193
                && EscapedBird.HighMinHp == 197
                && EscapedBird.HighMaxHp == 200,
            "Escaped Bird HP contract drifted.");
        Require(
            EscapedBird.PanicLowDamage == 6
                && EscapedBird.PanicHighDamage == 8
            && EscapedBird.PanicHits == 3
                && EscapedBird.ScreamLowDamage == 5
                && EscapedBird.ScreamHighDamage == 7
            && EscapedBird.ScreamHits == 2
            && EscapedBird.ScreamWeak == 2,
            "Escaped Bird move values drifted.");
    }

    private static void VerifyGazeStateMachine()
    {
        var boss = (JudgementBird)ModelDb.Monster<JudgementBird>()
            .ToMutable();
        MethodInfo generate = typeof(JudgementBird).GetMethod(
                "GenerateMoveStateMachine",
                PrivateInstance)
            ?? throw new MissingMethodException(
                typeof(JudgementBird).FullName,
                "GenerateMoveStateMachine");
        MonsterMoveStateMachine machine = generate.Invoke(boss, null)
            as MonsterMoveStateMachine
            ?? throw new InvalidOperationException(
                "Judgement Bird state machine was unavailable.");
        Require(machine.States[JudgementBird.GazeRouterId]
                is DelegatingMonsterRouterState,
            "Judgement router does not defer its choice until move roll.");
        RandomBranchState router = machine.States[
                JudgementBird.GazeRandomRouterId]
            as RandomBranchState
            ?? throw new InvalidOperationException(
                "Judgement Bird gaze router was unavailable.");

        string[] expectedIds =
        [
            JudgementBird.GazeOneMoveId,
            JudgementBird.GazeTwoMoveId,
            JudgementBird.GazeThreeMoveId
        ];
        Require(
            router.States.Select(static state => state.stateId)
                .OrderBy(static id => id)
                .SequenceEqual(expectedIds.OrderBy(static id => id)),
            "Gaze router branches drifted.");
        Require(router.States.All(static state =>
                state.repeatType == MoveRepeatType.CannotRepeat),
            "A gaze move can repeat immediately.");
    }

    private static void VerifyPageModels()
    {
        JudgementBirdPageRelic relic = ModelDb
            .Relic<JudgementBirdPageRelic>();
        Require(relic.Rarity == RelicRarity.Event,
            "Page relic rarity drifted.");
        Require(AbnormalityPageRewardPreselection.IsPageRelic(relic),
            "Page relic preselection registration is missing.");
        Require(ModelDb.RelicPool<EventRelicPool>()
                .AllRelicIds.Contains(relic.Id),
            "Page relic is absent from the Relic Collection source pool.");

        VerifyChoiceCard<JudgementBirdWeightOfSinChoiceCard>();
        VerifyChoiceCard<JudgementBirdJudgementChoiceCard>();
        VerifyChoiceCard<JudgementBirdTiltedScaleChoiceCard>();
    }

    private static void VerifyChoiceCard<TCard>()
        where TCard : JudgementBirdPageChoiceCardBase
    {
        CardModel card = ModelDb.Card<TCard>();
        CardPoolAttribute? registration = typeof(TCard)
            .GetCustomAttribute<CardPoolAttribute>();
        Require(registration?.PoolType == typeof(TokenCardPool),
            typeof(TCard).Name + " is not registered to TokenCardPool.");
        Require(card.Pool is TokenCardPool,
            typeof(TCard).Name + " resolved to the wrong card pool.");
        Require(
            card.Rarity == CardRarity.Ancient
            && !card.ShouldShowInCardLibrary
            && !card.CanBeGeneratedInCombat,
            typeof(TCard).Name + " visibility contract drifted.");
        Require(ResourceExists(card.PortraitPath),
            typeof(TCard).Name + " portrait is missing.");
    }

    private static void VerifySavedPropertySchema()
    {
        Type[] savedTypes = SavedPropertiesTypeCacheCompat
            .ModSavedPropertyTypes;
        Require(savedTypes.Contains(typeof(JudgementBirdPageRelic)),
            "Page relic SavedProperty type is not pinned.");
        Require(CombatStateProperties.IsTransient(typeof(JudgementBird))
                && CombatStateProperties.IsListed(typeof(JudgementBird), "FullOfEvilPending"),
            "Judgement Bird combat state is a SavedProperty again, or Full of Evil pending is missing from the reload list.");
        string fingerprintMaterial = SavedPropertiesTypeCacheCompat
            .BuildSchemaFingerprintMaterial();
        Require(fingerprintMaterial.Contains(
                "enum|"
                + typeof(JudgementBirdPageMode).FullName
                + "|WeightOfSin=1",
                StringComparison.Ordinal),
            "Judgement Bird page mode enum is absent from the schema fingerprint.");
    }

    private static void VerifyBgmRegistration()
    {
        FieldInfo field = typeof(EncounterBgmController).GetField(
                "ConfigByEncounterType",
                PrivateStatic)
            ?? throw new MissingFieldException(
                typeof(EncounterBgmController).FullName,
                "ConfigByEncounterType");
        IDictionary configs = field.GetValue(null) as IDictionary
            ?? throw new InvalidOperationException(
                "Encounter BGM registry was unavailable.");
        Require(configs.Contains(typeof(JudgementBirdElite)),
            "Judgement Bird BGM registration is missing.");
    }

    private static void VerifyPassiveIcons()
    {
        PowerModel[] passives =
        [
            ModelDb.Power<JudgementBirdUnjustScalePower>(),
            ModelDb.Power<JudgementBirdWeightOfSinPower>(),
            ModelDb.Power<JudgementBirdJudgementPower>(),
            ModelDb.Power<JudgementBirdFullOfEvilPower>(),
            ModelDb.Power<EscapedBirdScaryPower>()
        ];
        string[] iconMismatches = passives
            .Where(power =>
                power.PackedIconPath
                    != JudgementBirdPassivePower.GreenPassiveIconPath
                || power.ResolvedBigIconPath
                    != JudgementBirdPassivePower.GreenPassiveIconPath)
            .Select(power =>
                power.GetType().Name
                + " packed=" + power.PackedIconPath
                + " big=" + power.ResolvedBigIconPath)
            .ToArray();
        Require(iconMismatches.Length == 0,
            "Judgement Bird passive icon mismatch: "
            + string.Join("; ", iconMismatches));
        Require(ResourceExists(
                JudgementBirdPassivePower.GreenPassiveIconPath),
            "Shared green passive icon is missing.");
    }

    private static void VerifyForecastOrigin()
    {
        Require(
            JudgementBirdSinPower.ForecastDirection
                == HealthBarForecastGrowthDirection.FromLeft,
            "Sin forecast does not start from the health bar's left edge.");
        Require(typeof(IHealthBarVisualGraftSource).IsAssignableFrom(
                typeof(JudgementBirdSinPower)),
            "Sin forecast cannot extend past current HP.");
    }

    private static void VerifyCreatureVisualScenes()
    {
        VerifyCreatureVisualScene(
            JudgementBirdCreatureVisuals.ScenePath,
            ["Idle", "Attack", "Guard", "Hit"]);
        VerifyCreatureVisualScene(
            EscapedBirdCreatureVisuals.ScenePath,
            [
                "Idle",
                "AttackOne",
                "AttackTwo",
                "ScreamOne",
                "ScreamTwo",
                "Hit"
            ]);
    }

    private static void VerifyCreatureVisualScene(
        string scenePath,
        IReadOnlyList<string> animationNames)
    {
        PackedScene packed = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException(
                "Missing creature visual scene " + scenePath + ".");
        Node2D root = packed.Instantiate<Node2D>();
        try
        {
            Require(root.GetScript().VariantType == Variant.Type.Nil,
                scenePath + " template root must remain scriptless.");
            Require(root.GetNodeOrNull<Node2D>("%MotionRoot") != null,
                scenePath + " is missing %MotionRoot.");
            Require(root.GetNodeOrNull<Sprite2D>("%Visuals") != null,
                scenePath + " is missing %Visuals.");
            Require(root.GetNodeOrNull<Sprite2D>("%AttackVisuals") != null,
                scenePath + " is missing %AttackVisuals.");
            Require(root.GetNodeOrNull<Control>("%Bounds") != null,
                scenePath + " is missing %Bounds.");
            Require(root.GetNodeOrNull<Marker2D>("%CenterPos") != null,
                scenePath + " is missing %CenterPos.");
            Require(root.GetNodeOrNull<Marker2D>("%IntentPos") != null,
                scenePath + " is missing %IntentPos.");
            Require(root.GetNodeOrNull<Marker2D>("%TalkPos") != null,
                scenePath + " is missing %TalkPos.");
            AnimationPlayer player = root
                .GetNodeOrNull<AnimationPlayer>("%AnimationPlayer")
                ?? throw new InvalidOperationException(
                    scenePath + " is missing %AnimationPlayer.");
            foreach (string animationName in animationNames)
            {
                Require(player.HasAnimation("default/" + animationName),
                    scenePath
                    + " is missing animation default/"
                    + animationName
                    + ".");
            }
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyLocalizationKeys()
    {
        (string table, string key)[] keys =
        [
            ("monsters", "JUDGEMENT_BIRD.name"),
            ("monsters", "ESCAPED_BIRD.name"),
            ("encounters", "JUDGEMENT_BIRD_ELITE.title"),
            ("intents", "JUDGEMENT_BIRD_JUDGEMENT.description"),
            ("powers", "JUDGEMENT_BIRD_SIN_POWER.smartDescription"),
            ("relics", "JUDGEMENT_BIRD_PAGE_RELIC.description"),
            ("relics", "JUDGEMENT_BIRD_PAGE_RELIC.selectionScreenPrompt"),
            ("cards", "JUDGEMENT_BIRD_WEIGHT_OF_SIN_CHOICE_CARD.description"),
            ("cards", "JUDGEMENT_BIRD_JUDGEMENT_CHOICE_CARD.description"),
            ("cards", "JUDGEMENT_BIRD_TILTED_SCALE_CHOICE_CARD.description")
        ];
        foreach ((string table, string key) in keys)
        {
            Require(new LocString(table, key).Exists(),
                "Missing localization key " + table + ":" + key + ".");
        }
    }

    private static void VerifyResourcePaths()
    {
        string[] resources =
        [
            "res://scenes/encounters/judgement_bird_elite.tscn",
            "res://scenes/backgrounds/judgement_bird_elite/judgement_bird_elite_background.tscn",
            "res://scenes/backgrounds/judgement_bird_elite/layers/judgement_bird_elite_bg_00_a.tscn",
            JudgementBirdCreatureVisuals.ScenePath,
            EscapedBirdCreatureVisuals.ScenePath,
            JudgementBird.IdleTexturePath,
            JudgementBird.FireTexturePath,
            JudgementBird.GuardTexturePath,
            JudgementBird.HitTexturePath,
            EscapedBird.IdleTexturePath,
            EscapedBird.AttackTexturePath,
            EscapedBird.AttackTwoTexturePath,
            EscapedBird.HitTexturePath,
            JudgementBird.OnSfxPath,
            JudgementBird.DownSfxPath,
            JudgementBird.HangSfxPath,
            JudgementBird.StunSfxPath,
            EscapedBird.AttackSfxPath,
            EscapedBird.ScreamSfxPath,
            JudgementBirdJudgementVideoController.VideoPath,
            "res://images/relics/judgement_bird_page_relic.png",
            "res://images/powers/judgement_bird_sin_power.png"
        ];
        foreach (string resource in resources)
        {
            Require(ResourceExists(resource),
                "Missing resource " + resource + ".");
        }
    }

    private static async Task VerifyCombatContracts(
        JudgementBirdFight fight)
    {
        Require(fight.Players.Length == 2,
            "Fake multiplayer fight did not create two players.");
        Require(fight.EscapedBirds.Length == 2,
            "Encounter did not create two Escaped Birds.");

        var context = new ThrowingPlayerChoiceContext();
        await VerifyJudgementTargetPlan(fight, context);
        await VerifySinTransfer(fight, context);
        await VerifySinForecastPastCurrentHp(fight, context);
        await VerifyEscapedBirdTransfer(fight, context);
        VerifyEscapedBirdMoveCycles(fight);
        await VerifyWeightOfSinMultiplier(fight, context);
        await VerifyPageModes(fight, context);
        await VerifyPageRewardDedupe(fight, context);
        await VerifySinLethalThreshold(fight, context);
        await VerifyFullOfEvilDelay(fight, context);
    }

    private static async Task VerifyFullOfEvilDelay(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        var bossCreature = (LibraryCreature)fight.BossCreature;
        int chaoBeforeJudgement = bossCreature.CurrentChaoValue;
        fight.Boss.ScheduleFullOfEvil();

        Require(
            fight.Boss.FullOfEvilPending
            && bossCreature.CurrentChaoValue == chaoBeforeJudgement
            && !bossCreature.IsStunPending,
            "Judgement resolved Full of Evil before the next player turn.");

        JudgementBirdFullOfEvilPower power = bossCreature
            .GetPower<JudgementBirdFullOfEvilPower>()
            ?? throw new InvalidOperationException(
                "Judgement Bird Full of Evil power is missing.");
        await power.BeforeSideTurnStart(
            context,
            CombatSide.Player,
            fight.CombatState.PlayerCreatures,
            fight.CombatState);

        Require(
            !fight.Boss.FullOfEvilPending
            && bossCreature.CurrentChaoValue == 0
            && bossCreature.IsStunPending,
            "Full of Evil did not resolve once at the next player turn start.");
    }

    private static async Task VerifyJudgementTargetPlan(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        Creature[] candidates = fight.CombatState.Creatures
            .Where(JudgementBirdSinService
                .IsEncounterTransferableSinTarget)
            .OrderBy(static creature => creature.CombatId ?? uint.MaxValue)
            .ToArray();
        foreach (Creature candidate in candidates)
        {
            await ClearSin(candidate);
            await CreatureCmd.SetCurrentHp(candidate, 5);
            await JudgementBirdSinService.ApplyTransferable(
                context,
                candidate,
                6,
                fight.BossCreature,
                null);
        }

        Creature boundary = candidates[^1];
        await CreatureCmd.SetCurrentHp(boundary, 6);
        string moveBeforePlanning = fight.Boss.NextMove.Id;
        await fight.Boss.QueueJudgementIfRequired();
        Require(
            fight.Boss.PlannedJudgementTargetCombatIds.Length
                == candidates.Length - 1,
            "Judgement ignored the strict greater-than boundary.");
        Require(fight.Boss.NextMove.Id == moveBeforePlanning,
            "Judgement changed intent before the next move roll.");

        await JudgementBirdSinService.ApplyTransferable(
            context,
            boundary,
            1,
            fight.BossCreature,
            null);
        await fight.Boss.QueueJudgementIfRequired();
        int[] expectedIds = candidates
            .Select(CombatIdAsInt)
            .OrderBy(static id => id)
            .ToArray();
        Require(
            fight.Boss.PlannedJudgementTargetCombatIds
                .OrderBy(static id => id)
                .SequenceEqual(expectedIds),
            "Judgement did not include every qualifying creature.");
        (fight.Boss.MoveStateMachine
            ?? throw new InvalidOperationException(
                "Judgement Bird move state machine is missing."))
            .OnMovePerformed(fight.Boss.NextMove);
        fight.BossCreature.PrepareForNextTurn(
            fight.CombatState.PlayerCreatures);
        Require(fight.Boss.NextMove.Id == JudgementBird.JudgementMoveId,
            "Judgement router did not select Judgement on the next move roll.");

        JudgementBirdJudgementIntent intent = fight.Boss.NextMove.Intents
            .OfType<JudgementBirdJudgementIntent>()
            .Single();
        int[] lineTargetIds = intent.GetIntentTargetLineTargets(
                fight.BossCreature,
                null)
            .Select(static line => CombatIdAsInt(line.Target))
            .OrderBy(static id => id)
            .ToArray();
        Require(lineTargetIds.SequenceEqual(expectedIds),
            "Judgement intent lines diverged from execution targets.");

        foreach (Creature candidate in candidates)
        {
            await CreatureCmd.SetCurrentHp(candidate, candidate.MaxHp);
            await ClearSin(candidate);
        }
        await fight.Boss.QueueJudgementIfRequired();
    }

    private static async Task VerifySinForecastPastCurrentHp(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        Creature target = fight.Players[0].Creature;
        await ClearSin(target);
        await CreatureCmd.SetCurrentHp(target, 20);
        await JudgementBirdSinService.ApplyTransferable(
            context,
            target,
            70,
            fight.BossCreature,
            null);
        JudgementBirdSinPower power = target
            .GetPower<JudgementBirdSinPower>()
            ?? throw new InvalidOperationException(
                "Sin forecast test power was not applied.");
        HealthBarForecastSegment segment = power
            .GetHealthBarForecastSegments(new(target))
            .Single();
        HealthBarVisualGraftMetrics graft = power.GetHealthBarVisualGraft(
            new(target));
        Require(
            segment.Amount == 70
            && segment.Amount > target.CurrentHp
            && graft.GraftHp == 50,
            "Sin forecast was clipped to current HP.");

        NHealthBar healthBar = NCombatRoom.Instance
            ?.GetCreatureNode(target)
            ?.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar")
            ?.GetNodeOrNull<NHealthBar>("%HealthBar")
            ?? throw new InvalidOperationException(
                "Sin forecast test health bar is missing.");
        healthBar.RefreshValues();
        NinePatchRect overlay = healthBar.FindChild(
                JudgementBirdSinHealthBarForecastUi.OverlayNodeName,
                recursive: true,
                owned: false)
            as NinePatchRect
            ?? throw new InvalidOperationException(
                "Sin max-HP forecast overlay is missing.");
        Require(
            overlay.Visible
            && overlay.Size.X > overlay.GetParent<Control>().Size.X
                * target.CurrentHp / target.MaxHp,
            "Sin max-HP forecast overlay did not extend past current HP.");
        await CreatureCmd.SetCurrentHp(target, target.MaxHp);
        await ClearSin(target);
    }

    private static async Task VerifySinTransfer(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        Creature source = fight.EscapedBirds[0];
        Creature target = fight.Players[0].Creature;
        await ClearSin(source);
        await ClearSin(target);
        await JudgementBirdSinService.ApplyTransferable(
            context, source, 3, source, null);
        await JudgementBirdSinService.ApplyLocked(
            context, source, 4, source, null);
        int moved = await JudgementBirdSinService.Transfer(
            context, source, target, 5, source, null);
        Require(
            moved == 3
            && JudgementBirdSinService.GetTotal(source) == 4
            && JudgementBirdSinService.GetLocked(source) == 4
            && JudgementBirdSinService.GetTotal(target) == 3,
            "Transferable and locked Sin boundaries diverged.");
    }

    private static async Task VerifyEscapedBirdTransfer(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        Creature source = fight.EscapedBirds[0];
        Creature target = fight.Players[0].Creature;
        await ClearSin(source);
        await ClearSin(target);
        await JudgementBirdSinService.ApplyTransferable(
            context, source, 10, source, null);
        var result = new DamageResult(target, ValueProp.Move)
        {
            UnblockedDamage = 1
        };
        EscapedBirdScaryPower scary = source
            .GetPower<EscapedBirdScaryPower>()
            ?? throw new InvalidOperationException(
                "Escaped Bird scary power is missing.");
        JudgementBirdSinPower sinPower = source
            .GetPower<JudgementBirdSinPower>()
            ?? throw new InvalidOperationException(
                "Escaped Bird Sin power is missing.");
        await scary.AfterDamageGiven(
            context, source, result, ValueProp.Move, target, null);
        await sinPower.AfterDamageGiven(
            context, source, result, ValueProp.Move, target, null);
        Require(
            JudgementBirdSinService.GetTotal(source) == 6
            && JudgementBirdSinService.GetTotal(target) == 4,
            "Low-ascension Escaped Bird did not transfer 3+1 Sin.");
    }

    private static void VerifyEscapedBirdMoveCycles(
        JudgementBirdFight fight)
    {
        VerifyEscapedBirdMoveCycle(
            fight,
            JudgementBirdElite.LeftEscapedBirdSlot,
            EscapedBird.PanicMoveId,
            EscapedBird.ScreamMoveId);
        VerifyEscapedBirdMoveCycle(
            fight,
            JudgementBirdElite.RightEscapedBirdSlot,
            EscapedBird.ScreamMoveId,
            EscapedBird.PanicMoveId);
    }

    private static void VerifyEscapedBirdMoveCycle(
        JudgementBirdFight fight,
        string slotName,
        string firstMoveId,
        string secondMoveId)
    {
        LibraryCreature creature = fight.EscapedBirds.Single(
            escapedBird => escapedBird.SlotName == slotName);
        EscapedBird bird = (EscapedBird)creature.Monster!;
        MonsterMoveStateMachine machine = bird.MoveStateMachine
            ?? throw new InvalidOperationException(
                "Escaped Bird move state machine is missing.");

        Require(bird.NextMove.Id == firstMoveId,
            "Escaped Bird opening move cycle drifted for "
            + slotName + ".");
        machine.OnMovePerformed(bird.NextMove);
        creature.PrepareForNextTurn(fight.CombatState.PlayerCreatures);
        Require(bird.NextMove.Id == secondMoveId,
            "Escaped Bird second move cycle drifted for "
            + slotName + ".");
        machine.OnMovePerformed(bird.NextMove);
        creature.PrepareForNextTurn(fight.CombatState.PlayerCreatures);
        Require(bird.NextMove.Id == firstMoveId,
            "Escaped Bird loop did not return to its opening move for "
            + slotName + ".");
    }

    private static async Task VerifyWeightOfSinMultiplier(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        Creature target = fight.Players[0].Creature;
        await ClearSin(target);
        await JudgementBirdSinService.ApplyTransferable(
            context, target, 25, fight.BossCreature, null);
        JudgementBirdWeightOfSinPower power = fight.BossCreature
            .GetPower<JudgementBirdWeightOfSinPower>()
            ?? throw new InvalidOperationException(
                "Judgement Bird weight power is missing.");
        decimal multiplier = power.ModifyDamageMultiplicative(
            target,
            JudgementBird.GazeTwoLowDamage,
            ValueProp.Move,
            fight.BossCreature,
            null,
            null);
        Require(multiplier == 1.25m,
            "Weight of Sin did not use target Sin as a percentage.");
        await ClearSin(target);
    }

    private static async Task VerifyPageModes(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        Player owner = fight.Players[0];
        var relic = (JudgementBirdPageRelic)ModelDb
            .Relic<JudgementBirdPageRelic>()
            .ToMutable();
        owner.AddRelicInternal(relic, silent: true);
        Require(!relic.IsAllowed(owner.RunState),
            "Page relic can enter random relic generation.");

        SetMode(relic, JudgementBirdPageMode.WeightOfSin);
        await CreatureCmd.SetCurrentHp(owner.Creature, owner.Creature.MaxHp);
        await CreatureCmd.LoseBlock(
            context,
            owner.Creature,
            decimal.MaxValue,
            fight.BossCreature);
        CardModel attackCard = owner.RunState.CreateCard<StrikeIronclad>(
            owner);
        AttackCommand emptyAttack = DamageCmd.Attack(1)
            .FromCard(attackCard, null)
            .Targeting(fight.BossCreature);
        int hpBefore = owner.Creature.CurrentHp;
        await relic.AfterAttack(context, emptyAttack);
        Require(owner.Creature.CurrentHp == hpBefore - 1,
            "Weight of Sin did not deal one blockable self damage.");
        Require(owner.Creature.GetPower<LibraryStrongPower>()?.Amount == 5,
            "Weight of Sin did not grant five Strong.");

        SetMode(relic, JudgementBirdPageMode.TiltedScale);
        await CreatureCmd.SetCurrentHp(
            owner.Creature,
            owner.Creature.MaxHp - 5);
        await CreatureCmd.SetCurrentHp(
            fight.BossCreature,
            fight.BossCreature.MaxHp);
        int healBefore = owner.Creature.CurrentHp;
        await relic.BeforeDamageReceived(
            context,
            fight.BossCreature,
            1,
            ValueProp.Move,
            owner.Creature,
            attackCard);
        await relic.AfterDamageGiven(
            context,
            owner.Creature,
            new DamageResult(fight.BossCreature, ValueProp.Move)
            {
                BlockedDamage = 1
            },
            ValueProp.Move,
            fight.BossCreature,
            attackCard);
        Require(
            owner.Creature.CurrentHp == healBefore + 2
            && relic.TiltedScaleTriggersThisTurn == 1
            && relic.DisplayAmount == 2,
            "Tilted Scale heal or remaining counter drifted.");

        SetMode(relic, JudgementBirdPageMode.Judgement);
        LibraryCreature target = fight.EscapedBirds[0];
        await ClearSin(target);
        await CreatureCmd.SetCurrentHp(target, 10);
        await relic.AfterDamageGiven(
            context,
            owner.Creature,
            new DamageResult(target, ValueProp.Move)
            {
                BlockedDamage = 2,
                UnblockedDamage = 3
            },
            ValueProp.Move,
            target,
            attackCard);
        Require(
            JudgementBirdSinService.GetTotal(target) == 10
            && JudgementBirdSinService.GetLocked(target) == 10,
            "Judgement page did not apply twice the total hit damage as locked Sin.");
        await relic.AfterSideTurnEnd(
            context,
            owner.Creature.Side,
            fight.CombatState.PlayerCreatures);
        Require(
            target.CurrentChaoValue == 0
            && JudgementBirdSinService.GetTotal(target) == 5
            && JudgementBirdSinService.GetLocked(target) == 5,
            "Judgement page stun or floor-halving drifted.");
    }

    private static async Task VerifyPageRewardDedupe(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        await fight.Boss.AfterDeath(
            context,
            fight.BossCreature,
            wasRemovalPrevented: false,
            deathAnimLength: 0f);
        await fight.Boss.AfterDeath(
            context,
            fight.BossCreature,
            wasRemovalPrevented: false,
            deathAnimLength: 0f);
        ModelId relicId = ModelDb.GetId<JudgementBirdPageRelic>();
        foreach (Player player in fight.Players)
        {
            int count = fight.Room.ExtraRewards
                .GetValueOrDefault(player, [])
                .OfType<RelicReward>()
                .Count(reward => reward.Relic?.Id == relicId);
            Require(count == 1,
                "Page relic reward was not deduplicated for player "
                + player.NetId
                + ".");
        }
    }

    private static async Task VerifySinLethalThreshold(
        JudgementBirdFight fight,
        PlayerChoiceContext context)
    {
        Creature target = fight.EscapedBirds[1];
        await ClearSin(target);
        await CreatureCmd.SetCurrentHp(target, target.MaxHp);
        await JudgementBirdSinService.ApplyTransferable(
            context,
            target,
            target.MaxHp,
            fight.BossCreature,
            null);
        JudgementBirdSinPower power = target
            .GetPower<JudgementBirdSinPower>()
            ?? throw new InvalidOperationException(
                "Sin lethal test power was not applied.");
        await power.AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        Require(target.IsDead,
            "Sin equal to Max HP did not kill at side turn end.");
    }

    private static async Task<JudgementBirdFight> StartFight()
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
            "JUDGEMENTBIRDVERIFY");
        RunManager.Instance.SetUpNewSingleplayer(
            runState,
            shouldSave: false);
        ArmActLikeIt2OneShotVanillaEntry();

        MethodInfo startRun = typeof(NGame).GetMethod(
                "StartRun",
                PrivateInstance)
            ?? throw new MissingMethodException(
                typeof(NGame).FullName,
                "StartRun");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException(
                "NGame.StartRun did not return a Task.");
        await startRunTask;

        await RunManager.Instance.EnterRoomDebug(
            RoomType.Elite,
            MapPointType.Unassigned,
            ModelDb.Encounter<JudgementBirdElite>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "Judgement Bird combat start");
        await WaitUntil(
            () => players.All(player =>
                player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
            "Judgement Bird opening player turns");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        JudgementBird boss = combatState.Enemies
            .Select(static creature => creature.Monster)
            .OfType<JudgementBird>()
            .Single();
        LibraryCreature[] escapedBirds = combatState.Enemies
            .Where(static creature => creature.Monster is EscapedBird)
            .Cast<LibraryCreature>()
            .OrderBy(static creature => creature.CombatId ?? uint.MaxValue)
            .ToArray();
        CombatRoom room = runState.CurrentRoom as CombatRoom
            ?? throw new InvalidOperationException(
                "Current room is not the Judgement Bird combat room.");
        return new JudgementBirdFight(
            combatState,
            room,
            players,
            boss,
            boss.Creature as LibraryCreature
                ?? throw new InvalidOperationException(
                    "Judgement Bird is not a LibraryCreature."),
            escapedBirds);
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
            Log.Info(LogPrefix
                + "Armed ActLikeIt2 one-shot vanilla EnterAct gate.");
        }
    }

    private static async Task ClearSin(Creature creature)
    {
        if (creature.GetPower<JudgementBirdSinPower>() is { } visible)
        {
            await PowerCmd.Remove(visible);
        }

        if (creature.GetPower<JudgementBirdLockedSinMarkerPower>()
                is { } marker)
        {
            await PowerCmd.Remove(marker);
        }
    }

    private static void SetMode(
        JudgementBirdPageRelic relic,
        JudgementBirdPageMode mode)
    {
        MethodInfo method = typeof(JudgementBirdPageRelic).GetMethod(
                "SetMode",
                PrivateInstance)
            ?? throw new MissingMethodException(
                typeof(JudgementBirdPageRelic).FullName,
                "SetMode");
        method.Invoke(relic, [mode]);
    }

    private static void RequireAllResistances(
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
            Require(
                creature.GetPhysicalResistanceLevel(type) == expected
                && creature.GetChaosResistanceLevel(type) == expected,
                creature.Name
                + " resistance drifted for "
                + type
                + ".");
        }
    }

    private static int CombatIdAsInt(Creature creature) =>
        creature.CombatId is { } id && id <= int.MaxValue
            ? (int)id
            : throw new InvalidOperationException(
                "Creature has no stable combat ID: " + creature.Name + ".");

    private static bool ResourceExists(string path) =>
        ResourceLoader.Exists(path) || FileAccess.FileExists(path);

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

    private sealed record JudgementBirdFight(
        CombatState CombatState,
        CombatRoom Room,
        Player[] Players,
        JudgementBird Boss,
        LibraryCreature BossCreature,
        LibraryCreature[] EscapedBirds);
}
