using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.events;
using LibraryOfRuina.relics.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.events.HistoryFloorLiberation;

public sealed class HistoryFloorLiberationSettlementEvent : AncientEventModel
{
    private static readonly LiberationSettlementRewardTier[] RewardTiers =
    [
        LiberationSettlementRewardPolicy.CreateTier(1, 2, "TIER_2", "Tier2"),
        LiberationSettlementRewardPolicy.CreateTier(2, 3, "TIER_3", "Tier3"),
        LiberationSettlementRewardPolicy.CreateTier(3, 4, "TIER_4", "Tier4"),
        LiberationSettlementRewardPolicy.CreateTier(4, 5, "TIER_5", "Tier5")
    ];

    private int _kills;

    public override bool IsShared => true;

    public override string LocTable => "ancients";

    public override IEnumerable<EventOption> AllPossibleOptions =>
        RewardTiers.Select(RewardOption).ToArray();

    public override LocString InitialDescription =>
        L10NLookup("HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.description");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Kills", 0),
        new HealVar(30),
        new StringVar(
            "PageRelic",
            ModelDb.Relic<SnowWhiteApplePageRelic>().Title.GetFormattedText()),
        .. LiberationSettlementRewardPolicy.CreateDynamicVars(RewardTiers)
    ];

    public override bool IsAllowed(IRunState runState) => true;

    protected override AncientDialogueSet DefineDialogues()
    {
        return new AncientDialogueSet
        {
            FirstVisitEverDialogue = new AncientDialogue(""),
            CharacterDialogues = [],
            AgnosticDialogues =
            [
                new AncientDialogue("")
            ]
        };
    }

    public override void CalculateVars()
    {
        HistoryFloorLiberationSettlementData data = HistoryFloorLiberationSettlementStore.Current;
        _kills = Math.Max(2, data.KilledBossCount);
        RefreshVars();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        RefreshVars();
        return RewardTiers.Select(RewardOption).ToArray();
    }

    private async Task ClaimReward(LiberationSettlementRewardTier tier)
    {
        if (Owner == null)
        {
            return;
        }
        await PlayerCmd.GainGold(
            LiberationSettlementRewardPolicy.GetAdjustedGold(tier),
            Owner);

        var rewards = new List<Reward>();
        for (int i = 0; i < tier.CardRewardCount; i++)
        {
            rewards.Add(
                new CardReward(
                    LiberationSettlementRewardPolicy.CreateBossCardOptions(Owner),
                    3,
                    Owner));
        }

        LiberationSettlementRewardPolicy.AppendRelicRewards(rewards, tier, Owner);
        if (Owner.GetRelic<SnowWhiteApplePageRelic>() == null)
        {
            rewards.Add(
                new RelicReward(
                    ModelDb.Relic<SnowWhiteApplePageRelic>().ToMutable(),
                    Owner));
        }
        AppendLavaRockRewardsIfNeeded(rewards);
        await RewardsCmd.OfferCustom(Owner, rewards);

        if (tier.MaxHp > 0)
        {
            await CreatureCmd.GainMaxHp(Owner.Creature, tier.MaxHp);
        }

        

        if (tier.UpgradeCardCount > 0)
        {
            var upgradable = Owner.Deck.Cards
                .Where(static c => c.IsUpgradable)
                .ToArray();
            foreach (CardModel card in upgradable.OrderBy(_ => Rng.NextFloat()).Take(tier.UpgradeCardCount))
            {
                CardCmd.Upgrade(card, CardPreviewStyle.EventLayout);
            }
        }

        SetEventFinished(L10NLookup("HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.DONE.description"));
    }

    private void AppendLavaRockRewardsIfNeeded(List<Reward> rewards)
    {
        if (Owner == null)
        {
            return;
        }

        LavaRock? lavaRock = Owner.GetRelic<LavaRock>();
        if (lavaRock == null || Owner.RunState.CurrentActIndex != 0 || lavaRock.HasTriggered)
        {
            return;
        }

        for (int i = 0; i < lavaRock.DynamicVars["Relics"].IntValue; i++)
        {
            rewards.Add(new RelicReward(Owner));
        }

        lavaRock.Flash();
        lavaRock.HasTriggered = true;
        lavaRock.Status = RelicStatus.Disabled;
    }

    private EventOption RewardOption(LiberationSettlementRewardTier tier)
    {
        bool unlocked = _kills >= tier.RequiredKills;
        string suffix = unlocked ? tier.OptionKey : $"{tier.OptionKey}_LOCKED";
        IEnumerable<IHoverTip> hoverTips = unlocked
            ? HoverTipFactory.FromRelic<SnowWhiteApplePageRelic>()
            : [];
        return new EventOption(
            this,
            unlocked ? () => ClaimReward(tier) : null,
            $"HISTORY_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.options.{suffix}",
            hoverTips);
    }

    private void RefreshVars()
    {
        DynamicVars["Kills"].BaseValue = _kills;

        foreach (LiberationSettlementRewardTier tier in RewardTiers)
        {
            DynamicVars[tier.VariablePrefix + "Gold"].BaseValue =
                LiberationSettlementRewardPolicy.GetAdjustedGold(tier);
        }
    }
}
