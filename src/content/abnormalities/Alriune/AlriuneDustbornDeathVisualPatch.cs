using HarmonyLib;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.Alriune;

// 保留节点的死亡不进入原版死亡淡出，也不清空即将显示的恢复意图。
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
[LibraryPatch(Reason = "原版 StartDeathAnim 非虚，会禁用交互、冻结意图并播放死亡淡出，没有跳过的扩展点；只作用于阿尔卢尼存活时不移除的尘土所生。")]
internal static class AlriuneDustbornDeathVisualPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NCreature __instance, bool shouldRemove, ref float __result)
    {
        if (!shouldRemove && __instance.Entity.Monster is AlriuneDustborn { HasLivingAlriune: true })
        {
            __instance.ToggleIsInteractable(false);
            __result = 0f;
            return false;
        }
        return true;
    }
}
