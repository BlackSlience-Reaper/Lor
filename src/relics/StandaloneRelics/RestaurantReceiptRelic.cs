using System.Threading.Tasks;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class RestaurantReceiptRelic : YanamiRelicModel
{
    private const int GoldCostPerTurn = 15;
    private const int GoldTarget = 225;

    protected override string IconBaseName => "restaurant_receipt_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool ShowCounter => !EnergyBonusActive;

    public override int DisplayAmount => GoldPaidTotal;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar(GoldCostPerTurn),
        new DynamicVar("TargetGold", GoldTarget),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    [SavedProperty]
    public int GoldPaidTotal { get; private set; }

    [SavedProperty]
    public bool EnergyBonusActive { get; private set; }

    [SavedProperty]
    public bool HasGrantedEnergyThisCombat { get; private set; }

    public override Task AfterObtained()
    {
        UpdateStatusAndCounter();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        HasGrantedEnergyThisCombat = false;
        UpdateStatusAndCounter();
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        UpdateStatusAndCounter();
        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner)
        {
            return;
        }

        GoldPaidTotal = int.Clamp(GoldPaidTotal, 0, GoldTarget);

        if (!EnergyBonusActive && GoldPaidTotal >= GoldTarget)
        {
            EnergyBonusActive = true;
        }

        if (EnergyBonusActive || !HasGrantedEnergyThisCombat)
        {
            HasGrantedEnergyThisCombat = true;
            Flash();
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        }

        if (!EnergyBonusActive && player.Gold >= GoldCostPerTurn)
        {
            await PlayerCmd.LoseGold(DynamicVars.Gold.BaseValue, player);
            GoldPaidTotal += GoldCostPerTurn;
            if (GoldPaidTotal >= GoldTarget)
            {
                GoldPaidTotal = GoldTarget;
                EnergyBonusActive = true;
            }
        }

        UpdateStatusAndCounter();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        HasGrantedEnergyThisCombat = false;
        return Task.CompletedTask;
    }

    private void UpdateStatusAndCounter()
    {
        Status = EnergyBonusActive ? RelicStatus.Active : RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
    }
}
