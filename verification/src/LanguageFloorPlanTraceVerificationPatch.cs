using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 语言层四个多意图怪物的逐回合轨迹：一阶段猩红伤痕与失去一切的狼（低血量模式、暴怒、狼死后的无法平息的愤怒），
/// 二阶段钴蓝伤痕（开场吞牌与反击计划、眩晕重排、变身大灰狼、影子形态与释放后的咆哮），三阶段微笑的尸山
/// （假死降形态、尸体审判、审判失败复位、满血升形态），以及一、三阶段各一场“已击杀两个 Boss 后玩家濒死”的战斗，
/// 让结算在复合行动中途把 <c>PhaseComplete</c> 置真，检查后面的招式不再执行。
/// 每个玩家回合开始与每个脚本动作后记录计划槽位、意图、状态机、怪物自身状态、怪物 AI 随机数计数和战斗摘要；
/// 另把存档属性原样填进一个新实例再建状态机，记录读档后的初始行动与意图。敌方回合里的行动切换、行动掷骰、
/// 复合行动与其中每个招式的执行、动画触发、格挡获得记为 <c>TRACE|…|exec|</c> 行。
/// 同一个验证程序集分别配改动前后的主模组构建各跑一次，逐行比较 TRACE 行。
/// 参数 <c>lor-verify-language-floor-plan-trace</c>；加 <c>-p1</c>、<c>-p1lethal</c>、<c>-p2</c>、<c>-p3</c>、
/// <c>-p3lethal</c> 只跑一场。
/// </summary>
internal static class LanguageFloorPlanTraceVerificationPatch
{
    private const string VerifyArg = "lor-verify-language-floor-plan-trace";
    private const string LogPrefix = "[LibraryOfRuina.LanguageFloorPlanTrace.Verify] ";
    private const int PlayerMaxHp = 9999;

    private static readonly (string Name, Func<Task> Run)[] Scenarios =
    [
        ("p1", RunPhaseOne),
        ("p1lethal", RunPhaseOneLethal),
        ("p2", RunPhaseTwo),
        ("p3", RunPhaseThree),
        ("p3lethal", RunPhaseThreeLethal),
    ];

    private static readonly List<string> Failures = [];
    private static bool _started;
    private static bool _tracing;
    private static string _label = string.Empty;
    private static Harmony? _harmony;

    internal static void Start()
    {
        if (_started || SelectedScenarios() is not { Length: > 0 })
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static string[] SelectedScenarios()
    {
        if (HasArg(VerifyArg))
        {
            return Scenarios.Select(static scenario => scenario.Name).ToArray();
        }

        return Scenarios
            .Select(static scenario => scenario.Name)
            .Where(static name => HasArg(VerifyArg + "-" + name))
            .ToArray();
    }

    private static bool HasArg(string arg) =>
        CommandLineHelper.HasArg(arg)
        || Environment.GetCommandLineArgs().Any(value => string.Equals(
            value.TrimStart('-'),
            arg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            InstallTracePatches();
            string[] selected = SelectedScenarios();
            Log.Info(LogPrefix + "scenarios: " + string.Join(",", selected));
            foreach ((string name, Func<Task> run) in Scenarios)
            {
                if (!selected.Contains(name))
                {
                    continue;
                }

                // 一场出错不影响后面的场次；异常本身也写进 TRACE 参与对照。
                try
                {
                    await run();
                }
                catch (Exception ex)
                {
                    _tracing = false;
                    Log.Info(LogPrefix + "TRACE|" + name + "|exception|" + ex.GetType().Name + ": " + ex.Message);
                    Check(false, name + ": " + ex);
                }
                finally
                {
                    _tracing = false;
                    CleanupRun();
                    await WaitUntil(
                        static () => !CombatManager.Instance.IsInProgress
                            && CombatManager.Instance.DebugOnlyGetState() == null
                            && RunManager.Instance.DebugOnlyGetState() == null,
                        name + " cleanup");
                }
            }

            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(
                    Failures.Count + " check(s) failed: " + string.Join("; ", Failures));
            }

            Log.Info(LogPrefix + "LANGUAGE_FLOOR_PLAN_TRACE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LANGUAGE_FLOOR_PLAN_TRACE_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    // ---- scenarios ------------------------------------------------------------------------------------------

    private static async Task RunPhaseOne()
    {
        await StartRun("LORLANGTRACEP1");
        CombatState state = await EnterLanguageFloor("p1", phase: 1, killedBossCount: null);
        await PlayRounds("p1", state, 8, async (round, combat) =>
        {
            switch (round)
            {
                case 2 when Monster<LanguageFloorLostEverythingWolf>(combat) is { } wolf:
                    await CreatureCmd.SetCurrentHp(wolf.Creature, Math.Ceiling(wolf.Creature.MaxHp * 0.5m));
                    return "wolf to low-health threshold";
                case 4 when Monster<LanguageFloorScarletScar>(combat) is { } scarlet:
                    await scarlet.EnterRage();
                    return "scarlet rage";
                case 6 when Monster<LanguageFloorLostEverythingWolf>(combat) is { } wolf:
                    await CreatureCmd.Kill(wolf.Creature);
                    return "wolf killed";
                default:
                    return null;
            }
        });
    }

    private static async Task RunPhaseOneLethal()
    {
        await StartRun("LORLANGTRACEP1L");
        CombatState state = await EnterLanguageFloor("p1lethal", phase: 1, killedBossCount: 2);
        await PlayRounds("p1lethal", state, 3, async (round, combat) =>
        {
            if (round != 2)
            {
                return null;
            }

            // 暴怒让猩红伤痕第一个槽位变成无差别射击，第一段命中就会触发结算。
            if (Monster<LanguageFloorScarletScar>(combat) is { } scarlet)
            {
                await scarlet.EnterRage();
            }

            foreach (Creature player in combat.PlayerCreatures)
            {
                await CreatureCmd.SetCurrentHp(player, 1m);
            }

            return "scarlet rage, players to 1 hp";
        });
    }

    private static async Task RunPhaseTwo()
    {
        await StartRun("LORLANGTRACEP2");
        CombatState state = await EnterLanguageFloor("p2", phase: 2, killedBossCount: null);
        await PlayRounds("p2", state, 10, async (round, combat) =>
        {
            LanguageFloorCobaltScar? cobalt = Monster<LanguageFloorCobaltScar>(combat);
            if (cobalt == null)
            {
                return null;
            }

            switch (round)
            {
                case 1 when cobalt.Creature is LibraryCreature library:
                    await LibraryCreatureCmd.SetCurrentChaoValue(library, 0m);
                    return "cobalt stunned";
                case 3:
                    await CreatureCmd.SetCurrentHp(
                        cobalt.Creature,
                        LanguageFloorCobaltScar.TransformHpThreshold(cobalt.Creature.MaxHp));
                    return "cobalt to transform threshold";
                case 5:
                    // 大灰狼形态的抗性会削减伤害，给足余量让累计伤害在本回合结束时越过影子阈值。
                    await CreatureCmd.Damage(
                        new ThrowingPlayerChoiceContext(),
                        cobalt.Creature,
                        cobalt.GetShadowDamageThreshold() * 4,
                        ValueProp.Unpowered,
                        combat.PlayerCreatures.First());
                    return "big wolf damaged past shadow threshold";
                default:
                    return null;
            }
        });
    }

    private static async Task RunPhaseThree()
    {
        await StartRun("LORLANGTRACEP3");
        CombatState state = await EnterLanguageFloor("p3", phase: 3, killedBossCount: null);
        await PlayRounds("p3", state, 14, async (round, combat) =>
        {
            LanguageFloorSmilingFace? face = combat.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<LanguageFloorSmilingFace>()
                .FirstOrDefault();
            if (face == null)
            {
                return null;
            }

            switch (round)
            {
                case 2:
                case 4:
                case 6:
                    if (face.Creature.IsDead || face.IsFakeDead)
                    {
                        return null;
                    }

                    await CreatureCmd.Kill(face.Creature);
                    return "smiling face killed in form " + face.Form;
                case 11 when face.Creature.IsAlive && !face.IsFakeDead:
                    await CreatureCmd.SetCurrentHp(face.Creature, face.Creature.MaxHp);
                    return "smiling face to full hp";
                default:
                    return null;
            }
        });
    }

    private static async Task RunPhaseThreeLethal()
    {
        await StartRun("LORLANGTRACEP3L");
        CombatState state = await EnterLanguageFloor("p3lethal", phase: 3, killedBossCount: null);
        LanguageFloorSmilingFace face = Monster<LanguageFloorSmilingFace>(state)
            ?? throw new InvalidOperationException("Smiling Face was missing.");
        // 一形态每回合三个槽位，濒死的玩家在第一个命中他的招式后结算。
        await face.DebugTransitionToForm(LanguageFloorSmilingFaceForm.First);
        await WaitFrames(4);
        Trace("p3lethal", "after form one", state);
        await PlayRounds("p3lethal", state, 4, async (round, combat) =>
        {
            if (round != 2)
            {
                return null;
            }

            foreach (Creature player in combat.PlayerCreatures)
            {
                await CreatureCmd.SetCurrentHp(player, 1m);
            }

            return "players to 1 hp";
        });
    }

    // ---- turn driving ---------------------------------------------------------------------------------------

    private static async Task PlayRounds(
        string label,
        CombatState state,
        int rounds,
        Func<int, CombatState, Task<string?>> action)
    {
        for (int round = 1; round <= rounds && CombatManager.Instance.IsInProgress; round++)
        {
            Trace(label, "r" + round + " start", state);
            string? performed = await action(round, state);
            if (performed != null)
            {
                await WaitFrames(4);
                Trace(label, "r" + round + " after " + performed, state);
            }

            if (round < rounds)
            {
                await EndTurnAndWait(label, state, round);
            }
        }

        EndOfScenario(label, state);
    }

    private static void EndOfScenario(string label, CombatState state)
    {
        TraceLine(label, "end", Summary(state) + " encounter=" + DescribeEncounter(state));
        _tracing = false;
    }

    private static async Task EndTurnAndWait(string label, CombatState state, int round)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        int startRound = state.RoundNumber;
        Player player = state.Players.Single();
        TraceLine(label, "r" + round + " end turn", "round=" + startRound);
        PlayerCmd.EndTurn(player, canBackOut: false);
        await WaitUntil(
            () => !CombatManager.Instance.IsInProgress
                || (state.RoundNumber > startRound
                    && state.CurrentSide == CombatSide.Player
                    && state.Players.All(static p => p.PlayerCombatState?.Phase == PlayerTurnPhase.Play)),
            label + " round " + round + " enemy turn",
            3600);
        await WaitFrames(4);
        if (!CombatManager.Instance.IsInProgress)
        {
            TraceLine(label, "r" + round + " combat ended", Summary(state) + " encounter=" + DescribeEncounter(state));
        }
    }

    private static async Task StartRun(string seed)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
    }

    private static async Task<CombatState> EnterLanguageFloor(string label, int phase, int? killedBossCount)
    {
        _label = label;
        _tracing = true;
        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        var custom = new Dictionary<string, string>
        {
            ["CurrentPhase"] = phase.ToString(CultureInfo.InvariantCulture),
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False",
        };
        if (killedBossCount is int killed)
        {
            custom["KilledBossCount"] = killed.ToString(CultureInfo.InvariantCulture);
        }

        encounter.LoadCustomState(custom);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() is
                {
                    CurrentSide: CombatSide.Player
                } state
                && state.Players.All(static player =>
                    player.PlayerCombatState?.Phase == PlayerTurnPhase.Play)
                && state.Enemies.All(static enemy =>
                    enemy.Monster is not LanguageFloorCobaltScar { OpeningResolved: false }),
            label + " combat start",
            1800);
        await WaitFrames(8);
        CombatState state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");

        // 玩家不出牌，只挨打；加厚血量让回合数由脚本决定，而不是由玩家死亡决定。
        foreach (Creature player in state.PlayerCreatures)
        {
            await CreatureCmd.SetMaxHp(player, PlayerMaxHp);
            await CreatureCmd.SetCurrentHp(player, PlayerMaxHp);
        }

        TraceLine(label, "encounter", DescribeEncounter(state));
        return state;
    }

    // ---- trace ----------------------------------------------------------------------------------------------

    private static void Trace(string label, string step, CombatState state)
    {
        TraceLine(label, step, Summary(state) + " encounter=" + DescribeEncounter(state));
        foreach (Creature enemy in state.Enemies)
        {
            if (enemy.Monster is { } monster && IsPlanMonster(monster))
            {
                string key = step + "|" + monster.GetType().Name + "@" + enemy.SlotName;
                TraceLine(label, key, DescribeMonster(monster, state));
                TraceLine(label, key + "|machine", monster.MoveStateMachine is { } machine
                    ? DescribeMachine(machine)
                    : "<none>");
                TraceLine(label, key + "|reload", DescribeReload(monster));
            }
        }
    }

    private static void TraceLine(string label, string step, string payload) =>
        Log.Info(LogPrefix + "TRACE|" + label + "|" + step + "|" + payload);

    private static string DescribeEncounter(CombatState state) =>
        state.Encounter is LanguageFloorLiberationEncounter encounter
            ? "phase=" + encounter.CurrentPhase
              + " complete=" + encounter.PhaseComplete
              + " pending=" + encounter.TransitionPending
              + " kills=" + encounter.KilledBossCount
              + " settled=" + encounter.SettlementTriggered
              + " lethal=" + encounter.EndedByLethalDamage
            : "<none>";

    private static string Summary(CombatState state)
    {
        var line = new StringBuilder();
        line.Append("round=").Append(state.RoundNumber)
            .Append(" side=").Append(state.CurrentSide)
            .Append(" combat=").Append(CombatManager.Instance.IsInProgress)
            .Append(" players=");
        line.Append(string.Join(";", state.PlayerCreatures.Select(DescribeCreature)));
        line.Append(" enemies=");
        line.Append(string.Join(";", state.Enemies.Select(static enemy =>
            enemy.Monster?.GetType().Name + "@" + enemy.SlotName + "#" + enemy.CombatId + ":" + DescribeCreature(enemy))));
        return line.ToString();
    }

    private static string DescribeCreature(Creature creature)
    {
        string chao = creature is LibraryCreature library
            ? " chao=" + library.CurrentChaoValue.ToString(CultureInfo.InvariantCulture) + "/" + library.MaxChaoValue
              + (library.IsChaoed ? " chaoed" : string.Empty)
            : string.Empty;
        return creature.CurrentHp + "/" + creature.MaxHp
            + " blk=" + creature.Block
            + (creature.IsDead ? " dead" : string.Empty)
            + chao
            + " pw=[" + string.Join(",", creature.Powers
                .Select(static power => power.Id.Entry + ":" + power.Amount)
                .OrderBy(static text => text, StringComparer.Ordinal)) + "]";
    }

    private static bool IsPlanMonster(MonsterModel? monster) =>
        monster is LanguageFloorLostEverythingWolf
            or LanguageFloorScarletScar
            or LanguageFloorCobaltScar
            or LanguageFloorSmilingFace;

    private static string DescribeMonster(MonsterModel monster, CombatState state)
    {
        var line = new StringBuilder();
        MoveState next = monster.NextMove;
        line.Append("next=").Append(next.Id)
            .Append(" once=").Append(next.MustPerformOnceBeforeTransitioning)
            .Append(" canLeave=").Append(next.CanTransitionAway)
            .Append(" rng=").Append(MonsterAiCounter(monster))
            .Append(" intents=[").Append(DescribeIntents(next.Intents, monster, state)).Append(']');
        switch (monster)
        {
            case LanguageFloorLostEverythingWolf wolf:
                line.Append(" slots=").Append(string.Join(",", wolf.PlannedMoveOne, wolf.PlannedMoveTwo,
                        wolf.PlannedMoveThree, wolf.PlannedMoveFour))
                    .Append(" turn=").Append(wolf.WolfTurnCount)
                    .Append(" low=").Append(wolf.LowHealthMode)
                    .Append(" pendingLow=").Append(ReadField(wolf, "_pendingLowHealthMode"))
                    .Append(" planned=").Append(string.Join(",", wolf.PlannedMoves));
                break;
            case LanguageFloorScarletScar scarlet:
                line.Append(" slots=").Append(string.Join(",", scarlet.PlannedMoveOne, scarlet.PlannedMoveTwo))
                    .Append(" turn=").Append(scarlet.EnemyTurnCount)
                    .Append(" unrelieved=").Append(scarlet.UnrelievedAnger)
                    .Append(" raging=").Append(scarlet.IsRaging)
                    .Append(" planned=").Append(string.Join(",", scarlet.PlannedMoves));
                break;
            case LanguageFloorCobaltScar cobalt:
                line.Append(" slots=").Append(string.Join(",", cobalt.PlannedMoveOne, cobalt.PlannedMoveTwo,
                        cobalt.PlannedMoveThree))
                    .Append(" form=").Append(cobalt.Form)
                    .Append(" opening=").Append(cobalt.OpeningResolved)
                    .Append(" swallow=").Append(cobalt.SwallowWindowActive).Append('/').Append(cobalt.PlayerTurnsSinceSwallow)
                    .Append('/').Append(cobalt.SwallowedCards.Count)
                    .Append(" instinct=").Append(cobalt.ForceInstinctNextTurn).Append('/').Append(cobalt.TurnsUntilInstinct)
                    .Append(" entry=").Append(cobalt.BigWolfEntryHp)
                    .Append(" acc=").Append(cobalt.AccumulatedDamage)
                    .Append(" shadow=").Append(cobalt.ShadowTurnsRemaining)
                    .Append(" release=").Append(cobalt.ShadowReleasePending)
                    .Append(" roar=").Append(cobalt.ForceRoarNextTurn)
                    .Append(" shadowCards=").Append(string.Join(",", cobalt.ShadowCardsPlayedByPlayer))
                    .Append(" planned=").Append(string.Join(",", cobalt.PlannedMoves));
                break;
            case LanguageFloorSmilingFace face:
                line.Append(" slots=").Append(string.Join(",", face.PlannedMoveOne, face.PlannedMoveTwo,
                        face.PlannedMoveThree, face.PlannedMoveFour))
                    .Append(" targets=").Append(string.Join(",", face.PlannedTargetOne, face.PlannedTargetTwo,
                        face.PlannedTargetThree, face.PlannedTargetFour))
                    .Append(" form=").Append(face.Form)
                    .Append(" init=").Append(face.Initialized)
                    .Append(" maxHp=").Append(face.FormOneMaxHp).Append('/').Append(face.FormTwoMaxHp)
                    .Append('/').Append(face.FormThreeMaxHp)
                    .Append(" formTurn=").Append(face.FormTurnCount)
                    .Append(" prev=").Append(face.PreviousNormalMove)
                    .Append(" spawns=").Append(face.PendingCorpseSpawns)
                    .Append(" hpAtSpawn=").Append(face.HpAtLastSpawnThreshold)
                    .Append(" downgrade=").Append(face.WaitingForDowngrade)
                    .Append(" fakeTurns=").Append(face.FakeDeathPlayerTurnsRemaining)
                    .Append(" transition=").Append(face.PendingFormTransition)
                    .Append('/').Append(face.PendingFormTransitionIsPromotion)
                    .Append(" trial=").Append(face.CorpseTrialPending).Append('/').Append(face.CorpseTrialActive)
                    .Append('/').Append(face.CorpseTrialPlayerTurnsRemaining)
                    .Append(" killable=").Append(face.ForceKillable)
                    .Append(" fake=").Append(face.IsFakeDead)
                    .Append(" planned=").Append(string.Join(",", face.PlannedMoves))
                    .Append(" plannedTargets=").Append(string.Join(",", face.PlannedTargets
                        .Select(static target => target == null ? "-" : target.CombatId.ToString())));
                break;
        }

        return line.ToString();
    }

    /// <summary>
    /// 模拟读档：把 <see cref="CombatStateProperties"/> 列出的战斗状态填进 <c>ToMutable</c> 出来的新实例，
    /// 再 <c>SetUpForCombat</c> 建状态机（原版不存怪物，这条路径只在套件里走）。
    /// 记录新状态机的初始行动与意图类型；新实例没有生物，不取标签与伤害。
    /// </summary>
    private static string DescribeReload(MonsterModel monster)
    {
        try
        {
            SavedProperties? props = CombatStateProperties.From(monster);
            MonsterModel fresh = ModelDb.GetById<MonsterModel>(monster.Id).ToMutable();
            props?.Fill(fresh);
            fresh.SetUpForCombat();
            MonsterMoveStateMachine machine = fresh.MoveStateMachine
                ?? throw new InvalidOperationException("reloaded machine is null");
            object? initial = AccessTools.Field(typeof(MonsterMoveStateMachine), "_initialState")
                ?.GetValue(machine);
            string intents = initial is MoveState move
                ? string.Join(";", move.Intents.Select(static intent => intent.GetType().Name + ":" + intent.IntentType))
                : "<router>";
            return "init=" + ((MonsterState?)initial)?.Id + " intents=[" + intents + "]";
        }
        catch (Exception ex)
        {
            return "reload!" + ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static string MonsterAiCounter(MonsterModel monster)
    {
        try
        {
            return monster.RunRng.MonsterAi.ToSerializable().counter.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return "!" + ex.GetType().Name;
        }
    }

    private static string ReadField(object instance, string name) =>
        AccessTools.Field(instance.GetType(), name)?.GetValue(instance)?.ToString() ?? "<missing>";

    private static string DescribeIntents(IReadOnlyList<AbstractIntent> intents, MonsterModel owner, CombatState state)
    {
        Creature[] targets = state.PlayerCreatures.Where(static creature => creature.IsAlive).ToArray();
        return string.Join(";", intents.Select(intent =>
        {
            var text = new StringBuilder();
            text.Append(intent.GetType().Name).Append(':').Append(intent.IntentType);
            try
            {
                text.Append(":label=").Append(intent.GetIntentLabel(targets, owner.Creature).GetFormattedText());
            }
            catch (Exception ex)
            {
                text.Append(":label!").Append(ex.GetType().Name);
            }

            if (intent is AttackIntent attack)
            {
                try
                {
                    text.Append(":dmg=").Append(attack.GetTotalDamage(targets, owner.Creature))
                        .Append('x').Append(attack.Repeats);
                }
                catch (Exception ex)
                {
                    text.Append(":dmg!").Append(ex.GetType().Name);
                }
            }

            return text.ToString();
        }));
    }

    private static string DescribeMachine(MonsterMoveStateMachine machine) =>
        string.Join(",", machine.States.Values.Select(static state => state is MoveState move
            ? move.Id + "{once=" + move.MustPerformOnceBeforeTransitioning
              + ",intents=" + move.Intents.Count
              + ",follow=" + (move.FollowUpState?.Id ?? "null") + "}"
            : state.Id + "{router}"));

    // ---- execution trace patches ----------------------------------------------------------------------------

    private static void InstallTracePatches()
    {
        if (_harmony != null)
        {
            return;
        }

        _harmony = new Harmony("LibraryOfRuina.Verification.LanguageFloorPlanTrace");
        _harmony.Patch(
            AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.SetMoveImmediate)),
            postfix: new HarmonyMethod(typeof(LanguageFloorPlanTraceVerificationPatch), nameof(AfterSetMoveImmediate)));
        _harmony.Patch(
            AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.RollMove)),
            postfix: new HarmonyMethod(typeof(LanguageFloorPlanTraceVerificationPatch), nameof(AfterRollMove)));
        _harmony.Patch(
            AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.PerformMove)),
            prefix: new HarmonyMethod(typeof(LanguageFloorPlanTraceVerificationPatch), nameof(BeforePerformMove)));
        _harmony.Patch(
            AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim)),
            prefix: new HarmonyMethod(typeof(LanguageFloorPlanTraceVerificationPatch), nameof(BeforeTriggerAnim)));
        _harmony.Patch(
            AccessTools.Method(
                typeof(CreatureCmd),
                nameof(CreatureCmd.GainBlock),
                [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(LanguageFloorPlanTraceVerificationPatch), nameof(BeforeGainBlock)));

        // 各怪物执行单个招式的私有方法：复合行动逐槽调用它们，记录执行了哪些招式、在第几个槽位停下。
        foreach ((Type type, string method, Type[] parameters) in new[]
                 {
                     (typeof(LanguageFloorLostEverythingWolf), "PerformMove", new[] { typeof(LanguageFloorMoveKind) }),
                     (typeof(LanguageFloorScarletScar), "PerformMove", new[] { typeof(LanguageFloorMoveKind) }),
                     (typeof(LanguageFloorCobaltScar), "PerformMove", new[] { typeof(LanguageFloorMoveKind) }),
                     (typeof(LanguageFloorSmilingFace), "PerformPlannedMove",
                         new[] { typeof(LanguageFloorSmilingFaceMove), typeof(int) }),
                 })
        {
            MethodInfo target = AccessTools.DeclaredMethod(type, method, parameters)
                ?? throw new MissingMethodException(type.FullName, method);
            _harmony.Patch(
                target,
                prefix: new HarmonyMethod(typeof(LanguageFloorPlanTraceVerificationPatch), nameof(BeforeSingleMove)));
        }
    }

    private static bool IsTraced(MonsterModel? monster) => _tracing && IsPlanMonster(monster);

    private static void AfterSetMoveImmediate(MonsterModel __instance, MoveState state, bool forceTransition)
    {
        if (IsTraced(__instance))
        {
            TraceLine(_label, "exec", "set " + __instance.GetType().Name + " " + state.Id
                + " force=" + forceTransition + " next=" + __instance.NextMove.Id
                + " rng=" + MonsterAiCounter(__instance));
        }
    }

    private static void AfterRollMove(MonsterModel __instance)
    {
        if (IsTraced(__instance))
        {
            TraceLine(_label, "exec", "roll " + __instance.GetType().Name + " next=" + __instance.NextMove.Id
                + " rng=" + MonsterAiCounter(__instance));
        }
    }

    private static void BeforePerformMove(MonsterModel __instance)
    {
        if (IsTraced(__instance))
        {
            TraceLine(_label, "exec", "perform " + __instance.GetType().Name + " " + __instance.NextMove.Id
                + " rng=" + MonsterAiCounter(__instance));
        }
    }

    private static void BeforeSingleMove(MonsterModel __instance, object[] __args)
    {
        if (IsTraced(__instance))
        {
            TraceLine(_label, "exec", "move " + __instance.GetType().Name + " "
                + string.Join(",", __args.Select(static arg => arg?.ToString() ?? "null"))
                + " rng=" + MonsterAiCounter(__instance)
                + " encounter=" + (__instance.CombatState is CombatState state ? DescribeEncounter(state) : "<none>"));
        }
    }

    private static void BeforeTriggerAnim(Creature creature, string triggerName)
    {
        if (IsTraced(creature.Monster))
        {
            TraceLine(_label, "exec", "anim " + creature.Monster!.GetType().Name + " " + triggerName);
        }
    }

    private static void BeforeGainBlock(Creature creature, decimal amount)
    {
        if (IsTraced(creature.Monster))
        {
            TraceLine(_label, "exec", "block " + creature.Monster!.GetType().Name + " "
                + amount.ToString(CultureInfo.InvariantCulture));
        }
    }

    // ---- helpers --------------------------------------------------------------------------------------------

    private static T? Monster<T>(CombatState state) where T : MonsterModel =>
        state.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<T>()
            .FirstOrDefault();

    private static void ArmActLikeIt2OneShotVanillaEntry()
    {
        Type? gateType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ActLikeIt2.Runtime.ActSelectionGate", throwOnError: false))
            .FirstOrDefault(static type => type != null);
        PropertyInfo? skipNextFork = gateType?.GetProperty(
            "SkipNextFork",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
        }
    }

    private static void CleanupRun()
    {
        if (RunManager.Instance.DebugOnlyGetState() != null)
        {
            RunManager.Instance.CleanUp(graceful: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            Failures.Add(message);
            Log.Error(LogPrefix + "CHECK FAILED: " + message);
        }
    }

    private static async Task WaitFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    // headless 下帧不限速，按帧数计的超时只有几秒；这里按墙钟计，只影响何时放弃，不影响取样时刻。
    private static async Task WaitUntil(Func<bool> predicate, string description, int maxFrames = 900)
    {
        _ = maxFrames;
        if (!await TryWaitUntil(predicate, TimeSpan.FromSeconds(180)))
        {
            CombatState? state = CombatManager.Instance.DebugOnlyGetState();
            throw new TimeoutException("Timed out waiting for " + description + " (combat="
                + CombatManager.Instance.IsInProgress
                + " side=" + state?.CurrentSide
                + " round=" + state?.RoundNumber
                + " phase=" + string.Join(",", state?.Players.Select(static p => p.PlayerCombatState?.Phase.ToString()) ?? [])
                + ").");
        }
    }

    private static async Task<bool> TryWaitUntil(Func<bool> predicate, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return true;
            }

            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        return predicate();
    }
}
