using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.compat;

internal static class NCreatureCompat
{
    private static readonly MethodInfo? AnimHideIntentDouble =
        typeof(NCreature).GetMethod(
            nameof(NCreature.AnimHideIntent),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            [typeof(double)],
            modifiers: null);

    private static readonly MethodInfo? AnimHideIntentFloat =
        typeof(NCreature).GetMethod(
            nameof(NCreature.AnimHideIntent),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            [typeof(float)],
            modifiers: null);

    public static void AnimHideIntent(NCreature node, double delay = 0d)
    {
        if (AnimHideIntentDouble != null)
        {
            AnimHideIntentDouble.Invoke(node, [delay]);
            return;
        }

        if (AnimHideIntentFloat != null)
        {
            AnimHideIntentFloat.Invoke(node, [(float)delay]);
            return;
        }

        throw new MissingMethodException(typeof(NCreature).FullName, nameof(NCreature.AnimHideIntent));
    }
}
