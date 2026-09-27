using HarmonyLib;
using LibraryOfRuina.acts;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(TreasureRoom), MethodType.Constructor, new[] { typeof(int) })]
internal static class ReverberationEnsembleTreasureRoomPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref int actIndex)
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state?.Act is not ReverberationEnsembleAct act
            || actIndex != state.CurrentActIndex)
        {
            return;
        }

        // 原版构造函数只接受前三幕索引；残响乐团沿用 Glory 模板的合法索引。
        // 仅调整构造参数，房间资源、奖励和存档继续使用真实的第四幕运行状态。
        actIndex = act.TemplateAct.Index;
    }
}
