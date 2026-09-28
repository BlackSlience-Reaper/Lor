using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Godot;
using LibraryOfRuina.features.settings.ui;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.settings;

internal class ExtAutoModSettings : ExtModSettings
{
    protected readonly List<EventHandler> _configChangedHandlers = new();
    protected readonly List<Action> _configReloadedHandlers = new();

    public override void SetupConfigUI(Control optionContainer)
    {
        Log.Info($"Setting up config UI for {GetType().FullName}");
        GenerateOptionsForAllProperties(optionContainer);
        AddRestoreDefaultsButton(optionContainer);
    }

    protected void AddRestoreDefaultsButton(Control optionContainer)
    {
        var resetButton = CreateRawButtonControl("Restore Defaults", () =>
        {
            RestoreDefaultsNoConfirm();
        });
        resetButton.CustomMinimumSize = new Vector2(360, resetButton.CustomMinimumSize.Y);
        resetButton.SetColor(0.45f, 1.5f, 0.8f);

        var centerContainer = new CenterContainer();
        centerContainer.CustomMinimumSize = new Vector2(0, 128);
        centerContainer.AddChild(resetButton);
        optionContainer.AddChild(centerContainer);

        var selfNodePath = new NodePath(".");
        resetButton.FocusNeighborBottom = selfNodePath;
        resetButton.FocusNeighborLeft = selfNodePath;
        resetButton.FocusNeighborRight = selfNodePath;
    }

    protected NBetterConfigOptionRow CreateToggleOption(PropertyInfo property, bool addHoverTip = false) =>
        CreateStandardOption(CreateRawTickboxControl, property, addHoverTip);

    protected NBetterConfigOptionRow CreateSliderOption(PropertyInfo property, bool addHoverTip = false) =>
        CreateStandardOption(CreateRawSliderControl, property, addHoverTip);

    protected NBetterConfigOptionRow CreateDropdownOption(PropertyInfo property, bool addHoverTip = false) =>
        CreateStandardOption(CreateRawDropdownControl, property, addHoverTip);

    protected NBetterConfigOptionRow CreateLineEditOption(PropertyInfo property, bool addHoverTip = false) =>
        CreateStandardOption(CreateRawLineEditControl, property, addHoverTip);

    protected NBetterConfigOptionRow CreateButton(string rowLabelKey, string buttonLabelKey, Action onPressed, bool addHoverTip = false)
    {
        var control = CreateRawButtonControl(GetLabelText(buttonLabelKey), onPressed);
        var label = CreateRawLabelControl(GetLabelText(rowLabelKey), 28);
        var option = new NBetterConfigOptionRow(ModPrefix, rowLabelKey, label, control);
        if (addHoverTip) option.AddHoverTip();
        return option;
    }

    protected MarginContainer CreateSectionHeader(string labelName, bool alignToTop = false)
    {
        MarginContainer container = new();
        container.Name = "Container_" + labelName.Replace(" ", "");
        container.AddThemeConstantOverride("margin_left", 24);
        container.AddThemeConstantOverride("margin_right", 24);
        container.MouseFilter = Control.MouseFilterEnum.Ignore;
        container.FocusMode = Control.FocusModeEnum.None;

        var label = CreateRawLabelControl($"[center][b]{GetLabelText(labelName)}[/b][/center]", 40);
        label.Name = "SectionLabel_" + labelName.Replace(" ", "");
        label.CustomMinimumSize = new Vector2(0, 64);

        if (alignToTop) label.VerticalAlignment = VerticalAlignment.Top;

        container.AddChild(label);
        return container;
    }

    protected NBetterConfigOptionRow CreateStandardOption(Func<PropertyInfo, Control> controlCreator, PropertyInfo property, bool addHoverTip = false)
    {
        var control = controlCreator.Invoke(property);
        var label = CreateRawLabelControl(GetLabelText(property.Name), 28);
        var option = new NBetterConfigOptionRow(ModPrefix, property.Name, label, control);
        if (addHoverTip) option.AddHoverTip();
        return option;
    }

    protected NBetterConfigOptionRow GenerateOptionFromProperty(PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        NBetterConfigOptionRow optionRow;
        if (propertyType == typeof(bool)) optionRow = CreateToggleOption(property);
        else if (propertyType == typeof(double)) optionRow = CreateSliderOption(property);
        else if (propertyType == typeof(string)) optionRow = CreateLineEditOption(property);
        else if (propertyType.IsEnum) optionRow = CreateDropdownOption(property);
        else throw new NotSupportedException($"Type {propertyType.FullName} is not supported.");

        AddHoverTipToOptionRowIfEnabled(optionRow, property);
        return optionRow;
    }

    protected NBetterConfigOptionRow GenerateButtonRowFromMethod(MethodInfo method)
    {
        var attr = method.GetCustomAttribute<SettingsButtonAttribute>() ?? throw new ArgumentException(
            $"GenerateButtonRowFromMethod called on {method.Name} but it lacks [SettingsButton].");

        foreach (var param in method.GetParameters())
            ResolveButtonArgument(param, null!);

        NBetterConfigOptionRow optionRow = null!;

        var onButtonClicked = () =>
        {
            try
            {
                var args = method.GetParameters()
                    .Select(param => ResolveButtonArgument(param, optionRow))
                    .ToArray();
                method.Invoke(method.IsStatic ? null : this, args);
            }
            catch (Exception e)
            {
                Log.Error($"Error executing [SettingsButton] method {method.Name}: {e}");
            }
            ConfigReloaded();
            ShowAndClearPendingErrors();
        };

        optionRow = CreateButton(method.Name, attr.ButtonLabelKey, onButtonClicked, true);
        AddHoverTipToOptionRowIfEnabled(optionRow, method);
        return optionRow;
    }

    protected object ResolveButtonArgument(ParameterInfo param, NBetterConfigOptionRow? optionRow)
    {
        var t = param.ParameterType;
        if (typeof(ExtModSettings).IsAssignableFrom(t)) return this;
        if (t == typeof(NBetterConfigOptionRow)) return optionRow!;
        if (t == typeof(NExtActionButton)) return optionRow?.SettingControl!;
        throw new ArgumentException($"Unsupported parameter type '{t.Name}' for method {param.Member.Name}.");
    }

    protected void AddHoverTipToOptionRowIfEnabled(NBetterConfigOptionRow row, MemberInfo member)
    {
        var propertyHoverAttr = member.GetCustomAttribute<SettingsHoverTipAttribute>();
        var classHoverAttr = GetType().GetCustomAttribute<HoverTipsByDefaultAttribute>();

        var hoverTipsByDefault = classHoverAttr != null;
        var explicitEnabled = propertyHoverAttr?.Enabled;

        if (explicitEnabled ?? hoverTipsByDefault)
            row.AddHoverTip();
    }

    protected void GenerateOptionsForAllProperties(Control targetContainer)
    {
        Control? currentSetting = null;
        string? currentSection = null;
        var selfNodePath = new NodePath(".");

        var filteredMembers = GetType()
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(IsVisibleMember)
            .Where(static member => member.GetCustomAttribute<SettingsLockedDuringRunAttribute>() == null
                || !RunManager.Instance.IsInProgress)
            .OrderBy(GetSourceOrder)
            .ToList();

        for (var i = 0; i < filteredMembers.Count; i++)
        {
            var member = filteredMembers[i];
            var nextMember = i < filteredMembers.Count - 1 ? filteredMembers[i + 1] : null;

            var visibleWhen = member.GetCustomAttribute<SettingsVisibleWhenAttribute>();
            Control? associatedDivider = null;
            Action? updateVisibility = null;

            var sectionName = member.GetCustomAttribute<SettingsSectionAttribute>()?.Name;
            if (sectionName != null && sectionName != currentSection)
            {
                currentSection = sectionName;
                var isFirstChild = targetContainer.GetChildCount() == 0;
                targetContainer.AddChild(CreateSectionHeader(currentSection, alignToTop: isFirstChild));
            }

            try
            {
                var newRow = member switch
                {
                    PropertyInfo p => GenerateOptionFromProperty(p),
                    MethodInfo m => GenerateButtonRowFromMethod(m),
                    _ => throw new UnreachableException()
                };
                targetContainer.AddChild(newRow);

                if (visibleWhen != null)
                {
                    var watchedProp = GetType().GetProperty(visibleWhen.WatchedPropertyName);
                    if (watchedProp != null)
                    {
                        updateVisibility = () =>
                        {
                            var currentVal = watchedProp.GetValue(null);
                            var shouldBeVisible = Equals(currentVal?.ToString(), visibleWhen.ExpectedValue?.ToString());
                            if (visibleWhen.Invert) shouldBeVisible = !shouldBeVisible;
                            newRow.Visible = shouldBeVisible;
                            if (associatedDivider != null) associatedDivider.Visible = shouldBeVisible;
                        };

                        EventHandler configChangedHandler = (_, _) => updateVisibility();
                        ConfigChanged += configChangedHandler;
                        OnConfigReloaded += updateVisibility;
                        _configChangedHandlers.Add(configChangedHandler);
                        _configReloadedHandlers.Add(updateVisibility);
                    }
                }

                var previousSetting = currentSetting;
                currentSetting = newRow.SettingControl;

                if (previousSetting != null)
                {
                    currentSetting.FocusNeighborTop = currentSetting.GetPathTo(previousSetting);
                    previousSetting.FocusNeighborBottom = previousSetting.GetPathTo(currentSetting);
                }
                else
                {
                    currentSetting.FocusNeighborTop = selfNodePath;
                }

                currentSetting.FocusNeighborLeft = selfNodePath;
                currentSetting.FocusNeighborRight = selfNodePath;
            }
            catch (NotSupportedException ex)
            {
                Log.Error($"Not creating UI for unsupported property '{member.Name}': {ex}");
                continue;
            }

            var nextSectionName = nextMember?.GetCustomAttribute<SettingsSectionAttribute>()?.Name;
            var nextIsSameSection = nextSectionName == null || nextSectionName == currentSection;
            if (nextMember != null && nextIsSameSection)
            {
                var divider = CreateDividerControl();
                targetContainer.AddChild(divider);
                if (visibleWhen != null) associatedDivider = divider;
            }
            updateVisibility?.Invoke();
        }

        return;

        bool IsVisibleMember(MemberInfo m) => m switch
        {
            PropertyInfo p => ConfigProperties.Contains(p) && p.GetCustomAttribute<SettingsHideInUI>() == null,
            MethodInfo mt => mt.GetCustomAttribute<SettingsButtonAttribute>() != null,
            _ => false
        };

        int GetSourceOrder(MemberInfo m) => m switch
        {
            MethodInfo mt => mt.MetadataToken,
            PropertyInfo p => p.GetMethod?.MetadataToken ?? p.SetMethod?.MetadataToken ?? 0,
            _ => 0
        };
    }

    public void ClearUIEventHandlers()
    {
        foreach (var handler in _configChangedHandlers) ConfigChanged -= handler;
        foreach (var handler in _configReloadedHandlers) OnConfigReloaded -= handler;
        _configChangedHandlers.Clear();
        _configReloadedHandlers.Clear();
    }
}
