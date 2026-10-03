using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;

namespace LibraryOfRuina.content.liberation.Religion;

public enum ReligionFloorOutcome { Pending, Salvation, Explosion }

public sealed partial class ReligionFloorLiberationEncounter : LiberationEncounterBase, IEncounterBgmSource, IFloorLiberationEncounter
{
    private static readonly JsonSerializerOptions StateJson = new() { IncludeFields = true };
    private const float CameraScaling = 0.72f; // 宗教层：容纳空中失乐园和三使徒的战斗缩放。
    private const int MaximumAttackIntents = 2; // 使徒：最多同时显示两个攻击意图。
    private const int MinimumAttackIntents = 1; // 使徒：存在可行动者时至少一个攻击意图。
    private CombatStateLike? _combat;
    private bool _repairingPlans;

    [SavedProperty]
    public int Phase { get; private set; } = 1;

    [SavedProperty]
    public int KilledApostles { get; private set; }

    [SavedProperty]
    public int PendingApostleClockSteps { get; private set; }

    [SavedProperty]
    public int EnemyTurnsCompleted { get; private set; }

    [SavedProperty]
    public int TrialTurnsCompleted { get; private set; }

    [SavedProperty]
    public ReligionFloorOutcome Outcome { get; private set; }

    [SavedProperty]
    public bool Initialized { get; private set; }

    [SavedProperty]
    public bool Completed { get; private set; }

    [SavedProperty]
    public bool Won { get; private set; }

    [SavedProperty]
    public bool IsSettling { get; private set; }

    [SavedProperty]
    public bool RepentanceVisible { get; private set; }

    [SavedProperty]
    public int InitialPlayerCount { get; private set; } = 1;

    internal int MusicIndex => IsSettling && Outcome == ReligionFloorOutcome.Salvation ? 2 : Phase - 1;

    internal ReligionFloorLostParadise? Boss => _combat?.Enemies.Select(static enemy => enemy.Monster).OfType<ReligionFloorLostParadise>().FirstOrDefault();

    internal ReligionFloorApostle[] Apostles => _combat?.Enemies.Select(static enemy => enemy.Monster).OfType<ReligionFloorApostle>().OrderBy(static apostle => apostle.ApostleIndex).ToArray() ?? [];

    internal int ExplosionDamage => (_combat?.Players.Max(static player => player.Creature.MaxHp) ?? 0) * ReligionFloorRules.ExplosionMultiplier;

    internal int ReclaimPercent => ReligionFloorRules.ReclaimPercent(InitialPlayerCount);

    internal int RemainingTrialTurns => Math.Max(0, ReligionFloorRules.TrialTurns - TrialTurnsCompleted);

    public string LiberationFloorId => LiberationFloorIds.Religion;

    public bool IsFullyLiberated => Completed && Won;

    public override RoomType RoomType => RoomType.Boss;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override float GetCameraScaling() => CameraScaling;

    public override Vector2 GetCameraOffset() => new(-100, 50);

    public override IReadOnlyList<string> Slots => ["paradise", "scythe", "spear", "staff"];

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => ReligionFloorAssets.MapIcon;

    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.DynamicSourceBased(
        "ReligionFloorLiberation", [ReligionFloorAssets.FirstMusic, ReligionFloorAssets.SecondMusic, ReligionFloorAssets.SalvationMusic], volumeScale: 1f);

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<ReligionFloorLostParadise>(), ModelDb.Monster<ReligionFloorScytheApostle>(),
        ModelDb.Monster<ReligionFloorSpearApostle>(), ModelDb.Monster<ReligionFloorStaffApostle>()
    ];

    public override IEnumerable<string> ExtraAssetPaths => AllPossibleMonsters.SelectMany(static monster => monster.AssetPaths).Concat(
    [
        ReligionFloorAssets.EncounterScene, ReligionFloorAssets.BackgroundScene, ReligionFloorAssets.PresentationScene,
        ReligionFloorAssets.ClockScene,
        ReligionFloorAssets.MapIcon + ".png", ReligionFloorAssets.MapIcon + "_outline.png",
        ReligionFloorAssets.FirstMusic, ReligionFloorAssets.SecondMusic, ReligionFloorAssets.SalvationMusic
    ]).Concat(ReligionFloorAssets.SoundPaths).Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<ReligionFloorLostParadise>().ToMutable(), "paradise"),
        (ModelDb.Monster<ReligionFloorScytheApostle>().ToMutable(), "scythe"),
        (ModelDb.Monster<ReligionFloorSpearApostle>().ToMutable(), "spear"),
        (ModelDb.Monster<ReligionFloorStaffApostle>().ToMutable(), "staff")
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _combat = null;
        _repairingPlans = false;
        CloneSacrificeState();
    }

    internal void Initialize(CombatStateLike combat)
    {
        _combat = combat;
        if (Initialized)
        {
            RestoreSacrificeReferences();
            return;
        }

        Initialized = true;
        InitialPlayerCount = combat.Players.Count;
        PlanNextTurn();
    }

    internal void RecordApostleKill()
    {
        KilledApostles++;
        PendingApostleClockSteps++;
    }

    internal void CheckSalvation()
    {
        if (Phase == 2 && Outcome == ReligionFloorOutcome.Pending && Boss is { } boss
            && boss.Creature.CurrentHp <= ReligionFloorRules.SecondPhaseMinimumHp(boss.Creature.MaxHp))
        {
            Outcome = ReligionFloorOutcome.Salvation;
            boss.PlanMove(ReligionFloorMonster.SalvationMove);
        }
    }

    internal async Task BeforeTurn(PlayerChoiceContext context, CombatSide side, CombatStateLike combat)
    {
        Initialize(combat);
        if (Completed || IsSettling)
        {
            return;
        }

        await EnsurePhasePowers();

        if (side == CombatSide.Enemy && Outcome != ReligionFloorOutcome.Pending)
        {
            await ResolveOutcome(context);
        }
        else if (side == CombatSide.Player)
        {
            ResetReclaimProgress();
            RepairApostlePlans();
            ReligionFloorPresentation.Refresh(this);
        }
    }

    internal async Task EndEnemyTurn(PlayerChoiceContext context)
    {
        if (Completed || IsSettling || Boss is not { } boss || _combat == null)
        {
            return;
        }

        // 自上次敌方回合结束起累计，包含上个玩家回合及当前敌方回合的每次新增死亡。
        int clockSteps = PendingApostleClockSteps;
        PendingApostleClockSteps = 0;
        await ReligionFloorClockPresentation.Play(KilledApostles - clockSteps, clockSteps);

        EnemyTurnsCompleted++;
        if (KilledApostles >= ReligionFloorRules.RequiredKills)
        {
            IsSettling = true;
            foreach (Creature player in _combat.PlayerCreatures.Where(static player => player.IsAlive))
            {
                await CreatureCmd.SetCurrentHp(player, ReligionFloorRules.SurvivalHp);
            }
            await FinishVictory();
            return;
        }

        if (Phase == 1 && boss.Creature.CurrentHp <= ReligionFloorRules.FirstPhaseMinimumHp)
        {
            Phase = 2;
            await EnsurePhasePowers();
            await CreatureCmd.SetCurrentHp(boss.Creature, boss.Creature.MaxHp);
            if (boss.Creature is LibraryCreature library)
            {
                library.RestorePreStunResistance();
                library.SetCurrentChaoValueInternal(library.MaxChaoValue);
            }
            foreach (ReligionFloorApostle apostle in Apostles)
            {
                await apostle.Wake();
            }
            RefreshLiberationPhaseBgm();
            ReligionFloorPresentation.Refresh(this);
        }
        else if (Phase == 2 && Outcome == ReligionFloorOutcome.Pending)
        {
            TrialTurnsCompleted++;
            CheckSalvation();
            if (Outcome == ReligionFloorOutcome.Pending && TrialTurnsCompleted >= ReligionFloorRules.TrialTurns)
            {
                Outcome = ReligionFloorOutcome.Explosion;
            }
        }

        boss.Creature.GetPower<ReligionFloorTrialPower>()?.RefreshDisplayedState();
        await ExhaustSacrifices(context);
        PlanNextTurn();
    }

    internal async Task EnsurePhasePowers()
    {
        if (Phase == 2 && Boss is { } boss)
        {
            await PowerCmdCompat.Ensure<ReligionFloorTrialPower>(boss.Creature);
        }
    }

    private void PlanNextTurn()
    {
        if (Boss is not { } boss || _combat == null)
        {
            return;
        }

        if (Outcome != ReligionFloorOutcome.Pending)
        {
            boss.PlanMove(Outcome == ReligionFloorOutcome.Salvation ? ReligionFloorMonster.SalvationMove : ReligionFloorMonster.ExplosionMove);
            return;
        }

        int turn = EnemyTurnsCompleted + 1;
        int move = 0;
        if (turn >= ReligionFloorRules.AweFirstTurn && (turn - ReligionFloorRules.AweFirstTurn) % ReligionFloorRules.AweInterval == 0)
        {
            move = 1;
        }
        else
        {
            int chance = Apostles.Count(static apostle => apostle.IsFakeDead) switch
            {
                1 => ReligionFloorRules.OneDeadReviveChance,
                2 => ReligionFloorRules.TwoDeadReviveChance,
                3 => ReligionFloorRules.ThreeDeadReviveChance,
                _ => 0
            };
            if (chance > 0 && _combat.RunState.Rng.MonsterAi.NextInt(100) < chance)
            {
                move = 2;
            }
        }

        boss.PlanMove(move);
        RepairApostlePlans(rerollAll: true);
    }

    internal void RepairApostlePlans(ReligionFloorApostle? awakened = null, bool rerollAll = false)
    {
        if (!Initialized || _repairingPlans || _combat == null || Completed || IsSettling)
        {
            return;
        }

        _repairingPlans = true;
        try
        {
            ReligionFloorApostle[] active = Apostles.Where(static apostle => apostle.IsAvailable).ToArray();
            if (active.Length == 0)
            {
                return;
            }

            bool schedulingNextTurn = rerollAll || _combat.CurrentSide != CombatSide.Enemy;
            bool CanExecute(ReligionFloorApostle apostle) => schedulingNextTurn || apostle.CanActThisTurn;
            int requiredAttacks = active.Any(CanExecute) ? MinimumAttackIntents : 0;
            int attacking = active.Count(static apostle => apostle.AttackValues(apostle.PlannedMove).Hits > 0);
            int executableAttacks = active.Count(apostle => CanExecute(apostle) && apostle.AttackValues(apostle.PlannedMove).Hits > 0);
            if (!rerollAll && awakened == null && executableAttacks >= requiredAttacks && attacking <= MaximumAttackIntents)
            {
                return;
            }

            var candidates = new List<(int[] Moves, int Weight, int Changes)>();
            for (int code = 0; code < (int)Math.Pow(3, active.Length); code++)
            {
                int value = code;
                int attacks = 0;
                int executable = 0;
                int weight = 1;
                int changes = 0;
                int[] moves = new int[active.Length];
                for (int index = 0; index < active.Length; index++)
                {
                    int selection = value % 3;
                    value /= 3;
                    moves[index] = selection;
                    weight *= active[index].MoveWeight(selection);
                    if (active[index].AttackValues(selection).Hits > 0)
                    {
                        attacks++;
                        if (CanExecute(active[index]))
                        {
                            executable++;
                        }
                    }
                    if (active[index] != awakened && active[index].PlannedMove != selection)
                    {
                        changes++;
                    }
                }
                if (attacks >= MinimumAttackIntents && attacks <= MaximumAttackIntents && executable >= requiredAttacks)
                {
                    candidates.Add((moves, weight, changes));
                }
            }

            if (!rerollAll)
            {
                int minimumChanges = candidates.Min(static candidate => candidate.Changes);
                candidates.RemoveAll(candidate => candidate.Changes != minimumChanges);
            }

            int roll = _combat.RunState.Rng.MonsterAi.NextInt(candidates.Sum(static candidate => candidate.Weight));
            foreach (var candidate in candidates)
            {
                roll -= candidate.Weight;
                if (roll >= 0)
                {
                    continue;
                }

                for (int index = 0; index < active.Length; index++)
                {
                    bool changed = active[index].PlannedMove != candidate.Moves[index];
                    active[index].PlanMove(candidate.Moves[index], rerollAll || changed || active[index] == awakened);
                }
                break;
            }
        }
        finally
        {
            _repairingPlans = false;
        }
    }

    private async Task ResolveOutcome(PlayerChoiceContext context)
    {
        if (Boss is not { } boss || _combat == null)
        {
            return;
        }

        IsSettling = true;
        if (Outcome == ReligionFloorOutcome.Salvation)
        {
            RefreshLiberationPhaseBgm();
            await ReligionFloorPresentation.ShowSalvation(this);
            foreach (Creature player in _combat.PlayerCreatures.Where(static player => player.IsAlive))
            {
                await PowerCmdCompat.Apply<ReligionFloorCrownPower>(player, 1, boss.Creature, null);
                await LibraryPowerCmd.Apply<LibraryProtectionPower>(context, player, ReligionFloorRules.CrownProtection, 0, true, boss.Creature, null);
            }
            await CreatureCmd.Kill(boss.Creature, force: true);
            RepentanceVisible = true;
            ReligionFloorPresentation.ShowRepentance(boss.Creature);
            await Cmd.Wait(ReligionFloorRules.RepentanceSeconds);
        }
        else
        {
            using (TargetedMonsterAttackHelper.ForceTargets(boss.Creature, _combat.PlayerCreatures))
            {
                ReligionFloorPresentation.PlayAttackStart(boss, "Special");
                await LibraryDamageCmd.Attack(ExplosionDamage).FromMonster(boss)
                    .WithAttackerAnim("Special", ReligionFloorRules.FrameSeconds)
                    .AfterAttackerAnim(() => ReligionFloorPresentation.PlayAttackImpact(boss))
                    .Execute(null);
            }
        }

        await FinishVictory();
    }

    private async Task FinishVictory()
    {
        ClearSacrifices();
        if (_combat == null)
        {
            return;
        }

        foreach (Creature enemy in _combat.Enemies.ToArray())
        {
            if (enemy.IsAlive)
            {
                await CreatureCmd.Kill(enemy, force: true);
            }
        }

        Won = _combat.PlayerCreatures.Any(static player => player.IsAlive);
        Completed = true;
        IsSettling = false;
        await CombatManager.Instance.CheckWinCondition();
    }

    public override Dictionary<string, string> SaveCustomState() => new()
    {
        ["ReligionState"] = JsonSerializer.Serialize(SavedProperties.From(this), StateJson)
    };

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        if (state.TryGetValue("ReligionState", out string? value))
        {
            JsonSerializer.Deserialize<SavedProperties>(value, StateJson)?.Fill(this);
        }
    }

    internal void CleanupPresentation() => ClearSacrifices();
}
