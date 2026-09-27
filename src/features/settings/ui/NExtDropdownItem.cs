using System;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace LibraryOfRuina.features.settings.ui;

internal partial class NExtDropdownItem : NDropdownItem
{
    private static readonly string BaseScenePath = SceneHelper.GetScenePath("ui/dropdown_item");

    public static NExtDropdownItem Create(ItemData data)
    {
        var dropdownItem = new NExtDropdownItem { Data = data };
        dropdownItem.SetCustomMinimumSize(new Vector2(288, 44));
        dropdownItem.MouseFilter = MouseFilterEnum.Pass;
        dropdownItem.TransferAllNodes(BaseScenePath);
        return dropdownItem;
    }

    public required ItemData Data;
    public int DisplayIndex;

    private NExtDropdownItem() { }

    public void Init(int setIndex)
    {
        DisplayIndex = setIndex;
        _label.SetTextAutoSize(Data.Text);
    }

    public class ItemData(string text, object? value, Action onSet)
    {
        public string Text { get; } = text;

        public object? Value { get; } = value;

        public Action OnSet { get; } = onSet;
    }
}

