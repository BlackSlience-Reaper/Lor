using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedCardPools), MethodType.Getter)]
internal static class LibraryOfRuinaEgoSharedCardPoolPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        __result = LibraryOfRuinaEgoCardPoolCollection.Include(__result);
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCardPools), MethodType.Getter)]
internal static class LibraryOfRuinaEgoAllCardPoolsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        __result = LibraryOfRuinaEgoCardPoolCollection.Include(__result);
    }
}

internal static class LibraryOfRuinaEgoCardPoolCollection
{
    public static IEnumerable<CardPoolModel> Include(IEnumerable<CardPoolModel> pools)
    {
        List<CardPoolModel> poolList = pools.ToList();
        if (!poolList.Any(static pool => pool is LibraryOfRuinaEgoCardPool))
        {
            poolList.Add(ModelDb.CardPool<LibraryOfRuinaEgoCardPool>());
        }
        if (!poolList.Any(static pool =>
                pool is LibraryOfRuinaCharacterEgoCardPool))
        {
            poolList.Add(
                ModelDb.CardPool<LibraryOfRuinaCharacterEgoCardPool>());
        }
        return poolList;
    }
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary._Ready))]
internal static class LibraryOfRuinaEgoCardLibraryReadyPatch
{
    private const string EgoPoolNodeName = "EgoPool";
    private const string CharacterEgoPoolNodeName = "CharacterEgoPool";
    private const string PoolToggleScenePath = "res://scenes/screens/card_library/library_pool_toggle.tscn";
    private const string CharacterEgoFilterIconPath =
        "ui/cards/character_ego/energy.png";


    [HarmonyPostfix]
    private static void Postfix(NCardLibrary __instance)
    {
        try
        {
            AddEgoFilter(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to add EGO card library filter: " + ex);
        }
    }

    private static void AddEgoFilter(NCardLibrary cardLibrary)
    {
        if (VanillaPrivate.CardLibraryColorlessFilter.Get(cardLibrary) is not NCardPoolFilter colorlessFilter)
        {
            return;
        }

        Node? parent = colorlessFilter.GetParent();
        if (parent == null)
        {
            return;
        }

        AddPoolFilter(
            cardLibrary,
            parent,
            colorlessFilter,
            EgoPoolNodeName,
            "POOL_EGO_TIP",
            static card => card.Pool is LibraryOfRuinaEgoCardPool,
            insertOffset: 1,
            customIconPath: null);
        AddPoolFilter(
            cardLibrary,
            parent,
            colorlessFilter,
            CharacterEgoPoolNodeName,
            "POOL_CHARACTER_EGO_TIP",
            static card =>
                card.Pool is LibraryOfRuinaCharacterEgoCardPool,
            insertOffset: 2,
            customIconPath: CharacterEgoFilterIconPath);
    }

    private static void AddPoolFilter(
        NCardLibrary cardLibrary,
        Node parent,
        NCardPoolFilter colorlessFilter,
        string nodeName,
        string locKey,
        Func<CardModel, bool> predicate,
        int insertOffset,
        string? customIconPath)
    {
        NCardPoolFilter? egoFilter =
            parent.GetNodeOrNull<NCardPoolFilter>(nodeName);
        bool created = false;
        if (egoFilter == null)
        {
            PackedScene scene = ResourceLoader.Load<PackedScene>(PoolToggleScenePath);
            egoFilter = scene.Instantiate<NCardPoolFilter>();
            egoFilter.Name = nodeName;
            egoFilter.UniqueNameInOwner = true;
            egoFilter.Loc = new LocString("card_library", locKey);
            CopyFilterIcon(colorlessFilter, egoFilter);
            if (customIconPath != null)
            {
                SetFilterIcon(egoFilter, customIconPath);
            }
            EnsureFilterShaderMaterial(colorlessFilter, egoFilter);
            parent.AddChild(egoFilter);
            egoFilter.Owner = parent.Owner;
            parent.MoveChild(
                egoFilter,
                Math.Min(
                    parent.GetChildCount() - 1,
                    colorlessFilter.GetIndex() + insertOffset));
            created = true;
        }

        egoFilter.Loc = new LocString("card_library", locKey);

        if (VanillaPrivate.CardLibraryPoolFilters.Get(cardLibrary) is not Dictionary<NCardPoolFilter, Func<CardModel, bool>> poolFilters)
        {
            return;
        }

        if (!poolFilters.ContainsKey(egoFilter))
        {
            poolFilters.Add(egoFilter, predicate);
        }

        if (created)
        {
            egoFilter.Connect(
                NCardPoolFilter.SignalName.Toggled,
                Callable.From<NCardPoolFilter>(filter => VanillaPrivate.CardLibraryUpdateCardPoolFilter.Invoke(cardLibrary, new object[] { filter })));
            egoFilter.Connect(
                Control.SignalName.FocusEntered,
                Callable.From(() => VanillaPrivate.CardLibraryLastHoveredControl.Set(cardLibrary, egoFilter)));
        }
    }

    private static void SetFilterIcon(
        NCardPoolFilter target,
        string imagePath)
    {
        Texture2D texture = ResourceLoader.Load<Texture2D>(
            ImageHelper.GetImagePath(imagePath));
        TextureRect targetImage = target.GetNode<TextureRect>("Image");
        TextureRect targetShadow =
            target.GetNode<TextureRect>("Image/Shadow");
        GodotTextureSafety.TrySetTexture(targetImage, texture);
        GodotTextureSafety.TrySetTexture(targetShadow, texture);
    }

    private static void EnsureFilterShaderMaterial(
        NCardPoolFilter source,
        NCardPoolFilter target)
    {
        TextureRect targetImage = target.GetNode<TextureRect>("Image");
        if (targetImage.Material is ShaderMaterial)
        {
            return;
        }

        TextureRect sourceImage = source.GetNode<TextureRect>("Image");
        if (sourceImage.Material is not ShaderMaterial sourceMaterial)
        {
            throw new InvalidOperationException(
                "The native card-pool filter image has no HSV shader material.");
        }

        targetImage.Material = (ShaderMaterial)sourceMaterial.Duplicate(true);
    }

    private static void CopyFilterIcon(NCardPoolFilter source, NCardPoolFilter target)
    {
        TextureRect sourceImage = source.GetNode<TextureRect>("Image");
        TextureRect sourceShadow = source.GetNode<TextureRect>("Image/Shadow");
        TextureRect targetImage = target.GetNode<TextureRect>("Image");
        TextureRect targetShadow = target.GetNode<TextureRect>("Image/Shadow");

        GodotTextureSafety.TrySetTexture(targetImage, sourceImage.Texture);
        GodotTextureSafety.TrySetTexture(targetShadow, sourceShadow.Texture);
        if (sourceImage.Material != null)
        {
            targetImage.Material = (Material)sourceImage.Material.Duplicate(true);
        }
    }
}
