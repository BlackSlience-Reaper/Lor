using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.features.intentgraph;

/// <summary>
/// 本模组意图图改编自创意工坊 Intent Graph（Chaofan，3747528152，模组 ID <c>intentgraph2</c>），检测到它已加载时自动启用，
/// 没有设置开关；它未加载时不显示本模组意图图。启用后不单独弹出本模组的面板：Intent Graph 为图书馆怪物（<see cref="LibraryCreature"/>）建好面板后，
/// 把面板里它的图换成本模组的图，面板的位置、固定、拖动、缩放与快捷键仍由它的配置决定；其他怪物保持它原本的图。
/// 只在它建面板之后替换图节点，不修改、不禁用它的功能。
/// </summary>
internal static class IntentGraphWorkshopModBridge
{
    internal const string WorkshopModId = "intentgraph2";

    // Intent Graph 1.6.0 起由 IntentGraphHost 创建与管理面板。
    private const string HostTypeName = "IntentGraph2.Utils.IntentGraphHost";
    private const string CreateMethodName = "Create";
    private const string PanelsFieldName = "availableIntentGraphs";
    private const string PanelPropertyName = "IntentGraphPanel";

    private static bool _patchAttempted;
    private static FieldInfo? _panelsField;

    /// <summary>Intent Graph 是否已加载；未加载时本模组意图图不可启用。</summary>
    internal static bool IsWorkshopModLoaded =>
        ModManager.GetLoadedMods().Any(static mod => string.Equals(mod.manifest?.id, WorkshopModId, StringComparison.Ordinal));

    /// <summary>挂上 Intent Graph 创建面板后的替换；它的程序集在它自己的初始化里才加载，所以延后到需要时再找。</summary>
    internal static void EnsurePatched()
    {
        if (_patchAttempted || !IsWorkshopModLoaded)
        {
            return;
        }

        Type? hostType = FindHostType();
        if (hostType == null)
        {
            return;
        }

        _patchAttempted = true;
        MethodInfo? createMethod = hostType.GetMethod(
            CreateMethodName,
            BindingFlags.Static | BindingFlags.Public,
            [typeof(NCreature)]);
        _panelsField = hostType.GetField(PanelsFieldName, BindingFlags.Static | BindingFlags.NonPublic);
        if (createMethod == null || _panelsField == null)
        {
            LorLog.Warn("[LibraryOfRuina.IntentGraph] Intent Graph is loaded, but IntentGraphHost has an unexpected shape; "
                        + "its graph is left unchanged.");
            return;
        }

        try
        {
            new Harmony(LibraryPatcher.HarmonyId).Patch(
                createMethod,
                postfix: new HarmonyMethod(typeof(IntentGraphWorkshopModBridge), nameof(AfterWorkshopPanelCreated)));
            LorLog.Info("[LibraryOfRuina.IntentGraph] Hooked Intent Graph panel creation for Library creatures.");
        }
        catch (Exception exception)
        {
            LorLog.Warn("[LibraryOfRuina.IntentGraph] Failed to hook Intent Graph panel creation: " + exception);
        }
    }

    /// <summary>悬停时补挂钩子，并补替换一次（首次悬停时它的面板可能在钩子挂上之前就建好了）。</summary>
    internal static void OnCreatureHovered(NCreature nCreature)
    {
        EnsurePatched();
        if (_panelsField != null)
        {
            AfterWorkshopPanelCreated(nCreature);
        }
    }

    private static Type? FindHostType()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? hostType = assembly.GetType(HostTypeName, throwOnError: false);
            if (hostType != null)
            {
                return hostType;
            }
        }

        return null;
    }

    private static void AfterWorkshopPanelCreated(NCreature nCreature)
    {
        try
        {
            if (nCreature.Entity is not LibraryCreature)
            {
                return;
            }

            Control? panel = FindWorkshopPanel(nCreature);
            if (panel != null)
            {
                NLibraryIntentGraphSwap.Attach(panel, nCreature);
            }
        }
        catch (Exception exception)
        {
            if (LorLog.FirstTime("IntentGraph.WorkshopSwap"))
            {
                LorLog.Warn("[LibraryOfRuina.IntentGraph] Failed to swap the graph inside the Intent Graph panel: " + exception);
            }
        }
    }

    private static Control? FindWorkshopPanel(NCreature nCreature)
    {
        if (_panelsField?.GetValue(null) is not IDictionary panels || !panels.Contains(nCreature))
        {
            return null;
        }

        object? item = panels[nCreature];
        return item?.GetType()
            .GetProperty(PanelPropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(item) as Control;
    }
}
