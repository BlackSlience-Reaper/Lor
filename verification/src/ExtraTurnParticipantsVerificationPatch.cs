using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.RedShoes;
using LibraryOfRuina.content.abnormalities.SpinyBus;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 队友的额外回合不能触发只属于持有者回合、或按轮推进的敌方效果（重构指导 §4.5 第 2 条）。
/// 假双人局：玩家 B 带原版 <see cref="PaelsEye"/>，第 1 轮不出牌，正常回合结束后取得一次额外回合；
/// 玩家 A 不参与这次额外回合。用真实回合循环驱动，在三处取样：
/// s0 第 1 轮出牌阶段（布置完毕）、s1 B 的额外回合开始钩子跑完之后（见 <see cref="EndRoundThroughExtraTurn{T}"/>）、
/// s2 第 2 轮出牌阶段。
/// <list type="bullet">
/// <item><c>field</c>：A 带红鞋书页（闪亮）、绝望骑士书页（加护，已挂起）、蕾蒂希娅书页（恶作剧，原本就按参与者过滤）、
/// 身上有腐蚀；B 带红鞋书页（闪亮）作正面对照。一只史莱姆带小红帽“无处宣泄的怒火”（敌方按轮），
/// 另一只带尖刺巴士“无法忍受的愉悦”（每个出手回合重置一次，保持原样）。敌人被击晕，s1→s2 之间没有敌方攻击。</item>
/// <item><c>kali</c>：卡莉的玩家回合开始逻辑（抗性重掷消耗 MonsterAi 随机数、再给一层缓冲）只在正常回合执行。</item>
/// </list>
/// 每处取样记为 <c>TRACE|</c> 行，逐条检查记为 <c>CHECK|</c> 行。同一个验证程序集分别配改动前后的主模组各跑一次：
/// 改动前应在标明 “extra turn” 的检查上失败，改动后全部通过。
/// 参数 <c>lor-verify-extra-turn-participants</c>；加 <c>-field</c>、<c>-kali</c> 只跑一个场景。
/// </summary>
internal static class ExtraTurnParticipantsVerificationPatch
{
    private const string VerifyArg = "lor-verify-extra-turn-participants";
    private const string LogPrefix = "[LibraryOfRuina.ExtraTurnParticipants.Verify] ";
    private const int PlayerMaxHp = 9999;
    private const decimal CorrosionAmount = 3m;

    private static readonly (string Name, Func<Task> Run)[] Scenarios =
    [
        ("field", RunField),
        ("kali", RunKali),
    ];

    private static readonly List<string> Failures = [];
    private static bool _started;
    private static string _label = string.Empty;

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
        var harmony = new Harmony("LibraryOfRuina.Verification.ExtraTurnParticipants");
        try
        {
            harmony.CreateClassProcessor(typeof(ExtraTurnReadyProbe)).Patch();
            string[] selected = SelectedScenarios();
            Log.Info(LogPrefix + "scenarios: " + string.Join(",", selected));
            foreach ((string name, Func<Task> run) in Scenarios)
            {
                if (!selected.Contains(name))
                {
                    continue;
                }

                _label = name;
                try
                {
                    await run();
                }
                catch (Exception ex)
                {
                    Trace("exception", ex.GetType().Name + ": " + ex.Message);
                    Check(false, name + ": " + ex);
                }
                finally
                {
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

            Log.Info(LogPrefix + "EXTRA_TURN_PARTICIPANTS_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "EXTRA_TURN_PARTICIPANTS_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // ---- field: relics, player-held powers, enemy powers ------------------------------------------------------

    private static async Task RunField()
    {
        (Player a, Player b) = await StartTwoPlayerRun("LOREXTRATURNFIELD");
        CombatState state = await EnterCombat(RoomType.Monster, ModelDb.Encounter<SlimesWeak>().ToMutable());

        await AddPageRelic<RedShoesPageRelic, RedShoesPageMode>(a, RedShoesPageMode.Glitter);
        await AddPageRelic<RedShoesPageRelic, RedShoesPageMode>(b, RedShoesPageMode.Glitter);
        DespairKnightPageRelic despair =
            await AddPageRelic<DespairKnightPageRelic, DespairKnightPageMode>(a, DespairKnightPageMode.Blessing);
        SetProperty(despair, nameof(DespairKnightPageRelic.BlessingPendingNextPlayerTurn), true);
        LeticiaPageRelic leticia = await AddPageRelic<LeticiaPageRelic, LeticiaPageMode>(a, LeticiaPageMode.Mischief);
        SetProperty(leticia, nameof(LeticiaPageRelic.MischiefTurnsSeen), 0);
        b.AddRelicInternal(ModelDb.Relic<PaelsEye>().ToMutable(), silent: true);

        Creature[] slimes = state.Enemies.Where(static enemy => enemy.IsAlive).OrderBy(static enemy => enemy.CombatId).ToArray();
        Require(slimes.Length >= 2, "SlimesWeak did not spawn two slimes.");
        await PowerCmdCompat.Apply<WrathServantCorrosionPower>(a.Creature, CorrosionAmount, slimes[0], null);
        await PowerCmdCompat.Apply<LittleRedUnrelievedAngerPower>(slimes[0], 1m, slimes[0], null);
        SpinyBusUnbearablePleasurePower? spiny =
            await PowerCmdCompat.Apply<SpinyBusUnbearablePleasurePower>(slimes[1], 1m, slimes[1], null);
        Require(spiny != null, "Spiny Bus power was not applied.");
        SetField(spiny!, "_triggeredThisPlayerTurn", true);
        foreach (Creature slime in slimes)
        {
            await CreatureCmd.Stun(slime, slime.Monster!.NextMove.Id);
        }

        await WaitFrames(4);
        FieldSample s0 = SampleField("s0 round1", state, a, b, despair, leticia, slimes[0], spiny!);

        FieldSample s1 = await EndRoundThroughExtraTurn(
            state,
            a,
            b,
            () => SampleField("s1 extra turn", state, a, b, despair, leticia, slimes[0], spiny!));
        FieldSample s2 = SampleField("s2 round2", state, a, b, despair, leticia, slimes[0], spiny!);

        int glitter = RedShoesPageRelic.GlitterEndTurnHpLoss;
        // 正常回合结束：两人各掉一次闪亮；A 另掉一次腐蚀（3 层），腐蚀降到 2 层。
        Check(s0.HpA - s1.HpA == glitter + (int)CorrosionAmount,
            "normal turn end: A lost " + (s0.HpA - s1.HpA) + " HP, expected Red Shoes + corrosion");
        Check(s0.HpB - s1.HpB == glitter, "normal turn end: B lost " + (s0.HpB - s1.HpB) + " HP, expected Red Shoes");
        // B 的额外回合结束：只有 B 的闪亮结算；A 不参与，红鞋与腐蚀都不动。
        Check(s1.HpB - s2.HpB == glitter,
            "extra turn end: participating B lost " + (s1.HpB - s2.HpB) + " HP, expected its own Red Shoes");
        Check(s1.HpA == s2.HpA,
            "extra turn end: non-participating A lost " + (s1.HpA - s2.HpA) + " HP, expected 0 (Red Shoes and corrosion wait for A's own turn)");
        Check(s1.CorrosionA == CorrosionAmount - 1 && s2.CorrosionA == s1.CorrosionA,
            "extra turn end: A corrosion ticked " + s1.CorrosionA + " -> " + s2.CorrosionA);
        // 绝望骑士加护：挂起到 A 自己的下一个回合开始（第 2 轮），不在 B 的额外回合开始时提前给出。
        Check(s1.BlessingPending && !s1.EnduranceA,
            "extra turn start: Despair Knight blessing stays pending for A (pending=" + s1.BlessingPending
            + " endurance=" + s1.EnduranceA + ")");
        Check(!s2.BlessingPending && s2.EnduranceA,
            "round 2 start: Despair Knight blessing resolved (pending=" + s2.BlessingPending + " endurance=" + s2.EnduranceA + ")");
        // 蕾蒂希娅书页原本就按 participants 过滤：额外回合不计数，第 2 轮计一次。
        Check(s1.Mischief == s0.Mischief && s2.Mischief != s1.Mischief,
            "Leticia mischief counter " + s0.Mischief + " -> " + s1.Mischief + " -> " + s2.Mischief);
        // 敌方按轮：额外回合不给力量，第 2 轮给一次。
        Check(s1.SlimeStrength == s0.SlimeStrength,
            "extra turn start: enemy per-round Strength " + s0.SlimeStrength + " -> " + s1.SlimeStrength + ", expected unchanged");
        Check(s2.SlimeStrength == s1.SlimeStrength + LittleRedUnrelievedAngerPower.StrengthPerPlayerTurn,
            "round 2 start: enemy per-round Strength " + s1.SlimeStrength + " -> " + s2.SlimeStrength);
        // 每个出手回合重置一次的敌方状态保持原样：B 的额外回合开始时也重置。
        Check(s0.SpinyTriggered && !s1.SpinyTriggered,
            "extra turn start: Spiny Bus per-turn trigger reset (" + s0.SpinyTriggered + " -> " + s1.SpinyTriggered + ")");
    }

    private readonly record struct FieldSample(
        int HpA,
        int HpB,
        decimal CorrosionA,
        bool BlessingPending,
        bool EnduranceA,
        int Mischief,
        decimal SlimeStrength,
        bool SpinyTriggered);

    private static FieldSample SampleField(
        string step,
        CombatState state,
        Player a,
        Player b,
        DespairKnightPageRelic despair,
        LeticiaPageRelic leticia,
        Creature angrySlime,
        SpinyBusUnbearablePleasurePower spiny)
    {
        var sample = new FieldSample(
            a.Creature.CurrentHp,
            b.Creature.CurrentHp,
            a.Creature.GetPower<WrathServantCorrosionPower>()?.Amount ?? 0m,
            despair.BlessingPendingNextPlayerTurn,
            a.Creature.Powers.Any(static power => power is LibraryEndurancePower),
            leticia.MischiefTurnsSeen,
            angrySlime.GetPower<StrengthPower>()?.Amount ?? 0m,
            GetField<bool>(spiny, "_triggeredThisPlayerTurn"));
        Trace(step, Summary(state) + " | " + sample);
        return sample;
    }

    // ---- kali: enemy per-round turn start ---------------------------------------------------------------------

    private static async Task RunKali()
    {
        (Player a, Player b) = await StartTwoPlayerRun("LOREXTRATURNKALI");
        CombatState state = await EnterCombat(RoomType.Elite, ModelDb.Encounter<KaliSpecialGuestEncounter>().ToMutable());
        b.AddRelicInternal(ModelDb.Relic<PaelsEye>().ToMutable(), silent: true);
        Kali kali = state.Enemies.Select(static enemy => enemy.Monster).OfType<Kali>().FirstOrDefault()
            ?? throw new InvalidOperationException("Kali is missing.");

        await WaitFrames(4);
        (int rng0, decimal buffer0) = SampleKali("s0 round1", state, kali);
        (int rng1, decimal buffer1) = await EndRoundThroughExtraTurn(
            state,
            a,
            b,
            () => SampleKali("s1 extra turn", state, kali));
        (int rng2, _) = SampleKali("s2 round2", state, kali);

        Check(rng1 == rng0,
            "extra turn start: Kali MonsterAi " + rng0 + " -> " + rng1 + ", expected unchanged");
        Check(buffer1 == buffer0,
            "extra turn start: Kali Buffer " + buffer0 + " -> " + buffer1 + ", expected unchanged");
        Check(rng2 > rng1, "round 2 start: Kali rerolled its resistances (MonsterAi " + rng1 + " -> " + rng2 + ")");
    }

    private static (int Rng, decimal Buffer) SampleKali(string step, CombatState state, Kali kali)
    {
        int rng = state.RunState.Rng.MonsterAi.CounterCompat();
        decimal buffer = kali.Creature.GetPower<BufferPower>()?.Amount ?? 0m;
        Trace(step, Summary(state) + " | kaliMonsterAi=" + rng + " kaliBuffer=" + buffer
            + " capacity=" + kali.IntentCapacity);
        return (rng, buffer);
    }

    // ---- setup and turn driving ------------------------------------------------------------------------------

    private static async Task<(Player A, Player B)> StartTwoPlayerRun(string seed)
    {
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        Player[] players =
        [
            Player.CreateForNewRun(ModelDb.Character<Ironclad>(), SaveManager.Instance.GenerateUnlockStateFromProgress(), 1uL),
            Player.CreateForNewRun(ModelDb.Character<Silent>(), SaveManager.Instance.GenerateUnlockStateFromProgress(), 2uL),
        ];
        RunState runState = RunState.CreateForNewRun(
            players,
            ActModel.GetDefaultList().Select(static act => act.ToMutable()).ToList(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            ascensionLevel: 0,
            seed);
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);
        MethodInfo startRun = typeof(NGame).GetMethod("StartRun", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("NGame.StartRun was unavailable for fake multiplayer.");
        await (startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException("NGame.StartRun did not return a Task."));

        // 初始遗物里可能有回合开始或结束的效果，去掉后只剩本套件布置的对象。
        foreach (Player player in players)
        {
            foreach (RelicModel relic in player.Relics.ToArray())
            {
                await RelicCmd.Remove(relic);
            }
        }

        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        return (players[0], players[1]);
    }

    private static async Task<CombatState> EnterCombat(RoomType roomType, EncounterModel encounter)
    {
        await RunManager.Instance.EnterRoomDebug(roomType, MapPointType.Unassigned, encounter, showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() is { CurrentSide: CombatSide.Player } state
                && state.Players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
            _label + " combat start");
        await WaitFrames(8);
        CombatState combat = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        foreach (Creature player in combat.PlayerCreatures)
        {
            await CreatureCmd.SetMaxHp(player, PlayerMaxHp);
            await CreatureCmd.SetCurrentHp(player, PlayerMaxHp);
        }

        return combat;
    }

    private static async Task<T> AddPageRelic<T, TMode>(Player owner, TMode mode)
        where T : RelicModel
        where TMode : struct, Enum
    {
        T relic = (T)ModelDb.Relic<T>().ToMutable();
        owner.AddRelicInternal(relic, silent: true);
        SetProperty(relic, "Mode", mode);
        await relic.BeforeCombatStart();
        return relic;
    }

    /// <summary>
    /// 结束第 1 轮，经过 <paramref name="extraTurnPlayer"/> 的额外回合与敌方回合，等到第 2 轮出牌阶段。
    /// 假联机（<c>IsSingleplayerOrFakeMultiplayer</c>）里原版 <c>AllPlayersReadyToEndTurn</c> 只要有一人准备就成立：
    /// 额外回合开始钩子跑完后，原版把不参与的玩家设为准备（<c>StartTurn</c> 末尾），额外回合随即结束。
    /// 所以额外回合里的取样放在这一次 <c>SetReadyToEndTurn</c> 的前缀里做：此时开始钩子都已执行，结束钩子还没有。
    /// 正常回合只让 <paramref name="endingPlayer"/> 结束；结束钩子仍带全部玩家（原版 playersEndingTurn）。
    /// </summary>
    private static async Task<T> EndRoundThroughExtraTurn<T>(
        CombatState state,
        Player endingPlayer,
        Player extraTurnPlayer,
        Func<T> sampleDuringExtraTurn)
    {
        int round = state.RoundNumber;
        Trace("end turn", "round=" + round);
        T? sample = default;
        bool sampled = false;
        ExtraTurnReadyProbe.OnNonParticipantReady = () =>
        {
            if (sampled || !CombatManager.Instance.PlayersTakingExtraTurn.Contains(extraTurnPlayer))
            {
                return;
            }

            sampled = true;
            sample = sampleDuringExtraTurn();
        };
        try
        {
            PlayerCmd.EndTurn(endingPlayer, canBackOut: false);
            await WaitUntil(
                () => CombatManager.Instance.IsInProgress
                    && state.RoundNumber > round
                    && state.CurrentSide == CombatSide.Player
                    && state.Players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
                _label + " round " + (round + 1));
        }
        finally
        {
            ExtraTurnReadyProbe.OnNonParticipantReady = null;
        }

        await WaitFrames(4);
        Require(sampled, "No extra turn was observed for player " + extraTurnPlayer.NetId + ".");
        return sample!;
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToEndTurn))]
    private static class ExtraTurnReadyProbe
    {
        internal static Action? OnNonParticipantReady;

        private static void Prefix(Player player)
        {
            if (OnNonParticipantReady is { } callback
                && CombatManager.Instance.PlayersTakingExtraTurn is { Count: > 0 } extra
                && !extra.Contains(player))
            {
                callback();
            }
        }
    }

    // ---- helpers ---------------------------------------------------------------------------------------------

    private static string Summary(CombatState state)
    {
        var line = new StringBuilder();
        line.Append("round=").Append(state.RoundNumber)
            .Append(" side=").Append(state.CurrentSide)
            .Append(" extra=[").Append(string.Join(",", CombatManager.Instance.PlayersTakingExtraTurn.Select(static p => p.NetId)))
            .Append("] players=")
            .Append(string.Join(";", state.PlayerCreatures.Select(static creature =>
                creature.Player?.NetId + ":" + creature.CurrentHp + " pw=["
                + string.Join(",", creature.Powers.Select(static power => power.Id.Entry + ":" + power.Amount)
                    .OrderBy(static text => text, StringComparer.Ordinal)) + "]")));
        return line.ToString();
    }

    private static void SetProperty(object target, string name, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(target.GetType().Name + "." + name + " was unavailable.");
        property.SetValue(target, value);
    }

    private static void SetField(object target, string name, object value) =>
        (target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(target.GetType().Name + "." + name + " was unavailable."))
        .SetValue(target, value);

    private static T GetField<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(target.GetType().Name + "." + name + " was unavailable."))
        .GetValue(target)!;

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

    private static void Trace(string step, string payload) =>
        Log.Info(LogPrefix + "TRACE|" + _label + "|" + step + "|" + payload);

    private static void Check(bool condition, string message)
    {
        Log.Info(LogPrefix + "CHECK|" + _label + "|" + (condition ? "pass" : "FAIL") + "|" + message);
        if (!condition)
        {
            Failures.Add(_label + ": " + message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
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

    // headless 下帧不限速，按帧数计的超时只有几秒；这里按墙钟计。
    private static async Task WaitUntil(Func<bool> predicate, string description)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        if (!predicate())
        {
            CombatState? state = CombatManager.Instance.DebugOnlyGetState();
            throw new TimeoutException("Timed out waiting for " + description + " (combat="
                + CombatManager.Instance.IsInProgress
                + " side=" + state?.CurrentSide
                + " round=" + state?.RoundNumber
                + " extra=" + CombatManager.Instance.PlayersTakingExtraTurn.Count
                + " phase=" + string.Join(",", state?.Players.Select(static p => p.PlayerCombatState?.Phase.ToString()) ?? [])
                + ").");
        }
    }
}
