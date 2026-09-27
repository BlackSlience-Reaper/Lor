using Godot;

namespace LibraryOfRuina.powers;










public interface ISecondaryDisplayAmountPower
{
    bool ShowSecondaryDisplayAmount { get; }

    int SecondaryDisplayAmount { get; }

    Color SecondaryDisplayAmountLabelColor { get; }
}
