using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Text;
using GameLabelHelper = MegaCrit.Sts2.addons.mega_text.MegaLabelHelper;
using ModLabelHelper = LibraryOfRuina.addons.mega_text.MegaLabelHelper;

namespace LibraryOfRuina.localization;

internal static class PowerInlineIconSizing
{
    // 尚未交给正文控件排版时的初始边长；正式显示和测量均跟随正文当前字号。
    private const int IconMaxSize = 20;

    private static readonly Dictionary<string, Vector2> IconRatios = new(StringComparer.Ordinal);

    private static readonly Regex InlineMarkup = new(
        @"\[img width=\d+ height=\d+\](?<path>[^\[\]]+)\[/img\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly ConditionalWeakTable<List<BbcodeObject>, Dictionary<string, Vector2>> MeasuredSizes = new();

    internal static string CreateMarkup(ResolvedPowerIcon icon)
    {
        Vector2 original = icon.Texture.GetSize();
        float scale = IconMaxSize / Math.Max(1f, Math.Max(original.X, original.Y));
        int width = Math.Max(1, (int)Math.Round(original.X * scale));
        int height = Math.Max(1, (int)Math.Round(original.Y * scale));
        string markup = $"[img width={width} height={height}]{icon.Path}[/img]";
        IconRatios[icon.Path] = original / Math.Max(1f, Math.Max(original.X, original.Y));
        return markup;
    }

    [HarmonyPatch]
    private static class PreserveInlineDimensionsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameLabelHelper), nameof(GameLabelHelper.ParseBbcode));
            yield return AccessTools.Method(typeof(ModLabelHelper), nameof(ModLabelHelper.ParseBbcode));
        }

        private static void Postfix(string bbcode, List<BbcodeObject> __result)
        {
            // 原解析器复用结果列表；每次解析清除上一次文本的测量尺寸。
            MeasuredSizes.Remove(__result);
            Dictionary<string, Vector2>? sizes = null;
            foreach (Match match in InlineMarkup.Matches(bbcode))
            {
                string path = match.Groups["path"].Value;
                if (!IconRatios.TryGetValue(path, out Vector2 ratio))
                {
                    continue;
                }

                sizes ??= new Dictionary<string, Vector2>(StringComparer.Ordinal);
                sizes[path] = ratio;
            }

            if (sizes != null)
            {
                MeasuredSizes.Add(__result, sizes);
            }
        }
    }

    [HarmonyPatch]
    private static class MeasureInlineDimensionsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] parameters =
            [
                typeof(TextParagraph), typeof(List<BbcodeObject>), typeof(Font),
                typeof(int), typeof(float), typeof(float)
            ];
            yield return AccessTools.Method(typeof(GameLabelHelper), nameof(GameLabelHelper.EstimateTextSize), parameters);
            yield return AccessTools.Method(typeof(ModLabelHelper), nameof(ModLabelHelper.EstimateTextSize), parameters);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getSize = AccessTools.Method(typeof(Texture2D), nameof(Texture2D.GetSize));
            MethodInfo measuredSize = AccessTools.Method(typeof(PowerInlineIconSizing), nameof(GetMeasuredSize));
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(getSize))
                {
                    // 保留原来的换行、行距和二分字号算法，仅修正新增图标的占位尺寸。
                    var loadObjects = new CodeInstruction(OpCodes.Ldarg_1);
                    loadObjects.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    yield return loadObjects;
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = measuredSize;
                    replaced = true;
                }

                yield return instruction;
            }

            if (!replaced)
            {
                throw new InvalidOperationException("Inline Power icon sizing: Texture2D.GetSize call was not found.");
            }
        }
    }

    private static Vector2 GetMeasuredSize(Texture2D texture, List<BbcodeObject> objects, int fontSize)
    {
        if (MeasuredSizes.TryGetValue(objects, out Dictionary<string, Vector2>? sizes)
            && sizes.TryGetValue(texture.ResourcePath, out Vector2 size))
        {
            return ScaleToFont(size, fontSize);
        }

        return texture.GetSize();
    }

    private static Vector2 ScaleToFont(Vector2 ratio, int fontSize) =>
        new(Math.Max(1, (int)Math.Round(ratio.X * fontSize)),
            Math.Max(1, (int)Math.Round(ratio.Y * fontSize)));

    [HarmonyPatch]
    private static class MatchRenderedFontSizePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel), "SetFontSize");
            yield return AccessTools.Method(typeof(LibraryOfRuina.addons.mega_text.MegaRichTextLabel), "SetFontSize");
        }

        private static void Postfix(RichTextLabel __instance, int size)
        {
            string original = __instance.Text;
            string resized = InlineMarkup.Replace(original, match =>
            {
                string path = match.Groups["path"].Value;
                if (!IconRatios.TryGetValue(path, out Vector2 ratio))
                {
                    return match.Value;
                }

                Vector2 dimensions = ScaleToFont(ratio, size);
                return $"[img width={(int)dimensions.X} height={(int)dimensions.Y}]{path}[/img]";
            });
            if (resized != original)
            {
                // 直接更新 Godot 正文，避免再次进入 SetTextAutoSize 触发缩放循环。
                __instance.Text = resized;
            }
        }
    }

}
