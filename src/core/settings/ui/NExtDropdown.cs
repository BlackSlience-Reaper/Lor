using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.core.settings.ui;

internal partial class NExtDropdown : NSettingsDropdown
{
    private List<NExtDropdownItem.ItemData>? _items;
    private ExtModSettings? _config;
    private PropertyInfo? _property;

    private int _currentDisplayIndex = -1;
    private NodePath _selfNodePath = new(".");

    private Control? _dropdownContainerRef;

    public NExtDropdown()
    {
        SetCustomMinimumSize(new Vector2(324, 64));
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.Fill;
        FocusMode = FocusModeEnum.All;

        this.TransferAllNodes(SceneHelper.GetScenePath("screens/settings_dropdown"));
    }

    private Control? GetDropdownContainer()
    {
        if (_dropdownContainerRef != null && IsInstanceValid(_dropdownContainerRef))
            return _dropdownContainerRef;

        _dropdownContainerRef = VanillaPrivate.SettingsDropdownDropdownContainer.Get(this);

        if (_dropdownContainerRef == null)
        {
            _dropdownContainerRef = FindChild("DropdownContainer", true, false) as Control
                ?? FindChild("*Container*", true, false) as Control;
        }

        return _dropdownContainerRef;
    }

    public void Initialize(ExtModSettings config, PropertyInfo property, string modPrefix, Action? onChanged)
    {
        _config = config;
        _property = property;
        _items = [];

        var type = property.PropertyType;
        if (!type.IsEnum) throw new NotSupportedException("Dropdown only supports enum types");

        foreach (var value in type.GetEnumValues())
        {
            var loc = LocString.GetIfExists("settings_ui", $"{modPrefix}{StringHelper.Slugify(property.Name)}.{value}");
            var label = loc?.GetRawText() ?? value?.ToString() ?? "UNKNOWN";

            _items.Add(new NExtDropdownItem.ItemData(label, value, () =>
            {
                _property.SetValue(null, value);
                onChanged?.Invoke();
            }));
        }

        _config.OnConfigReloaded += SetFromProperty;
    }

    public void SetFromProperty()
    {
        if (_property == null || _items == null) return;

        var currentValue = _property.GetValue(null);
        var newIndex = _items.FindIndex(item => item.Value?.Equals(currentValue) == true);
        if (newIndex < 0) newIndex = 0;
        _currentDisplayIndex = newIndex;

        if (!IsNodeReady()) return;
        _currentOptionLabel.SetTextAutoSize(_items[newIndex].Text);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (FocusNeighborLeft != _selfNodePath || FocusNeighborRight != _selfNodePath)
        {
            FocusNeighborLeft = _selfNodePath;
            FocusNeighborRight = _selfNodePath;
        }

        var container = GetDropdownContainer();
        if (IsNodeReady() && container is { Visible: true })
        {
            container.GlobalPosition = GlobalPosition + new Vector2(0, Size.Y);
        }
    }

    public override void _Ready()
    {
        ConnectSignals();
        ClearDropdownItems();

        if (_items == null) throw new Exception("Created dropdown without calling Initialize");

        for (var i = 0; i < _items.Count; i++)
        {
            NExtDropdownItem child = NExtDropdownItem.Create(_items[i]);
            _dropdownItems.AddChild(child);
            child.Connect(NDropdownItem.SignalName.Selected,
                Callable.From(new Action<NDropdownItem>(OnDropdownItemSelected)));
            child.Init(i);

            if (i == _currentDisplayIndex)
                _currentOptionLabel.SetTextAutoSize(child.Data.Text);
        }

        _dropdownItems.GetParent<NDropdownContainer>().RefreshLayout();

        var container = GetDropdownContainer();
        if (container != null)
        {
            container.ZIndex = 100;
            container.VisibilityChanged += () =>
            {
                container.TopLevel = container.Visible;
                container.GlobalPosition = GlobalPosition + new Vector2(0, Size.Y);

                if (_currentDisplayIndex < 0 || _items == null || _currentDisplayIndex >= _items.Count) return;
                var entry = _dropdownItems.GetChildOrNull<NExtDropdownItem>(_currentDisplayIndex);
                entry?.TryGrabFocus();
            };
        }
    }

    private void OnDropdownItemSelected(NDropdownItem nDropdownItem)
    {
        if (nDropdownItem is not NExtDropdownItem configDropdownItem) return;

        CloseDropdown();
        _currentOptionLabel.SetTextAutoSize(configDropdownItem.Data.Text);
        _currentDisplayIndex = configDropdownItem.DisplayIndex;
        configDropdownItem.Data.OnSet();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_config != null) _config.OnConfigReloaded -= SetFromProperty;
    }
}

