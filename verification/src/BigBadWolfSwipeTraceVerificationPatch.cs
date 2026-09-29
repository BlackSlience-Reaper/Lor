using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.abnormalities.BigBadWolf;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 大灰狼吞牌标记的轨迹转储，用于改动前后逐行对照。
/// <list type="bullet">
/// <item><c>kill-1p</c> / <c>kill-2p</c>：吞牌后用原版 <c>CreatureCmd.Kill</c> 击杀，记额外奖励、牌组、运行状态，
/// 以及房间序列化再读回（原版读档恢复额外奖励的路径）后的奖励；</item>
/// <item><c>spit-1p</c>：吞牌后开启破绽窗口并吐牌，记牌组、手牌与血量；</item>
/// <item><c>born-1p</c>：吞牌后“天生如此”消化，记已吃计数与下回合力量。</item>
/// </list>
/// 标记能力按玩家逐层记录类型、叠加方式、实例方式、目标、被吞的牌、名称、描述、悬停提示与图标资源路径；
/// <c>display</c> 行在若干语言下各记一遍名称与描述。能力类型名与模型 ID 单独写在 <c>type</c> 行，改动前后按设计不同，
/// 其余字段应逐行相同。套件只按名字反射狼的私有成员，不引用新增类型，可以配改动前的主模组运行。
/// </summary>
internal static class BigBadWolfSwipeTraceVerificationPatch
{
    private const string VerifyArg = "lor-verify-big-bad-wolf-swipe-trace";
    private const string LogPrefix = "[LibraryOfRuina.BigBadWolfSwipeTrace.Verify] ";

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] DisplayLanguages = ["eng", "zhs", "jpn", "kor", "fra", "deu"];
    private static readonly List<string> Failures = [];

    private static bool _started;
    private static string _label = "";

    private enum Ending
    {
        Kill,
        Spit,
        BornToBe
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
            await RunGuarded("kill-1p", () => RunScenario(Ending.Kill, 1, traceDisplay: true));
            await RunGuarded("kill-2p", () => RunScenario(Ending.Kill, 2, traceDisplay: false));
            await RunGuarded("spit-1p", () => RunScenario(Ending.Spit, 1, traceDisplay: false));
            await RunGuarded("born-1p", () => RunScenario(Ending.BornToBe, 1, traceDisplay: false));

            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(
                    Failures.Count + " scenario(s) failed: " + string.Join("; ", Failures));
            }

            Log.Info(LogPrefix + "BIG_BAD_WOLF_SWIPE_TRACE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "BIG_BAD_WOLF_SWIPE_TRACE_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunGuarded(string label, Func<Task> run)
    {
        _label = label;
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

    private static async Task RunScenario(Ending ending, int playerCount, bool traceDisplay)
    {
        CombatState combatState = await StartFight(
            "BIGBADWOLFSWIPE_" + _label.ToUpperInvariant().Replace('-', '_'),
            playerCount);
        BigBadWolf wolf = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<BigBadWolf>()
            .Single();
        RunState runState = RunManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Run state is null.");
        PlayerChoiceContext context = new ThrowingPlayerChoiceContext();
        // 吞牌从抽牌堆与弃牌堆里选，开局抽牌完成前后候选不同；等手牌稳定再开始，避免轨迹随帧数浮动。
        await WaitForStableHands(combatState);
        Trace("start", DescribeState(combatState, runState, wolf));

        await InvokePrivate(wolf, "StealPendingCards", (IReadOnlyList<Creature>)combatState.PlayerCreatures.ToList());
        await WaitFrames(4);
        Require(wolf.HasPendingCard, _label + ": the wolf did not swallow a card");
        Trace("steal", DescribeState(combatState, runState, wolf));
        TraceSwipePowers(wolf);
        if (traceDisplay)
        {
            TraceDisplay(wolf);
        }

        switch (ending)
        {
            case Ending.Kill:
                await CreatureCmd.Kill(wolf.Creature, force: true);
                await WaitFrames(4);
                Trace("killed", DescribeRewards(combatState) + "|" + DescribeState(combatState, runState, wolf));
                // 假联机里第二名玩家没有客户端，战斗停在等待其结束回合，只在单人场景里等胜利结算。
                if (playerCount == 1)
                {
                    // 玩家回合中途击杀不会自动走到胜利判定（原版在行动结束时检查），与其他套件一样手动检查一次。
                    await CombatManager.Instance.CheckWinCondition();
                    await WaitUntil(static () => !CombatManager.Instance.IsInProgress, _label + " victory", 1800);
                    await WaitFrames(8);
                    Trace("victory", DescribeRewards(combatState) + "|" + DescribeDecks(runState));
                }

                TraceRoundTrip((CombatRoom)runState.CurrentRoom!, runState);
                break;
            case Ending.Spit:
                await wolf.BeginSwallowVulnerabilityWindow(context);
                await InvokePrivate(wolf, "SpitPendingCard", context);
                await WaitFrames(4);
                Trace("spit", DescribeState(combatState, runState, wolf));
                break;
            case Ending.BornToBe:
                await wolf.ResolveBornToBe(context);
                await WaitFrames(4);
                Trace("born", DescribeState(combatState, runState, wolf)
                    + "|eaten=" + DescribeEatenCounts(wolf));
                break;
        }
    }

    private static async Task WaitForStableHands(CombatState combatState)
    {
        string previous = "";
        int stableFrames = 0;
        for (int frame = 0; frame < 1800 && stableFrames < 60; frame++)
        {
            string current = string.Join(";", combatState.Players.Select(static player =>
                CardPile.GetCards(player, PileType.Hand).Count() + "/" + CardPile.GetCards(player, PileType.Draw).Count()));
            bool drawn = combatState.Players.All(static player => CardPile.GetCards(player, PileType.Hand).Any());
            stableFrames = drawn && current == previous ? stableFrames + 1 : 0;
            previous = current;
            await WaitFrames(1);
        }

        Require(stableFrames >= 60, _label + ": opening hands did not settle");
    }

    private static void TraceSwipePowers(BigBadWolf wolf)
    {
        foreach (PowerModel power in SwipePowers(wolf))
        {
            Trace("type", power.GetType().FullName + "|id=" + power.Id);
            var stolen = (CardModel?)power.GetType().GetProperty("StolenCard")?.GetValue(power);
            Trace("power", "type=" + power.Type
                + "|stack=" + power.StackType
                + "|instance=" + power.InstanceType
                + "|amount=" + power.Amount
                + "|visible=" + power.IsVisible
                + "|target=" + power.Target?.Player?.NetId
                + "|stolen=" + stolen?.Id.Entry
                + "|title=" + power.Title.LocTable + ":" + power.Title.LocEntryKey
                + "=" + power.Title.GetFormattedText()
                + "|description=" + power.Description.GetFormattedText()
                + "|smart=" + power.HasSmartDescription
                + "|icon=" + power.Icon?.ResourcePath
                + "|bigIcon=" + power.BigIcon?.ResourcePath
                + "|tips=" + DescribeHoverTips(power.HoverTips));
        }
    }

    private static void TraceDisplay(BigBadWolf wolf)
    {
        PowerModel? power = SwipePowers(wolf).FirstOrDefault();
        if (power == null)
        {
            return;
        }

        string original = LocManager.Instance.Language;
        try
        {
            foreach (string language in DisplayLanguages)
            {
                LocManager.Instance.SetLanguage(language);
                Trace("display", language
                    + "|title=" + power.Title.GetFormattedText()
                    + "|description=" + power.Description.GetFormattedText()
                    + "|tips=" + DescribeHoverTips(power.HoverTips));
            }
        }
        finally
        {
            LocManager.Instance.SetLanguage(original);
        }
    }

    private static void TraceRoundTrip(CombatRoom room, RunState runState)
    {
        SerializableRoom serialized = room.ToSerializable();
        Trace("serialized", string.Join(";", serialized.ExtraRewards
            .OrderBy(static pair => pair.Key)
            .Select(static pair => pair.Key + ":" + string.Join(",", pair.Value.Select(static reward =>
                reward.RewardType + "/" + reward.SpecialCard?.Id?.Entry
                + "+" + reward.SpecialCard?.CurrentUpgradeLevel
                + "/source=" + reward.CustomDescriptionEncounterSourceId.Entry)))));
        CombatRoom reloaded = CombatRoom.FromSerializable(serialized, runState);
        Trace("reloaded", string.Join(";", reloaded.ExtraRewards
            .OrderBy(static pair => pair.Key.NetId)
            .Select(pair => pair.Key.NetId + ":" + string.Join(",", pair.Value.Select(DescribeReward)))));
    }

    private static IEnumerable<PowerModel> SwipePowers(BigBadWolf wolf)
    {
        var pending = (IEnumerable)(typeof(BigBadWolf).GetField("_pendingCards", PrivateInstance)?.GetValue(wolf)
            ?? throw new InvalidOperationException("BigBadWolf._pendingCards was not found."));
        foreach (object entry in pending)
        {
            yield return (PowerModel)(entry.GetType().GetProperty("SwipePower")?.GetValue(entry)
                ?? throw new InvalidOperationException("PendingStolenCard.SwipePower was not found."));
        }
    }

    private static string DescribeEatenCounts(BigBadWolf wolf)
    {
        var counts = (Dictionary<ulong, int>)(typeof(BigBadWolf)
            .GetField("_eatenCardCountsByPlayerId", PrivateInstance)?.GetValue(wolf)
            ?? throw new InvalidOperationException("BigBadWolf._eatenCardCountsByPlayerId was not found."));
        return string.Join(",", counts.OrderBy(static pair => pair.Key)
            .Select(static pair => pair.Key + "=" + pair.Value));
    }

    private static string DescribeState(CombatState combatState, RunState runState, BigBadWolf wolf)
    {
        var text = new StringBuilder();
        text.Append("wolf=").Append(wolf.Creature.CurrentHp).Append('/').Append(wolf.Creature.MaxHp)
            .Append(wolf.Creature.IsDead ? ":dead" : "")
            .Append(":pending=").Append(wolf.HasPendingCard)
            .Append(":pw=").Append(string.Join(",", wolf.Creature.Powers.Select(DescribePowerShort)));
        text.Append("|combat=").Append(CombatManager.Instance.IsInProgress);
        foreach (Player player in combatState.Players.OrderBy(static player => player.NetId))
        {
            text.Append("|p").Append(player.NetId)
                .Append(":hand=").Append(DescribeCards(CardPile.GetCards(player, PileType.Hand)))
                .Append(":draw=").Append(DescribeCards(CardPile.GetCards(player, PileType.Draw)))
                .Append(":discard=").Append(DescribeCards(CardPile.GetCards(player, PileType.Discard)))
                .Append(":loot=").Append(runState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).StolenLoot.ToString() ?? "-")
                .Append(":pw=").Append(string.Join(",", player.Creature.Powers.Select(DescribePowerShort)));
        }

        text.Append('|').Append(DescribeDecks(runState));
        return text.ToString();
    }

    // 标记能力的类型名在 type 行单独比较，这里统一写成 Swipe，避免同一行混入预期差异。
    private static string DescribePowerShort(PowerModel power) =>
        (power.GetType().Name.EndsWith("SwipePower", StringComparison.Ordinal) ? "Swipe" : power.GetType().Name)
        + "=" + power.Amount
        + (power.Target?.Player != null ? "@" + power.Target.Player.NetId : "");

    private static string DescribeDecks(RunState runState) =>
        "decks=" + string.Join(";", runState.Players.OrderBy(static player => player.NetId).Select(player =>
            player.NetId + ":" + DescribeCards(player.Deck.Cards)
            + ":inRun=" + player.Deck.Cards.Count(runState.ContainsCard)));

    private static string DescribeCards(IEnumerable<CardModel> cards) =>
        string.Join(",", cards
            .GroupBy(static card => card.Id.Entry + (card.CurrentUpgradeLevel > 0 ? "+" + card.CurrentUpgradeLevel : ""))
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => group.Key + "x" + group.Count()));

    private static string DescribeRewards(CombatState combatState)
    {
        var room = (CombatRoom?)combatState.RunState.CurrentRoom;
        if (room == null)
        {
            return "rewards=-";
        }

        return "rewards=" + string.Join(";", room.ExtraRewards
            .OrderBy(static pair => pair.Key.NetId)
            .Select(pair => pair.Key.NetId + ":" + string.Join(",", pair.Value.Select(DescribeReward))));
    }

    private static string DescribeReward(Reward reward)
    {
        if (reward is not SpecialCardReward special)
        {
            return reward.GetType().Name;
        }

        SerializableReward serialized = special.ToSerializable();
        return "SpecialCard/" + serialized.SpecialCard?.Id?.Entry
            + "/source=" + serialized.CustomDescriptionEncounterSourceId.Entry
            + "/text=" + special.Description.GetFormattedText();
    }

    private static string DescribeHoverTips(IEnumerable<IHoverTip> tips) =>
        string.Join(" ~ ", tips.Select(static tip => tip switch
        {
            HoverTip hover => "tip:" + hover.Title + ":" + hover.Description + ":smart=" + hover.IsSmart
                + ":instanced=" + hover.IsInstanced + ":icon=" + hover.Icon?.ResourcePath,
            CardHoverTip card => "card:" + card.Card.Id.Entry,
            _ => tip.GetType().Name
        }));

    private static async Task InvokePrivate(BigBadWolf wolf, string name, object argument)
    {
        MethodInfo method = typeof(BigBadWolf).GetMethod(name, PrivateInstance)
            ?? throw new InvalidOperationException("BigBadWolf." + name + " was not found.");
        await (method.Invoke(wolf, [argument]) as Task
            ?? throw new InvalidOperationException("BigBadWolf." + name + " did not return a Task."));
    }

    private static void Trace(string step, string detail)
    {
        Log.Info(LogPrefix + "TRACE|" + _label + "|" + step + "|" + detail);
    }

    private static async Task<CombatState> StartFight(string seed, int playerCount)
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
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<BigBadWolfWeak>().ToMutable(),
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
