using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.patches;

internal static class LiberationSettlementProceedHelper
{
    public static void ClearLingeringCardPreviews()
    {
        ClearChildren(NRun.Instance?.GlobalUi?.CardPreviewContainer);
        ClearChildren(NRun.Instance?.GlobalUi?.MessyCardPreviewContainer);
        ClearChildren(NRun.Instance?.GlobalUi?.GridCardPreviewContainer);
        ClearChildren(NRun.Instance?.GlobalUi?.EventCardPreviewContainer);
        ClearChildren(NCombatRoom.Instance?.Ui?.CardPreviewContainer);
        ClearChildren(NCombatRoom.Instance?.Ui?.MessyCardPreviewContainer);
    }

    private static void ClearChildren(Node? container)
    {
        if (container == null || !GodotObject.IsInstanceValid(container))
        {
            return;
        }

        foreach (Node child in container.GetChildren().OfType<Node>().ToArray())
        {
            child.GetParent()?.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }
    }
}
