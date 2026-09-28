using System;

namespace LibraryOfRuina.infra.patching;

/// <summary>
/// 补丁类的元数据，由 <see cref="LibraryPatcher"/> 读取。不标注的 <c>[HarmonyPatch]</c> 类按必需补丁安装。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class LibraryPatchAttribute : Attribute
{
    /// <summary>
    /// 可选补丁：在全部必需补丁之后安装，目标缺失或 Prepare 返回 false 只记 Info，不计入初始化失败。
    /// 可选补丁类可以不带类级 <c>[HarmonyPatch]</c>，只用 <c>[HarmonyTargetMethod]</c> 给出目标。
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>为什么需要这个补丁；跳过型前缀与 Transpiler 应写明原版为什么没有可用的 Hook 或虚方法。</summary>
    public string? Reason { get; init; }
}
