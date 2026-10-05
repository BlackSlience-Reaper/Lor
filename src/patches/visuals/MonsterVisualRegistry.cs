using System;
using System.Linq;
using System.Reflection;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.visuals;

/// <summary>
/// 把一只本模组怪物登记进外观目录（<see cref="MonsterVisualCatalog"/>）。
/// <list type="bullet">
/// <item>代码拼装的精灵外观：挂在外观类的静态只读 <see cref="CreatureVisualLayout"/> 字段上，这个字段就是该怪物的布局；
/// 精灵配置取外观类自己声明的那个静态 <see cref="SpriteVisualProfile"/> 字段。几只怪共用同一布局时，同一字段挂多个特性。</item>
/// <item>场景外观：挂在外观类上并写 <see cref="ScenePath"/>，布局由场景自己决定。</item>
/// <item>没有外观类的静态贴图：挂在布局字段上并写 <see cref="StaticTexture"/>。</item>
/// </list>
/// 外观类不在本目录维护时，特性和布局写在旁边的静态声明类里，用 <see cref="Visuals"/> 指明外观类。
/// 声明类上带 <see cref="MonsterVisualFactoryAttribute"/> 的方法代替默认工厂。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
internal sealed class MonsterVisualAttribute(Type monsterType) : Attribute
{
    public Type MonsterType { get; } = monsterType;

    /// <summary>外观节点的类型；缺省是声明特性的类。</summary>
    public Type? Visuals { get; set; }

    public string? ScenePath { get; set; }

    public string? StaticTexture { get; set; }
}

/// <summary>
/// 按怪物当前状态生成外观的工厂，签名为 <c>static NCreatureVisuals (MonsterModel)</c>。
/// 声明类里至多一个；有它时，该类登记的所有怪物都用它代替默认工厂。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class MonsterVisualFactoryAttribute : Attribute;

/// <summary>
/// 扫描本程序集里的 <see cref="MonsterVisualAttribute"/>，为外观目录建表。表以怪物类型的模型 ID
/// （<c>ModelDb.GetId(type).Entry</c>，原版给模型实例赋 <c>Id</c> 用的同一个函数）为键，所以按
/// <c>monster.Id.Entry</c> 查询只命中登记类型本身，子类不会顺带被包装。
/// 只由 <see cref="MonsterVisualCatalog"/> 的静态初始化调用：建表时机与原来的字面量表相同（第一次查目录时），
/// 读取布局字段会触发外观类的静态初始化，与原来直接引用 <c>Profile</c> 触发的是同一批类。
/// </summary>
internal static class MonsterVisualRegistry
{
    private const BindingFlags DeclaredStatic =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static string EntryOf(Type monsterType) => ModelDb.GetId(monsterType).Entry;

    internal static Dictionary<string, MonsterVisualCatalogEntry> BuildEntries()
    {
        var entries = new Dictionary<string, MonsterVisualCatalogEntry>(StringComparer.Ordinal);
        foreach (Type declaring in LibraryAssemblyTypes.All)
        {
            foreach (MonsterVisualAttribute attribute in
                     declaring.GetCustomAttributes<MonsterVisualAttribute>(inherit: false))
            {
                Add(entries, attribute, declaring, layout: null);
            }

            foreach (FieldInfo field in declaring.GetFields(DeclaredStatic))
            {
                foreach (MonsterVisualAttribute attribute in
                         field.GetCustomAttributes<MonsterVisualAttribute>(inherit: false))
                {
                    if (field.FieldType != typeof(CreatureVisualLayout) || !field.IsInitOnly)
                    {
                        throw new InvalidOperationException(
                            $"[MonsterVisual] on {declaring.FullName}.{field.Name} must mark a static readonly "
                            + $"{nameof(CreatureVisualLayout)} field.");
                    }

                    Add(entries, attribute, declaring, (CreatureVisualLayout)field.GetValue(null)!);
                }
            }
        }

        return entries;
    }

    private static void Add(
        Dictionary<string, MonsterVisualCatalogEntry> entries,
        MonsterVisualAttribute attribute,
        Type declaring,
        CreatureVisualLayout? layout)
    {
        Type monsterType = attribute.MonsterType;
        if (!typeof(MonsterModel).IsAssignableFrom(monsterType) || monsterType.IsAbstract)
        {
            throw new InvalidOperationException(
                $"[MonsterVisual] on {declaring.FullName} names {monsterType.FullName}, "
                + "which is not a concrete monster model.");
        }

        string id = EntryOf(monsterType);
        MonsterVisualCatalogEntry entry = CreateEntry(id, attribute, declaring, layout);
        if (!entries.TryAdd(id, entry))
        {
            throw new InvalidOperationException(
                $"Monster visual '{id}' is registered more than once (again by {declaring.FullName}).");
        }
    }

    private static MonsterVisualCatalogEntry CreateEntry(
        string id,
        MonsterVisualAttribute attribute,
        Type declaring,
        CreatureVisualLayout? layout)
    {
        Func<MonsterModel, NCreatureVisuals>? custom = FindCustomFactory(declaring);

        if (attribute.StaticTexture is { } texturePath)
        {
            if (layout == null || attribute.Visuals != null || attribute.ScenePath != null || custom != null)
            {
                throw new InvalidOperationException(
                    $"Static sprite visual '{id}' must be declared on a layout field without a visuals class.");
            }

            return new MonsterVisualCatalogEntry(
                Layout: layout,
                Profile: null,
                StaticDefaultIdleTexturePath: texturePath,
                Factory: monster => WrappedMonsterVisualFactory.CreateStaticSpriteVisuals(
                    monster.Id.Entry,
                    MonsterVisualCatalog.GetLayout(monster.Id.Entry),
                    texturePath));
        }

        Type visuals = attribute.Visuals ?? declaring;
        if (attribute.ScenePath is { } scenePath)
        {
            if (layout != null || !typeof(SceneAnimatedCreatureVisuals).IsAssignableFrom(visuals))
            {
                throw new InvalidOperationException(
                    $"Scene-backed monster visual '{id}' must be declared on a "
                    + $"{nameof(SceneAnimatedCreatureVisuals)} class, not on a layout field.");
            }

            return new MonsterVisualCatalogEntry(
                Layout: null,
                Profile: null,
                StaticDefaultIdleTexturePath: null,
                Factory: custom ?? DefaultFactory(typeof(SceneDefaultFactory<>), visuals, scenePath),
                ScenePath: scenePath);
        }

        if (layout == null || !typeof(SpriteAttackCreatureVisuals).IsAssignableFrom(visuals))
        {
            throw new InvalidOperationException(
                $"Sprite monster visual '{id}' must be declared on a layout field of a "
                + $"{nameof(SpriteAttackCreatureVisuals)} class.");
        }

        return new MonsterVisualCatalogEntry(
            Layout: layout,
            Profile: FindProfile(id, visuals),
            StaticDefaultIdleTexturePath: null,
            Factory: custom ?? DefaultFactory(typeof(ScriptedDefaultFactory<>), visuals, null));
    }

    private static SpriteVisualProfile FindProfile(string id, Type visuals)
    {
        FieldInfo[] fields = visuals.GetFields(DeclaredStatic)
            .Where(static field => field.FieldType == typeof(SpriteVisualProfile))
            .ToArray();
        if (fields.Length != 1)
        {
            throw new InvalidOperationException(
                $"Sprite monster visual '{id}': {visuals.FullName} must declare exactly one static "
                + $"{nameof(SpriteVisualProfile)} field; found {fields.Length}.");
        }

        return fields[0].GetValue(null) as SpriteVisualProfile
            ?? throw new InvalidOperationException(
                $"Sprite monster visual '{id}': {visuals.FullName}.{fields[0].Name} is null.");
    }

    private static Func<MonsterModel, NCreatureVisuals>? FindCustomFactory(Type declaring)
    {
        MethodInfo[] methods = declaring.GetMethods(DeclaredStatic)
            .Where(static method => method.IsDefined(typeof(MonsterVisualFactoryAttribute), inherit: false))
            .ToArray();
        return methods.Length switch
        {
            0 => null,
            1 => methods[0].CreateDelegate<Func<MonsterModel, NCreatureVisuals>>(),
            _ => throw new InvalidOperationException(
                $"{declaring.FullName} declares more than one [{nameof(MonsterVisualFactoryAttribute)}] method."),
        };
    }

    // 默认工厂按外观类实例化泛型包装：委托里直接调用泛型工厂，异常原样抛出，不经反射调用包装。
    private static Func<MonsterModel, NCreatureVisuals> DefaultFactory(
        Type factoryDefinition,
        Type visuals,
        string? scenePath) =>
        ((IDefaultFactory)Activator.CreateInstance(factoryDefinition.MakeGenericType(visuals))!).Create(scenePath);

    private interface IDefaultFactory
    {
        Func<MonsterModel, NCreatureVisuals> Create(string? scenePath);
    }

    private sealed class ScriptedDefaultFactory<TVisuals> : IDefaultFactory
        where TVisuals : SpriteAttackCreatureVisuals, new()
    {
        public Func<MonsterModel, NCreatureVisuals> Create(string? scenePath) =>
            static monster => WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<TVisuals>(monster.Id.Entry);
    }

    private sealed class SceneDefaultFactory<TVisuals> : IDefaultFactory
        where TVisuals : SceneAnimatedCreatureVisuals, new()
    {
        public Func<MonsterModel, NCreatureVisuals> Create(string? scenePath) =>
            monster => WrappedMonsterVisualFactory.CreateSceneBackedVisuals<TVisuals>(monster.Id.Entry, scenePath!);
    }
}
