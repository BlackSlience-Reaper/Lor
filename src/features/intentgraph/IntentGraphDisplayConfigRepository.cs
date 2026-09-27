using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Logging;
using FileAccess = Godot.FileAccess;

namespace LibraryOfRuina.features.intentgraph;

internal static class IntentGraphDisplayConfigRepository
{
    private const string ConfigPathInPack = "res://LibraryOfRuina/config/intentgraph_display.jsonc";
    private const string LegacyConfigPathInPack = "res://LibraryOfRuina/config/intentgraph_display.json";
    private const string ConfigPathInUser = "user://LibraryOfRuina/config/intentgraph_display.jsonc";
    private const string LegacyConfigPathInUser = "user://LibraryOfRuina/config/intentgraph_display.json";
    private const string ConfigPathInModsRelative = "config/intentgraph_display.jsonc";
    private const string LegacyConfigPathInModsRelative = "config/intentgraph_display.json";
    private const string DefaultConfigTemplate = "{\n  \"enabled\": false\n}\n";
    private const string LegacyDefaultEnabledTemplate = "{\n  \"enabled\": true\n}\n";

    private static readonly object LockObj = new object();
    private static bool _loaded;
    private static bool _editableConfigEnsured;
    private static bool _lastLoggedEnabled;

    private static bool _enabled;

    public static bool IsEnabled
    {
        get
        {
            bool settingVal = LibraryOfRuinaSettings.IntentGraphEnabled;
            if (!settingVal)
            {
                return false;
            }

            lock (LockObj)
            {
                EnsureLoadedLocked();
                return _enabled;
            }
        }
    }

    public static void Initialize()
    {
        lock (LockObj)
        {
            EnsureEditableConfigExistsLocked();
            ReloadLocked();
        }
    }

    private static void EnsureLoadedLocked()
    {
        if (_loaded)
        {
            return;
        }

        ReloadLocked();
    }

    private static void ReloadLocked()
    {
        _loaded = true;
        _enabled = false;

        string? configPath = ResolveConfigPathLocked();
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return;
        }

        if (!TryReadTextFromPath(configPath, out string? jsonText) || string.IsNullOrWhiteSpace(jsonText))
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonText, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("enabled", out JsonElement enabled)
                && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                _enabled = enabled.GetBoolean();
            }

            if (!_lastLoggedEnabled || !_enabled)
            {
                _lastLoggedEnabled = _enabled;
                Log.Info("[IntentGraph] Display switch loaded: enabled=" + _enabled + " from " + FormatPathForLog(configPath));
            }
        }
        catch (Exception exception)
        {
            Log.Warn("[IntentGraph] Failed to parse display config from " + configPath + ": " + exception.Message);
        }
    }

    private static string? ResolveConfigPathLocked()
    {
        string? modsConfigPath = ResolveModsConfigAbsolutePathLocked();
        string? legacyModsConfigPath = ResolveLegacyModsConfigAbsolutePathLocked();
        TryMigrateLegacyConfigFile(legacyModsConfigPath, modsConfigPath);
        TryMigrateLegacyEnabledTrueTemplate(modsConfigPath);

        if (!string.IsNullOrWhiteSpace(modsConfigPath) && File.Exists(modsConfigPath))
        {
            return modsConfigPath;
        }

        if (!string.IsNullOrWhiteSpace(legacyModsConfigPath) && File.Exists(legacyModsConfigPath))
        {
            return legacyModsConfigPath;
        }

        if (FileAccess.FileExists(ConfigPathInUser))
        {
            return ConfigPathInUser;
        }

        if (FileAccess.FileExists(LegacyConfigPathInUser))
        {
            return LegacyConfigPathInUser;
        }

        if (FileAccess.FileExists(ConfigPathInPack))
        {
            return ConfigPathInPack;
        }

        if (FileAccess.FileExists(LegacyConfigPathInPack))
        {
            return LegacyConfigPathInPack;
        }

        return null;
    }

    private static void EnsureEditableConfigExistsLocked()
    {
        if (_editableConfigEnsured)
        {
            return;
        }

        _editableConfigEnsured = true;

        string? modsConfigPath = ResolveModsConfigAbsolutePathLocked();
        string? legacyModsConfigPath = ResolveLegacyModsConfigAbsolutePathLocked();
        TryMigrateLegacyConfigFile(legacyModsConfigPath, modsConfigPath);
        TryMigrateLegacyEnabledTrueTemplate(modsConfigPath);

        if (string.IsNullOrWhiteSpace(modsConfigPath) || File.Exists(modsConfigPath))
        {
            return;
        }

        if (TryReadTextFromPath(ConfigPathInPack, out string? defaultJson) && !string.IsNullOrWhiteSpace(defaultJson))
        {
            if (TryWriteDefaultConfig(modsConfigPath, defaultJson))
            {
                return;
            }
        }

        TryWriteDefaultConfig(modsConfigPath, DefaultConfigTemplate);
    }

    private static bool TryReadTextFromPath(string path, out string? text)
    {
        try
        {
            if (!IsGodotPath(path))
            {
                text = File.ReadAllText(path);
                return true;
            }

            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                text = null;
                return false;
            }

            text = file.GetAsText();
            return true;
        }
        catch
        {
            text = null;
            return false;
        }
    }

    private static bool TryWriteDefaultConfig(string absolutePath, string content)
    {
        try
        {
            string? parent = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(absolutePath, content, Encoding.UTF8);
            Log.Info("[IntentGraph] Generated editable display config at: " + absolutePath);
            return true;
        }
        catch (Exception exception)
        {
            Log.Warn("[IntentGraph] Failed to write default display config: " + exception.Message);
            return false;
        }
    }

    private static bool IsGodotPath(string path)
    {
        return path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveModsConfigAbsolutePathLocked()
    {
        return ResolveModsConfigAbsolutePathLocked(ConfigPathInModsRelative);
    }

    private static string? ResolveLegacyModsConfigAbsolutePathLocked()
    {
        return ResolveModsConfigAbsolutePathLocked(LegacyConfigPathInModsRelative);
    }

    private static string? ResolveModsConfigAbsolutePathLocked(string relativeConfigPath)
    {
        try
        {
            string assemblyPath = typeof(LibraryOfRuinaInitializer).Assembly.Location;
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                return null;
            }

            string? modDir = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrWhiteSpace(modDir))
            {
                return null;
            }

            string relative = relativeConfigPath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(modDir, relative);
        }
        catch
        {
            return null;
        }
    }

    private static void TryMigrateLegacyConfigFile(string? legacyPath, string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath)
            || string.IsNullOrWhiteSpace(currentPath)
            || !File.Exists(legacyPath))
        {
            return;
        }

        if (string.Equals(
                Path.GetFullPath(legacyPath),
                Path.GetFullPath(currentPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            string? parent = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            if (!File.Exists(currentPath))
            {
                File.Move(legacyPath, currentPath);
            }
            else
            {
                File.Delete(legacyPath);
            }
        }
        catch (Exception exception)
        {
            Log.Warn("[IntentGraph] Failed to migrate legacy config file: " + exception.Message);
        }
    }

    private static void TryMigrateLegacyEnabledTrueTemplate(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return;
        }

        if (!TryReadTextFromPath(configPath, out string? currentText) || string.IsNullOrWhiteSpace(currentText))
        {
            return;
        }

        if (!IsLegacyEnabledTrueTemplate(currentText))
        {
            return;
        }

        try
        {
            File.WriteAllText(configPath, DefaultConfigTemplate, Encoding.UTF8);
            Log.Info("[IntentGraph] Migrated legacy default display config to enabled=false at: " + configPath);
        }
        catch (Exception exception)
        {
            Log.Warn("[IntentGraph] Failed to migrate legacy default display config: " + exception.Message);
        }
    }

    private static bool IsLegacyEnabledTrueTemplate(string text)
    {
        string normalizedCurrent = NormalizeTemplateText(text);
        string normalizedLegacy = NormalizeTemplateText(LegacyDefaultEnabledTemplate);
        return string.Equals(normalizedCurrent, normalizedLegacy, StringComparison.Ordinal);
    }

    private static string NormalizeTemplateText(string text)
    {
        if (!string.IsNullOrEmpty(text) && text[0] == '\uFEFF')
        {
            text = text.Substring(1);
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
    }

    private static string FormatPathForLog(string path)
    {
        if (!IsGodotPath(path))
        {
            return path;
        }

        string globalPath = ProjectSettings.GlobalizePath(path);
        return path + " (" + globalPath + ")";
    }
}
