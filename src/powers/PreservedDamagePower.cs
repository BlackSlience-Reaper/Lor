using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.relics.HistoryFloorLiberation;
using LibraryOfRuina.relics.StandaloneRelics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace LibraryOfRuina.powers;

public sealed class PreservedDamagePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "PRESERVED_DAMAGE_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    internal static Task SyncFor(Player player)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return Task.CompletedTask;
        }

        // 两种遗物共用一个显示能力，计数必须包含双方尚未结算的伤害。
        int pendingDamage = player.Relics
            .Where(static relic => !relic.IsMelted)
            .Sum(static relic => relic switch
            {
                MatchMarkRelic matchMark => matchMark.PendingDamage,
                FerrisWheelTicketRelic ticket => ticket.PendingDamage,
                _ => 0
            });

        if (pendingDamage <= 0)
        {
            return PowerCmdCompat.RemoveIfPresent<PreservedDamagePower>(player.Creature);
        }

        return PowerCmdCompat.SetAmount<PreservedDamagePower>(
            player.Creature,
            pendingDamage,
            player.Creature,
            null);
    }
}
