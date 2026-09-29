using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LibraryOfRuina.infra.helpers;

/// <summary>
/// 自动发现的遍历顺序。补丁安装（同目标、同优先级补丁按安装先后执行）、卡池登记（原版按追加顺序拼到池尾）、
/// 盟友 provider（先匹配的优先）与事件遗物池（按顺序追加到原版结果后）都直接按发现顺序生效。
/// <see cref="Assembly.GetTypes"/> 返回 TypeDef 顺序，编译器按命名空间首次出现的位置排，移动文件或改命名空间
/// 就会整块换位，所以这些类型的先后固定在 <c>type_discovery_order.txt</c> 里：表中的类型按表的顺序排在前面，
/// 其余类型保持 TypeDef 顺序排在后面。表的键不含命名空间，之后再移动文件也不影响顺序。
/// 运行期（<see cref="LibraryAssemblyTypes"/>）与离线快照工具 tools/ModSnapshot 共用这份源码，
/// 所以这里只按名字比较，不引用本模组或游戏的类型。
/// </summary>
internal static class TypeDiscoveryOrder
{
    /// <summary>嵌入资源名，与 LibraryOfRuina.csproj 的 LogicalName 一致。</summary>
    public const string ResourceName = "LibraryOfRuina.type_discovery_order.txt";

    /// <summary>表里的键：去掉命名空间的全名（嵌套类型为 <c>Outer+Inner</c>，泛型带 <c>`n</c>）。</summary>
    public static string Key(Type type)
    {
        string fullName = type.FullName ?? type.Name;
        string? ns = type.Namespace;
        return string.IsNullOrEmpty(ns) ? fullName : fullName[(ns.Length + 1)..];
    }

    /// <summary>
    /// 读取嵌入的顺序表。运行期缺表必须失败（否则会悄悄退回 TypeDef 顺序）；快照工具也会扫描不带表的
    /// 测试程序集，传 <paramref name="required"/> = false 时返回空表。
    /// </summary>
    public static IReadOnlyList<string> ReadTable(Assembly assembly, bool required = true)
    {
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            return required ? throw new InvalidOperationException("Missing embedded resource " + ResourceName) : [];
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd()
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
    }

    /// <summary>表中的类型按表序在前，其余保持输入顺序；表里的键重复或同一个键对应多个类型时抛出。</summary>
    public static Type[] Sort(IReadOnlyList<Type> types, IReadOnlyList<string> table)
    {
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < table.Count; i++)
        {
            if (!rank.TryAdd(table[i], i))
            {
                throw new InvalidOperationException("Duplicate key in " + ResourceName + ": " + table[i]);
            }
        }

        var listed = new Type?[table.Count];
        var rest = new List<Type>(types.Count);
        foreach (Type type in types)
        {
            if (rank.TryGetValue(Key(type), out int index))
            {
                if (listed[index] != null)
                {
                    throw new InvalidOperationException("Key " + table[index] + " in " + ResourceName
                                                        + " matches both " + listed[index]!.FullName + " and " + type.FullName);
                }

                listed[index] = type;
            }
            else
            {
                rest.Add(type);
            }
        }

        return listed.OfType<Type>().Concat(rest).ToArray();
    }
}
