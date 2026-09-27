using System.Linq;
using System.Threading.Tasks;
using System;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.relics.NaturalFloorLiberation;

public abstract class EnhancedMagicalGirlPageRelic<TMode> : LibraryRelicModel
    where TMode : struct, Enum
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) => false;

    protected abstract TMode SelectedMode { get; set; }

    protected abstract IReadOnlyList<CardModel> CreateModeChoiceCards();

    protected abstract TMode ResolveChoice(CardModel card);

    protected CardModel CreateUpgradedChoice<TCard>() where TCard : CardModel, new()
    {
        CardModel card = Owner.RunState.CreateCard<TCard>(Owner);
        card.UpgradeInternal();
        card.FinalizeUpgradeInternal();
        return card;
    }

    public override async Task AfterObtained()
    {
        if (Convert.ToInt32(SelectedMode) != 0)
        {
            EnsureMode();
            await OnModeObtained();
            return;
        }

        CardModel? selected = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(), CreateModeChoiceCards(), Owner, canSkip: true);
        if (selected == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        SetMode(ResolveChoice(selected));
        await OnModeObtained();
    }

    protected virtual Task OnModeObtained() => Task.CompletedTask;

    internal void InheritMode(TMode mode)
    {
        AssertMutable();
        SelectedMode = mode;
        EnsureMode();
    }

    protected void SetMode(TMode mode)
    {
        SelectedMode = mode;
        RelicIconChanged();
        RefreshInventoryIcon();
        UpdateModeUiState();
    }

    private void RefreshInventoryIcon()
    {
        NRelicInventory? inventory = NRun.Instance?.GlobalUi?.RelicInventory;
        if (inventory == null)
        {
            return;
        }

        foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
        {
            if (ReferenceEquals(holder.Relic.Model, this))
            {
                holder.Relic.Icon.Texture = Icon;
                holder.Relic.Outline.Texture = IconOutline;
                break;
            }
        }
    }

    protected void EnsureMode()
    {
        if (!Enum.IsDefined(SelectedMode) || Convert.ToInt32(SelectedMode) == 0)
        {
            SelectedMode = Enum.GetValues<TMode>().First(value => Convert.ToInt32(value) != 0);
        }

        UpdateModeUiState();
    }

    protected void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = Convert.ToInt32(SelectedMode);
        InvokeDisplayAmountChanged();
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureMode();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }
}
