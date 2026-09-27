using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.addons.mega_text;
using LibraryOfRuina.features.settings.ui;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace LibraryOfRuina.features.settings;

internal abstract partial class ExtModSettings
{
    private const string SettingsTheme = "res://themes/settings_screen_line_header.tres";

    public event EventHandler? ConfigChanged;

    public event Action? OnConfigReloaded;

    public void ConfigReloaded() => OnConfigReloaded?.Invoke();

    private readonly string _path;

    public string ModPrefix { get; private set; }

    [SettingsIgnore] public string? ModId { get; set; }

    private readonly string _configName;
    private bool _savingDisabled;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private CancellationTokenSource? _saveDebounceToken;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly HashSet<string> _lastMissingProperties = [];

    protected readonly List<PropertyInfo> ConfigProperties = [];
    private readonly Dictionary<string, object?> _defaultValues = new();

    public static class SettingsLogger
    {
        public static List<string> PendingUserMessages { get; } = [];

        public static void Warn(string message, bool showInGui = false)
        {
            Log.Warn(message);
            if (showInGui && !PendingUserMessages.Contains(message))
                PendingUserMessages.Add(message);
        }

        public static void Error(string message, bool showInGui = true)
        {
            Log.Error(message);
            if (showInGui && !PendingUserMessages.Contains(message))
                PendingUserMessages.Add(message);
        }
    }

    public ExtModSettings(string? filename = null)
    {
        ModPrefix = GetModPrefix(GetType());
        ModId = null;
        _configName = GetType().FullName ?? "unknown";
        var rootNs = GetRootNamespace(GetType());

        if (string.IsNullOrEmpty(rootNs) && string.IsNullOrEmpty(filename))
        {
            var message = $"Cannot determine a safe configuration file path for {_configName}. " +
                          "Place your configuration class inside a namespace, or provide a filename.";
            SettingsLogger.Error(message);
            throw new InvalidOperationException(message);
        }

        var defaultFilename = SpecialCharRegex().Replace(rootNs, "");
        filename = filename == null ? defaultFilename : SpecialCharRegex().Replace(filename, "");
        if (!filename.Contains('.')) filename += ".cfg";

        _path = Path.Combine(OS.GetUserDataDir(), "mod_configs", filename);

        CheckConfigProperties();
        Init();
    }

    public bool HasSettings() => ConfigProperties.Count > 0;

    public bool HasVisibleSettings() =>
        ConfigProperties.Any(p => p.GetCustomAttribute<SettingsHideInUI>() == null);

    private void CheckConfigProperties()
    {
        ConfigProperties.Clear();
        foreach (var property in GetType().GetProperties())
        {
            if (property.GetCustomAttribute<SettingsIgnoreAttribute>() != null) continue;
            if (!property.CanRead || !property.CanWrite) continue;
            if (property.GetMethod?.IsStatic != true)
            {
                SettingsLogger.Warn($"Ignoring {_configName} property {property.Name}: only static properties are supported");
                continue;
            }
            ConfigProperties.Add(property);
        }
    }

    public T? GetDefaultValue<T>(string propertyName)
    {
        if (_defaultValues.TryGetValue(propertyName, out var val) && val is T typedValue)
            return typedValue;
        return default;
    }

    protected bool WasConfigPropertyMissingOnLoad(string propertyName) =>
        _lastMissingProperties.Contains(propertyName);

    protected void RestoreDefaultsNoConfirm()
    {
        RestoreDefaultValues();
        Save();
        OnConfigReloaded?.Invoke();
    }

    private void RestoreDefaultValues()
    {
        foreach (var property in ConfigProperties)
        {
            var defaultValue = GetDefaultValue<object?>(property.Name);
            property.SetValue(null, defaultValue);
        }
    }

    public abstract void SetupConfigUI(Control optionContainer);

    private void Init()
    {
        foreach (var property in ConfigProperties)
            _defaultValues.TryAdd(property.Name, property.GetValue(null));

        if (File.Exists(_path)) Load();
        else Save();
    }

    public void Changed()
    {
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public static void Load<T>() where T : ExtModSettings
    {
        ExtSettingsRegistry.Get<T>()?.Load();
    }

    public static void SaveDebounced<T>(int delayMs = 1000) where T : ExtModSettings
    {
        ExtSettingsRegistry.Get<T>()?.SaveDebounced(delayMs);
    }

    public void SaveDebounced(int delayMs = 1000) => SaveDebouncedInternal(delayMs);

    private async void SaveDebouncedInternal(int delayMs = 1000)
    {
        try
        {
            _saveDebounceToken?.Cancel();
            _saveDebounceToken?.Dispose();
            _saveDebounceToken = new CancellationTokenSource();
            var token = _saveDebounceToken.Token;
            await Task.Delay(delayMs, token);
            await _saveLock.WaitAsync(token);
            try { Save(); }
            finally { _saveLock.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SettingsLogger.Error($"Failed to save config for {_configName}: {ex.Message}");
        }
    }

    public void Save()
    {
        if (_savingDisabled)
        {
            SettingsLogger.Warn($"Skipping save for {_configName} (corrupted/read-only state).");
            return;
        }

        Dictionary<string, string> values = [];
        try
        {
            foreach (var property in ConfigProperties)
            {
                var value = property.GetValue(null);
                var converter = TypeDescriptor.GetConverter(property.PropertyType);
                var stringValue = converter.ConvertToInvariantString(value);
                if (stringValue != null)
                    values.Add(property.Name, stringValue);
                else
                    SettingsLogger.Warn($"Failed to convert {_configName}.{property.Name} to string; omitted.");
            }
        }
        catch (Exception)
        {
            SettingsLogger.Error($"Failed to save config {_configName}: conversion error.", false);
            return;
        }

        try
        {
            new FileInfo(_path).Directory?.Create();
            using var fs = File.Create(_path);
            JsonSerializer.Serialize(fs, values, JsonOptions);
        }
        catch (Exception e)
        {
            SettingsLogger.Error($"Failed to save config {_configName}: {e.Message}");
        }
    }

    public void Load()
    {
        if (!File.Exists(_path))
        {
            SettingsLogger.Error($"Load for {_configName} failed. File not found: {_path}");
            return;
        }

        var hasSoftErrors = false;
        _savingDisabled = false;
        _lastMissingProperties.Clear();

        try
        {
            Dictionary<string, string>? values;
            using (var fs = File.OpenRead(_path))
                values = JsonSerializer.Deserialize<Dictionary<string, string>>(fs);

            if (values == null)
            {
                SettingsLogger.Warn($"Config file {_configName} was empty. Re-saving defaults.");
                hasSoftErrors = true;
            }
            else
            {
                foreach (var property in ConfigProperties)
                {
                    if (!values.TryGetValue(property.Name, out var value))
                    {
                        SettingsLogger.Warn($"Config {_configName} missing {property.Name}; will re-save.");
                        _lastMissingProperties.Add(property.Name);
                        hasSoftErrors = true;
                        continue;
                    }
                    if (!TryApplyPropertyValue(property, value)) hasSoftErrors = true;
                }

                if (hasSoftErrors)
                    SettingsLogger.Warn($"Loaded config {_configName} with some missing/invalid fields.");
            }
        }
        catch (JsonException jsonEx)
        {
            var locationText = jsonEx.LineNumber.HasValue
                ? $"Line {jsonEx.LineNumber + 1}, position {jsonEx.BytePositionInLine + 1}"
                : "unknown line";
            if (TryMoveInvalidConfigAside(locationText))
            {
                RestoreDefaultValues();
                Save();
                SettingsLogger.Warn($"Invalid config for {_configName} was replaced with defaults.\nPath: {_path}\nError: {locationText}");
                return;
            }

            SettingsLogger.Error($"Failed to parse config for {_configName}. Invalid JSON.\nPath: {_path}\nError: {locationText}");
            SettingsLogger.Warn("Config saving DISABLED for this mod to protect manual edits.", true);
            _savingDisabled = true;
            return;
        }
        catch (Exception e)
        {
            SettingsLogger.Error($"Unexpected error loading config {_configName}: {e.Message}");
            return;
        }

        if (hasSoftErrors && !_savingDisabled)
        {
            SettingsLogger.Warn($"Saving fresh config for {_configName} to correct soft errors.");
            Save();
        }
    }

    private static bool TryApplyPropertyValue(PropertyInfo property, string value)
    {
        try
        {
            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            var configVal = converter.ConvertFromInvariantString(value);
            if (configVal == null)
            {
                SettingsLogger.Warn($"Converter returned null for {property.Name} value \"{value}\".");
                return false;
            }
            var oldVal = property.GetValue(null);
            if (!configVal.Equals(oldVal))
                property.SetValue(null, configVal);
            return true;
        }
        catch (Exception ex)
        {
            SettingsLogger.Warn($"Failed to load value \"{value}\" for {property.Name}: {ex.Message}");
            return false;
        }
    }

    private bool TryMoveInvalidConfigAside(string locationText)
    {
        try
        {
            var backupPath = GetInvalidConfigBackupPath();
            File.Move(_path, backupPath);
            SettingsLogger.Warn($"Moved invalid config for {_configName} to {backupPath} ({locationText}).");
            return true;
        }
        catch (Exception ex)
        {
            SettingsLogger.Error($"Failed to move invalid config for {_configName}: {ex.Message}", false);
            return false;
        }
    }

    private string GetInvalidConfigBackupPath()
    {
        var directory = Path.GetDirectoryName(_path);
        var fileName = Path.GetFileName(_path);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(directory ?? "", $"{fileName}.invalid-{stamp}.bak");
        var suffix = 1;

        while (File.Exists(backupPath))
        {
            backupPath = Path.Combine(directory ?? "", $"{fileName}.invalid-{stamp}-{suffix}.bak");
            suffix++;
        }

        return backupPath;
    }

    protected string GetLabelText(string labelName)
    {
        var loc = LocString.GetIfExists("settings_ui", $"{ModPrefix}{StringHelper.Slugify(labelName)}.title");
        return loc != null ? loc.GetFormattedText() : labelName;
    }

    protected NExtToggle CreateRawTickboxControl(PropertyInfo property)
    {
        var tickbox = new NExtToggle();
        tickbox.Initialize(this, property);
        return tickbox;
    }

    protected NExtSlider CreateRawSliderControl(PropertyInfo property)
    {
        var slider = new NExtSlider();
        slider.Initialize(this, property);
        return slider;
    }

    protected NExtTextInput CreateRawLineEditControl(PropertyInfo property)
    {
        var lineEdit = new NExtTextInput();
        lineEdit.Initialize(this, property);
        return lineEdit;
    }

    protected NExtActionButton CreateRawButtonControl(string labelText, Action onPressed)
    {
        var button = new NExtActionButton();
        button.Initialize(labelText, onPressed);
        return button;
    }

    protected NDropdownPositioner CreateRawDropdownControl(PropertyInfo property)
    {
        var dropdown = new NExtDropdown();
        dropdown.Initialize(this, property, ModPrefix, Changed);
        dropdown.SetFromProperty();

        var positioner = new NDropdownPositioner();
        positioner.SetCustomMinimumSize(new Vector2(324, 64));
        positioner.FocusMode = Control.FocusModeEnum.All;
        positioner.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        positioner.SizeFlagsVertical = Control.SizeFlags.Fill;

        var dropdownNodeField = typeof(NDropdownPositioner).GetField("_dropdownNode",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        dropdownNodeField?.SetValue(positioner, dropdown);

        positioner.AddChild(dropdown);
        positioner.MouseFilter = Control.MouseFilterEnum.Ignore;

        return positioner;
    }

    public static MegaRichTextLabel CreateRawLabelControl(string labelText, int fontSize)
    {
        var kreonNormal = PreloadManager.Cache.GetAsset<Font>("res://themes/kreon_regular_shared.tres");
        var kreonBold = PreloadManager.Cache.GetAsset<Font>("res://themes/kreon_bold_shared.tres");

        MegaRichTextLabel label = new()
        {
            Name = "Label",
            Theme = PreloadManager.Cache.GetAsset<Theme>(SettingsTheme),
            AutoSizeEnabled = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None,
            BbcodeEnabled = true,
            ScrollActive = false,
            VerticalAlignment = VerticalAlignment.Center,
            Text = labelText
        };

        label.AddThemeFontOverride("normal_font", kreonNormal);
        label.AddThemeFontOverride("bold_font", kreonBold);
        AddThemeFontSizeOverrideAll(label, fontSize);

        return label;
    }

    protected static ColorRect CreateDividerControl()
    {
        return new ColorRect
        {
            Name = "Divider",
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(0.909804f, 0.862745f, 0.745098f, 0.25098f)
        };
    }

    public static void ShowAndClearPendingErrors()
    {
        var pendingMessages = SettingsLogger.PendingUserMessages;
        if (pendingMessages.Count <= 0) return;

        var errorPopup = NErrorPopup.Create("Mod configuration error",
            string.Join('\n', pendingMessages), false);
        if (errorPopup == null || NModalContainer.Instance == null) return;
        NModalContainer.Instance.Add(errorPopup);

        pendingMessages.Clear();
    }

    public static string GetModPrefix(Type t)
    {
        if (t.Namespace == null) return "";
        var dotIndex = t.Namespace.IndexOf('.');
        if (dotIndex == -1) dotIndex = t.Namespace.Length;
        return $"{t.Namespace[..dotIndex].ToUpperInvariant()}-";
    }

    public static string GetRootNamespace(Type t)
    {
        if (t.Namespace == null) return "";
        var dotIndex = t.Namespace.IndexOf('.');
        if (dotIndex == -1) dotIndex = t.Namespace.Length;
        return t.Namespace[..dotIndex];
    }

    public static void AddThemeFontSizeOverrideAll(Control control, int fontSize)
    {
        string[] fontTypes = [
            "font_size",
            "normal_font_size", "bold_font_size", "italics_font_size",
            "bold_italics_font_size", "mono_font_size"
        ];
        foreach (var fontType in fontTypes)
            control.AddThemeFontSizeOverride(fontType, fontSize);
    }

    [GeneratedRegex("[^a-zA-Z0-9_.]")]
    private static partial Regex SpecialCharRegex();
}
