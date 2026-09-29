using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.intents;

internal static class TargetedAttackIntentPreviewHelper
{
    public static int GetModifiedDamage(Creature owner, decimal baseDamage)
    {
        return GetModifiedDamage(owner, null, baseDamage);
    }

    public static int GetModifiedDamage(Creature owner, Creature? target, decimal baseDamage)
    {
        decimal damage = baseDamage;
        if (target?.CombatState != null)
        {
            damage = Hook.ModifyDamage(
                target.CombatState.RunState,
                target.CombatState,
                target,
                owner,
                baseDamage,
                ValueProp.Move,
                null, null, ModifyDamageHookType.All,
                CardPreviewMode.None,
                out _);
        }

        return Math.Max(0, (int)damage);
    }
}
