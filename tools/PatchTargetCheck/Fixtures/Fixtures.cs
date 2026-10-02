using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PatchTargetFixtures;

public class Base
{
    public void Inherited() { }
}

public class Target : Base
{
    private int count;

    public void M(int x, string name) { }

    public void Over(int a) { }

    public void Over(string a) { }

    public int Value() => count;
}

// 通过：参数级 HarmonyArgument、字段注入、按序号注入到 object。
[HarmonyPatch(typeof(Target), nameof(Target.M))]
internal static class GoodPatch
{
    private static void Prefix(Target __instance, int x, [HarmonyArgument("name")] string renamed, int ___count, object __1) { }
}

// 失败：参数名不存在、类型不兼容、序号越界、字段不存在。
[HarmonyPatch(typeof(Target), nameof(Target.M))]
internal static class BadInjectionPatch
{
    private static void Prefix(int y, string x, int __7, int ___nothere) { }
}

// 失败：无返回值的原方法取 __result，__instance 类型不兼容。
[HarmonyPatch(typeof(Target), nameof(Target.M))]
internal static class BadResultPatch
{
    private static void Postfix(int __result, string __instance) { }
}

// 失败：AccessTools.DeclaredMethod 只看声明类型本身，基类方法找不到。
[HarmonyPatch(typeof(Target), nameof(Base.Inherited))]
internal static class InheritedPatch
{
    private static void Prefix() { }
}

// 失败：没给参数表而目标有重载，Harmony 抛 AmbiguousMatchException。
[HarmonyPatch(typeof(Target), nameof(Target.Over))]
internal static class AmbiguousPatch
{
    private static void Prefix() { }
}

// 通过：类级给类型，方法级给方法名与参数表。
[HarmonyPatch(typeof(Target))]
internal static class MethodLevelPatch
{
    [HarmonyPatch(nameof(Target.Over), typeof(string))]
    [HarmonyPrefix]
    private static void Before(string a) { }

    [HarmonyPatch(nameof(Target.Value))]
    private static void Postfix(ref int __result) { }
}

// 失败：TargetMethod 返回 null。
[HarmonyPatch]
internal static class NullTargetPatch
{
    private static MethodBase TargetMethod() => null;

    private static void Prefix() { }
}

// 失败：TargetMethods 含 null。
[HarmonyPatch]
internal static class NullElementPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => [AccessTools.Method(typeof(Target), nameof(Target.Value)), null];

    private static void Prefix() { }
}

// 失败：TargetMethod 抛异常（对应只存在于另一目标的方法）。
[HarmonyPatch]
internal static class ThrowingTargetPatch
{
    private static MethodBase TargetMethod() => typeof(Target).GetMethod("Missing") ?? throw new MissingMethodException(nameof(Target), "Missing");

    private static void Prefix() { }
}

// 失败：Prepare 拒绝安装。
[HarmonyPatch(typeof(Target), nameof(Target.Value))]
internal static class DeclinePatch
{
    private static bool Prepare() => false;

    private static void Prefix() { }
}

// 失败：TargetMethod 与目标特性混用。
[HarmonyPatch(typeof(Target), nameof(Target.Value))]
internal static class MixedTargetPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(Target), nameof(Target.Value));

    private static void Prefix() { }
}
