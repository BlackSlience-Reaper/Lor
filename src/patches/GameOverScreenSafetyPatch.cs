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
/// 结算画面计分行的兜底：原版 AddScoreLine 正常时不介入，只在它抛异常时（例如图标纹理已被释放）
/// 补建这一行并吞掉异常，免得整个结算画面中断。原版在创建节点之后只剩 <c>_scoreLines.Add</c>，不会留下重复的行。
/// </summary>
[HarmonyPatch(typeof(NGameOverScreen), "AddScoreLine")]
public static class GameOverScoreLineCompatibilityPatch
{
    [HarmonyFinalizer]
    public static Exception? Finalizer(
        Exception? __exception,
        NGameOverScreen __instance,
        string locEntryKey,
        string? locAmountKey,
        int amount,
        string scoreLabel,
        string? iconPath)
    {
        if (__exception == null)
        {
            return null;
        }

        Log.Warn($"[GameOverFix] Vanilla AddScoreLine failed; rebuilding the line. entry={locEntryKey} error={__exception.Message}");
        try
        {
            var locString = new LocString("game_over_screen", locEntryKey);
            if (locAmountKey != null)
            {
                locString.Add(locAmountKey, amount);
            }

            Texture2D? icon = GameOverScreenPatchHelper.TryLoadTexture(iconPath);
            Control scoreLine = GameOverScreenPatchHelper.CreateScoreLineControl(locString.GetFormattedText(), scoreLabel, icon);

            GridContainer? container = Traverse.Create(__instance).Field("_scoreLineContainer").GetValue<GridContainer>();
            IList? scoreLines = Traverse.Create(__instance).Field("_scoreLines").GetValue() as IList;
            if (container == null || scoreLines == null)
            {
                throw new InvalidOperationException("NGameOverScreen score line fields are unavailable.");
            }

            container.AddChild(scoreLine);
            scoreLines.Add(scoreLine);
        }
        catch (Exception e)
        {
            Log.Error(
                "[GameOverFix] Failed to add score line. "
                + $"entry={locEntryKey} amountKey={locAmountKey ?? "null"} amount={amount} "
                + $"scoreLabel={scoreLabel} icon={iconPath ?? "null"} error={e}");
        }

        return null;
    }
}
