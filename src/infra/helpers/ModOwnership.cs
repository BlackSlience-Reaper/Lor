using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.infra.helpers;

/// <summary>
/// "Is this ours" checks for gating patches on shared vanilla entry points. Vanilla and third-party
/// models must keep vanilla behavior (design philosophy §2), so a patch that changes flow has to
/// prove the object belongs to this assembly first.
/// </summary>
internal static class ModOwnership
{
    private static readonly System.Reflection.Assembly Assembly = typeof(ModOwnership).Assembly;

    internal static bool IsOwn(AbstractModel? model) =>
        model != null && model.GetType().Assembly == Assembly;

    internal static bool IsOwnMonster(Creature? creature)
    {
        try
        {
            return creature is { IsMonster: true } && IsOwn(creature.Monster);
        }
        catch
        {
            // Monster throws while the creature is detached; such a creature is never ours to handle.
            return false;
        }
    }
}
