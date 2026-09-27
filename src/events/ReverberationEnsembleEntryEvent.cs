using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.events;

public sealed class ReverberationEnsembleEntryEvent : EventModel
{
    public override bool IsShared => true;

    public override bool IsAllowed(IRunState runState) => false;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, ReceiveGuests, InitialOptionKey("RECEIVE_GUESTS"))
    ];

    private Task ReceiveGuests()
    {
        SetEventFinished(InitialDescription);
        // 共享选项会为每位玩家执行；仅本地玩家负责打开本机地图界面。
        if (Owner != null && LocalContext.IsMe(Owner))
        {
            return NEventRoom.Proceed();
        }

        return Task.CompletedTask;
    }
}
