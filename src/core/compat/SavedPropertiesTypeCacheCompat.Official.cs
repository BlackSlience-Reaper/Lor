#if STS2_0_111_0
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.core.compat;

public static partial class SavedPropertiesTypeCacheCompat
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

        _cachedAutoDiscoveredTypes = LibraryAssemblyTypes.All
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


    public static void InjectModSavedPropertyTypes()
    {
        Type[] allTypes = GetAllModSavedPropertyTypes();
        Log.Info("[LibraryOfRuina.SavedProperties] 0.111.0 由 ModelIdSerializationCache 管理属性网络 ID；无需注入已移除的 SavedPropertiesTypeCache（"
            + allTypes.Length
            + " 个模组类型）。");
    }

    public static void RefreshNetIdBitSize(string reason, bool forceLog = false)
    {
    }

    /// <summary>
    /// 本程序集 SavedProperty 的“类型全名 | 属性名 | 类型 | 序列化条件 | 顺序”与相关枚举值的文本，只给验证套件核对用。
    /// 它不参与联机校验（v0.21.2 的联机指纹也只带设置，指纹已随 MultiplayerConfigFingerprintPatch 删除）；
    /// 与旧版本能否联机由 net-id 布局和 manifest 版本号决定。
    /// </summary>
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
    }
}

#endif
