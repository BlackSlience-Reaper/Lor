using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.specialguests.Xiao;
using MegaCrit.Sts2.Core.Combat;
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
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class XiaoIntentAnchorVerificationPatch
{
    private const string VerifyArg = "lor-verify-xiao-intent-anchor";
    private const string LogPrefix = "[LibraryOfRuina.XiaoIntent.Verify] ";
    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() => TaskHelper.RunSafely(RunAsync())).CallDeferred();
    }

    private static bool HasVerifyArg()
    {
        return CommandLineHelper.HasArg(VerifyArg)
               || Environment.GetCommandLineArgs().Any(arg =>
                   string.Equals(
                       arg.TrimStart('-'),
                       VerifyArg,
                       StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunAsync()
    {
        try
        {
            await RunCoreAsync();
            Log.Info(LogPrefix + "XIAO_INTENT_ANCHOR_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "XIAO_INTENT_ANCHOR_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "XIAOINTENTVERIFY",
            GameMode.Standard,
            ascensionLevel: 0);
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<XiaoSpecialGuestStageOneEncounter>().ToMutable(),
            showTransition: false);

        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                         && NCombatRoom.Instance != null,
            "Xiao stage-one combat start");
        CombatState state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        XiaoStageOne xiao = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<XiaoStageOne>()
            .Single();
        NCreature xiaoNode = await WaitFor(
            () => NCombatRoom.Instance?.GetCreatureNode(xiao.Creature),
            "Xiao creature node");

        await xiaoNode.RefreshIntents();
        await WaitFrames(3);

        NIntent[] intents = xiaoNode.IntentContainer.GetChildren()
            .OfType<NIntent>()
            .Where(static intent => intent.Visible)
            .ToArray();
        if (intents.Length == 0)
        {
            throw new InvalidOperationException("Xiao has no visible intent nodes.");
        }

        Rect2 visibleBounds = intents[0].GetGlobalRect();
        foreach (NIntent intent in intents.Skip(1))
        {
            visibleBounds = visibleBounds.Merge(intent.GetGlobalRect());
        }

        Vector2 markerCenter = xiaoNode.Visuals.IntentPosition.GlobalPosition;
        Vector2 visibleCenter = visibleBounds.GetCenter();
        Vector2 delta = markerCenter - visibleCenter;
        Sprite2D idleSprite = xiaoNode.Visuals.GetNode<Sprite2D>("%Visuals");
        float visibleHeadCenterX = GetVisibleHeadCenterX(idleSprite);
        float headDeltaX = visibleHeadCenterX - visibleCenter.X;
        Log.Info(LogPrefix
                 + $"marker={markerCenter} visible={visibleCenter} "
                 + $"delta={delta} visibleHeadCenterX={visibleHeadCenterX} "
                 + $"headDeltaX={headDeltaX} count={intents.Length} "
                 + $"containerPos={xiaoNode.IntentContainer.GlobalPosition} "
                 + $"containerSize={xiaoNode.IntentContainer.Size}");
        if (Mathf.Abs(headDeltaX) > 2f)
        {
            throw new InvalidOperationException(
                "Visible Xiao intent row is not centered over her head: "
                + headDeltaX);
        }
    }

    private static float GetVisibleHeadCenterX(Sprite2D sprite)
    {
        Texture2D texture = sprite.Texture
            ?? throw new InvalidOperationException("Xiao idle texture is missing.");
        Image image = texture.GetImage()
            ?? throw new InvalidOperationException("Xiao idle image is unavailable.");
        int width = image.GetWidth();
        int height = image.GetHeight();
        int yStart = Mathf.RoundToInt(height * 0.15f);
        int yEnd = Mathf.RoundToInt(height * 0.45f);
        int minX = width;
        int maxX = -1;
        for (int y = yStart; y <= yEnd; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (image.GetPixel(x, y).A <= 0.5f)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
            }
        }

        if (maxX < minX)
        {
            throw new InvalidOperationException("Xiao visible head pixels were not found.");
        }

        float textureCenterX = (minX + maxX) * 0.5f;
        float localX = sprite.Centered
            ? textureCenterX - width * 0.5f + sprite.Offset.X
            : textureCenterX + sprite.Offset.X;
        return sprite.ToGlobal(new Vector2(localX, 0f)).X;
    }

    private static async Task<T> WaitFor<T>(
        Func<T?> resolve,
        string description,
        int maxFrames = 900)
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

        throw new TimeoutException("Timed out waiting for " + description + ".");
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
}
