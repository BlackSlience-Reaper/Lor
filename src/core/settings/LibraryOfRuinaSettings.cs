using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using LibraryOfRuina.features.ftue;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.core.settings;

public enum DisplayDensity
{
    Compact,
    Normal,
    Spacious
}

internal sealed class LibraryOfRuinaSettings : ExtAutoModSettings
{
    private const string PatchNotesDirectory = "res://localization/eng/patch_notes";
    private const string CurrentUpdateLogPatchNotePath =
        PatchNotesDirectory + "/2026_09_11.md";

    private static bool _mainMenuBgmEnabled = true;
    private static bool _nonCombatRunBgmEnabled = true;
    // 主界面 BGM 的默认音量，保留原有混音比例。
    private static double _mainMenuBgmVolume = 0.6d;
    // 战斗外 BGM 的默认音量，保留原有混音比例。
    private static double _nonCombatRunBgmVolume = 0.6d;
    private static bool _modSfxEnabled = true;
    private static double _modSfxVolume = 0.25d;
    private static bool _runtimeSideEffectsEnabled;
    private static bool _monsterExtensionEnabled = true;
    private static double _resistanceMode = 3d;

    [SettingsSection("UpdateLog")]
    [SettingsButton("ViewUpdateLog")]
    private static void LatestUpdateLog()
    {
        NMainMenu? mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu == null)
        {
            SettingsLogger.Error("[LibraryOfRuina] Main menu is unavailable; cannot open the update log.");
            return;
        }

        if (!Godot.FileAccess.FileExists(CurrentUpdateLogPatchNotePath))
        {
            SettingsLogger.Error(
                $"[LibraryOfRuina] Update log resource is missing: {CurrentUpdateLogPatchNotePath}");
            return;
        }

        NPatchNotesScreen patchNotesScreen = mainMenu.PatchNotesScreen;
        if (!VanillaPrivate.PatchNotesScreenPatchNotePaths.IsAvailable || !VanillaPrivate.PatchNotesScreenIndex.IsAvailable)
        {
            SettingsLogger.Error("[LibraryOfRuina] The native update-log screen fields could not be found.");
            return;
        }

        List<string> patchNotePaths = DirAccess.GetFilesAt(PatchNotesDirectory)
            .Select(fileName => PatchNotesDirectory + "/" + fileName)
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .ToList();
        int updateLogIndex = patchNotePaths.FindIndex(path =>
            string.Equals(path, CurrentUpdateLogPatchNotePath, StringComparison.Ordinal));
        if (updateLogIndex < 0)
        {
            SettingsLogger.Error(
                $"[LibraryOfRuina] Update log resource was not listed: {CurrentUpdateLogPatchNotePath}");
            return;
        }

        VanillaPrivate.PatchNotesScreenPatchNotePaths.Set(patchNotesScreen, patchNotePaths);
        VanillaPrivate.PatchNotesScreenIndex.Set(patchNotesScreen, updateLogIndex);
        patchNotesScreen.GetNode<NButton>("PrevButton").Visible = updateLogIndex < patchNotePaths.Count - 1;
        patchNotesScreen.GetNode<NButton>("NextButton").Visible = updateLogIndex > 0;

        while (mainMenu.SubmenuStack.SubmenusOpen)
            mainMenu.SubmenuStack.Pop();

        Callable.From(patchNotesScreen.Open).CallDeferred();
    }

    // 局内的抗性由本局的 LibraryRunSettingsModifier 决定；这里只在局外改写基础库的静态值，
    // 让主菜单、图鉴里的倍率说明跟随本地设置，离开本局时由 LibraryRunSettings 复位回这个值。
    [SettingsSection("Resistance")]
    [SliderRange(1, 3)]
    [SliderValueLabels("ignore", "weak", "normal")]
    [SettingsLockedDuringRun]
    public static double ResistanceMode
    {
        get => _resistanceMode;
        set
        {
            double clamped = Math.Clamp(value, 1d, 3d);
            if (clamped == _resistanceMode || RejectDuringRun(nameof(ResistanceMode)))
            {
                return;
            }

            _resistanceMode = clamped;
            LibraryResistanceModeState.Current = ToLibraryResistanceMode(ResistanceSettingLevel);
            Log.Info($"[LibraryOfRuina.Settings] ResistanceMode changed to {_resistanceMode:0} ({LibraryResistanceModeState.Current}).");
        }
    }

    internal const int NormalResistanceSetting = 3;

    /// <summary>本地抗性设置映射到 1–3 的整数；只有恰好 1 或 2 才是无视或弱抗性，其余一律正常。</summary>
    internal static int ResistanceSettingLevel => _resistanceMode switch
    {
        1d => 1,
        2d => 2,
        _ => NormalResistanceSetting
    };

    internal static LibraryResistanceMode ToLibraryResistanceMode(int level) => level switch
    {
        1 => LibraryResistanceMode.Ignore,
        2 => LibraryResistanceMode.Weak,
        _ => LibraryResistanceMode.Normal
    };

    // 局内以本局是否有图书馆内容为准（联机时是房主开局时的设置），局外读本地设置。BGM 控制器用它决定是否接管音乐。
    internal static bool RuntimeSideEffectsEnabled => _runtimeSideEffectsEnabled && LibraryRunSettings.MonsterExtensionEnabled;

    // 这两项玩法设置在局内不能改（暂停菜单里能打开设置界面）：局内的值已由本局的 LibraryRunSettingsModifier 固定，
    // 本地设置只影响之后新开的局，局内改了也不会生效。所以这两行在局内不显示，setter 也拒绝“恢复默认”之类的写入，
    // 免得玩家以为改动作用于当前这局。
    private static bool RejectDuringRun(string setting)
    {
        if (!RunManager.Instance.IsInProgress)
        {
            return false;
        }

        Log.Warn($"[LibraryOfRuina.Settings] {setting} cannot change while a run is in progress; kept current value.");
        return true;
    }

    internal static void EnableRuntimeSideEffects()
    {
        _runtimeSideEffectsEnabled = true;
    }

    /// <summary>本次启动是否注入了内容（初始化越过了“关闭内容 / 不兼容模组”分支）；进程内不再改变。</summary>
    internal static bool ContentInjected => _runtimeSideEffectsEnabled;

    [SettingsSection("MonsterExtension")]
    [SettingsLockedDuringRun]
    public static bool MonsterExtensionEnabled
    {
        get => _monsterExtensionEnabled;
        set
        {
            if (_monsterExtensionEnabled == value || RejectDuringRun(nameof(MonsterExtensionEnabled)))
            {
                return;
            }

            _monsterExtensionEnabled = value;
            Log.Info($"[LibraryOfRuina.Settings] MonsterExtensionEnabled changed to {value}.");
            if (!value)
            {
                if (_runtimeSideEffectsEnabled)
                {
                    MainMenuBgmController.ForceStop();
                    NonCombatRunBgmController.OnSettingChanged();
                    EncounterBgmController.StopRuntimeSession();
                    AbnormalityEliteBgmController.StopRuntimeSession();
                }
            }
            else if (RuntimeSideEffectsEnabled)
            {
                MainMenuBgmController.OnSettingChanged();
                NonCombatRunBgmController.OnSettingChanged();
            }
        }
    }

    [SettingsSection("MonsterExtension")]
    public static bool VanillaCharacterProtectionEnabled { get; set; } = true;

    [SettingsSection("Audio")]
    public static bool AudioSettingsExpanded { get; set; } = false;

    [SettingsSection("Audio")]
    [SettingsVisibleWhen(nameof(AudioSettingsExpanded), true)]
    public static bool MainMenuBgmEnabled
    {
        get => _mainMenuBgmEnabled;
        set
        {
            if (_mainMenuBgmEnabled == value)
            {
                return;
            }

            _mainMenuBgmEnabled = value;
            if (RuntimeSideEffectsEnabled)
            {
                MainMenuBgmController.OnSettingChanged();
            }
        }
    }

    [SettingsSection("Audio")]
    [SettingsVisibleWhen(nameof(AudioSettingsExpanded), true)]
    [SliderRange(0d, 1d, 0.05d)]
    [SliderLabelFormat("{0:P0}")]
    public static double MainMenuBgmVolume
    {
        get => _mainMenuBgmVolume;
        set
        {
            _mainMenuBgmVolume = Math.Clamp(value, 0d, 1d);
            if (RuntimeSideEffectsEnabled)
            {
                MainMenuBgmController.RefreshVolumeFromSettings();
            }
        }
    }

    [SettingsSection("Audio")]
    [SettingsVisibleWhen(nameof(AudioSettingsExpanded), true)]
    public static bool NonCombatRunBgmEnabled
    {
        get => _nonCombatRunBgmEnabled;
        set
        {
            if (_nonCombatRunBgmEnabled == value)
            {
                return;
            }

            _nonCombatRunBgmEnabled = value;
            if (RuntimeSideEffectsEnabled)
            {
                NonCombatRunBgmController.OnSettingChanged();
            }
        }
    }

    [SettingsSection("Audio")]
    [SettingsVisibleWhen(nameof(AudioSettingsExpanded), true)]
    [SliderRange(0d, 1d, 0.05d)]
    [SliderLabelFormat("{0:P0}")]
    public static double NonCombatRunBgmVolume
    {
        get => _nonCombatRunBgmVolume;
        set
        {
            _nonCombatRunBgmVolume = Math.Clamp(value, 0d, 1d);
            if (RuntimeSideEffectsEnabled)
            {
                NonCombatRunBgmController.RefreshVolumeFromSettings();
            }
        }
    }

    [SettingsSection("Audio")]
    [SettingsVisibleWhen(nameof(AudioSettingsExpanded), true)]
    public static bool ModSfxEnabled
    {
        get => _modSfxEnabled;
        set
        {
            _modSfxEnabled = value;
            LibrarySfxMixer.RefreshMasterVolume();
        }
    }

    [SettingsSection("Audio")]
    [SettingsVisibleWhen(nameof(AudioSettingsExpanded), true)]
    [SliderRange(0d, 1d, 0.05d)]
    [SliderLabelFormat("{0:P0}")]
    public static double ModSfxVolume
    {
        get => _modSfxVolume;
        set
        {
            _modSfxVolume = Math.Clamp(value, 0d, 1d);
            LibrarySfxMixer.RefreshMasterVolume();
        }
    }

    [SettingsHideInUI]
    [SettingsKeepOnRestoreDefaults]
    public static bool StringTheocracyRunBgmDefaultApplied { get; set; } = true;

    [SettingsSection("MoonText")]
    public static bool MoonTextEnabled { get; set; } = false;

    [SettingsSection("FtueTutorial")]
    public static bool FtueTutorialEnabled { get; set; } = true;

    [SettingsHideInUI]
    [SettingsKeepOnRestoreDefaults]
    public static string FtueTutorialShownIds { get; set; } = "";

    [SettingsHideInUI]
    [SettingsKeepOnRestoreDefaults]
    public static string FtueTutorialDefaultFingerprint { get; set; } =
        LibraryOfRuinaFtueIds.CurrentUpdateLogTutorialFingerprint;

    [SettingsSection("ExperimentalContent")]
    public static bool ExperimentalContentEnabled { get; set; } = false;

    [SettingsSection("ExperimentalContent")]
    [SettingsVisibleWhen(nameof(ExperimentalContentEnabled), true)]
    public static bool SecondAscensionEnabled { get; set; } = false;

    [SettingsHideInUI]
    [SettingsSection("IntentGraph")]
    public static bool IntentGraphEnabled { get; set; } = false;

    [SettingsIgnore]
    public static bool MultiplayerScalingEnabled
    {
        get => false;
        set { }
    }

    [SettingsHideInUI]
    [SliderRange(0, 10)]
    public static int PreferredSecondAscensionLevel { get; set; } = 0;

    [SettingsHideInUI]
    [SliderRange(0.5, 2.0, 0.1)]
    [SliderLabelFormat("{0:0.0}x")]
    public static double UiScale { get; set; } = 1.0;

    [SettingsHideInUI]
    [SliderRange(0.5, 2.0, 0.1)]
    [SliderLabelFormat("{0:0.0}x")]
    public static double OverlayOpacity { get; set; } = 0.8;

    [SettingsHideInUI]
    public static DisplayDensity Density { get; set; } = DisplayDensity.Normal;

    [SettingsHideInUI]
    [SettingsTextInput(TextInputPreset.SafeDisplayName)]
    public static string PlayerTag { get; set; } = "";

    [SettingsHideInUI]
    public static bool DebugMode { get; set; }

    public LibraryOfRuinaSettings()
    {
        bool changed = TryMigrateLegacyFtueConsumedFlag();
        changed |= ApplyCurrentFtueTutorialDefaults();
        if (WasConfigPropertyMissingOnLoad(nameof(StringTheocracyRunBgmDefaultApplied)))
        {
            NonCombatRunBgmEnabled = true;
            changed = true;
        }

        if (changed)
            Save();
    }

    private bool TryMigrateLegacyFtueConsumedFlag()
    {
        if (FtueTutorialEnabled
            || !string.IsNullOrWhiteSpace(FtueTutorialShownIds)
            || !WasConfigPropertyMissingOnLoad(nameof(FtueTutorialShownIds)))
        {
            return false;
        }

        FtueTutorialEnabled = true;
        return true;
    }

    private bool ApplyCurrentFtueTutorialDefaults()
    {
        string currentFingerprint = LibraryOfRuinaFtueIds.CurrentUpdateLogTutorialFingerprint;
        if (!WasConfigPropertyMissingOnLoad(nameof(FtueTutorialDefaultFingerprint))
            && string.Equals(FtueTutorialDefaultFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        FtueTutorialEnabled = true;
        FtueTutorialDefaultFingerprint = currentFingerprint;
        Log.Info($"[LibraryOfRuina.Settings] Enabled FTUE tutorials for fingerprint {currentFingerprint}.");
        return true;
    }
}
