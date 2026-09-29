using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers.HistoryFloorLiberation;
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

namespace LibraryOfRuina.powers.QueenBee;

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
        if (side != CombatSide.Player || Owner.IsDead)
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
        return HealthBarForecasts.Single(
            QueenBeeWorkerDeathEmbracePassivePower.HpThresholdPercent * context.Creature.MaxHp, // 展示的数量（例如如果你的能力有2倍效果可以乘2）
            new Color(0.4f, 0.2f, 0.0f), // 颜色
            HealthBarForecastGrowthDirection.FromLeft // 从左边开始延伸还是右边开始
            // 0, // 顺序，越大越远离血条边缘，默认0
            // PreloadManager.Cache.GetMaterial("res://xxx.tres") // 如果需要自定义材质
        );
    }
}
