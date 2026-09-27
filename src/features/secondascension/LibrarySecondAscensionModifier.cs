using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.features.secondascension;

public sealed class LibrarySecondAscensionModifier : ModifierModel
{
    [SavedProperty]
    public int LibraryOfRuina_SecondAscensionLevel { get; set; }

    public int Level
    {
        get => LibrarySecondAscensionState.Clamp(LibraryOfRuina_SecondAscensionLevel);
        set => LibraryOfRuina_SecondAscensionLevel = LibrarySecondAscensionState.Clamp(value);
    }

    public override LocString Title => new("gameplay_ui", "LIBRARY_SECOND_ASCENSION_MODIFIER.title");

    public override LocString Description => new("gameplay_ui", "LIBRARY_SECOND_ASCENSION_MODIFIER.description");

    public static LibrarySecondAscensionModifier Create(int level)
    {
        LibrarySecondAscensionModifier modifier = (LibrarySecondAscensionModifier)ModelDb.Modifier<LibrarySecondAscensionModifier>().ToMutable();
        modifier.Level = level;
        return modifier;
    }

    public override bool IsEquivalent(ModifierModel other) =>
        other is LibrarySecondAscensionModifier modifier && LibrarySecondAscensionState.Clamp(Level) == LibrarySecondAscensionState.Clamp(modifier.Level);

    protected override void AfterRunCreated(RunState runState)
    {
        if (Level < (int)LibrarySecondAscensionLevel.ReverberationEnsemble)
        {
            return;
        }

        foreach (Player player in runState.Players)
        {
            LoseStartingMaxHp(player);
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Player)
        {
            return;
        }

        decimal recoveryPercent = LibrarySecondAscensionState.GetChaoRecoveryPercent(
            Level,
            MultiplayerScalingPatchHelper.ResolveRunPlayerCount(combatState));
        if (recoveryPercent <= 0m)
        {
            return;
        }

        foreach (LibraryCreature creature in combatState.Enemies.OfType<LibraryCreature>())
        {
            if (!ShouldRecoverChao(creature))
            {
                continue;
            }

            int amount = (int)Math.Ceiling(creature.MaxChaoValue * recoveryPercent);
            await LibraryCreatureCmd.HealChaoValue(creature, amount);
        }
    }

    // 残响乐团（8级）：新局创建时直接扣减生命上限，读档不会重复触发。
    private static void LoseStartingMaxHp(Player player)
    {
        Creature? creature = player.Creature;
        if (creature == null || creature.MaxHp <= 1)
        {
            return;
        }

        int oldMaxHp = creature.MaxHp;
        int newMaxHp = Math.Max(1, oldMaxHp - LibrarySecondAscensionState.GetStartingMaxHpLoss(oldMaxHp));
        creature.SetMaxHpInternal(newMaxHp);
        Log.Info("[LibrarySecondAscension] Applied starting max HP loss: player="
            + player.NetId
            + ", maxHp="
            + oldMaxHp
            + "->"
            + newMaxHp
            + ".");
    }

    private static bool ShouldRecoverChao(LibraryCreature creature) =>
        creature.IsAlive
        && creature.HasChaoResistance
        && !creature.IsChaoed
        && creature.CurrentChaoValue < creature.MaxChaoValue
        && creature.MaxChaoValue > 0;
}
