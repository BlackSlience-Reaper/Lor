using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.BlueStar;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.BlueStar;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.BlueStar;
using LibraryOfRuina.powers.BlueStar;
using LibraryOfRuina.relics.BlueStar;
using LibraryOfRuina.visuals.BlueStar;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class BlueStarStrongVerificationPatch
{
    private const string VerifyArg = "lor-verify-blue-star";
    private const string AnimationVerifyArg =
        "lor-verify-blue-star-animation-duration";
    private const string LogPrefix = "[LibraryOfRuina.BlueStar.Verify] ";
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static bool _started;

    internal static void Start()
    {
        if (_started || (!HasVerifyArg() && !HasAnimationVerifyArg()))
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
        HasArg(VerifyArg);

    private static bool HasAnimationVerifyArg() =>
        HasArg(AnimationVerifyArg);

    private static bool HasArg(string expected) =>
        CommandLineHelper.HasArg(expected)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            expected,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            if (HasAnimationVerifyArg())
            {
                VerifyAnimationResourceContracts();
                await VerifyThresholdIntentRefresh();
                Log.Info(LogPrefix + "BLUE_STAR_ANIMATION_DURATION_OK");
                Log.Info(LogPrefix + "BLUE_STAR_THRESHOLD_INTENT_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyStaticContracts();
            await VerifyRuntimeContracts();
            Log.Info(LogPrefix + "BLUE_STAR_STRONG_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "BLUE_STAR_STRONG_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyAnimationResourceContracts()
    {
        AnimationLibrary normalAltar = LoadAnimationLibrary(
            BlueStarAltarCreatureVisuals.NormalAnimationsPath);
        AnimationLibrary novaAltar = LoadAnimationLibrary(
            BlueStarAltarCreatureVisuals.NovaAnimationsPath);
        AnimationLibrary follower = LoadAnimationLibrary(
            BlueStarFollowerCreatureVisuals.AnimationsPath);

        VerifyAnimationLength(
            normalAltar,
            "Idle",
            BlueStarAltarAnimationContract.IdleDurationSeconds);
        VerifyAnimationLength(
            normalAltar,
            "Nova",
            BlueStarAltarAnimationContract.NovaDurationSeconds);
        VerifyAnimationLength(
            novaAltar,
            "Idle",
            BlueStarAltarAnimationContract.IdleDurationSeconds);
        VerifyAnimationLength(
            novaAltar,
            "Nova",
            BlueStarAltarAnimationContract.NovaDurationSeconds);

        VerifyAnimationLength(
            follower,
            "Idle",
            BlueStarFollowerAnimationContract.IdleDurationSeconds);
        VerifyAnimationLength(
            follower,
            "BasicAttack",
            BlueStarFollowerAnimationContract.BasicAttackDurationSeconds);
        VerifyAnimationLength(
            follower,
            "VoiceAttack",
            BlueStarFollowerAnimationContract.VoiceAttackDurationSeconds);
        VerifyAnimationLength(
            follower,
            "Guard",
            BlueStarFollowerAnimationContract.GuardDurationSeconds);
        VerifyAnimationLength(
            follower,
            "Hit",
            BlueStarFollowerAnimationContract.HitDurationSeconds);
        VerifyAnimationLength(
            follower,
            "SelfDestruct",
            BlueStarFollowerAnimationContract.SelfDestructDurationSeconds);

        Animation voiceAttack = follower.GetAnimation("VoiceAttack")
            ?? throw new InvalidOperationException(
                "Missing follower VoiceAttack animation.");
        int multiKeyTrackCount = 0;
        for (int track = 0; track < voiceAttack.GetTrackCount(); track++)
        {
            if (voiceAttack.TrackGetKeyCount(track) < 2)
            {
                continue;
            }

            multiKeyTrackCount++;
            Require(
                Mathf.IsEqualApprox(
                    (float)voiceAttack.TrackGetKeyTime(track, 1),
                    0.40f),
                "VoiceAttack second-frame key was not doubled to 0.40s.");
        }

        Require(multiKeyTrackCount == 2,
            "VoiceAttack texture/position frame tracks are incomplete.");
    }

    private static AnimationLibrary LoadAnimationLibrary(string path) =>
        ResourceLoader.Load<AnimationLibrary>(path)
        ?? throw new InvalidOperationException(
            "Unable to load animation library " + path + ".");

    private static void VerifyAnimationLength(
        AnimationLibrary library,
        string animationName,
        float expectedSeconds)
    {
        Animation animation = library.GetAnimation(animationName)
            ?? throw new InvalidOperationException(
                "Missing animation " + animationName + ".");
        Require(
            Mathf.IsEqualApprox(animation.Length, expectedSeconds),
            animationName + " resource length mismatch: expected="
                + expectedSeconds + ", actual=" + animation.Length + ".");
    }

    private static async Task VerifyThresholdIntentRefresh()
    {
        await StartFight(ascensionLevel: 0);
        try
        {
            CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Combat state is null.");
            BlueStarFollower follower = combatState.Creatures
                .Select(static creature => creature.Monster)
                .OfType<BlueStarFollower>()
                .First();
            var creature = (LibraryCreature)follower.Creature;

            combatState.RoundNumber = 1;
            follower.ForceRefreshMoveState();
            string expectedNormalMove = BlueStarFollower.ResolveNormalMoveId(
                follower.Role,
                combatState.RoundNumber);
            Require(
                follower.NextMove.Id == expectedNormalMove,
                "Round-one follower did not begin on its normal cycle move.");

            int threshold = creature.MaxChaoValue
                * BlueStarFollower.SelfDestructThresholdPercent / 100;
            await LibraryCreatureCmd.SetCurrentChaoValue(
                creature,
                threshold);
            await WaitUntil(
                () => follower.NextMove.Id
                    == BlueStarFollower.SelfDestructMoveId,
                "immediate self-destruct intent refresh");
            VerifyAscensionDamageValues(
                BlueStarAltar.NovaLowAscensionDamage,
                BlueStarFollower.BasicAttackLowAscensionDamage,
                BlueStarFollower.VoiceAttackLowAscensionDamage,
                BlueStarFollower.SelfDestructLowAscensionDamage);
            VerifySelfDestructIntents(
                follower,
                combatState,
                BlueStarFollower.SelfDestructLowAscensionDamage);

            await LibraryCreatureCmd.SetCurrentChaoValue(
                creature,
                threshold + 1);
            await WaitUntil(
                () => follower.NextMove.Id == expectedNormalMove,
                "immediate normal-cycle intent restore");
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Blue Star threshold verifier cleanup");
        }

        await StartFight(
            ascensionLevel: (int)AscensionLevel.DeadlyEnemies);
        try
        {
            VerifyAscensionDamageValues(
                BlueStarAltar.NovaHighAscensionDamage,
                BlueStarFollower.BasicAttackHighAscensionDamage,
                BlueStarFollower.VoiceAttackHighAscensionDamage,
                BlueStarFollower.SelfDestructHighAscensionDamage);
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Blue Star high-ascension verifier cleanup");
        }
    }

    private static void VerifyAscensionDamageValues(
        int nova,
        int basicAttack,
        int voiceAttack,
        int selfDestruct)
    {
        Require(BlueStarAltar.NovaDamage == nova,
            "Nova damage does not match the current ascension tier.");
        Require(BlueStarFollower.BasicAttackDamage == basicAttack,
            "Basic attack damage does not match the current ascension tier.");
        Require(BlueStarFollower.VoiceAttackDamage == voiceAttack,
            "Voice attack damage does not match the current ascension tier.");
        Require(BlueStarFollower.SelfDestructDamage == selfDestruct,
            "Self-destruct damage does not match the current ascension tier.");
    }

    private static void VerifySelfDestructIntents(
        BlueStarFollower follower,
        CombatState combatState,
        int expectedDamage)
    {
        Require(follower.NextMove.Intents.Count == 2,
            "Self-destruct must expose exactly two intents.");
        DeathBlowIntent deathBlow = follower.NextMove.Intents
            .OfType<DeathBlowIntent>()
            .Single();
        DetailedDebuffIntent<VulnerablePower> vulnerable =
            follower.NextMove.Intents
                .OfType<DetailedDebuffIntent<VulnerablePower>>()
                .Single();
        Require(
            deathBlow.GetSingleDamage(
                combatState.PlayerCreatures,
                follower.Creature) == expectedDamage,
            "DeathBlowIntent damage does not match execution damage.");
        Require(vulnerable.Amount == BlueStarFollower.SelfDestructVulnerable,
            "Detailed vulnerable intent amount mismatch.");
    }

    private static void VerifyStaticContracts()
    {
        BlueStarStrong encounter = ModelDb.Encounter<BlueStarStrong>();
        Require(encounter.RoomType == RoomType.Monster, "Encounter is not a normal room.");
        Require(!encounter.IsWeak, "Encounter was registered as weak.");
        Require(encounter.HasScene, "Encounter scene is disabled.");
        Require(encounter.Slots.SequenceEqual(
            [
                BlueStarStrong.AltarSlot,
                BlueStarStrong.LeftFollowerSlot,
                BlueStarStrong.MiddleFollowerSlot,
                BlueStarStrong.RightFollowerSlot
            ]), "Encounter slots do not match the four-slot contract.");
        Require(ResourceLoader.Exists(BlueStarAltarCreatureVisuals.ScenePath),
            "Altar creature scene is missing.");
        Require(ResourceLoader.Exists(BlueStarFollowerCreatureVisuals.ScenePath),
            "Follower creature scene is missing.");
        Require(ResourceLoader.Exists(
                BlueStarAltarCreatureVisuals.NormalAnimationsPath)
                && ResourceLoader.Exists(
                    BlueStarAltarCreatureVisuals.NovaAnimationsPath),
            "Altar AnimationPlayer libraries are missing.");
        Require(ResourceLoader.Exists(
                BlueStarFollowerCreatureVisuals.AnimationsPath),
            "Follower AnimationPlayer library is missing.");

        BlueStarAltar altar = ModelDb.Monster<BlueStarAltar>();
        BlueStarFollower follower = ModelDb.Monster<BlueStarFollower>();
        Require(altar.MinInitialHp == 1000 && altar.MaxInitialHp == 1000,
            "Altar HP contract failed.");
        Require(altar.DefaultChaoResistance == 1000,
            "Altar Chao contract failed.");
        Require(follower.MinInitialHp == 150 && follower.MaxInitialHp == 150,
            "Follower HP contract failed.");
        Require(follower.DefaultChaoResistance == 120,
            "Follower Chao contract failed.");
        Require(BlueStarEncounterHelper.IsNovaRound(3)
                && BlueStarEncounterHelper.IsNovaRound(6)
                && !BlueStarEncounterHelper.IsNovaRound(2),
            "Nova round cadence failed.");
        Require(BlueStarFollower.ShouldSelfDestruct(24, 120),
            "Exactly 20 percent did not self-destruct.");
        Require(!BlueStarFollower.ShouldSelfDestruct(25, 120),
            "Above 20 percent still self-destructed.");

        string[][] expected =
        [
            [
                BlueStarFollower.ForTheStarMoveId,
                BlueStarFollower.FaithMoveId,
                BlueStarFollower.SinnersMoveId,
                BlueStarFollower.HearVoiceMoveId
            ],
            [
                BlueStarFollower.FaithMoveId,
                BlueStarFollower.ForTheStarMoveId,
                BlueStarFollower.HearVoiceMoveId,
                BlueStarFollower.SinnersMoveId
            ],
            [
                BlueStarFollower.HearVoiceMoveId,
                BlueStarFollower.SinnersMoveId,
                BlueStarFollower.ForTheStarMoveId,
                BlueStarFollower.FaithMoveId
            ]
        ];
        int[] normalRounds = [1, 2, 4, 5];
        for (int role = 0; role < expected.Length; role++)
        {
            for (int index = 0; index < normalRounds.Length; index++)
            {
                Require(
                    BlueStarFollower.ResolveNormalMoveId(
                        (BlueStarFollowerRole)role,
                        normalRounds[index]) == expected[role][index],
                    $"Follower cycle mismatch role={role} index={index}.");
            }
        }

        Require(
            typeof(BlueStarPageRelic).GetProperty(nameof(BlueStarPageRelic.Mode))
                ?.GetCustomAttribute<SavedPropertyAttribute>() != null,
            "Page Mode is not a SavedProperty.");
        Require(
            typeof(BlueStarPageRelic).GetProperty(nameof(BlueStarPageRelic.VoiceTurnsSeen))
                ?.GetCustomAttribute<SavedPropertyAttribute>() != null,
            "Voice counter is not a SavedProperty.");
        Require(
            SavedPropertiesTypeCacheCompat.ModSavedPropertyTypes.Contains(
                typeof(BlueStarPageRelic)),
            "Page relic was omitted from the stable SavedProperty table.");
    }

    private static async Task VerifyRuntimeContracts()
    {
        Player player = await StartFight();
        try
        {
            CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Combat state is null.");
            Creature altarCreature = combatState.Creatures.Single(
                static creature => creature.Monster is BlueStarAltar);
            BlueStarFollower[] followers = combatState.Creatures
                .Where(static creature => creature.Monster is BlueStarFollower)
                .OrderBy(static creature =>
                    BlueStarEncounterHelper.FollowerSlotOrder(creature.SlotName))
                .Select(static creature => (BlueStarFollower)creature.Monster!)
                .ToArray();
            Require(followers.Length == 3, "Runtime follower count is not three.");
            FieldInfo stateCacheField = typeof(BlueStarFollower).GetField(
                    "_statesById",
                    PrivateInstance)
                ?? throw new MissingFieldException(
                    nameof(BlueStarFollower),
                    "_statesById");
            FieldInfo onPerformField = typeof(MoveState).GetField(
                    "_onPerform",
                    PrivateInstance)
                ?? throw new MissingFieldException(
                    nameof(MoveState),
                    "_onPerform");
            var stateCaches = followers
                .Select(follower =>
                    stateCacheField.GetValue(follower)
                        as Dictionary<string, MoveState>
                    ?? throw new InvalidOperationException(
                        "Blue Star follower state cache is unavailable."))
                .ToArray();
            Require(
                !ReferenceEquals(stateCaches[0], stateCaches[1])
                && !ReferenceEquals(stateCaches[0], stateCaches[2])
                && !ReferenceEquals(stateCaches[1], stateCaches[2]),
                "Blue Star followers share a mutable MoveState cache.");
            for (int index = 0; index < followers.Length; index++)
            {
                Require(stateCaches[index].Values.All(state =>
                        onPerformField.GetValue(state) is Delegate callback
                        && ReferenceEquals(callback.Target, followers[index])),
                    "Blue Star follower MoveState delegates are bound to another clone.");
            }
            Require(altarCreature.CurrentHp == 1000,
                "Runtime altar HP was scaled or initialized incorrectly.");
            Require(altarCreature is LibraryCreature { MaxChaoValue: 1000 },
                "Runtime altar Chao was initialized incorrectly.");
            Require(altarCreature.GetPower<BlueStarDivinePower>() != null
                    && altarCreature.GetPower<BlueStarNovaVoicePower>() != null
                    && altarCreature.GetPower<BlueStarReturnToStarsPower>() != null
                    && altarCreature.GetPower<BlueStarMartyrPower>() != null,
                "Altar passive powers are incomplete.");
            var altarVisuals =
                NCombatRoom.Instance?.GetCreatureNode(altarCreature)?.Visuals
                    as BlueStarAltarCreatureVisuals
                ?? throw new InvalidOperationException(
                    "Altar runtime visuals are not scene-backed visuals.");
            Require(
                altarVisuals.AnimationPlayer.HasAnimation("normal/Idle")
                && altarVisuals.AnimationPlayer.HasAnimation("normal/Nova")
                && altarVisuals.AnimationPlayer.HasAnimation("nova/Idle")
                && altarVisuals.AnimationPlayer.HasAnimation("nova/Nova"),
                "Altar AnimationPlayer contract is incomplete.");
            VerifyAnimationLength(
                altarVisuals.AnimationPlayer,
                "normal/Idle",
                BlueStarAltarAnimationContract.IdleDurationSeconds);
            VerifyAnimationLength(
                altarVisuals.AnimationPlayer,
                "normal/Nova",
                BlueStarAltarAnimationContract.NovaDurationSeconds);
            VerifyAnimationLength(
                altarVisuals.AnimationPlayer,
                "nova/Idle",
                BlueStarAltarAnimationContract.IdleDurationSeconds);
            VerifyAnimationLength(
                altarVisuals.AnimationPlayer,
                "nova/Nova",
                BlueStarAltarAnimationContract.NovaDurationSeconds);

            LibraryDamageType[] fatalTypes =
            [
                LibraryDamageType.Slash,
                LibraryDamageType.Pierce,
                LibraryDamageType.Blunt
            ];
            for (int index = 0; index < followers.Length; index++)
            {
                var creature = (LibraryCreature)followers[index].Creature;
                var followerVisuals =
                    NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals
                        as BlueStarFollowerCreatureVisuals
                    ?? throw new InvalidOperationException(
                        "Follower runtime visuals are not scene-backed visuals.");
                Require(
                    followerVisuals.AnimationPlayer.HasAnimation(
                        "follower/Idle")
                    && followerVisuals.AnimationPlayer.HasAnimation(
                        "follower/BasicAttack")
                    && followerVisuals.AnimationPlayer.HasAnimation(
                        "follower/VoiceAttack")
                    && followerVisuals.AnimationPlayer.HasAnimation(
                        "follower/Guard")
                    && followerVisuals.AnimationPlayer.HasAnimation(
                        "follower/Hit")
                    && followerVisuals.AnimationPlayer.HasAnimation(
                        "follower/SelfDestruct"),
                    "Follower AnimationPlayer contract is incomplete.");
                VerifyAnimationLength(
                    followerVisuals.AnimationPlayer,
                    "follower/Idle",
                    BlueStarFollowerAnimationContract.IdleDurationSeconds);
                VerifyAnimationLength(
                    followerVisuals.AnimationPlayer,
                    "follower/BasicAttack",
                    BlueStarFollowerAnimationContract
                        .BasicAttackDurationSeconds);
                VerifyAnimationLength(
                    followerVisuals.AnimationPlayer,
                    "follower/VoiceAttack",
                    BlueStarFollowerAnimationContract
                        .VoiceAttackDurationSeconds);
                VerifyAnimationLength(
                    followerVisuals.AnimationPlayer,
                    "follower/Guard",
                    BlueStarFollowerAnimationContract.GuardDurationSeconds);
                VerifyAnimationLength(
                    followerVisuals.AnimationPlayer,
                    "follower/Hit",
                    BlueStarFollowerAnimationContract.HitDurationSeconds);
                VerifyAnimationLength(
                    followerVisuals.AnimationPlayer,
                    "follower/SelfDestruct",
                    BlueStarFollowerAnimationContract
                        .SelfDestructDurationSeconds);
                Require(creature.MaxChaoValue == 120,
                    "Follower max Chao was not 120.");
                Require(creature.GetChaosResistanceLevel(fatalTypes[index])
                        == LibraryResistanceLevel.Fatal,
                    "Follower role-specific fatal Chao resistance failed.");
                Require(IsPhysicalNormal(creature),
                    "Follower physical resistance was not initially Normal.");
            }

            LibraryCreature left = (LibraryCreature)followers[0].Creature;
            left.SaveAndSetStunResistance();
            Require(IsPhysicalNormal(left),
                "Follower physical resistance changed during stagger.");
            left.RestorePreStunResistance();
            await LibraryCreatureCmd.SetCurrentChaoValue(left, left.MaxChaoValue);

            LibraryCreature middle = (LibraryCreature)followers[1].Creature;
            await LibraryCreatureCmd.SetCurrentChaoValue(middle, 0m);
            await WaitUntil(
                () => middle.IsDead && altarCreature.CurrentHp == 900,
                "follower return-to-stars resolution");
            Require(BlueStarEncounterHelper.LivingFollowers(combatState).Count == 2,
                "Staggered follower was not removed.");

            BlueStarMartyrPower martyr =
                altarCreature.GetPower<BlueStarMartyrPower>()
                ?? throw new InvalidOperationException("Martyr power is missing.");
            await martyr.BeforeSideTurnStart(
                new ThrowingPlayerChoiceContext(),
                CombatSide.Player,
                combatState.Creatures,
                combatState);
            await WaitUntil(
                () => BlueStarEncounterHelper.LivingFollowers(combatState).Count == 3,
                "single follower resummon");

            BlueStarMartyrdomCard card =
                player.RunState.CreateCard<BlueStarMartyrdomCard>(player);
            Require(card.DynamicVars["ChaoDamage"].IntValue == 4,
                "Martyrdom base Chao damage failed.");
            CardCmd.Upgrade(card);
            Require(card.DynamicVars["ChaoDamage"].IntValue == 6,
                "Martyrdom upgrade Chao damage failed.");
            Require(card.Pool is TokenCardPool,
                "Martyrdom card was not registered in TokenCardPool.");

            var relic = (BlueStarPageRelic)ModelDb.Relic<BlueStarPageRelic>()
                .ToMutable();
            relic.Owner = player;
            SetMode(relic, BlueStarPageMode.VoiceOfRemembrance);
            await relic.AfterPlayerTurnStart(
                new ThrowingPlayerChoiceContext(), player);
            Require(relic.DisplayAmount == 1, "Voice counter turn one failed.");
            await relic.AfterPlayerTurnStart(
                new ThrowingPlayerChoiceContext(), player);
            Require(relic.DisplayAmount == 2, "Voice counter turn two failed.");
            await relic.AfterPlayerTurnStart(
                new ThrowingPlayerChoiceContext(), player);
            Require(relic.DisplayAmount == 3, "Voice counter trigger failed.");

            SetMode(relic, BlueStarPageMode.Atonement);
            decimal multiplier = relic.ModifyChaoDamageMultiplicative(
                altarCreature,
                10m,
                ValueProp.Move,
                player.Creature,
                card,
                null,
                LibraryDamageType.None);
            Require(multiplier == 1.10m,
                "Atonement multiplier failed.");
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Blue Star verifier cleanup");
        }
    }

    private static async Task<Player> StartFight(int ascensionLevel = 0)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        Player player = Player.CreateForNewRun(
            ModelDb.Character<Ironclad>(),
            SaveManager.Instance.GenerateUnlockStateFromProgress(),
            1uL);
        RunState runState = RunState.CreateForNewRun(
            [player],
            ActModel.GetDefaultList()
                .Select(static act => act.ToMutable())
                .ToList(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            ascensionLevel,
            "BLUESTARVERIFY" + ascensionLevel);
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);
        ArmActLikeIt2OneShotVanillaEntry();

        MethodInfo startRun = typeof(NGame).GetMethod(
                "StartRun",
                PrivateInstance)
            ?? throw new InvalidOperationException("NGame.StartRun is unavailable.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException("NGame.StartRun did not return Task.");
        await startRunTask;

        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<BlueStarStrong>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "Blue Star combat start");
        return player;
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
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
        }
    }

    private static void SetMode(
        BlueStarPageRelic relic,
        BlueStarPageMode mode)
    {
        MethodInfo setter = typeof(BlueStarPageRelic).GetMethod(
                "SetMode",
                PrivateInstance)
            ?? throw new MissingMethodException(
                typeof(BlueStarPageRelic).FullName,
                "SetMode");
        setter.Invoke(relic, [mode]);
    }

    private static bool IsPhysicalNormal(LibraryCreature creature) =>
        creature.GetPhysicalResistanceLevel(LibraryDamageType.Slash)
            == LibraryResistanceLevel.Normal
        && creature.GetPhysicalResistanceLevel(LibraryDamageType.Pierce)
            == LibraryResistanceLevel.Normal
        && creature.GetPhysicalResistanceLevel(LibraryDamageType.Blunt)
            == LibraryResistanceLevel.Normal;

    private static void VerifyAnimationLength(
        AnimationPlayer player,
        string animationName,
        float expectedSeconds)
    {
        Animation animation = player.GetAnimation(animationName)
            ?? throw new InvalidOperationException(
                "Missing animation " + animationName + ".");
        Require(
            Mathf.IsEqualApprox(animation.Length, expectedSeconds),
            animationName + " length mismatch: expected="
                + expectedSeconds + ", actual=" + animation.Length + ".");
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
