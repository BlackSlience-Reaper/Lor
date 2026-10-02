#if STS2_0_111_0
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
namespace LibraryOfRuina.content.specialguests;
public sealed partial class SpecialGuestRunStateModifier
{
    // 只有新版有 BeforeCombatRewardOffered 钩子；旧版由 SpecialGuestRewardResumePatch.Legacy 直接调用同一增补流程。
    public override Task BeforeCombatRewardOffered(RewardsSet rewards, CombatRoom room) =>
        SpecialGuestStageFlow.AugmentRewardsAsync(rewards.Player.RunState, room, rewards);
}
#endif
