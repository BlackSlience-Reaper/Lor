using System;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.secondascension;

[HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
internal static class LibrarySecondAscensionCreateCreaturePatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature __result, CombatState __instance)
    {
        if (__result is not LibraryCreature creature || __result.Side != CombatSide.Enemy)
        {
            return;
        }

        IRunState runState = __instance.RunState;
        int level = LibrarySecondAscensionState.GetRunLevel(runState);
        if (level <= 0)
        {
            return;
        }

        LibrarySecondAscensionGameplay.ApplyGeneratedEnemyEffects(
            creature,
            level,
            MultiplayerScalingPatchHelper.ResolveRunPlayerCount(__instance));
    }
}

internal static class LibrarySecondAscensionGameplay
{
    public static void ApplyGeneratedEnemyEffects(LibraryCreature creature, int level, int playerCount)
    {
        // 友方单位不参与人数缩放，第二进阶的人数倍率同样豁免。
        if (AllyTurnRegistry.ShouldSkipMultiplayerScaling(creature))
        {
            return;
        }

        (int OldMax, int NewMax)? chaoCapChange = ApplyChaoCapMultiplier(
            creature,
            LibrarySecondAscensionState.GetChaoCapMultiplier(level, playerCount));
        (int OldMax, int NewMax)? maxHpChange = ApplyMaxHpMultiplier(
            creature,
            LibrarySecondAscensionState.GetEnemyMaxHpMultiplier(level, playerCount));

        Log.Info("[LibrarySecondAscension] Applied enemy effects: level="
            + level
            + ", players="
            + playerCount
            + ", monster="
            + (creature.Monster?.Id.Entry ?? "UNKNOWN_MONSTER")
            + ", chaoCap="
            + FormatMaxChange(chaoCapChange)
            + ", maxHp="
            + FormatMaxChange(maxHpChange)
            + ".");
    }

    // 形态切换重设混乱抗性上限时按当前人数补回传闻倍率，避免覆盖生成时的进阶提高。
    public static decimal ScaleEnemyChaoCap(LibraryCreature creature, decimal amount)
    {
        if (!AppliesToEnemy(creature))
        {
            return amount;
        }

        decimal multiplier = LibrarySecondAscensionState.GetChaoCapMultiplier(
            LibrarySecondAscensionState.GetRunLevel(creature.CombatState?.RunState),
            MultiplayerScalingPatchHelper.ResolveRunPlayerCount(creature.CombatState));
        return multiplier > 1m ? Math.Ceiling(amount * multiplier) : amount;
    }

    // 形态切换重算生命上限时按当前人数补回都市传说倍率，避免覆盖生成时的进阶提高。
    public static decimal ScaleEnemyMaxHp(Creature creature, decimal amount)
    {
        if (!AppliesToEnemy(creature))
        {
            return amount;
        }

        decimal multiplier = LibrarySecondAscensionState.GetEnemyMaxHpMultiplier(
            LibrarySecondAscensionState.GetRunLevel(creature.CombatState?.RunState),
            MultiplayerScalingPatchHelper.ResolveRunPlayerCount(creature.CombatState));
        return multiplier > 1m ? Math.Ceiling(amount * multiplier) : amount;
    }

    private static bool AppliesToEnemy(Creature creature) =>
        creature is LibraryCreature
        && creature.Side == CombatSide.Enemy
        && !AllyTurnRegistry.ShouldSkipMultiplayerScaling(creature);

    private static string FormatMaxChange((int OldMax, int NewMax)? change)
    {
        return change.HasValue ? change.Value.OldMax + "->" + change.Value.NewMax : "none";
    }

    private static (int OldMax, int NewMax)? ApplyChaoCapMultiplier(LibraryCreature creature, decimal multiplier)
    {
        if (multiplier <= 1m || !creature.HasChaoResistance || creature.MaxChaoValue <= 0)
        {
            return null;
        }

        int oldMax = creature.MaxChaoValue;
        int newMax = (int)Math.Ceiling(creature.MaxChaoValue * multiplier);
        creature.SetMaxChaoValueInternal(newMax);
        creature.SetCurrentChaoValueInternal(creature.MaxChaoValue);
        return (oldMax, newMax);
    }

    private static (int OldMax, int NewMax)? ApplyMaxHpMultiplier(LibraryCreature creature, decimal multiplier)
    {
        if (multiplier <= 1m || creature.MaxHp <= 0)
        {
            return null;
        }

        int oldMax = creature.MaxHp;
        int newMax = (int)Math.Ceiling(creature.MaxHp * multiplier);
        int increase = newMax - oldMax;
        creature.SetMaxHpInternal(newMax);
        creature.SetCurrentHpInternal(creature.CurrentHp + increase);
        return (oldMax, newMax);
    }
}
