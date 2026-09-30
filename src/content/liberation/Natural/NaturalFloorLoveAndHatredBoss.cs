using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorLoveAndHatredBoss : LorMonsterModel, ILiberationPrimaryPhaseBoss
{
    private const string ReviveMoveId = "REVIVE_AND_EMPOWER";

    public int LiberationPhase => 1;

    public const string VideoPath = NaturalFloorAssets.LoveAndHatredInversionVideo;
    public const int BlockAmount = 22; // 以爱之名！：自身格挡。

    public const int BindAmount = 7; // 以爱与正义之名：束缚层数。

    public const int DisarmAmount = 3; // 以爱与恨之名：破绽层数。

    public const int VulnerableAmount = 3; // 以爱与恨之名：易损层数。

    public const int BindingTurns = 3; // 以爱与正义之名：束缚持续回合数。

    public const int DisarmTurns = 3; // 以爱与恨之名：破绽持续回合数。

    public const int VulnerableTurns = 3; // 以爱与恨之名：易损持续回合数。

    public const int MagicInterval = 4; // 各形态魔法之力：每隔多少次行动发动。

    public const int HumanChao = 140; // 人形：初始混乱抗性。

    public const int SnakeChao = 190; // 蛇形：初始混乱抗性。

    public const string SfxRoot = NaturalFloorAssets.LoveAndHatredSfxRoot;
    private const float SfxVolumeDb = -1.5f; // 爱与憎恨：音效音量，单位 dB。
    private static readonly string[] SfxFiles =
    [
        "human_attack", "human_fire", "human_guard", "snake_attack", "snake_fire", "snake_guard",
        "human_casting", "snake_casting", "human_laser_loop", "snake_laser_loop",
        "laser_end", "transform_start", "transform_end"
    ];
    internal static readonly string[] MoveIds =
    [
        "IN_THE_NAME_OF_LOVE_AND_HATRED", "WITH_LOVE", "IN_THE_NAME_OF_JUSTICE",
        "IN_THE_NAME_OF_LOVE_AND_JUSTICE", "MAGIC_HUMAN", "HATRED_BRAND",
        "LIGHT_OF_HATRED", "IN_THE_NAME_OF_LOVE_AND_HATE", "MAGIC_SNAKE"
    ];
    private Dictionary<string, MoveState> _moves = [];
    private bool _transforming;
    private Dictionary<string, string> _restoredState = [];
    private string? _restoredMove;

    public bool IsSnakeForm { get; private set; }

    public int CompletedFormActions { get; private set; }

    public int CompletedMoveCycleMask { get; private set; }

    public int LastMoveNumber { get; private set; }

    public int SnakeBaseHp { get; private set; }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            290, // 人形初始体力下限：ToughEnemies 进阶。
            280); // 人形初始体力下限：普通。

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            300, // 人形初始体力上限：ToughEnemies 进阶。
            290); // 人形初始体力上限：普通。

    private static int SnakeMinHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            296, // 蛇形体力下限（含端点）：ToughEnemies 进阶。
            290); // 蛇形体力下限（含端点）：普通。

    private static int SnakeHpUpperExclusive =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            301, // 蛇形体力上界（不含端点）：ToughEnemies 进阶。
            296); // 蛇形体力上界（不含端点）：普通。

    public override int DefaultChaoResistance => IsSnakeForm ? SnakeChao : HumanChao;

    public override bool HasDeathSfx => false;

    public override bool ShouldDisappearFromDoom => false;

    internal bool IsSpecialIdle => !IsSnakeForm && NextMove.StateId == MoveIds[4];

    internal static int StrengthAmount =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3, // 以爱与正义之名：DeadlyEnemies 进阶力量层数。
            2); // 以爱与正义之名：普通力量层数。

    public override IEnumerable<string> AssetPaths =>
        new[] { NaturalFloorLoveAndHatredVisuals.ScenePath }
            .Concat(new[] { "human", "special", "snake" }.Select(form =>
                NaturalFloorLoveAndHatredVisuals.AnimationRoot + form + "_animations.tres"))
            .Concat(new[] { "human_idle", "human_special", "human_fire", "human_strike", "human_hit", "human_guard",
                "snake_idle", "snake_fire", "snake_strike", "snake_hit", "snake_guard" }
                .Select(frame => NaturalFloorLoveAndHatredVisuals.ImageRoot + frame + ".png"))
            .Concat(new[] { VideoPath, NaturalFloorAssets.LibraryPassiveGreenIcon,
                NaturalFloorAssets.HistoryFloorCorrosionPowerIcon, NaturalFloorBadGuyPower.CustomIconPath })
            .Concat(SfxFiles.Select(file => SfxRoot + file + ".ogg"))
            .Concat(Enumerable.Range(0, MoveIds.Length).SelectMany(i => CreateIntent(i).AssetPaths))
            .Distinct();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _moves = [];
        _transforming = false;
        _restoredState = new(_restoredState);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Creature.CombatState?.Encounter is NaturalFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        await PowerCmdCompat.Ensure<HistoryFloorCorrosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorInversionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorHatredPower>(Creature, 1m, Creature, null, silent: true);
        if (_restoredState.Count > 0)
        {
            RestoreCombatSnapshot();
            _restoredState.Clear();
        }
        if (Creature.CombatState?.Encounter is NaturalFloorLiberationEncounter { TransitionPending: true })
        {
            ForceReviveAndEmpowerState();
        }
    }

    private void RestoreCombatSnapshot()
    {
        int maxHp = ReadInt(_restoredState, "BossMaxHp", Creature.MaxHp);
        // Loading a snapshot must not dispatch HP-change or death hooks again.
        Creature.SetMaxHpInternal(maxHp);
        Creature.SetCurrentHpInternal(ReadInt(_restoredState, "BossHp", maxHp));
        Creature.GetPower<NaturalFloorInversionPower>()?.SetAmount(
            Math.Clamp(ReadInt(_restoredState, "Hysteria", 0), 0, NaturalFloorInversionPower.MaxHysteria) + 1, silent: true);
        if (Creature is LibraryCreature library)
        {
            library.SetMaxChaoValueInternal(ReadInt(_restoredState, "BossMaxChao", library.MaxChaoValue));
            library.SetCurrentChaoValueInternal(ReadInt(_restoredState, "BossChao", library.MaxChaoValue));
        }
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature
            && Creature.CombatState?.Encounter is NaturalFloorLiberationEncounter encounter)
        {
            return encounter.OnFirstPhaseDeath(this, wasRemovalPrevented);
        }

        return Task.CompletedTask;
    }

    public Task TriggerReviveAndEmpowerState()
    {
        ForceReviveAndEmpowerState();
        return Task.CompletedTask;
    }

    public void ForceReviveAndEmpowerState()
    {
        _ = MoveStateMachine;
        SetMoveImmediate(_moves[ReviveMoveId], forceTransition: true);
    }

    private async Task ReviveAndEmpower()
    {
        if (Creature.CombatState?.Encounter is not NaturalFloorLiberationEncounter { TransitionPending: true } encounter)
        {
            return;
        }

        await CreatureCmd.SetCurrentHp(Creature, 1);
        await CreatureCmd.TriggerAnim(Creature, "Cast", NaturalFloorWrathMonster.HitTime);
        await encounter.CompletePhaseTransition();
    }

    public override Task AfterDamageReceivedLate(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Creature && Creature.IsAlive && result.TotalDamage > 0 && result.WasFullyBlocked
            ? PlayGuard()
            : Task.CompletedTask;

    private Task PlayGuard()
    {
        LocalOggOneShotPlayer.Play(SfxRoot + (IsSnakeForm ? "snake_guard.ogg" : "human_guard.ogg"), SfxVolumeDb);
        return CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
    }

    internal Creature[] LivingPlayers() => Creature.CombatState?.LivingPlayerCreatures().ToArray() ?? [];

    private bool HasLivingMark() => LivingPlayers().Any(static p => p.HasPower<NaturalFloorBadGuyPower>());

    private static int InTheNameOfLoveAndHatredDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16, // 以爱与憎之名：DeadlyEnemies 进阶单次伤害。
            14); // 以爱与憎之名：普通单次伤害。

    private const int InTheNameOfLoveAndHatredHits = 1; // 以爱与憎之名：攻击次数。

    private static int WithLoveDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14, // 以爱之名！：DeadlyEnemies 进阶单次伤害。
            12); // 以爱之名！：普通单次伤害。

    private const int WithLoveHits = 1; // 以爱之名！：攻击次数。

    private static int InTheNameOfJusticeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12, // 以正义之名！：DeadlyEnemies 进阶单次伤害。
            10); // 以正义之名！：普通单次伤害。

    private const int InTheNameOfJusticeHits = 2; // 以正义之名！：攻击次数。

    private static int InTheNameOfLoveAndJusticeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            20, // 以爱与正义之名~：DeadlyEnemies 进阶单次伤害。
            19); // 以爱与正义之名~：普通单次伤害。

    private const int InTheNameOfLoveAndJusticeHits = 1; // 以爱与正义之名~：攻击次数。

    private static int MagicHumanDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            24, // 魔法之力！：DeadlyEnemies 进阶单次伤害。
            22); // 魔法之力！：普通单次伤害。

    private const int MagicHumanHits = 1; // 魔法之力！：攻击次数。

    private static int HatredBrandDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            21, // 憎恶烙印：DeadlyEnemies 进阶单次伤害。
            19); // 憎恶烙印：普通单次伤害。

    private const int HatredBrandHits = 1; // 憎恶烙印：攻击次数。

    private static int LightOfHatredDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11, // 憎恶之光：DeadlyEnemies 进阶单次伤害。
            10); // 憎恶之光：普通单次伤害。

    private const int LightOfHatredHits = 2; // 憎恶之光：攻击次数。

    private static int InTheNameOfLoveAndHateDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10, // 以爱与恨之名：DeadlyEnemies 进阶单次伤害。
            9); // 以爱与恨之名：普通单次伤害。

    private const int InTheNameOfLoveAndHateHits = 3; // 以爱与恨之名：攻击次数。

    private static int MagicSnakeDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            30, // 魔法之力！！！：DeadlyEnemies 进阶单次伤害。
            27); // 魔法之力！！！：普通单次伤害。

    private const int MagicSnakeHits = 1; // 魔法之力！！！：攻击次数。

    private static int Damage(int move) =>
        move switch
        {
            0 => InTheNameOfLoveAndHatredDamage,
            1 => WithLoveDamage,
            2 => InTheNameOfJusticeDamage,
            3 => InTheNameOfLoveAndJusticeDamage,
            4 => MagicHumanDamage,
            5 => HatredBrandDamage,
            6 => LightOfHatredDamage,
            7 => InTheNameOfLoveAndHateDamage,
            8 => MagicSnakeDamage,
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, null)
        };

    private static int Hits(int move) =>
        move switch
        {
            0 => InTheNameOfLoveAndHatredHits,
            1 => WithLoveHits,
            2 => InTheNameOfJusticeHits,
            3 => InTheNameOfLoveAndJusticeHits,
            4 => MagicHumanHits,
            5 => HatredBrandHits,
            6 => LightOfHatredHits,
            7 => InTheNameOfLoveAndHateHits,
            8 => MagicSnakeHits,
            _ => MagicSnakeHits
        };

    private static AbstractIntent CreateIntent(int move)
    {
        string key = "NATURAL_FLOOR_" + MoveIds[move] + ".description";
        return move switch
        {
            0 or 5 => new CombinedAttackDebuffIntent(() => Damage(move), () => Hits(move), key,
                IntentBadge.FromPower<NaturalFloorBadGuyPower>(1)),
            1 => new CombinedAttackDefendIntent(() => Damage(move), () => Hits(move), key, BlockAmount),
            2 => new MultiAttackIntent(Damage(move), Hits(move)),
            3 => new CombinedAttackDebuffIntent(() => Damage(move), () => Hits(move), key,
                IntentBadge.FromPower<LibraryBindingPower>(BindAmount, BindingTurns.ToString(), BindAmount.ToString()),
                IntentBadge.FromPower<StrengthPower>(StrengthAmount)),
            6 => new CombinedAttackBuffIntent(() => Damage(move), () => Hits(move), key,
                IntentBadge.Heal()),
            7 => new CombinedAttackDebuffIntent(() => Damage(move), () => Hits(move), key,
                IntentBadge.FromPower<LibraryDisarmPower>(DisarmAmount, DisarmTurns.ToString(), DisarmAmount.ToString()),
                IntentBadge.FromPower<LibraryVulnerablePower>(VulnerableAmount, VulnerableTurns.ToString(), VulnerableAmount.ToString())),
            4 or 8 => new IndiscriminateAttackIntent(() => Damage(move), () => Hits(move), key,
                static owner => owner.CombatState?.LivingPlayerCreatures().ToArray() ?? []),
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, null)
        };
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _moves = [];
        var router = new DelegatingMonsterRouterState("NATURAL_FLOOR_ROUTER", (_, rng) => SelectMove(rng));
        for (int index = 0; index < MoveIds.Length; index++)
        {
            int move = index;
            AbstractIntent intent = CreateIntent(move);
            Func<IReadOnlyList<Creature>, Task> action = _ => PerformAttack(move);
            if (intent is IndiscriminateAttackIntent group)
            {
                action = group.WithPreAttackBlockBreak(this, action);
            }

            _moves.Add(MoveIds[move], new MoveState(MoveIds[move], action, intent)
            {
                FollowUpState = router
            });
        }
        _moves.Add(ReviveMoveId, new LibraryPhaseTransitionMoveState(ReviveMoveId, _ => ReviveAndEmpower(), new HealIntent(), new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
            FollowUpState = router
        });
        return new MonsterMoveStateMachine(_moves.Values.Cast<MonsterState>().Append(router), router);
    }

    private string SelectMove(Rng rng)
    {
        if (_restoredMove is { } restored)
        {
            _restoredMove = null;
            return restored;
        }
        if (CompletedFormActions == 0)
        {
            return MoveIds[IsSnakeForm ? 5 : 0];
        }

        if ((CompletedFormActions + 1) % MagicInterval == 0)
        {
            return MoveIds[IsSnakeForm ? 8 : 4];
        }

        if (!HasLivingMark())
        {
            return MoveIds[IsSnakeForm ? 5 : 0];
        }

        int[] normal = IsSnakeForm ? [6, 7] : [1, 2, 3];
        int[] eligible = normal.Where(i => (CompletedMoveCycleMask & (1 << i)) == 0).ToArray();
        if (eligible.Length == 0)
        {
            CompletedMoveCycleMask = 0;
            eligible = normal;
        }
        int[] withoutLast = eligible.Where(i => i + 1 != LastMoveNumber).ToArray();
        return MoveIds[rng.NextItem(withoutLast.Length > 0 ? withoutLast : eligible)];
    }

    private async Task PerformAttack(int move)
    {
        // A displayed move is committed once; all effects use the same live target set.
        bool fire = move is 0 or 3 or 4 or 5 or 6 or 8;
        string trigger = fire ? "Fire" : "Strike";
        if (Creature.IsDead || LivingPlayers().Length == 0)
        {
            return;
        }

        int healing = await ExecuteAttackWithSound(move, trigger, fire);
        if (Creature.IsDead)
        {
            return;
        }

        if (move is 0 or 5)
        {
            await TransferMark();
        }

        if (move == 1)
        {
            await PlayGuard();
            await CreatureCmd.GainBlock(Creature, BlockAmount, ValueProp.Move, null);
        }
        if (move == 3)
        {
            foreach (Creature player in LivingPlayers())
            {
                await LibraryPowerCmd.Apply<LibraryBindingPower>(player, BindAmount, BindingTurns, Creature, null);
            }

            await PowerCmdCompat.Apply<StrengthPower>(Creature, StrengthAmount, Creature, null);
        }
        if (move == 6 && healing > 0)
        {
            await CreatureCmd.Heal(Creature, healing);
        }

        if (move == 7)
        {
            foreach (Creature player in LivingPlayers())
            {
                await LibraryPowerCmd.Apply<LibraryDisarmPower>(player, DisarmAmount, DisarmTurns, Creature, null);
                await LibraryPowerCmd.Apply<LibraryVulnerablePower>(player, VulnerableAmount, VulnerableTurns, Creature, null);
            }
        }

        CompletedFormActions++;
        LastMoveNumber = move + 1;
        if (move is 1 or 2 or 3 or 6 or 7)
        {
            CompletedMoveCycleMask |= 1 << move;
        }
    }

    private async Task<int> ExecuteAttackWithSound(int move, string trigger, bool fire)
    {
        string form = IsSnakeForm ? "snake" : "human";
        bool magic = move is 4 or 8;
        if (magic)
        {
            await LocalOggOneShotPlayer.PlayAsync(SfxRoot + form + "_casting.ogg", SfxVolumeDb);
        }
        else
        {
            LocalOggOneShotPlayer.Play(SfxRoot + form + (fire ? "_fire.ogg" : "_attack.ogg"), SfxVolumeDb);
        }

        if (Creature.IsDead || LivingPlayers().Length == 0)
        {
            return 0;
        }

        using LocalOggLoopPlayer.LoopHandle? loop = magic
            ? LocalOggLoopPlayer.StartLoop(SfxRoot + form + "_laser_loop.ogg", SfxVolumeDb) : null;
        try
        {
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, LivingPlayers()))
            {
                var attack = await DamageCmd.Attack(Damage(move)).FromMonster(this).WithHitCount(Hits(move))
                    .WithAttackerAnim(trigger, NaturalFloorLoveAndHatredVisuals.AttackHitTime).Execute(null);
                return attack.Results.SelectMany(static results => results).Sum(static result => result.UnblockedDamage);
            }
        }
        finally
        {
            loop?.Stop();
            if (magic)
            {
                LocalOggOneShotPlayer.Play(SfxRoot + "laser_end.ogg", SfxVolumeDb);
            }
        }
    }

    private async Task TransferMark()
    {
        Creature[] players = LivingPlayers();
        Creature[] eligible = players.Where(static p => !p.HasPower<NaturalFloorBadGuyPower>()).ToArray();
        if (eligible.Length == 0)
        {
            return;
        }

        Creature target = RunRng.MonsterAi.NextItem(eligible)!;
        // Apply first: Artifact may prevent the new mark, in which case the existing mark remains.
        await PowerCmdCompat.Apply<NaturalFloorBadGuyPower>(target, 1m, Creature, null);
        if (!target.HasPower<NaturalFloorBadGuyPower>())
        {
            return;
        }

        foreach (Creature player in players.Where(p => p != target))
        {
            if (player.GetPower<NaturalFloorBadGuyPower>() is { } old)
            {
                await PowerCmd.Remove(old);
            }
        }
    }

    internal async Task TransformToSnake()
    {
        if (IsSnakeForm || _transforming || Creature.IsDead)
        {
            return;
        }

        _transforming = true;
        try
        {
            LocalOggOneShotPlayer.Play(SfxRoot + "transform_start.ogg", SfxVolumeDb);
            try
            {
                await QueenOfHatredInversionVideoController.PlayAsync(VideoPath);
            }
            catch (Exception exception)
            {
                Log.Warn("[NaturalFloorLiberation] Inversion video failed: " + exception);
            }
            if (Creature.IsDead || Creature.CombatState == null)
            {
                return;
            }

            foreach (PowerModel power in Creature.Powers.Where(static p => p.Type == PowerType.Debuff).ToArray())
            {
                await PowerCmd.Remove(power);
            }

            IsSnakeForm = true;
            SnakeBaseHp = RunRng.MonsterAi.NextInt(SnakeMinHp, SnakeHpUpperExclusive);
            var state = Creature.CombatState;
            await CreatureCmd.SetMaxHp(Creature, Creature.ScaleHpForMultiplayer(
                SnakeBaseHp, state.Encounter, state.Players.Count, state.RunState.CurrentActIndex));
            await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);
            if (Creature is LibraryCreature library)
            {
                library.RestorePreStunResistance();
                library.SetMaxChaoValueInternal(LibraryCreature.ScaleChaoValueForMultiplayer(
                    SnakeChao, state.Encounter, state.Players.Count, state.RunState.CurrentActIndex));
                library.SetCurrentChaoValueInternal(library.MaxChaoValue);
                library.HealthBar?.RefreshValues();
            }
            CompletedFormActions = 0;
            CompletedMoveCycleMask = 0;
            LastMoveNumber = 0;
            _ = MoveStateMachine;
            SetMoveImmediate(_moves[MoveIds[5]], forceTransition: true);
            LocalOggOneShotPlayer.Play(SfxRoot + "transform_end.ogg", SfxVolumeDb);
            if (Creature.GetCreatureNode() is { } node)
            {
                await node.RefreshIntents();
            }
        }
        finally
        {
            _transforming = false;
        }
    }

    internal Dictionary<string, string> SaveFormState()
    {
        var state = new Dictionary<string, string>
        {
            ["SnakeForm"] = IsSnakeForm.ToString(),
            ["FormActions"] = CompletedFormActions.ToString(),
            ["MoveCycleMask"] = CompletedMoveCycleMask.ToString(),
            ["LastMove"] = LastMoveNumber.ToString(),
            ["SnakeBaseHp"] = SnakeBaseHp.ToString(),
            ["NextMove"] = NextMove.StateId
        };
        if (Creature != null)
        {
            state["BossHp"] = Creature.CurrentHp.ToString();
            state["BossMaxHp"] = Creature.MaxHp.ToString();
            state["Hysteria"] = (Creature.GetPower<NaturalFloorInversionPower>()?.DisplayAmount ?? 0).ToString();
            if (Creature is LibraryCreature library)
            {
                state["BossChao"] = library.CurrentChaoValue.ToString();
                state["BossMaxChao"] = library.MaxChaoValue.ToString();
            }
        }
        return state;
    }

    internal void RestoreFormState(Dictionary<string, string> state)
    {
        _restoredState = new(state);
        IsSnakeForm = state.TryGetValue("SnakeForm", out string? snake) && bool.TryParse(snake, out bool flag) && flag;
        CompletedFormActions = Math.Max(0, ReadInt(state, "FormActions", 0));
        CompletedMoveCycleMask = ReadInt(state, "MoveCycleMask", 0);
        LastMoveNumber = ReadInt(state, "LastMove", 0);
        SnakeBaseHp = ReadInt(state, "SnakeBaseHp", 0);
        _restoredMove = state.TryGetValue("NextMove", out string? move) && (MoveIds.Contains(move) || move == ReviveMoveId) ? move : null;
    }

    private static int ReadInt(Dictionary<string, string> state, string key, int fallback) =>
        state.TryGetValue(key, out string? value) && int.TryParse(value, out int parsed) ? parsed : fallback;
}
