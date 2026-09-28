using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.ftue;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.settings;

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
    private static bool _monsterExtensionActive = true;
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
        FieldInfo? pathsField = typeof(NPatchNotesScreen).GetField(
            "_patchNotePaths",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo? indexField = typeof(NPatchNotesScreen).GetField(
            "_index",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (pathsField == null || indexField == null)
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

        pathsField.SetValue(patchNotesScreen, patchNotePaths);
        indexField.SetValue(patchNotesScreen, updateLogIndex);
        patchNotesScreen.GetNode<NButton>("PrevButton").Visible = updateLogIndex < patchNotePaths.Count - 1;
        patchNotesScreen.GetNode<NButton>("NextButton").Visible = updateLogIndex > 0;

        while (mainMenu.SubmenuStack.SubmenusOpen)
            mainMenu.SubmenuStack.Pop();

        Callable.From(patchNotesScreen.Open).CallDeferred();
    }

    [SettingsSection("Resistance")]
    [SliderRange(1, 3)]
    [SliderValueLabels("ignore", "weak", "normal")]
    public static double ResistanceMode
    {
        get => _resistanceMode;
        set
        {
            _resistanceMode = Math.Clamp(value, 1d, 3d);
            Log.Info($"[LibraryOfRuina.Settings] ResistanceMode set to {_resistanceMode:0}.");
            ApplyRunScopedSettingsIfIdle();
        }
    }

    internal static bool RuntimeSideEffectsEnabled => _runtimeSideEffectsEnabled && MonsterExtensionActive;

    /// <summary>
    /// The monster-extension switch gameplay code reads. It follows <see cref="MonsterExtensionEnabled"/>
    /// only between runs: changing a gameplay setting mid-run used to rewrite the current run's rooms
    /// and flip dozens of gates on one client only (design philosophy §4).
    /// </summary>
    [SettingsIgnore]
    internal static bool MonsterExtensionActive => _monsterExtensionActive;

    private static void ApplyRunScopedSettingsIfIdle()
    {
        if (RunManager.Instance?.IsInProgress == true)
        {
            Log.Info("[LibraryOfRuina.Settings] run in progress; gameplay settings apply from the next run.");
            return;
        }

        ApplyRunScopedSettings();
    }

    /// <summary>Applies configured gameplay settings. Called outside runs and on run cleanup.</summary>
    internal static void ApplyRunScopedSettings()
    {
        LibraryResistanceModeState.Current = _resistanceMode switch
        {
            1d => LibraryResistanceMode.Ignore,
            2d => LibraryResistanceMode.Weak,
            _ => LibraryResistanceMode.Normal
        };

        if (_monsterExtensionActive == _monsterExtensionEnabled)
        {
            return;
        }

        _monsterExtensionActive = _monsterExtensionEnabled;
        Log.Info($"[LibraryOfRuina.Settings] MonsterExtension active={_monsterExtensionActive}.");
        if (!_monsterExtensionActive)
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

    internal static void EnableRuntimeSideEffects()
    {
        _runtimeSideEffectsEnabled = true;
    }

    [SettingsSection("MonsterExtension")]
    public static bool MonsterExtensionEnabled
    {
        get => _monsterExtensionEnabled;
        set
        {
            if (_monsterExtensionEnabled == value)
            {
                return;
            }

            _monsterExtensionEnabled = value;
            Log.Info($"[LibraryOfRuina.Settings] MonsterExtensionEnabled set to {value}.");
            ApplyRunScopedSettingsIfIdle();
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
    public static bool StringTheocracyRunBgmDefaultApplied { get; set; } = true;

    [SettingsSection("MoonText")]
    public static bool MoonTextEnabled { get; set; } = false;

    [SettingsSection("FtueTutorial")]
    public static bool FtueTutorialEnabled { get; set; } = true;

    [SettingsHideInUI]
    public static string FtueTutorialShownIds { get; set; } = "";

    [SettingsHideInUI]
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

    [SettingsIgnore]
    public static double InternalCounter { get; set; }

    [SettingsHideInUI]
    public static string CachedToken { get; set; } = "";

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
