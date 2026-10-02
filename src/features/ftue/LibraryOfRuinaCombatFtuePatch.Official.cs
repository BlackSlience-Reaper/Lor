#if STS2_0_111_0
using System;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Saves;
namespace LibraryOfRuina.features.ftue;
public static partial class LibraryOfRuinaCombatFtuePatch
{
    private static void ScheduleCombatFtue(CombatManager manager, CombatState state, FtueConfig config)
    {
        if (SaveManager.Instance.SeenFtue("combat_rules_ftue"))
        {
            _ = TaskHelper.RunSafely(ShowCombatFtue(manager, state, config));
            return;
        }
        Action<CombatState>? onCombatBegan = null;
        onCombatBegan = beganState =>
        {
            manager.CombatBegan -= onCombatBegan;
            if (ReferenceEquals(beganState, state) && IsCurrentCombat(manager, state))
                _ = TaskHelper.RunSafely(ShowCombatFtue(manager, state, config));
        };
        manager.CombatBegan += onCombatBegan;
    }
}
#endif
