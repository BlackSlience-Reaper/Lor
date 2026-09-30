using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.abnormalities.PunishingBird;

public sealed class ForestKeeperStolenChainsPower : PunishingBirdBasePower
{
    public const int CostReduction = 1;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override string? LegacyPowerId => "FOREST_KEEPER_STOLEN_CHAINS_POWER";

    protected override string IconFileName => "forest_keeper_stolen_chains.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || Owner.IsDead || Owner.CombatState == null)
        {
            return Task.CompletedTask;
        }

        foreach (var player in Owner.CombatState.Players.Where(player =>
                     player.Creature.IsAlive && participants.Contains(player.Creature)))
        {
            CardModel[] locks = PileType.Hand.GetPile(player).Cards
                .Where(static card => card is ForestKeeperLockStatusCard)
                .Where(static card => card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
                .ToArray();
            CardModel? selected = Owner.Monster?.RunRng.CombatCardSelection.NextItem(locks);
            if (selected == null)
            {
                continue;
            }

            selected.EnergyCost.AddUntilPlayed(-CostReduction, reduceOnly: true);
            selected.InvokeEnergyCostChanged();
        }

        return Task.CompletedTask;
    }
}
