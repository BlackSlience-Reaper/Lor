using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorLiberationSettlementEvent : AncientEventModel, ILibrarySettlementEvent
{
    // 结算事件不走先古开场回血。仍调用基类（传 isPreFinished: true 让基类跳过回血），
    // 这样挂在 AncientEventModel.BeforeEventStarted 上的书影遗物授予后缀照常执行。
    protected override Task BeforeEventStarted(bool isPreFinished) =>
        base.BeforeEventStarted(isPreFinished: true);

    private const string LocId = "NATURAL_FLOOR_LIBERATION_SETTLEMENT_EVENT";
    private static readonly LiberationSettlementRewardTier[] Tiers = Enumerable.Range(1, 4)
        .Select(i => LiberationSettlementRewardPolicy.CreateTier(i, i, "TIER_" + i, "Tier" + i)).ToArray();

    // 特殊档沿用第四档基础奖励；所有档位均附带虚无弄臣之页。
    private static readonly LiberationSettlementRewardTier SpecialTier =
        LiberationSettlementRewardPolicy.CreateTier(4, 4, "SPECIAL", "Special");

    private int _kills;
    private bool _claimed;

    public override bool IsShared => true;

    public override string LocTable => "ancients";

    public override bool IsAllowed(IRunState runState) => true;

    public override LocString InitialDescription => L10NLookup(LocId + ".pages.INITIAL.description");

    public override IEnumerable<EventOption> AllPossibleOptions => Tiers.Select(RewardOption).Append(SpecialRewardOption());

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Kills", 0),
        new StringVar("PageRelic", ModelDb.Relic<NihilPageRelic>().Title.GetFormattedText()),
        .. LiberationSettlementRewardPolicy.CreateDynamicVars(Tiers.Append(SpecialTier))
    ];

    protected override AncientDialogueSet DefineDialogues() => new()
    {
        FirstVisitEverDialogue = new AncientDialogue("", "", ""),
        CharacterDialogues = [],
        AgnosticDialogues = [new AncientDialogue("", "", "")]
    };

    public override void CalculateVars()
    {
        _kills = Math.Clamp(NaturalFloorLiberationSettlementStore.KilledBossCount, 1, 4);
        RefreshVars();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        RefreshVars();
        return Tiers.Select(RewardOption).Append(SpecialRewardOption()).ToArray();
    }

    private EventOption SpecialRewardOption()
    {
        bool unlocked = NaturalFloorLiberationSettlementStore.NihilCompleted;
        return new EventOption(this, unlocked ? () => ClaimReward(SpecialTier) : null,
            LocId + ".pages.INITIAL.options.SPECIAL" + (unlocked ? "" : "_LOCKED"),
            unlocked ? HoverTipFactory.FromRelic<NihilPageRelic>() : []);
    }

    private EventOption RewardOption(LiberationSettlementRewardTier tier)
    {
        bool unlocked = _kills >= tier.RequiredKills;
        return new EventOption(this, unlocked ? () => ClaimReward(tier) : null,
            LocId + ".pages.INITIAL.options." + tier.OptionKey + (unlocked ? "" : "_LOCKED"),
            unlocked ? HoverTipFactory.FromRelic<NihilPageRelic>() : []);
    }

    private void RefreshVars()
    {
        DynamicVars["Kills"].BaseValue = _kills;
        foreach (var tier in Tiers.Append(SpecialTier))
            DynamicVars[tier.VariablePrefix + "Gold"].BaseValue = LiberationSettlementRewardPolicy.GetAdjustedGold(tier);
    }

    private async Task ClaimReward(LiberationSettlementRewardTier tier)
    {
        if (_claimed || Owner == null || _kills < tier.RequiredKills) return;
        _claimed = true;
        await PlayerCmd.GainGold(LiberationSettlementRewardPolicy.GetAdjustedGold(tier), Owner);
        var rewards = new List<Reward>();
        for (int i = 0; i < tier.CardRewardCount; i++)
            rewards.Add(new CardReward(LiberationSettlementRewardPolicy.CreateBossCardOptions(Owner), 3, Owner));
        LiberationSettlementRewardPolicy.AppendRelicRewards(rewards, tier, Owner);
        if (Owner.GetRelic<NihilPageRelic>() == null)
        {
            rewards.Add(new RelicReward(ModelDb.Relic<NihilPageRelic>().ToMutable(), Owner));
        }

        await RewardsCmd.OfferCustom(Owner, rewards);
        await CreatureCmd.GainMaxHp(Owner.Creature, tier.MaxHp);
        foreach (CardModel card in Owner.Deck.Cards.Where(static card => card.IsUpgradable)
                     .OrderBy(_ => Rng.NextFloat()).Take(tier.UpgradeCardCount).ToArray())
            CardCmd.Upgrade(card, CardPreviewStyle.EventLayout);
        SetEventFinished(L10NLookup(LocId + ".pages.DONE.description"));
    }
}
