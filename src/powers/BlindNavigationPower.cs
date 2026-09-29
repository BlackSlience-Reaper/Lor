using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers;

public sealed class LibraryOfRuinaBlindNavigationPower : LibraryOfRuinaPowerModel
{
    private const int DamagePercent = 50;
    private const decimal DamageMultiplier = DamagePercent / 100m;

    protected override string LegacyPowerId => "BLIND_NAVIGATION_POWER";

    private sealed class Data
    {
        public Queue<decimal> PendingReducedMoveDamage { get; } = new();
    }

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamagePercent", DamagePercent)
    ];

    protected override object InitInternalData() => new Data();

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer == Owner && target != null && target.IsPlayer && ValuePropCompat.IsCardOrMonsterMove(props))
        {
            return DamageMultiplier;
        }

        return 1m;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner || !ValuePropCompat.IsCardOrMonsterMove(props))
        {
            return;
        }

        var data = GetInternalData<Data>();
        if (data.PendingReducedMoveDamage.Count == 0)
        {
            return;
        }

        decimal retaliationDamage = data.PendingReducedMoveDamage.Dequeue();
        if (retaliationDamage <= 0m)
        {
            return;
        }

        Flash();
        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner,
            retaliationDamage,
            ValueProp.Unpowered | ValueProp.Unblockable | ValueProp.SkipHurtAnim,
            Owner,
            null);
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (dealer != Owner || !target.IsPlayer || !ValuePropCompat.IsCardOrMonsterMove(props))
        {
            return Task.CompletedTask;
        }

        var data = GetInternalData<Data>();
        data.PendingReducedMoveDamage.Enqueue(amount);
        return Task.CompletedTask;
    }
}



