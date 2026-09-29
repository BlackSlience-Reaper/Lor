using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

internal static class MultiplayerScalingPatchHelper
{
    // 形态切换传入单人基础混乱上限，按当前战斗人数重新计算，避免覆盖生成时的缩放。
    public static Task SetMonsterBaseMaxAndCurrentChaoValue(
        LibraryCreature creature,
        decimal baseAmount)
    {
        decimal amount = baseAmount;
        if (!AllyTurnRegistry.ShouldSkipMultiplayerScaling(creature))
        {
            amount = ScaleHpAmount(creature.CombatState, creature.Monster, baseAmount);
            amount = LibrarySecondAscensionGameplay.ScaleEnemyChaoCap(creature, amount);
        }

        return LibraryCreatureCmd.SetMaxAndCurrentChaoValue(creature, amount);
    }

    public static int ResolveRunPlayerCount(CombatStateLike? CombatState)
    {
        if (CombatState == null)
        {
            return 1;
        }

        int runPlayerCount = CombatState.RunState?.Players?.Count ?? 0;
        if (runPlayerCount >= 1)
        {
            return runPlayerCount;
        }

        int combatPlayerCount = CombatState.Players.Count;
        return combatPlayerCount >= 1 ? combatPlayerCount : 1;
    }

    public static decimal ScaleHpAmount(CombatStateLike? CombatState, MonsterModel? monster, decimal amount)
    {
        if (CombatState == null)
        {
            return amount;
        }

        int playerCount = ResolveRunPlayerCount(CombatState);
        if (playerCount <= 1)
        {
            return amount;
        }

        int actIndex = CombatState.RunState?.CurrentActIndex ?? -1;
        if (actIndex < 0)
        {
            return amount;
        }

        return Creature.ScaleHpForMultiplayer(amount, CombatState.Encounter, playerCount, actIndex);
    }

    public static decimal ScaleMonsterHealAmount(Creature? creature, decimal amount)
    {
        if (creature?.Monster == null || amount <= 0m)
        {
            return amount;
        }

        return ScaleHpAmount(creature.CombatState, creature.Monster, amount);
    }

    public static async Task RescaleMonsterMaxHpAndRestoreDifference(
        Creature creature)
    {
        CombatStateLike? combatState = creature.CombatState;
        if (combatState == null || creature.Monster == null)
        {
            return;
        }

        int playerCount = ResolveRunPlayerCount(combatState);
        int actIndex = combatState.RunState?.CurrentActIndex ?? -1;
        if (playerCount <= 1 || actIndex < 0)
        {
            return;
        }

        decimal unscaledMaxHp =
            creature.MonsterMaxHpBeforeModification ?? creature.MaxHp;
        decimal scaledMaxHp = Creature.ScaleHpForMultiplayer(
            unscaledMaxHp,
            combatState.Encounter,
            playerCount,
            actIndex);
        scaledMaxHp = LibrarySecondAscensionGameplay.ScaleEnemyMaxHp(creature, scaledMaxHp);
        if (scaledMaxHp <= creature.MaxHp)
        {
            return;
        }

        decimal maxHpIncrease = await CreatureCmd.SetMaxHp(
            creature,
            scaledMaxHp);
        if (maxHpIncrease > 0m)
        {
            await CreatureCmd.Heal(creature, maxHpIncrease);
        }
    }
}

internal sealed class ScaledMonsterHealVar : HealVar
{
    public ScaledMonsterHealVar(decimal healAmount)
        : base(healAmount)
    {
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        // Canonical powers throw on Owner and unapplied ones have none; use the unscaled base value.
        return _owner is PowerModel { IsMutable: true, Owner: not null } power
            ? MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(power.Owner, BaseValue)
            : base.GetBaseValueForIConvertible();
    }
}
