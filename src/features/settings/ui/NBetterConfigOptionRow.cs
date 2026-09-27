using Godot;

namespace LibraryOfRuina.features.settings.ui;

internal partial class NBetterConfigOptionRow : MarginContainer
{
    public Control SettingControl { get; private set; }

    private readonly string _modPrefix;

    public NBetterConfigOptionRow(string modPrefix, string name, Control label, Control settingControl)
    {
        _modPrefix = modPrefix;
        Name = name;
        SettingControl = settingControl;

        AddThemeConstantOverride("margin_left", 12);
        AddThemeConstantOverride("margin_right", 12);
        MouseFilter = MouseFilterEnum.Pass;
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(0, 64);

        label.CustomMinimumSize = new Vector2(0, 64);

        AddChild(label);
        AddChild(settingControl);
    }

    public void AddHoverTip()
    {
        
    }

    public void RemoveHoverTip()
    {
        
    }
}

