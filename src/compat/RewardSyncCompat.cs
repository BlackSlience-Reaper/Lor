using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.compat;

/// <summary>
/// Public/Beta 版差：奖励遗物获取后的对端同步方式不同。
/// - Beta：RewardsSetSynchronizer 会在对端确定性回放 RelicReward.OnSelect（内部含 RelicCmd.Obtain），
///   原版 OnSelect 已移除 SyncLocalObtainedRelic。若补丁再发 RewardObtainedMessage，对端会按 sender
///   归属玩家重复 Obtain 一份副本（错误归属 + 重复入账），多开一次模式/附魔/移卡选择，导致双端
///   PlayerChoiceSynchronizer 的 choice ID 计数分叉，最终在回合开始的 checksum 上踢人。
/// - Public：对端不回放 OnSelect，原版仍靠 SyncLocalObtainedRelic 消息传播遗物，必须保留发送。
/// </summary>
internal static class RewardSyncCompat
{
    public static void SyncObtainedRelicForReward(RelicModel relic)
    {
#if STS2_BETA
        // Beta 对端通过 RewardsSetSynchronizer 回放 OnSelect 自行获得遗物；发送消息会造成重复 Obtain。
#else
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedRelic(relic);
#endif
    }
}
