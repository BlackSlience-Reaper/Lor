using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using OzmaMonster = LibraryOfRuina.monsters.Ozma.Ozma;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.powers.Ozma;

// 层数显示全体玩家共享的真杰克剩余击中次数；进度、回合与真杰克方位由奥兹玛统一保存和结算。
public sealed class OzmaForgottenPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "OZMA_FORGOTTEN_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class OzmaLostMemoryPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "OZMA_LOST_MEMORY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public abstract class OzmaJackDirectionPower : LibraryOfRuinaPowerModel
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class OzmaEastJackPower : OzmaJackDirectionPower
{
    protected override string LegacyPowerId => "OZMA_EAST_JACK_POWER";
}

public sealed class OzmaSouthJackPower : OzmaJackDirectionPower
{
    protected override string LegacyPowerId => "OZMA_SOUTH_JACK_POWER";
}

public sealed class OzmaWestJackPower : OzmaJackDirectionPower
{
    protected override string LegacyPowerId => "OZMA_WEST_JACK_POWER";
}

public sealed class OzmaNorthJackPower : OzmaJackDirectionPower
{
    protected override string LegacyPowerId => "OZMA_NORTH_JACK_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];
}

public sealed class OzmaPainPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "OZMA_PAIN_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class OzmaSorrowPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "OZMA_SORROW_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class OzmaWhichIsRealPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "OZMA_WHICH_IS_REAL_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class OzmaTakeOrBeTakenPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "OZMA_TAKE_OR_BE_TAKEN_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new RequiredTrueJackHitsVar(),
        new DynamicVar("Turns", OzmaMonster.AllowedPlayerTurns),
        new DynamicVar("HpLossPercent", OzmaMonster.ForgottenFailureHpLossPercent)
    ];

    // 战斗中显示按人数缩放后的全队所需击中次数；图鉴等无战斗状态时显示单人基础次数。
    private sealed class RequiredTrueJackHitsVar : DynamicVar
    {
        public RequiredTrueJackHitsVar()
            : base("RequiredHits", OzmaMonster.RequiredTrueJackHits)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            if (_owner is not OzmaTakeOrBeTakenPower { IsMutable: true } power
                || power.Owner?.CombatState is not { } combatState)
            {
                return base.GetBaseValueForIConvertible();
            }

            return OzmaMonster.ResolveRequiredTrueJackHits(combatState);
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }
}
