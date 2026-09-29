using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 社会层试炼状态机的轨迹转储，用于改动前后逐行对照（不判定数值，只要求跑完）。
/// <para>
/// 每个场景开一场社会层解放战，按回合边界依次直接调用遭遇的 <c>OnBeforeEnemyTurn</c>、<c>OnAfterEnemyTurn</c>、
/// <c>OnBeforePlayerTurn</c>（与 <c>FalseThrone</c> 的回合钩子调用的是同一组方法），每一步后记一行 <c>TRACE|</c>：
/// 试炼、回合、待进入的试炼、计划行动、遭遇存档字典（按枚举顺序，顺序本身是存档格式）、敌人与玩家的血量、混乱值、能力、
/// 相关卡牌所在牌堆，以及几条共享随机数流的计数器。
/// </para>
/// <list type="bullet">
/// <item><c>passive</c> / <c>passive-tough</c>：不击杀召唤物，各试炼按回合数超时推进；家园第 3 回合排入愤怒。
/// 后者用坚韧敌人进阶，走智慧卡数与判定阈值的另一分支。</item>
/// <item><c>active</c>：樵夫试炼击杀全部水晶、狮子试炼提交卡牌并击杀全部面孔，愤怒试炼记变身与终结一击。</item>
/// <item><c>multi-lion</c> / <c>multi-rage</c>：两名玩家，分别在狮子试炼击杀非胆小猫持有者、在愤怒试炼击杀奥兹玛持有者。</item>
/// <item><c>resume</c>：前两个场景每进入一个新试炼时保存的字典，读进新遭遇再开战，走读档恢复路径后再推进一轮。</item>
/// </list>
/// </summary>
internal static class SocialTrialTraceVerificationPatch
{
    private const string VerifyArg = "lor-verify-social-trial-trace";
    private const string LogPrefix = "[LibraryOfRuina.SocialTrialTrace.Verify] ";
    private const int MaxCycles = 16;

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly List<string> Failures = [];
    private static bool _started;

    private enum Scenario
    {
        Passive,
        Active,
        MultiLion,
        MultiRage
    }

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
            var saves = new List<(string Label, Dictionary<string, string> State)>();
            await RunGuarded("passive", () => RunScenario("passive", Scenario.Passive, 0, 1, saves));
            await RunGuarded("passive-tough", () => RunScenario(
                "passive-tough",
                Scenario.Passive,
                (int)AscensionLevel.ToughEnemies,
                1,
                null));
            await RunGuarded("active", () => RunScenario("active", Scenario.Active, 0, 1, saves));
            await RunGuarded("multi-lion", () => RunScenario("multi-lion", Scenario.MultiLion, 0, 2, null));
            await RunGuarded("multi-rage", () => RunScenario("multi-rage", Scenario.MultiRage, 0, 2, null));
            foreach ((string label, Dictionary<string, string> state) in saves)
            {
                await RunGuarded("resume-" + label, () => RunResume("resume-" + label, state));
            }

            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(
                    Failures.Count + " scenario(s) threw: " + string.Join("; ", Failures));
            }

            Log.Info(LogPrefix + "SOCIAL_TRIAL_TRACE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "SOCIAL_TRIAL_TRACE_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunGuarded(string label, Func<Task> run)
    {
        // 一个场景抛错不影响后面的场景；异常类型与消息也写进 TRACE，对照时一并比较。
        try
        {
            await run();
        }
        catch (Exception ex)
        {
            Log.Info(LogPrefix + "TRACE|" + label + "|exception|" + ex.GetType().Name + ": " + ex.Message);
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

    private static async Task RunScenario(
        string label,
        Scenario scenario,
        int ascension,
        int playerCount,
        List<(string Label, Dictionary<string, string> State)>? saves)
    {
        CombatState state = await StartFight(
            ModelDb.Encounter<SocialFloorLiberationEncounter>().ToMutable(),
            "SOCIALTRIAL_" + label.ToUpperInvariant().Replace('-', '_'),
            ascension,
            playerCount);
        var encounter = (SocialFloorLiberationEncounter)state.Encounter!;
        FalseThrone boss = FindBoss(state);
        PlayerChoiceContext context = new ThrowingPlayerChoiceContext();
        Trace(label, "start", state);
        saves?.Add((label + "-" + encounter.Trial, encounter.SaveCustomState()));
        SocialFloorTrial lastSaved = encounter.Trial;

        for (int cycle = 0; cycle < MaxCycles && CombatManager.Instance.IsInProgress; cycle++)
        {
            string prefix = "c" + cycle.ToString("00");
            await ApplyScenarioActions(label, prefix, scenario, encounter, state);
            if (!CombatManager.Instance.IsInProgress || !boss.Creature.IsAlive)
            {
                break;
            }

            await encounter.OnBeforeEnemyTurn(context, boss, state);
            await Settle();
            Trace(label, prefix + ".beforeEnemy", state);

            await encounter.OnAfterEnemyTurn(context, boss, state);
            await Settle();
            Trace(label, prefix + ".afterEnemy", state);

            await encounter.OnBeforePlayerTurn(boss, state);
            await Settle();
            Trace(label, prefix + ".beforePlayer", state);

            if (saves != null && encounter.Trial != lastSaved)
            {
                saves.Add((label + "-" + encounter.Trial, encounter.SaveCustomState()));
                lastSaved = encounter.Trial;
            }

            if (encounter.Trial == SocialFloorTrial.Rage && encounter.TrialRound >= 3)
            {
                break;
            }
        }

        Trace(label, "end", state);
    }

    private static async Task ApplyScenarioActions(
        string label,
        string prefix,
        Scenario scenario,
        SocialFloorLiberationEncounter encounter,
        CombatState state)
    {
        SocialFloorTrial trial = encounter.Trial;
        int round = encounter.TrialRound;
        switch (trial)
        {
            case SocialFloorTrial.Woodsman when scenario == Scenario.Active:
                // 第 0 回合击杀前两个水晶、第 1 回合击杀第三个：第 2 回合结束时全部摧毁，提前进入稻草人试炼。
                foreach (string slot in round == 0
                             ? [SocialFloorLiberationEncounter.CrystalSlotOne, SocialFloorLiberationEncounter.CrystalSlotTwo]
                             : round == 1
                                 ? [SocialFloorLiberationEncounter.CrystalSlotThree]
                                 : Array.Empty<string>())
                {
                    await KillEnemyAt<EmeraldCrystal>(label, prefix, state, slot);
                }
                break;

            case SocialFloorTrial.Lion when scenario == Scenario.Active && round == 0:
                if (encounter.ScaredyCatPlayerNetId is { } catNetId)
                {
                    encounter.MarkLionCardsSubmitted(catNetId, 2);
                    encounter.MarkCouragePlayed(catNetId);
                    Trace(label, prefix + ".lionMarks", state);
                }
                foreach (string slot in SocialFloorLiberationEncounter.FaceSlots)
                {
                    await KillEnemyAt<ScowlingFace>(label, prefix, state, slot);
                }
                break;

            case SocialFloorTrial.Lion when scenario == Scenario.MultiLion && round == 0:
                Player? bystander = state.Players
                    .Where(player => player.Creature.IsAlive && player.NetId != encounter.ScaredyCatPlayerNetId)
                    .OrderBy(static player => player.NetId)
                    .FirstOrDefault();
                if (bystander != null)
                {
                    await CreatureCmd.Kill(bystander.Creature, force: true);
                    await Settle();
                    Trace(label, prefix + ".killPlayer" + bystander.NetId, state);
                }
                break;

            case SocialFloorTrial.Home when round == 2:
                encounter.QueueRageTrial();
                Trace(label, prefix + ".queueRage", state);
                break;

            case SocialFloorTrial.Rage when scenario == Scenario.Active && round == 1:
                encounter.MarkPowderCost(7);
                encounter.MarkTransformed();
                bool first = encounter.TryMarkFinalStrikeTriggered();
                bool second = encounter.TryMarkFinalStrikeTriggered();
                Trace(label, prefix + ".transform|final=" + first + "," + second, state);
                break;

            case SocialFloorTrial.Rage when scenario == Scenario.MultiRage && round == 0:
                Player? holder = state.Players.FirstOrDefault(player =>
                    player.Creature.IsAlive && player.NetId == encounter.OzmaPlayerNetId);
                if (holder != null)
                {
                    await CreatureCmd.Kill(holder.Creature, force: true);
                    await Settle();
                    Trace(label, prefix + ".killOzma" + holder.NetId, state);
                }
                break;
        }
    }

    private static async Task RunResume(string label, Dictionary<string, string> saved)
    {
        Log.Info(LogPrefix + "TRACE|" + label + "|loaded|" + FormatDictionary(saved));
        var encounter = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>(saved));
        CombatState state = await StartFight(encounter, "SOCIALTRIAL_RESUME", 0, 1);
        FalseThrone boss = FindBoss(state);
        PlayerChoiceContext context = new ThrowingPlayerChoiceContext();
        Trace(label, "start", state);

        await encounter.OnBeforePlayerTurn(boss, state);
        await Settle();
        Trace(label, "restore.beforePlayer", state);

        await encounter.OnBeforeEnemyTurn(context, boss, state);
        await Settle();
        Trace(label, "beforeEnemy", state);

        await encounter.OnAfterEnemyTurn(context, boss, state);
        await Settle();
        Trace(label, "afterEnemy", state);

        await encounter.OnBeforePlayerTurn(boss, state);
        await Settle();
        Trace(label, "beforePlayer", state);
    }

    private static async Task KillEnemyAt<T>(
        string label,
        string prefix,
        CombatState state,
        string slot) where T : MonsterModel
    {
        Creature? target = state.Enemies.FirstOrDefault(enemy =>
            enemy.IsAlive && enemy.Monster is T && enemy.SlotName == slot);
        if (target == null)
        {
            Log.Info(LogPrefix + "TRACE|" + label + "|" + prefix + ".kill " + slot + "|missing");
            return;
        }

        await CreatureCmd.Kill(target, force: true);
        await Settle();
        Trace(label, prefix + ".kill " + slot, state);
    }

    private static FalseThrone FindBoss(CombatState state) =>
        state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<FalseThrone>()
            .Single();

    private static void Trace(string label, string step, CombatState state)
    {
        var line = new StringBuilder();
        line.Append("TRACE|").Append(label).Append('|').Append(step);
        if (state.Encounter is SocialFloorLiberationEncounter encounter)
        {
            line.Append("|trial=").Append(encounter.Trial)
                .Append("|round=").Append(encounter.TrialRound)
                .Append("|setup=").Append(encounter.SetupComplete)
                .Append("|pending=").Append(encounter.HasPendingTrial ? encounter.PendingTrial.ToString() : "-")
                .Append("|move=").Append(encounter.PlannedMove)
                .Append("|keepCrystals=").Append(encounter.ShouldKeepWoodsmanSummons)
                .Append("|enc=").Append(FormatDictionary(encounter.SaveCustomState()));
        }

        line.Append("|combat=").Append(CombatManager.Instance.IsInProgress);
        line.Append("|enemies=").Append(string.Join(";", state.Enemies.Select(DescribeCreature)));
        line.Append("|players=").Append(string.Join(";", state.Players.Select(DescribePlayer)));
        RunRngSet rng = state.RunState.Rng;
        line.Append("|rng=")
            .Append("targets:").Append(Counter(rng.CombatTargets))
            .Append(",ai:").Append(Counter(rng.MonsterAi))
            .Append(",shuffle:").Append(Counter(rng.Shuffle))
            .Append(",cardgen:").Append(Counter(rng.CombatCardGeneration));
        Log.Info(LogPrefix + line);
    }

    private static int Counter(Rng rng) => rng.ToSerializable().counter;

    private static string FormatDictionary(IReadOnlyDictionary<string, string> state) =>
        string.Join(",", state.Select(static pair => pair.Key + "=" + pair.Value));

    private static string DescribeCreature(Creature creature)
    {
        var text = new StringBuilder();
        text.Append(creature.Monster?.GetType().Name ?? "player")
            .Append('@').Append(creature.SlotName)
            .Append(':').Append(creature.CurrentHp).Append('/').Append(creature.MaxHp)
            .Append(":blk").Append(creature.Block);
        if (creature is LibraryCreature library)
        {
            text.Append(":chao").Append(library.CurrentChaoValue).Append('/').Append(library.MaxChaoValue);
        }

        text.Append(creature.IsDead ? ":dead" : "")
            .Append(creature.IsStunned ? ":stunned" : "")
            .Append(":next=").Append(creature.Monster?.NextMove?.Id)
            .Append(":pw=").Append(DescribePowers(creature));
        return text.ToString();
    }

    private static string DescribePlayer(Player player)
    {
        var text = new StringBuilder();
        text.Append(player.NetId)
            .Append(':').Append(player.Creature.CurrentHp).Append('/').Append(player.Creature.MaxHp)
            .Append(":blk").Append(player.Creature.Block)
            .Append(player.Creature.IsDead ? ":dead" : "")
            .Append(":pw=").Append(DescribePowers(player.Creature))
            .Append(":cards=");
        IEnumerable<CardModel> cards = player.PlayerCombatState?.AllCards ?? [];
        text.Append(string.Join(",", cards
            .GroupBy(static card => card.GetType().Name + "/" + card.Pile?.Type)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => group.Key + "x" + group.Count())));
        return text.ToString();
    }

    private static string DescribePowers(Creature creature) =>
        string.Join(",", creature.Powers.Select(static power =>
            power.GetType().Name + "=" + power.Amount));

    private static async Task Settle() => await WaitFrames(4);

    private static async Task<CombatState> StartFight(
        EncounterModel encounter,
        string seed,
        int ascension,
        int playerCount)
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
                ascensionLevel: ascension);
        }
        else
        {
            // 与哲学层套件相同的假联机：两名本地玩家的 RunState 直接交给 NGame.StartRun。
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
                ascensionLevel: ascension,
                seed);
            RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);
            MethodInfo startRun = typeof(NGame).GetMethod("StartRun", PrivateInstance)
                ?? throw new InvalidOperationException("NGame.StartRun was unavailable for fake multiplayer.");
            await (startRun.Invoke(game, [runState]) as Task
                ?? throw new InvalidOperationException("NGame.StartRun did not return a Task."));
        }

        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
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
}
