using System;
using System.Threading;
using System.Threading.Tasks;
using LibraryLib.Combat.HealthBars;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.WrathServant;
using LibraryOfRuina.interop;
using LibraryOfRuina.monsters.WrathServant;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.WrathServant;

internal static class WrathServantDamageSourceContext
{
    private static readonly AsyncLocal<int> SourceDepth = new();

    public static bool IsActive => SourceDepth.Value > 0;

    public static IDisposable Enter()
    {
        SourceDepth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            SourceDepth.Value = Math.Max(0, SourceDepth.Value - 1);
            _disposed = true;
        }
    }
}

/// <summary>
/// 下回合腐蚀 - 下回合开始时转化为等量的腐蚀。
/// </summary>
public sealed class WrathServantNextTurnCorrosionPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WRATH_SERVANT_NEXT_TURN_CORROSION_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Owner.IsDead || side != CombatSide.Player || Amount <= 0)
        {
            return;
        }

        decimal convertAmount = Amount;
        Creature? applier = Applier;
        Flash();
        await PowerCmd.Remove(this);
        await PowerCmdCompat.Apply<WrathServantCorrosionPower>(Owner, convertAmount, applier, null);
    }
}

/// <summary>
/// 腐蚀 - 每回合结束时承受X点伤害，随后层数-1。被击中时追加承受等同于层数的伤害。
/// </summary>
public sealed class WrathServantCorrosionPower :
    LibraryOfRuinaPowerModel,
    ILibraryHealthBarDamageForecastSource,
    ICorrosionFollowUpPower
{
    protected override string LegacyPowerId => "WRATH_SERVANT_CORROSION_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 由傲慢之仆或盲怒施加时，腐蚀伤害以施加者为来源并进入其伤害来源上下文。
    private bool IsAppliedByWrathSource =>
        Applier?.Monster is monsters.WrathServant.WrathServant or monsters.NaturalFloorLiberation.NaturalFloorBlindRageBoss;

    public IEnumerable<LibraryHealthBarDamageForecast>
        GetLibraryHealthBarDamageForecasts(
            LibraryHealthBarForecastContext context) =>
        [
            new(
                Amount,
                LibraryOfRuina.ui.LibraryHealthBarForecastColors.Corrosion,
                CorrosionFollowUpRules.DamageProps,
                Order: 10,
                Dealer: GetTurnEndDamageDealer())
        ];

    public Creature? GetTurnEndDamageDealer() => IsAppliedByWrathSource ? Applier : Owner;

    public Creature? GetHitFollowUpDealer(Creature? attacker) => IsAppliedByWrathSource ? Applier : attacker;

    public IDisposable EnterDamageSourceScope() =>
        IsAppliedByWrathSource
            ? WrathServantDamageSourceContext.Enter()
            : CorrosionFollowUpRules.NoSourceScope;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || side != Owner.Side || Amount <= 0)
        {
            return;
        }

        Flash();
        using (EnterDamageSourceScope())
        {
            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner,
                Amount,
                CorrosionFollowUpRules.DamageProps,
                GetTurnEndDamageDealer(),
                null);
        }

        await PowerCmd.Decrement(this);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || Owner.IsDead
            || !CorrosionFollowUpRules.TriggersOnHit(Amount, result.TotalDamage, props))
        {
            return;
        }

        Flash();
        using (EnterDamageSourceScope())
        {
            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner,
                Amount,
                CorrosionFollowUpRules.DamageProps,
                GetHitFollowUpDealer(dealer),
                null);
        }
    }
}

/// <summary>
/// 手杖 - 被"汝终将崩溃……！"攻击时攻击威力额外+层数×3。
/// 实际加成在 GreenStemHermit 的 Move5 中直接读取。
/// </summary>
public sealed class WrathServantStaffMarkPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WRATH_SERVANT_STAFF_MARK_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public const int DamageMultiplier = 3;
}

/// <summary>
/// 异界的罪人！ - 累计伤害追踪器（从阈值往下计数）。
/// 当计数≤0且场上存在隐士之杖时，标记下回合使用群体攻击。
/// </summary>
public sealed class WrathServantSinnerCounterPower : LibraryOfRuinaPowerModel
{
    public const int DamageThreshold = 60;

    protected override string LegacyPowerId => "WRATH_SERVANT_SINNER_COUNTER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || 
            Owner.IsDead || 
            result.UnblockedDamage <= 0 || 
            dealer == null ||
            (dealer is not { Monster: GreenStemHermit } && 
            dealer.Monster is not HermitStaff))
        {
            return Task.CompletedTask;
        }

        var newAmount = Math.Max(0, Amount - result.UnblockedDamage);
        SetAmount(newAmount);

        if (newAmount > 0 || !WrathServantEncounterHelper.StaffsExist(Owner.CombatState)) return Task.CompletedTask;
        if (Owner.Monster is monsters.WrathServant.WrathServant servant)
        {
            servant.MarkPendingSpecialAttack();
        }

        Flash();
        SetAmount(DamageThreshold);
        return Task.CompletedTask;
    }

    
}

/// <summary>
/// 今日的演剧 - 只有愤怒侍从可打倒青林隐士。若青林隐士被打倒，则玩家获得胜利。
/// 挂在 GreenStemHermit 上的死亡追踪能力。
/// </summary>
public sealed class WrathServantTodayPlayPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WRATH_SERVANT_TODAY_PLAY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        await WrathServantEncounterHelper.CheckSpecialDeathOutcome(
            choiceContext, creature, WrathServantDeathContext.Consume(creature), wasRemovalPrevented);
    }
}

/// <summary>
/// 被利用之人 - 玩家伤害无法使HP低于最大HP的10% + 每回合结束若HP不高于阈值则恢复50%最大HP。
/// </summary>
public sealed class GreenStemHermitProtectionPower : LibraryOfRuinaPowerModel, LibraryOfRuina.helpers.IFinalHpLossClamp
{
    //public const int MinimumHpPercent = 10;
    public const int RecoveryPercent = 15;

    protected override string LegacyPowerId => "GREEN_STEM_HERMIT_PROTECTION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    internal bool IsHealthBarLockActive => Owner.CurrentHp <= MinimumHp;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new MinimumHpVar(),
        new DynamicVar("RecoveryPercent", RecoveryPercent)
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || side != CombatSide.Enemy)
        {
            return;
        }

        if (Owner.CurrentHp <= MinimumHp)
        {
            int recoveryAmount = Math.Max(1, (int)Math.Ceiling(Owner.MaxHp * RecoveryPercent / 100m));
            Flash();
            await CreatureCmd.Heal(Owner, recoveryAmount);
        }
    }

    /// <summary>
    /// 限制玩家造成的伤害不能使HP低于MinimumHp。
    /// 侍从造成的伤害不受限制。
    /// </summary>

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || Owner.IsDead || amount <= 0m)
        {
            return amount;
        }

        if (IsWrathServantSource(dealer))
        {
            return amount;
        }

        return Math.Min(amount, Math.Max(0m, Owner.CurrentHp - MinimumHp));
    }

    private int MinimumHp => GetMinimumHp(Owner);

    private static int GetMinimumHp(Creature owner)
    {
        return 20;
    }

    private static bool IsWrathServantSource(Creature? dealer)
    {
        return dealer?.Monster is monsters.WrathServant.WrathServant || WrathServantDamageSourceContext.IsActive;
    }

    private sealed class MinimumHpVar : DynamicVar
    {
        public MinimumHpVar()
            : base("MinHp", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is GreenStemHermitProtectionPower { IsMutable: true } power
                ? GetMinimumHp(power.Owner)
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }
}

/// <summary>
/// 亲爱的朋友 - 免疫烧伤、流血与腐蚀（描述性标记能力，实际免疫通过拦截实现）。
/// 在 AfterApplied 时检查并移除被免疫的负面效果。
/// </summary>
/// <summary>
/// 死亡归因追踪（类似 LittleRedDeathContext）
/// </summary>
internal static class WrathServantDeathContext
{
    private static readonly Dictionary<Creature, Creature?> DealersByDeadCreature = [];

    public static void Clear()
    {
        DealersByDeadCreature.Clear();
    }

    public static void Record(Creature deadCreature, Creature? dealer)
    {
        DealersByDeadCreature[deadCreature] = dealer;
    }

    public static Creature? Consume(Creature deadCreature)
    {
        if (!DealersByDeadCreature.Remove(deadCreature, out Creature? dealer))
        {
            return null;
        }

        return dealer;
    }
}
