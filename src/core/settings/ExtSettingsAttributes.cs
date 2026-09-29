using System;

namespace LibraryOfRuina.core.settings;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class SettingsSectionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property)]
public class SliderRangeAttribute : Attribute
{
    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    public SliderRangeAttribute(double min, double max, double step = 1.0)
    {
        if (min > max)
            throw new ArgumentException($"SliderRange: Min ({min}) cannot be greater than Max ({max}).");
        if (step <= 0)
            throw new ArgumentOutOfRangeException(nameof(step), "SliderRange: Step must be greater than 0.");
        Min = min;
        Max = max;
        Step = step;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class SliderLabelFormatAttribute(string format) : Attribute
{
    public string Format { get; } = format;
}

[AttributeUsage(AttributeTargets.Property)]
public class SliderValueLabelsAttribute(params string[] keys) : Attribute
{
    public string[] Keys { get; } = keys;
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsHoverTipAttribute(bool enabled = true) : Attribute
{
    public bool Enabled { get; } = enabled;
}

[AttributeUsage(AttributeTargets.Class)]
public class HoverTipsByDefaultAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class SettingsIgnoreAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class SettingsHideInUI : Attribute;

/// <summary>
/// Progress or migration marker stored in the config file. "Restore Defaults" leaves it alone,
/// so resetting preferences does not replay tutorials or re-run one-time migrations.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SettingsKeepOnRestoreDefaultsAttribute : Attribute;

/// <summary>Gameplay setting that is hidden from the settings screen while a run is in progress.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class SettingsLockedDuringRunAttribute : Attribute;

public enum TextInputPreset
{
    Anything,
    Alphanumeric,
    AlphanumericWithSpaces,
    SafeDisplayName,
}

[AttributeUsage(AttributeTargets.Property)]
public class SettingsTextInputAttribute : Attribute
{
    public string AllowedCharactersRegex { get; }

    public int MaxLength { get; set; }

    public SettingsTextInputAttribute(TextInputPreset preset)
    {
        AllowedCharactersRegex = preset switch
        {
            TextInputPreset.Alphanumeric => "[a-zA-Z0-9]+",
            TextInputPreset.AlphanumericWithSpaces => "[a-zA-Z0-9 ]+",
            TextInputPreset.SafeDisplayName => @"[\p{L}\d_\- ]+",
            TextInputPreset.Anything or _ => ".*",
        };
    }

    public SettingsTextInputAttribute(string customRegex)
    {
        AllowedCharactersRegex = customRegex;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class SettingsButtonAttribute(string buttonLabelKey) : Attribute
{
    public string ButtonLabelKey { get; } = buttonLabelKey;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class SettingsVisibleWhenAttribute(string watchedPropertyName, object expectedValue, bool invert = false) : Attribute
{
    public string WatchedPropertyName { get; } = watchedPropertyName;

    public object ExpectedValue { get; } = expectedValue;

    public bool Invert { get; } = invert;
}
