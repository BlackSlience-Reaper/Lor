using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Models;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 解放战主阶段 Boss 的“复活并强化”转阶段：艺术、历史、语言、文学、技术五层逐阶段开战（读档状态直接进第 N 阶段），
/// 对每个本阶段的主阶段 Boss：
/// <list type="bullet">
/// <item>状态机里有且只有一个 <see cref="LibraryPhaseTransitionMoveState"/>，必须执行一次、意图为治疗 + 强化；</item>
/// <item><see cref="ILiberationPhaseBoss.ForceReviveAndEmpowerState"/> 与 <see cref="ILiberationPhaseBoss.TriggerReviveAndEmpowerState"/>
/// 都把下一步行动切到它（检查后恢复原行动）；</item>
/// <item>用原版 <c>CreatureCmd.Kill</c> 击杀，再让 Boss 执行下一步行动，走完遭遇的死亡处理、复活与下一阶段生成。</item>
/// </list>
/// 每一步都把遭遇存档状态、敌人（类型、血量、死亡、下一步行动）与玩家血量记为 <c>TRACE|</c> 行，
/// 用同一套件对照改动前后的构建时逐行比较。参数 <c>lor-verify-liberation-phase-boss-transition</c> 跑五层，
/// 加 <c>-art</c>、<c>-history</c>、<c>-language</c>、<c>-literature</c>、<c>-technology</c> 后缀只跑一层。
/// </summary>
internal static class LiberationPhaseBossTransitionVerificationPatch
{
    private const string VerifyArg = "lor-verify-liberation-phase-boss-transition";
    private const string LogPrefix = "[LibraryOfRuina.LiberationPhaseBossTransition.Verify] ";

    private static readonly (string Name, Func<EncounterModel> Create, int MaxPhase)[] Floors =
    [
        ("art", static () => ModelDb.Encounter<ArtFloorLiberationEncounter>().ToMutable(), 6),
        ("history", static () => ModelDb.Encounter<HistoryFloorLiberationEncounter>().ToMutable(), 5),
        ("language", static () => ModelDb.Encounter<LanguageFloorLiberationEncounter>().ToMutable(), 5),
        ("literature", static () => ModelDb.Encounter<LiteratureFloorLiberationEncounter>().ToMutable(), 5),
        ("technology", static () => ModelDb.Encounter<TechnologyFloorLiberationEncounter>().ToMutable(), 5),
    ];

    private static readonly List<string> Failures = [];
    private static bool _started;

    internal static void Start()
    {
        if (_started || SelectedFloors() is not { Length: > 0 })
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static string[] SelectedFloors()
    {
        if (HasArg(VerifyArg))
        {
            return Floors.Select(static floor => floor.Name).ToArray();
        }

        return Floors
            .Select(static floor => floor.Name)
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
            string[] selected = SelectedFloors();
            Log.Info(LogPrefix + "floors: " + string.Join(",", selected));
            foreach ((string name, Func<EncounterModel> create, int maxPhase) in Floors)
            {
                if (!selected.Contains(name))
                {
                    continue;
                }

                for (int phase = 1; phase <= maxPhase; phase++)
                {
                    // 一个阶段出错不影响后面的阶段，对照时异常也作为 TRACE 的一部分比较。
                    try
                    {
                        await VerifyPhase(name, create, phase);
                    }
                    catch (Exception ex)
                    {
                        Log.Info(LogPrefix + "TRACE|" + name + "|p" + phase + "|exception|"
                            + ex.GetType().Name + ": " + ex.Message);
                        Check(false, name + "|p" + phase + ": " + ex);
                    }
                }
            }

            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(
                    Failures.Count + " check(s) failed: " + string.Join("; ", Failures));
            }

            Log.Info(LogPrefix + "LIBERATION_PHASE_BOSS_TRANSITION_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LIBERATION_PHASE_BOSS_TRANSITION_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task VerifyPhase(string floor, Func<EncounterModel> create, int phase)
    {
        string label = floor + "|p" + phase;
        EncounterModel encounter = create();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = phase.ToString(),
            ["KilledBossCount"] = (phase - 1).ToString()
        });
        CombatState state = await StartFight(encounter, "LIBERATIONPHASEBOSS_" + floor.ToUpperInvariant() + phase);
        try
        {
            Trace(label, "start", state);
            ILiberationPrimaryPhaseBoss[] bosses = state.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<ILiberationPrimaryPhaseBoss>()
                .Where(boss => boss.LiberationPhase == phase)
                .OrderBy(static boss => boss.Creature.SlotName, StringComparer.Ordinal)
                .ToArray();
            Check(bosses.Length > 0, label + ": no primary phase boss");

            foreach (ILiberationPrimaryPhaseBoss boss in bosses)
            {
                await VerifyForceAndTrigger(label, boss);
            }

            foreach (ILiberationPrimaryPhaseBoss boss in bosses)
            {
                if (!CombatManager.Instance.IsInProgress || !boss.Creature.IsAlive)
                {
                    continue;
                }

                await CreatureCmd.Kill(boss.Creature, force: true);
                await WaitFrames(8);
                // 末阶段结算后战斗结束的时机取决于实时计时器，等到结束再记，避免对照时出现时序噪声。
                if (state.Encounter?.SaveCustomState().GetValueOrDefault("SettlementTriggered") == "True")
                {
                    await WaitUntil(
                        static () => !CombatManager.Instance.IsInProgress,
                        label + " settlement combat end");
                }

                Trace(label, "killed " + boss.GetType().Name, state);
            }

            foreach (ILiberationPrimaryPhaseBoss boss in bosses)
            {
                var monster = (MonsterModel)boss;
                if (!CombatManager.Instance.IsInProgress
                    || !state.Enemies.Contains(boss.Creature)
                    || monster.NextMove is not LibraryPhaseTransitionMoveState)
                {
                    continue;
                }

                await monster.PerformMove();
                await WaitFrames(8);
                Trace(label, "performed " + boss.GetType().Name, state);
                Check(boss.Creature.IsDead || boss.Creature.CurrentHp >= 1,
                    label + ": " + boss.GetType().Name + " performed the transition without reviving");
            }

            Trace(label, "end", state);
        }
        finally
        {
            CleanupRun();
            await WaitUntil(
                static () => !CombatManager.Instance.IsInProgress
                    && CombatManager.Instance.DebugOnlyGetState() == null
                    && RunManager.Instance.DebugOnlyGetState() == null,
                label + " cleanup");
        }
    }

    private static async Task VerifyForceAndTrigger(string label, ILiberationPrimaryPhaseBoss boss)
    {
        var monster = (MonsterModel)boss;
        string name = boss.GetType().Name;
        MonsterMoveStateMachine machine = monster.MoveStateMachine
            ?? throw new InvalidOperationException(label + ": " + name + " has no move state machine.");
        LibraryPhaseTransitionMoveState[] transitions = machine.States.Values
            .OfType<LibraryPhaseTransitionMoveState>()
            .ToArray();
        Log.Info(LogPrefix + "TRACE|" + label + "|states " + name + "|"
            + string.Join(",", machine.States.Keys)
            + "|transitions=" + string.Join(",", transitions.Select(DescribeTransition)));
        foreach (LibraryPhaseTransitionMoveState transition in transitions)
        {
            Check(transition.MustPerformOnceBeforeTransitioning,
                label + ": " + name + " " + transition.Id + " is not MustPerformOnceBeforeTransitioning");
            Check(transition.Intents.Count == 2
                  && transition.Intents[0] is HealIntent
                  && transition.Intents[1] is BuffIntent,
                label + ": " + name + " " + transition.Id + " intents are not Heal + Buff");
        }

        // 没有转阶段状态的 Boss（末阶段黑天鹅）Force/Trigger 是空操作；其余必须切到其中一个转阶段状态
        // （微笑的尸山另有一个普通复活 REVIVE，Force 应选 REVIVE_AND_EMPOWER，记进 TRACE 对照）。
        MoveState original = monster.NextMove;
        boss.ForceReviveAndEmpowerState();
        string forced = monster.NextMove.Id;
        CheckSwitched(label, name, "Force", monster, original, transitions);
        monster.SetMoveImmediate(original, forceTransition: true);

        await boss.TriggerReviveAndEmpowerState();
        string triggered = monster.NextMove.Id;
        CheckSwitched(label, name, "Trigger", monster, original, transitions);
        monster.SetMoveImmediate(original, forceTransition: true);
        Check(ReferenceEquals(monster.NextMove, original),
            label + ": " + name + " did not return to " + original.Id);
        Log.Info(LogPrefix + "TRACE|" + label + "|switch " + name + "|force=" + forced + "|trigger=" + triggered);
        await WaitFrames(2);
    }

    private static void CheckSwitched(
        string label,
        string name,
        string call,
        MonsterModel monster,
        MoveState original,
        IReadOnlyCollection<LibraryPhaseTransitionMoveState> transitions)
    {
        bool ok = transitions.Count == 0
            ? ReferenceEquals(monster.NextMove, original)
            : transitions.Any(transition => ReferenceEquals(transition, monster.NextMove));
        Check(ok, label + ": " + call + " left " + name + " on " + monster.NextMove.Id);
    }

    private static string DescribeTransition(LibraryPhaseTransitionMoveState state) =>
        state.Id
        + "{once=" + state.MustPerformOnceBeforeTransitioning
        + ",intents=" + string.Join("+", state.Intents.Select(static intent => intent.GetType().Name))
        + ",follow=" + (state.FollowUpState?.Id ?? "null") + "}";

    private static void Trace(string label, string step, CombatState state)
    {
        var line = new StringBuilder();
        line.Append("TRACE|").Append(label).Append('|').Append(step).Append("|enc=");
        line.Append(string.Join(",", state.Encounter?.SaveCustomState()
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Key + "=" + pair.Value) ?? []));
        line.Append("|combat=").Append(CombatManager.Instance.IsInProgress);
        line.Append("|enemies=");
        line.Append(string.Join(";", state.Enemies.Select(static enemy =>
            enemy.Monster?.GetType().Name + "@" + enemy.SlotName
            + ":" + enemy.CurrentHp + "/" + enemy.MaxHp
            + (enemy.IsDead ? ":dead" : "")
            + ":next=" + enemy.Monster?.NextMove.Id)));
        line.Append("|players=");
        line.Append(string.Join(";", state.PlayerCreatures.Select(static player =>
            player.CurrentHp + "/" + player.MaxHp)));
        Log.Info(LogPrefix + line);
    }

    private static async Task<CombatState> StartFight(EncounterModel encounter, string seed)
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
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress,
            seed + " combat start");
        await WaitFrames(8);
        return CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
    }

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

    private static async Task WaitUntil(Func<bool> predicate, string description, int maxFrames = 900)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
            {
                return;
            }

            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
    }
}
