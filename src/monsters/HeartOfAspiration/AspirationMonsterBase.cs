using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.HeartOfAspiration;

public abstract class AspirationMonsterBase : LorMonsterModel
{
    public bool HasDealtLifeDamageThisEnemyTurn { get; private set; }

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await ApplyFlankingPowers();
        await ApplyAspirationPassive();
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == Creature.Side)
        {
            HasDealtLifeDamageThisEnemyTurn = false;
        }

        return base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer == Creature
            && target.Side != Creature.Side
            && result.UnblockedDamage > 0m)
        {
            HasDealtLifeDamageThisEnemyTurn = true;
        }

        return Task.CompletedTask;
    }

    public void ResetLifeDamageFlag()
    {
        HasDealtLifeDamageThisEnemyTurn = false;
    }

    protected abstract Task ApplyAspirationPassive();

    private async Task ApplyFlankingPowers()
    {
        if (Creature.HasPower<FlankingPower>())
        {
            return;
        }

        IReadOnlyList<Creature> players = Creature.CombatState!.Players
            .Select(player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .ToArray();

        foreach (Creature player in players)
        {
            
            await PowerCmdCompat.Ensure<SurroundedPower>(
                player,
                1m,
                Creature,
                null,
                silent: true);
        }

        if (this is HeartOfAspiration)
        {
            await PowerCmdCompat.Apply<BackAttackRightPower>(Creature, 1, Creature, null, silent: true);
        }
        else
        {
            await PowerCmdCompat.Apply<BackAttackLeftPower>(Creature, 1, Creature, null, silent: true);
        }
    }
}
