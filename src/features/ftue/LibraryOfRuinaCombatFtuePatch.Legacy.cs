#if STS2_0_107_1
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Saves;
namespace LibraryOfRuina.features.ftue;
public static partial class LibraryOfRuinaCombatFtuePatch
{
    private static void ScheduleCombatFtue(CombatManager manager, CombatState state, FtueConfig config) =>
        _ = TaskHelper.RunSafely(WaitForCombatStart(manager, state, config));

    private static async Task WaitForCombatStart(CombatManager manager, CombatState state, FtueConfig config)
    {
        // 旧版无 CombatBegan；保留原版教学的优先权，等待启动阶段结束并逐帧核对同一场战斗。
        while (!SaveManager.Instance.SeenFtue("combat_rules_ftue") && manager.IsStarting)
        {
            var modal = NModalContainer.Instance;
            var tree = modal?.GetTree();
            if (tree == null || !IsCurrentCombat(manager, state)) return;
            await modal!.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        if (IsCurrentCombat(manager, state)) await ShowCombatFtue(manager, state, config);
    }
}
#endif
