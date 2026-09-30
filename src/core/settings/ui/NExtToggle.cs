using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace LibraryOfRuina.core.settings.ui;

internal partial class NExtToggle : NSettingsTickbox
{
    private ExtModSettings? _config;
    private PropertyInfo? _property;

    public NExtToggle()
    {
        SetCustomMinimumSize(new Vector2(324, 64));
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.Fill;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Pass;

        this.TransferAllNodes(SceneHelper.GetScenePath("screens/settings_tickbox"));
    }

    public override void _Ready()
    {
        if (_property == null) throw new Exception("NExtToggle added to tree without an assigned property");
        ConnectSignals();
        SetFromProperty();
    }

    public void Initialize(ExtModSettings modConfig, PropertyInfo property)
    {
        if (property.PropertyType != typeof(bool))
            throw new ArgumentException("NExtToggle requires a bool property");
        _config = modConfig;
        _property = property;
        _config.OnConfigReloaded += SetFromProperty;
    }

    private void SetFromProperty()
    {
        IsTicked = (bool?)_property!.GetValue(null) == true;
    }

    protected override void OnTick()
    {
        _property?.SetValue(null, true);
        _config?.Changed();
    }

    protected override void OnUntick()
    {
        _property?.SetValue(null, false);
        _config?.Changed();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_config != null) _config.OnConfigReloaded -= SetFromProperty;
    }
}

