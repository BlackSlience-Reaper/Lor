using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.acts;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.content.liberation.Philosophy;
using LibraryOfRuina.content.liberation.Social;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.features.ftue;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;
using VoidCard = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace LibraryOfRuinaVerification;

internal static class PhilosophyFloorLiberationVerificationPatch
{
    private const string VerifyArg = "lor-verify-philosophy-floor";
    private const string ChaosBreakVerifyArg =
        "lor-verify-philosophy-floor-chaos-break";
    private const string LogPrefix =
        "[LibraryOfRuina.PhilosophyFloor.Verify] ";
    private const string ExpectedSourceIconSha256 =
        "EEDD560CEBD904E155F6F68AB6DCFCC0B2BC1155723B3991D2E65EABF24DAF28";
    private const string ExpectedIconPixelSha256 =
        "F6AA8A266F24B3C3E17A1337872BB91C18C3D1A9275407884B89E724DB9AE475";
    private const string ExpectedGreenPassiveIconSha256 =
        "5CAB0AAF5F932C9926865F1B5D4F137CAEC26A9B4BD6089B3BD5A098435A72D7";
    private const string ExpectedFearIconSha256 =
        "04D2E8C7386B8AAEE8A1D4B6E557983F18C8970D58E7FBEC1C443864BA1BDBE5";
    private const string TwilightAnimationLibraryPath =
        "res://scenes/creature_visuals/philosophy_floor_twilight_animations.tres";
    private const string TwilightAttackVisualTrackPrefix =
        "MotionRoot/AttackFrameNormalizer/AttackVisuals:";

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PrivateStatic =
        BindingFlags.Static | BindingFlags.NonPublic;

    private static bool _started;

    internal static void Start()
    {
        bool verifyChaosBreak = HasArg(ChaosBreakVerifyArg);
        if (_started || (!verifyChaosBreak && !HasArg(VerifyArg)))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(
                verifyChaosBreak ? RunChaosBreakAsync() : RunAsync());
        }).CallDeferred();
    }

    private static bool HasArg(string argument) =>
        CommandLineHelper.HasArg(argument)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            argument,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunChaosBreakAsync()
    {
        try
        {
            PhilosophyFloorCombatContext fight = await StartFight(
                "PHILOSOPHYFLOORVERIFY_CHAOS_BREAK");
            try
            {
                await WaitUntil(
                    () => fight.Player.Player?.PlayerCombatState?.Phase
                        == PlayerTurnPhase.Play,
                    "Philosophy floor chaos-break opening draw");
                await VerifyPreStunnedChaosDepletionBreaksEgg(fight);
            }
            finally
            {
                RunManager.Instance.CleanUp(graceful: true);
                await WaitUntil(
                    static () =>
                        RunManager.Instance.DebugOnlyGetState() == null,
                    "Philosophy floor chaos-break verifier cleanup");
            }

            Log.Info(LogPrefix + "PHILOSOPHY_FLOOR_CHAOS_BREAK_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(
                LogPrefix + "PHILOSOPHY_FLOOR_CHAOS_BREAK_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunAsync()
    {
        try
        {
            VerifyRegistrationAndActRouting();
            VerifyEncounterContractAndResources();
            VerifyOrdinaryIntentContracts();
            VerifyThreeBirdsDescriptionRouting();
            VerifyBigEyesArtifactBypass();
            VerifyEyeProjectileRoutingContract();
            VerifyTwilightAnimationResourceContract();
            VerifyEggPrioritySchedule();
            VerifyEncounterStateRoundTrip();
            VerifyAiBranchFallbacks();
            await VerifyAscensionValues();
            await VerifyRuntimeStateMachineAndActions();
            await VerifyFakeMultiplayerRuntime();

            Log.Info(LogPrefix + "PHILOSOPHY_FLOOR_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "PHILOSOPHY_FLOOR_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyOrdinaryIntentContracts()
    {
        PhilosophyFloorTwilight boss = CreateDetachedBoss();
        IReadOnlyDictionary<PhilosophyFloorTwilightAction, AbstractIntent>
            actionIntents =
            Enum.GetValues<PhilosophyFloorTwilightAction>()
                .ToDictionary(
                    static action => action,
                    action => boss.DebugCreateActionIntent(action));

        Require(actionIntents.Values.All(static intent =>
                intent is not CombinedMagicIntent),
            "Twilight still exposes a magic intent icon.");

        Require(actionIntents[PhilosophyFloorTwilightAction.SlamDown]
                is CombinedAttackDefendIntent
                {
                    Repeats: 1,
                    BlockAmount: 13
                },
            "Slam Down intent did not show one attack plus 13 Block.");
        Require(actionIntents[PhilosophyFloorTwilightAction.Talon]
                is MultiAttackIntent
                {
                    Repeats: 3
                },
            "Talon intent was not an ordinary three-hit attack.");

        Require(actionIntents[PhilosophyFloorTwilightAction.Prowl]
                is CombinedDefendBuffIntent
                {
                    BlockAmount: 16
                },
            "Prowl intent did not show 16 Block plus a buff.");
        RequirePowerEffects(
            actionIntents[PhilosophyFloorTwilightAction.Prowl],
            (typeof(LibraryStrongPower), 1));

        Require(actionIntents[
                    PhilosophyFloorTwilightAction.ProtectBlackForest]
                is CombinedAttackDebuffIntent { Repeats: 1 },
            "Protect Black Forest intent was not attack plus debuffs.");
        RequirePowerEffects(
            actionIntents[
                PhilosophyFloorTwilightAction.ProtectBlackForest],
            (typeof(FrailPower), 2),
            (typeof(WeakPower), 2));

        Require(actionIntents[PhilosophyFloorTwilightAction.TornMouth]
                is CombinedAttackDebuffIntent { Repeats: 3 },
            "Torn Mouth intent did not show three attack hits.");
        RequirePowerEffects(
            actionIntents[PhilosophyFloorTwilightAction.TornMouth],
            (typeof(LibraryBleedingPower), 3));

        Require(actionIntents[PhilosophyFloorTwilightAction.TiltedScale]
                is CombinedAttackDebuffIntent { Repeats: 1 },
            "Tilted Scale intent was not attack plus Sin.");
        RequirePowerEffects(
            actionIntents[PhilosophyFloorTwilightAction.TiltedScale],
            (typeof(PhilosophyFloorTwilightSinPower), 3));

        Require(
            actionIntents[PhilosophyFloorTwilightAction.ForestLight]
                is PhilosophyFloorTwilightTargetedDebuffIntent,
            "Forest Light is not presented as an ordinary targeted debuff.");
        RequirePowerEffects(
            actionIntents[PhilosophyFloorTwilightAction.ForestLight],
            (typeof(LibraryBindingPower), 6),
            (typeof(LibraryOfRuinaConfusionPower), 1));

        Require(actionIntents[PhilosophyFloorTwilightAction.Punishment]
                is PhilosophyFloorTwilightPunishmentIntent
                {
                    Repeats: 4,
                    HealPercent: 10
                },
            "Punishment intent did not show four attacks plus healing.");
        Require(actionIntents[PhilosophyFloorTwilightAction.Punishment]
                    is IIntentEffectProvider
                    {
                        Effects: [{ Kind: IntentBadgeKind.Heal }]
                    },
            "Punishment intent did not show its healing effect.");

        Require(
            actionIntents[PhilosophyFloorTwilightAction.BrilliantEyes]
                is TargetedDetailedStatusCardIntent<VoidCard>
                {
                    CardCount: 2
                }
                && actionIntents[PhilosophyFloorTwilightAction.BrilliantEyes]
                    is not IGroupAttackIntent,
            "Brilliant Eyes lost its targeted status-card intent or gained "
            + "a group-attack marker.");
        Require(actionIntents[PhilosophyFloorTwilightAction.PeaceForAll]
                is IndiscriminateAttackIntent
                {
                    Repeats: 1,
                    IsGroupAttack: true
                },
            "Peace for All intent did not use the indiscriminate attack.");

        Require(
            actionIntents[PhilosophyFloorTwilightAction.Surveillance]
                is PhilosophyFloorTwilightTargetedDebuffIntent
                {
                    Effects:
                    [
                        {
                            IsVisible: false,
                            HasExtraHoverTips: true,
                            PowerType: not null
                        } fearHoverTip,
                        {
                            PowerType: not null,
                            Amount: 2
                        } strengthBadge
                    ]
                }
                && fearHoverTip.PowerType
                    == typeof(PhilosophyFloorTwilightFearPower)
                && strengthBadge.PowerType == typeof(StrengthPower)
                && actionIntents[PhilosophyFloorTwilightAction.Surveillance]
                    is not IBadgedIntent,
            "Surveillance did not expose hidden Fear plus 2 Strength.");

        Require(actionIntents[PhilosophyFloorTwilightAction.Judgment]
                is PhilosophyFloorTwilightJudgmentIntent { Repeats: 1 },
            "Judgment is not a one-hit attack intent.");
        Require(actionIntents[PhilosophyFloorTwilightAction.Judgment]
                    is IndiscriminateAttackIntent
                    {
                        IsGroupAttack: true
                    }
                && actionIntents[PhilosophyFloorTwilightAction.Judgment]
                    is IBadgedIntent,
            "Judgment did not use the indiscriminate group-attack intent.");
        Require(!GetPrivateStaticBool(
                "RequiresPlannedPlayerTargets",
                PhilosophyFloorTwilightAction.Judgment),
            "Judgment incorrectly reserves a non-attack target selector.");

        PhilosophyFloorTwilightAction[] ordinaryAttacks =
        [
            PhilosophyFloorTwilightAction.SlamDown,
            PhilosophyFloorTwilightAction.Talon,
            PhilosophyFloorTwilightAction.ProtectBlackForest,
            PhilosophyFloorTwilightAction.TornMouth,
            PhilosophyFloorTwilightAction.TiltedScale,
            PhilosophyFloorTwilightAction.Punishment
        ];
        Require(ordinaryAttacks.All(action =>
                actionIntents[action] is AttackIntent
                && actionIntents[action] is not IndiscriminateAttackIntent
                && actionIntents[action]
                    is not IGroupAttackIntent { IsGroupAttack: true }),
            "An ordinary Twilight attack gained an indiscriminate marker.");
        PhilosophyFloorTwilightAction[] indiscriminateAttacks =
        [
            PhilosophyFloorTwilightAction.PeaceForAll,
            PhilosophyFloorTwilightAction.Judgment
        ];
        Require(indiscriminateAttacks.All(action =>
                actionIntents[action] is IndiscriminateAttackIntent
                && actionIntents[action]
                    is IGroupAttackIntent { IsGroupAttack: true }),
            "A declared Twilight group attack lost indiscriminate semantics.");

        int[] expectedTargetCounts = [0, 1, 1, 2, 4, 4, 5];
        Require(Enumerable.Range(0, expectedTargetCounts.Length)
                .All(playerCount =>
                    PhilosophyFloorTwilight.ResolveNonAttackTargetCount(
                        playerCount) == expectedTargetCounts[playerCount]),
            "Twilight non-attack target-count mapping is incorrect.");
    }

    private static void VerifyThreeBirdsDescriptionRouting()
    {
        (PhilosophyFloorTwilightEgg Egg, string Suffix)[] cases =
        [
            (PhilosophyFloorTwilightEgg.BigEyes, "bigEyes"),
            (PhilosophyFloorTwilightEgg.SmallBeak, "smallBeak"),
            (PhilosophyFloorTwilightEgg.LongArms, "longArms"),
            (PhilosophyFloorTwilightEgg.None, "none")
        ];
        foreach ((PhilosophyFloorTwilightEgg egg, string suffix) in cases)
        {
            string actual = PhilosophyFloorTwilightThreeBirdsPower
                .ResolveSmartDescriptionLocKey(egg);
            string expected =
                "PHILOSOPHY_FLOOR_TWILIGHT_THREE_BIRDS_POWER"
                + $".smartDescription.{suffix}";
            Require(string.Equals(actual, expected,
                    StringComparison.Ordinal),
                $"Three Birds description did not follow {egg}: "
                + $"actual={actual} expected={expected}.");
        }
    }

    private static void VerifyBigEyesArtifactBypass()
    {
        Require(!PhilosophyFloorTwilightBigEyesHookSuspension
                .ShouldSuspendPower(new ArtifactPower()),
            "Big Eyes incorrectly disables Artifact.");
        Require(PhilosophyFloorTwilightBigEyesHookSuspension
                .ShouldSuspendPower(new StrengthPower()),
            "Big Eyes no longer disables ordinary positive powers.");
    }

    private static void VerifyEyeProjectileRoutingContract()
    {
        Require(PhilosophyFloorLiberationBackgroundController.EndBirdEyeCount
                == 16,
            "Apocalypse Bird eye projectiles no longer cover all 16 eyes.");
        Require(
            PhilosophyFloorLiberationVfx.ResolveEyeCurveTargetIndex(0, 2)
                == 0
            && PhilosophyFloorLiberationVfx.ResolveEyeCurveTargetIndex(1, 2)
                == 1
            && PhilosophyFloorLiberationVfx.ResolveEyeCurveTargetIndex(2, 2)
                == 0
            && PhilosophyFloorLiberationVfx.ResolveEyeCurveTargetIndex(15, 2)
                == 1
            && PhilosophyFloorLiberationVfx.ResolveEyeCurveTargetIndex(15, 1)
                == 0
            && PhilosophyFloorLiberationVfx.ResolveEyeCurveTargetIndex(0, 0)
                == -1,
            "Eye projectiles no longer route independently to saved players.");
    }

    private static void VerifyTwilightAnimationResourceContract()
    {
        foreach (string animationName in new[] { "F", "S3" })
        {
            Require(Mathf.IsEqualApprox(
                    PhilosophyFloorTwilightCreatureVisuals
                        .ResolveAttackFrameNormalization(animationName),
                    2f),
                $"Twilight {animationName} lost its exact 50-to-100 PPU "
                + "normalization.");
        }

        foreach (string animationName in new[]
                 {
                     "Default", "G", "Hit", "J", "Z", "S1", "S2", "S4",
                     "S5"
                 })
        {
            Require(Mathf.IsEqualApprox(
                    PhilosophyFloorTwilightCreatureVisuals
                        .ResolveAttackFrameNormalization(animationName),
                    1f),
                $"Twilight {animationName} was incorrectly normalized.");
        }

        foreach (float normalization in new[] { 1f, 2f })
        {
            Vector2 compensatedPivot =
                PhilosophyFloorTwilightCreatureVisuals
                    .ResolveAttackFrameNormalizerPosition(normalization)
                + PhilosophyFloorTwilightCreatureVisuals.AnimationRootPivot
                * normalization;
            Require(compensatedPivot.DistanceSquaredTo(
                        PhilosophyFloorTwilightCreatureVisuals
                            .AnimationRootPivot) < 0.0001f,
                $"Twilight frame normalization {normalization} drifts away "
                + "from the shared Unity root.");
        }

        AnimationLibrary library = ResourceLoader.Load<AnimationLibrary>(
                TwilightAnimationLibraryPath)
            ?? throw new InvalidOperationException(
                "Could not load Twilight animation library.");
        int attackVisualTrackCount = 0;
        foreach (StringName animationName in library.GetAnimationList())
        {
            Animation animation = library.GetAnimation(animationName)
                ?? throw new InvalidOperationException(
                    "Missing Twilight animation: " + animationName);
            for (int track = 0; track < animation.GetTrackCount(); track++)
            {
                string trackPath = animation.TrackGetPath(track).ToString();
                if (!trackPath.Contains("AttackVisuals",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                attackVisualTrackCount++;
                Require(trackPath.StartsWith(
                        TwilightAttackVisualTrackPrefix,
                        StringComparison.Ordinal),
                    $"Twilight animation {animationName} bypasses the "
                    + $"frame normalizer: {trackPath}");
            }
        }
        Require(attackVisualTrackCount > 0,
            "Twilight animation library has no AttackVisuals tracks.");

        Image defaultImage = LoadTextureImage(
            PhilosophyFloorTwilightCreatureVisuals.DefaultTexturePath);
        Image forestLightImage = LoadTextureImage(
            PhilosophyFloorTwilightCreatureVisuals.ForestLightTexturePath);
        Image punishmentFollowupImage = LoadTextureImage(
            PhilosophyFloorTwilightCreatureVisuals
                .PunishmentFollowupTexturePath);
        int defaultVisibleHeight = GetVisibleAlphaHeight(defaultImage);
        int forestLightVisibleHeight = GetVisibleAlphaHeight(forestLightImage);
        int punishmentFollowupVisibleHeight =
            GetVisibleAlphaHeight(punishmentFollowupImage);
        Require(defaultVisibleHeight == 1015
                && forestLightVisibleHeight == 488
                && punishmentFollowupVisibleHeight == 587,
            "Twilight animation alpha bounds changed: "
            + $"Default={defaultVisibleHeight}, F={forestLightVisibleHeight}, "
            + $"S3={punishmentFollowupVisibleHeight}.");
        Require(forestLightVisibleHeight
                * PhilosophyFloorTwilightCreatureVisuals
                    .ResolveAttackFrameNormalization("F")
                >= defaultVisibleHeight * 0.9f,
            "Twilight F remains especially small after PPU normalization.");
        Require(punishmentFollowupVisibleHeight
                * PhilosophyFloorTwilightCreatureVisuals
                    .ResolveAttackFrameNormalization("S3")
                >= defaultVisibleHeight * 0.9f,
            "Twilight S3 remains especially small after PPU normalization.");

        Image hitImage = LoadTextureImage(
            PhilosophyFloorTwilightCreatureVisuals.HitTexturePath);
        Image penetrateImage = LoadTextureImage(
            PhilosophyFloorTwilightCreatureVisuals.PenetrateTexturePath);
        Require(hitImage.GetWidth() == 1111
                && hitImage.GetHeight() == 1126,
            "Twilight Hit texture dimensions changed: "
            + $"actual={hitImage.GetWidth()}x{hitImage.GetHeight()}.");
        Require(hitImage.GetWidth() != penetrateImage.GetWidth()
                || hitImage.GetHeight() != penetrateImage.GetHeight(),
            "Twilight Hit texture reverted to the Z texture dimensions.");

        Require(Mathf.IsEqualApprox(
                PhilosophyFloorTwilight.ResolveAttackerAnimationDuration(
                    "Punishment"),
                0.58f),
            "Punishment attacker animation duration changed.");
        Require(Mathf.IsEqualApprox(
                PhilosophyFloorTwilight.ResolveAttackerAnimationDuration(
                    "PunishmentFollowup"),
                0.54f),
            "Punishment follow-up attacker animation duration changed.");
        Require(Mathf.IsEqualApprox(
                    library.GetAnimation("S2").Length,
                    0.58f)
                && Mathf.IsEqualApprox(
                    library.GetAnimation("S3").Length,
                    0.54f),
            "Twilight S2/S3 resource lengths no longer match attack timing.");
        Require(Mathf.IsEqualApprox(
                    PhilosophyFloorTwilight.AttackHitStopFastSeconds,
                    0.06f)
                && Mathf.IsEqualApprox(
                    PhilosophyFloorTwilight.AttackHitStopStandardSeconds,
                    0.12f),
            "Twilight per-segment attack hit stop changed.");
        Require(Mathf.IsEqualApprox(
                PhilosophyFloorLiberationVfx.TiltedScaleVisualScale,
                0.3f),
            "Twilight Sin effect is no longer half of its previous scale.");
        VerifyTwilightIdleFloat(library);
        VerifyTwilightAnimationVerticalStability(library);
        Require(
            PhilosophyFloorTwilight.ShouldPlayDefaultAttackSfx(
                hasCustomBeforeHit: false,
                playAttackerAnimations: true)
            && !PhilosophyFloorTwilight.ShouldPlayDefaultAttackSfx(
                hasCustomBeforeHit: true,
                playAttackerAnimations: true)
            && !PhilosophyFloorTwilight.ShouldPlayDefaultAttackSfx(
                hasCustomBeforeHit: false,
                playAttackerAnimations: false),
            "Twilight command-level attack SFX routing changed.");
    }

    private static void VerifyTwilightAnimationVerticalStability(
        AnimationLibrary library)
    {
        foreach (string animationName in new[]
                 {
                     "G", "Hit", "J", "Z", "F", "S1", "S2", "S3",
                     "S4", "S5"
                 })
        {
            Animation animation = library.GetAnimation(animationName)
                ?? throw new InvalidOperationException(
                    "Missing Twilight animation: " + animationName);
            int positionTrack = FindTwilightVisualTrack(
                animation,
                "AttackVisuals",
                "position");
            int scaleTrack = FindTwilightVisualTrack(
                animation,
                "AttackVisuals",
                "scale");
            Vector2 baselinePosition = animation
                .TrackGetKeyValue(positionTrack, 0)
                .AsVector2();
            for (int key = 1;
                 key < animation.TrackGetKeyCount(positionTrack);
                 key++)
            {
                Vector2 position = animation
                    .TrackGetKeyValue(positionTrack, key)
                    .AsVector2();
                Require(Mathf.IsEqualApprox(
                        position.Y,
                        baselinePosition.Y),
                    $"Twilight {animationName} still moves vertically: "
                    + $"key={key} y={position.Y} "
                    + $"baseline={baselinePosition.Y}.");
            }

            for (int key = 0;
                 key < animation.TrackGetKeyCount(scaleTrack);
                 key++)
            {
                Vector2 scale = animation
                    .TrackGetKeyValue(scaleTrack, key)
                    .AsVector2();
                Require(scale.DistanceSquaredTo(
                            new Vector2(0.42f, 0.42f)) < 0.0001f,
                    $"Twilight {animationName} still changes visual scale: "
                    + $"key={key} scale={scale}.");
            }
        }
    }

    private static void VerifyTwilightIdleFloat(AnimationLibrary library)
    {
        Animation animation = library.GetAnimation("Default")
            ?? throw new InvalidOperationException(
                "Missing Twilight idle animation.");
        int positionTrack = FindTwilightVisualTrack(
            animation,
            "Visuals",
            "position");
        Require(animation.TrackGetKeyCount(positionTrack) == 3,
            "Twilight idle float key count changed.");
        Vector2 start = animation.TrackGetKeyValue(positionTrack, 0)
            .AsVector2();
        Vector2 peak = animation.TrackGetKeyValue(positionTrack, 1)
            .AsVector2();
        Vector2 end = animation.TrackGetKeyValue(positionTrack, 2)
            .AsVector2();
        Require(start.IsEqualApprox(new Vector2(0f, -316f))
                && peak.IsEqualApprox(new Vector2(0f, -319f))
                && end.IsEqualApprox(start),
            "Twilight idle no longer keeps its three-pixel vertical float.");
    }

    private static int FindTwilightVisualTrack(
        Animation animation,
        string visualNode,
        string property)
    {
        string suffix = visualNode + ":" + property;
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            if (animation.TrackGetPath(track).ToString()
                .EndsWith(suffix, StringComparison.Ordinal))
            {
                return track;
            }
        }

        throw new InvalidOperationException(
            $"Twilight animation {animation.ResourceName} has no "
            + suffix + " track.");
    }

    private static Image LoadTextureImage(string path)
    {
        Texture2D texture = ResourceLoader.Load<Texture2D>(path)
            ?? throw new InvalidOperationException(
                "Could not load Twilight texture: " + path);
        return texture.GetImage();
    }

    private static int GetVisibleAlphaHeight(Image image)
    {
        int top = -1;
        int bottom = -1;
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A <= 0.03f)
                {
                    continue;
                }

                top = top < 0 ? y : top;
                bottom = y;
                break;
            }
        }

        Require(top >= 0 && bottom >= top,
            "Twilight texture has no visible alpha pixels.");
        return bottom - top + 1;
    }

    private static void RequirePowerEffects(
        AbstractIntent intent,
        params (Type PowerType, int Amount)[] expected)
    {
        Require(intent is IIntentEffectProvider,
            $"{intent.GetType().Name} did not expose effect badges.");
        IReadOnlyList<IntentBadge> actual =
            ((IIntentEffectProvider)intent).Effects;
        Require(actual.Count == expected.Length,
            $"{intent.GetType().Name} badge count mismatch: "
            + $"actual={actual.Count} expected={expected.Length}.");
        for (int i = 0; i < expected.Length; i++)
        {
            Require(actual[i].PowerType == expected[i].PowerType
                    && actual[i].Amount == expected[i].Amount,
                $"{intent.GetType().Name} badge {i} mismatch: "
                + $"actual={actual[i].PowerType?.Name}:{actual[i].Amount} "
                + $"expected={expected[i].PowerType.Name}:"
                + $"{expected[i].Amount}.");
        }
    }

    private static void VerifyRegistrationAndActRouting()
    {
        Require(LibraryOfRuinaSettings.MonsterExtensionEnabled,
            "Monster extension must be enabled for the verifier.");
        Require(LiberationBossRegistry.RegisterPhilosophyFloorLiberation,
            "Philosophy floor liberation is not registered by default.");
        Require(
            LiberationBossRegistry
                .IsEncounterRegistered<PhilosophyFloorLiberationEncounter>(),
            "Generic Philosophy floor registration lookup failed.");

        EncounterModel encounter =
            ModelDb.Encounter<PhilosophyFloorLiberationEncounter>();
        Require(LiberationBossRegistry.IsEncounterRegistered(encounter),
            "Instance Philosophy floor registration lookup failed.");

        (ActModel act, int index, Type expectedType)[] libraryActTargets =
        [
            (ModelDb.Act<Malkuth>(), 0, typeof(HistoryFloorLiberationEncounter)),
            (ModelDb.Act<Yesod>(), 0, typeof(TechnologyFloorLiberationEncounter)),
            (ModelDb.Act<Hod>(), 0, typeof(LiteratureFloorLiberationEncounter)),
            (ModelDb.Act<NetZech>(), 1, typeof(ArtFloorLiberationEncounter)),
            (ModelDb.Act<Gebura>(), 1, typeof(LanguageFloorLiberationEncounter)),
            (ModelDb.Act<Chesed>(), 2, typeof(SocialFloorLiberationEncounter)),
            (ModelDb.Act<Binah>(), 2, typeof(PhilosophyFloorLiberationEncounter))
        ];
        foreach ((ActModel act, int index, Type expectedType) in libraryActTargets)
        {
            EncounterModel? selected =
                LibraryEncounterWeighting.ChooseLiberationEncounter(
                    act,
                    index);
            Require(selected?.GetType() == expectedType,
                act.Id.Entry + " did not keep its fixed Library boss: "
                + (selected?.GetType().Name ?? "null"));
        }

        ActModel[] externalActs =
        [
            ModelDb.Act<Overgrowth>(),
            ModelDb.Act<Underdocks>(),
            ModelDb.Act<Hive>(),
            ModelDb.Act<Glory>()
        ];
        foreach (ActModel externalAct in externalActs)
        {
            Require(
                LibraryEncounterWeighting.ChooseLiberationEncounter(
                    externalAct,
                    externalAct.Index) == null,
                externalAct.Id.Entry
                + " was incorrectly routed by Library encounter weighting.");
        }

        ActModel mutableBinah = ModelDb.Act<Binah>().ToMutable();
        mutableBinah.SetBossEncounter(
            ModelDb.Encounter<ArtFloorLiberationEncounter>().ToMutable());
        LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(
            mutableBinah,
            2);
        Require(mutableBinah.BossEncounter
                is PhilosophyFloorLiberationEncounter,
            "Binah did not restore its fixed Philosophy floor boss.");

        mutableBinah.SetBossEncounter(
            ModelDb.Encounter<PhilosophyFloorLiberationEncounter>()
                .ToMutable());
        LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(
            mutableBinah,
            2);
        Require(mutableBinah.BossEncounter
                is PhilosophyFloorLiberationEncounter,
            "Binah replaced its existing Philosophy floor boss.");

        mutableBinah.SetBossEncounter(
            ModelDb.Encounter<SocialFloorLiberationEncounter>().ToMutable());
        LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(
            mutableBinah,
            2);
        Require(mutableBinah.BossEncounter
                is PhilosophyFloorLiberationEncounter,
            "Social floor replaced Binah's fixed Philosophy floor boss.");
    }

    private static void VerifyEncounterContractAndResources()
    {
        var encounter =
            (PhilosophyFloorLiberationEncounter)ModelDb
                .Encounter<PhilosophyFloorLiberationEncounter>()
                .ToMutable();
        Require(encounter.ShouldGiveRewards,
            "Philosophy floor must retain standard act-3 boss rewards.");
        Require(FtueGuard.IsLiberationEncounter(encounter),
            "Philosophy floor was excluded from the standard liberation FTUE.");
        Require(encounter.BossNodePath ==
                "res://images/map/placeholder/"
                + "philosophy_floor_liberation_encounter_icon",
            "Unexpected Philosophy floor boss-node icon path.");

        string[] requiredIconPaths =
        [
            "res://images/map/placeholder/"
                + "philosophy_floor_liberation_encounter_icon.png",
            "res://images/map/placeholder/"
                + "philosophy_floor_liberation_encounter_icon_outline.png",
            "res://images/ui/run_history/"
                + "philosophy_floor_liberation_encounter.png",
            "res://images/ui/run_history/"
                + "philosophy_floor_liberation_encounter_outline.png"
        ];
        HashSet<string> assetPaths = encounter.ExtraAssetPaths
            .ToHashSet(StringComparer.Ordinal);
        foreach (string path in requiredIconPaths)
        {
            Require(assetPaths.Contains(path),
                "Encounter asset list omitted icon: " + path);
            Require(ResourceLoader.Exists(path),
                "Missing Philosophy floor icon resource: " + path);
            if (path.Contains(
                    "/ui/run_history/",
                    StringComparison.Ordinal))
            {
                VerifyIconHash(path);
            }
        }
        LiberationBossMapIconVerificationPatch.VerifyFloor("philosophy");

        foreach (string path in assetPaths)
        {
            Require(ResourceLoader.Exists(path),
                "Missing Philosophy floor resource: " + path);
        }

        VerifyScaledBackgroundComposition(encounter);
        VerifyBackgroundLayerDirectoryContract();
        VerifyPowerIconResources();
        VerifyOriginalCgContract();
    }

    private static void VerifyOriginalCgContract()
    {
        Require(Mathf.IsEqualApprox(
                PhilosophyFloorLiberationCgController.IntroAudioLinearScale,
                0.48f),
            "Philosophy floor intro CG audio is no longer reduced by 40%.");
        PackedScene packed = ResourceLoader.Load<PackedScene>(
                PhilosophyFloorLiberationCgController.ScenePath)
            ?? throw new InvalidOperationException(
                "Could not load Philosophy floor CG scene.");
        Control root = packed.Instantiate<Control>();
        try
        {
            string[] expectedLayerOrder =
            [
                "Background",
                "StoryImage",
                "Foreground",
                "UpperFrame",
                "StoryText",
                "AnimationPlayer"
            ];
            Require(root.GetChildCount() == expectedLayerOrder.Length
                    && Enumerable.Range(0, expectedLayerOrder.Length)
                        .All(index => root.GetChild(index).Name
                            == expectedLayerOrder[index]),
                "Philosophy floor CG no longer matches the original "
                + "five-layer Canvas order.");
            foreach (string nodeName in expectedLayerOrder.Take(4))
            {
                TextureRect layer = root.GetNode<TextureRect>(nodeName);
                Require((int)layer.ExpandMode == 1
                        && (int)layer.StretchMode == 0,
                    "Philosophy floor CG layer no longer uses the original "
                    + "1920x1080 stretched-canvas layout: " + nodeName);
            }

            AnimationPlayer player =
                root.GetNode<AnimationPlayer>("AnimationPlayer");
            Animation animation = player.GetAnimation("play")
                ?? throw new InvalidOperationException(
                    "Philosophy floor CG play animation is missing.");
            Require(Mathf.IsEqualApprox(animation.Length, 5f)
                    && animation.TrackGetKeyCount(0) == 4
                    && Mathf.IsEqualApprox(
                        (float)animation.TrackGetKeyTime(0, 1),
                        1f)
                    && Mathf.IsEqualApprox(
                        (float)animation.TrackGetKeyTime(0, 2),
                        4.5f),
                "Philosophy floor CG lost the original 1.0/4.5/5.0 second "
                + "Animator timing.");
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyScaledBackgroundComposition(
        PhilosophyFloorLiberationEncounter encounter)
    {
        Require(Mathf.IsEqualApprox(encounter.GetCameraScaling(), 0.82f),
            "Philosophy floor lost the multi-slot liberation camera scale.");
        Vector2 cameraOffset = encounter.GetCameraOffset();
        Require(Mathf.IsEqualApprox(cameraOffset.X, -100f)
                && Mathf.IsEqualApprox(cameraOffset.Y, 50f),
            "Philosophy floor lost the multi-slot liberation camera offset.");

        PackedScene packed = ResourceLoader.Load<PackedScene>(
                PhilosophyFloorLiberationEncounter.BackgroundLayerScenePath)
            ?? throw new InvalidOperationException(
                "Could not load Philosophy floor background layer scene.");
        Control layer = packed.Instantiate<Control>();
        try
        {
            Require(Mathf.IsEqualApprox(layer.OffsetLeft, -1280f)
                    && Mathf.IsEqualApprox(layer.OffsetTop, -720f)
                    && Mathf.IsEqualApprox(layer.OffsetRight, 1280f)
                    && Mathf.IsEqualApprox(layer.OffsetBottom, 720f),
                "Philosophy floor background is not the 2560x1440 "
                + "multi-slot liberation canvas.");

            TextureRect sky = layer.GetNode<TextureRect>("Sky");
            TextureRect ground = layer.GetNode<TextureRect>("Ground");
            Node2D endBird = layer.GetNode<Node2D>("PhilosophyEndBird");
            Node2D eggs = layer.GetNode<Node2D>("PhilosophyEggs");
            Require((int)sky.ExpandMode == 1
                    && (int)sky.StretchMode == 6
                    && (int)ground.ExpandMode == 1
                    && (int)ground.StretchMode == 6,
                "Philosophy floor background layers no longer cover "
                + "the scaled combat viewport.");
            Require(sky.ZIndex < endBird.ZIndex
                    && endBird.ZIndex < ground.ZIndex
                    && ground.ZIndex < eggs.ZIndex
                    && eggs.ZIndex < -10,
                "Philosophy floor background depth order is invalid.");
            Require(!sky.ZAsRelative
                    && !ground.ZAsRelative
                    && !endBird.ZAsRelative
                    && !eggs.ZAsRelative,
                "Philosophy floor background depth must be absolute.");
            Require(endBird.Position.IsEqualApprox(new Vector2(1385f, 340f))
                    && endBird.Scale.IsEqualApprox(new Vector2(2.82f, 2.82f)),
                "The background Apocalypse Bird is outside its visible "
                + "multi-slot composition.");
            CanvasItem endBirdVisuals = endBird.GetNode<CanvasItem>(
                "FacingRoot/Visuals");
            Require(endBirdVisuals.Modulate.IsEqualApprox(Colors.White),
                "The background Apocalypse Bird must remain fully opaque.");

            Node2D bigEyes = eggs.GetNode<Node2D>("BigEyes");
            Node2D smallBeak = eggs.GetNode<Node2D>("SmallBeak");
            Node2D longArms = eggs.GetNode<Node2D>("LongArms");
            foreach (Node2D egg in new[] { bigEyes, smallBeak, longArms })
            {
                Sprite2D glow = egg.GetNode<Sprite2D>("Glow");
                Require(glow.Scale.IsEqualApprox(new Vector2(1.12f, 1.12f))
                        && glow.Modulate.IsEqualApprox(
                            new Color(1f, 0.94f, 0.38f, 0.62f)),
                    "The active egg glow is no longer clearly visible.");
            }
            Require(Mathf.IsEqualApprox(
                        smallBeak.Position.X - bigEyes.Position.X,
                        120f)
                    && Mathf.IsEqualApprox(
                        longArms.Position.X - smallBeak.Position.X,
                        120f),
                "The three eggs are no longer centered at compact spacing.");

            float bigBottom = GetNormalEggBottom(bigEyes);
            float smallBottom = GetNormalEggBottom(smallBeak);
            float longBottom = GetNormalEggBottom(longArms);
            float minBottom = Math.Min(bigBottom,
                Math.Min(smallBottom, longBottom));
            float maxBottom = Math.Max(bigBottom,
                Math.Max(smallBottom, longBottom));
            Require(maxBottom - minBottom <= 2f
                    && Math.Abs((bigBottom + smallBottom + longBottom) / 3f
                        - 916f) <= 2f,
                "The three eggs are not aligned with the Y=735 creature "
                + "ground line under the scaled background canvas.");
        }
        finally
        {
            layer.Free();
        }
    }

    private static float GetNormalEggBottom(Node2D egg)
    {
        Sprite2D normal = egg.GetNode<Sprite2D>("Normal");
        return egg.Position.Y
            + egg.Scale.Y * (normal.Position.Y
                + normal.Scale.Y * normal.Texture.GetHeight() * 0.5f);
    }

    private static void VerifyBackgroundLayerDirectoryContract()
    {
        const string layersDirectory =
            "res://scenes/backgrounds/"
            + "philosophy_floor_liberation_encounter/layers";
        string[] files = DirAccess.GetFilesAt(layersDirectory);
        Require(files.Length > 0,
            "Philosophy floor background layers directory is empty.");
        foreach (string file in files)
        {
            Require(file.Contains("_bg_", StringComparison.Ordinal)
                    || file.Contains("_fg_", StringComparison.Ordinal),
                "Invalid Philosophy floor background layer filename: "
                + file);
        }
    }

    private static void VerifyPowerIconResources()
    {
        string[] greenPassivePaths =
        [
            "res://images/powers/"
                + "philosophy_floor_twilight_black_monster_power.png",
            "res://images/powers/"
                + "philosophy_floor_twilight_three_birds_power.png",
            "res://images/powers/"
                + "philosophy_floor_twilight_broken_egg_power.png",
            "res://images/powers/"
                + "philosophy_floor_twilight_peace75_power.png",
            "res://images/powers/"
                + "philosophy_floor_twilight_peace50_power.png",
            "res://images/powers/"
                + "philosophy_floor_twilight_peace25_power.png"
        ];
        foreach (string path in greenPassivePaths)
        {
            VerifyRuntimeResourceHash(
                path,
                ExpectedGreenPassiveIconSha256);
        }

        VerifyRuntimeResourceHash(
            "res://images/powers/"
            + "philosophy_floor_twilight_fear_power.png",
            ExpectedFearIconSha256);
    }

    private static void VerifyRuntimeResourceHash(
        string path,
        string expectedSha256)
    {
        Require(ResourceLoader.Exists(path),
            "Missing Philosophy floor runtime resource: " + path);
        if (!FileAccess.FileExists(path))
        {
            return;
        }

        string actualSha256 = Convert.ToHexString(
            SHA256.HashData(FileAccess.GetFileAsBytes(path)));
        Require(actualSha256 == expectedSha256,
            "Philosophy floor runtime resource hash mismatch: "
            + path + " actual=" + actualSha256);
    }

    private static void VerifyIconHash(string path)
    {
        byte[] sourceBytes = FileAccess.FileExists(path)
            ? FileAccess.GetFileAsBytes(path)
            : Array.Empty<byte>();
        if (sourceBytes.Length > 0)
        {
            string sourceHash = Convert.ToHexString(
                SHA256.HashData(sourceBytes));
            Require(sourceHash == ExpectedSourceIconSha256,
                "Philosophy floor source-icon hash mismatch: " + path
                + " actual=" + sourceHash);
            return;
        }

        Texture2D texture = ResourceLoader.Load<Texture2D>(path)
            ?? throw new InvalidOperationException(
                "Could not load Philosophy floor icon texture: " + path);
        Image image = texture.GetImage();
        Require(image.GetWidth() == 242 && image.GetHeight() == 229,
            "Philosophy floor icon dimensions changed: " + path
            + $" actual={image.GetWidth()}x{image.GetHeight()}");
        string pixelHash = Convert.ToHexString(
            SHA256.HashData(image.GetData()));
        Require(pixelHash == ExpectedIconPixelSha256,
            "Philosophy floor imported-icon pixel hash mismatch: " + path
            + " actual=" + pixelHash);
    }

    private static void VerifyEggPrioritySchedule()
    {
        PhilosophyFloorTwilight boss = CreateDetachedBoss();
        for (int aliveMask = 0;
             aliveMask <= PhilosophyFloorTwilight.AllEggMask;
             aliveMask++)
        {
            SetProperty(boss, nameof(boss.AliveEggMask), aliveMask);
            int aliveCount = (aliveMask & 1)
                + ((aliveMask >> 1) & 1)
                + ((aliveMask >> 2) & 1);
            int destroyedCount = 3 - aliveCount;
            Require(boss.DestroyedEggCount == destroyedCount,
                $"Destroyed egg count mismatch for mask {aliveMask}.");
            Require(boss.CurrentEncounterBgmTrackIndex
                    == Math.Clamp(destroyedCount, 0, 2),
                $"Dynamic BGM index mismatch for mask {aliveMask}.");
            for (int round = 1; round <= 6; round++)
            {
                PhilosophyFloorTwilightEgg actual =
                    InvokePrivate<PhilosophyFloorTwilightEgg>(
                        boss,
                        "ResolveActiveEgg",
                        round);
                PhilosophyFloorTwilightEgg expected =
                    ExpectedActiveEgg(round, aliveMask);
                Require(actual == expected,
                    $"Egg priority mismatch: round={round} mask={aliveMask} "
                    + $"actual={actual} expected={expected}");
            }
        }
    }

    private static void VerifyEncounterStateRoundTrip()
    {
        var sourceEncounter = (PhilosophyFloorLiberationEncounter)ModelDb
            .Encounter<PhilosophyFloorLiberationEncounter>()
            .ToMutable();
        sourceEncounter.GenerateMonstersWithSlots(NullRunState.Instance);
        PhilosophyFloorTwilight source = sourceEncounter.MonstersWithSlots
            .Select(static entry => entry.Item1)
            .OfType<PhilosophyFloorTwilight>()
            .Single();

        SetProperty(source, nameof(source.AliveEggMask), 0b101);
        SetProperty(
            source,
            nameof(source.ActiveEgg),
            PhilosophyFloorTwilightEgg.LongArms);
        SetProperty(
            source,
            nameof(source.PlannedMode),
            PhilosophyFloorTwilightMode.Other);
        SetProperty(source, nameof(source.HasPlannedMode), true);
        SetProperty(source, nameof(source.ModeCycleStep), 2);
        SetProperty(source, nameof(source.JudgmentBranchEntries), 5);
        SetProperty(source, nameof(source.SinTraceBranchEntries), 8);
        SetProperty(source, nameof(source.PunishmentBranchEntries), 11);
        SetProperty(source, nameof(source.NextEndFallbackIsOne), false);
        SetProperty(
            source,
            nameof(source.PlannedBranchCounter),
            PhilosophyFloorTwilightBranchCounter.Judgment
            | PhilosophyFloorTwilightBranchCounter.Punishment);
        SetProperty(source, nameof(source.PlannedUsesEndFallback), true);
        SetProperty(
            source,
            nameof(source.PlannedOtherFirstAction),
            PhilosophyFloorTwilightAction.BrilliantEyes);
        SetProperty(
            source,
            nameof(source.PlannedOtherSecondAction),
            PhilosophyFloorTwilightAction.Prowl);
        SetProperty(source, nameof(source.LastEggScheduleRound), 37);
        SetProperty(source, nameof(source.BrokenEggRecoveryPending), true);
        SetProperty(source, nameof(source.IntroCgPlayed), true);
        SetProperty(source, nameof(source.PlannedTargetOneCombatId), 101);
        SetProperty(source, nameof(source.PlannedTargetTwoCombatId), 102);
        SetProperty(source, nameof(source.PlannedTargetThreeCombatId), 103);
        SetProperty(source, nameof(source.PlannedTargetFourCombatId), 104);
        SetProperty(source, "SmallBeakProcessedRound", 37);
        SetProperty(
            source,
            "SmallBeakProcessedPlayerCombatIds",
            new[] { 201, 202 });

        var restoredEncounter = (PhilosophyFloorLiberationEncounter)ModelDb
            .Encounter<PhilosophyFloorLiberationEncounter>()
            .ToMutable();
        restoredEncounter.LoadCustomState(sourceEncounter.SaveCustomState());
        restoredEncounter.GenerateMonstersWithSlots(NullRunState.Instance);
        PhilosophyFloorTwilight restored = restoredEncounter.MonstersWithSlots
            .Select(static entry => entry.Item1)
            .OfType<PhilosophyFloorTwilight>()
            .Single();

        Require(restored.AliveEggMask == source.AliveEggMask
                && restored.ActiveEgg == source.ActiveEgg
                && restored.PlannedMode == source.PlannedMode
                && restored.HasPlannedMode == source.HasPlannedMode
                && restored.ModeCycleStep == source.ModeCycleStep
                && restored.JudgmentBranchEntries
                    == source.JudgmentBranchEntries
                && restored.SinTraceBranchEntries
                    == source.SinTraceBranchEntries
                && restored.PunishmentBranchEntries
                    == source.PunishmentBranchEntries
                && restored.NextEndFallbackIsOne
                    == source.NextEndFallbackIsOne
                && restored.PlannedBranchCounter
                    == source.PlannedBranchCounter
                && restored.PlannedUsesEndFallback
                    == source.PlannedUsesEndFallback
                && restored.PlannedOtherFirstAction
                    == source.PlannedOtherFirstAction
                && restored.PlannedOtherSecondAction
                    == source.PlannedOtherSecondAction
                && restored.LastEggScheduleRound
                    == source.LastEggScheduleRound
                && restored.BrokenEggRecoveryPending
                    == source.BrokenEggRecoveryPending
                && restored.IntroCgPlayed == source.IntroCgPlayed
                && Enumerable.Range(0, 4).All(slot =>
                    restored.GetPlannedTargetCombatId(slot)
                    == source.GetPlannedTargetCombatId(slot)),
            "Encounter save/load did not restore the Twilight plan state.");
        Require(!restored.TryMarkSmallBeakPlayerProcessed(37, 201)
                && !restored.TryMarkSmallBeakPlayerProcessed(37, 202)
                && restored.TryMarkSmallBeakPlayerProcessed(37, 203)
                && restored.TryMarkSmallBeakPlayerProcessed(38, 201),
            "Encounter save/load did not restore Small Beak per-player state.");
    }

    private static void VerifyAiBranchFallbacks()
    {
        PhilosophyFloorTwilight boss = CreateDetachedBoss();

        SetEggState(boss, PhilosophyFloorTwilightEgg.LongArms);
        for (int priorEntries = 0; priorEntries < 3; priorEntries++)
        {
            SetProperty(
                boss,
                nameof(boss.JudgmentBranchEntries),
                priorEntries);
            ResetPlanningMarkers(boss);
            PhilosophyFloorTwilightMode actual =
                InvokePrivate<PhilosophyFloorTwilightMode>(
                    boss,
                    "PlanFirstCycleMode");
            PhilosophyFloorTwilightMode expected = priorEntries == 2
                ? PhilosophyFloorTwilightMode.EndTwo
                : PhilosophyFloorTwilightMode.Judgment;
            Require(actual == expected,
                $"Judgment third-entry fallback mismatch: prior="
                + $"{priorEntries} actual={actual} expected={expected}");
        }

        SetEggState(boss, PhilosophyFloorTwilightEgg.LongArms);
        SetProperty(boss, nameof(boss.SinTraceBranchEntries), 2);
        SetProperty(boss, nameof(boss.NextEndFallbackIsOne), true);
        ResetPlanningMarkers(boss);
        PhilosophyFloorTwilightMode firstFallback =
            InvokePrivate<PhilosophyFloorTwilightMode>(
                boss,
                "PlanSecondCycleMode");
        Require(firstFallback == PhilosophyFloorTwilightMode.EndOne,
            "Sin Trace third entry did not fall back to End 1.");
        InvokePrivate<object?>(boss, "CommitPerformedPlan");
        Require(!boss.NextEndFallbackIsOne,
            "End fallback did not rotate from End 1 to End 3.");

        SetProperty(boss, nameof(boss.SinTraceBranchEntries), 2);
        ResetPlanningMarkers(boss);
        PhilosophyFloorTwilightMode secondFallback =
            InvokePrivate<PhilosophyFloorTwilightMode>(
                boss,
                "PlanSecondCycleMode");
        Require(secondFallback == PhilosophyFloorTwilightMode.EndThree,
            "Sin Trace fallback did not rotate to End 3.");
        InvokePrivate<object?>(boss, "CommitPerformedPlan");
        Require(boss.NextEndFallbackIsOne,
            "End fallback did not rotate back to End 1.");

        SetEggState(boss, PhilosophyFloorTwilightEgg.SmallBeak);
        for (int priorEntries = 0; priorEntries < 3; priorEntries++)
        {
            SetProperty(
                boss,
                nameof(boss.PunishmentBranchEntries),
                priorEntries);
            SetProperty(boss, nameof(boss.NextEndFallbackIsOne), true);
            ResetPlanningMarkers(boss);
            PhilosophyFloorTwilightMode actual =
                InvokePrivate<PhilosophyFloorTwilightMode>(
                    boss,
                    "PlanSecondCycleMode");
            PhilosophyFloorTwilightMode expected = priorEntries == 2
                ? PhilosophyFloorTwilightMode.EndOne
                : PhilosophyFloorTwilightMode.Punishment;
            Require(actual == expected,
                $"Punishment third-entry fallback mismatch: prior="
                + $"{priorEntries} actual={actual} expected={expected}");
        }
    }

    private static async Task VerifyAscensionValues()
    {
        await VerifyAscensionValues(
            ascensionLevel: 0,
            expectedHp: 1200,
            expectedDamage:
            [
                (PhilosophyFloorTwilightAction.SlamDown,
                    "SlamDownDamage", 9, 1),
                (PhilosophyFloorTwilightAction.Talon,
                    "TalonDamage", 4, 3),
                (PhilosophyFloorTwilightAction.ProtectBlackForest,
                    "ProtectBlackForestDamage", 11, 1),
                (PhilosophyFloorTwilightAction.TornMouth,
                    "TornMouthDamage", 4, 3),
                (PhilosophyFloorTwilightAction.TiltedScale,
                    "TiltedScaleDamage", 11, 1),
                (PhilosophyFloorTwilightAction.Punishment,
                    "PunishmentDamage", 3, 4),
                (PhilosophyFloorTwilightAction.PeaceForAll,
                    "PeaceForAllDamage", 24, 1)
            ],
            seed: "PHILOSOPHYFLOORVERIFY_LOW");
        await VerifyAscensionValues(
            ascensionLevel: (int)AscensionLevel.DeadlyEnemies,
            expectedHp: 1400,
            expectedDamage:
            [
                (PhilosophyFloorTwilightAction.SlamDown,
                    "SlamDownDamage", 10, 1),
                (PhilosophyFloorTwilightAction.Talon,
                    "TalonDamage", 5, 3),
                (PhilosophyFloorTwilightAction.ProtectBlackForest,
                    "ProtectBlackForestDamage", 12, 1),
                (PhilosophyFloorTwilightAction.TornMouth,
                    "TornMouthDamage", 5, 3),
                (PhilosophyFloorTwilightAction.TiltedScale,
                    "TiltedScaleDamage", 12, 1),
                (PhilosophyFloorTwilightAction.Punishment,
                    "PunishmentDamage", 4, 4),
                (PhilosophyFloorTwilightAction.PeaceForAll,
                    "PeaceForAllDamage", 26, 1)
            ],
            seed: "PHILOSOPHYFLOORVERIFY_HIGH");
    }

    private static async Task VerifyAscensionValues(
        int ascensionLevel,
        int expectedHp,
        IReadOnlyList<(
            PhilosophyFloorTwilightAction Action,
            string PropertyName,
            int Expected,
            int Hits)> expectedDamage,
        string seed)
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
        try
        {
            PhilosophyFloorTwilight boss = CreateDetachedBoss();
            Require(boss.MinInitialHp == expectedHp
                    && boss.MaxInitialHp == expectedHp,
                $"Boss HP mismatch at ascension {ascensionLevel}: "
                + $"{boss.MinInitialHp}/{boss.MaxInitialHp}");
            Require(boss.DefaultChaoResistance == 250,
                "Boss maximum Chao resistance was not 250.");
            Require(boss.DefaultPhysicalResistanceData is
                {
                    Slash: LibraryResistanceLevel.Immune,
                    Pierce: LibraryResistanceLevel.Immune,
                    Blunt: LibraryResistanceLevel.Immune
                },
                "Boss opening physical resistances were not all Immune.");
            Require(boss.DefaultChaoResistanceData is
                {
                    Slash: LibraryResistanceLevel.Vulnerable,
                    Pierce: LibraryResistanceLevel.Resist,
                    Blunt: LibraryResistanceLevel.Resist
                },
                "Boss opening Big Eyes Chao resistances were incorrect.");
            foreach ((
                         PhilosophyFloorTwilightAction action,
                         string propertyName,
                         int expected,
                         int hits) in expectedDamage)
            {
                int actual = GetPrivateStaticInt(propertyName);
                Require(actual == expected,
                    $"{propertyName} mismatch at ascension "
                    + $"{ascensionLevel}: actual={actual} expected={expected}");

                AbstractIntent intent = boss.DebugCreateActionIntent(action);
                Require(intent is AttackIntent,
                    $"{action} did not expose an attack intent.");
                int previewTotal = ((AttackIntent)intent).GetTotalDamage(
                    Array.Empty<Creature>(),
                    boss.Creature);
                int expectedPreviewTotal = expected * hits;
                Require(previewTotal == expectedPreviewTotal,
                    $"{action} intent damage mismatch at ascension "
                    + $"{ascensionLevel}: actual={previewTotal} "
                    + $"expected={expectedPreviewTotal}.");
            }
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Philosophy floor ascension verifier cleanup");
        }
    }

    private sealed record PhilosophyFloorCombatContext(
        CombatState CombatState,
        PhilosophyFloorTwilight Boss,
        LibraryCreature BossCreature,
        Creature Player);

    private sealed record PhilosophyFloorMultiplayerCombatContext(
        CombatState CombatState,
        PhilosophyFloorTwilight Boss,
        LibraryCreature BossCreature,
        Player[] Players);

    private static async Task VerifyRuntimeStateMachineAndActions()
    {
        PhilosophyFloorCombatContext fight = await StartFight(
            "PHILOSOPHYFLOORVERIFY_RUNTIME");
        try
        {
            await CreatureCmd.SetMaxHp(fight.Player, 100000m);
            await CreatureCmd.SetCurrentHp(fight.Player, 100000m);

            PhilosophyFloorTwilightMode?[] expectedModes =
            [
                PhilosophyFloorTwilightMode.Surveillance,
                PhilosophyFloorTwilightMode.EndOne,
                PhilosophyFloorTwilightMode.Other,
                PhilosophyFloorTwilightMode.EndTwo,
                null,
                PhilosophyFloorTwilightMode.EndThree,
                PhilosophyFloorTwilightMode.Other,
                PhilosophyFloorTwilightMode.Surveillance,
                PhilosophyFloorTwilightMode.Punishment
            ];

            for (int round = 1; round <= expectedModes.Length; round++)
            {
                fight.CombatState.RoundNumber = round;
                await fight.Boss.BeforeSideTurnStart(
                    new ThrowingPlayerChoiceContext(),
                    CombatSide.Player,
                    fight.CombatState.PlayerCreatures,
                    fight.CombatState);

                if (round > 1)
                {
                    fight.Boss.RollMove(fight.CombatState.PlayerCreatures);
                }

                if (round == 5)
                {
                    int cycleStep = fight.Boss.ModeCycleStep;
                    await LibraryCreatureCmd.Stun(fight.BossCreature);
                    Require(fight.Boss.NextMove.Id == "STUNNED",
                        "AfterStun did not install the real stunned move.");
                    Require(!fight.Boss.HasPlannedMode
                            && fight.Boss.ModeCycleStep == cycleStep,
                        "AfterStun advanced or retained the interrupted plan.");
                    await fight.Boss.PerformMove();
                    continue;
                }

                PhilosophyFloorTwilightMode expectedMode =
                    expectedModes[round - 1]!.Value;
                Require(fight.Boss.HasPlannedMode
                        && fight.Boss.PlannedMode == expectedMode,
                    $"Runtime mode mismatch at round {round}: "
                    + $"actual={fight.Boss.PlannedMode} "
                    + $"expected={expectedMode}.");
                string expectedStateId =
                    "PHILOSOPHY_FLOOR_TWILIGHT_"
                    + expectedMode.ToString().ToUpperInvariant();
                Require(fight.Boss.NextMove.Id == expectedStateId,
                    $"Runtime state mismatch at round {round}: "
                    + $"actual={fight.Boss.NextMove.Id} "
                    + $"expected={expectedStateId}.");

                PhilosophyFloorTwilightAction[] expectedActions =
                    ExpectedActionsForCurrentPlan(fight.Boss);
                await CreatureCmd.SetCurrentHp(fight.Player, fight.Player.MaxHp);
                await fight.Boss.PerformMove();
                Require(fight.Boss.DebugPerformedActionTrace.SequenceEqual(
                        expectedActions),
                    $"Runtime action sequence mismatch at round {round}: "
                    + $"actual=[{string.Join(',', fight.Boss.DebugPerformedActionTrace)}] "
                    + $"expected=[{string.Join(',', expectedActions)}].");
                Require(!fight.Boss.HasPlannedMode,
                    $"Runtime plan was not committed at round {round}.");
            }

            await VerifyEveryRuntimeAction(fight);
            await VerifySurveillanceMutuallyExclusiveBranches(fight);
            await VerifyJudgmentPowerBypass(fight);
            await VerifySinHpLossAndDecrement(fight);
            await VerifyImmediatePostBreakEggEffects(fight);
            await VerifyPreStunnedChaosDepletionBreaksEgg(fight);
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Philosophy floor runtime verifier cleanup");
        }
    }

    private static async Task VerifyFakeMultiplayerRuntime()
    {
        PhilosophyFloorMultiplayerCombatContext fight =
            await StartFakeMultiplayerFight(
                "PHILOSOPHYFLOORVERIFY_MULTIPLAYER");
        try
        {
            Require(fight.Players.Length == 2
                    && fight.CombatState.PlayerCreatures.Count == 2,
                "Fake multiplayer fight did not create two players.");

            int[] livingPlayerCombatIds = fight.Players
                .Select(static player => player.Creature)
                .Where(static creature => creature.IsAlive)
                .Select(static creature => creature.CombatId)
                .Where(static combatId => combatId.HasValue
                    && combatId.Value <= int.MaxValue)
                .Select(static combatId => (int)combatId!.Value)
                .ToArray();
            Require(livingPlayerCombatIds.Length == 2,
                "Fake multiplayer players did not receive combat IDs.");
            Require(fight.Boss.HasPlannedMode,
                "Fake multiplayer boss did not retain its opening plan.");
            int[] plannedTargetCombatIds = Enumerable.Range(0, 4)
                .Select(fight.Boss.GetPlannedTargetCombatId)
                .ToArray();
            Require(plannedTargetCombatIds.Any(static id => id > 0)
                    && plannedTargetCombatIds
                        .Where(static id => id > 0)
                        .All(livingPlayerCombatIds.Contains),
                "Fake multiplayer plan targeted a non-player combat ID: "
                + $"targets=[{string.Join(',', plannedTargetCombatIds)}] "
                + $"players=[{string.Join(',', livingPlayerCombatIds)}].");
            int plannedTargetSlot = Array.FindIndex(
                plannedTargetCombatIds,
                static combatId => combatId > 0);
            Require(plannedTargetSlot >= 0,
                "Fake multiplayer plan did not reserve a non-attack target.");
            AbstractIntent surveillanceIntent = fight.Boss
                .DebugCreateActionIntent(
                    PhilosophyFloorTwilightAction.Surveillance,
                    plannedTargetSlot);
            Require(surveillanceIntent is IIntentTargetLineProvider
                    && ((IIntentTargetLineProvider)surveillanceIntent)
                        .GetIntentTargetLineTargets(
                            fight.BossCreature,
                            fight.CombatState.PlayerCreatures)
                        .Count
                    == PhilosophyFloorTwilight.ResolveNonAttackTargetCount(2),
                "Fake multiplayer non-attack intent exposed the wrong "
                + "number of random targets.");
            VerifyJudgmentIntentPreview(fight);
            await VerifyVanillaAttackHitsEveryLivingPlayer(fight);

            SetProperty(
                fight.Boss,
                nameof(fight.Boss.AliveEggMask),
                PhilosophyFloorTwilight.AllEggMask);
            SetProperty(
                fight.Boss,
                nameof(fight.Boss.ActiveEgg),
                PhilosophyFloorTwilightEgg.SmallBeak);
            fight.CombatState.RoundNumber = 77;

            PhilosophyFloorTwilightSmallBeakControllerPower controller =
                fight.BossCreature
                    .GetPower<
                        PhilosophyFloorTwilightSmallBeakControllerPower>()
                ?? throw new InvalidOperationException(
                    "Small Beak controller power was missing.");
            var choiceContext = new ThrowingPlayerChoiceContext();
            foreach (Player player in fight.Players)
            {
                CardModel[] hand = player.PlayerCombatState?.Hand.Cards
                    .ToArray() ?? [];
                CardModel[] eligible = hand.Where(static card =>
                        !card.EnergyCost.CostsX
                        && card.EnergyCost.GetWithModifiers(
                            CostModifiers.None) >= 0)
                    .ToArray();
                Require(eligible.Length >= 2,
                    $"Player {player.NetId} had fewer than two normal-cost "
                    + "cards after the real opening draw.");
                var costsBefore = eligible.ToDictionary(
                    static card => card,
                    static card => card.EnergyCost.GetWithModifiers(
                        CostModifiers.Local));

                await controller.AfterAutoPrePlayPhaseEntered(
                    choiceContext,
                    player);

                int[] deltas = eligible.Select(card =>
                        card.EnergyCost.GetWithModifiers(
                            CostModifiers.Local) - costsBefore[card])
                    .ToArray();
                Require(deltas.All(static delta => delta == 2),
                    $"Small Beak did not increase every eligible card "
                    + $"for player {player.NetId}: "
                    + $"deltas=[{string.Join(',', deltas)}].");

                int[] costsAfter = eligible.Select(card =>
                        card.EnergyCost.GetWithModifiers(
                            CostModifiers.Local))
                    .ToArray();
                await controller.AfterAutoPrePlayPhaseEntered(
                    choiceContext,
                    player);
                Require(eligible.Select(card =>
                            card.EnergyCost.GetWithModifiers(
                                CostModifiers.Local))
                        .SequenceEqual(costsAfter),
                    $"Small Beak processed player {player.NetId} twice in "
                    + "the same round.");
            }

            int[] voidCountsBefore = fight.Players.Select(player =>
                    player.PlayerCombatState?.DiscardPile.Cards
                        .Count(static card => card is VoidCard) ?? 0)
                .ToArray();
            await fight.Boss.DebugExecuteAction(
                PhilosophyFloorTwilightAction.BrilliantEyes,
                fight.CombatState.PlayerCreatures,
                plannedTargetSlot);
            int[] voidDeltas = fight.Players.Select((player, index) =>
                    (player.PlayerCombatState?.DiscardPile.Cards
                        .Count(static card => card is VoidCard) ?? 0)
                    - voidCountsBefore[index])
                .ToArray();
            Require(voidDeltas.Count(static delta => delta == 2) == 1
                    && voidDeltas.Count(static delta => delta == 0) == 1,
                "Brilliant Eyes did not add two Void cards to exactly one "
                + $"player discard pile: deltas=[{string.Join(',', voidDeltas)}].");
            await VerifyJudgmentHitsEveryLivingPlayer(fight);

            PhilosophyFloorLiberationEncounter liveEncounter =
                fight.CombatState.Encounter
                    as PhilosophyFloorLiberationEncounter
                ?? throw new InvalidOperationException(
                    "Fake multiplayer combat encounter type was incorrect.");
            var restoredEncounter =
                (PhilosophyFloorLiberationEncounter)ModelDb
                    .Encounter<PhilosophyFloorLiberationEncounter>()
                    .ToMutable();
            restoredEncounter.LoadCustomState(liveEncounter.SaveCustomState());
            restoredEncounter.GenerateMonstersWithSlots(NullRunState.Instance);
            PhilosophyFloorTwilight restoredBoss = restoredEncounter
                .MonstersWithSlots
                .Select(static entry => entry.Item1)
                .OfType<PhilosophyFloorTwilight>()
                .Single();
            Require(restoredBoss.HasPlannedMode == fight.Boss.HasPlannedMode
                    && restoredBoss.PlannedMode == fight.Boss.PlannedMode
                    && Enumerable.Range(0, 4).All(slot =>
                        restoredBoss.GetPlannedTargetCombatId(slot)
                        == fight.Boss.GetPlannedTargetCombatId(slot)),
                "Fake multiplayer save/load changed the planned mode or "
                + "target combat IDs.");
            Require(livingPlayerCombatIds.All(combatId =>
                    !restoredBoss.TryMarkSmallBeakPlayerProcessed(
                        77,
                        combatId)),
                "Fake multiplayer save/load lost Small Beak per-player "
                + "processing state.");
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "Philosophy floor fake multiplayer verifier cleanup");
        }
    }

    private static async Task VerifyEveryRuntimeAction(
        PhilosophyFloorCombatContext fight)
    {
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.ActiveEgg),
            PhilosophyFloorTwilightEgg.SmallBeak);
        foreach (PhilosophyFloorTwilightAction action in
                 Enum.GetValues<PhilosophyFloorTwilightAction>())
        {
            await CreatureCmd.SetCurrentHp(fight.Player, fight.Player.MaxHp);
            RuntimeActionSnapshot snapshot =
                await PrepareRuntimeActionContract(fight, action);
            int attackHistoryBefore = CombatManager.Instance.History.Entries
                .OfType<CreatureAttackedEntry>()
                .Count(entry => entry.Actor == fight.BossCreature);
            await fight.Boss.DebugExecuteAction(
                action,
                fight.CombatState.PlayerCreatures);
            int attackHistoryAfter = CombatManager.Instance.History.Entries
                .OfType<CreatureAttackedEntry>()
                .Count(entry => entry.Actor == fight.BossCreature);
            int expectedAttackCommands = ExpectedAttackCommandCount(action);
            Require(
                attackHistoryAfter - attackHistoryBefore
                == expectedAttackCommands,
                $"{action} attack command count mismatch: actual="
                + $"{attackHistoryAfter - attackHistoryBefore} "
                + $"expected={expectedAttackCommands}.");
            VerifyRuntimeActionContract(fight, action, snapshot);
        }
    }

    private sealed record RuntimeActionSnapshot(
        int BossHpBefore,
        int DiscardVoidCountBefore);

    private static async Task<RuntimeActionSnapshot>
        PrepareRuntimeActionContract(
            PhilosophyFloorCombatContext fight,
            PhilosophyFloorTwilightAction action)
    {
        switch (action)
        {
            case PhilosophyFloorTwilightAction.SlamDown:
            case PhilosophyFloorTwilightAction.Prowl:
                if (fight.BossCreature.Block > 0)
                {
                    await CreatureCmd.LoseBlock(
                        new BlockingPlayerChoiceContext(),
                        fight.BossCreature,
                        decimal.MaxValue,
                        fight.Player);
                }
                if (action == PhilosophyFloorTwilightAction.Prowl)
                {
                    await RemovePowers<LibraryStrongPower>(
                        fight.BossCreature);
                }
                break;
            case PhilosophyFloorTwilightAction.ProtectBlackForest:
                await RemovePowers<FrailPower>(fight.Player);
                await RemovePowers<WeakPower>(fight.Player);
                break;
            case PhilosophyFloorTwilightAction.TornMouth:
                await RemovePowers<LibraryBleedingPower>(fight.Player);
                break;
            case PhilosophyFloorTwilightAction.TiltedScale:
                await RemovePowers<PhilosophyFloorTwilightSinPower>(
                    fight.Player);
                break;
            case PhilosophyFloorTwilightAction.ForestLight:
                await RemovePowers<LibraryBindingPower>(fight.Player);
                await RemovePowers<LibraryOfRuinaConfusionPower>(
                    fight.Player);
                break;
            case PhilosophyFloorTwilightAction.Punishment:
                await RemovePowers<PhilosophyFloorTwilightSinPower>(
                    fight.Player);
                await PowerCmdCompat
                    .ApplyDebuff<PhilosophyFloorTwilightSinPower>(
                        fight.Player,
                        3,
                        fight.BossCreature,
                        null);
                await CreatureCmd.SetCurrentHp(
                    fight.BossCreature,
                    fight.BossCreature.MaxHp / 2);
                break;
            case PhilosophyFloorTwilightAction.Surveillance:
                await RemovePowers<PhilosophyFloorTwilightFearPower>(
                    fight.Player);
                await RemovePowers<StrengthPower>(fight.BossCreature);
                break;
        }

        return new RuntimeActionSnapshot(
            fight.BossCreature.CurrentHp,
            fight.Player.Player?.PlayerCombatState?.DiscardPile.Cards
                .Count(static card => card is VoidCard) ?? 0);
    }

    private static void VerifyRuntimeActionContract(
        PhilosophyFloorCombatContext fight,
        PhilosophyFloorTwilightAction action,
        RuntimeActionSnapshot snapshot)
    {
        switch (action)
        {
            case PhilosophyFloorTwilightAction.SlamDown:
                Require(fight.BossCreature.Block == 13,
                    "Slam Down did not grant exactly 13 Block.");
                break;
            case PhilosophyFloorTwilightAction.Prowl:
                Require(fight.BossCreature.Block == 16,
                    "Prowl did not grant exactly 16 Block.");
                LibraryStrongPower[] strong = fight.BossCreature
                    .GetPowerInstances<LibraryStrongPower>()
                    .Where(static power => power.AmountPlan.Count == 0)
                    .ToArray();
                Require(strong.Length == 1 && strong[0].Amount == 1,
                    "Prowl did not grant exactly 1 permanent Strong.");
                break;
            case PhilosophyFloorTwilightAction.ProtectBlackForest:
                Require(fight.Player.GetPower<FrailPower>()?.Amount == 2
                        && fight.Player.GetPower<WeakPower>()?.Amount == 2,
                    "Protect Black Forest did not apply 2 Vulnerable and "
                    + "2 Weak.");
                break;
            case PhilosophyFloorTwilightAction.TornMouth:
                Require(fight.Player.GetPower<LibraryBleedingPower>()?.Amount
                        == 9,
                    "Torn Mouth did not apply 3 Bleed on each of 3 hits.");
                break;
            case PhilosophyFloorTwilightAction.TiltedScale:
                Require(fight.Player
                            .GetPower<PhilosophyFloorTwilightSinPower>()
                            ?.Amount == 3,
                    "Tilted Scale did not apply exactly 3 Sin.");
                break;
            case PhilosophyFloorTwilightAction.ForestLight:
                LibraryBindingPower? binding = fight.Player
                    .GetPowerInstances<LibraryBindingPower>()
                    .SingleOrDefault(static power => power.TurnsRemaining > 0);
                Require(binding is { Amount: 6, TurnsRemaining: 2 }
                        && fight.Player
                            .GetPower<LibraryOfRuinaConfusionPower>()
                            ?.Amount == 1,
                    "Forest Light did not apply 2 turns of 6 Bind and "
                    + "1 Confusion.");
                break;
            case PhilosophyFloorTwilightAction.Punishment:
                int expectedHeal = (int)Math.Ceiling(
                    fight.BossCreature.MaxHp * 30m / 100m);
                Require(
                    fight.BossCreature.CurrentHp
                    == Math.Min(
                        fight.BossCreature.MaxHp,
                        snapshot.BossHpBefore + expectedHeal),
                    "Punishment did not heal exactly once for the target's "
                    + "3 Sin.");
                break;
            case PhilosophyFloorTwilightAction.BrilliantEyes:
                int voidCount = fight.Player.Player?.PlayerCombatState
                    ?.DiscardPile.Cards.Count(static card => card is VoidCard)
                    ?? 0;
                Require(voidCount - snapshot.DiscardVoidCountBefore == 2,
                    "Brilliant Eyes did not add exactly 2 Voids to discard.");
                break;
            case PhilosophyFloorTwilightAction.Surveillance:
                Require(fight.Player
                            .GetPower<PhilosophyFloorTwilightFearPower>()
                            ?.Amount == 1
                        && fight.BossCreature.GetPower<StrengthPower>() == null,
                    "Surveillance without Fear did not apply only Fear.");
                break;
        }
    }

    private static async Task VerifySurveillanceMutuallyExclusiveBranches(
        PhilosophyFloorCombatContext fight)
    {
        await RemovePowers<PhilosophyFloorTwilightFearPower>(fight.Player);
        await RemovePowers<StrengthPower>(fight.BossCreature);

        await fight.Boss.DebugExecuteAction(
            PhilosophyFloorTwilightAction.Surveillance,
            fight.CombatState.PlayerCreatures);
        PhilosophyFloorTwilightFearPower? appliedFear = fight.Player
            .GetPower<PhilosophyFloorTwilightFearPower>();
        Require(appliedFear?.Amount == 1
                && fight.BossCreature.GetPower<StrengthPower>() == null,
            "Surveillance's no-Fear branch did not apply only permanent Fear.");

        await fight.Boss.DebugExecuteAction(
            PhilosophyFloorTwilightAction.Surveillance,
            fight.CombatState.PlayerCreatures);
        Require(ReferenceEquals(
                    fight.Player.GetPower<PhilosophyFloorTwilightFearPower>(),
                    appliedFear)
                && appliedFear?.Amount == 1
                && fight.BossCreature.GetPower<StrengthPower>()?.Amount == 2,
            "Surveillance's existing-Fear branch did not grant only 2 "
            + "Strength.");
    }

    private static async Task VerifySinHpLossAndDecrement(
        PhilosophyFloorCombatContext fight)
    {
        await RemovePowers<PhilosophyFloorTwilightSinPower>(fight.Player);
        await CreatureCmd.SetCurrentHp(fight.Player, fight.Player.MaxHp);
        PhilosophyFloorTwilightSinPower? sin =
            await PowerCmdCompat
                .ApplyDebuff<PhilosophyFloorTwilightSinPower>(
                    fight.Player,
                    2,
                    fight.BossCreature,
                    null);
        Require(sin?.Amount == 2,
            "Sin card-play verifier could not apply two Sin.");

        Player player = fight.Player.Player
            ?? throw new InvalidOperationException(
                "Sin card-play verifier player model was missing.");
        CardModel card = CardPile.GetCards(
                player,
                PileType.Hand,
                PileType.Draw,
                PileType.Discard)
            .First();
        var cardPlay = new CardPlay
        {
            Card = card,
            Player = player,
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
        var context = new BlockingPlayerChoiceContext();
        int hpBefore = fight.Player.CurrentHp;

        await sin!.AfterCardPlayed(context, cardPlay);
        Require(sin.Amount == 1 && fight.Player.CurrentHp == hpBefore - 2,
            "Playing one card at two Sin did not lose 2 HP then remove "
            + "exactly one Sin.");
        await sin.AfterCardPlayed(context, cardPlay);
        Require(fight.Player.GetPower<PhilosophyFloorTwilightSinPower>()
                    == null
                && fight.Player.CurrentHp == hpBefore - 3,
            "Playing one card at one Sin did not lose 1 HP then remove "
            + "the Sin power.");
    }

    private static int ExpectedAttackCommandCount(
        PhilosophyFloorTwilightAction action) => action switch
        {
            PhilosophyFloorTwilightAction.SlamDown => 1,
            PhilosophyFloorTwilightAction.Talon => 3,
            PhilosophyFloorTwilightAction.ProtectBlackForest => 1,
            PhilosophyFloorTwilightAction.TornMouth => 3,
            PhilosophyFloorTwilightAction.TiltedScale => 1,
            PhilosophyFloorTwilightAction.Punishment => 4,
            PhilosophyFloorTwilightAction.PeaceForAll => 1,
            PhilosophyFloorTwilightAction.Judgment => 1,
            _ => 0
        };

    private static async Task RemovePowers<TPower>(Creature creature)
        where TPower : PowerModel
    {
        foreach (TPower power in creature
                     .GetPowerInstances<TPower>()
                     .ToArray())
        {
            await PowerCmd.Remove(power);
        }
    }

    private static async Task VerifyImmediatePostBreakEggEffects(
        PhilosophyFloorCombatContext fight)
    {
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.AliveEggMask),
            PhilosophyFloorTwilight.AllEggMask);
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.ActiveEgg),
            PhilosophyFloorTwilightEgg.SmallBeak);
        fight.CombatState.RoundNumber = 9;

        await PowerCmdCompat.ApplyDebuff<PhilosophyFloorTwilightSinPower>(
            fight.BossCreature,
            1,
            fight.BossCreature,
            null);
        Require(
            fight.BossCreature
                .GetPower<PhilosophyFloorTwilightSinPower>() != null,
            "Post-break regression setup failed to apply a visible debuff.");

        await LibraryCreatureCmd.Stun(fight.BossCreature);
        Require(
            fight.Boss.ActiveEgg == PhilosophyFloorTwilightEgg.LongArms,
            "Breaking Small Beak did not immediately activate Long Arms.");
        Require(
            fight.BossCreature.GetChaosResistanceLevel(
                LibraryDamageType.Pierce) == LibraryResistanceLevel.Vulnerable
            && fight.BossCreature.GetChaosResistanceLevel(
                LibraryDamageType.Slash) == LibraryResistanceLevel.Resist
            && fight.BossCreature.GetChaosResistanceLevel(
                LibraryDamageType.Blunt) == LibraryResistanceLevel.Resist,
            "Post-break resistance matrix did not immediately follow the newly active egg.");
        Require(
            fight.BossCreature
                .GetPower<PhilosophyFloorTwilightSinPower>() == null,
            "Newly activated Long Arms did not immediately clear visible debuffs.");
    }

    private static async Task VerifyPreStunnedChaosDepletionBreaksEgg(
        PhilosophyFloorCombatContext fight)
    {
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.AliveEggMask),
            PhilosophyFloorTwilight.AllEggMask);
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.ActiveEgg),
            PhilosophyFloorTwilightEgg.SmallBeak);
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.BrokenEggRecoveryPending),
            false);
        fight.CombatState.RoundNumber = 9;

        fight.BossCreature.StunInternal(
            static _ => Task.CompletedTask,
            fight.Boss.NextMove.Id);
        fight.BossCreature.SetCurrentChaoValueInternal(1m);
        Require(
            fight.BossCreature.IsStunned
            && fight.BossCreature.CurrentChaoValue == 1
            && fight.Boss.AliveEggMask
            == PhilosophyFloorTwilight.AllEggMask,
            "Pre-stunned Chao-depletion regression setup failed.");

        await LibraryCreatureCmd.ChaoDamage(
            new ThrowingPlayerChoiceContext(),
            fight.BossCreature,
            1m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            fight.Player,
            null,
            LibraryDamageType.Blunt);

        Require(
            fight.Boss.AliveEggMask
            == (PhilosophyFloorTwilight.AllEggMask
                & ~PhilosophyFloorTwilight.EggBit(
                    PhilosophyFloorTwilightEgg.SmallBeak))
            && fight.Boss.ActiveEgg
            == PhilosophyFloorTwilightEgg.LongArms
            && fight.Boss.BrokenEggRecoveryPending,
            "Chao depletion while already stunned did not immediately break "
            + "the active egg: "
            + $"mask={fight.Boss.AliveEggMask} "
            + $"active={fight.Boss.ActiveEgg} "
            + $"pending={fight.Boss.BrokenEggRecoveryPending} "
            + $"chao={fight.BossCreature.CurrentChaoValue} "
            + $"stunned={fight.BossCreature.IsStunned}.");
    }

    private static async Task VerifyJudgmentPowerBypass(
        PhilosophyFloorCombatContext fight)
    {
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.AliveEggMask),
            PhilosophyFloorTwilight.AllEggMask);
        SetProperty(
            fight.Boss,
            nameof(fight.Boss.ActiveEgg),
            PhilosophyFloorTwilightEgg.SmallBeak);

        StrengthPower? strength =
            await PowerCmdCompat.Apply<StrengthPower>(
                fight.BossCreature,
                1000,
                fight.BossCreature,
                null);
        IntangiblePower? intangible =
            await PowerCmdCompat.Apply<IntangiblePower>(
                fight.Player,
                1,
                fight.Player,
                null);
        Require(strength != null && intangible != null,
            "Judgment Power-bypass regression setup failed.");

        var choiceContext = new ThrowingPlayerChoiceContext();
        await CreatureCmd.LoseBlock(
            choiceContext,
            fight.Player,
            decimal.MaxValue,
            fight.BossCreature);
        const int startingBlock = 137;
        await CreatureCmd.GainBlock(
            fight.Player,
            startingBlock,
            ValueProp.Unpowered,
            null,
            fast: true);
        await CreatureCmd.SetCurrentHp(fight.Player, fight.Player.MaxHp);

        int hpBefore = fight.Player.CurrentHp;
        int rawJudgmentDamage = Math.Max(
            1,
            (int)Math.Floor(fight.Player.MaxHp * 20m / 100m));
        int expectedHpLoss = Math.Max(
            0,
            rawJudgmentDamage - startingBlock);
        await fight.Boss.DebugExecuteAction(
            PhilosophyFloorTwilightAction.Judgment,
            fight.CombatState.PlayerCreatures);
        Require(
            fight.Player.CurrentHp == hpBefore - expectedHpLoss,
            "Judgment was modified by Strength or Intangible: "
            + $"before={hpBefore} after={fight.Player.CurrentHp} "
            + $"expectedLoss={expectedHpLoss}.");
        Require(fight.Player.Block == 0,
            "Judgment did not consume normal block before fixed HP loss.");

        await CreatureCmd.SetCurrentHp(fight.Player, fight.Player.MaxHp);
        await CreatureCmd.LoseBlock(
            choiceContext,
            fight.Player,
            decimal.MaxValue,
            fight.BossCreature);
        int ordinaryHpBefore = fight.Player.CurrentHp;
        await CreatureCmd.Damage(
            choiceContext,
            fight.Player,
            50m,
            ValueProp.Unpowered,
            fight.BossCreature,
            null,
            null);
        Require(fight.Player.CurrentHp == ordinaryHpBefore - 1,
            "Judgment Power-bypass context leaked into ordinary damage.");
    }

    private static void VerifyJudgmentIntentPreview(
        PhilosophyFloorMultiplayerCombatContext fight)
    {
        Creature[] players = fight.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.CombatId)
            .ToArray();
        int[] expectedDamages = players
            .Select(PhilosophyFloorTwilightJudgmentIntent.CalculateDamage)
            .ToArray();
        Require(expectedDamages.Distinct().Count() == players.Length,
            "Judgment preview regression requires players with different "
            + "fixed damage values.");
        AbstractIntent intent = fight.Boss.DebugCreateActionIntent(
            PhilosophyFloorTwilightAction.Judgment,
            slot: 0);
        Require(intent is PhilosophyFloorTwilightJudgmentIntent
                && intent is IIntentTargetLineProvider
                && intent is IndiscriminateAttackIntent
                && intent is IGroupAttackIntent { IsGroupAttack: true }
                && intent is IBadgedIntent,
            "Judgment intent is not an indiscriminate group attack.");
        var judgment = (PhilosophyFloorTwilightJudgmentIntent)intent;

        int actualTierDamage = judgment.GetTotalDamage(
            fight.CombatState.PlayerCreatures,
            fight.BossCreature);
        string label = judgment.GetIntentLabel(
                fight.CombatState.PlayerCreatures,
                fight.BossCreature)
            .GetFormattedText() ?? string.Empty;
        string expectedDamageList = string.Join("/", expectedDamages);
        IReadOnlyList<IntentTargetLineTarget> targetLines =
            judgment
            .GetIntentTargetLineTargets(
                fight.BossCreature,
                fight.CombatState.PlayerCreatures);
        Require(actualTierDamage == expectedDamages.Max()
                && label.Contains(expectedDamageList,
                    StringComparison.Ordinal)
                && !label.Contains('%')
                && targetLines.Count == players.Length
                && targetLines.Select(static line => line.Target)
                    .Distinct()
                    .OrderBy(static target => target.CombatId)
                    .SequenceEqual(players.OrderBy(static target =>
                        target.CombatId)),
            "Judgment intent did not expose every player's independent "
            + $"fixed damage: expected={expectedDamageList} "
            + $"tier={actualTierDamage} label={label} "
            + $"lines={targetLines.Count}.");
    }

    private static async Task VerifyVanillaAttackHitsEveryLivingPlayer(
        PhilosophyFloorMultiplayerCombatContext fight)
    {
        Creature[] players = fight.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .ToArray();
        var choiceContext = new ThrowingPlayerChoiceContext();
        foreach (Creature player in players)
        {
            await CreatureCmd.SetCurrentHp(player, player.MaxHp);
            await CreatureCmd.LoseBlock(
                choiceContext,
                player,
                decimal.MaxValue,
                fight.BossCreature);
        }

        int[] hpBefore = players.Select(static player => player.CurrentHp)
            .ToArray();
        await fight.Boss.DebugExecuteAction(
            PhilosophyFloorTwilightAction.SlamDown,
            fight.CombatState.PlayerCreatures);
        Require(players.Select((player, index) =>
                    hpBefore[index] - player.CurrentHp)
                .All(static hpLoss => hpLoss > 0),
            "Vanilla Twilight attack did not hit every living player.");
        foreach (Creature player in players)
        {
            await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        }
    }

    private static async Task VerifyJudgmentHitsEveryLivingPlayer(
        PhilosophyFloorMultiplayerCombatContext fight)
    {
        Creature[] players = fight.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .ToArray();
        var choiceContext = new ThrowingPlayerChoiceContext();
        foreach (Creature player in players)
        {
            await CreatureCmd.SetCurrentHp(player, player.MaxHp);
            await CreatureCmd.LoseBlock(
                choiceContext,
                player,
                decimal.MaxValue,
                fight.BossCreature);
        }

        int[] hpBefore = players.Select(static player => player.CurrentHp)
            .ToArray();
        await fight.Boss.DebugExecuteAction(
            PhilosophyFloorTwilightAction.Judgment,
            fight.CombatState.PlayerCreatures);
        for (int index = 0; index < players.Length; index++)
        {
            int expectedLoss = PhilosophyFloorTwilightJudgmentIntent
                .CalculateDamage(players[index]);
            int actualLoss = hpBefore[index] - players[index].CurrentHp;
            Require(actualLoss == expectedLoss,
                "Judgment did not resolve fixed damage against every living "
                + $"player: target={players[index].Name} "
                + $"expected={expectedLoss} actual={actualLoss}.");
        }
    }

    private static PhilosophyFloorTwilightAction[]
        ExpectedActionsForCurrentPlan(PhilosophyFloorTwilight boss) =>
            boss.PlannedMode switch
            {
                PhilosophyFloorTwilightMode.Surveillance =>
                [
                    PhilosophyFloorTwilightAction.Surveillance,
                    PhilosophyFloorTwilightAction.BrilliantEyes,
                    PhilosophyFloorTwilightAction.ForestLight
                ],
                PhilosophyFloorTwilightMode.Punishment =>
                [
                    PhilosophyFloorTwilightAction.Punishment,
                    PhilosophyFloorTwilightAction.TornMouth,
                    PhilosophyFloorTwilightAction.SlamDown
                ],
                PhilosophyFloorTwilightMode.SinTrace =>
                [
                    PhilosophyFloorTwilightAction.TiltedScale,
                    PhilosophyFloorTwilightAction.SlamDown,
                    PhilosophyFloorTwilightAction.Talon
                ],
                PhilosophyFloorTwilightMode.Judgment =>
                [
                    PhilosophyFloorTwilightAction.Judgment,
                    PhilosophyFloorTwilightAction.TiltedScale,
                    PhilosophyFloorTwilightAction.Prowl
                ],
                PhilosophyFloorTwilightMode.EndOne =>
                [
                    PhilosophyFloorTwilightAction.PeaceForAll,
                    PhilosophyFloorTwilightAction.ProtectBlackForest,
                    PhilosophyFloorTwilightAction.ForestLight
                ],
                PhilosophyFloorTwilightMode.EndTwo =>
                [
                    PhilosophyFloorTwilightAction.PeaceForAll,
                    PhilosophyFloorTwilightAction.Prowl,
                    PhilosophyFloorTwilightAction.SlamDown
                ],
                PhilosophyFloorTwilightMode.EndThree =>
                [
                    PhilosophyFloorTwilightAction.ProtectBlackForest,
                    PhilosophyFloorTwilightAction.ForestLight,
                    PhilosophyFloorTwilightAction.Talon,
                    PhilosophyFloorTwilightAction.SlamDown
                ],
                PhilosophyFloorTwilightMode.Other =>
                [
                    boss.PlannedOtherFirstAction,
                    boss.PlannedOtherSecondAction,
                    PhilosophyFloorTwilightAction.SlamDown
                ],
                _ => []
            };

    private static async Task<PhilosophyFloorCombatContext> StartFight(
        string seed)
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
        if (RunManager.Instance.DebugOnlyGetState()?.Players.Single()
                .GetRelic<BurningBlood>() is { } burningBlood)
        {
            await RelicCmd.Remove(burningBlood);
        }

        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            ModelDb.Encounter<PhilosophyFloorLiberationEncounter>()
                .ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "Philosophy floor combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        PhilosophyFloorTwilight boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<PhilosophyFloorTwilight>()
            .Single();
        return new PhilosophyFloorCombatContext(
            combatState,
            boss,
            boss.Creature as LibraryCreature
                ?? throw new InvalidOperationException(
                    "Twilight creature is not a LibraryCreature."),
            combatState.PlayerCreatures.Single());
    }

    private static async Task<PhilosophyFloorMultiplayerCombatContext>
        StartFakeMultiplayerFight(string seed)
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
            seed);
        RunManager.Instance.SetUpNewSingleplayer(
            runState,
            shouldSave: false);

        MethodInfo startRun = typeof(NGame).GetMethod(
                "StartRun",
                PrivateInstance)
            ?? throw new InvalidOperationException(
                "NGame.StartRun was unavailable for fake multiplayer.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException(
                "NGame.StartRun did not return a Task.");
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
            RoomType.Boss,
            MapPointType.Boss,
            ModelDb.Encounter<PhilosophyFloorLiberationEncounter>()
                .ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null,
            "Philosophy floor fake multiplayer combat start");
        await WaitUntil(
            () => players.All(player =>
                player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
            "Philosophy floor fake multiplayer opening draws");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        PhilosophyFloorTwilight boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<PhilosophyFloorTwilight>()
            .Single();
        return new PhilosophyFloorMultiplayerCombatContext(
            combatState,
            boss,
            boss.Creature as LibraryCreature
                ?? throw new InvalidOperationException(
                    "Twilight creature is not a LibraryCreature."),
            players);
    }

    private static PhilosophyFloorTwilight CreateDetachedBoss()
    {
        var boss = (PhilosophyFloorTwilight)ModelDb
            .Monster<PhilosophyFloorTwilight>()
            .ToMutable();
        _ = new Creature(
            boss,
            CombatSide.Enemy,
            PhilosophyFloorLiberationEncounter.TwilightSlot);
        return boss;
    }

    private static void SetEggState(
        PhilosophyFloorTwilight boss,
        PhilosophyFloorTwilightEgg egg)
    {
        SetProperty(boss, nameof(boss.AliveEggMask), ExpectedEggBit(egg));
        SetProperty(boss, nameof(boss.ActiveEgg), egg);
    }

    private static void ResetPlanningMarkers(PhilosophyFloorTwilight boss)
    {
        SetProperty(
            boss,
            nameof(boss.PlannedBranchCounter),
            PhilosophyFloorTwilightBranchCounter.None);
        SetProperty(boss, nameof(boss.PlannedUsesEndFallback), false);
    }

    private static PhilosophyFloorTwilightEgg ExpectedActiveEgg(
        int round,
        int aliveMask)
    {
        PhilosophyFloorTwilightEgg[] priority = ((round - 1) % 6) switch
        {
            0 or 1 =>
            [
                PhilosophyFloorTwilightEgg.BigEyes,
                PhilosophyFloorTwilightEgg.SmallBeak,
                PhilosophyFloorTwilightEgg.LongArms
            ],
            2 or 3 =>
            [
                PhilosophyFloorTwilightEgg.SmallBeak,
                PhilosophyFloorTwilightEgg.LongArms,
                PhilosophyFloorTwilightEgg.BigEyes
            ],
            _ =>
            [
                PhilosophyFloorTwilightEgg.LongArms,
                PhilosophyFloorTwilightEgg.BigEyes,
                PhilosophyFloorTwilightEgg.SmallBeak
            ]
        };
        return priority.FirstOrDefault(
            egg => (aliveMask & ExpectedEggBit(egg)) != 0);
    }

    private static int ExpectedEggBit(PhilosophyFloorTwilightEgg egg) =>
        egg switch
        {
            PhilosophyFloorTwilightEgg.BigEyes => 0b001,
            PhilosophyFloorTwilightEgg.SmallBeak => 0b010,
            PhilosophyFloorTwilightEgg.LongArms => 0b100,
            _ => 0
        };

    private static T InvokePrivate<T>(
        PhilosophyFloorTwilight boss,
        string methodName,
        params object?[] args)
    {
        MethodInfo method = typeof(PhilosophyFloorTwilight).GetMethod(
                methodName,
                PrivateInstance)
            ?? throw new InvalidOperationException(
                "Missing private boss method: " + methodName);
        object? result = method.Invoke(boss, args);
        if (result is T typed)
        {
            return typed;
        }

        if (result == null && default(T) == null)
        {
            return default!;
        }

        throw new InvalidOperationException(
            "Unexpected result from private boss method: " + methodName);
    }

    private static void SetProperty<T>(
        PhilosophyFloorTwilight boss,
        string propertyName,
        T value)
    {
        PropertyInfo property = typeof(PhilosophyFloorTwilight).GetProperty(
                propertyName,
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Missing boss property: " + propertyName);
        property.SetValue(boss, value);
    }

    private static int GetPrivateStaticInt(string propertyName)
    {
        PropertyInfo property = typeof(PhilosophyFloorTwilight).GetProperty(
                propertyName,
                PrivateStatic)
            ?? throw new InvalidOperationException(
                "Missing private boss property: " + propertyName);
        return property.GetValue(null) is int value
            ? value
            : throw new InvalidOperationException(
                "Private boss property was not an int: " + propertyName);
    }

    private static bool GetPrivateStaticBool(
        string methodName,
        params object?[] args)
    {
        MethodInfo method = typeof(PhilosophyFloorTwilight).GetMethod(
                methodName,
                PrivateStatic)
            ?? throw new InvalidOperationException(
                "Missing private static boss method: " + methodName);
        return method.Invoke(null, args) is bool value
            ? value
            : throw new InvalidOperationException(
                "Private boss method did not return bool: " + methodName);
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
