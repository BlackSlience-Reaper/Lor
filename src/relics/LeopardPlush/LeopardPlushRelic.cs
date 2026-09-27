using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.relics.LeopardPlush;

public sealed class LeopardPlushRelic : YanamiRelicModel
{
    protected override string IconBaseName => "leopard_plush_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    public static bool IsShopLocked(Player? player)
    {
        if (player == null)
        {
            return false;
        }

        return player.Relics.Any(static relic => relic is LeopardPlushRelic);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner)
        {
            return;
        }

        
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    public override bool ShouldAllowMerchantCardRemoval(Player player)
    {
        if (player == Owner)
        {
            return false;
        }

        return true;
    }
}

