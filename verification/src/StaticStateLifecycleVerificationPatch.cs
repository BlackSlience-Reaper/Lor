using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.content.abnormalities.QueenBee;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 局级、战斗级静态状态的生命周期检查。只经过修复前后都存在的入口（各状态自己的读写方法、原版命令与
/// <c>RunManager.CleanUp</c>），同一个验证程序集可以配修复前后的主模组各跑一次：修复前应在对应场景失败。
/// <list type="bullet">
/// <item><c>settlement-stores</c>：六层结算记录在离开本局后回到初值。</item>
/// <item><c>settlement-reload</c>：上一局留下待结算标志后，读入停在语言层终局奖励界面的另一局，结算事件的击杀数取自这一局的遭遇。</item>
/// <item><c>solemn-mourning</c>：战斗中途退出后开新局，救赎之手在新战斗里照常计入封印牌。</item>
/// <item><c>solemn-mourning-limit</c>：救赎之手计满 4 张封印牌后，同一回合里第 5 张封印牌不能打出（文案“每回合最多打出 4 张”）。</item>
/// <item><c>solemn-mourning-nested</c>：已打出 3 张封印牌后，被封印的破灭自动打出的封印牌不能再打出（名额在出牌开始时占用）。</item>
/// <item><c>combat-tables</c>：三份死亡归因表在离开本局后清空。</item>
/// <item><c>page-preselect</c>：假双人局里一名玩家的书页预选等待期间，另一名玩家的书页遗物获得后跳过选择，照常移除。</item>
/// </list>
/// </summary>
internal static class StaticStateLifecycleVerificationPatch
{
    private const string VerifyArg = "lor-verify-static-state-lifecycle";
    private const string LogPrefix = "[LibraryOfRuina.StaticStateLifecycle.Verify] ";

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly List<string> Failures = [];
    private static bool _started;

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
            await RunGuarded("settlement-stores", VerifySettlementStores);
            await RunGuarded("settlement-reload", VerifySettlementReload);
            await RunGuarded("solemn-mourning", VerifySolemnMourningCounter);
            await RunGuarded("solemn-mourning-limit", VerifySolemnMourningLimit);
            await RunGuarded("solemn-mourning-nested", VerifySolemnMourningNestedAutoPlay);
            await RunGuarded("combat-tables", VerifyCombatTables);
            await RunGuarded("page-preselect", VerifyPagePreselect);
            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(Failures.Count + " scenario(s) failed: " + string.Join("; ", Failures));
            }

            Log.Info(LogPrefix + "STATIC_STATE_LIFECYCLE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "STATIC_STATE_LIFECYCLE_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunGuarded(string label, Func<Task> run)
    {
        // 一个场景失败不影响后面的场景；每个场景结束都离开本局，保证下一个场景从干净状态开始。
        try
        {
            await run();
            Trace(label, "result", "ok");
        }
        catch (Exception ex)
        {
            Trace(label, "result", "FAIL " + ex.GetType().Name + ": " + ex.Message);
            Failures.Add(label + ": " + ex.Message);
        }
        finally
        {
            await LeaveRun(label + " cleanup");
        }
    }

    private static Task VerifySettlementStores()
    {
        var language = LoadEncounter<LanguageFloorLiberationEncounter>(3, 4);
        var art = LoadEncounter<ArtFloorLiberationEncounter>(3, 4);
        var history = LoadEncounter<HistoryFloorLiberationEncounter>(3, 4);
        var literature = LoadEncounter<LiteratureFloorLiberationEncounter>(3, 4);
        var technology = LoadEncounter<TechnologyFloorLiberationEncounter>(3, 4);
        var natural = LoadEncounter<NaturalFloorLiberationEncounter>(3, 4);
        Require(language.KilledBossCount == 4 && language.SettlementTriggered
                && art.KilledBossCount == 4 && art.SettlementTriggered
                && history.KilledBossCount == 4 && history.SettlementTriggered
                && literature.KilledBossCount == 4 && literature.SettlementTriggered
                && technology.KilledBossCount == 4 && technology.SettlementTriggered
                && natural.KilledBossCount == 4 && natural.SettlementTriggered,
            "Encounter state did not load four kills with settlement triggered.");

        LanguageFloorLiberationSettlementStore.Record(language);
        ArtFloorLiberationSettlementStore.Record(art);
        HistoryFloorLiberationSettlementStore.Record(history);
        LiteratureFloorLiberationSettlementStore.Record(literature);
        TechnologyFloorLiberationSettlementStore.Record(technology);
        NaturalFloorLiberationSettlementStore.Record(natural);
        string recorded = DescribeSettlementStores();
        Trace("settlement-stores", "recorded", recorded);
        Require(recorded == "lang=4/True art=4/True hist=4/True lit=4/True tech=4/True nat=4/True",
            "Recording four-kill settlements did not queue all six floors: " + recorded);

        // 赢下解放战后没有按继续就离开本局。
        RunManager.Instance.CleanUp(graceful: true);
        string afterCleanup = DescribeSettlementStores();
        Trace("settlement-stores", "afterCleanup", afterCleanup);
        Require(afterCleanup == "lang=2/False art=2/False hist=2/False lit=2/False tech=2/False nat=0/False",
            "Leaving the run kept settlement state: " + afterCleanup);
        return Task.CompletedTask;
    }

    private static async Task VerifySettlementReload()
    {
        // 上一局：赢下五阶段语言层解放，停在终局奖励界面时离开。
        var previous = (LanguageFloorLiberationEncounter)ModelDb.Encounter<LanguageFloorLiberationEncounter>().ToMutable();
        previous.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "5",
            ["PhaseComplete"] = "True",
            ["TransitionPending"] = "False",
            ["KilledBossCount"] = "5",
            ["SettlementTriggered"] = "True",
            ["EndedByLethalDamage"] = "False"
        });
        LanguageFloorLiberationSettlementStore.Record(previous);
        Require(LanguageFloorLiberationSettlementStore.PendingSettlement,
            "The previous run's five-kill settlement was not queued.");
        RunManager.Instance.CleanUp(graceful: true);

        // 这一局：读档回到两杀结算的终局奖励界面（预完成的战斗房间，遭遇状态来自存档）。
        RunState runState = await StartSingleplayerRun("STATICSTATE_SETTLEMENT");
        var current = (LanguageFloorLiberationEncounter)ModelDb.Encounter<LanguageFloorLiberationEncounter>().ToMutable();
        current.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "3",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False",
            ["KilledBossCount"] = "2",
            ["SettlementTriggered"] = "True",
            ["EndedByLethalDamage"] = "True"
        });
        var room = new CombatRoom(current, runState);
        room.MarkPreFinished();
        _ = TaskHelper.RunSafely(RunManager.Instance.EnterRoom(room));
        await WaitUntil(() => ReferenceEquals(RunManager.Instance.DebugOnlyGetState()?.CurrentRoom, room),
            "pre-finished Language Floor combat room");
        await WaitFrames(8);

        await RunManager.Instance.ProceedFromTerminalRewardsScreen();
        await WaitUntil(
            static () => RunManager.Instance.DebugOnlyGetState()?.CurrentRoom
                is EventRoom { CanonicalEvent: LanguageFloorLiberationSettlementEvent },
            "Language Floor settlement event room");
        await WaitFrames(4);
        EventModel settlement = RunManager.Instance.EventSynchronizer.GetLocalEvent();
        int kills = (int)settlement.DynamicVars["Kills"].BaseValue;
        Trace("settlement-reload", "event", "kills=" + kills
            + "|pending=" + LanguageFloorLiberationSettlementStore.PendingSettlement);
        Require(kills == 2, "The settlement event used " + kills + " kills instead of the loaded encounter's 2.");
    }

    private static async Task VerifySolemnMourningCounter()
    {
        // 第一局：救赎之手计满四张封印牌后战斗中途退出，没有走到玩家回合开始或战斗结束的清零。
        CombatState first = await StartFight("STATICSTATE_SOLEMN_A");
        int firstStrength = await PlaySealedCards(first, 4);
        Trace("solemn-mourning", "first", "strength=" + firstStrength);
        Require(firstStrength == 4, "Four sealed cards in the first combat gave " + firstStrength + " Strength.");
        await LeaveRun("solemn-mourning first run");

        // 第二局：救赎之手在玩家回合中途加入（与阶段转换相同），第一张封印牌应计入。
        CombatState second = await StartFight("STATICSTATE_SOLEMN_B");
        int secondStrength = await PlaySealedCards(second, 1);
        Trace("solemn-mourning", "second", "strength=" + secondStrength);
        Require(secondStrength == 1, "The first sealed card in a new run gave " + secondStrength + " Strength.");
    }

    private static async Task VerifySolemnMourningLimit()
    {
        // 上限写成字面量 4（文案的数），不引用主模组常量，同一个验证程序集可以配修复前的主模组跑。
        const int limit = 4;
        CombatState combatState = await StartFight("STATICSTATE_SOLEMN_LIMIT");
        Creature boss = combatState.Enemies.First(static enemy => enemy.IsAlive);
        Player player = combatState.Players.First();
        await WaitUntil(
            () => !CombatManager.Instance.PlayerActionsDisabled && player.PlayerCombatState!.Hand.Cards.Count > 0,
            "first player turn");
        await WaitFrames(4);
        var context = new ThrowingPlayerChoiceContext();
        SolemnMourningRedemptionHandPower hand = await PowerCmdCompat.Apply<SolemnMourningRedemptionHandPower>(
                context, boss, 1m, boss, null, silent: true)
            ?? throw new InvalidOperationException("Redemption Hand was not applied.");
        Require(hand.Owner == boss, "Redemption Hand is not on the boss.");

        // 经原版 CardCmd.AutoPlay 真正打出（ShouldPlay → OnPlayWrapper → Before/AfterCardPlayed），不直接调用能力的钩子。
        for (int i = 0; i < limit; i++)
        {
            CardModel sealedDefend = await CreateSealedCard<DefendIronclad>(combatState, player, PileType.Hand);
            Require(Hook.ShouldPlay(combatState, sealedDefend, out _, AutoPlayType.None),
                "Sealed card " + (i + 1) + " was blocked before the limit.");
            await CardCmd.AutoPlay(context, sealedDefend, null);
        }

        CardModel fifth = await CreateSealedCard<DefendIronclad>(combatState, player, PileType.Hand);
        bool lastPlayable = Hook.ShouldPlay(combatState, fifth, out AbstractModel? preventer, AutoPlayType.None);
        Trace("solemn-mourning-limit", "fifth", "playable=" + lastPlayable
            + "|preventer=" + (preventer?.GetType().Name ?? "none"));
        Require(!lastPlayable && preventer is SolemnMourningPersistentSealAffliction,
            "The fifth sealed card in one turn was still playable.");
    }

    /// <summary>
    /// 已打出 3 张封印牌后，打出被封印的破灭（Havoc），它自动打出的抽牌堆顶牌也被封印：
    /// 破灭在开始结算时就占用第 4 个名额，内层的封印牌不能再打出。只按结算后的计数判断时，两张牌都看到 3，一回合打出 5 张。
    /// </summary>
    private static async Task VerifySolemnMourningNestedAutoPlay()
    {
        const string label = "solemn-mourning-nested";
        CombatState combatState = await StartFight("STATICSTATE_SOLEMN_NESTED");
        Creature boss = combatState.Enemies.First(static enemy => enemy.IsAlive);
        Player player = combatState.Players.First();
        await WaitUntil(
            () => !CombatManager.Instance.PlayerActionsDisabled && player.PlayerCombatState!.Hand.Cards.Count > 0,
            "first player turn");
        await WaitFrames(4);
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmdCompat.Apply<SolemnMourningRedemptionHandPower>(
            context, boss, 1m, boss, null, silent: true);

        for (int i = 0; i < 3; i++)
        {
            CardModel sealedDefend = await CreateSealedCard<DefendIronclad>(combatState, player, PileType.Hand);
            await CardCmd.AutoPlay(context, sealedDefend, null);
        }

        decimal blockBefore = player.Creature.Block;
        CardModel inner = await CreateSealedCard<DefendIronclad>(combatState, player, PileType.Draw, CardPilePosition.Top);
        CardModel havoc = await CreateSealedCard<Havoc>(combatState, player, PileType.Hand);
        Require(Hook.ShouldPlay(combatState, havoc, out _, AutoPlayType.None), "The sealed Havoc was blocked as the fourth card.");
        await CardCmd.AutoPlay(context, havoc, null);
        await WaitFrames(4);
        decimal blockAfter = player.Creature.Block;
        Trace(label, "result", "blockBefore=" + blockBefore + "|blockAfter=" + blockAfter
            + "|innerPile=" + (inner.Pile?.Type.ToString() ?? "none"));
        Require(blockBefore > 0, "The first three sealed Defends did not give Block.");
        Require(blockAfter == blockBefore,
            "The sealed card auto-played by the sealed Havoc was played as a fifth sealed card (Block "
            + blockBefore + " -> " + blockAfter + ").");
    }

    private static async Task<CardModel> CreateSealedCard<T>(
        CombatState combatState,
        Player player,
        PileType pile,
        CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        CardModel card = combatState.CreateCard<T>(player);
        await CardPileCmd.Add(card, pile, position);
        await CardCmd.Afflict<SolemnMourningPersistentSealAffliction>(card, 1);
        Require(SolemnMourningPersistentSealAffliction.IsPersistentSeal(card), "Could not seal " + card.Id.Entry + ".");
        return card;
    }

    private static async Task<int> PlaySealedCards(CombatState combatState, int count)
    {
        Creature boss = combatState.Enemies.First(static enemy => enemy.IsAlive);
        Player player = combatState.Players.First();
        // 等玩家回合开始的钩子跑完（抽完牌、解除操作锁定），之后再加入的救赎之手要到下个玩家回合才清零。
        await WaitUntil(
            () => !CombatManager.Instance.PlayerActionsDisabled && player.PlayerCombatState!.Hand.Cards.Count > 0,
            "first player turn");
        await WaitFrames(4);
        var context = new ThrowingPlayerChoiceContext();
        SolemnMourningRedemptionHandPower hand = await PowerCmdCompat.Apply<SolemnMourningRedemptionHandPower>(
                context, boss, 1m, boss, null, silent: true)
            ?? throw new InvalidOperationException("Redemption Hand was not applied.");
        CardModel[] cards = player.PlayerCombatState!.AllCards
            .Where(static card => !SolemnMourningPersistentSealAffliction.IsAnySeal(card))
            .Take(count)
            .ToArray();
        Require(cards.Length == count, "Not enough cards to seal.");
        foreach (CardModel card in cards)
        {
            await CardCmd.Afflict<SolemnMourningPersistentSealAffliction>(card, 1);
            Require(SolemnMourningPersistentSealAffliction.IsAnySeal(card), "Could not seal " + card.Id.Entry + ".");
            await hand.AfterCardPlayed(context, CreateCardPlay(card));
        }

        await WaitFrames(4);
        return boss.GetPower<StrengthPower>()?.Amount ?? 0;
    }

    private static async Task VerifyCombatTables()
    {
        CombatState combatState = await StartFight("STATICSTATE_TABLES");
        Creature enemy = combatState.Enemies.First(static creature => creature.IsAlive);
        Creature dealer = combatState.PlayerCreatures.First();
        LanguageFloorDeathContext.Record(enemy, dealer);
        LittleRedDeathContext.Record(enemy, dealer);
        WrathServantDeathContext.Record(enemy, dealer);

        // 战斗中途离开本局：死亡结算与 AfterCombatEnd 都不会来清理。
        await LeaveRun("combat-tables leave");
        string after = "language=" + (LanguageFloorDeathContext.Consume(enemy) != null)
            + "|littleRed=" + (LittleRedDeathContext.Consume(enemy) != null)
            + "|wrath=" + (WrathServantDeathContext.Consume(enemy) != null);
        Trace("combat-tables", "afterCleanup", after);
        Require(after == "language=False|littleRed=False|wrath=False",
            "Leaving the run kept combat tables: " + after);
    }

    private static async Task VerifyPagePreselect()
    {
        (RunState runState, Player first, Player second) = await StartFakeMultiplayerRun("STATICSTATE_PAGES");
        runState.AppendToMapPointHistory(MapPointType.Treasure, RoomType.Treasure, null);
        var selector = new PageSelector(first);
        using (CardSelectCmd.UseSelector(selector))
        {
            // 对照：没有预选在等待时，第二名玩家获得书页后跳过选择会移除遗物。
            RelicModel control = ModelDb.Relic<QueenBeePageRelic>().ToMutable();
            await WaitFor(RelicCmd.Obtain(control, second), "control obtain");
            bool controlOwned = second.Relics.Contains(control);
            Trace("page-preselect", "control", "owned=" + controlOwned);
            Require(!controlOwned, "A skipped page relic stayed owned without any concurrent preselection.");

            // 第一名玩家的书页预选正在等待选择（本机的奖励界面，或对端奖励在本机回放）。
            RelicModel pending = ModelDb.Relic<QueenBeePageRelic>().ToMutable();
            Task<bool> preselect = AbnormalityPageRewardPreselection.TryPreselectPageChoice(pending, first);
            await WaitUntil(() => selector.PendingFirstChoice, "first player's pending page choice");
            Require(!preselect.IsCompleted, "The first player's page choice did not wait.");

            RelicModel skipped = ModelDb.Relic<QueenBeePageRelic>().ToMutable();
            await WaitFor(RelicCmd.Obtain(skipped, second), "second player's obtain");
            bool skippedOwned = second.Relics.Contains(skipped);

            selector.CompleteFirstChoice();
            bool preselected = await WaitFor(preselect, "first player's preselect");
            Trace("page-preselect", "concurrent", "secondOwned=" + skippedOwned + "|firstPreselected=" + preselected);
            Require(preselected, "The first player's preselect did not finish with a mode.");
            Require(!skippedOwned, "The second player's skipped page relic stayed owned while the first player was choosing.");
        }
    }

    private sealed class PageSelector(Player first) : ICardSelector
    {
        private TaskCompletionSource<IEnumerable<CardModel>>? _firstChoice;
        private List<CardModel>? _firstOptions;

        public bool PendingFirstChoice => _firstChoice is { Task.IsCompleted: false };

        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            List<CardModel> list = options.ToList();
            if (list.Count > 0 && list[0].Owner == first && _firstChoice == null)
            {
                _firstOptions = list;
                _firstChoice = new TaskCompletionSource<IEnumerable<CardModel>>();
                return _firstChoice.Task;
            }

            return Task.FromResult<IEnumerable<CardModel>>([]);
        }

        public void CompleteFirstChoice() => _firstChoice?.TrySetResult([_firstOptions![0]]);

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new NotSupportedException("Card rewards are not part of this check.");
    }

    private static T LoadEncounter<T>(int phase, int kills) where T : EncounterModel
    {
        var encounter = (T)ModelDb.Encounter<T>().ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = phase.ToString(),
            ["PhaseComplete"] = "True",
            ["TransitionPending"] = "False",
            ["KilledBossCount"] = kills.ToString(),
            ["SettlementTriggered"] = "True",
            ["EndedByLethalDamage"] = "True"
        });
        return encounter;
    }

    private static string DescribeSettlementStores() =>
        "lang=" + LanguageFloorLiberationSettlementStore.Current.KilledBossCount
        + "/" + LanguageFloorLiberationSettlementStore.PendingSettlement
        + " art=" + ArtFloorLiberationSettlementStore.Current.KilledBossCount
        + "/" + ArtFloorLiberationSettlementStore.PendingSettlement
        + " hist=" + HistoryFloorLiberationSettlementStore.Current.KilledBossCount
        + "/" + HistoryFloorLiberationSettlementStore.PendingSettlement
        + " lit=" + LiteratureFloorLiberationSettlementStore.Current.KilledBossCount
        + "/" + LiteratureFloorLiberationSettlementStore.PendingSettlement
        + " tech=" + TechnologyFloorLiberationSettlementStore.Current.KilledBossCount
        + "/" + TechnologyFloorLiberationSettlementStore.PendingSettlement
        + " nat=" + NaturalFloorLiberationSettlementStore.KilledBossCount
        + "/" + NaturalFloorLiberationSettlementStore.PendingSettlement;

    private static CardPlay CreateCardPlay(CardModel card) => VerificationApi.CreateCardPlay(card, card.Owner);

    private static async Task<RunState> StartSingleplayerRun(string seed)
    {
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");
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
        return RunManager.Instance.DebugOnlyGetState() ?? throw new InvalidOperationException("Run state is null.");
    }

    private static async Task<CombatState> StartFight(string seed)
    {
        await StartSingleplayerRun(seed);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            MapPointType.Unassigned,
            ModelDb.Encounter<SlimesWeak>().ToMutable(),
            showTransition: false);
        await WaitUntil(static () => CombatManager.Instance.IsInProgress, seed + " combat start");
        await WaitFrames(8);
        return CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
    }

    private static async Task<(RunState RunState, Player First, Player Second)> StartFakeMultiplayerRun(string seed)
    {
        // 与社会层轨迹套件相同的假联机：两名本地玩家的 RunState 直接交给 NGame.StartRun。
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");
        Player[] players = Enumerable.Range(1, 2)
            .Select(index => Player.CreateForNewRun(
                index == 1 ? ModelDb.Character<Ironclad>() : ModelDb.Character<Silent>(),
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
        ArmActLikeIt2OneShotVanillaEntry();
        MethodInfo startRun = typeof(NGame).GetMethod("StartRun", PrivateInstance)
            ?? throw new InvalidOperationException("NGame.StartRun was unavailable for fake multiplayer.");
        await (startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException("NGame.StartRun did not return a Task."));
        await WaitUntil(static () => RunManager.Instance.DebugOnlyGetState()?.CurrentRoom != null, "first room");
        return (runState, players[0], players[1]);
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

    private static async Task LeaveRun(string description)
    {
        if (RunManager.Instance.DebugOnlyGetState() == null)
        {
            return;
        }

        RunManager.Instance.CleanUp(graceful: true);
        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() == null
                && RunManager.Instance.DebugOnlyGetState() == null,
            description);
    }

    private static void Trace(string label, string step, string detail) =>
        Log.Info(LogPrefix + "TRACE|" + label + "|" + step + "|" + detail);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static async Task WaitFor(Task task, string description)
    {
        await WaitUntil(() => task.IsCompleted, description);
        await task;
    }

    private static async Task<T> WaitFor<T>(Task<T> task, string description)
    {
        await WaitUntil(() => task.IsCompleted, description);
        return await task;
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
