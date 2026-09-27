using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.compat;

public static class SavedPropertiesTypeCacheCompat
{
    private static readonly object LockObj = new();

    private static Type[]? _cachedAutoDiscoveredTypes;

    public static Type[] ModSavedPropertyTypes =>
        AutoDiscoverSavedPropertyTypes();

    /// <summary>
    /// Scans the mod assembly for every type whose members carry at least one
    /// <see cref="SavedPropertyAttribute"/>, sorted by full name for deterministic
    /// ordering. Only concrete <see cref="AbstractModel"/> types are included,
    /// matching the model instances that can enter saves and network snapshots.
    /// </summary>
    public static Type[] AutoDiscoverSavedPropertyTypes()
    {
        if (_cachedAutoDiscoveredTypes != null)
        {
            return _cachedAutoDiscoveredTypes;
        }

        var assembly = Assembly.GetExecutingAssembly();
        _cachedAutoDiscoveredTypes = assembly
            .GetTypes()
            .Where(static type =>
                !type.IsAbstract
                && typeof(AbstractModel).IsAssignableFrom(type)
                && type.Namespace != null
                && type.Namespace.StartsWith(
                    "LibraryOfRuina",
                    StringComparison.Ordinal))
            .Where(HasAnySavedProperty)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        if (_cachedAutoDiscoveredTypes.Length > 0)
        {
            Log.Info("[LibraryOfRuina.SavedProperties] Auto-discovered "
                + _cachedAutoDiscoveredTypes.Length
                + " types with SavedProperty attributes.");
        }

        return _cachedAutoDiscoveredTypes;
    }

    public static Type[] GetAllModSavedPropertyTypes() =>
        AutoDiscoverSavedPropertyTypes();

    public static void ClearAutoDiscoveryCache()
    {
        _cachedAutoDiscoveredTypes = null;
    }

    private static bool HasAnySavedProperty(Type type)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return type.GetProperties(flags).Any(p => p.IsDefined(typeof(SavedPropertyAttribute), inherit: false))
                || type.GetFields(flags).Any(f => f.IsDefined(typeof(SavedPropertyAttribute), inherit: false));
        }
        catch
        {
            return false;
        }
    }

#if !STS2_BETA
    private static readonly FieldInfo? NetIdToPropertyNameMapField =
        typeof(SavedPropertiesTypeCache).GetField("_netIdToPropertyNameMap", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo? NetIdBitSizeSetter =
        typeof(SavedPropertiesTypeCache)
            .GetProperty(nameof(SavedPropertiesTypeCache.NetIdBitSize), BindingFlags.Static | BindingFlags.Public)
            ?.GetSetMethod(nonPublic: true);

    private static bool _refreshFailureLogged;
#endif

    public static void InjectModSavedPropertyTypes()
    {
#if STS2_BETA
        Type[] allTypes = GetAllModSavedPropertyTypes();
        Log.Info("[LibraryOfRuina.SavedProperties] Current API caches SavedProperty metadata from "
            + "ModelDb.All during SavedPropertiesTypeCache.Init; explicit type registration is unnecessary ("
            + allTypes.Length
            + " mod types discovered).");
#else
        Type[] allTypes = GetAllModSavedPropertyTypes();
        foreach (Type type in allTypes)
        {
            SavedPropertiesTypeCache.InjectTypeIntoCache(type);
        }

        RefreshNetIdBitSize(
            "automatic LibraryOfRuina saved-property injection ("
                + allTypes.Length
                + " types discovered)",
            forceLog: true);
#endif
    }

    public static void RefreshNetIdBitSize(string reason, bool forceLog = false)
    {
#if STS2_BETA
#else
        lock (LockObj)
        {
            try
            {
                if (NetIdToPropertyNameMapField?.GetValue(null) is not ICollection<string> propertyNames
                    || NetIdBitSizeSetter == null)
                {
                    LogRefreshFailure("SavedPropertiesTypeCache reflection targets were not found.");
                    return;
                }

                int oldBitSize = SavedPropertiesTypeCache.NetIdBitSize;
                int nextBitSize = CalculateNetIdBitSize(propertyNames.Count);
                if (oldBitSize != nextBitSize)
                {
                    NetIdBitSizeSetter.Invoke(null, [nextBitSize]);
                }

                if (forceLog || oldBitSize != nextBitSize)
                {
                    Log.Info("[LibraryOfRuina.SavedProperties] Refreshed NetIdBitSize after "
                        + reason
                        + ": "
                        + oldBitSize
                        + " -> "
                        + nextBitSize
                        + " (propertyNames="
                        + propertyNames.Count
                        + ").");
                }
            }
            catch (Exception exception)
            {
                LogRefreshFailure(exception.ToString());
            }
        }
#endif
    }

    public static string BuildSchemaFingerprintMaterial()
    {
        StringBuilder builder = new();
        Type[] allTypes = GetAllModSavedPropertyTypes();
        foreach (Type type in allTypes.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            AppendSavedPropertySchema(builder, type);
        }

        foreach (Type enumType in GetSavedPropertyEnumTypes(allTypes)
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            AppendEnumSchema(builder, enumType);
        }

        return builder.ToString();
    }

    private static IEnumerable<Type> GetSavedPropertyEnumTypes(
        IEnumerable<Type> types)
    {
        return types
            .SelectMany(type => type.GetProperties(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic))
            .Where(property =>
                property.GetCustomAttribute<SavedPropertyAttribute>() != null)
            .Select(property =>
            {
                Type propertyType = Nullable.GetUnderlyingType(
                        property.PropertyType)
                    ?? property.PropertyType;
                return propertyType.IsArray
                    ? propertyType.GetElementType()
                    : propertyType;
            })
            .OfType<Type>()
            .Where(static type => type.IsEnum)
            .Distinct();
    }

    private static void AppendSavedPropertySchema(StringBuilder builder, Type type)
    {
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(property => new
            {
                Property = property,
                Attribute = property.GetCustomAttribute<SavedPropertyAttribute>()
            })
            .Where(entry => entry.Attribute != null)
            .OrderBy(entry => entry.Attribute!.order)
            .ThenBy(entry => entry.Property.Name, StringComparer.Ordinal)
            .Select(entry => entry.Property)
            .ToArray();

        foreach (PropertyInfo property in properties)
        {
            SavedPropertyAttribute attribute = property.GetCustomAttribute<SavedPropertyAttribute>()!;
            builder
                .Append("saved|")
                .Append(type.FullName)
                .Append('|')
                .Append(property.Name)
                .Append('|')
                .Append(property.PropertyType.FullName)
                .Append('|')
                .Append(attribute.defaultBehaviour)
                .Append('|')
                .Append(attribute.order)
                .AppendLine();
        }
    }

    private static void AppendEnumSchema(
        StringBuilder builder,
        Type enumType)
    {
        foreach (object value in Enum.GetValues(enumType)
                     .Cast<object>()
                     .OrderBy(Convert.ToInt64))
        {
            builder
                .Append("enum|")
                .Append(enumType.FullName)
                .Append('|')
                .Append(value)
                .Append('=')
                .Append(Convert.ToInt64(value))
                .AppendLine();
        }
    }

    private static int CalculateNetIdBitSize(int propertyNameCount)
    {
        if (propertyNameCount <= 1)
        {
            return 0;
        }

        int maxNetId = propertyNameCount - 1;
        int bits = 0;
        while (maxNetId > 0)
        {
            bits++;
            maxNetId >>= 1;
        }

        return bits;
    }

    private static void LogRefreshFailure(string reason)
    {
#if STS2_BETA
#else
        if (_refreshFailureLogged)
        {
            return;
        }

        _refreshFailureLogged = true;
        Log.Error("[LibraryOfRuina.SavedProperties] Failed to refresh SavedPropertiesTypeCache.NetIdBitSize: " + reason);
#endif
    }
}

public static class LibraryOfRuinaCompatibilityFingerprint
{
    public static string GetGameplayRelevantSuffix() =>
        "+设置.抗性" + GetResistanceMode()
        + ".第二进阶" + (LibrarySecondAscensionState.IsSelectionEnabled ? "开" : "关");

    private static int GetResistanceMode() => LibraryOfRuinaSettings.ResistanceMode switch
    {
        1d => 1,
        2d => 2,
        _ => 3
    };
}

#if !STS2_BETA
[HarmonyPatch(typeof(SavedPropertiesTypeCache), nameof(SavedPropertiesTypeCache.InjectTypeIntoCache))]
public static class SavedPropertiesTypeCacheInjectPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        SavedPropertiesTypeCacheCompat.RefreshNetIdBitSize("SavedPropertiesTypeCache.InjectTypeIntoCache postfix");
    }
}
#endif
