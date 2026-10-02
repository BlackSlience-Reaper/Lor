using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.HealthBars;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

public sealed class QueenBeeSporesPassivePower : LibraryOfRuinaPowerModel, IHealthBarForecastSource
{
    // 孢子被动：玩家回合开始时，对上个玩家回合中造成未被格挡攻击伤害的攻击者施加的孢子层数。
    internal const int SporeAmount = 3;

    // 孢子被动：每位玩家每回合第一次对蜂后造成未被格挡攻击伤害时返还的能量。
    internal const int FirstAttackEnergyRefund = 1;

    internal const string SporeSfxPath = QueenBeeAssets.QueenSporeSfx;

    private List<Creature> _sporeTargets = [];
    private HashSet<ulong> _playersGrantedEnergyThisTurn = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _sporeTargets = [.. _sporeTargets];
        _playersGrantedEnergyThisTurn = [.. _playersGrantedEnergyThisTurn];
    }

    protected override string LegacyPowerId => "QUEEN_BEE_SPORES_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
    
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("SporeAmount", SporeAmount),
        new EnergyVar(FirstAttackEnergyRefund)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HistoryFloorWaspSporePower>()
    ];

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || dealer == null
            || dealer == Owner
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        if (!_sporeTargets.Contains(dealer))
        {
            _sporeTargets.Add(dealer);
        }

        Player? attacker = dealer.Player ?? dealer.PetOwner;
        if (attacker == null)
        {
            return;
        }

        if (_playersGrantedEnergyThisTurn.Add(attacker.NetId))
        {
            await PlayerCmd.GainEnergy(FirstAttackEnergyRefund, attacker);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _sporeTargets.Clear();
        _playersGrantedEnergyThisTurn.Clear();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            // 返能额度按玩家自己的回合计：正常回合开始时 participants 是玩家一侧全体，
            // 额外回合（大鸟书页等）只有取得额外回合的玩家，只重置这些玩家的额度。
            foreach (Creature participant in participants)
            {
                if (participant.Player is { } player)
                {
                    _playersGrantedEnergyThisTurn.Remove(player.NetId);
                }
            }
        }

        // 孢子按轮结算，额外回合不结算。
        if (!TurnParticipants.IsRoundPlayerTurn(side) || Owner.IsDead)
        {
            return;
        }

        IReadOnlyList<Creature> attackers = _sporeTargets
            .Where(creature => !creature.IsDead)
            .ToArray();
        _sporeTargets = [];
        if (attackers.Count == 0)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(SporeSfxPath, -2f);
        await PowerCmdCompat.Apply<HistoryFloorWaspSporePower>(
            attackers,
            SporeAmount,
            Owner,
            null);
    }

    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        Creature creature = context.Creature;
        // 蜂后血条：当前生命严格低于最大生命的 30% 时，才显示低血量颜色。
        decimal thresholdHp = creature.MaxHp * (QueenBeeWorkerDeathEmbracePassivePower.HpThresholdPercent / 100m);
        if (creature.IsDead || creature.CurrentHp <= 0 || creature.CurrentHp >= thresholdHp)
        {
            return [];
        }

        return HealthBarForecasts.Single(
            creature.CurrentHp,
            new Color(0.4f, 0.2f, 0.0f),
            HealthBarForecastGrowthDirection.FromLeft);
    }
}
