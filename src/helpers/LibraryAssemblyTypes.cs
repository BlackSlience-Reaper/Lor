using System;
using System.Linq;
using System.Reflection;

namespace LibraryOfRuina.helpers;

/// <summary>
/// 本程序集的类型表，只枚举一次，供补丁安装、卡池、盟友回合、SavedProperty、特殊来宾等自动发现共用。
/// 顺序由 <see cref="TypeDiscoveryOrder"/> 决定：补丁安装与同目标补丁的执行顺序、卡池与事件遗物池的追加顺序、
/// 盟友 provider 的匹配顺序都依赖它。
/// </summary>
internal static class LibraryAssemblyTypes
{
    private static readonly Lazy<(Type[] Types, ReflectionTypeLoadException? Error)> Scan = new(static () =>
    {
        Assembly assembly = typeof(LibraryAssemblyTypes).Assembly;
        IReadOnlyList<string> table = TypeDiscoveryOrder.ReadTable(assembly);
        try
        {
            return (TypeDiscoveryOrder.Sort(assembly.GetTypes(), table), null);
        }
        catch (ReflectionTypeLoadException exception)
        {
            Type[] loaded = exception.Types.Where(static type => type != null).Select(static type => type!).ToArray();
            return (TypeDiscoveryOrder.Sort(loaded, table), exception);
        }
    });

    /// <summary>能加载的全部类型；部分类型加载失败时照样返回其余类型（补丁安装按类跳过失败项）。</summary>
    public static Type[] Loadable => Scan.Value.Types;

    /// <summary>全部类型；任何类型加载失败时抛出，供不能在缺类型时继续的发现流程使用。</summary>
    public static Type[] All
    {
        get
        {
            var (types, error) = Scan.Value;
            if (error != null)
            {
                string loaderErrors = string.Join(
                    Environment.NewLine,
                    error.LoaderExceptions.Where(static e => e != null).Select(static e => e!.Message));
                throw new InvalidOperationException("Unable to enumerate LibraryOfRuina types. " + loaderErrors, error);
            }

            return types;
        }
    }
}
