using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.events;
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

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorLiberationSettlementEvent :
    AncientEventModel, ILibrarySettlementEvent
{
    // 结算事件不走先古开场回血。仍调用基类（传 isPreFinished: true 让基类跳过回血），
    // 这样挂在 AncientEventModel.BeforeEventStarted 上的书影遗物授予后缀照常执行。
    protected override Task BeforeEventStarted(bool isPreFinished) =>
        base.BeforeEventStarted(isPreFinished: true);

    private static readonly LiberationSettlementRewardTier[] RewardTiers =
    [
        LiberationSettlementRewardPolicy.CreateTier(
            1,
            LiteratureFloorLiberationSettlementStore.MinimumKilledBossCount,
            "TIER_2",
            "Tier2"),
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
        L10NLookup(
            "LITERATURE_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.INITIAL.description");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Kills", 0),
        new HealVar(15),
        new StringVar(
            "PageRelic",
            ModelDb.Relic<BlackSwanDreamPageRelic>().Title.GetFormattedText()),
        .. LiberationSettlementRewardPolicy.CreateDynamicVars(RewardTiers)
    ];

    public override bool IsAllowed(IRunState runState) => true;

    protected override AncientDialogueSet DefineDialogues()
    {
        return new AncientDialogueSet
        {
            FirstVisitEverDialogue = new AncientDialogue("", "", ""),
            CharacterDialogues = [],
            AgnosticDialogues =
            [
                new AncientDialogue("")
            ]
        };
    }

    public override void CalculateVars()
    {
        LiteratureFloorLiberationSettlementData data =
            LiteratureFloorLiberationSettlementStore.Current;
        _kills = Math.Max(
            LiteratureFloorLiberationSettlementStore
                .MinimumKilledBossCount,
            data.KilledBossCount);
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
            CardCreationOptions options =
                LiberationSettlementRewardPolicy.CreateBossCardOptions(Owner);
            rewards.Add(new CardReward(options, 3, Owner));
        }

        LiberationSettlementRewardPolicy.AppendRelicRewards(rewards, tier, Owner);
        if (Owner.GetRelic<BlackSwanDreamPageRelic>() == null)
        {
            rewards.Add(
                new RelicReward(
                    ModelDb.Relic<BlackSwanDreamPageRelic>().ToMutable(),
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
            CardModel[] upgradable = Owner.Deck.Cards
                .Where(static card => card.IsUpgradable)
                .ToArray();
            foreach (CardModel card in upgradable
                         .OrderBy(_ => Rng.NextFloat())
                         .Take(tier.UpgradeCardCount))
            {
                CardCmd.Upgrade(card, CardPreviewStyle.EventLayout);
            }
        }

        SetEventFinished(
            L10NLookup(
                "LITERATURE_FLOOR_LIBERATION_SETTLEMENT_EVENT.pages.DONE.description"));
    }

    private void AppendLavaRockRewardsIfNeeded(List<Reward> rewards)
    {
        if (Owner == null)
        {
            return;
        }

        LavaRock? lavaRock = Owner.GetRelic<LavaRock>();
        if (lavaRock == null
            || Owner.RunState.CurrentActIndex != 0
            || lavaRock.HasTriggered)
        {
            return;
        }

        for (int i = 0;
             i < lavaRock.DynamicVars["Relics"].IntValue;
             i++)
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
        string suffix = unlocked
            ? tier.OptionKey
            : $"{tier.OptionKey}_LOCKED";
        IEnumerable<IHoverTip> hoverTips = unlocked
            ? HoverTipFactory.FromRelic<BlackSwanDreamPageRelic>()
            : [];
        return new EventOption(
            this,
            unlocked ? () => ClaimReward(tier) : null,
            "LITERATURE_FLOOR_LIBERATION_SETTLEMENT_EVENT"
            + $".pages.INITIAL.options.{suffix}",
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
