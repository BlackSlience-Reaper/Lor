#if STS2_0_107_1
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
namespace LibraryOfRuina.content.specialguests;
internal static partial class SpecialGuestRewardResumePatch
{
    // 旧版没有 BeforeCombatRewardOffered；仅嘉宾战沿用原版先全部生成、再逐个展示的顺序补入奖励。
    private static bool ShouldReplaceOffer(CombatRoom room) => room.Encounter is ISpecialGuestEncounterStage;
    private static Task BeforeRewards(RewardsSet rewards, CombatRoom room) =>
        SpecialGuestStageFlow.AugmentRewardsAsync(rewards.Player.RunState, room, rewards);
}
#endif
