using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.relics;

internal static class AbnormalityPageRewardHelper
{
    /// <summary>给本场战斗的每名玩家各加一份书页遗物奖励；本局已出现过或房间里已有同一书页奖励的玩家跳过。</summary>
    public static void AddPageRewardForEachPlayer<TPageRelic>(CombatRoom room, string titleLocKey)
        where TPageRelic : RelicModel
    {
        foreach (Player player in room.CombatState.Players)
        {
            if (!ShouldAddPageReward<TPageRelic>(room, player, titleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<TPageRelic>().ToMutable(), player));
        }
    }

    public static bool ShouldAddPageReward<TPageRelic>(
        CombatRoom room,
        Player player,
        string titleLocKey)
        where TPageRelic : RelicModel
    {
        return ShouldAddPageReward<TPageRelic>(room, player, [], titleLocKey);
    }

    public static bool ShouldAddPageReward<TPageRelic>(
        CombatRoom room,
        Player player,
        IEnumerable<Reward> pendingRewards,
        string titleLocKey)
        where TPageRelic : RelicModel
    {
        RelicModel pageRelic = ModelDb.Relic<TPageRelic>();
        return !HasPageRelicAppearedInRun(player.RunState, pageRelic.Id)
            && !HasPageRelicReward(room, player, pageRelic.Id, titleLocKey)
            && !HasPageRelicReward(pendingRewards, pageRelic.Id, titleLocKey);
    }

    public static bool CanPageRelicAppear<TPageRelic>(IRunState runState)
        where TPageRelic : RelicModel
    {
        return !HasPageRelicAppearedInRun(runState, ModelDb.Relic<TPageRelic>().Id);
    }

    public static bool HasRelicReward(CombatRoom room, Player player, string titleLocKey)
    {
        return HasPageRelicReward(room, player, titleLocKey);
    }

    public static InvalidOperationException UnexpectedPageChoiceCard(CardModel? card)
    {
        return new InvalidOperationException("[LibraryOfRuina.PageRelic] Unexpected page choice card: "
                                             + (card?.Id.Entry ?? "<skip>"));
    }

    public static async Task SkipObtainedPageRelic(RelicModel relic, string context)
    {
        if (AbnormalityPageRewardPreselection.IsPreselectingPageReward)
        {
            Log.Info("[LibraryOfRuina.PageRelic] "
                + relic.GetType().Name
                + " skip was consumed during pre-obtain page choice; keeping reward available.");
            return;
        }

        Player owner = relic.Owner;
        MarkCurrentRelicChoiceAsSkipped(owner, relic.Id);

        if (!owner.Relics.Contains(relic))
        {
            Log.Warn("[LibraryOfRuina.PageRelic] "
                + relic.GetType().Name
                + " was skipped during "
                + context
                + " but was not present in the owner's relic list.");
            return;
        }

        Log.Info("[LibraryOfRuina.PageRelic] "
            + relic.GetType().Name
            + " was skipped during "
            + context
            + "; removing the just-obtained page relic.");
        owner.RemoveRelicInternal(relic);
        await relic.AfterRemoved();
    }

    private static bool HasPageRelicAppearedInRun(IRunState runState, ModelId relicId)
    {
        string? enhancedEntry = relicId.Entry switch
        {
            "QUEEN_OF_HATRED_PAGE_RELIC" => "QUEEN_OF_HATRED_ENHANCED_PAGE_RELIC",
            "KING_OF_GREED_PAGE_RELIC" => "KING_OF_GREED_ENHANCED_PAGE_RELIC",
            "WRATH_SERVANT_PAGE_RELIC" => "WRATH_SERVANT_ENHANCED_PAGE_RELIC",
            "DESPAIR_KNIGHT_PAGE_RELIC" => "DESPAIR_KNIGHT_ENHANCED_PAGE_RELIC",
            _ => null
        };

        // 魔法少女补齐强化书页后，对应原版书页也视为已经取得，避免再次掉落叠加。
        if (runState.Players.Any(player => player.Relics.Any(relic =>
            relic.Id == relicId || relic.Id.Entry == enhancedEntry)))
        {
            return true;
        }

        return runState.MapPointHistory
            .SelectMany(actHistory => actHistory)
            .SelectMany(entry => entry.PlayerStats)
            .SelectMany(playerEntry => playerEntry.RelicChoices)
            .Any(choice => choice.wasPicked
                && (choice.choice == relicId || choice.choice.Entry == enhancedEntry));
    }

    private static bool HasPageRelicReward(CombatRoom room, Player player, string titleLocKey)
    {
        if (!room.ExtraRewards.TryGetValue(player, out List<Reward>? rewards) || rewards == null)
        {
            return false;
        }

        return HasPageRelicReward(rewards, titleLocKey);
    }

    private static bool HasPageRelicReward(IEnumerable<Reward> rewards, string titleLocKey)
    {
        return rewards
            .OfType<RelicReward>()
            .Any(reward =>
                reward.IsPopulated
                && reward.Description.LocTable == "relics"
                && reward.Description.LocEntryKey == titleLocKey);
    }

    private static bool HasPageRelicReward(CombatRoom room, Player player, ModelId relicId, string titleLocKey)
    {
        if (!room.ExtraRewards.TryGetValue(player, out List<Reward>? rewards) || rewards == null)
        {
            return false;
        }

        return HasPageRelicReward(rewards, relicId, titleLocKey);
    }

    private static bool HasPageRelicReward(IEnumerable<Reward> rewards, ModelId relicId, string titleLocKey)
    {
        return rewards
            .OfType<RelicReward>()
            .Any(reward => RewardMatchesPageRelic(reward, relicId, titleLocKey));
    }

    private static bool RewardMatchesPageRelic(RelicReward reward, ModelId relicId, string titleLocKey)
    {
        if (reward.ClaimedRelic?.Id == relicId)
        {
            return true;
        }

        if (VanillaPrivate.RelicRewardRelic.Get(reward) is RelicModel pendingRelic && pendingRelic.Id == relicId)
        {
            return true;
        }

        return reward.IsPopulated
            && reward.Description.LocTable == "relics"
            && reward.Description.LocEntryKey == titleLocKey;
    }

    private static void MarkCurrentRelicChoiceAsSkipped(Player player, ModelId relicId)
    {
        List<ModelChoiceHistoryEntry>? choices = player.RunState.CurrentMapPointHistoryEntry?
            .GetEntry(player.NetId)
            .RelicChoices;
        if (choices == null)
        {
            return;
        }

        for (int i = choices.Count - 1; i >= 0; i--)
        {
            ModelChoiceHistoryEntry choice = choices[i];
            if (choice.choice != relicId || !choice.wasPicked)
            {
                continue;
            }

            choices[i] = new ModelChoiceHistoryEntry(relicId, wasPicked: false);
            return;
        }

        choices.Add(new ModelChoiceHistoryEntry(relicId, wasPicked: false));
    }
}
