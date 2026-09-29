using System;
using System.Linq;
using HarmonyLib;
using LibraryOfRuina.compat;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(AttackIntent), nameof(AttackIntent.GetSingleDamage))]
public static class PetIntentDamagePatch
{
    [HarmonyPostfix]
    // Only this mod's allied pets: vanilla and third-party pets compute their own intent damage.
    public static void Postfix(ref int __result, AttackIntent __instance, Creature owner)
    {
        if (!owner.IsPet || !ModOwnership.IsOwnMonster(owner)) return;

        var CombatState = owner.CombatState;
        if (CombatState == null) return;

        var enemy = CombatState.Enemies.FirstOrDefault(e => e.IsAlive);
        if (enemy == null) return;

        var me = LocalContextCompat.GetMe(CombatState);
        if (me == null) return;

        var damageCalc = __instance.DamageCalc;
        if (damageCalc == null) return;

        decimal dmg = Hook.ModifyDamage(
            me.RunState, CombatState, enemy, owner,
            damageCalc(), ValueProp.Move, null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _);

        __result = Math.Max(0, (int)dmg);
    }
}
