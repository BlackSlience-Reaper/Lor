using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.RedMist;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.RedMist;
using LibraryOfRuina.specialguests.Kali;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class EnemyCardIntentVerificationPatch
{
    private const string VerifyArg = "lor-verify-enemycards";
    private const string LayoutVerifyArg = "lor-verify-kali-layout";
    private const string LogPrefix = "[LibraryOfRuina.EnemyCards.Verify] ";

    private static bool _started;

    internal static void Start()
    {
        bool verifyAll = HasVerifyArg(VerifyArg);
        bool verifyLayout = HasVerifyArg(LayoutVerifyArg);
        if (_started || (!verifyAll && !verifyLayout))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(verifyLayout ? RunLayoutAsync() : RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg(string verifyArg)
    {
        return CommandLineHelper.HasArg(verifyArg)
               || Environment.GetCommandLineArgs()
                   .Any(arg => string.Equals(arg.TrimStart('-'), verifyArg, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunLayoutAsync()
    {
        try
        {
            RedMistCombatContext fight = await StartRedMistFight(
                "KALILAYOUTVERIFY");
            VerifyKaliLayout(fight.KaliNode, requireLegacyHeadCenter: false);
            Log.Info(LogPrefix + "KALI_LAYOUT_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "KALI_LAYOUT_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunAsync()
    {
        try
        {
            await RunCoreAsync();
            Log.Info(LogPrefix + "RED_MIST_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "RED_MIST_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        RedMistCombatContext firstFight = await StartRedMistFight("REDMISTVERIFY1");

        await firstFight.KaliNode.RefreshIntents();
        await WaitFrames(3);

        VerifyNormalIdleVisual(firstFight.KaliNode, "first RedMist fight initial visual");
        VerifyKaliStats(firstFight.KaliCreature, firstFight.LibraryKali);
        await VerifyKaliPowersAndLayout(firstFight.KaliCreature, firstFight.KaliNode);
        VerifyIntentNodes(firstFight.Kali, firstFight.KaliNode);
        VerifyRuntimePlan(firstFight.Kali);
        await VerifyCardSequenceExecution(firstFight.Kali, firstFight.CombatState);
        await VerifyOpeningFollowUpPlans(firstFight.Kali, firstFight.KaliNode);
        await VerifyEgoLifecycle(firstFight.Kali, firstFight.KaliCreature, firstFight.LibraryKali);

        RunManager.Instance.CleanUp(graceful: true);
        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                         && CombatManager.Instance.DebugOnlyGetState() == null
                         && RunManager.Instance.DebugOnlyGetState() == null,
            "RedMist first combat cleanup");
        Log.Info(LogPrefix + "FirstRedMistAfterEgo cleanup=clean");

        RedMistCombatContext secondFight = await StartRedMistFight("REDMISTVERIFY2");
        await WaitFrames(3);

        Require(!secondFight.Kali.IsEgoActive, "Fresh RedMist fight started with Kali EGO active.");
        Require(secondFight.KaliCreature.GetPower<RedMistEgoPower>() == null, "Fresh RedMist fight started with RedMistEgoPower.");
        VerifyNormalIdleVisual(secondFight.KaliNode, "second RedMist fight initial visual after previous EGO");
        Log.Info(LogPrefix + "FreshRedMistAfterEgo visual=normal");
    }

    private sealed record RedMistCombatContext(
        CombatState CombatState,
        Kali Kali,
        Creature KaliCreature,
        LibraryCreature LibraryKali,
        NCreature KaliNode);

    private static async Task<RedMistCombatContext> StartRedMistFight(string seed)
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
            RoomType.Elite,
            MapPointType.Elite,
            ModelDb.Encounter<RedMistElite>().ToMutable(),
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
            "RedMistElite combat start");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        Kali kali = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<Kali>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("Kali enemy was not found.");
        Creature kaliCreature = kali.Creature;
        LibraryCreature libraryKali = kaliCreature as LibraryCreature
            ?? throw new InvalidOperationException("Kali creature is not a LibraryCreature.");
        NCreature kaliNode = await WaitFor(
            () => NCombatRoom.Instance?.GetCreatureNode(kaliCreature),
            "Kali creature node");

        return new RedMistCombatContext(combatState, kali, kaliCreature, libraryKali, kaliNode);
    }

    private static void VerifyNormalIdleVisual(NCreature kaliNode, string label)
    {
        Sprite2D sprite = kaliNode.Visuals.GetNode<Sprite2D>("%Visuals");
        string? texturePath = sprite.Texture?.ResourcePath;
        Require(texturePath == Kali.IdleTexturePath, label + " was not normal idle: " + (texturePath ?? "<null>"));
        Log.Info(LogPrefix + label + " texture=" + texturePath);
    }

    private static void VerifyKaliStats(Creature kaliCreature, LibraryCreature libraryKali)
    {
        Require(kaliCreature.MaxHp is >= 685 and <= 700, "Kali max HP is outside 685..700: " + kaliCreature.MaxHp);
        Require(libraryKali.MaxChaoValue == Kali.DefaultChaoMax, "Kali max chao is not 150: " + libraryKali.MaxChaoValue);
        Require(libraryKali.CurrentChaoValue == Kali.DefaultChaoMax, "Kali current chao is not 150: " + libraryKali.CurrentChaoValue);

        Require(libraryKali.GetPhysicalResistanceLevel(LibraryDamageType.Slash) == LibraryResistanceLevel.Normal, "Slash physical resistance mismatch.");
        Require(libraryKali.GetPhysicalResistanceLevel(LibraryDamageType.Pierce) == LibraryResistanceLevel.Endure, "Pierce physical resistance mismatch.");
        Require(libraryKali.GetPhysicalResistanceLevel(LibraryDamageType.Blunt) == LibraryResistanceLevel.Normal, "Blunt physical resistance mismatch.");
        Require(libraryKali.GetChaosResistanceLevel(LibraryDamageType.Slash) == LibraryResistanceLevel.Normal, "Slash chao resistance mismatch.");
        Require(libraryKali.GetChaosResistanceLevel(LibraryDamageType.Pierce) == LibraryResistanceLevel.Normal, "Pierce chao resistance mismatch.");
        Require(libraryKali.GetChaosResistanceLevel(LibraryDamageType.Blunt) == LibraryResistanceLevel.Endure, "Blunt chao resistance mismatch.");
    }

    private static async Task VerifyKaliPowersAndLayout(Creature kaliCreature, NCreature kaliNode)
    {
        Require(kaliCreature.GetPower<KaliPower>() != null, "KaliPower was not applied.");
        Require(kaliCreature.GetPower<RedMistStrongestOnePower>() != null, "RedMistStrongestOnePower was not applied.");

        IReadOnlyList<NPower> powerNodes = await WaitForPowerNodes(
            kaliNode,
            typeof(KaliPower),
            typeof(RedMistStrongestOnePower));
        RequirePowerIcon(powerNodes, typeof(KaliPower));
        RequirePowerIcon(powerNodes, typeof(RedMistStrongestOnePower));

        VerifyKaliLayout(kaliNode);
    }

    private static void VerifyKaliLayout(
        NCreature kaliNode,
        bool requireLegacyHeadCenter = true)
    {
        NCreatureStateDisplay stateDisplay = kaliNode.GetNode<NCreatureStateDisplay>("%HealthBar");
        NHealthBar healthBar = stateDisplay.GetNode<NHealthBar>("%HealthBar");
        Sprite2D sprite = kaliNode.Visuals.GetNode<Sprite2D>("%Visuals");

        Vector2 hitboxCenter = kaliNode.Hitbox.GlobalPosition + kaliNode.Hitbox.Size * 0.5f;
        Vector2 hpCenter = healthBar.HpBarContainer.GlobalPosition + healthBar.HpBarContainer.Size * 0.5f;
        Vector2 spriteAnchor = sprite.GlobalPosition;
        float visibleHeadCenterX = GetUpperVisibleCenterX(sprite);
        float footToHpTop = healthBar.HpBarContainer.GlobalPosition.Y - spriteAnchor.Y;
        float intentCenterX = kaliNode.IntentContainer.GlobalPosition.X + kaliNode.IntentContainer.Size.X * 0.5f;

        Log.Info(LogPrefix
                 + "KaliLayout "
                 + $"spriteAnchor={spriteAnchor} "
                 + $"visibleHeadCenterX={visibleHeadCenterX} "
                 + $"hitboxGlobal={kaliNode.Hitbox.GlobalPosition} hitboxSize={kaliNode.Hitbox.Size} "
                 + $"hpGlobal={healthBar.HpBarContainer.GlobalPosition} hpSize={healthBar.HpBarContainer.Size} "
                 + $"intentGlobal={kaliNode.IntentContainer.GlobalPosition} intentSize={kaliNode.IntentContainer.Size}");

        Require(Mathf.Abs(hitboxCenter.X - hpCenter.X) <= 2f, "Kali HP bar is not centered on hitbox.");
        if (requireLegacyHeadCenter)
        {
            Require(Mathf.Abs(visibleHeadCenterX - hpCenter.X) <= 24f, "Kali visible head is not centered on HP bar.");
        }
        Require(Mathf.Abs(intentCenterX - hpCenter.X) <= 12f, "Kali intent container is not centered on HP bar.");
        Require(footToHpTop is >= -20f and <= 48f, "Kali sprite feet are not aligned with HP bar: " + footToHpTop);
        Require(kaliNode.IntentContainer.GlobalPosition.Y > kaliNode.Hitbox.GlobalPosition.Y - 80f, "Kali intent container is too high.");
        Require(kaliNode.IntentContainer.GlobalPosition.Y < kaliNode.Hitbox.GlobalPosition.Y + 80f, "Kali intent container is too low.");
    }

    private static float GetUpperVisibleCenterX(Sprite2D sprite)
    {
        Texture2D texture = sprite.Texture ?? throw new InvalidOperationException("Kali sprite texture was not loaded.");
        Image image = texture.GetImage() ?? throw new InvalidOperationException("Kali sprite image was not available.");
        int width = image.GetWidth();
        int height = image.GetHeight();
        int yEnd = Math.Min(height, 360);
        int minX = width;
        int maxX = -1;

        for (int y = 0; y < yEnd; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (image.GetPixel(x, y).A <= 0.03f)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
            }
        }

        Require(maxX >= minX, "Kali visible head pixels were not found.");
        float textureCenterX = (minX + maxX) * 0.5f;
        float pivotX = width * 0.5f;
        return sprite.GlobalPosition.X + (textureCenterX - pivotX) * Math.Abs(sprite.GlobalScale.X);
    }

    private static async Task<IReadOnlyList<NPower>> WaitForPowerNodes(NCreature creatureNode, params Type[] requiredTypes)
    {
        return await WaitFor(
            () =>
            {
                NCreatureStateDisplay? stateDisplay = creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
                NPowerContainer? powerContainer = stateDisplay?.GetNodeOrNull<NPowerContainer>("%PowerContainer");
                IReadOnlyList<NPower> powerNodes = powerContainer?
                    .GetChildren()
                    .OfType<NPower>()
                    .ToArray()
                    ?? [];
                if (powerNodes.Count == 0)
                {
                    return null;
                }

                return requiredTypes.All(type => powerNodes.Any(node => type.IsInstanceOfType(node.Model)))
                    ? powerNodes
                    : null;
            },
            "Kali power UI nodes");
    }

    private static void RequirePowerIcon(IReadOnlyList<NPower> powerNodes, Type powerType)
    {
        NPower? powerNode = powerNodes.FirstOrDefault(node => node.Model.GetType() == powerType);
        TextureRect? icon = powerNode?.GetNodeOrNull<TextureRect>("%Icon");
        Require(icon?.Texture != null, powerType.Name + " UI icon texture was not loaded.");
    }

    private static void VerifyIntentNodes(Kali kali, NCreature kaliNode)
    {
        IReadOnlyList<string> expectedPlan = CurrentPlanIds(kali);
        IReadOnlyList<NIntent> intentNodes = kaliNode.IntentContainer.GetChildren().OfType<NIntent>().ToArray();
        Require(intentNodes.Count == expectedPlan.Count, "Unexpected intent node count: " + intentNodes.Count);

        IReadOnlyList<EnemyCardIntentVisualNode> cardVisuals = intentNodes
            .Select(GetCardIntentVisual)
            .Where(static visual => visual != null)
            .Cast<EnemyCardIntentVisualNode>()
            .ToArray();
        Require(cardVisuals.Count == expectedPlan.Count, "Unexpected enemy card visual count: " + cardVisuals.Count);
        RequireSequence(cardVisuals.Select(static visual => visual.CardId).ToArray(), expectedPlan, "intent card visual order");
    }

    private static EnemyCardIntentVisualNode? GetCardIntentVisual(NIntent intentNode)
    {
        if (!intentNode.HasNode("%IntentHolder"))
        {
            return null;
        }

        return intentNode.GetNode<Control>("%IntentHolder")
            .GetChildren()
            .OfType<EnemyCardIntentVisualNode>()
            .FirstOrDefault();
    }

    private static void VerifyRuntimePlan(Kali kali)
    {
        EnemyCardRuntime runtime = kali.EnemyCards
            ?? throw new InvalidOperationException("Kali enemy-card runtime is null.");
        VerifyPlanShape(kali, runtime.CurrentPlan.Select(static card => card.Id).ToArray(), "runtime plan");
    }

    private static async Task VerifyCardSequenceExecution(Kali kali, CombatState combatState)
    {
        EnemyCardRuntime runtime = kali.EnemyCards
            ?? throw new InvalidOperationException("Kali enemy-card runtime is null.");
        IReadOnlyList<Creature> targets = combatState.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .ToArray();
        Require(targets.Count > 0, "No live player targets for RedMist card sequence.");

        int expectedCount = runtime.CurrentPlan.Count;
        await runtime.PlayPlannedHandLikeDownfall(targets);
        Require(runtime.CurrentPlanIndex == expectedCount, "RedMist card sequence did not finish.");
        Require(targets.Any(static creature => creature.IsAlive), "RedMist verification player died during card sequence.");
    }

    private static async Task VerifyOpeningFollowUpPlans(Kali kali, NCreature kaliNode)
    {
        EnemyCardRuntime runtime = kali.EnemyCards
            ?? throw new InvalidOperationException("Kali enemy-card runtime is null.");

        runtime.RefreshDefaultPlan();
        await kaliNode.RefreshIntents();
        await WaitFrames(3);
        VerifyRuntimePlan(kali);
        VerifyIntentNodes(kali, kaliNode);

        runtime.RefreshDefaultPlan();
        await kaliNode.RefreshIntents();
        await WaitFrames(3);
        VerifyRuntimePlan(kali);
        VerifyIntentNodes(kali, kaliNode);
    }

    private static IReadOnlyList<string> CurrentPlanIds(Kali kali)
    {
        EnemyCardRuntime runtime = kali.EnemyCards
            ?? throw new InvalidOperationException("Kali enemy-card runtime is null.");
        return runtime.CurrentPlan.Select(static card => card.Id).ToArray();
    }

    private static void VerifyPlanShape(Kali kali, IReadOnlyList<string> cardIds, string label)
    {
        int expectedCount = Kali.ResolvePlanCardLimit(
            kali.EnemyCardPlanNumber,
            kali.IntentCapacity);
        Require(
            cardIds.Count == expectedCount,
            label + " should fill exactly " + expectedCount + " available intent slots on plan "
            + kali.EnemyCardPlanNumber + ": " + string.Join(",", cardIds));

        int firstGroupCount = cardIds.Count(Kali.FirstGroupCardIds.Contains);
        int secondGroupCount = cardIds.Count(Kali.SecondGroupCardIds.Contains);
        int fieldCount = cardIds.Count(id => id == Kali.FieldOfCorpsesCardId);
        Require(firstGroupCount + secondGroupCount + fieldCount == cardIds.Count, label + " contains unexpected card: " + string.Join(",", cardIds));
        Require(kali.IsEgoActive || fieldCount == 0,
            label + " used Field of Corpses without manifested E.G.O: " + string.Join(",", cardIds));
    }

    private static async Task VerifyEgoLifecycle(Kali kali, Creature kaliCreature, LibraryCreature libraryKali)
    {
        await LibraryCreatureCmd.SetCurrentChaoValue(libraryKali, 1m);
        Require(libraryKali.CurrentChaoValue == 1, "Could not lower Kali chao before EGO trigger.");

        decimal egoThreshold = Kali.ResolveScaledEgoHpThreshold(kaliCreature);
        await LibraryCreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            kaliCreature,
            kaliCreature.CurrentHp - Math.Max(1m, egoThreshold - 1m),
            ValueProp.Unblockable | ValueProp.Move,
            dealer: null,
            cardSource: null,
            LibraryDamageType.Slash);
        await WaitFrames(3);

        Require(kaliCreature.CurrentHp == egoThreshold, "Kali HP lock did not stop at scaled threshold: " + kaliCreature.CurrentHp);
        Require(kali.EgoManifestationPending, "Kali EGO was not queued for the next player-turn start.");
        Require(!kali.IsEgoActive, "Kali EGO manifested before the next player-turn start.");

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state disappeared.");
        var context = new ThrowingPlayerChoiceContext();
        await kali.BeforeSideTurnStart(
            context,
            CombatSide.Player,
            combatState.PlayerCreatures,
            combatState);
        Require(kali.IsEgoActive, "Kali EGO did not activate at scaled threshold.");
        IReadOnlyList<string> manifestationPlan = CurrentPlanIds(kali);
        Require(Kali.ManifestationRequiredCardIds.All(manifestationPlan.Contains),
            "Kali manifestation plan did not force Blood Mist and Field of Corpses: "
            + string.Join(",", manifestationPlan));
        Require(kaliCreature.GetPower<RedMistEgoPower>() != null, "RedMistEgoPower was not applied.");
        IReadOnlyList<NPower> egoPowerNodes = await WaitForPowerNodes(
            NCombatRoom.Instance?.GetCreatureNode(kaliCreature)
            ?? throw new InvalidOperationException("Kali creature node disappeared after EGO."),
            typeof(RedMistEgoPower),
            typeof(LibraryStrongPower),
            typeof(LibraryEndurancePower));
        Require(egoPowerNodes.Any(static node => node.Model is RedMistEgoPower), "RedMistEgoPower UI node was not found after EGO.");
        Require(egoPowerNodes.Any(static node => node.Model is LibraryStrongPower), "LibraryStrongPower UI node was not found after EGO.");
        Require(egoPowerNodes.Any(static node => node.Model is LibraryEndurancePower), "LibraryEndurancePower UI node was not found after EGO.");
        RequirePowerIcon(egoPowerNodes, typeof(RedMistEgoPower));
        RequirePowerIcon(egoPowerNodes, typeof(LibraryStrongPower));
        RequirePowerIcon(egoPowerNodes, typeof(LibraryEndurancePower));
        Require(libraryKali.CurrentChaoValue == libraryKali.MaxChaoValue, "EGO did not restore chao to max.");

        await LibraryCreatureCmd.SetCurrentChaoValue(libraryKali, 0m);
        await WaitFrames(3);
        Require(!kali.IsEgoActive, "Kali EGO did not dismiss after chao stun.");
        Require(kaliCreature.GetPower<RedMistEgoPower>() == null, "RedMistEgoPower was not removed after dismissal.");

        await kali.BeforeSideTurnStart(context, CombatSide.Enemy, combatState.Enemies, combatState);
        Require(!kali.IsEgoActive && kali.EgoReturnCountdown == Kali.EgoReturnTurns,
            "Kali EGO countdown changed on the enemy turn after dismissal.");
        await kali.BeforeSideTurnStart(context, CombatSide.Player, combatState.PlayerCreatures, combatState);
        Require(!kali.IsEgoActive && kali.EgoReturnCountdown == 1,
            "Kali EGO returned before the second player-turn start.");
        await kali.BeforeSideTurnStart(context, CombatSide.Enemy, combatState.Enemies, combatState);
        Require(!kali.IsEgoActive && kali.EgoReturnCountdown == 1,
            "Kali EGO countdown changed during the intervening enemy turn.");
        await kali.BeforeSideTurnStart(context, CombatSide.Player, combatState.PlayerCreatures, combatState);
        Require(kali.IsEgoActive, "Kali EGO did not return at the second player-turn start.");
        Require(libraryKali.CurrentChaoValue == libraryKali.MaxChaoValue, "Returned EGO did not restore chao to max.");
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

    private static void RequireSequence(IReadOnlyList<string> actual, IReadOnlyList<string> expected, string label)
    {
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                "Unexpected " + label + ": actual="
                + string.Join(",", actual)
                + " expected="
                + string.Join(",", expected));
        }
    }
}
