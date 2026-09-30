using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.specialguests;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 楼层完全解放进度在胜利时的写入轨迹，用于改动前后逐行对照。
/// <para>
/// 每个场景读档状态直接进解放战末阶段，用原版 <c>CreatureCmd.Kill</c> 击杀主阶段 Boss，等结算结束战斗，走原版
/// <c>EndCombatInternal</c> 的胜利流程。沿途用 Harmony 记录 <c>TRACE|</c> 行：
/// </para>
/// <list type="bullet">
/// <item><c>hook-enter</c>：<c>Hook.AfterCombatVictory</c> 最先执行的前缀，此时的进度、嘉宾载体是否存在、是否在战斗 Modifiers 快照里；</item>
/// <item><c>mark</c>：每次调用 <c>FloorLiberationProgress.MarkFullyLiberated</c>（调用前后的进度与载体）；</item>
/// <item><c>early</c> / <c>victory</c>：没有覆写这两个回调的监听者（牌组的牌、遗物等）被调用时看到的进度；</item>
/// <item><c>carrier-victory</c>：嘉宾载体自己的胜利回调；<c>hook-exit</c>：整个 Hook 完成后（RitsuLib 胜利事件之后）；</item>
/// <item><c>save-run</c>：胜利后的 <c>SaveRun</c>；<c>end</c>：战斗结束后的进度与写入次数。</item>
/// </list>
/// <c>listeners</c> 行列出胜利 Hook 的监听者序列，自有监听模型出现在这里，改动前后按设计不同。
/// </summary>
internal static class LiberationVictoryProgressTraceVerificationPatch
{
    private const string VerifyArg = "lor-verify-liberation-victory-progress-trace";
    private const string LogPrefix = "[LibraryOfRuina.LiberationVictoryProgressTrace.Verify] ";

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Failures = [];

    private static bool _started;
    private static Harmony? _harmony;
    private static string _label = "";
    private static bool _insideVictoryHook;
    private static int _markCount;

    internal static void Start()
    {
        if (_started || !HasArg(VerifyArg))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
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
            InstallProbes();
            await RunGuarded("history-final", () => RunLiberation(
                "history-final",
                ModelDb.Encounter<HistoryFloorLiberationEncounter>().ToMutable(),
                phase: 5,
                playerCount: 1,
                preCreateCarrier: false));
            await RunGuarded("technology-final-carrier", () => RunLiberation(
                "technology-final-carrier",
                ModelDb.Encounter<TechnologyFloorLiberationEncounter>().ToMutable(),
                phase: 5,
                playerCount: 1,
                preCreateCarrier: true));
            await RunGuarded("art-final-2p", () => RunLiberation(
                "art-final-2p",
                ModelDb.Encounter<ArtFloorLiberationEncounter>().ToMutable(),
                phase: 6,
                playerCount: 2,
                preCreateCarrier: false));
            await RunGuarded("vanilla-slimes", RunVanillaVictory);

            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(
                    Failures.Count + " scenario(s) failed: " + string.Join("; ", Failures));
            }

            Log.Info(LogPrefix + "LIBERATION_VICTORY_PROGRESS_TRACE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LIBERATION_VICTORY_PROGRESS_TRACE_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunGuarded(string label, Func<Task> run)
    {
        _label = label;
        _markCount = 0;
        _insideVictoryHook = false;
        try
        {
            await run();
        }
        catch (Exception ex)
        {
            Trace("exception", ex.GetType().Name + ": " + ex.Message);
            Failures.Add(label + ": " + ex.GetType().Name + ": " + ex.Message);
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

    private static async Task RunLiberation(
        string label,
        EncounterModel encounter,
        int phase,
        int playerCount,
        bool preCreateCarrier)
    {
        var state = new Dictionary<string, string>
        {
            ["CurrentPhase"] = phase.ToString(),
            ["KilledBossCount"] = (phase - 1).ToString(),
            ["TransitionPending"] = "False",
            ["SettlementTriggered"] = "False",
            ["EndedByLethalDamage"] = "False"
        };
        if (encounter is ArtFloorLiberationEncounter)
        {
            state["EndedByPlaceholder"] = "False";
        }

        encounter.LoadCustomState(state);
        CombatState combatState = await StartFight(
            encounter,
            "LIBVICTORY_" + label.ToUpperInvariant().Replace('-', '_'),
            playerCount,
            preCreateCarrier);
        var liberation = (IFloorLiberationEncounter)combatState.Encounter!;
        Trace("start", "floor=" + liberation.LiberationFloorId
            + "|full=" + liberation.IsFullyLiberated
            + "|" + DescribeProgress(combatState.RunState));

        ILiberationPrimaryPhaseBoss[] bosses = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ILiberationPrimaryPhaseBoss>()
            .Where(boss => boss.LiberationPhase == phase)
            .OrderBy(static boss => boss.Creature.SlotName, StringComparer.Ordinal)
            .ToArray();
        Require(bosses.Length > 0, label + ": no primary phase boss for phase " + phase);
        foreach (ILiberationPrimaryPhaseBoss boss in bosses)
        {
            if (CombatManager.Instance.IsInProgress && boss.Creature.IsAlive)
            {
                await CreatureCmd.Kill(boss.Creature, force: true);
                await WaitFrames(8);
            }
        }

        Trace("killed", "full=" + liberation.IsFullyLiberated + "|combat=" + CombatManager.Instance.IsInProgress);
        await WaitUntil(static () => !CombatManager.Instance.IsInProgress, label + " victory", 1800);
        await WaitFrames(8);
        Trace("end", "marks=" + _markCount + "|" + DescribeProgress(RunManager.Instance.DebugOnlyGetState()));
        Require(_markCount == 1, label + ": expected exactly one progress write, saw " + _markCount);
    }

    private static async Task RunVanillaVictory()
    {
        CombatState combatState = await StartFight(
            ModelDb.Encounter<SlimesWeak>().ToMutable(),
            "LIBVICTORY_VANILLA_SLIMES",
            playerCount: 1,
            preCreateCarrier: false);
        Trace("start", DescribeProgress(combatState.RunState));
        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (CombatManager.Instance.IsInProgress && enemy.IsAlive)
            {
                await CreatureCmd.Kill(enemy, force: true);
                await WaitFrames(4);
            }
        }

        await WaitUntil(static () => !CombatManager.Instance.IsInProgress, "vanilla victory", 1800);
        await WaitFrames(8);
        Trace("end", "marks=" + _markCount + "|" + DescribeProgress(RunManager.Instance.DebugOnlyGetState()));
        Require(_markCount == 0, "vanilla-slimes: a vanilla victory wrote liberation progress");
    }

    private static void InstallProbes()
    {
        _harmony = new Harmony("LibraryOfRuina.Verification.LiberationVictoryProgressTrace");
        MethodInfo hook = AccessTools.Method(typeof(Hook), nameof(Hook.AfterCombatVictory))
            ?? throw new InvalidOperationException("Hook.AfterCombatVictory was not found.");
        _harmony.Patch(
            hook,
            prefix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(BeforeVictoryHook))
            {
                priority = Priority.First
            },
            postfix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(AfterVictoryHook))
            {
                priority = Priority.Last
            });
        _harmony.Patch(
            AccessTools.Method(typeof(FloorLiberationProgress), "MarkFullyLiberated")
            ?? throw new InvalidOperationException("FloorLiberationProgress.MarkFullyLiberated was not found."),
            prefix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(BeforeMark)),
            postfix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(AfterMark)));
        _harmony.Patch(
            AccessTools.Method(typeof(AbstractModel), nameof(AbstractModel.AfterCombatVictoryEarly)),
            prefix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(BeforeBaseEarly)));
        _harmony.Patch(
            AccessTools.Method(typeof(AbstractModel), nameof(AbstractModel.AfterCombatVictory)),
            prefix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(BeforeBaseVictory)));
        _harmony.Patch(
            AccessTools.Method(typeof(SpecialGuestRunStateModifier), nameof(SpecialGuestRunStateModifier.AfterCombatVictory)),
            prefix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(BeforeCarrierVictory)));
        _harmony.Patch(
            AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveRun), [typeof(AbstractRoom), typeof(bool)])
            ?? throw new InvalidOperationException("SaveManager.SaveRun(AbstractRoom, bool) was not found."),
            prefix: new HarmonyMethod(typeof(LiberationVictoryProgressTraceVerificationPatch), nameof(BeforeSaveRun)));
    }

    private static void BeforeVictoryHook(IRunState runState, ICombatState? combatState, CombatRoom room)
    {
        _insideVictoryHook = true;
        SpecialGuestRunStateModifier? carrier = SpecialGuestRunStateModifier.TryGet(runState);
        bool inSnapshot = carrier != null
            && (combatState as CombatState)?.Modifiers.Contains(carrier) == true;
        Trace("hook-enter", "encounter=" + room.Encounter.GetType().Name
            + "|" + DescribeProgress(runState)
            + "|inSnapshot=" + inSnapshot);
        Trace("listeners", DescribeListeners(runState, combatState));
    }

    private static void AfterVictoryHook(IRunState runState, ref Task __result)
    {
        Task original = __result;
        __result = LogWhenDone(original, runState);
    }

    private static async Task LogWhenDone(Task original, IRunState runState)
    {
        try
        {
            await original;
        }
        finally
        {
            _insideVictoryHook = false;
            Trace("hook-exit", DescribeProgress(runState));
        }
    }

    private static void BeforeMark(IRunState? runState, string floorId)
    {
        _markCount++;
        Trace("mark", "floor=" + floorId + "|inHook=" + _insideVictoryHook + "|before:" + DescribeProgress(runState));
    }

    private static void AfterMark(IRunState? runState)
    {
        Trace("mark-done", DescribeProgress(runState));
    }

    private static void BeforeBaseEarly(AbstractModel __instance)
    {
        if (_insideVictoryHook)
        {
            Trace("early", __instance.GetType().Name + "|" + DescribeMarked(RunManager.Instance.DebugOnlyGetState()));
        }
    }

    private static void BeforeBaseVictory(AbstractModel __instance)
    {
        if (_insideVictoryHook)
        {
            Trace("victory", __instance.GetType().Name + "|" + DescribeMarked(RunManager.Instance.DebugOnlyGetState()));
        }
    }

    private static void BeforeCarrierVictory(CombatRoom room)
    {
        Trace("carrier-victory", DescribeProgress(room.CombatState.RunState));
    }

    private static void BeforeSaveRun(AbstractRoom? preFinishedRoom)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        SpecialGuestRunStateModifier? carrier = SpecialGuestRunStateModifier.TryGet(runState);
        Trace("save-run", "room=" + preFinishedRoom?.GetType().Name
            + "|" + DescribeProgress(runState)
            + "|carrierIndex=" + (runState == null || carrier == null ? -1 : runState.Modifiers.ToList().IndexOf(carrier))
            + "|stored=" + (carrier?.LibraryOfRuina_FullyLiberatedFloorIds.Replace('\n', ',') ?? "-"));
    }

    private static string DescribeProgress(IRunState? runState)
    {
        SpecialGuestRunStateModifier? carrier = SpecialGuestRunStateModifier.TryGet(runState);
        return DescribeMarked(runState) + "|carrier=" + (carrier != null);
    }

    private static string DescribeMarked(IRunState? runState) =>
        "marked=" + string.Join(",", FloorLiberationProgress.GetFullyLiberatedFloorIds(runState));

    private static string DescribeListeners(IRunState runState, ICombatState? combatState)
    {
        var parts = new List<string>();
        string? previous = null;
        int count = 0;
        foreach (AbstractModel model in runState.IterateHookListeners(combatState))
        {
            string name = model.GetType().Name;
            if (name == previous)
            {
                count++;
                continue;
            }

            if (previous != null)
            {
                parts.Add(count > 1 ? previous + "x" + count : previous);
            }

            previous = name;
            count = 1;
        }

        if (previous != null)
        {
            parts.Add(count > 1 ? previous + "x" + count : previous);
        }

        return string.Join(",", parts);
    }

    private static void Trace(string step, string detail)
    {
        var line = new StringBuilder();
        line.Append("TRACE|").Append(_label).Append('|').Append(step).Append('|').Append(detail);
        Log.Info(LogPrefix + line);
    }

    private static async Task<CombatState> StartFight(
        EncounterModel encounter,
        string seed,
        int playerCount,
        bool preCreateCarrier)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        if (playerCount <= 1)
        {
            await game.StartNewSingleplayerRun(
                ModelDb.Character<Ironclad>(),
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                seed,
                GameMode.Standard,
                ascensionLevel: 0);
        }
        else
        {
            // 与社会层轨迹套件相同的假联机：两名本地玩家的 RunState 直接交给 NGame.StartRun。
            Player[] players = Enumerable.Range(1, playerCount)
                .Select(index => Player.CreateForNewRun(
                    index % 2 == 1 ? ModelDb.Character<Ironclad>() : ModelDb.Character<Silent>(),
                    SaveManager.Instance.GenerateUnlockStateFromProgress(),
                    (ulong)index))
                .ToArray();
            RunState runState = RunState.CreateForNewRun(
                players,
                ActModel.GetDefaultList().Select(static act => act.ToMutable()).ToList(),
                Array.Empty<ModifierModel>(),
                GameMode.Standard,
                ascensionLevel: 0,
                seed);
            RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);
            MethodInfo startRun = typeof(NGame).GetMethod("StartRun", PrivateInstance)
                ?? throw new InvalidOperationException("NGame.StartRun was unavailable for fake multiplayer.");
            await (startRun.Invoke(game, [runState]) as Task
                ?? throw new InvalidOperationException("NGame.StartRun did not return a Task."));
        }

        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        if (preCreateCarrier)
        {
            // 载体在进房间之前存在时，会进入 CombatRoom 构造时的 Modifiers 快照，收到自己的胜利回调。
            SpecialGuestRunStateModifier.GetOrCreate(RunManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Run state is null."));
        }

        await RunManager.Instance.EnterRoomDebug(
            encounter.RoomType,
            encounter.RoomType == RoomType.Boss ? MapPointType.Boss : MapPointType.Unassigned,
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
