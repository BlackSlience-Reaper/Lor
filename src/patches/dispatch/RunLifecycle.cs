using HarmonyLib;
using LibraryOfRuina.content.specialguests;
using LibraryOfRuina.content.specialguests.Iori;
using LibraryOfRuina.content.specialguests.Xiao;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// 局生命周期的唯一入口。原来 6 个功能各自在 <c>RunManager.CleanUp</c> 上挂前缀；这里按原来的安装顺序
/// 依次调用，前一个抛异常时后面的不再执行，与多个前缀时相同。新增的复位逻辑加在这里，不要再单独挂补丁；
/// 简单的局级、战斗级值用 <see cref="RunScoped{T}"/> / <see cref="CombatScoped{TKey, TValue}"/>，由这里统一复位。
/// </summary>
internal static class RunLifecycle
{
    internal static void OnRunCleaningUp(bool graceful)
    {
        // 放在最前：只改静态值、不会抛出，后面的步骤抛异常时抗性也已回到本地设置。
        LibraryRunSettings.OnRunCleaningUp();
        // 局级、战斗级容器（RunScoped / CombatScoped）同样只写内存、不会抛出，排在可能抛异常的步骤之前。
        ScopedState.ResetAll();
        SpecialGuestRunCleanup.OnRunCleaningUp();
        XiaoSpecialGuestBgmController.Stop();
        IoriSpecialGuestBgmController.Stop();
        EncounterBgmController.OnRunCleaningUp(graceful);
        AbnormalityEliteBgmController.OnRunCleaningUp();
        ReverberationEnsembleBgmController.OnRunCleaningUp();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp), typeof(bool))]
    private static class CleanUpPatch
    {
        private static void Prefix(bool graceful) => OnRunCleaningUp(graceful);
    }
}
