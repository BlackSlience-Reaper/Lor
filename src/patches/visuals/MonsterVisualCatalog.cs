using System;
using System.Linq;
using Godot;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

internal sealed record MonsterVisualCatalogEntry(
    CreatureVisualLayout? Layout,
    SpriteVisualProfile? Profile,
    string? StaticDefaultIdleTexturePath,
    Func<MonsterModel, NCreatureVisuals> Factory,
    string? ScenePath = null);

internal static class MonsterVisualCatalog
{
    private static readonly IReadOnlyDictionary<string, string> SpecialScenePaths =
        MonsterVisualRegistry.BuildSpecialScenePaths();

    private static readonly object ModelAssetPathValidationLock = new();

    private static readonly HashSet<string> ValidatedModelAssetPathIds =
        new(StringComparer.Ordinal);

    // 由各外观类上的 [MonsterVisual] 建成（见 MonsterVisualRegistry），按 ID 排序，枚举顺序不随类型扫描顺序变。
    private static readonly IReadOnlyDictionary<string, MonsterVisualCatalogEntry> Entries =
        MonsterVisualRegistry.BuildEntries()
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

    static MonsterVisualCatalog()
    {
        Validate();
    }

    internal static IEnumerable<string> RegisteredIds => Entries.Keys;

    internal static bool Contains(string monsterIdEntry) =>
        Entries.ContainsKey(monsterIdEntry)
        || SpecialScenePaths.ContainsKey(monsterIdEntry);

    internal static CreatureVisualLayout GetLayout(string monsterIdEntry)
    {
        if (Entries.TryGetValue(
                monsterIdEntry,
                out MonsterVisualCatalogEntry? entry))
        {
            if (entry.Layout is { } layout)
            {
                return layout;
            }

            throw new InvalidOperationException(
                $"Monster visual '{monsterIdEntry}' owns its layout in a scene.");
        }

        throw new InvalidOperationException(
            $"No monster visual layout is registered for '{monsterIdEntry}'.");
    }

    internal static SpriteVisualProfile GetRequiredProfile(
        string monsterIdEntry)
    {
        if (Entries.TryGetValue(
                monsterIdEntry,
                out MonsterVisualCatalogEntry? entry)
            && entry.Profile != null)
        {
            return entry.Profile;
        }

        throw new InvalidOperationException(
            $"No sprite visual profile is registered for '{monsterIdEntry}'.");
    }

    internal static string GetStaticDefaultIdleTexturePath(
        string monsterIdEntry)
    {
        if (Entries.TryGetValue(
                monsterIdEntry,
                out MonsterVisualCatalogEntry? entry)
            && !string.IsNullOrWhiteSpace(
                entry.StaticDefaultIdleTexturePath))
        {
            return entry.StaticDefaultIdleTexturePath;
        }

        throw new InvalidOperationException(
            $"No static idle texture is registered for '{monsterIdEntry}'.");
    }

    internal static NCreatureVisuals Create(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        if (SpecialScenePaths.TryGetValue(id, out string? scenePath))
        {
            MonsterVisualDebug.Trace(
                $"Create id={id} scenePath={scenePath}");
            return WrappedMonsterVisualFactory.CreateFromScene(scenePath);
        }

        if (Entries.TryGetValue(id, out MonsterVisualCatalogEntry? entry))
        {
            ValidateModelAssetPaths(monster, entry);
            return entry.Factory(monster);
        }

        throw new InvalidOperationException(
            $"No monster visual catalog entry is registered for '{id}'.");
    }

    private static void ValidateModelAssetPaths(
        MonsterModel monster,
        MonsterVisualCatalogEntry entry)
    {
        string id = monster.Id.Entry;
        lock (ModelAssetPathValidationLock)
        {
            if (ValidatedModelAssetPathIds.Contains(id))
            {
                return;
            }

            var declaredPaths = new HashSet<string>(
                monster.AssetPaths,
                StringComparer.Ordinal);
            IEnumerable<string> requiredPaths = entry.Profile?.AssetPaths
                ?? (!string.IsNullOrWhiteSpace(entry.StaticDefaultIdleTexturePath)
                    ? [entry.StaticDefaultIdleTexturePath!]
                    : !string.IsNullOrWhiteSpace(entry.ScenePath)
                        ? [entry.ScenePath!]
                        : throw new InvalidOperationException(
                            $"Monster visual '{id}' has no preloadable "
                            + "resource source."));
            string? missingPath = requiredPaths.FirstOrDefault(
                path => !declaredPaths.Contains(path));
            if (missingPath != null)
            {
                throw new InvalidOperationException(
                    $"Monster '{id}' AssetPaths does not declare sprite "
                    + $"resource '{missingPath}'.");
            }

            ValidatedModelAssetPathIds.Add(id);
        }
    }

    internal static void Validate()
    {
        var validatedResourcePaths = new HashSet<string>(
            StringComparer.Ordinal);
        string staticSpriteId = MonsterVisualRegistry.EntryOf(typeof(Finn));

        foreach ((string id, MonsterVisualCatalogEntry entry) in Entries)
        {
            if (entry.Factory == null)
            {
                throw new InvalidOperationException(
                    $"Monster visual '{id}' has no node factory.");
            }

            int sourceCount = (entry.Profile != null ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(
                    entry.StaticDefaultIdleTexturePath) ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(entry.ScenePath) ? 1 : 0);
            if (sourceCount != 1)
            {
                throw new InvalidOperationException(
                    $"Monster visual '{id}' must declare exactly one source: "
                    + "Profile, static texture, or ScenePath.");
            }

            if (!string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                if (entry.Layout != null)
                {
                    throw new InvalidOperationException(
                        $"Scene-backed monster visual '{id}' must keep layout "
                        + "inside its scene instead of the catalog.");
                }

                ValidateResourcePath(
                    id,
                    entry.ScenePath!,
                    validatedResourcePaths);
                continue;
            }

            if (entry.Layout is not { } layout)
            {
                throw new InvalidOperationException(
                    $"Code-backed monster visual '{id}' has no layout.");
            }
            ValidateLayout(id, layout);

            if (entry.Profile != null)
            {
                entry.Profile.Validate();
                if (entry.Profile.AssetPaths.Count == 0
                    || string.IsNullOrWhiteSpace(
                        entry.Profile.DefaultIdleTexturePath))
                {
                    throw new InvalidOperationException(
                        $"Sprite profile for '{id}' has no default idle resource.");
                }

                foreach (string assetPath in entry.Profile.AssetPaths)
                {
                    ValidateResourcePath(
                        id,
                        assetPath,
                        validatedResourcePaths);
                }
                continue;
            }

            ValidateResourcePath(
                id,
                entry.StaticDefaultIdleTexturePath!,
                validatedResourcePaths);

            if (id != staticSpriteId)
            {
                throw new InvalidOperationException(
                    $"FINN must remain the single static sprite fallback; "
                    + $"found '{id}'.");
            }
        }

        foreach ((string id, string scenePath) in SpecialScenePaths)
        {
            ValidateResourcePath(id, scenePath, validatedResourcePaths);
        }
    }

    private static void ValidateLayout(
        string id,
        CreatureVisualLayout layout)
    {
        if (!float.IsFinite(layout.SpriteScale.X)
            || !float.IsFinite(layout.SpriteScale.Y)
            || Mathf.IsZeroApprox(layout.SpriteScale.X)
            || Mathf.IsZeroApprox(layout.SpriteScale.Y))
        {
            throw new InvalidOperationException(
                $"Monster visual '{id}' has a non-finite or zero sprite scale.");
        }
    }

    private static void ValidateResourcePath(
        string id,
        string path,
        ISet<string> validatedResourcePaths)
    {
        if (!validatedResourcePaths.Add(path))
        {
            return;
        }

        if (!ResourceLoader.Exists(path))
        {
            throw new InvalidOperationException(
                $"Monster visual '{id}' references a missing resource: "
                + path);
        }
    }
}
