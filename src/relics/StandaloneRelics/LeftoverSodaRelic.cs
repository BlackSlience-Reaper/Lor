using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class LeftoverSodaRelic : YanamiRelicModel
{
    protected override string IconBaseName => "leftover_soda_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        ..HoverTipFactory.FromCardWithCardHoverTips<Regret>()
    ];

    public override async Task BeforeCombatStart()
    {
        Player? owner = Owner;
        if (owner?.Creature?.CombatState is not { } combatState)
        {
            return;
        }

        CardModel regret = combatState.CreateCard<Regret>(owner);
        await CardPileCmdCompat.AddGeneratedCardToCombat(regret, PileType.Draw, addedByPlayer: true);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }
}
