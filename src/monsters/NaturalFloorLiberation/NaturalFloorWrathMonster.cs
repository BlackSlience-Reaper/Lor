using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryLib.Models;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryOfRuina.powers.WrathServant;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public abstract class NaturalFloorWrathMonster : LorMonsterModel, ITargetedMonsterAttackProvider
{
    internal const string SfxRoot = "res://audio/sfx/natural_floor_liberation/blind_rage/";
    internal const float HitTime = 0.96f; // 自然层怒火阶段：攻击结算等待秒数。
    private Dictionary<string, MoveState> _moves = [];
    private Dictionary<string, string> _restoredState = [];
    private string? _restoredMove;

    protected bool Initializing { get; private set; } = true;

    protected abstract string[] MoveIds { get; }

    protected abstract IEnumerable<AbstractIntent> CreateIntents(int move);

    protected abstract string SelectMove(Rng rng);

    protected abstract Task PerformMove(int move);

    protected abstract IEnumerable<string> VisualAssets { get; }

    internal NaturalFloorLiberationEncounter? Encounter => Creature?.CombatState?.Encounter as NaturalFloorLiberationEncounter;

    protected bool CanAct => Creature.IsAlive && Encounter is { SettlementTriggered: false, TransitionPending: false, SecondPhaseResolutionPending: false };

    public override bool HasDeathSfx => false;

    public override bool ShouldDisappearFromDoom => false;

    protected static int AttackValue(int normal, int ascension) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, ascension, normal);

    protected static int HpValue(int normal, int ascension) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ascension, normal);

    public override IEnumerable<string> AssetPaths => VisualAssets
        .Concat(new[] { "strike", "slash", "thrust", "decay", "special_1", "special_2", "special_3", "hermit_attack", "hermit_strong", "hermit_ground", "meet" }
            .Select(file => SfxRoot + file + ".ogg"))
        .Concat(new[] { "res://images/powers/library_passive_green.png" })
        .Concat(Enumerable.Range(0, MoveIds.Length).SelectMany(i => CreateIntents(i)).SelectMany(i => i.AssetPaths))
        .Distinct();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _moves = [];
        _restoredState = new(_restoredState);
        Initializing = true;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _moves = [];
        var router = new DelegatingMonsterRouterState("NATURAL_WRATH_ROUTER", (_, rng) =>
        {
            if (_restoredMove is { } restored)
            {
                _restoredMove = null;
                return restored;
            }
            return SelectMove(rng);
        });
        for (int i = 0; i < MoveIds.Length; i++)
        {
            int move = i;
            AbstractIntent[] intents = CreateIntents(move).ToArray();
            Func<IReadOnlyList<Creature>, Task> action = _ => PerformMove(move);
            if (intents.OfType<IndiscriminateAttackIntent>().FirstOrDefault() is { } group)
            {
                action = group.WithPreAttackBlockBreak(this, action);
            }

            MoveState state;
            if (MoveIds[move] == "REVIVE_AND_EMPOWER")
            {
                // 盲目怒火在混乱中死亡后仍须承担下一阶段的复活行动。
                state = new LibraryPhaseTransitionMoveState(MoveIds[move], action, intents);
            }
            else
            {
                state = new MoveState(MoveIds[move], action, intents);
            }

            state.FollowUpState = router;
            state.MustPerformOnceBeforeTransitioning = MoveIds[move] == "REVIVE_AND_EMPOWER";
            _moves.Add(MoveIds[move], state);
        }
        return new MonsterMoveStateMachine(_moves.Values.Cast<MonsterState>().Append(router), router);
    }

    protected void ForceMove(int move)
    {
        _ = MoveStateMachine;
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
            await RestoreCombatSnapshot();
            _restoredState.Clear();
        }
        Initializing = false;
    }

    private async Task RestoreCombatSnapshot()
    {
        // Loading a snapshot must not dispatch HP-change or death hooks again.
        Creature.SetMaxHpInternal(ReadInt(_restoredState, "MaxHp", Creature.MaxHp));
        Creature.SetCurrentHpInternal(ReadInt(_restoredState, "Hp", Creature.MaxHp));
        Creature.LoseBlockInternal(Creature.Block);
        Creature.GainBlockInternal(ReadInt(_restoredState, "Block"));
        if (Creature is LibraryCreature library)
        {
            library.SetMaxChaoValueInternal(ReadInt(_restoredState, "MaxChao", library.MaxChaoValue));
            library.SetCurrentChaoValueInternal(ReadInt(_restoredState, "Chao", library.MaxChaoValue));
        }

        int strong = ReadInt(_restoredState, "Strong", 0);
        if (strong > 0)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(Creature, strong, -1, Creature, null);
        }

        int corrosion = ReadInt(_restoredState, "Corrosion", 0);
        if (corrosion > 0)
        {
            await PowerCmdCompat.Apply<WrathServantCorrosionPower>(Creature, corrosion, Creature, null);
        }

        int delayed = ReadInt(_restoredState, "NextCorrosion", 0);
        if (delayed > 0)
        {
            await PowerCmdCompat.Apply<WrathServantNextTurnCorrosionPower>(Creature, delayed, Creature, null);
        }

        if (ReadBool(_restoredState, "Stunned") && Creature is LibraryCreature stunned)
        {
            string? resume = _restoredState.GetValueOrDefault("ResumeMove");
            // Rebuild the saved stun without treating it as a newly inflicted effect.
            stunned.StunInternal(static _ => Task.CompletedTask, MoveIds.Contains(resume) ? resume : MoveIds[0]);
            if (!ReadBool(_restoredState, "StunResistance"))
            {
                stunned.RestorePreStunResistance();
            }

            stunned.SetCurrentChaoValueInternal(ReadInt(_restoredState, "Chao", stunned.MaxChaoValue));
        }
    }

    protected static void Sound(string file) => LocalOggOneShotPlayer.Play(SfxRoot + file + ".ogg", -2f);

    protected async Task<Creature[]> Attack(int damage, int hits, Func<IReadOnlyList<Creature>> targets,
        string[] animations, string[] sounds, Action<Creature, int>? onDamage = null)
    {
        var hitTargets = new List<Creature>();
        for (int i = 0; i < hits && CanAct; i++)
        {
            Creature[] current = targets().Where(static c => c.IsAlive).ToArray();
            if (current.Length == 0)
            {
                break;
            }

            Sound(sounds[i % sounds.Length]);
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, current))
            {
                var attack = await DamageCmd.Attack(damage).FromMonster(this)
                    .WithAttackerAnim(animations[i % animations.Length], HitTime)
                    .WithHitFx("vfx/vfx_attack_blunt").Execute(null);
                foreach (var result in attack.Results.SelectMany(static r => r))
                {
                    if (!hitTargets.Contains(result.Receiver))
                    {
                        hitTargets.Add(result.Receiver);
                    }

                    onDamage?.Invoke(result.Receiver, result.UnblockedDamage);
                }
            }
        }
        return hitTargets.ToArray();
    }

    protected async Task Corrode(IEnumerable<Creature> targets, int amount)
    {
        foreach (Creature target in targets.Where(static c => c.IsAlive))
        {
            if (CanAct)
            {
                await PowerCmdCompat.Apply<WrathServantNextTurnCorrosionPower>(target, amount, Creature, null);
            }
        }
    }

    internal virtual Dictionary<string, string> CaptureState()
    {
        var state = new Dictionary<string, string>
        {
            ["Hp"] = Creature.CurrentHp.ToString(),
            ["MaxHp"] = Creature.MaxHp.ToString(),
            ["Block"] = Creature.Block.ToString(),
            ["Move"] = NextMove.StateId,
            ["Strong"] = (Creature.GetPower<LibraryStrongPower>()?.Amount ?? 0).ToString(),
            ["Corrosion"] = (Creature.GetPower<WrathServantCorrosionPower>()?.Amount ?? 0).ToString(),
            ["NextCorrosion"] = (Creature.GetPower<WrathServantNextTurnCorrosionPower>()?.Amount ?? 0).ToString()
        };
        if (Creature is LibraryCreature library)
        {
            state["Chao"] = library.CurrentChaoValue.ToString();
            state["MaxChao"] = library.MaxChaoValue.ToString();
            state["Stunned"] = (NextMove.StateId == "STUNNED").ToString();
            state["StunResistance"] = library.IsChaoed.ToString();
            state["ResumeMove"] = NextMove.FollowUpStateId ?? MoveIds[0];
        }
        return state;
    }

    internal virtual void RestoreState(Dictionary<string, string> state)
    {
        _restoredState = new(state);
        _restoredMove = state.TryGetValue("Move", out string? move) && MoveIds.Contains(move) ? move : null;
    }

    internal static int ReadInt(Dictionary<string, string> state, string key, int fallback = 0) =>
        state.TryGetValue(key, out string? text) && int.TryParse(text, out int value) ? value : fallback;

    internal static bool ReadBool(Dictionary<string, string> state, string key) =>
        state.TryGetValue(key, out string? text) && bool.TryParse(text, out bool value) && value;

    public virtual bool UsesTargetedAttackContract(Creature owner) => true;

    public abstract IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner);

    public string GetTargetedAttackTargetName(Creature owner) => GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "";
}
