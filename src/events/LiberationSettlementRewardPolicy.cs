using System;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.events;

internal sealed record LiberationSettlementReward(
    int Gold,
    int CardRewardCount,
    int RelicRewardCount,
    RelicRarity RelicRarity,
    decimal MaxHp,
    int UpgradeCardCount);

internal sealed record LiberationSettlementRewardTier(
    int RequiredKills,
    string OptionKey,
    string VariablePrefix,
    LiberationSettlementReward Reward)
{
    public int Gold => Reward.Gold;

    public int CardRewardCount => Reward.CardRewardCount;

    public int RelicRewardCount => Reward.RelicRewardCount;

    public RelicRarity RelicRarity => Reward.RelicRarity;

    public decimal MaxHp => Reward.MaxHp;

    public int UpgradeCardCount => Reward.UpgradeCardCount;
}

internal static class LiberationSettlementRewardPolicy
{
    // 通用第一档：基础金币，贫困进阶的折减在领取时统一计算。
    private const int TierOneGold = 60;

    // 通用第一档：Boss 卡牌奖励组数。
    private const int TierOneCardRewards = 1;

    // 通用第一档：普通遗物数量，不包含楼层专属书页。
    private const int TierOneRelicRewards = 2;

    // 通用第一档：增加的最大生命值。
    private const decimal TierOneMaxHp = 10m;

    // 通用第一档：随机升级的卡牌数量。
    private const int TierOneUpgrades = 1;

    // 通用第二档：基础金币，贫困进阶的折减在领取时统一计算。
    private const int TierTwoGold = 10;

    // 通用第二档：Boss 卡牌奖励组数。
    private const int TierTwoCardRewards = 2;

    // 通用第二档：普通遗物数量，不包含楼层专属书页。
    private const int TierTwoRelicRewards = 1;

    // 通用第二档：增加的最大生命值。
    private const decimal TierTwoMaxHp = 1m;

    // 通用第二档：随机升级的卡牌数量。
    private const int TierTwoUpgrades = 1;

    // 通用第三档：基础金币，贫困进阶的折减在领取时统一计算。
    private const int TierThreeGold = 250;

    // 通用第三档：Boss 卡牌奖励组数。
    private const int TierThreeCardRewards = 1;

    // 通用第三档：罕见遗物数量，不包含楼层专属书页。
    private const int TierThreeRelicRewards = 2;

    // 通用第三档：增加的最大生命值。
    private const decimal TierThreeMaxHp = 5m;

    // 通用第三档：随机升级的卡牌数量。
    private const int TierThreeUpgrades = 2;

    // 通用第四档：基础金币，贫困进阶的折减在领取时统一计算。
    private const int TierFourGold = 120;

    // 通用第四档：Boss 卡牌奖励组数。
    private const int TierFourCardRewards = 1;

    // 通用第四档：稀有遗物数量，不包含楼层专属书页。
    private const int TierFourRelicRewards = 2;

    // 通用第四档：增加的最大生命值。
    private const decimal TierFourMaxHp = 10m;

    // 通用第四档：随机升级的卡牌数量。
    private const int TierFourUpgrades = 3;

    private static readonly LiberationSettlementReward[] Rewards =
    [
        new(TierOneGold, TierOneCardRewards, TierOneRelicRewards,
            RelicRarity.Common, TierOneMaxHp, TierOneUpgrades),
        new(TierTwoGold, TierTwoCardRewards, TierTwoRelicRewards,
            RelicRarity.Common, TierTwoMaxHp, TierTwoUpgrades),
        new(TierThreeGold, TierThreeCardRewards, TierThreeRelicRewards,
            RelicRarity.Uncommon, TierThreeMaxHp, TierThreeUpgrades),
        new(TierFourGold, TierFourCardRewards, TierFourRelicRewards,
            RelicRarity.Rare, TierFourMaxHp, TierFourUpgrades)
    ];

    public static LiberationSettlementRewardTier CreateTier(
        int rewardLevel,
        int requiredKills,
        string optionKey,
        string variablePrefix)
    {
        if (rewardLevel < 1 || rewardLevel > Rewards.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(rewardLevel));
        }

        return new LiberationSettlementRewardTier(
            requiredKills,
            optionKey,
            variablePrefix,
            Rewards[rewardLevel - 1]);
    }

    public static IEnumerable<DynamicVar> CreateDynamicVars(
        IEnumerable<LiberationSettlementRewardTier> tiers)
    {
        foreach (LiberationSettlementRewardTier tier in tiers)
        {
            yield return new DynamicVar(
                tier.VariablePrefix + "Kills",
                tier.RequiredKills);
            yield return new DynamicVar(
                tier.VariablePrefix + "Gold",
                tier.Gold);
            yield return new DynamicVar(
                tier.VariablePrefix + "CardRewards",
                tier.CardRewardCount);
            yield return new DynamicVar(
                tier.VariablePrefix + "RelicRewards",
                tier.RelicRewardCount);
            yield return new DynamicVar(
                tier.VariablePrefix + "MaxHp",
                tier.MaxHp);
            yield return new DynamicVar(
                tier.VariablePrefix + "Upgrades",
                tier.UpgradeCardCount);
        }
    }

    public static void AppendRelicRewards(
        List<Reward> rewards,
        LiberationSettlementRewardTier tier,
        Player owner)
    {
        for (int i = 0; i < tier.RelicRewardCount; i++)
        {
            rewards.Add(new RelicReward(tier.RelicRarity, owner));
        }
    }

    public static int GetAdjustedGold(
        LiberationSettlementRewardTier tier) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.Poverty,
            (int)(tier.Gold * 0.75m),
            tier.Gold);

    public static CardCreationOptions CreateBossCardOptions(Player owner) =>
        new(
            [owner.Character.CardPool],
            CardCreationSource.Other,
            CardRarityOddsType.BossEncounter);
}
