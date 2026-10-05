using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.specialguests;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using static LibraryOfRuina.content.reverberation.GearChurch.GearChurchRules;

namespace LibraryOfRuina.content.reverberation.GearChurch;

public abstract class GearChurchMonsterBase : SpecialGuestMonsterBase, ITargetedMonsterAttackProvider
{
    private MoveState? _action;
    private AbstractIntent[]? _intents;

    public int TargetCombatId { get; private set; } = -1;

    public int LastPerformedMove { get; private set; } = -1;

    public int NextMoveSlot { get; private set; }

    public int LastPreparedRound { get; protected set; } = -1;

    public override bool HasDeathSfx => false;

    public override string? StunRecoveryStateId => "GEAR_CHURCH_ACTION";

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths
            .Concat(GearChurchAssets.Paths)
            .Concat(Enum.GetValues<GearChurchMove>()
                .Where(move => move != GearChurchMove.None)
                .SelectMany(move => GearChurchIntents.Create(this, move).AssetPaths));

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
        if (MoveStateMachine != null)
        {
            return MoveStateMachine;
        }
        _intents = new AbstractIntent[StoredIntentSlots];
        RefreshIntents();
        _action = new MoveState("GEAR_CHURCH_ACTION", PerformPlan, _intents);
        _action.FollowUpState = _action;
        return new MonsterMoveStateMachine([_action], _action);
    }

    protected void InstallPlan(IReadOnlyList<GearChurchMove> moves)
    {
        ClearStoredIntentPlan();
        int count = Math.Min(moves.Count, Math.Min(IntentCapacity, StoredIntentSlots));
        for (int slot = 0; slot < count; slot++)
        {
            SetStoredIntent(slot, (int)moves[slot]);
        }
        NextMoveSlot = 0;
        Creature[] players = LivingPlayers();
        bool needsTarget = moves.Take(count).Any(move => Damage(move) > 0 && move != GearChurchMove.Brainwash);
        TargetCombatId = needsTarget && players.Length > 0
            ? checked((int)(players[RunRng.MonsterAi.NextInt(players.Length)].CombatId ?? 0))
            : -1;
        RefreshIntents();
        if (_action != null && Creature is not LibraryCreature { IsChaoed: true })
        {
            SetMoveImmediate(_action, forceTransition: true);
        }
    }

    private void RefreshIntents()
    {
        if (_intents == null)
        {
            return;
        }
        for (int slot = 0; slot < _intents.Length; slot++)
        {
            _intents[slot] = GearChurchIntents.Create(this, (GearChurchMove)GetStoredIntent(slot));
        }
    }

    internal Creature[] LivingPlayers() =>
        Creature?.CombatState?.LivingPlayerCreatures()
            .OrderBy(creature => creature.CombatId)
            .ToArray() ?? [];

    internal Creature? SelectedTarget() =>
        LivingPlayers().FirstOrDefault(creature => creature.CombatId == TargetCombatId)
        ?? LivingPlayers().FirstOrDefault();

    bool ITargetedMonsterAttackProvider.UsesTargetedAttackContract(Creature owner) => true;

    IReadOnlyList<Creature> ITargetedMonsterAttackProvider.GetTargetedAttackTargets(Creature owner) =>
        SelectedTarget() is { } target ? [target] : [];

    string ITargetedMonsterAttackProvider.GetTargetedAttackTargetName(Creature owner) =>
        SelectedTarget()?.Name ?? string.Empty;

    internal Creature[] LivingChurch() =>
        Creature.CombatState?.Enemies
            .Where(creature => creature.IsAlive && creature.Monster is GearChurchMonsterBase)
            .OrderBy(creature => creature.CombatId)
            .ToArray() ?? [];

    private async Task PerformPlan(IReadOnlyList<Creature> targets)
    {
        while (NextMoveSlot < StoredIntentSlots && Creature.IsAlive
            && Creature is not LibraryCreature { IsChaoed: true })
        {
            GearChurchMove move = (GearChurchMove)GetStoredIntent(NextMoveSlot);
            if (move == GearChurchMove.None)
            {
                break;
            }
            NextMoveSlot++;
            LastPerformedMove = (int)move;
            await PerformMove(move);
        }
    }

    private async Task PerformMove(GearChurchMove move)
    {
        switch (move)
        {
            case GearChurchMove.ThoughtAcceleration:
                await Cast("SpecialOne");
                await BuffChurch(ThoughtAccelerationVigor, ThoughtAccelerationPlating);
                await BlockChurch(ThoughtAccelerationBlock);
                break;
            case GearChurchMove.ThoughtReveal:
                await Attack(move, "Strike");
                await BuffChurch(RevealVigor, RevealPlating);
                break;
            case GearChurchMove.ThoughtProselytize:
                await Cast("SpecialOne");
                await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(
                    LivingPlayers(), PileType.Draw, ProselytizeCardCount, addedByPlayer: false);
                await BuffChurch(ProselytizeVigor, ProselytizePlating);
                break;
            case GearChurchMove.FleshEncourage:
                await Cast("SpecialTwo");
                foreach (Creature target in LivingChurch())
                {
                    await CreatureCmd.Heal(target, decimal.Ceiling(target.MaxHp * EncourageHealPercent / 100m));
                }
                break;
            case GearChurchMove.FleshStrengthen:
                await Cast("SpecialOne");
                foreach (Creature target in LivingChurch())
                {
                    await LibraryPowerCmd.Apply<LibraryStrongPower>(target,
                        DamageValue(StrengthenStrong, StrengthenHighStrong), -1, Creature, null);
                }
                await BlockChurch(StrengthenBlock);
                break;
            case GearChurchMove.FleshAcceleration:
                await Cast("Guard");
                await CreatureCmd.GainBlock(Creature, FleshAccelerationBlock, ValueProp.Move, null);
                await BuffChurch(FleshAccelerationVigor, FleshAccelerationPlating);
                break;
            case GearChurchMove.Brainwash:
                await Attack(move, "SpecialTwo", allPlayers: true);
                if (Creature.IsAlive)
                {
                    await PowerCmdCompat.Apply<RingingPower>(LivingPlayers(), BrainwashRinging, Creature, null);
                }
                break;
            case GearChurchMove.Guidance:
                await Attack(move, "Slash");
                await GainSmoke(GuidanceSmoke);
                break;
            case GearChurchMove.DefenseInstruction:
                await Attack(move, "Strike");
                if (Creature.IsAlive)
                {
                    await CreatureCmd.GainBlock(Creature, DefenseBlock, ValueProp.Move, null);
                    await GainSmoke(DefenseSmoke);
                }
                break;
            case GearChurchMove.Assault:
                await Attack(move, "Pierce");
                await GainSmoke(AssaultSmoke);
                break;
            case GearChurchMove.SteamEruption:
                await Attack(move, "SpecialTwo");
                await GainSmoke(SteamSmoke);
                break;
        }
    }

    private async Task Attack(GearChurchMove move, string animation, bool allPlayers = false)
    {
        Creature[] targets;
        if (allPlayers)
        {
            targets = LivingPlayers();
        }
        else
        {
            targets = SelectedTarget() is { } target ? [target] : [];
        }
        if (targets.Length == 0 || !Creature.IsAlive)
        {
            return;
        }
        if (allPlayers)
        {
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(
                this, Damage(move), targets, suppressNextDamageHook: false);
        }
        LocalOggOneShotPlayer.Play(GearChurchAssets.AttackSound(this, animation));
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, targets))
        {
            var command = await DamageCmd.Attack(Damage(move))
                .WithHitCount(Hits(move))
                .FromMonster(this)
                .WithAttackerAnim(animation, ActionSeconds)
                .Execute(null);
            RecordDirectAttackDamageDealt(AttackCommandCompat.Results(command));
        }
    }

    protected Task Cast(string animation) =>
        CreatureCmd.TriggerAnim(Creature, animation, ActionSeconds);

    private async Task BuffChurch(int vigor, int plating)
    {
        if (!Creature.IsAlive)
        {
            return;
        }
        await PowerCmdCompat.Apply<NextTurnVigorPower>(LivingChurch(), vigor, Creature, null);
        await PowerCmdCompat.Apply<PlatingPower>(LivingChurch(), plating, Creature, null);
    }

    private async Task BlockChurch(int amount)
    {
        foreach (Creature target in LivingChurch())
        {
            await CreatureCmd.GainBlock(target, amount, ValueProp.Move, null);
        }
    }

    private Task GainSmoke(int amount) =>
        Creature.IsAlive
            ? PowerCmdCompat.Apply<GearChurchSmokePower>(Creature, amount, Creature, null)
            : Task.CompletedTask;

    public override Task AfterDeath(PlayerChoiceContext context, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (this is not ReverberationEileen eileen)
        {
            return Task.CompletedTask;
        }
        if (!wasRemovalPrevented && creature == Creature)
        {
            GearChurchAssets.Speak(eileen, "death");
        }
        if (!wasRemovalPrevented && creature.IsPlayer && Creature.IsAlive)
        {
            GearChurchAssets.Speak(eileen, LivingPlayers().Length == 0 ? "victory" : "kill");
        }
        return Task.CompletedTask;
    }
}

public sealed class ReverberationEileen : GearChurchMonsterBase, IFinalHpLossClamp, ILibraryAbstractModel
{
    private bool _wasChaoedBeforeStun;

    public int Phase { get; private set; } = 1;

    public bool TransitionPending { get; private set; }

    public int PhaseFollowerDeaths { get; private set; }

    public int LastFollowerDeathRound { get; private set; } = -1;

    public int LastChaoRound { get; private set; } = -1;

    public override int MinInitialHp => HpValue(EileenMinHp, EileenHighMinHp);

    public override int MaxInitialHp => HpValue(EileenMaxHp, EileenHighMaxHp);

    public override int DefaultChaoResistance => EileenChao;

    protected override int InitialIntentCapacity => EileenInitialCapacity;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        Resistance(LibraryResistanceLevel.Endure);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData =>
        Resistance(LibraryResistanceLevel.Immune);

    internal int HpFloorPercent => Phase switch
    {
        1 => FirstHpFloorPercent,
        2 => SecondHpFloorPercent,
        _ => 0
    };

    internal int MinimumHp => (int)Math.Ceiling(Creature.MaxHp * HpFloorPercent / 100m);

    internal bool IsHealthBarLockActive => Phase < FinalPhase && Creature.CurrentHp <= MinimumHp;

    internal int ScaledDeathChaoDamage =>
        (int)Math.Ceiling(MultiplayerScalingPatchHelper.ScaleHpAmount(Creature.CombatState, this, DeathChaoDamage));

    public Task BeforeStun(Creature creature)
    {
        if (creature == Creature && creature is LibraryCreature library)
        {
            _wasChaoedBeforeStun = library.IsChaoed;
        }
        return Task.CompletedTask;
    }

    public Task AfterStun(Creature creature)
    {
        if (creature == Creature && !_wasChaoedBeforeStun
            && creature is LibraryCreature { IsChaoed: true }
            && creature.CombatState is { } state)
        {
            LastChaoRound = state.RoundNumber;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Ensure<EileenNuovoFabricPower>(Creature);
        await PowerCmdCompat.Ensure<EileenFleshRebirthPower>(Creature);
        if (Phase < FinalPhase)
        {
            await PowerCmdCompat.Ensure<EileenPrestigePower>(Creature);
        }
        else
        {
            await PowerCmdCompat.RemoveIfPresent<EileenPrestigePower>(Creature);
        }
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike state)
    {
        await base.BeforeSideTurnStart(context, side, participants, state);
        if (side != CombatSide.Player || !Creature.IsAlive
            || CombatManager.Instance.PlayersTakingExtraTurn.Count > 0)
        {
            return;
        }
        await PrepareRound(state);
    }

    internal async Task PrepareRound(CombatStateLike state)
    {
        if (LastPreparedRound == state.RoundNumber || state.Encounter is not GearChurchEncounter encounter)
        {
            return;
        }
        LastPreparedRound = state.RoundNumber;
        Creature.GetPower<EileenNuovoFabricPower>()?.ResetForRound(state.RoundNumber);
        if (TransitionPending && Phase < FinalPhase)
        {
            Phase++;
            TransitionPending = false;
            if (Phase == FinalPhase)
            {
                await PowerCmdCompat.RemoveIfPresent<EileenPrestigePower>(Creature);
            }
            foreach (PowerModel power in Creature.Powers
                .Where(power => power.GetTypeForAmount(power.Amount) == PowerType.Debuff).ToArray())
            {
                await PowerCmd.Remove(power);
            }
            await RestoreChao(Creature);
            foreach (GearChurchFollower follower in GearChurchEncounter.Followers(state))
            {
                await CreatureCmd.Heal(follower.Creature, follower.Creature.MaxHp - follower.Creature.CurrentHp);
                await RestoreChao(follower.Creature);
            }
            await encounter.SpawnFollowers(state, PhaseFollowerDeaths);
            PhaseFollowerDeaths = 0;
            InstallPlan([GearChurchMove.Brainwash]);
            await Cast("SpecialTwo");
        }
        else if (GearChurchEncounter.Followers(state).Length == 0
            && LastChaoRound != state.RoundNumber - 1)
        {
            await encounter.SpawnFollowers(state, OpeningFollowers);
            InstallRegularPlan();
        }
        else if (LastFollowerDeathRound == state.RoundNumber - 1)
        {
            var flesh = new List<GearChurchMove>
            {
                GearChurchMove.FleshStrengthen, GearChurchMove.FleshEncourage, GearChurchMove.FleshAcceleration
            };
            RunRng.MonsterAi.Shuffle(flesh);
            InstallPlan([GearChurchMove.Brainwash, flesh[0], flesh[1]]);
        }
        else
        {
            InstallRegularPlan();
        }
        // 覆盖的死亡响应在本回合消耗，不延迟至之后的回合。
        LastFollowerDeathRound = -1;
        if (state.RoundNumber == 1)
        {
            GearChurchAssets.Speak(this, "opening");
        }
        await encounter.PrepareFollowers(state);
    }

    private void InstallRegularPlan()
    {
        var moves = new List<GearChurchMove>
        {
            GearChurchMove.ThoughtAcceleration, GearChurchMove.ThoughtReveal, GearChurchMove.ThoughtProselytize
        };
        RunRng.MonsterAi.Shuffle(moves);
        if ((int)moves[0] == LastPerformedMove)
        {
            (moves[0], moves[1]) = (moves[1], moves[0]);
        }
        InstallPlan(moves.Take(EileenMaximumMoves).ToArray());
    }

    private static async Task RestoreChao(Creature creature)
    {
        if (creature is LibraryCreature library)
        {
            library.RestorePreStunResistance();
            await LibraryCreatureCmd.SetCurrentChaoValue(library, library.MaxChaoValue);
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext context, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        await base.AfterDeath(context, creature, wasRemovalPrevented, deathAnimLength);
        if (wasRemovalPrevented || !Creature.IsAlive
            || creature.Monster is not GearChurchFollower follower || !follower.TryRecordDeath()
            || Creature.CombatState is not { } state)
        {
            return;
        }
        PhaseFollowerDeaths++;
        LastFollowerDeathRound = state.RoundNumber;
        await LibraryCreatureCmd.ChaoDamage(context, Creature, ScaledDeathChaoDamage,
            ValueProp.Unpowered | ValueProp.Unblockable, Creature, null);
    }

    public decimal ClampFinalHpLoss(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) =>
        target == Creature && Phase < FinalPhase && amount > 0
            ? Math.Min(amount, Math.Max(0, Creature.CurrentHp - MinimumHp))
            : amount;

    public override bool ShouldDieLate(Creature creature) => creature != Creature || Phase >= FinalPhase;

    public override Task AfterPreventingDeath(Creature creature) =>
        creature == Creature ? EnforceHpFloor() : Task.CompletedTask;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta) =>
        creature == Creature ? EnforceHpFloor() : Task.CompletedTask;

    private async Task EnforceHpFloor()
    {
        if (Phase >= FinalPhase || Creature.CurrentHp > MinimumHp)
        {
            return;
        }
        TransitionPending = true;
        if (Creature.CurrentHp < MinimumHp)
        {
            await CreatureCmd.SetCurrentHp(Creature, MinimumHp);
        }
    }
}

public sealed class GearChurchFollower : GearChurchMonsterBase
{
    public bool DeathRecorded { get; private set; }

    public override int MinInitialHp => HpValue(FollowerMinHp, FollowerHighMinHp);

    public override int MaxInitialHp => HpValue(FollowerMaxHp, FollowerHighMaxHp);

    public override int DefaultChaoResistance => FollowerChao;

    protected override int InitialIntentCapacity => FollowerCapacity;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        Resistance(LibraryResistanceLevel.Normal);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData =>
        new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure
        };

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        IntentCapacity = FollowerCapacity;
        await PowerCmdCompat.Ensure<GearChurchSmokeWreathPower>(Creature);
        await PowerCmdCompat.Ensure<GearChurchSoberSmokePower>(Creature);
    }

    protected override async Task ApplyEmotionLevelReward(int level)
    {
        await base.ApplyEmotionLevelReward(level);
        IntentCapacity = FollowerCapacity;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike state)
    {
        await base.BeforeSideTurnStart(context, side, participants, state);
        if (side != CombatSide.Player || !Creature.IsAlive
            || CombatManager.Instance.PlayersTakingExtraTurn.Count > 0
            || state.Encounter is not GearChurchEncounter encounter)
        {
            return;
        }
        ReverberationEileen? eileen = state.LivingEnemies()
            .Select(creature => creature.Monster)
            .OfType<ReverberationEileen>()
            .FirstOrDefault();
        if (eileen != null)
        {
            await eileen.PrepareRound(state);
        }
        await encounter.PrepareFollowers(state);
    }

    internal async Task PrepareRound(int round, GearChurchMove move)
    {
        if (LastPreparedRound == round)
        {
            return;
        }
        LastPreparedRound = round;
        IntentCapacity = FollowerCapacity;
        await PowerCmdCompat.Apply<GearChurchSmokePower>(Creature, RoundSmoke, Creature, null);
        InstallPlan([move]);
    }

    internal bool TryRecordDeath()
    {
        if (DeathRecorded)
        {
            return false;
        }
        DeathRecorded = true;
        return true;
    }
}
