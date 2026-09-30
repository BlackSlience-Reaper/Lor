using Godot;

namespace LibraryOfRuina.framework.powers;










public interface ISecondaryDisplayAmountPower
{
    bool ShowSecondaryDisplayAmount { get; }

    int SecondaryDisplayAmount { get; }

    Color SecondaryDisplayAmountLabelColor { get; }
}
