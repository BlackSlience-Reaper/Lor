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
    internal const int SporeAmount = 3;
    internal const string SporeSfxPath = "res://audio/sfx/queen_bee/queen_spore.ogg";

    private bool _tookAttackDamageLastPlayerTurn;
    private HashSet<ulong> _playersGrantedEnergy = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _playersGrantedEnergy = [.. _playersGrantedEnergy];
    }

    protected override string LegacyPowerId => "QUEEN_BEE_SPORES_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
    
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("SporeAmount", SporeAmount),
        new EnergyVar(1)
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

        _tookAttackDamageLastPlayerTurn = true;
        if (dealer.IsPlayer
            && _playersGrantedEnergy.Add(dealer.Player!.NetId))
        {
            await PlayerCmd.GainEnergy(1, dealer.Player);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _playersGrantedEnergy.Clear();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsRoundPlayerTurn(side) || Owner.IsDead)
        {
            return;
        }

        bool shouldApply = _tookAttackDamageLastPlayerTurn;
        _tookAttackDamageLastPlayerTurn = false;
        if (!shouldApply)
        {
            return;
        }

        IReadOnlyList<Creature> players = combatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        if (players.Count == 0)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(SporeSfxPath, -2f);
        await PowerCmdCompat.Apply<HistoryFloorWaspSporePower>(
            players,
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
