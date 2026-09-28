using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LibraryOfRuina.infra.patching;

/// <summary>
/// 哪些类会被当作补丁类安装、哪些方法是跳过型前缀。<see cref="LibraryPatcher"/> 与离线快照工具
/// tools/ModSnapshot（MetadataLoadContext，只有元数据）编译同一份源码，所以这里只读
/// <see cref="CustomAttributeData"/>、按全名比较，不实例化特性，也不引用 Harmony 类型。
/// 规则照 Harmony 2.4 的实现写：改动这里等于改动安装范围，要用 headless 补丁表比对确认。
/// </summary>
internal static class PatchClassRules
{
    private const string HarmonyAttributeFullName = "HarmonyLib.HarmonyAttribute";
    private const string LibraryPatchAttributeName = "LibraryPatchAttribute";

    // AttributePatch.allPatchTypes 的顺序：方法名或 [HarmonyLib.Harmony<种类>] 特性先命中哪个种类就算哪个。
    private static readonly string[] PatchTypes =
        ["Prefix", "Postfix", "Transpiler", "Finalizer", "ReversePatch", "InnerPrefix", "InnerPostfix"];

    /// <summary>
    /// 对应 Harmony 的 <c>Type.HasHarmonyAttribute()</c>：类（含基类继承来的）上任一特性是
    /// HarmonyAttribute 的子类即可，不要求是 <c>[HarmonyPatch]</c>。只写 <c>[HarmonyPriority]</c>、
    /// 目标放在方法级 <c>[HarmonyPatch]</c> 上的类同样会被安装。
    /// </summary>
    public static bool HasHarmonyClassAttribute(Type type)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            if (current.GetCustomAttributesData().Any(static data => IsHarmonyAttribute(data.AttributeType)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary><c>[LibraryPatch(Optional = true)]</c> 的类不论有没有类级 Harmony 特性都会安装。</summary>
    public static bool IsOptional(Type type)
    {
        return LibraryPatchArgument(type, "Optional") is true;
    }

    public static bool IsInstalled(Type type)
    {
        return IsOptional(type) || HasHarmonyClassAttribute(type);
    }

    public static string? Reason(Type type)
    {
        return LibraryPatchArgument(type, "Reason") as string;
    }

    /// <summary>Harmony 视为前缀、且返回 bool（可以跳过原方法和之后影响原方法的前缀）的方法。</summary>
    public static bool IsSkipPrefix(MethodInfo method)
    {
        return method.IsStatic && HarmonyPatchType(method) == "Prefix" && method.ReturnType.FullName == "System.Boolean";
    }

    public static IEnumerable<MethodInfo> SkipPrefixes(Type type)
    {
        return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(IsSkipPrefix);
    }

    private static string? HarmonyPatchType(MethodInfo method)
    {
        var attributes = new HashSet<string>(method.GetCustomAttributesData().Select(static data => data.AttributeType.FullName ?? ""),
            StringComparer.Ordinal);
        return PatchTypes.FirstOrDefault(kind => method.Name == kind || attributes.Contains("HarmonyLib.Harmony" + kind));
    }

    private static bool IsHarmonyAttribute(Type attributeType)
    {
        for (Type? current = attributeType.BaseType; current != null; current = current.BaseType)
        {
            if (current.FullName == HarmonyAttributeFullName)
            {
                return true;
            }
        }

        return false;
    }

    private static object? LibraryPatchArgument(Type type, string name)
    {
        CustomAttributeData? data = type.GetCustomAttributesData()
            .FirstOrDefault(static data => data.AttributeType.Name == LibraryPatchAttributeName);
        return data?.NamedArguments.FirstOrDefault(argument => argument.MemberName == name).TypedValue.Value;
    }
}
