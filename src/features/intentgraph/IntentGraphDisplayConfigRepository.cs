using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Logging;
using FileAccess = Godot.FileAccess;

namespace LibraryOfRuina.features.intentgraph;

/// <summary>
/// 意图图的显示开关。玩家可编辑的副本只放在 <c>user://LibraryOfRuina/config/</c>：
/// 模组安装目录可能只读，创意工坊更新时也会被覆盖或校验，所以这里不写入安装目录。
/// </summary>
internal static class IntentGraphDisplayConfigRepository
{
    private const string ConfigPathInPack = "res://LibraryOfRuina/config/intentgraph_display.jsonc";
    private const string LegacyConfigPathInPack = "res://LibraryOfRuina/config/intentgraph_display.json";
    private const string ConfigPathInUser = "user://LibraryOfRuina/config/intentgraph_display.jsonc";
    private const string LegacyConfigPathInUser = "user://LibraryOfRuina/config/intentgraph_display.json";
    // 旧版把可编辑副本生成在模组安装目录；首次运行时复制到 user://，原文件不动。
    private const string LegacyConfigPathInModsRelative = "config/intentgraph_display.jsonc";
    private const string OlderConfigPathInModsRelative = "config/intentgraph_display.json";
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
        foreach (string path in new[] { ConfigPathInUser, LegacyConfigPathInUser, ConfigPathInPack, LegacyConfigPathInPack })
        {
            if (FileAccess.FileExists(path))
            {
                return path;
            }
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
        if (FileAccess.FileExists(ConfigPathInUser) || FileAccess.FileExists(LegacyConfigPathInUser))
        {
            return;
        }

        string content = DefaultConfigTemplate;
        if (TryReadLegacyModsConfig(out string? legacyText))
        {
            // 旧版的 enabled=true 默认模板已改为默认关闭；玩家手改过的内容原样保留。
            content = IsLegacyEnabledTrueTemplate(legacyText) ? DefaultConfigTemplate : legacyText;
        }
        else if (TryReadTextFromPath(ConfigPathInPack, out string? packText) && !string.IsNullOrWhiteSpace(packText))
        {
            content = packText;
        }

        TryWriteUserConfig(content);
    }

    private static bool TryReadLegacyModsConfig(out string text)
    {
        foreach (string relative in new[] { LegacyConfigPathInModsRelative, OlderConfigPathInModsRelative })
        {
            string? path = ResolveModsDirectoryPath(relative);
            if (path != null
                && File.Exists(path)
                && TryReadTextFromPath(path, out string? content)
                && !string.IsNullOrWhiteSpace(content))
            {
                text = content;
                Log.Info("[IntentGraph] Copying display config from the mod directory to " + ConfigPathInUser + ": " + path);
                return true;
            }
        }

        text = "";
        return false;
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

    private static void TryWriteUserConfig(string content)
    {
        string absolutePath = ProjectSettings.GlobalizePath(ConfigPathInUser);
        try
        {
            string? parent = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            string temporaryPath = absolutePath + ".tmp";
            File.WriteAllText(temporaryPath, content, Encoding.UTF8);
            File.Move(temporaryPath, absolutePath, overwrite: true);
            Log.Info("[IntentGraph] Generated editable display config at: " + absolutePath);
        }
        catch (Exception exception)
        {
            Log.Warn("[IntentGraph] Failed to write default display config: " + exception.Message);
        }
    }

    private static bool IsGodotPath(string path)
    {
        return path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveModsDirectoryPath(string relativeConfigPath)
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

    private static bool IsLegacyEnabledTrueTemplate(string text)
    {
        string normalizedCurrent = NormalizeTemplateText(text);
        string normalizedLegacy = NormalizeTemplateText(LegacyDefaultEnabledTemplate);
        return string.Equals(normalizedCurrent, normalizedLegacy, StringComparison.Ordinal);
    }

    private static string NormalizeTemplateText(string text)
    {
        if (!string.IsNullOrEmpty(text) && text[0] == '﻿')
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
