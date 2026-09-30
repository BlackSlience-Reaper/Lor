using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.JudgementBird;

internal static class JudgementBirdSinHealthBarForecastUi
{
    internal const string OverlayNodeName =
        "LibraryOfRuinaJudgementBirdSinForecast";

    internal static void Apply(
        Creature creature,
        Control hpForegroundContainer,
        Control doomForeground,
        float expectedMaxForegroundWidth)
    {
        NinePatchRect? overlay = EnsureOverlay(doomForeground);
        if (overlay == null)
        {
            return;
        }

        JudgementBirdSinPower? sin =
            creature.GetPower<JudgementBirdSinPower>();
        if (creature.CurrentHp <= 0
            || creature.MaxHp <= 0
            || creature.IsDead
            || sin == null
            || sin.Amount <= 0)
        {
            overlay.Visible = false;
            return;
        }

        float maxWidth = expectedMaxForegroundWidth > 0f
            ? expectedMaxForegroundWidth
            : hpForegroundContainer.Size.X;
        if (maxWidth <= 0f)
        {
            overlay.Visible = false;
            return;
        }

        int forecastAmount = sin.GetForecastAmount();
        float forecastWidth = Math.Max(
            (float)forecastAmount / creature.MaxHp * maxWidth,
            12f);
        overlay.Visible = true;
        overlay.Material = JudgementBirdSinPower.ForecastOverlayMaterial;
        overlay.Modulate = Colors.White;
        overlay.SelfModulate = Colors.White;
        overlay.OffsetLeft = 0f;
        overlay.OffsetRight = Math.Min(
            0f,
            forecastWidth - maxWidth + overlay.PatchMarginRight);

        MoveImmediatelyBefore(overlay, doomForeground);
    }

    private static NinePatchRect? EnsureOverlay(Control doomForeground)
    {
        if (doomForeground.GetParent() is not Control parent
            || doomForeground is not NinePatchRect template)
        {
            return null;
        }

        if (parent.GetNodeOrNull<NinePatchRect>(OverlayNodeName)
                is { } existing)
        {
            return existing;
        }

        var overlay = (NinePatchRect)template.Duplicate();
        overlay.Name = OverlayNodeName;
        overlay.UniqueNameInOwner = false;
        overlay.Visible = false;
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.Material = JudgementBirdSinPower.ForecastOverlayMaterial;
        overlay.Modulate = Colors.White;
        overlay.SelfModulate = Colors.White;
        overlay.OffsetLeft = 0f;
        overlay.OffsetRight = 0f;
        parent.AddChild(overlay);
        MoveImmediatelyBefore(overlay, doomForeground);
        return overlay;
    }

    private static void MoveImmediatelyBefore(
        Control overlay,
        Control doomForeground)
    {
        if (overlay.GetParent() is not Control parent
            || doomForeground.GetParent() != parent)
        {
            return;
        }

        int targetIndex = doomForeground.GetIndex();
        if (overlay.GetIndex() < targetIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Max(0, targetIndex);
        if (overlay.GetIndex() != targetIndex)
        {
            parent.MoveChild(overlay, targetIndex);
        }
    }
}

[HarmonyPatch(typeof(NHealthBar), "RefreshForeground")]
[HarmonyAfter("BaseLib", "com.ritsukage.sts2-RitsuLib.framework-core")]
internal static class JudgementBirdSinHealthBarForegroundPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Creature ____creature,
        Control ____hpForegroundContainer,
        Control ____doomForeground,
        float ____expectedMaxFgWidth)
    {
        JudgementBirdSinHealthBarForecastUi.Apply(
            ____creature,
            ____hpForegroundContainer,
            ____doomForeground,
            ____expectedMaxFgWidth);
    }
}

[HarmonyPatch(typeof(NHealthBar),
    "SetHpBarContainerSizeWithOffsetsImmediately")]
[HarmonyAfter("BaseLib", "com.ritsukage.sts2-RitsuLib.framework-core")]
internal static class JudgementBirdSinHealthBarResizePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Creature ____creature,
        Control ____hpForegroundContainer,
        Control ____doomForeground,
        float ____expectedMaxFgWidth)
    {
        JudgementBirdSinHealthBarForecastUi.Apply(
            ____creature,
            ____hpForegroundContainer,
            ____doomForeground,
            ____expectedMaxFgWidth);
    }
}
