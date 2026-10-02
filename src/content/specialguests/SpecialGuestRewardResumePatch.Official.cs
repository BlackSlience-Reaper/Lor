#if STS2_0_111_0
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
namespace LibraryOfRuina.content.specialguests;
internal static partial class SpecialGuestRewardResumePatch
{
    private static bool ShouldReplaceOffer(CombatRoom room) => room is
        { IsPreFinished: true, ParentEventId: not null, Encounter: ISpecialGuestEncounterStage };
    private static Task BeforeRewards(RewardsSet rewards, CombatRoom room) =>
        Hook.BeforeCombatRewardOffered(rewards, room.CombatState.RunState, room);
}
#endif
