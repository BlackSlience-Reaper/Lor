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
    public static void Initialize()
    {
        try
        {
            LibraryManagedNetTypes.RegisterAssembly(Assembly.GetExecutingAssembly());
            LibraryNetwork.Initialize();
            LibraryHealthBarForecastFeature.Initialize();
            MadokaLongbowSavedStateCompat.Initialize();
        }
        catch (Exception exception)
        {
            Log.Error(
                "[LibraryOfRuina] Required multiplayer/UI framework initialization failed; "
                + "gameplay initialization was skipped: "
                + exception);
            return;
        }

        var settings = new LibraryOfRuinaSettings();
        ExtSettingsRegistry.Register("LibraryOfRuina", settings);
        LibrarySfxMixer.Initialize();
        var harmony = new Harmony("FYY.LibraryOfRuina");

        // 检测到已知不兼容模组时与关闭“启用废墟图书馆内容”走同一条路径：不改动玩家的设置值，
        // 仅本次启动不注入内容，并由 MainMenuIncompatibleModNoticePatch 在主菜单弹窗说明。
        bool blockedByIncompatibleMod = IncompatibleModGuard.DetectBlockingMods();
        if (!LibraryOfRuinaSettings.MonsterExtensionEnabled || blockedByIncompatibleMod)
        {
            PatchSettingsUi(harmony);
            Log.Info("[LibraryOfRuina] Library injection disabled; settings UI remains available. Skipping content pools, runtime controllers, BGM, encounters, and gameplay Harmony patches.");
            return;
        }

        LibraryOfRuinaSettings.EnableRuntimeSideEffects();

        IntentGraphDisplayConfigRepository.Initialize();
        TemporaryMapController.Initialize();

        MainMenuBgmController.Initialize();
        NonCombatRunBgmController.Initialize();
        AbnormalityEliteBgmController.Initialize();
        ReverberationEnsembleBgmController.Initialize();
        CombatSafetyNet.Initialize();
        RegisterAllyTurnProviders();
        SavedPropertiesTypeCacheCompat.InjectModSavedPropertyTypes();
        SpecialGuestAutoRegistrar.Initialize();

        RegisterRuntimeCardPools();
        IReadOnlyList<string> failedPatchClasses = ApplyGameplayPatches(harmony);
        try
        {
            LibraryCursorPatch.ApplyToCurrentGame();
        }
        catch (Exception e)
        {
            Log.Error("[LibraryOfRuina] Failed to apply the Library cursor: " + e);
        }

        if (failedPatchClasses.Count == 0)
        {
            Log.Info("LibraryOfRuina loaded successfully.");
        }
        else
        {
            Log.Error(
                "LibraryOfRuina loaded with "
                + failedPatchClasses.Count
                + " failed Harmony patch class(es): "
                + string.Join(", ", failedPatchClasses));
            Log.Warn("LibraryOfRuina will continue with partial initialization.");
        }

        TryApplyOptionalPatches();
    }

    // 逐个补丁类应用，等价于 Harmony.PatchAll 的遍历顺序。单个补丁类失败（例如 Android 的 Mono 运行时
    // 无法为某些方法生成替换体）时只记录并跳过该类，其余补丁照常生效，避免整个模组停在部分初始化。
    private static IReadOnlyList<string> ApplyGameplayPatches(Harmony harmony)
    {
        var failedPatchClasses = new List<string>();
        foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
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
                    "[LibraryOfRuina] Harmony patch class "
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
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            var attr = type.GetCustomAttribute<CardPoolAttribute>();
            if (attr != null)
            {
                ModHelper.AddModelToPool(attr.PoolType, type);
            }
        }
    }

    private static void TryApplyOptionalPatches()
    {
        var harmony = new Harmony("FYY.LibraryOfRuina");
        try
        {
            harmony.CreateClassProcessor(typeof(FocusOfAttentionCardCmdAutoPlayPatch)).Patch();
        }
        catch (Exception e)
        {
            Log.Warn($"[LibraryOfRuina] Optional patch FocusOfAttentionCardCmdAutoPlayPatch skipped: {e.Message}");
        }
    }

    private static void RegisterAllyTurnProviders()
    {
        // AllyTurnRegistry.RegisterProvider(new LittleRedAllyTurnProvider());
        // AllyTurnRegistry.RegisterProvider(new WrathServantAllyTurnProvider());
        // AllyTurnRegistry.RegisterProvider(new WoodsmanTreeAllyTurnProvider());
        var providerType = typeof(IAllyTurnProvider);
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(p => providerType.IsAssignableFrom(p)
                        && p is { IsAbstract: false, IsInterface: false });
        foreach (var type in types)
        {
            var provider = (IAllyTurnProvider)Activator.CreateInstance(type)!;
            AllyTurnRegistry.RegisterProvider(provider);
        }
    }
}
