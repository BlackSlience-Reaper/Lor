using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.acts;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetNumberOfRooms))]
internal static class ReverberationEnsembleFloorCountPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref int __result)
    {
        // 固定地图在单人与联机中使用相同层数，避免原版联机规则减少一层。
        if (__instance is ReverberationEnsembleAct)
        {
            __result = ReverberationEnsembleAct.InteriorFloorCount;
        }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.Map), MethodType.Setter)]
internal static class ReverberationEnsembleReceptionTypePatch
{
    [HarmonyPostfix]
    private static void Postfix(RunState __instance)
    {
        if (__instance.Act is not ReverberationEnsembleAct)
        {
            return;
        }

        // 旧存档中的残响地图也恢复为精英节点，房间结算和地图图例共用真实类型。
        foreach (MapPoint point in __instance.Map.GetAllMapPoints())
        {
            if (ReverberationEnsembleActMap.GetReceptionKey(point.coord) != null)
            {
                point.PointType = MapPointType.Elite;
            }
        }
    }
}

[HarmonyPatch(typeof(NNormalMapPoint), "UpdateIcon")]
internal static class ReverberationEnsembleReceptionIconPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        NNormalMapPoint __instance,
        IRunState ____runState,
        TextureRect ____icon,
        TextureRect ____outline)
    {
        if (____runState.Act is not ReverberationEnsembleAct)
        {
            return;
        }

        string? reception = ReverberationEnsembleActMap.GetReceptionKey(__instance.Point.coord);
        LibraryOfRuinaActModel? floor = ReverberationEnsembleMapIcons.ResolveFloor(reception);
        if (floor == null && reception != "RELIGION")
        {
            return;
        }

        bool isReligion = reception == "RELIGION";
        string iconPath = isReligion
            ? ReverberationEnsembleMapIcons.ReligionIconPath
            : floor!.ExpectedBoss.BossNodePath + ".png";
        string outlinePath = isReligion
            ? iconPath
            : floor!.ExpectedBoss.BossNodePath + "_outline.png";
        ____icon.Texture = GD.Load<Texture2D>(iconPath);
        ____outline.Texture = GD.Load<Texture2D>(outlinePath);
        // UpdateIcon 也在读档和状态变化时调用，保持专用图标并复用原版悬停动画。
        ShaderMaterial iconMaterial = ReverberationEnsembleMapIcons.CreateMaterial(isReligion, false);
        iconMaterial.SetShaderParameter("fill_enabled", true);
        iconMaterial.SetShaderParameter(
            "fill_mask",
            ReverberationEnsembleIconFill.GetMask(iconPath, ____icon.Texture, isReligion));
        iconMaterial.SetShaderParameter(
            "detail_color",
            floor?.BossMapOutlineColor ?? new Color("D5D8DA"));
        ____icon.Material = iconMaterial;
        ____outline.Material = isReligion
            ? ReverberationEnsembleMapIcons.CreateMaterial(true, true)
            : null;
    }
}

[HarmonyPatch]
internal static class ReverberationEnsembleReceptionColorPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(NNormalMapPoint), "TraveledColor");
        yield return AccessTools.PropertyGetter(typeof(NNormalMapPoint), "UntravelableColor");
        yield return AccessTools.PropertyGetter(typeof(NNormalMapPoint), "HoveredColor");
    }

    [HarmonyPostfix]
    private static void Postfix(
        NNormalMapPoint __instance,
        IRunState ____runState,
        MethodBase __originalMethod,
        ref Color __result)
    {
        if (____runState.Act is not ReverberationEnsembleAct)
        {
            return;
        }

        string? reception = ReverberationEnsembleActMap.GetReceptionKey(__instance.Point.coord);
        LibraryOfRuinaActModel? floor = ReverberationEnsembleMapIcons.ResolveFloor(reception);
        bool untravelable = __originalMethod.Name == "get_UntravelableColor";
        if (floor != null)
        {
            __result = untravelable ? floor.BossMapUntraveledColor : floor.BossMapTraveledColor;
        }
        else if (reception == "RELIGION")
        {
            __result = untravelable ? new Color("626A70") : new Color("30383F");
        }
    }
}

internal static class ReverberationEnsembleMapIcons
{
    internal const string ReligionIconPath = "res://images/map/reverberation_ensemble/hokma.png";

    internal static LibraryOfRuinaActModel? ResolveFloor(string? reception) =>
        reception switch
        {
            "HISTORY" => ModelDb.Act<Malkuth>(),
            "TECHNOLOGY" => ModelDb.Act<Yesod>(),
            "LITERATURE" => ModelDb.Act<Hod>(),
            "ART" => ModelDb.Act<NetZech>(),
            "LANGUAGE" => ModelDb.Act<Gebura>(),
            "NATURAL" => ModelDb.Act<Tiphereth>(),
            "PHILOSOPHY" => ModelDb.Act<Binah>(),
            "SOCIAL" => ModelDb.Act<Chesed>(),
            _ => null
        };

    internal static ShaderMaterial CreateMaterial(bool extractLines, bool drawOutline)
    {
        ShaderMaterial material = new()
        {
            Shader = GD.Load<Shader>(ReverberationEnsembleAct.BossOutlineShaderPath)
        };
        material.SetShaderParameter("extract_lines", extractLines);
        material.SetShaderParameter("draw_outline", drawOutline);
        return material;
    }
}
[HarmonyPatch(typeof(NBossMapPoint), nameof(NBossMapPoint._Ready))]
internal static class ReverberationEnsembleBossIconPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        NBossMapPoint __instance,
        IRunState ____runState,
        ref bool ____usesSpine,
        Node2D ____spineSprite,
        ref TextureRect ____placeholderImage,
        ref TextureRect ____placeholderOutline)
    {
        if (____runState.Act is not ReverberationEnsembleAct act)
        {
            return;
        }

        // 战斗暂用原版随机 Boss，但地图始终显示苍蓝残响的专用图标。
        ____usesSpine = false;
        ____spineSprite.Visible = false;
        ____placeholderImage = __instance.GetNode<TextureRect>("%PlaceholderImage");
        ____placeholderOutline = __instance.GetNode<TextureRect>("%PlaceholderOutline");
        Texture2D icon = GD.Load<Texture2D>(ReverberationEnsembleAct.BossIconPath);
        ____placeholderImage.Texture = icon;
        ____placeholderOutline.Texture = icon;
        ____placeholderImage.Visible = true;
        ____placeholderOutline.Visible = true;
        ____placeholderImage.SelfModulate = act.MapUntraveledColor;
        ____placeholderOutline.SelfModulate = act.MapBgColor;
        ____placeholderImage.Material = ReverberationEnsembleMapIcons.CreateMaterial(true, false);
        ____placeholderOutline.Material = ReverberationEnsembleMapIcons.CreateMaterial(true, true);
    }
}
