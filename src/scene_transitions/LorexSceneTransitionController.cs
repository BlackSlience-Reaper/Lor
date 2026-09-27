using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.scene_transitions;

internal static class LorexSceneTransitionController
{
    private const int RevealTimeoutMilliseconds = 6000;
    private static Task? _activeReveal;

    public static async Task PlayRevealAsync(
        TextureRect backgroundImage,
        Texture2D oldBackground,
        Texture2D newBackground)
    {
        await WaitForActiveRevealAsync();

        Task reveal = PlayRevealCoreAsync(backgroundImage, oldBackground, newBackground);
        _activeReveal = reveal;
        try
        {
            await reveal;
        }
        finally
        {
            if (ReferenceEquals(_activeReveal, reveal))
            {
                _activeReveal = null;
            }
        }
    }

    public static async Task PlayPreparedRevealAsync(
        TextureRect backgroundImage,
        LorexSceneRevealOverlay overlay,
        Texture2D newBackground)
    {
        await WaitForActiveRevealAsync();

        Task reveal = PlayPreparedRevealCoreAsync(backgroundImage, overlay, newBackground);
        _activeReveal = reveal;
        try
        {
            await reveal;
        }
        finally
        {
            if (ReferenceEquals(_activeReveal, reveal))
            {
                _activeReveal = null;
            }
        }
    }

    public static async Task PlayPhaseBackgroundRevealAsync(
        TextureRect? backgroundImage,
        string newBackgroundTexturePath,
        Func<Task> updatePhaseAsync,
        Action fallbackSetBackground)
    {
        await WaitForActiveRevealAsync();

        LorexSceneRevealOverlay? preparedReveal = null;
        Texture2D? oldBackgroundTexture = backgroundImage?.Texture;
        if (backgroundImage != null && oldBackgroundTexture != null)
        {
            preparedReveal = LorexSceneRevealOverlay.CreatePrimed(backgroundImage, oldBackgroundTexture);
        }

        try
        {
            await updatePhaseAsync();
            fallbackSetBackground();
            Texture2D? newBackgroundTexture = LoadTexture(newBackgroundTexturePath);
            if (backgroundImage != null
                && GodotObject.IsInstanceValid(backgroundImage)
                && preparedReveal != null
                && newBackgroundTexture != null)
            {
                await PlayPreparedRevealAsync(backgroundImage, preparedReveal, newBackgroundTexture);
                preparedReveal = null;
            }
        }
        finally
        {
            if (preparedReveal != null && GodotObject.IsInstanceValid(preparedReveal))
            {
                preparedReveal.QueueFreeSafely();
            }
        }
    }

    private static async Task PlayRevealCoreAsync(
        TextureRect backgroundImage,
        Texture2D oldBackground,
        Texture2D newBackground)
    {
        if (!GodotObject.IsInstanceValid(backgroundImage))
        {
            Log.Warn("[LorexSceneTransition] No background image host available; skipping reveal.");
            return;
        }

        LorexSceneRevealOverlay? overlay = LorexSceneRevealOverlay.Create(oldBackground, backgroundImage.StretchMode);
        if (overlay == null)
        {
            Log.Warn("[LorexSceneTransition] Missing reveal resources; skipping reveal.");
            return;
        }

        backgroundImage.AddChildSafely(overlay);
        overlay.PrimeFullCover();
        await PlayPreparedRevealCoreAsync(backgroundImage, overlay, newBackground);
    }

    private static async Task PlayPreparedRevealCoreAsync(
        TextureRect backgroundImage,
        LorexSceneRevealOverlay overlay,
        Texture2D newBackground)
    {
        if (!GodotObject.IsInstanceValid(backgroundImage)
            || !GodotObject.IsInstanceValid(overlay)
            || overlay.GetParent() == null)
        {
            Log.Warn("[LorexSceneTransition] Prepared reveal was not attached; skipping reveal.");
            return;
        }

        overlay.PrimeFullCover();
        backgroundImage.Texture = newBackground;
        SceneTree? tree = backgroundImage.GetTree();
        if (tree == null)
        {
            overlay.QueueFreeSafely();
            return;
        }

        await backgroundImage.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(overlay) || overlay.GetParent() == null)
        {
            return;
        }

        overlay.MoveToFront();
        try
        {
            Task revealTask = overlay.PlayAsync();
            Task completed = await Task.WhenAny(revealTask, Task.Delay(RevealTimeoutMilliseconds));
            if (completed == revealTask)
            {
                await revealTask;
            }
            else
            {
                Log.Warn("[LorexSceneTransition] Reveal timed out; continuing phase transition.");
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(overlay))
            {
                overlay.QueueFreeSafely();
            }
        }
    }

    private static Texture2D? LoadTexture(string path) =>
        ResourceLoader.Load<Texture2D>(path);

    private static async Task WaitForActiveRevealAsync()
    {
        Task? activeReveal = _activeReveal;
        if (activeReveal is not { IsCompleted: false })
        {
            return;
        }

        Task completed = await Task.WhenAny(activeReveal, Task.Delay(RevealTimeoutMilliseconds));
        if (completed == activeReveal)
        {
            await activeReveal;
            return;
        }

        Log.Warn("[LorexSceneTransition] Previous reveal timed out; continuing phase transition.");
        if (ReferenceEquals(_activeReveal, activeReveal))
        {
            _activeReveal = null;
        }
    }
}
