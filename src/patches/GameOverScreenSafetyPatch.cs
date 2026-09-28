using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.addons.mega_text;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.patches;

internal static class GameOverScreenPatchHelper
{
    private static readonly string ScoreLineScenePath = SceneHelper.GetScenePath("screens/game_over_screen/score_line");

    public static Texture2D? TryLoadTexture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
            if (GodotTextureSafety.IsValid(texture))
            {
                return texture;
            }
        }
        catch (Exception e)
        {
            Log.Error($"[GameOverFix] ResourceLoader texture load failed ({path}): {e}");
        }

        try
        {
            Texture2D? texture = PreloadManager.Cache.GetTexture2D(path);
            return GodotTextureSafety.IsValid(texture) ? texture : null;
        }
        catch (Exception e)
        {
            Log.Error($"[GameOverFix] Preload cache texture load failed ({path}): {e}");
            return null;
        }
    }

    public static Control CreateScoreLineControl(string label, string score, Texture2D? icon)
    {
        Control? reflected = TryCreateControlFromFactory(
            "MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen.NScoreLine",
            "Create",
            parameters => parameters.Length >= 2
                && parameters[0].ParameterType == typeof(string)
                && parameters[1].ParameterType == typeof(string),
            parameters => parameters.Length >= 3
                ? new object?[] { label, score, icon }
                : new object?[] { label, score });

        if (reflected != null)
        {
            return reflected;
        }

        try
        {
            PackedScene? scene = ResourceLoader.Load<PackedScene>(ScoreLineScenePath);
            if (scene != null)
            {
                Control line = scene.Instantiate<Control>();
                TrySetText(line, "%Label", label);
                TrySetText(line, "%Score", score);
                TrySetIcon(line, icon);
                return line;
            }
        }
        catch (Exception e)
        {
            Log.Error($"[GameOverFix] Direct score line scene load failed ({ScoreLineScenePath}): {e}");
        }

        throw new InvalidOperationException("Unable to create score line control.");
    }

    private static Control? TryCreateControlFromFactory(
        string typeName,
        string methodName,
        Func<ParameterInfo[], bool> parameterFilter,
        Func<ParameterInfo[], object?[]> argBuilder)
    {
        Type? type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            return null;
        }

        MethodInfo? factory = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    return false;
                }
                ParameterInfo[] parameters = method.GetParameters();
                return parameterFilter(parameters);
            });

        if (factory == null)
        {
            return null;
        }

        ParameterInfo[] factoryParams = factory.GetParameters();
        try
        {
            object? result = factory.Invoke(null, argBuilder(factoryParams));
            return result as Control;
        }
        catch (Exception e)
        {
            Exception logged = e is TargetInvocationException { InnerException: { } inner } ? inner : e;
            Log.Warn(
                "[GameOverFix] Factory "
                + typeName + "." + methodName
                + " failed; falling back to direct scene creation. error="
                + logged.GetType().Name + ": " + logged.Message);
            return null;
        }
    }

    private static void TrySetText(Control root, string nodePath, string value)
    {
        if (!root.HasNode(nodePath))
        {
            return;
        }

        if (root.GetNode(nodePath) is MegaLabel labelNode)
        {
            labelNode.SetTextAutoSize(value);
        }
    }

    private static void TrySetIcon(Control root, Texture2D? icon)
    {
        if (!GodotTextureSafety.IsValid(icon))
        {
            return;
        }

        if (root.HasNode("%Icon") && root.GetNode("%Icon") is TextureRect iconNode)
        {
            GodotTextureSafety.TrySetTexture(iconNode, icon);
            return;
        }

        if (root.HasNode("Icon") && root.GetNode("Icon") is TextureRect iconNodeByName)
        {
            GodotTextureSafety.TrySetTexture(iconNodeByName, icon);
        }
    }
}

/// <summary>
/// 结算画面计分行的兜底：原版 AddScoreLine 抛异常（例如图标纹理已被释放）、这一行没建成时，补建这一行并吞掉异常，
/// 免得整个结算画面中断。Finalizer 收到的异常也可能来自其他模组的前缀或后缀，所以前缀先记下调用前的状态：
/// <list type="bullet">
/// <item>原版最后一步 <c>_scoreLines.Add</c> 已经执行（行已建成，异常来自之后的后缀）：原样抛出，不补建。</item>
/// <item>没记下状态（前缀没执行）或补建本身失败：原样抛出，与没有本补丁时一致。</item>
/// </list>
/// 原版 <c>AddChildSafely</c> 在节点未就绪时会延迟添加，所以是否建成只看 <c>_scoreLines</c>；已同步挂上容器、
/// 却没进列表的半成品在补建前移除。
/// </summary>
[HarmonyPatch(typeof(NGameOverScreen), "AddScoreLine")]
public static class GameOverScoreLineCompatibilityPatch
{
    public sealed class Snapshot
    {
        public required GridContainer Container { get; init; }
        public required IList ScoreLines { get; init; }
        public required int LineCount { get; init; }
        public required int ChildCount { get; init; }
    }

    [HarmonyPrefix]
    public static void Prefix(NGameOverScreen __instance, out Snapshot? __state)
    {
        __state = null;
        try
        {
            GridContainer? container = VanillaPrivate.GameOverScreenScoreLineContainer.Get(__instance);
            if (container != null && VanillaPrivate.GameOverScreenScoreLines.Get(__instance) is { } scoreLines)
            {
                __state = new Snapshot
                {
                    Container = container,
                    ScoreLines = scoreLines,
                    LineCount = scoreLines.Count,
                    ChildCount = container.GetChildCount()
                };
            }
        }
        catch (Exception e)
        {
            Log.Warn("[GameOverFix] Could not read NGameOverScreen score line fields: " + e.Message);
        }
    }

    [HarmonyFinalizer]
    public static Exception? Finalizer(
        Exception? __exception,
        Snapshot? __state,
        string locEntryKey,
        string? locAmountKey,
        int amount,
        string scoreLabel,
        string? iconPath)
    {
        if (__exception == null || __state == null || __state.ScoreLines.Count > __state.LineCount)
        {
            return __exception;
        }

        Log.Warn($"[GameOverFix] AddScoreLine failed before the line was recorded; rebuilding it. entry={locEntryKey} error={__exception.Message}");
        try
        {
            RemoveUnrecordedLines(__state);

            var locString = new LocString("game_over_screen", locEntryKey);
            if (locAmountKey != null)
            {
                locString.Add(locAmountKey, amount);
            }

            Texture2D? icon = GameOverScreenPatchHelper.TryLoadTexture(iconPath);
            Control scoreLine = GameOverScreenPatchHelper.CreateScoreLineControl(locString.GetFormattedText(), scoreLabel, icon);
            __state.Container.AddChild(scoreLine);
            __state.ScoreLines.Add(scoreLine);
            return null;
        }
        catch (Exception e)
        {
            Log.Error(
                "[GameOverFix] Failed to rebuild score line; rethrowing the original exception. "
                + $"entry={locEntryKey} amountKey={locAmountKey ?? "null"} amount={amount} "
                + $"scoreLabel={scoreLabel} icon={iconPath ?? "null"} error={e}");
            return __exception;
        }
    }

    private static void RemoveUnrecordedLines(Snapshot state)
    {
        for (int index = state.Container.GetChildCount() - 1; index >= state.ChildCount; index--)
        {
            Node child = state.Container.GetChild(index);
            if (!state.ScoreLines.Contains(child))
            {
                state.Container.RemoveChild(child);
                child.QueueFree();
            }
        }
    }
}
