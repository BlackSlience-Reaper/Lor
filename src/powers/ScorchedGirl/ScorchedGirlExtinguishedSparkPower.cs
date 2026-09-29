using System;
using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.ScorchedGirl;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.ScorchedGirl;

public sealed class LibraryOfRuinaScorchedGirlExtinguishedSparkPower : LibraryOfRuinaPowerModel
{
    private const int CurrentHpLossPercent = 50;

    protected override string LegacyPowerId => "SCORCHED_GIRL_EXTINGUISHED_SPARK_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", CurrentHpLossPercent)
    ];

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || Owner.IsDead || creature == Owner)
        {
            return;
        }

        if (creature.Side != Owner.Side || creature.Monster is not TheFourthMatchFlame)
        {
            return;
        }

        int hpLoss = (int)Math.Ceiling(Owner.CurrentHp * CurrentHpLossPercent / 100m);
        if (hpLoss <= 0)
        {
            return;
        }

        Flash();
        decimal targetHp = Owner.CurrentHp - hpLoss;
        if (targetHp < 0m)
        {
            targetHp = 0m;
        }

        
        await CreatureCmd.SetCurrentHp(Owner, targetHp);

    }
}
