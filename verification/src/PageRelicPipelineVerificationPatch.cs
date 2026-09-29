using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina;
using LibraryOfRuina.core;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 书页遗物模式管线与选择卡的逐项记录，用于重构前后对照（A/B）。
/// 只经过重构前后都存在的入口：遗物的原版钩子、<see cref="RelicCmd.Obtain(RelicModel, Player, int)"/>、
/// <see cref="AbnormalityPageRewardPreselection"/> 的公开方法，以及按名字反射的 <c>Mode</c> 属性。
/// 选择界面由 <see cref="CardSelectCmd.UseSelector"/> 的脚本化选择器代替；每个场景输出一行 <c>ROW</c>，
/// 两次构建的 ROW 行应逐行相同。套件本身只在出现异常以外的崩溃时失败，结果对照在套件外做。
/// </summary>
internal static class PageRelicPipelineVerificationPatch
{
    private const string VerifyArg = "lor-verify-page-relic-pipeline";
    private const string LogPrefix = "[LibraryOfRuina.PageRelicPipeline.Verify] ";
    private const int InvalidModeValue = 99;
    private const int ScenarioFrameLimit = 900;

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static bool _started;
    private static int _rows;
    private static readonly StringBuilder Digest = new();

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            RecordCards();
            RecordCanonicalRelics();
            await RecordRunScenarios();
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Digest.ToString())))[..16];
            Log.Info(LogPrefix + "PAGE_RELIC_PIPELINE_OK rows=" + _rows + " digest=" + digest);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "PAGE_RELIC_PIPELINE_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void Row(string text)
    {
        _rows++;
        Digest.Append(text).Append('\n');
        Log.Info(LogPrefix + "ROW " + text);
    }

    private static Assembly ModAssembly => typeof(LibraryOfRuinaInitializer).Assembly;

    private static void RecordCards()
    {
        List<CardModel> cards = ModelDb.AllCards
            .Where(static card => card.GetType().Assembly == ModAssembly)
            .OrderBy(static card => card.Id.Entry, StringComparer.Ordinal)
            .ToList();
        int recognized = 0;
        foreach (CardModel card in cards)
        {
            bool isPageChoice = AbnormalityPageRewardPreselection.IsPageRelicChoiceCard(card);
            if (isPageChoice)
            {
                recognized++;
            }

            if (!isPageChoice && !card.GetType().Name.Contains("ChoiceCard", StringComparison.Ordinal))
            {
                continue;
            }

            Row("card " + card.Id.Entry
                + " type=" + card.GetType().FullName
                + " pageChoice=" + isPageChoice
                + " cost=" + card.EnergyCost.Canonical
                + " kind=" + card.Type
                + " rarity=" + card.Rarity
                + " target=" + card.TargetType
                + " library=" + card.ShouldShowInCardLibrary
                + " generated=" + card.CanBeGeneratedInCombat
                + " maxUpgrade=" + card.MaxUpgradeLevel
                + " pool=" + Safe(() => card.Pool.Id.Entry)
                + " visualPool=" + Safe(() => card.VisualCardPool.Id.Entry)
                + " portrait=" + Safe(() => card.PortraitPath)
                + " portraits=[" + Safe(() => string.Join(",", card.AllPortraitPaths)) + "]"
                + " vars=[" + Safe(() => DescribeVars(card.DynamicVars)) + "]"
                + " tips=[" + Safe(() => DescribeTips(card.HoverTips)) + "]"
                + " title=" + Safe(() => card.Title)
                + " desc=" + Safe(() => Hash(card.GetDescriptionForPile(PileType.None))));

            if (card.MaxUpgradeLevel > 0)
            {
                CardModel upgraded = card.ToMutable();
                Safe(() =>
                {
                    upgraded.UpgradeInternal();
                    upgraded.FinalizeUpgradeInternal();
                    return "";
                });
                Row("card+ " + card.Id.Entry
                    + " vars=[" + Safe(() => DescribeVars(upgraded.DynamicVars)) + "]"
                    + " tips=[" + Safe(() => DescribeTips(upgraded.HoverTips)) + "]"
                    + " desc=" + Safe(() => Hash(upgraded.GetDescriptionForPile(PileType.None))));
            }
        }

        Row("cards pageChoiceCount=" + recognized);
    }

    private static List<RelicModel> ModalRelics() =>
        ModelDb.AllRelics
            .Where(static relic => relic.GetType().Assembly == ModAssembly
                && ModeProperty(relic) != null)
            .OrderBy(static relic => relic.Id.Entry, StringComparer.Ordinal)
            .ToList();

    private static PropertyInfo? ModeProperty(RelicModel relic)
    {
        PropertyInfo? property = AccessTools.Property(relic.GetType(), "Mode");
        return property?.PropertyType.IsEnum == true ? property : null;
    }

    private static void RecordCanonicalRelics()
    {
        foreach (RelicModel canonical in ModalRelics())
        {
            Row("relic " + canonical.Id.Entry
                + " type=" + canonical.GetType().FullName
                + " isPage=" + AbnormalityPageRewardPreselection.IsPageRelic(canonical)
                + " hasConcrete=" + AbnormalityPageRewardPreselection.HasConcreteModeForPatch(canonical)
                + " allowedBase=" + canonical.GetType().BaseType?.Name
                + " " + DescribeRelic(canonical));
        }
    }

    private static async Task RecordRunScenarios()
    {
        Player player = await StartRun();
        try
        {
            RunState runState = (RunState)player.RunState;
            runState.AppendToMapPointHistory(MapPointType.Treasure, RoomType.Treasure, null);
            foreach (RelicModel canonical in ModalRelics())
            {
                for (int choice = 0; choice < 3; choice++)
                {
                    await Scenario(player, canonical, "obtain-choose-" + choice, choice, preselect: false, presetMode: null);
                }

                await Scenario(player, canonical, "obtain-skip", null, preselect: false, presetMode: null);
                for (int choice = 0; choice < 3; choice++)
                {
                    await Scenario(player, canonical, "preselect-choose-" + choice, choice, preselect: true, presetMode: null);
                }

                await Scenario(player, canonical, "preselect-skip", null, preselect: true, presetMode: null);
                await Scenario(player, canonical, "obtain-invalid", 0, preselect: false, presetMode: InvalidModeValue);
                await Scenario(player, canonical, "obtain-preset-1", 0, preselect: false, presetMode: 1);
            }
        }
        finally
        {
            RunManager.Instance.CleanUp(graceful: true);
            await WaitUntil(
                static () => RunManager.Instance.DebugOnlyGetState() == null,
                "page relic pipeline verifier cleanup");
        }
    }

    private static async Task Scenario(
        Player player,
        RelicModel canonical,
        string label,
        int? pageChoice,
        bool preselect,
        int? presetMode)
    {
        RelicModel relic = canonical.ToMutable();
        PropertyInfo modeProperty = ModeProperty(relic)!;
        ScriptedSelector selector = new(pageChoice);
        StringBuilder outcome = new();
        using (CardSelectCmd.UseSelector(selector))
        {
            try
            {
                if (presetMode.HasValue)
                {
                    SetMode(relic, modeProperty, presetMode.Value);
                }

                bool proceed = true;
                if (preselect)
                {
                    Task<bool> preselectTask = AbnormalityPageRewardPreselection.TryPreselectPageChoice(relic, player);
                    await WaitFor(preselectTask, label + " preselect");
                    proceed = preselectTask.Result;
                    outcome.Append(" preselected=").Append(proceed)
                        .Append(" modeAfterPreselect=").Append(ModeValue(relic, modeProperty));
                }

                if (proceed)
                {
                    Task<RelicModel> obtainTask = RelicCmd.Obtain(relic, player);
                    await WaitFor(obtainTask, label + " obtain");
                    outcome.Append(" sameInstance=").Append(ReferenceEquals(obtainTask.Result, relic));
                }

                outcome.Append(" | afterObtain ").Append(DescribeOwned(player, relic));

                if (player.Relics.Contains(relic))
                {
                    // 读档后模式为 0 或越界时，房间钩子走“确认模式合法，否则回退”的分支。
                    SetMode(relic, modeProperty, 0);
                    await WaitFor(relic.AfterRoomEntered(player.RunState.CurrentRoom!), label + " room none");
                    outcome.Append(" | roomNone ").Append(DescribeRelic(relic));
                    SetMode(relic, modeProperty, InvalidModeValue);
                    await WaitFor(relic.AfterRoomEntered(player.RunState.CurrentRoom!), label + " room invalid");
                    outcome.Append(" | roomInvalid ").Append(DescribeRelic(relic));
                }
            }
            catch (Exception ex)
            {
                outcome.Append(" | EXCEPTION ").Append(Describe(ex));
            }
        }

        try
        {
            if (player.Relics.Contains(relic))
            {
                await WaitFor(RelicCmd.Remove(relic), label + " remove");
            }
        }
        catch (Exception ex)
        {
            outcome.Append(" | REMOVE EXCEPTION ").Append(Describe(ex));
        }

        Row("scenario " + canonical.Id.Entry + " " + label
            + " selections=[" + string.Join(" ; ", selector.Calls) + "]"
            + outcome
            + " | player " + DescribePlayer(player));
    }

    private static void SetMode(RelicModel relic, PropertyInfo modeProperty, int value)
    {
        MethodInfo setter = modeProperty.GetSetMethod(nonPublic: true)
            ?? throw new MissingMethodException(relic.GetType().FullName, "set_Mode");
        setter.Invoke(relic, [Enum.ToObject(modeProperty.PropertyType, value)]);
    }

    private static int ModeValue(RelicModel relic, PropertyInfo modeProperty) =>
        Convert.ToInt32(modeProperty.GetValue(relic));

    private static string DescribeOwned(Player player, RelicModel relic) =>
        "owned=" + player.Relics.Contains(relic)
        + " ownedIds=" + player.Relics.Count(owned => owned.Id == relic.Id)
        + " " + DescribeRelic(relic);

    private static string DescribeRelic(RelicModel relic)
    {
        PropertyInfo? modeProperty = ModeProperty(relic);
        return "mode=" + (modeProperty == null ? "-" : Safe(() => ModeValue(relic, modeProperty).ToString()))
            + " dvMode=" + Safe(() => relic.DynamicVars.TryGetValue("Mode", out var modeVar)
                ? modeVar.BaseValue.ToString()
                : "-")
            + " status=" + Safe(() => relic.Status.ToString())
            + " counter=" + Safe(() => relic.ShowCounter + "/" + relic.DisplayAmount)
            + " icon=" + Safe(() => relic.PackedIconPath)
            + " tips=[" + Safe(() => DescribeTips(relic.HoverTips)) + "]"
            + " desc=" + Safe(() => Hash(relic.DynamicDescription.GetFormattedText()));
    }

    private static string DescribePlayer(Player player)
    {
        List<ModelChoiceHistoryEntry>? choices = player.RunState.CurrentMapPointHistoryEntry?
            .GetEntry(player.NetId)
            .RelicChoices;
        return "hp=" + player.Creature.CurrentHp + "/" + player.Creature.MaxHp
            + " gold=" + player.Gold
            + " relics=[" + string.Join(",", player.Relics.Select(static relic => relic.Id.Entry)) + "]"
            + " deck=" + player.Deck.Cards.Count + ":" + Hash(string.Join(",", player.Deck.Cards.Select(DescribeDeckCard)))
            + " relicChoices=[" + (choices == null
                ? "null"
                : string.Join(",", choices.Select(static choice => choice.choice.Entry + ":" + choice.wasPicked))) + "]";
    }

    private static string DescribeDeckCard(CardModel card) =>
        card.Id.Entry
        + (card.IsUpgraded ? "+" : "")
        + (card.Enchantment == null ? "" : "#" + card.Enchantment.Id.Entry);

    private static string DescribeVars(DynamicVarSet vars) =>
        string.Join(",", vars.Select(static pair => pair.Key + "=" + pair.Value.BaseValue));

    private static string DescribeTips(IEnumerable<IHoverTip> tips) =>
        string.Join(",", tips.Select(static tip => tip switch
        {
            CardHoverTip cardTip => tip.GetType().Name + ":" + cardTip.Card.Id.Entry + (cardTip.Card.IsUpgraded ? "+" : ""),
            _ => tip.GetType().Name + ":" + tip.Id
        }));

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];

    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            return "<" + Describe(ex) + ">";
        }
    }

    private static string Describe(Exception ex)
    {
        Exception inner = ex is TargetInvocationException { InnerException: not null } wrapped ? wrapped.InnerException : ex;
        string message = inner.Message.Split('\n')[0];
        return inner.GetType().Name + ": " + message;
    }

    private static async Task WaitFor(Task task, string description)
    {
        for (int frame = 0; frame < ScenarioFrameLimit && !task.IsCompleted; frame++)
        {
            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        if (!task.IsCompleted)
        {
            throw new TimeoutException("Timed out waiting for " + description + ".");
        }

        await task;
    }

    private static async Task<Player> StartRun()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        Player player = Player.CreateForNewRun(
            ModelDb.Character<Ironclad>(),
            SaveManager.Instance.GenerateUnlockStateFromProgress(),
            1uL);
        RunState runState = RunState.CreateForNewRun(
            [player],
            ActModel.GetDefaultList()
                .Select(static act => act.ToMutable())
                .ToList(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            ascensionLevel: 0,
            "PAGERELICPIPELINE");
        RunManager.Instance.SetUpNewSingleplayer(runState, shouldSave: false);
        ArmActLikeIt2OneShotVanillaEntry();

        MethodInfo startRun = typeof(NGame).GetMethod("StartRun", PrivateInstance)
            ?? throw new InvalidOperationException("NGame.StartRun was unavailable.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException("NGame.StartRun did not return a Task.");
        await startRunTask;
        await WaitUntil(static () => RunManager.Instance.DebugOnlyGetState()?.CurrentRoom != null, "first room");

        foreach (RelicModel relic in player.Relics.ToArray())
        {
            await RelicCmd.Remove(relic);
        }

        return player;
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

    /// <summary>
    /// 第一次选择（书页三选一）按给定下标选或跳过；之后的选择（拾取效果里的附魔、复制等）固定取前几张。
    /// </summary>
    private sealed class ScriptedSelector(int? pageChoice) : ICardSelector
    {
        public List<string> Calls { get; } = [];

        public Task<IEnumerable<CardModel>> GetSelectedCards(
            IEnumerable<CardModel> options,
            int minSelect,
            int maxSelect)
        {
            List<CardModel> list = options.ToList();
            List<CardModel> picked;
            if (Calls.Count == 0)
            {
                picked = pageChoice.HasValue && pageChoice.Value < list.Count
                    ? [list[pageChoice.Value]]
                    : [];
            }
            else
            {
                picked = list.Take(Math.Max(minSelect, Math.Min(1, maxSelect))).ToList();
            }

            Calls.Add("[" + string.Join(",", list.Select(static card => card.Id.Entry + (card.IsUpgraded ? "+" : "")))
                + "] " + minSelect + "-" + maxSelect
                + " -> [" + string.Join(",", picked.Select(static card => card.Id.Entry)) + "]");
            return Task.FromResult<IEnumerable<CardModel>>(picked);
        }

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new NotSupportedException("Card rewards are not part of the page relic pipeline.");
    }
}
