using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.specialguests;
using LibraryOfRuina.content.specialguests.Iori;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using static LibraryOfRuina.content.reverberation.CryingChildren.CryingChildrenRules;

namespace LibraryOfRuina.content.reverberation.CryingChildren;

public abstract class CryingChildMonsterBase : SpecialGuestMonsterBase
{
    private MoveState? _action;
    private AbstractIntent[]? _intents;

    public bool Overheated { get; private set; }

    protected abstract LibraryResistanceLevel BaselineResistance { get; }

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        Resistance(BaselineResistance);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData =>
        Resistance(BaselineResistance);

    public override bool HasDeathSfx => false;

    public override string? StunRecoveryStateId => "CRYING_ACTION";

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths
            .Concat(CryingChildrenAssets.Paths)
            .Concat(Enum.GetValues<CryingMove>()
                .Where(move => move != CryingMove.None)
                .SelectMany(move => CryingChildrenIntents.Create(this, move).AssetPaths));

    internal static LibraryCreatureResistanceData.Resistance Resistance(LibraryResistanceLevel level) =>
        new() { Slash = level, Pierce = level, Blunt = level };

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _action = null;
        _intents = null;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // AssetPaths 也会调用此方法；运行中的模型必须保持同一组状态和绑定委托。
        if (MoveStateMachine != null)
        {
            return MoveStateMachine;
        }
        _intents = new AbstractIntent[ExpandedIntentCount];
        RefreshIntentArray();
        _action = new MoveState("CRYING_ACTION", PerformPlan, _intents);
        _action.FollowUpState = _action;
        return new MonsterMoveStateMachine([_action], _action);
    }

    protected void InstallPlan(IReadOnlyList<CryingMove> moves)
    {
        ClearStoredIntentPlan();
        int count = Math.Min(moves.Count, Math.Min(IntentCapacity, ExpandedIntentCount));
        for (int slot = 0; slot < count; slot++)
        {
            SetStoredIntent(slot, (int)moves[slot]);
        }
        RefreshIntentArray();
        if (_action != null && Creature is not LibraryCreature { IsChaoed: true })
        {
            SetMoveImmediate(_action, forceTransition: true);
        }
    }

    private void RefreshIntentArray()
    {
        if (_intents == null)
        {
            return;
        }
        for (int slot = 0; slot < _intents.Length; slot++)
        {
            _intents[slot] = CryingChildrenIntents.Create(this, (CryingMove)GetStoredIntent(slot));
        }
    }

    internal Creature[] LivingPlayers() => Creature?.CombatState?.LivingPlayerCreatures()
        .OrderBy(target => target.CombatId)
        .ToArray() ?? [];

    internal int WillHits()
    {
        if (Creature?.CombatState is not { } state)
        {
            return WillMinimumHits;
        }
        long burn = state.Creatures
            .Where(target => target.IsAlive
                && (target.Side == CombatSide.Player
                    || LibraryOfRuina.framework.combat.AllyTurnRegistry.GetAllyType(target)
                        == LibraryOfRuina.framework.combat.AllyType.Friendly))
            .Sum(target => (long)(target.GetPower<LibraryBurnPower>()?.Amount ?? 0));
        long divisor = Math.Max(1, state.RunState.Players.Count) * (long)WillBurnPerPlayer;
        return (int)Math.Min(int.MaxValue, Math.Max(WillMinimumHits, burn / divisor));
    }

    internal int BurnApplied(int amount) =>
        amount + (Creature?.GetPower<CryingSwiftPower>() is { IsActive: true } ? SwiftBurnBonus : 0);

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        await base.AfterPowerAmountChanged(context, power, amount, applier, cardSource);
        // 目标方烧伤改变时重新读取次数；此路径仅刷新 UI，不写状态或消费 RNG。
        if (power is LibraryBurnPower && Creature.IsAlive
            && Creature.GetCreatureNode() is { } node)
        {
            _ = TaskHelper.RunSafely(node.RefreshIntents());
        }
    }

    protected async Task UpdateOverheat()
    {
        bool child = this is UnspeakingChild;
        bool next = (Creature.GetPower<LibraryBurnPower>()?.Amount ?? 0)
            >= (child ? ChildHeatThreshold : PhilipHeatThreshold);
        if (next && !Overheated)
        {
            await PowerCmdCompat.Apply<StrengthPower>(Creature, HeatStrength, Creature, null);
            if (child)
            {
                await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                    Creature, ChildHeatVulnerable, -1, Creature, null);
            }
        }
        Overheated = next;
        RefreshResistances();
    }

    internal void RefreshResistances()
    {
        if (Creature is not LibraryCreature library)
        {
            return;
        }
        LibraryResistanceLevel physical = BaselineResistance;
        if (this is ReverberationPhilip philip)
        {
            physical = philip.Phase switch
            {
                2 => LibraryResistanceLevel.Endure,
                3 => LibraryResistanceLevel.Normal,
                _ => BaselineResistance
            };
        }
        foreach (LibraryDamageType type in new[]
            { LibraryDamageType.Slash, LibraryDamageType.Pierce, LibraryDamageType.Blunt })
        {
            library.SetPhysicalResistance(type, physical);
            library.SetChaoResistance(type, Overheated ? LibraryResistanceLevel.Fatal : BaselineResistance);
        }
    }

    private async Task PerformPlan(IReadOnlyList<Creature> targets)
    {
        for (int slot = 0; slot < ExpandedIntentCount; slot++)
        {
            if (!Creature.IsAlive || Creature is LibraryCreature { IsChaoed: true })
            {
                break;
            }
            CryingMove move = (CryingMove)GetStoredIntent(slot);
            if (move == CryingMove.None)
            {
                break;
            }
            await PerformMove(move);
        }
    }

    private async Task PerformMove(CryingMove move)
    {
        switch (move)
        {
            case CryingMove.ColdSun:
                await GainBlock(ColdSunBlock);
                break;
            case CryingMove.DespairBrand:
                await Attack(move, BrandHits, "Slash");
                if (Creature.IsAlive)
                {
                    GrantEmotionUnits(BrandEmotion);
                }
                break;
            case CryingMove.EmotionalTurbulence:
                await Attack(move, TurbulenceHits, "Strike");
                break;
            case CryingMove.BurningCourage:
                await Cast("Ranged");
                await CardPileCmdCompat.AddToCombatAndPreview<Burn>(LivingPlayers(), PileType.Hand,
                    DamageValue(CourageCards, CourageHighCards), addedByPlayer: false);
                break;
            case CryingMove.SelfRestraint:
                await Attack(move, RestraintHits, "Pierce");
                foreach (Creature target in LivingPlayers())
                {
                    await LibraryPowerCmd.Apply<LibraryWeakPower>(target, RestraintWeak, -1, Creature, null);
                    await LibraryPowerCmd.Apply<LibraryBindingPower>(target, RestraintBinding, -1, Creature, null);
                    await LibraryPowerCmd.Apply<LibraryDisarmPower>(target, RestraintDisarm, -1, Creature, null);
                }
                break;
            case CryingMove.SearingPain:
                await Cast("Ranged");
                await PowerCmdCompat.Apply<IoriCardPlayPainPower>(LivingPlayers(), PainStacks, Creature, null);
                break;
            case CryingMove.FierceMomentum:
                await GainBlock(MomentumBlock);
                await PowerCmdCompat.Apply<CryingSwiftPower>(Creature, MomentumSwift, Creature, null);
                break;
            case CryingMove.BlazingWill:
                // 出招时锁定次数，本次命中追加的烧伤不改变攻击循环。
                int hits = WillHits();
                await Attack(move, hits, "Ranged");
                break;
            case CryingMove.ScorchedAsh:
                await Cast("Special");
                await Attack(move, AshHits, "Ranged", allPlayers: true);
                await PowerCmdCompat.Apply<LibraryBurnPower>(LivingPlayers(), AshBurn, Creature, null);
                break;
            case CryingMove.Murmur:
                await Attack(move, MurmurHits, "Pierce");
                if (Creature.IsAlive)
                {
                    await GainBlock(MurmurBlock);
                }
                break;
            case CryingMove.FoulWings:
                await Cast("Slash");
                await PowerCmdCompat.Apply<StrengthPower>(LivingPlayers(), -WingsStrengthLoss, Creature, null);
                await PowerCmdCompat.Apply<DexterityPower>(LivingPlayers(), -WingsDexterityLoss, Creature, null);
                break;
            case CryingMove.EndlessTorment:
                await Cast("Ranged");
                await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(LivingPlayers(), PileType.Discard,
                    DamageValue(TormentCards, TormentHighCards), addedByPlayer: false);
                break;
        }
    }

    private async Task Attack(CryingMove move, int hits, string animation, bool allPlayers = false)
    {
        IReadOnlyList<Creature> targets = LivingPlayers();
        if (targets.Count == 0 || !Creature.IsAlive)
        {
            return;
        }
        if (allPlayers)
        {
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(
                this, Damage(move), targets, suppressNextDamageHook: false);
        }
        LocalOggOneShotPlayer.Play(CryingChildrenAssets.AttackSound(this, animation));
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, targets))
        {
            var command = await DamageCmd.Attack(Damage(move))
                .WithHitCount(hits)
                .FromMonster(this)
                .WithAttackerAnim(animation, ImpactSeconds)
                .Execute(null);
            RecordDirectAttackDamageDealt(AttackCommandCompat.Results(command));
        }
    }

    private Task Cast(string animation) => CreatureCmd.TriggerAnim(Creature, animation, ActionSeconds);

    private async Task GainBlock(int amount)
    {
        await Cast("Guard");
        await CreatureCmd.GainBlock(Creature, amount, ValueProp.Move, null);
    }
}

public sealed class ReverberationPhilip : CryingChildMonsterBase, IFinalHpLossClamp
{
    public int Phase { get; private set; } = 1;

    public bool TransitionPending { get; private set; }

    public bool ChildrenSpawned { get; private set; }

    public int LastPreparedRound { get; private set; } = -1;

    public override int MinInitialHp => HpValue(PhilipMinHp, PhilipHighMinHp);

    public override int MaxInitialHp => HpValue(PhilipMaxHp, PhilipHighMaxHp);

    public override int DefaultChaoResistance => PhilipChao;

    protected override int InitialIntentCapacity => InitialIntentCount;

    protected override LibraryResistanceLevel BaselineResistance => LibraryResistanceLevel.Resist;

    internal int MinimumHp
    {
        get
        {
            int percent = Phase switch
            {
                1 => FirstHpFloorPercent,
                2 => SecondHpFloorPercent,
                _ => 0
            };
            return (int)Math.Ceiling(Creature.MaxHp * percent / 100m);
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Ensure<CryingNuovoFabricPower>(Creature);
        await PowerCmdCompat.Ensure<CryingPhilipOverheatPower>(Creature);
        await EnsurePhasePower();
        RefreshResistances();
    }

    public override Task AfterStun(Creature creature)
    {
        if (creature == Creature && Phase < 3)
        {
            TransitionPending = true;
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike state)
    {
        await base.BeforeSideTurnStart(context, side, participants, state);
        if (side != CombatSide.Player || !Creature.IsAlive
            || CombatManager.Instance.PlayersTakingExtraTurn.Count > 0
            || LastPreparedRound == state.RoundNumber)
        {
            return;
        }
        LastPreparedRound = state.RoundNumber;
        if (TransitionPending && Phase < 3)
        {
            TransitionPending = false;
            Phase++;
            PatternIndex = 0;
            if (Creature is LibraryCreature library)
            {
                library.RestorePreStunResistance();
                await LibraryCreatureCmd.SetCurrentChaoValue(library, library.MaxChaoValue);
            }
            foreach (PowerModel power in Creature.Powers.Where(power => power.Type == PowerType.Debuff).ToArray())
            {
                await PowerCmd.Remove(power);
            }
            await EnsurePhasePower();
            if (Phase == 2 && !ChildrenSpawned && state.Encounter is CryingChildrenEncounter encounter)
            {
                await encounter.SpawnChildren(state);
                ChildrenSpawned = true;
            }
            await CreatureCmd.TriggerAnim(Creature, "Idle", 0f);
            CryingChildrenAssets.Speak(this, Phase == 2 ? "phase_two" : "phase_three");
        }
        else if (state.RoundNumber == 1)
        {
            CryingChildrenAssets.Speak(this, "opening");
        }
        if (Creature.GetPower<CryingSwiftPower>() is { } swift)
        {
            await swift.ActivateForRound(state.RoundNumber);
        }
        await UpdateOverheat();
        InstallPlan(Pattern(Phase, PatternIndex));
        PatternIndex = (PatternIndex + 1) % PatternRoundCount;
        if (state.Encounter is CryingChildrenEncounter reception)
        {
            await reception.PrepareChildren(state);
        }
    }

    private async Task EnsurePhasePower()
    {
        foreach (CryingPhasePower power in Creature.Powers.OfType<CryingPhasePower>().ToArray())
        {
            if (power.Phase != Phase)
            {
                await PowerCmd.Remove(power);
            }
        }
        switch (Phase)
        {
            case 1:
                await PowerCmdCompat.Ensure<CryingPassionPower>(Creature);
                break;
            case 2:
                await PowerCmdCompat.Ensure<CryingSurgingHeartPower>(Creature);
                break;
            case 3:
                await PowerCmdCompat.Ensure<CryingBlazingBladePower>(Creature);
                break;
        }
    }

    public decimal ClampFinalHpLoss(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) =>
        target == Creature && Phase < 3 && amount > 0
            ? Math.Min(amount, Math.Max(0, Creature.CurrentHp - MinimumHp))
            : amount;

    public override bool ShouldDieLate(Creature creature) => creature != Creature || Phase >= 3;

    public override Task AfterPreventingDeath(Creature creature) =>
        creature == Creature ? EnforceHpFloor() : Task.CompletedTask;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta) =>
        creature == Creature ? EnforceHpFloor() : Task.CompletedTask;

    private Task EnforceHpFloor() =>
        Phase < 3 && Creature.CurrentHp < MinimumHp
            ? CreatureCmd.SetCurrentHp(Creature, MinimumHp)
            : Task.CompletedTask;
}

public sealed class UnspeakingChild : CryingChildMonsterBase
{
    public int SpawnRound { get; private set; } = -1;

    public int LastPreparedRound { get; private set; } = -1;

    public override int MinInitialHp => HpValue(ChildMinHp, ChildHighMinHp);

    public override int MaxInitialHp => HpValue(ChildMaxHp, ChildHighMaxHp);

    public override int DefaultChaoResistance => ChildChao;

    public override bool HasEmotionTrack => false;

    protected override int InitialIntentCapacity => ChildIntentCount;

    protected override LibraryResistanceLevel BaselineResistance => LibraryResistanceLevel.Endure;

    internal void MarkSpawned(int round) => SpawnRound = round;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (SpawnRound < 0)
        {
            SpawnRound = Creature.CombatState?.RoundNumber ?? 0;
        }
        await PowerCmdCompat.Ensure<CryingHotHeartPower>(Creature);
        await PowerCmdCompat.Ensure<CryingChildOverheatPower>(Creature);
        RefreshResistances();
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike state)
    {
        await base.BeforeSideTurnStart(context, side, participants, state);
        if (side == CombatSide.Player && Creature.IsAlive
            && CombatManager.Instance.PlayersTakingExtraTurn.Count == 0
            && state.Encounter is CryingChildrenEncounter encounter)
        {
            await encounter.PrepareChildren(state);
        }
    }

    internal async Task PrepareRound(int round, CryingMove move)
    {
        if (LastPreparedRound == round)
        {
            return;
        }
        LastPreparedRound = round;
        if (round > SpawnRound)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(Creature, ChildGrowthStrong, -1, Creature, null);
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(Creature, ChildGrowthEndurance, -1, Creature, null);
        }
        await UpdateOverheat();
        InstallPlan([move]);
    }
}
