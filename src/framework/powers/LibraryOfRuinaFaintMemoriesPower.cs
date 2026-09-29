using System.Threading.Tasks;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.powers;

public sealed class LibraryOfRuinaFaintMemoriesPower : LibraryOfRuinaPowerModel
{
    private const int RapidWearTurns = 3;
    private const int RapidWearAmount = 1;
    private const int HpThresholdPercent = 25;

    protected override string LegacyPowerId => "FAINT_MEMORIES_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", HpThresholdPercent),
        new DynamicVar("Vulnerable", RapidWearAmount),
        new DynamicVar("Turns", RapidWearTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryVulnerablePower>()
    ];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner || !target.IsPlayer || result.UnblockedDamage <= 0 || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        decimal hpThreshold = Owner.MaxHp * HpThresholdPercent / 100m;
        if (Owner.CurrentHp >= hpThreshold)
        {
            return;
        }

        await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
            new ThrowingPlayerChoiceContext(),
            target,
            RapidWearAmount,
            RapidWearTurns - 1,
            IsPermanent: false,
            Owner,
            null);
    }
}
