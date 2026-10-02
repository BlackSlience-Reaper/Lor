using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.core.compat;

/// <summary>
/// 0.107.1 与 0.111.0 均由 RewardsSetSynchronizer 回放 RelicReward.OnSelect。
/// 旧 API 在 0.107.1 仍存在，但原版 OnSelect 已不再调用；额外发送会重复 Obtain，破坏联机选择序号。
/// </summary>
internal static class RewardSyncCompat
{
    public static void SyncObtainedRelicForReward(RelicModel relic)
    {
        // 两个维护目标均通过 RewardsSetSynchronizer 回放 OnSelect；重复发送会重复 Obtain。
    }
}
