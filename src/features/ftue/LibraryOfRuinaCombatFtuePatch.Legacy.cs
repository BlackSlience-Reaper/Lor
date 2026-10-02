#if STS2_0_107_1
using System;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Saves;
namespace LibraryOfRuina.features.ftue;
public static partial class LibraryOfRuinaCombatFtuePatch
{
    // 旧版没有 CombatBegan。StartCombatInternal 先把 IsStarting 置 false，再 await Hook.BeforeCombatStart，
    // 之后才 NModalContainer.Add 原版战斗规则教学；Add 发现已有弹窗会直接返回，原版教学就被吞掉。
    // 所以不能按 IsStarting 判断，改为等本场第一次 TurnStarted：它在 StartTurn 里触发，
    // StartTurn 排在原版教学 Add 之后，ShowCombatFtue 再等原版教学关闭。
    private static void ScheduleCombatFtue(CombatManager manager, CombatState state, FtueConfig config)
    {
        if (SaveManager.Instance.SeenFtue("combat_rules_ftue"))
        {
            _ = TaskHelper.RunSafely(ShowCombatFtue(manager, state, config));
            return;
        }
        Action<CombatState>? onTurnStarted = null;
        onTurnStarted = startedState =>
        {
            manager.TurnStarted -= onTurnStarted;
            if (ReferenceEquals(startedState, state) && IsCurrentCombat(manager, state))
                _ = TaskHelper.RunSafely(ShowCombatFtue(manager, state, config));
        };
        manager.TurnStarted += onTurnStarted;
    }
}
#endif
