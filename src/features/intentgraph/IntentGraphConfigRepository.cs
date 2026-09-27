using System;
using System.Globalization;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.features.intentgraph;

internal static class IntentGraphConfigRepository
{
    private const string ConfigPath = "res://LibraryOfRuina/intentgraph/intentgraph.json";
    private const string ReleaseConfigTemplate = "res://LibraryOfRuina/intentgraph/intentgraph-{0}.json";
    private const string LocalizationTemplate = "res://LibraryOfRuina/localization/{0}/intentgraph.json";

    private static readonly object _lock = new object();
    private static readonly Dictionary<string, IntentGraphMonsterConfig> _configs =
        new Dictionary<string, IntentGraphMonsterConfig>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> _localizedText =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static bool _configLoaded;
    private static string? _localizedLanguage;

    public static IntentGraphMonsterConfig? GetConfigForMonster(string monsterTypeFullName)
    {
        lock (_lock)
        {
            EnsureConfigLoadedLocked();
            if (_configs.TryGetValue(monsterTypeFullName, out IntentGraphMonsterConfig? config))
            {
                return config;
            }

            return null;
        }
    }

    public static string? GetLocalizedText(string key)
    {
        lock (_lock)
        {
            EnsureLocalizationLoadedLocked();
            if (_localizedText.TryGetValue(key, out string? text))
            {
                return text;
            }

            return null;
        }
    }

    public static void ReloadLocalization()
    {
        lock (_lock)
        {
            _localizedLanguage = null;
            _localizedText.Clear();
        }
    }

    private static void EnsureConfigLoadedLocked()
    {
        if (_configLoaded)
        {
            return;
        }

        _configs.Clear();
        MergeConfigFromFileLocked(ConfigPath);

        string? release = ReleaseInfoManager.Instance.ReleaseInfo?.Version;
        if (!string.IsNullOrWhiteSpace(release))
        {
            string releasePath = string.Format(ReleaseConfigTemplate, release);
            MergeConfigFromFileLocked(releasePath);
        }

        _configLoaded = true;
    }

    private static void EnsureLocalizationLoadedLocked()
    {
        EnsureConfigLoadedLocked();

        string language = LocManager.Instance.Language;
        if (string.Equals(_localizedLanguage, language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _localizedText.Clear();
        MergeLocalizedTextFromFileLocked(string.Format(LocalizationTemplate, "eng"));

        if (!string.Equals(language, "eng", StringComparison.OrdinalIgnoreCase))
        {
            MergeLocalizedTextFromFileLocked(string.Format(LocalizationTemplate, language));
        }

        _localizedLanguage = language;
    }

    private static void MergeConfigFromFileLocked(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return;
        }

        try
        {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return;
            }

            string json = file.GetAsText();
            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty monsterEntry in document.RootElement.EnumerateObject())
            {
                if (monsterEntry.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                IntentGraphMonsterConfig parsed = ParseMonsterConfig(monsterEntry.Value);
                if (_configs.TryGetValue(monsterEntry.Name, out IntentGraphMonsterConfig? existing))
                {
                    existing.MergeFrom(parsed);
                }
                else
                {
                    _configs[monsterEntry.Name] = parsed;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Warn("[IntentGraph] Failed to parse config at " + path + ": " + exception.Message);
        }
    }

    private static IntentGraphMonsterConfig ParseMonsterConfig(JsonElement element)
    {
        IntentGraphMonsterConfig config = new IntentGraphMonsterConfig();

        if (TryGetProperty(element, "secondaryInitialStates", out JsonElement secondaryStates)
            && secondaryStates.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement state in secondaryStates.EnumerateArray())
            {
                if (state.ValueKind == JsonValueKind.String)
                {
                    string? stateId = state.GetString();
                    if (!string.IsNullOrWhiteSpace(stateId))
                    {
                        config.SecondaryInitialStates.Add(stateId);
                    }
                }
            }
        }

        if (TryGetProperty(element, "moveReplacements", out JsonElement moveReplacements)
            && moveReplacements.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty replacementEntry in moveReplacements.EnumerateObject())
            {
                List<IntentGraphMoveReplacement> replacements = new List<IntentGraphMoveReplacement>();
                if (replacementEntry.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement replacementValue in replacementEntry.Value.EnumerateArray())
                    {
                        if (replacementValue.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        IntentGraphMoveReplacement replacement = new IntentGraphMoveReplacement();
                        if (TryGetProperty(replacementValue, "timesText", out JsonElement timesText)
                            && timesText.ValueKind == JsonValueKind.String)
                        {
                            replacement.TimesText = timesText.GetString();
                        }

                        replacements.Add(replacement);
                    }
                }

                config.MoveReplacements[replacementEntry.Name] = replacements;
            }
        }

        if (TryGetProperty(element, "graph", out JsonElement graphElement)
            && graphElement.ValueKind == JsonValueKind.Object)
        {
            config.Graph = ParseGraphLayout(graphElement);
        }

        if (TryGetProperty(element, "graphPatch", out JsonElement graphPatchElement)
            && graphPatchElement.ValueKind == JsonValueKind.Object)
        {
            config.GraphPatch = ParseGraphLayout(graphPatchElement);
        }

        return config;
    }

    private static IntentGraphLayoutDefinition ParseGraphLayout(JsonElement element)
    {
        IntentGraphLayoutDefinition layout = new IntentGraphLayoutDefinition();

        if (TryGetProperty(element, "width", out JsonElement widthElement)
            && TryReadFloat(widthElement, out float width))
        {
            layout.Width = width;
        }

        if (TryGetProperty(element, "height", out JsonElement heightElement)
            && TryReadFloat(heightElement, out float height))
        {
            layout.Height = height;
        }

        if (TryGetProperty(element, "moves", out JsonElement movesElement)
            && movesElement.ValueKind == JsonValueKind.Array)
        {
            layout.Moves = new List<IntentGraphMoveLayout>();
            foreach (JsonElement moveElement in movesElement.EnumerateArray())
            {
                if (moveElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetProperty(moveElement, "id", out JsonElement moveIdElement)
                    || moveIdElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? moveId = moveIdElement.GetString();
                if (string.IsNullOrWhiteSpace(moveId))
                {
                    continue;
                }

                float x = TryGetProperty(moveElement, "x", out JsonElement xElement)
                          && TryReadFloat(xElement, out float xValue)
                    ? xValue
                    : 0f;
                float y = TryGetProperty(moveElement, "y", out JsonElement yElement)
                          && TryReadFloat(yElement, out float yValue)
                    ? yValue
                    : 0f;

                layout.Moves.Add(new IntentGraphMoveLayout
                {
                    Id = moveId,
                    X = x,
                    Y = y
                });
            }
        }

        if (TryGetProperty(element, "arrows", out JsonElement arrowsElement)
            && arrowsElement.ValueKind == JsonValueKind.Array)
        {
            layout.Arrows = new List<IntentGraphArrowLayout>();
            foreach (JsonElement arrowElement in arrowsElement.EnumerateArray())
            {
                if (arrowElement.ValueKind != JsonValueKind.Object
                    || !TryGetProperty(arrowElement, "path", out JsonElement pathElement)
                    || pathElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                IntentGraphArrowLayout arrow = new IntentGraphArrowLayout();
                bool validPath = true;
                foreach (JsonElement point in pathElement.EnumerateArray())
                {
                    if (!TryReadFloat(point, out float value))
                    {
                        validPath = false;
                        break;
                    }

                    arrow.Path.Add(value);
                }

                if (validPath && arrow.Path.Count >= 4 && arrow.Path.Count % 2 == 0)
                {
                    layout.Arrows.Add(arrow);
                }
            }
        }

        if (TryGetProperty(element, "labels", out JsonElement labelsElement)
            && labelsElement.ValueKind == JsonValueKind.Array)
        {
            layout.Labels = new List<IntentGraphLabelLayout>();
            foreach (JsonElement labelElement in labelsElement.EnumerateArray())
            {
                if (labelElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                float x = TryGetProperty(labelElement, "x", out JsonElement xElement)
                          && TryReadFloat(xElement, out float xValue)
                    ? xValue
                    : 0f;
                float y = TryGetProperty(labelElement, "y", out JsonElement yElement)
                          && TryReadFloat(yElement, out float yValue)
                    ? yValue
                    : 0f;

                string text = string.Empty;
                if (TryGetProperty(labelElement, "text", out JsonElement textElement)
                    && textElement.ValueKind == JsonValueKind.String)
                {
                    text = textElement.GetString() ?? string.Empty;
                }

                layout.Labels.Add(new IntentGraphLabelLayout
                {
                    X = x,
                    Y = y,
                    Text = text
                });
            }
        }

        if (TryGetProperty(element, "iconGroups", out JsonElement iconGroupsElement)
            && iconGroupsElement.ValueKind == JsonValueKind.Array)
        {
            layout.IconGroups = new List<IntentGraphGroupLayout>();
            foreach (JsonElement groupElement in iconGroupsElement.EnumerateArray())
            {
                if (groupElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                float x = TryGetProperty(groupElement, "x", out JsonElement xElement)
                          && TryReadFloat(xElement, out float xValue)
                    ? xValue
                    : 0f;
                float y = TryGetProperty(groupElement, "y", out JsonElement yElement)
                          && TryReadFloat(yElement, out float yValue)
                    ? yValue
                    : 0f;
                float groupWidth = TryGetProperty(groupElement, "width", out JsonElement groupWidthElement)
                                   && TryReadFloat(groupWidthElement, out float widthValue)
                    ? widthValue
                    : 0f;
                float groupHeight = TryGetProperty(groupElement, "height", out JsonElement groupHeightElement)
                                    && TryReadFloat(groupHeightElement, out float heightValue)
                    ? heightValue
                    : 0f;

                layout.IconGroups.Add(new IntentGraphGroupLayout
                {
                    X = x,
                    Y = y,
                    Width = groupWidth,
                    Height = groupHeight
                });
            }
        }

        return layout;
    }

    private static void MergeLocalizedTextFromFileLocked(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return;
        }

        try
        {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return;
            }

            string json = file.GetAsText();
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty entry in document.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                _localizedText[entry.Name] = entry.Value.GetString() ?? string.Empty;
            }
        }
        catch (Exception exception)
        {
            Log.Warn("[IntentGraph] Failed to parse localization at " + path + ": " + exception.Message);
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out JsonElement property))
        {
            value = property;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryReadFloat(JsonElement element, out float value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetSingle(out value):
                return float.IsFinite(value);
            case JsonValueKind.Number when element.TryGetDouble(out double doubleValue):
                value = (float)doubleValue;
                return float.IsFinite(value);
            case JsonValueKind.String when float.TryParse(
                                             element.GetString(),
                                             NumberStyles.Float,
                                             CultureInfo.InvariantCulture,
                                             out value):
                return float.IsFinite(value);
            default:
                value = 0f;
                return false;
        }
    }
}
