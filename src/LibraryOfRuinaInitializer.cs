using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryLib.Multiplayer;
using LibraryOfRuina.audio;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.features.intentgraph;
using LibraryOfRuina.features.settings;
using LibraryOfRuina.features.temporarymaps;
using LibraryOfRuina.helpers;
using LibraryOfRuina.networking;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.LittleRedMercenary;
using LibraryOfRuina.specialguests;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace LibraryOfRuina;

[ModInitializer(nameof(Initialize))]
public static class LibraryOfRuinaInitializer
{
    private const string LogPrefix = "[LibraryOfRuina] ";

    /// <summary>
    /// 一个初始化步骤。必需步骤失败时不再执行后续步骤（尤其不再安装玩法补丁），
    /// 避免内容注册了一半却没有补丁；可选步骤（表现层）失败只记录，继续初始化。
    /// </summary>
    private readonly record struct InitStep(string Name, bool Required, Action Run);

    public static void Initialize()
    {
        var report = new InitReport();

        // 联机类型与框架必须先于任何内容注册；失败时整个模组不注入内容。
        if (!report.RunAll(
            [
                new("NetTypes", true, static () => LibraryManagedNetTypes.RegisterAssembly(Assembly.GetExecutingAssembly())),
                new("Network", true, LibraryNetwork.Initialize),
                new("HealthBarForecast", true, LibraryHealthBarForecastFeature.Initialize),
                new("MadokaLongbowCompat", true, MadokaLongbowSavedStateCompat.Initialize),
            ]))
        {
            report.LogSummary("gameplay initialization was skipped");
            return;
        }

        var harmony = new Harmony("FYY.LibraryOfRuina");
        bool blockedByIncompatibleMod = false;
        if (!report.RunAll(
            [
                new("Settings", true, static () => ExtSettingsRegistry.Register("LibraryOfRuina", new LibraryOfRuinaSettings())),
                new("SfxMixer", false, LibrarySfxMixer.Initialize),
                new("IncompatibleModGuard", true, () => blockedByIncompatibleMod = IncompatibleModGuard.DetectBlockingMods()),
            ]))
        {
            report.LogSummary("gameplay initialization was skipped");
            return;
        }

        // 检测到已知不兼容模组时与关闭“启用废墟图书馆内容”走同一条路径：不改动玩家的设置值，
        // 仅本次启动不注入内容，并由 MainMenuIncompatibleModNoticePatch 在主菜单弹窗说明。
        if (!LibraryOfRuinaSettings.MonsterExtensionEnabled || blockedByIncompatibleMod)
        {
            report.RunAll([new("SettingsUiPatches", false, () => PatchSettingsUi(harmony))]);
            report.LogSummary(
                "library injection disabled; settings UI remains available. Skipped content pools, "
                + "runtime controllers, BGM, encounters, and gameplay Harmony patches");
            return;
        }

        LibraryOfRuinaSettings.EnableRuntimeSideEffects();

        IReadOnlyList<string> failedPatchClasses = [];
        bool completed = report.RunAll(
        [
            new("IntentGraphDisplayConfig", false, IntentGraphDisplayConfigRepository.Initialize),
            new("TemporaryMaps", true, TemporaryMapController.Initialize),
            new("MainMenuBgm", false, MainMenuBgmController.Initialize),
            new("NonCombatRunBgm", false, NonCombatRunBgmController.Initialize),
            new("AbnormalityEliteBgm", false, AbnormalityEliteBgmController.Initialize),
            new("ReverberationEnsembleBgm", false, ReverberationEnsembleBgmController.Initialize),
            new("AllyTurnProviders", true, RegisterAllyTurnProviders),
            new("SavedPropertyTypes", true, SavedPropertiesTypeCacheCompat.InjectModSavedPropertyTypes),
            new("SpecialGuests", true, SpecialGuestAutoRegistrar.Initialize),
            new("CardPools", true, RegisterRuntimeCardPools),
            new("GameplayPatches", true, () => failedPatchClasses = ApplyGameplayPatches(harmony)),
            new("Cursor", false, LibraryCursorPatch.ApplyToCurrentGame),
            new("OptionalPatches", false, () => TryApplyOptionalPatches(harmony)),
        ]);

        if (failedPatchClasses.Count > 0)
        {
            report.AddFailure(
                failedPatchClasses.Count + " Harmony patch class(es) skipped: " + string.Join(", ", failedPatchClasses));
        }

        report.LogSummary(completed ? null : "a required step failed; later steps were skipped");
    }

    // 逐个补丁类应用，等价于 Harmony.PatchAll 的遍历顺序。单个补丁类失败（例如 Android 的 Mono 运行时
    // 无法为某些方法生成替换体）时只记录并跳过该类，其余补丁照常生效，避免整个模组停在部分初始化。
    private static IReadOnlyList<string> ApplyGameplayPatches(Harmony harmony)
    {
        var failedPatchClasses = new List<string>();
        foreach (Type type in LibraryAssemblyTypes.Loadable)
        {
            try
            {
                if (!type.HasHarmonyAttribute())
                {
                    continue;
                }

                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception e)
            {
                failedPatchClasses.Add(type.FullName ?? type.Name);
                Log.Error(
                    LogPrefix + "Harmony patch class "
                    + type.FullName
                    + " failed to apply and was skipped: "
                    + e);
            }
        }

        return failedPatchClasses;
    }

    private static void PatchSettingsUi(Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(InjectExtSettingsSubmenuTypePatch)).Patch();
        harmony.CreateClassProcessor(typeof(InjectSettingsScreenModConfigPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SettingsScreenModConfigVisibilityPatch)).Patch();
        harmony.CreateClassProcessor(typeof(MainMenuShowPendingSettingsErrorsPatch)).Patch();
        harmony.CreateClassProcessor(typeof(MainMenuIncompatibleModNoticePatch)).Patch();
        harmony.CreateClassProcessor(typeof(NGameQuitExtSettingsSavePatch)).Patch();
    }

    private static void RegisterRuntimeCardPools()
    {
        foreach (var type in LibraryAssemblyTypes.All)
        {
            var attr = type.GetCustomAttribute<CardPoolAttribute>();
            if (attr != null)
            {
                ModHelper.AddModelToPool(attr.PoolType, type);
            }
        }
    }

    // 可选补丁：安装失败只记一条 Info，不计入初始化失败。
    private static void TryApplyOptionalPatches(Harmony harmony)
    {
        try
        {
            harmony.CreateClassProcessor(typeof(FocusOfAttentionCardCmdAutoPlayPatch)).Patch();
        }
        catch (Exception e)
        {
            Log.Info(LogPrefix + "Optional patch FocusOfAttentionCardCmdAutoPlayPatch skipped: " + e.Message);
        }
    }

    private static void RegisterAllyTurnProviders()
    {
        var providerType = typeof(IAllyTurnProvider);
        var types = LibraryAssemblyTypes.All
            .Where(p => providerType.IsAssignableFrom(p)
                        && p is { IsAbstract: false, IsInterface: false });
        foreach (var type in types)
        {
            var provider = (IAllyTurnProvider)Activator.CreateInstance(type)!;
            AllyTurnRegistry.RegisterProvider(provider);
        }
    }

    private sealed class InitReport
    {
        private readonly List<string> _succeeded = [];
        private readonly List<string> _failures = [];

        /// <summary>依次执行步骤；遇到失败的必需步骤时停止并返回 false。</summary>
        public bool RunAll(IEnumerable<InitStep> steps)
        {
            foreach (InitStep step in steps)
            {
                try
                {
                    step.Run();
                    _succeeded.Add(step.Name);
                }
                catch (Exception exception)
                {
                    _failures.Add(step.Name + (step.Required ? " (required)" : ""));
                    Log.Error(LogPrefix + "Initialization step " + step.Name + " failed: " + exception);
                    if (step.Required)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void AddFailure(string description) => _failures.Add(description);

        /// <summary>整个初始化只输出这一行结果，失败项与说明都在里面。</summary>
        public void LogSummary(string? note)
        {
            if (_failures.Count == 0 && note == null)
            {
                Log.Info("LibraryOfRuina loaded successfully (" + _succeeded.Count + " initialization steps).");
                return;
            }

            string summary = LogPrefix + "Initialization finished: " + _succeeded.Count + " step(s) ok"
                             + (_failures.Count == 0 ? "" : "; failed: " + string.Join("; ", _failures))
                             + (note == null ? "" : "; " + note)
                             + ".";
            if (_failures.Count == 0)
            {
                Log.Info(summary);
            }
            else
            {
                Log.Error(summary);
            }
        }
    }
}
