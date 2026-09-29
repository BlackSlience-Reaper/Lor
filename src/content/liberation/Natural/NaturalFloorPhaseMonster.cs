using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryLib.Models;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public abstract class NaturalFloorPhaseMonster : LorMonsterModel
{
    protected const string RouterStateId = "NATURAL_DESPAIR_ROUTER";
    private static readonly JsonSerializerOptions StateJson = new() { IncludeFields = true };
    private Dictionary<string, MoveState> _moves = [];
    private Dictionary<string, string> _restoredState = [];
    private string? _restoredMove;

    protected abstract string[] MoveIds { get; }

    protected abstract IEnumerable<AbstractIntent> CreateIntents(int move);

    protected abstract int SelectMove();

    protected abstract Task PerformMove(int move, IReadOnlyList<Creature> targets);

    protected abstract Task ApplyPassives();

    private MoveState CreateMoveState(int move)
    {
        if (MoveIds[move] == "PHASE_END")
        {
            // 泪滴之剑、闪金冲锋的阶段结算必须越过混乱锁。
            return new LibraryPhaseTransitionMoveState(
                MoveIds[move],
                targets => PerformMove(move, targets),
                CreateIntents(move).ToArray());
        }

        return new MoveState(MoveIds[move], targets => PerformMove(move, targets), CreateIntents(move).ToArray());
    }

    protected virtual void RebaseRounds(int offset)
    {
    }

    protected abstract IEnumerable<string> VisualAssets { get; }

    internal NaturalFloorLiberationEncounter? Encounter => Creature?.CombatState?.Encounter as NaturalFloorLiberationEncounter;

    protected bool CanAct => Creature.IsAlive && Encounter is { SettlementTriggered: false, TransitionPending: false };

    public override bool HasDeathSfx => false;

    public override bool ShouldDisappearFromDoom => false;

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => moveStateId is not ("PHASE_END" or "FALSE_DEATH_HIDDEN");

    protected static int DamageValue(int normal, int high) => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, high, normal);

    protected static int HpValue(int normal, int high) => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, high, normal);

    public override IEnumerable<string> AssetPaths => VisualAssets
        .Concat(Enumerable.Range(0, MoveIds.Length)
            .SelectMany(CreateIntents)
            .SelectMany(intent => intent.AssetPaths))
        .Distinct();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _moves = [];
        _restoredState = new(_restoredState);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _moves = [];
        var router = new DelegatingMonsterRouterState(RouterStateId, (_, _) =>
        {
            if (_restoredMove is { } restored)
            {
                _restoredMove = null;
                return restored;
            }
            return MoveIds[SelectMove()];
        });
        for (int i = 0; i < MoveIds.Length; i++)
        {
            int move = i;
            MoveState state = CreateMoveState(move);
            state.FollowUpState = router;
            state.MustPerformOnceBeforeTransitioning = true;
            _moves.Add(MoveIds[i], state);
        }
        return new MonsterMoveStateMachine(_moves.Values.Cast<MonsterState>().Append(router), router);
    }

    protected void ForceMove(int move)
    {
        _ = MoveStateMachine;
        _restoredMove = null;
        SetMoveImmediate(_moves[MoveIds[move]], forceTransition: true);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Encounter is { } encounter && Creature.CombatState is { } combat)
        {
            await encounter.EnsureControllerPowers(combat);
        }

        if (_restoredState.Count > 0)
        {
            RestoreCombatSnapshot();
            _restoredState.Clear();
        }
        await ApplyPassives();
    }

    private void RestoreCombatSnapshot()
    {
        RebaseRounds((Creature.CombatState?.RoundNumber ?? 1) - ReadInt("Round", Creature.CombatState?.RoundNumber ?? 1));
        // Set the snapshot directly: restoring zero HP must not replay death effects.
        Creature.SetMaxHpInternal(ReadInt("MaxHp", Creature.MaxHp));
        Creature.SetCurrentHpInternal(ReadInt("Hp", Creature.MaxHp));
        Creature.LoseBlockInternal(Creature.Block);
        Creature.GainBlockInternal(ReadInt("Block"));
        if (Creature is LibraryCreature library)
        {
            library.SetMaxChaoValueInternal(ReadInt("MaxChao", library.MaxChaoValue));
            if (_restoredState.TryGetValue("Resistance", out string? data))
            {
                var levels = JsonSerializer.Deserialize<int[]>(data)!;
                for (int i = 0; i < 3; i++)
                {
                    var type = new[] { LibraryDamageType.Slash, LibraryDamageType.Pierce, LibraryDamageType.Blunt }[i];
                    library.SetPhysicalResistance(type, (LibraryResistanceLevel)levels[i]);
                    library.SetChaoResistance(type, (LibraryResistanceLevel)levels[i + 3]);
                }
            }
            if (ReadInt("StunPending") != 0)
            {
                if (this is NaturalFloorNihilMonster nihil)
                {
                    nihil.RestoringStunTurns = ReadInt("NihilStunTurns", 1);
                }

                // 声明了路由恢复的怪物在恢复时重新选招，存档里的 ResumeMove 只服务于其余怪物。
                library.StunInternal(
                    static _ => Task.CompletedTask,
                    StunRecoveryStateId ?? _restoredState.GetValueOrDefault("ResumeMove", MoveIds[SelectMove()]));
            }

            library.SetCurrentChaoValueInternal(ReadInt("Chao", library.MaxChaoValue));
        }
        if (_restoredState.TryGetValue("Powers", out string? powers))
        {
            foreach (PowerModel power in Creature.Powers.ToArray())
            {
                power.RemoveInternal();
            }

            foreach (PowerState saved in JsonSerializer.Deserialize<PowerState[]>(powers, StateJson) ?? [])
            {
                PowerModel? canonical = ModelDb.GetByIdOrNull<PowerModel>(ModelId.Deserialize(saved.Id));
                if (canonical == null)
                {
                    continue;
                }

                PowerModel power = canonical.ToMutable();
                power.Applier = Creature;
                power.ApplyInternal(Creature, saved.Amount, silent: true);
                saved.Properties?.Fill(power);
                power.SkipNextDurationTick = saved.SkipTick;
                if (power is LibraryDurationPowerModel duration)
                {
                    duration.SetTurnsRemaining(saved.Turns, notifyDisplay: false);
                }

                if (power is LibraryTurnsPowerModel turns)
                {
                    turns.AmountPlan = new SortedDictionary<int, int>(saved.Plan.ToDictionary(p => (Creature.CombatState?.RoundNumber ?? 0) + p.Key, p => p.Value));
                }
            }
        }
    }

    protected int ReadInt(string key, int fallback = 0) => NaturalFloorWrathMonster.ReadInt(_restoredState, key, fallback);

    internal Dictionary<string, string> CaptureState()
    {
        int round = Creature.CombatState?.RoundNumber ?? 0;
        var state = new Dictionary<string, string>
        {
            ["Model"] = JsonSerializer.Serialize(SavedProperties.From(this), StateJson),
            ["Round"] = round.ToString(),
            ["Hp"] = Creature.CurrentHp.ToString(),
            ["MaxHp"] = Creature.MaxHp.ToString(),
            ["Block"] = Creature.Block.ToString(),
            ["Move"] = NextMove.StateId,
            ["ResumeMove"] = NextMove.FollowUpStateId is { } resume && MoveIds.Contains(resume) ? resume : MoveIds[SelectMove()],
            ["Powers"] = JsonSerializer.Serialize(Creature.Powers.Select(p => new PowerState(p.Id.ToString(), p.Amount,
                p.SkipNextDurationTick, p is LibraryDurationPowerModel d ? d.TurnsRemaining : 0,
                p is LibraryTurnsPowerModel t ? t.AmountPlan.ToDictionary(entry => entry.Key - round, entry => entry.Value) : [],
                SavedProperties.From(p))).ToArray(), StateJson)
        };
        if (Creature is LibraryCreature library)
        {
            state["Chao"] = library.CurrentChaoValue.ToString();
            state["MaxChao"] = library.MaxChaoValue.ToString();
            state["StunPending"] = library.IsChaoed ? "1" : "0";
            if (this is NaturalFloorNihilMonster)
            {
                state["NihilStunTurns"] = library.StunPlayerTurnsRemaining.ToString();
            }
            // Stun's Fatal layer is reconstructed by Stun; preserve the underlying form resistances.
            LibraryCreatureResistanceData.Resistance physical = library.IsChaoed ? DefaultPhysicalResistanceData! : library.ResistanceData.PhysicalResistance;
            LibraryCreatureResistanceData.Resistance chaos = library.IsChaoed ? DefaultChaoResistanceData! : library.ResistanceData.ChaosResistance;
            state["Resistance"] = JsonSerializer.Serialize(new[] { (int)physical.Slash, (int)physical.Pierce, (int)physical.Blunt,
                (int)chaos.Slash, (int)chaos.Pierce, (int)chaos.Blunt });
        }
        return state;
    }

    internal void RestoreState(Dictionary<string, string> state)
    {
        _restoredState = new(state);
        if (state.TryGetValue("Model", out string? model))
        {
            JsonSerializer.Deserialize<SavedProperties>(model, StateJson)?.Fill(this);
        }

        _restoredMove = state.TryGetValue("Move", out string? move) && MoveIds.Contains(move) ? move : null;
    }

    private sealed record PowerState(string Id, int Amount, bool SkipTick, int Turns, Dictionary<int, int> Plan, SavedProperties? Properties);
}
