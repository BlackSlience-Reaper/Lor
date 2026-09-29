using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.KingOfGreed;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.KingOfGreed;

public sealed class LibraryOfRuinaGoldenAmberPower : LibraryFakeDeathPowerModel
{
    protected override string LegacyPowerId => "GOLDEN_AMBER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is GoldenAmber { WasBroken: true, IsAwakening: false, HasAwakened: false };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        creature == Owner
        && Owner.Monster is GoldenAmber { WasBroken: false, IsAwakening: false, HasAwakened: false };

    protected override Task EnterFakeDeath(Creature creature)
    {
        if (Owner.Monster is GoldenAmber amber)
        {
            amber.MarkBroken();
        }

        return Task.CompletedTask;
    }
}

public sealed class LibraryOfRuinaFlickeringDesirePower :
    LibraryOfRuinaPowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    // 闪烁欲望：魔法少女形态触发变身时保留的生命百分比。
    private const int HpThresholdPercent = 50;

    protected override string LegacyPowerId => "FLICKERING_DESIRE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", HpThresholdPercent)
    ];

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || amount <= 0m
            || Owner.Monster is not monsters.KingOfGreed.KingOfGreed { IsMagicalGirl: true })
        {
            return amount;
        }

        return Math.Min(amount, Math.Max(0m, Owner.CurrentHp - MinimumHp));
    }

    /// <summary>
    /// 锁血下限必须是整数：生命值上限为奇数时 50% 带小数，最后一点被锁住的伤害
    /// 会在结算时截断为 0，生命不变化就不会触发变身，也不显示锁血，魔法少女形态
    /// 因此永久免伤。向上取整与其他锁血效果一致，保证保留至少 50% 生命。
    /// </summary>
    internal static int GetMinimumHp(Creature creature) =>
        (int)Math.Ceiling(creature.MaxHp * HpThresholdPercent / 100m);

    internal bool IsHealthBarLockActive =>
        Owner.Monster is monsters.KingOfGreed.KingOfGreed { IsMagicalGirl: true }
        && Owner.CurrentHp <= MinimumHp;

    private int MinimumHp => GetMinimumHp(Owner);

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner
            || delta >= 0m
            || Owner.Monster is not monsters.KingOfGreed.KingOfGreed king
            || !king.IsMagicalGirl
            || king.IsTransformPending)
        {
            return Task.CompletedTask;
        }

        if (Owner.CurrentHp <= MinimumHp)
        {
            king.MarkFlickeringDesireTriggered();
            if (Owner.CurrentHp < MinimumHp)
            {
                return CreatureCmd.SetCurrentHp(Owner, MinimumHp);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class LibraryOfRuinaSelfIntoxicationPower : LibraryOfRuinaPowerModel
{
    internal const int StrongStacks = 3; // 自我陶醉：群体攻击命中后获得的强壮层数。
    internal const int SwiftStacks = 3; // 自我陶醉：群体攻击命中后获得的守护层数。
    internal const int ProtectionTurns = 1; // 自我陶醉：回合结束时获得的守护持续回合数。

    protected override string LegacyPowerId => "SELF_INTOXICATION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", StrongStacks),
        new DynamicVar("Swift", SwiftStacks),
        new DynamicVar("ProtectionTurns", ProtectionTurns)
    ];
}

public sealed class LibraryOfRuinaMomentaryHappinessPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "MOMENTARY_HAPPINESS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LibraryOfRuinaKingOfGreedPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "KING_OF_GREED_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner
            || result.UnblockedDamage <= 0m
            || target.IsDead
            || props.HasFlag(ValueProp.Unpowered))
        {
            return;
        }

        LibraryBleedingPower? bleed = target.GetPower<LibraryBleedingPower>();
        int bleedStacks = bleed?.Amount ?? 0;
        if (bleedStacks <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Owner, bleedStacks));
    }
}

public sealed class LibraryOfRuinaGluttonyPower : LibraryOfRuinaPowerModel
{
    internal const int HealAmount = 25;

    protected override string LegacyPowerId => "GLUTTONY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Heal", HealAmount)
    ];
}

public sealed class LibraryOfRuinaShiningHappinessPower : LibraryOfRuinaPowerModel
{
    private Creature? _auraTarget;
    private bool _auraApplied;
    private bool _rewardGrantedOnDeath;

    protected override string LegacyPowerId => "SHINING_HAPPINESS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", ShiningHappiness.KingStrongStacks),
        new DynamicVar("Endurance", ShiningHappiness.KingEnduranceStacks),
        new DynamicVar("Turns", ShiningHappiness.KingBuffTurns),
        new DynamicVar("Cards", ShiningHappiness.DeathShardCount)
    ];

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await RemoveAuraContribution();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented)
        {
            return;
        }

        if (creature == Owner)
        {
            await RemoveAuraContribution();
            if (!_rewardGrantedOnDeath)
            {
                _rewardGrantedOnDeath = true;
                await GrantHappinessShardsToAllPlayers(ShiningHappiness.DeathShardCount);
            }

            return;
        }

        if (creature == _auraTarget && _auraApplied)
        {
            _auraApplied = false;
            _auraTarget = null;
        }
    }

    internal static async Task ReapplyAllAuraContributions(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (LibraryOfRuinaShiningHappinessPower power in combatState.Enemies
                     .Select(static creature => creature.GetPower<LibraryOfRuinaShiningHappinessPower>())
                     .OfType<LibraryOfRuinaShiningHappinessPower>()
                     .Where(static power => power.Owner.IsAlive)
                     .ToArray())
        {
            if (!power._auraApplied)
            {
                await power.ApplyAuraContribution();
            }
        }
    }

    internal async Task ApplyAuraContribution()
    {
        if (!Owner.IsAlive || _auraApplied)
        {
            return;
        }

        Creature? target = ResolveKingOfGreedTarget();
        if (target == null)
        {
            return;
        }

        await AddPermanentAuraPower<LibraryStrongPower>(target, ShiningHappiness.KingStrongStacks);
        await AddPermanentAuraPower<LibraryEndurancePower>(target, ShiningHappiness.KingEnduranceStacks);
        _auraTarget = target;
        _auraApplied = true;
    }

    private async Task RemoveAuraContribution()
    {
        if (!_auraApplied || _auraTarget == null)
        {
            return;
        }

        Creature target = _auraTarget;
        _auraApplied = false;
        _auraTarget = null;

        await RemovePermanentAuraPower<LibraryStrongPower>(target, ShiningHappiness.KingStrongStacks);
        await RemovePermanentAuraPower<LibraryEndurancePower>(target, ShiningHappiness.KingEnduranceStacks);
    }

    private Task AddPermanentAuraPower<TPower>(Creature target, int amount)
        where TPower : LibraryTurnsPowerModel => ShiningHappinessAura.Add<TPower>(target, amount, Owner);

    private Task RemovePermanentAuraPower<TPower>(Creature target, int amount)
        where TPower : LibraryTurnsPowerModel => ShiningHappinessAura.Remove<TPower>(target, amount, Owner);

    private async Task GrantHappinessShardsToAllPlayers(int count)
    {
        IReadOnlyList<Creature> players = Owner.CombatState?.Players
            .Select(player => player.Creature)
            .Where(creature => creature.IsAlive)
            .ToArray() ?? [];

        if (players.Count == 0 || count <= 0)
        {
            return;
        }

        await CardPileCmdCompat.AddToCombatAndPreview<HappinessShard>(
            players,
            PileType.Hand,
            count,
            addedByPlayer: false);
    }

    private Creature? ResolveKingOfGreedTarget()
    {
        return Owner.CombatState?.Enemies
            .FirstOrDefault(static creature =>
                creature.IsAlive
                && creature.Monster is monsters.KingOfGreed.KingOfGreed);
    }

}
