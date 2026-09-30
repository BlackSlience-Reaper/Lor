using System;
using Godot;
using HarmonyLib;
using STS2RitsuLib.Utils;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

internal static class FairyMassDevourHealthBarForecastUi
{
    // 与原版血条的最小可见宽度一致，确保 1 点生命阈值仍清晰可见。
    private const float MinimumForecastWidth = 12f;

    private static readonly Lazy<ShaderMaterial> ForecastMaterial = new(CreateForecastMaterial);

    private static ShaderMaterial CreateForecastMaterial()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.10f, 0.75f, 0.67f));
        gradient.SetColor(1, Colors.White);
        return MaterialUtils.CreateDoomBarShaderMaterial(
            new GradientTexture1D { Gradient = gradient });
    }

    internal const string OverlayNodeName =
        "LibraryOfRuinaFairyMassDevourForecast";

    internal static void Apply(
        Creature creature,
        Control hpForegroundContainer,
        Control doomForeground,
        float expectedMaxForegroundWidth)
    {
        if (creature.Monster is not FairyMass)
        {
            return;
        }

        NinePatchRect? overlay = EnsureOverlay(doomForeground);
        if (overlay == null)
        {
            return;
        }

        int forecastAmount = FairyFestivalCombatHelper.GetDevourForecastThreshold(creature);
        if (creature.CurrentHp <= 0
            || creature.MaxHp <= 0
            || creature.IsDead
            || forecastAmount <= 0)
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

        float forecastWidth = Math.Max(
            (float)forecastAmount / creature.MaxHp * maxWidth,
            MinimumForecastWidth);
        overlay.Visible = true;
        overlay.Material = ForecastMaterial.Value;
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
        overlay.Material = ForecastMaterial.Value;
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
internal static class FairyMassDevourHealthBarForegroundPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Creature ____creature,
        Control ____hpForegroundContainer,
        Control ____doomForeground,
        float ____expectedMaxFgWidth)
    {
        FairyMassDevourHealthBarForecastUi.Apply(
            ____creature,
            ____hpForegroundContainer,
            ____doomForeground,
            ____expectedMaxFgWidth);
    }
}

[HarmonyPatch(typeof(NHealthBar),
    "SetHpBarContainerSizeWithOffsetsImmediately")]
[HarmonyAfter("BaseLib", "com.ritsukage.sts2-RitsuLib.framework-core")]
internal static class FairyMassDevourHealthBarResizePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Creature ____creature,
        Control ____hpForegroundContainer,
        Control ____doomForeground,
        float ____expectedMaxFgWidth)
    {
        FairyMassDevourHealthBarForecastUi.Apply(
            ____creature,
            ____hpForegroundContainer,
            ____doomForeground,
            ____expectedMaxFgWidth);
    }
}
