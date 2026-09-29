using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.ScorchedGirl;

public sealed class LibraryOfRuinaScorchedGirlAshesPower : LibraryOfRuinaPowerModel
{
    private const int HitThreshold = 3;
    private const int WeakPerThreshold = 1;

    private sealed class Data
    {
        public int HitCountThisTurn;
    }

    protected override string LegacyPowerId => "SCORCHED_GIRL_ASHES_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Hits", HitThreshold),
        new PowerVar<WeakPower>(WeakPerThreshold)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Data data = GetInternalData<Data>();
        data.HitCountThisTurn++;

        int weakStacksToApply = data.HitCountThisTurn / HitThreshold;
        data.HitCountThisTurn %= HitThreshold;
        if (weakStacksToApply <= 0)
        {
            return;
        }

        List<Creature> playerTargets = new();
        foreach (Creature creature in CombatState.PlayerCreatures)
        {
            if (creature.IsAlive)
            {
                playerTargets.Add(creature);
            }
        }

        if (playerTargets.Count <= 0)
        {
            return;
        }

        Flash();
        for (int i = 0; i < weakStacksToApply; i++)
        {
            await PowerCmdCompat.Apply<WeakPower>(playerTargets, WeakPerThreshold, Owner, null);
        }
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side)
        {
            GetInternalData<Data>().HitCountThisTurn = 0;
        }

        return Task.CompletedTask;
    }
}
