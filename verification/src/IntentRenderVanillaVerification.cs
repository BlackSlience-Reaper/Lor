using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 原版风格意图画法（<see cref="IntentDisplayStyle.Vanilla"/>）的检查。只在同时传入 <c>--lor-verify-intent-render-vanilla</c> 时、
/// 默认画法的 ROW 行与摘要打印完之后运行：把设置切到原版风格，复用四场战斗，记录每个意图节点（<c>VROW|</c>），
/// 并对映射规则逐条断言（<c>VCHECK|ok|</c> / <c>VCHECK|FAIL|</c>）。这些行不进默认画法的摘要。
/// </summary>
internal static partial class IntentRenderVerificationPatch
{
    private const string VanillaVerifyArg = "lor-verify-intent-render-vanilla";
    private static readonly string[] NativeHolderChildren = ["Intent", "IntentParticle", "Value"];

    private static int _vanillaChecks;

    // VanillaCase 结束时会还原招式并重画节点，容器布局要在用例里记下。
    private static string _lastCaseLayout = "";
    private static readonly List<string> VanillaFailures = [];

    private sealed record VSlot(
        int Index,
        NIntent Node,
        AbstractIntent Intent,
        string Label,
        string Sprite,
        Color NodeModulate,
        Color SpriteModulate,
        Color ValueModulate,
        IReadOnlyList<string> HolderChildren,
        string Tip,
        string Shown,
        string Lines)
    {
        public string Type => Intent.GetType().Name;
    }

    // Godot 的命令行参数要从 OS.GetCmdlineArgs（CommandLineHelper）取；.NET 的 Environment 只在编辑器式启动时带上它们。
    private static bool HasVanillaArg() =>
        MegaCrit.Sts2.Core.Helpers.CommandLineHelper.HasArg(VanillaVerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VanillaVerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunVanillaAsync()
    {
        IntentDisplayStyle previous = LibraryOfRuinaSettings.IntentDisplayStyle;
        foreach (IntentRenderStage stage in Enum.GetValues<IntentRenderStage>())
        {
            VRow("order", stage.ToString(), string.Join(",", IntentRenderPipeline.DecoratorNames(stage, IntentDisplayStyle.Vanilla)));
        }

        try
        {
            // 不经设置界面、不保存配置：只改本进程里的值，结束时还原。
            LibraryOfRuinaSettings.IntentDisplayStyle = IntentDisplayStyle.Vanilla;
            await RunVanillaFight("vanilla-bird", VanillaBirdCases);
            await RunVanillaFight("vanilla-counter", VanillaCounterCases);
            await RunVanillaFight("vanilla-kali", VanillaKaliCases);
            await RunVanillaFight("vanilla-chord", VanillaChordCases);
        }
        finally
        {
            LibraryOfRuinaSettings.IntentDisplayStyle = previous;
        }

        Log.Info(LogPrefix + "VSUMMARY|checks=" + _vanillaChecks + "|failed=" + VanillaFailures.Count);
        if (VanillaFailures.Count > 0)
        {
            throw new InvalidOperationException(VanillaFailures.Count + " vanilla check(s) failed: " + string.Join("; ", VanillaFailures));
        }
    }

    private static async Task RunVanillaFight(string label, Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            Expect(label, "fight", false, ex.GetType().Name + ": " + ex.Message);
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

    // ---------------------------------------------------------------- fights

    private static async Task VanillaBirdCases()
    {
        CombatState state = await StartFight(
            ModelDb.Encounter<PunishingBirdStrong>().ToMutable(),
            RoomType.Monster,
            MapPointType.Monster,
            "LORINTENTRENDER_VBIRD");
        Creature bird = state.Enemies.Single(static enemy => enemy.Monster is PunishingBird);
        NCreature birdNode = RequireNode(bird);
        IReadOnlyList<Creature> players = state.PlayerCreatures.ToArray();
        Creature keeper = state.Enemies.First(static enemy => enemy.Monster is ForestKeeperBirdLeft);

        // 局内切换：默认画法下复合意图是一个节点，切到原版风格后设置的 setter 立即重画成两个，切回再恢复。
        LibraryOfRuinaSettings.IntentDisplayStyle = IntentDisplayStyle.Default;
        MoveState original = bird.Monster!.NextMove;
        var switchMove = new MoveState(
            "LOR_VERIFY_VANILLA_SWITCH",
            static _ => Task.CompletedTask,
            [new CombinedAttackDebuffIntent(8, 1, null, IntentBadge.Weak(2))]);
        bird.Monster.SetMoveImmediate(switchMove, forceTransition: true);
        await birdNode.UpdateIntent(players);
        await WaitFrames(2);
        int defaultCount = birdNode.IntentContainer.GetChildCount();
        LibraryOfRuinaSettings.IntentDisplayStyle = IntentDisplayStyle.Vanilla;
        await WaitFrames(2);
        string vanillaTypes = SlotTypes(birdNode);
        LibraryOfRuinaSettings.IntentDisplayStyle = IntentDisplayStyle.Default;
        await WaitFrames(2);
        string backTypes = SlotTypes(birdNode);
        bool backHasOverlay = ContainerIntents(birdNode).Any(static node => node
            .GetNode<Control>("%IntentHolder").HasMeta("LibraryOfRuina_VisualHash"));
        bird.Monster.SetMoveImmediate(original, forceTransition: true);
        LibraryOfRuinaSettings.IntentDisplayStyle = IntentDisplayStyle.Vanilla;
        Expect("switch", "refresh", defaultCount == 1 && vanillaTypes == "VanillaAttackIntentProxy,DebuffIntent"
                                    && backTypes == "CombinedAttackDebuffIntent" && backHasOverlay,
            "default=" + defaultCount + " vanilla=" + vanillaTypes + " back=" + backTypes + " backOverlay=" + backHasOverlay);

        // 复合拆分与不合成。
        CombinedAttackDebuffIntent attackDebuff = new(8, 1, null, IntentBadge.Weak(2));
        List<VSlot> slots = await VanillaCase("split.attack-debuff", birdNode, [attackDebuff], players);
        ExpectTypes("split.attack-debuff", slots, "VanillaAttackIntentProxy", "DebuffIntent");
        ExpectLabelFromSource("split.attack-debuff", slots[0], attackDebuff, players, bird);
        ExpectVanillaLook("split.attack-debuff", slots);
        ExpectTipFromSource("split.attack-debuff", slots, attackDebuff, players, bird);

        slots = await VanillaCase("split.defend-buff", birdNode, [new CombinedDefendBuffIntent(5, null, IntentBadge.Strength(2))], players);
        ExpectTypes("split.defend-buff", slots, "DefendIntent", "BuffIntent");
        ExpectVanillaLook("split.defend-buff", slots);

        slots = await VanillaCase("split.attack-defend", birdNode, [new CombinedAttackDefendIntent(4, 2, null, 6)], players);
        ExpectTypes("split.attack-defend", slots, "VanillaAttackIntentProxy", "DefendIntent");

        slots = await VanillaCase("split.attack-carddebuff", birdNode,
            [new CombinedAttackCardDebuffIntent(6, 1, null, IntentBadge.StatusCard<Dazed>(2))], players);
        ExpectTypes("split.attack-carddebuff", slots, "VanillaAttackIntentProxy", "VanillaStatusIntentProxy");
        Expect("split.attack-carddebuff", "status-label", slots[1].Label == "2", slots[1].Label);

        slots = await VanillaCase("split.magic", birdNode, [new CombinedMagicIntent()], players);
        ExpectTypes("split.magic", slots, "UnknownIntent");

        slots = await VanillaCase("nomerge.single-defend", birdNode, [new SingleAttackIntent(6), new DefendIntent()], players);
        ExpectTypes("nomerge.single-defend", slots, "SingleAttackIntent", "DefendIntent");

        slots = await VanillaCase("nomerge.defend-buff", birdNode, [new DefendIntent(), new BuffIntent()], players);
        ExpectTypes("nomerge.defend-buff", slots, "DefendIntent", "BuffIntent");

        slots = await VanillaCase("strong.single-debuff", birdNode, [new SingleAttackIntent(12), new DebuffIntent(strong: true)], players);
        ExpectTypes("strong.single-debuff", slots, "SingleAttackIntent", "DebuffIntent");
        Expect("strong.single-debuff", "strong", slots[1].Intent.IntentType == IntentType.DebuffStrong, slots[1].Intent.IntentType.ToString());

        slots = await VanillaCase("dodge.plain", birdNode, [new DodgeIntent(3)], players);
        ExpectTypes("dodge.plain", slots, "DefendIntent");
        ExpectVanillaLook("dodge.plain", slots);

        slots = await VanillaCase("hidden.then-pair", birdNode, [new HiddenIntent(), new SingleAttackIntent(6), new DebuffIntent()], players);
        ExpectTypes("hidden.then-pair", slots, "SingleAttackIntent", "DebuffIntent");

        // 徽记映射与去重。
        BadgedAttackIntent multi = new(5, 2, null,
            IntentBadge.Strength(2), IntentBadge.Bleed(3), IntentBadge.Heal(4), IntentBadge.Summon(1),
            IntentBadge.Custom(GroupAttackIconPath));
        slots = await VanillaCase("badge.multi", birdNode, [multi], players);
        ExpectTypes("badge.multi", slots, "VanillaAttackIntentProxy", "BuffIntent", "DebuffIntent", "HealIntent", "SummonIntent");
        ExpectLabelFromSource("badge.multi", slots[0], multi, players, bird);
        ExpectVanillaLook("badge.multi", slots);

        slots = await VanillaCase("badge.dedupe", birdNode,
            [new BadgedAttackIntent(6, null, IntentBadge.Weak(1), IntentBadge.Vulnerable(2))], players);
        ExpectTypes("badge.dedupe", slots, "VanillaAttackIntentProxy", "DebuffIntent");

        slots = await VanillaCase("badge.status", birdNode, [new BadgedAttackIntent(6, IntentBadge.StatusCard<Dazed>(2))], players);
        ExpectTypes("badge.status", slots, "VanillaAttackIntentProxy", "VanillaStatusIntentProxy");
        Expect("badge.status", "status-label", slots[1].Label == "2", slots[1].Label);

        slots = await VanillaCase("badge.card-debuff", birdNode, [new BadgedAttackIntent(6, IntentBadge.CardDebuff<Dazed>(1))], players);
        ExpectTypes("badge.card-debuff", slots, "VanillaAttackIntentProxy", "CardDebuffIntent");

        slots = await VanillaCase("badge.defend", birdNode, [new BadgedDefendIntent(IntentBadge.Guard(2, 1), 5)], players);
        ExpectTypes("badge.defend", slots, "DefendIntent", "BuffIntent");

        slots = await VanillaCase("badge.debuff-strong", birdNode,
            [new BadgedDebuffIntent(IntentBadge.Weak(2), 2, null, strong: true)], players);
        ExpectTypes("badge.debuff-strong", slots, "DebuffIntent");
        Expect("badge.debuff-strong", "strong", slots[0].Intent.IntentType == IntentType.DebuffStrong, slots[0].Intent.IntentType.ToString());

        // 详细意图只留原版主图标，状态牌留张数标签。
        slots = await VanillaCase("detailed.status", birdNode, [new DetailedStatusCardIntent<Dazed>(2, PileType.Draw)], players);
        ExpectTypes("detailed.status", slots, "VanillaStatusIntentProxy");
        Expect("detailed.status", "label", slots[0].Label == "2", slots[0].Label);
        ExpectVanillaLook("detailed.status", slots);

        slots = await VanillaCase("detailed.status-dynamic", birdNode,
            [new DynamicDetailedStatusCardIntent<Dazed>(() => 3, PileType.Discard)], players);
        ExpectTypes("detailed.status-dynamic", slots, "VanillaStatusIntentProxy");
        Expect("detailed.status-dynamic", "label", slots[0].Label == "3", slots[0].Label);

        slots = await VanillaCase("detailed.buff", birdNode,
            [new DetailedBuffIntent<StrengthPower>(2, DetailedBuffTargetScope.AllEnemies)], players);
        ExpectTypes("detailed.buff", slots, "BuffIntent");
        ExpectVanillaLook("detailed.buff", slots);

        // 攻击代理的伤害标签与源意图一致（多段、定向、继承 AbstractIntent 的敌方卡牌攻击）。
        CombinedAttackDebuffIntent multiHit = new(15, 3);
        TargetedMonsterAttackIntent targeted = new(7, 1, "WOLF_FEROCIOUS_FANGS.description");
        EnemyCardIntent cardAttack = new(VanillaSpec("LOR_VERIFY_VANILLA_A"), () => 6m);
        slots = await VanillaCase("attack.labels", birdNode, [multiHit, targeted, cardAttack], players);
        ExpectTypes("attack.labels", slots,
            "VanillaAttackIntentProxy", "DebuffIntent", "VanillaAttackIntentProxy", "VanillaAttackIntentProxy");
        ExpectLabelFromSource("attack.labels.multi", slots[0], multiHit, players, bird);
        ExpectLabelFromSource("attack.labels.targeted", slots[2], targeted, players, bird);
        ExpectLabelFromSource("attack.labels.enemy-card", slots[3], cardAttack, players, bird);
        Expect("attack.labels.enemy-card", "shown", slots[3].Label == "6", slots[3].Label);

        // 指示线：定向攻击的主图标画线，从附带效果拆出的减益图标不画。
        slots = await VanillaCase("target-lines", birdNode,
            [new CombinedTargetedAttackDebuffIntent(7, 1, null, null, false, IntentBadge.Vulnerable(1))], players);
        ExpectTypes("target-lines", slots, "VanillaAttackIntentProxy", "DebuffIntent");
        Expect("target-lines", "primary", slots[0].Lines.StartsWith("targets=ForestKeeperBirdLeft", StringComparison.Ordinal), slots[0].Lines);
        Expect("target-lines", "secondary", slots[1].Lines == "none", slots[1].Lines);

        // 敌方卡牌：每张牌一个意图，卡牌照默认画法挂在意图上方，容器换成卡牌布局；悬停不再附带卡牌提示（卡牌节点自己放大）。
        // 换回没有卡牌的招式后，卡牌清掉、布局还原。
        string layoutBefore = DescribeContainerLayout(birdNode.IntentContainer);
        slots = await VanillaCase("enemycard.card", birdNode,
            [new EnemyCardIntent(VanillaSpec("LOR_VERIFY_VANILLA_B"), () => 6m), new SingleAttackIntent(3)], players);
        ExpectTypes("enemycard.card", slots, "VanillaAttackIntentProxy", "SingleAttackIntent");
        Expect("enemycard.card", "card", HasEnemyCard(slots[0]) && !HasEnemyCard(slots[1]),
            string.Join(",", slots[0].HolderChildren) + " | " + string.Join(",", slots[1].HolderChildren));
        ExpectVanillaLook("enemycard.card", slots.Skip(1).ToList());
        Expect("enemycard.card", "layout", _lastCaseLayout.Contains("sep=64", StringComparison.Ordinal), _lastCaseLayout);
        Expect("enemycard.card", "no-card-tip", !slots[0].Shown.Contains("CardHoverTip", StringComparison.Ordinal), slots[0].Shown);
        slots = await VanillaCase("enemycard.cleared", birdNode, [new SingleAttackIntent(3)], players);
        Expect("enemycard.cleared", "card", !HasEnemyCard(slots[0]), string.Join(",", slots[0].HolderChildren));
        Expect("enemycard.cleared", "layout", _lastCaseLayout == layoutBefore, _lastCaseLayout + " vs " + layoutBefore);

        await VanillaTargetFlashCase(birdNode, players);

        // 友方着色：守林鸟（友方单位）的攻击图标与数字为绿色，拆出的增益图标保持白色。
        NCreature keeperNode = RequireNode(keeper);
        Expect("ally", "registered", AllyTurnRegistry.IsAllyCreature(keeper), DescribeCreature(keeper));
        slots = await VanillaCase("ally.tint", keeperNode, [new SingleAttackIntent(6), new CombinedAttackBuffIntent(4)], players);
        ExpectTypes("ally.tint", slots, "SingleAttackIntent", "VanillaAttackIntentProxy", "BuffIntent");
        Expect("ally.tint", "attack-green", slots[0].SpriteModulate == Colors.Green && slots[0].ValueModulate == Colors.Green
                                            && slots[1].SpriteModulate == Colors.Green && slots[1].ValueModulate == Colors.Green,
            Col(slots[0].SpriteModulate) + "/" + Col(slots[1].SpriteModulate));
        Expect("ally.tint", "buff-white", slots[2].SpriteModulate == Colors.White, Col(slots[2].SpriteModulate));

        // 肃穆哀悼的封印按源意图下标：封印 1 层时第一个源意图拆出的两个节点都变暗，第二个源意图不变。
        await PowerCmdCompat.Apply<SolemnMourningSealOnEnemyPower>(
            new ThrowingPlayerChoiceContext(), bird, 1m, applier: bird, cardSource: null, silent: true);
        slots = await VanillaCase("seal", birdNode, [new CombinedAttackDebuffIntent(8), new BuffIntent()], players);
        ExpectTypes("seal", slots, "VanillaAttackIntentProxy", "DebuffIntent", "BuffIntent");
        Expect("seal", "dim-by-source", Alpha(slots[0]) == 0.5f && Alpha(slots[1]) == 0.5f && Alpha(slots[2]) == 1f,
            string.Join(",", slots.Select(static slot => F(slot.NodeModulate.A))));
    }

    private static async Task VanillaCounterCases()
    {
        CombatState state = await StartFight(
            ModelDb.Encounter<ScorchedGirl>().ToMutable(),
            RoomType.Elite,
            MapPointType.Elite,
            "LORINTENTRENDER_VCOUNTER");
        Creature girl = state.Enemies.First(static enemy => enemy.Monster is ScorchedGirlMonster);
        NCreature node = RequireNode(girl);
        IReadOnlyList<Creature> players = state.PlayerCreatures.ToArray();
        var owner = (ICounterIntentQueueOwner)girl.Monster!;

        var combinedCounter = new CombinedCounterAttackDebuffIntent(() => 5m);
        AbstractIntent[] move = [new SingleAttackIntent(5), combinedCounter, new CounterBuffIntent()];
        owner.CounterIntentQueue.Clear();
        owner.CounterIntentQueue.Enqueue(combinedCounter);
        owner.CounterIntentQueue.Enqueue((ICounterIntent)move[2]);
        List<VSlot> slots = await VanillaCase("counter.split", node, move, players);
        owner.CounterIntentQueue.Clear();
        ExpectTypes("counter.split", slots, "SingleAttackIntent", "CounterAttackIntent", "CounterDebuffIntent", "CounterBuffIntent");
        Expect("counter.split", "golden", slots.Skip(1).All(static slot => slot.Sprite.Contains("/intents/counter/", StringComparison.Ordinal)),
            string.Join(",", slots.Select(static slot => slot.Sprite)));
        Expect("counter.split", "no-sts1", slots.All(static slot => !slot.Sprite.Contains("/combined/", StringComparison.Ordinal)),
            string.Join(",", slots.Select(static slot => slot.Sprite)));
        Expect("counter.split", "keyword", slots[1].Shown.Contains("COUNTER_KEYWORD", StringComparison.Ordinal), slots[1].Shown);
        Expect("counter.split", "label", slots[1].Label == combinedCounter.GetIntentLabel(players, girl).GetFormattedText(), slots[1].Label);
        Expect("counter.split", "viewer-targets", slots.Skip(1).All(slot =>
                GetField<IEnumerable<Creature>>(slot.Node, "_targets")?.SequenceEqual(players.Take(1)) == true),
            "targets");

        // 反击队列的过滤照旧在映射之前：同步时按招式重填队列，每个观察者最多看到 2 个反击条目，第三个不显示。
        slots = await VanillaCase("counter.visible-limit", node,
            [new CounterAttackIntent(3), new CounterDefendIntent(2), new CounterBuffIntent()], players);
        ExpectTypes("counter.visible-limit", slots, "CounterAttackIntent", "CounterDefendIntent");
    }

    private static async Task VanillaKaliCases()
    {
        CombatState state = await StartFight(
            ModelDb.Encounter<RedMistElite>().ToMutable(),
            RoomType.Elite,
            MapPointType.Elite,
            "LORINTENTRENDER_VKALI");
        Creature kali = state.Enemies.Single(static enemy => enemy.Monster is Kali);
        NCreature node = RequireNode(kali);
        IReadOnlyList<Creature> players = state.PlayerCreatures.ToArray();

        await node.UpdateIntent(players);
        await WaitFrames(2);
        string layout = DescribeContainerLayout(node.IntentContainer);
        List<NIntent> intents = ContainerIntents(node).ToList();
        VRow("kali.native", "container", "children=" + intents.Count + "|layout=" + layout);
        for (int i = 0; i < intents.Count; i++)
        {
            VRow("kali.native", "slot" + i, DescribeVanillaNode(intents[i]));
        }

        // 每张敌方卡牌挂在它拆出的第一个图标上，一张牌一个；有卡牌时用默认画法的卡牌布局。
        int cardSources = intents
            .Select(static intent => VanillaIntentProxies.SourceOf(GetField<AbstractIntent>(intent, "_intent")!))
            .Where(static source => source is IEnemyCardIntent)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Count();
        int cardNodes = intents.Count(HasEnemyCard);
        Expect("kali", "card-per-source", cardNodes == cardSources, "sources=" + cardSources + " cards=" + cardNodes);
        Expect("kali", "layout", layout.Contains("sep=64", StringComparison.Ordinal) == (cardSources > 0), layout);
        // 单人且没有友方单位：卡莉的攻击只可能打玩家，回合开始不亮指示线。
        AccessTools.Method(typeof(VanillaIntentTargetFlashPatch), "Postfix").Invoke(null, [kali, true]);
        await WaitFrames(2);
        Expect("kali", "no-flash-single-target", FlashOverlays(node).Count == 0,
            FlashOverlays(node).Count.ToString(CultureInfo.InvariantCulture));
        Expect("kali", "vanilla-types", intents.All(static intent => GetField<AbstractIntent>(intent, "_intent") is { } shown
                && shown is not IEnemyCardIntent and not ICombinedIntentVisual),
            string.Join(",", intents.Select(static intent => GetField<AbstractIntent>(intent, "_intent")?.GetType().Name)));
    }

    private static async Task VanillaChordCases()
    {
        EncounterModel encounter = ModelDb.Encounter<TechnologyFloorLiberationEncounter>().ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "2",
            ["KilledBossCount"] = "1"
        });
        CombatState state = await StartFight(encounter, RoomType.Boss, MapPointType.Boss, "LORINTENTRENDER_VCHORD");
        MonsterModel[] phaseTwoBosses = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ILiberationPrimaryPhaseBoss>()
            .Where(static boss => boss.LiberationPhase == 2)
            .Cast<MonsterModel>()
            .ToArray();
        foreach (MonsterModel boss in phaseTwoBosses)
        {
            await CreatureCmd.Kill(boss.Creature, force: true);
            await WaitFrames(8);
        }

        foreach (MonsterModel boss in phaseTwoBosses)
        {
            if (state.Enemies.Contains(boss.Creature) && boss.NextMove is LibraryPhaseTransitionMoveState)
            {
                await boss.PerformMove();
                await WaitFrames(8);
            }
        }

        await WaitUntil(
            () => state.Enemies.Any(static enemy => enemy.Monster is TechnologyFloorChordBoss
                                                    && NCombatRoom.Instance?.GetCreatureNode(enemy) != null),
            "chord boss node");
        await WaitFrames(8);
        Creature chord = state.Enemies.First(static enemy => enemy.Monster is TechnologyFloorChordBoss);
        NCreature node = RequireNode(chord);
        IReadOnlyList<Creature> players = state.PlayerCreatures.ToArray();
        MonsterModel monster = chord.Monster!;
        MoveState original = monster.NextMove;
        MoveState chordEgo = monster.MoveStateMachine!.States.Values
            .OfType<MoveState>()
            .Single(static move => move.StateId == "CHORD_EGO");

        monster.SetMoveImmediate(chordEgo, forceTransition: true);
        try
        {
            AttackIntent[] attacks = chordEgo.Intents.OfType<AttackIntent>().ToArray();
            await node.UpdateIntent(players);
            await WaitFrames(2);
            CheckChord("chord.no-block", node, attacks, expectDimFromSecond: true);

            foreach (Creature player in players)
            {
                await CreatureCmd.GainBlock(player, 999m, ValueProp.Unpowered, null, fast: true);
            }

            await node.UpdateIntent(players);
            await WaitFrames(2);
            CheckChord("chord.full-block", node, attacks, expectDimFromSecond: false);
        }
        finally
        {
            monster.SetMoveImmediate(original, forceTransition: true);
        }

        await WaitFrames(2);
    }

    private static void CheckChord(string name, NCreature node, AttackIntent[] attacks, bool expectDimFromSecond)
    {
        List<NIntent> intents = ContainerIntents(node).ToList();
        bool ok = attacks.Length >= 3;
        for (int i = 0; i < intents.Count; i++)
        {
            AbstractIntent shown = GetField<AbstractIntent>(intents[i], "_intent")!;
            AbstractIntent source = VanillaIntentProxies.SourceOf(shown);
            int attackIndex = Array.FindIndex(attacks, attack => ReferenceEquals(attack, source));
            bool dimmed = intents[i].Modulate.A < 1f;
            bool expectDim = expectDimFromSecond && attackIndex >= 1;
            ok &= dimmed == expectDim && shown is not ICombinedIntentVisual && shown is not IEnemyCardIntent;
            VRow(name, "slot" + i, shown.GetType().Name + "|source=" + source.GetType().Name + "|attack=" + attackIndex
                                   + "|alpha=" + F(intents[i].Modulate.A));
        }

        Expect(name, "dim-by-source", ok, "slots=" + intents.Count + " attacks=" + attacks.Length);
    }

    // ---------------------------------------------------------------- runner

    private static async Task<List<VSlot>> VanillaCase(string name, NCreature creatureNode, AbstractIntent[] intents, IReadOnlyList<Creature> targets)
    {
        MonsterModel monster = creatureNode.Entity.Monster!;
        Creature owner = creatureNode.Entity;
        MoveState original = monster.NextMove;
        var move = new MoveState("LOR_VERIFY_VANILLA_" + name.ToUpperInvariant(), static _ => Task.CompletedTask, intents);
        var slots = new List<VSlot>();
        try
        {
            monster.SetMoveImmediate(move, forceTransition: true);
            await creatureNode.UpdateIntent(targets);
            await WaitFrames(2);
            VRow(name, "move", string.Join(",", intents.Select(static intent => intent.GetType().Name)));
            _lastCaseLayout = DescribeContainerLayout(creatureNode.IntentContainer);
            VRow(name, "container", "children=" + creatureNode.IntentContainer.GetChildCount() + "|layout=" + _lastCaseLayout);
            int index = 0;
            foreach (NIntent node in ContainerIntents(creatureNode))
            {
                AbstractIntent intent = GetField<AbstractIntent>(node, "_intent")!;
                IEnumerable<Creature> nodeTargets = GetField<IEnumerable<Creature>>(node, "_targets") ?? targets;
                Control holder = node.GetNode<Control>("%IntentHolder");
                string tip = Safe(() => DescribeTip(intent.GetHoverTip(nodeTargets, owner)));

                CapturedHoverTips.Clear();
                _capturing = true;
                try
                {
                    AccessTools.Method(typeof(NIntent), "OnHovered").Invoke(node, null);
                }
                finally
                {
                    _capturing = false;
                }

                string shown = CapturedHoverTips.Count + ":" + string.Join(" || ", CapturedHoverTips);
                string lines = DescribeOverlay(owner);
                AccessTools.Method(typeof(NIntent), "OnUnhovered").Invoke(node, null);

                var slot = new VSlot(
                    index,
                    node,
                    intent,
                    node.GetNode<RichTextLabel>("%Value").Text,
                    FrameNumber.Replace(TexturePath(node.GetNode<Sprite2D>("%Intent").Texture), "_##"),
                    node.Modulate,
                    node.GetNode<CanvasItem>("%Intent").Modulate,
                    node.GetNode<CanvasItem>("%Value").Modulate,
                    holder.GetChildren().Select(NodeName).ToArray(),
                    tip,
                    shown,
                    lines);
                VRow(name, "slot" + index, slot.Type + "|type=" + intent.IntentType
                                           + "|source=" + SourceDescription(intent)
                                           + "|label=\"" + slot.Label + "\""
                                           + "|sprite=" + slot.Sprite
                                           + "|mod=" + Col(slot.NodeModulate)
                                           + "|spriteMod=" + Col(slot.SpriteModulate)
                                           + "|valueMod=" + Col(slot.ValueModulate)
                                           + "|holder=" + string.Join(",", slot.HolderChildren));
                VRow(name, "slot" + index + ".tip", tip);
                VRow(name, "slot" + index + ".shown", shown);
                VRow(name, "slot" + index + ".lines", lines);
                slots.Add(slot);
                index++;
            }
        }
        finally
        {
            monster.SetMoveImmediate(original, forceTransition: true);
        }

        return slots;
    }

    private static string SourceDescription(AbstractIntent intent) =>
        VanillaIntentProxies.TryGetSource(intent, out VanillaIntentProxySource? source)
            ? source.Source.GetType().Name + "#" + source.SourceIndex + (source.IsPrimary ? ":primary" : ":effect") + ":" + source.TitlePrefix
            : "self";

    private static string DescribeVanillaNode(NIntent node)
    {
        AbstractIntent? intent = GetField<AbstractIntent>(node, "_intent");
        return (intent?.GetType().Name ?? "null")
               + "|source=" + (intent == null ? "-" : SourceDescription(intent))
               + "|label=\"" + node.GetNode<RichTextLabel>("%Value").Text + "\""
               + "|sprite=" + FrameNumber.Replace(TexturePath(node.GetNode<Sprite2D>("%Intent").Texture), "_##")
               + "|holder=" + string.Join(",", node.GetNode<Control>("%IntentHolder").GetChildren().Select(NodeName));
    }

    private static IEnumerable<NIntent> ContainerIntents(NCreature creatureNode) =>
        creatureNode.IntentContainer.GetChildren().OfType<NIntent>();

    private static string SlotTypes(NCreature creatureNode) =>
        string.Join(",", ContainerIntents(creatureNode)
            .Select(static node => GetField<AbstractIntent>(node, "_intent")?.GetType().Name ?? "null"));

    private static EnemyCardSpec VanillaSpec(string id) => new(
        id,
        static () => EnemyCardSpec.CreateDisplayCardModel<RegretEgoCard>(),
        cost: 1,
        priority: 0,
        static (_, _) => Task.CompletedTask,
        static spec => new EnemyCardIntent(spec));

    private static float Alpha(VSlot slot) => slot.NodeModulate.A;

    private static bool HasEnemyCard(NIntent node) =>
        node.GetNode<Control>("%IntentHolder").GetChildren().Select(NodeName).Any(IsEnemyCardNode);

    // VanillaCase 结束时会还原招式并重画节点，用例里的判断要读当时记下的子节点名。
    private static bool HasEnemyCard(VSlot slot) => slot.HolderChildren.Any(IsEnemyCardNode);

    private static bool IsEnemyCardNode(string name) => name.StartsWith("LibraryOfRuinaEnemyCard", StringComparison.Ordinal);

    private static List<Node> FlashOverlays(NCreature creatureNode) =>
        creatureNode.GetChildren()
            .Where(static child => child.Name.ToString().StartsWith("LibraryOfRuinaTargetedIntentFlash", StringComparison.Ordinal)
                                   && !child.IsQueuedForDeletion())
            .ToList();

    /// <summary>
    /// 回合开始的指示线闪现。后缀挂在 <c>Creature.PrepareForNextTurn</c> 上；这里直接调用后缀，不掷新招式（掷招式会换掉测试招式）。
    /// 定向攻击与普通攻击各亮一份，附带效果拆出的减益不亮；悬停时收起闪现；不动时停留后自行消失；不掷新招式与默认画法都不亮。
    /// </summary>
    private static async Task VanillaTargetFlashCase(NCreature birdNode, IReadOnlyList<Creature> players)
    {
        Creature bird = birdNode.Entity;
        MonsterModel monster = bird.Monster!;
        MoveState original = monster.NextMove;
        MethodBase prepare = AccessTools.Method(typeof(Creature), nameof(Creature.PrepareForNextTurn));
        Expect("flash", "patched", Harmony.GetPatchInfo(prepare)?.Postfixes
                .Any(static patch => patch.PatchMethod.DeclaringType == typeof(VanillaIntentTargetFlashPatch)) == true,
            "PrepareForNextTurn");
        MethodInfo postfix = AccessTools.Method(typeof(VanillaIntentTargetFlashPatch), "Postfix");
        var move = new MoveState(
            "LOR_VERIFY_VANILLA_FLASH",
            static _ => Task.CompletedTask,
            [new CombinedTargetedAttackDebuffIntent(7, 1, null, null, false, IntentBadge.Vulnerable(1)), new SingleAttackIntent(4)]);
        try
        {
            monster.SetMoveImmediate(move, forceTransition: true);
            await birdNode.UpdateIntent(players);
            await WaitFrames(2);
            VRow("flash", "container", "alpha=" + F(birdNode.IntentContainer.Modulate.A) + "|types=" + SlotTypes(birdNode));

            postfix.Invoke(null, [bird, false]);
            await WaitFrames(2);
            Expect("flash", "no-roll", FlashOverlays(birdNode).Count == 0, FlashOverlays(birdNode).Count.ToString(CultureInfo.InvariantCulture));

            postfix.Invoke(null, [bird, true]);
            await WaitFrames(2);
            List<Node> flashes = FlashOverlays(birdNode);
            string described = string.Join(" ; ", flashes.Select(static overlay =>
                "targets=" + string.Join(",", GetField<IReadOnlyList<Creature>>(overlay, "_targets")?.Select(DescribeCreature) ?? [])));
            VRow("flash", "overlays", flashes.Count + ":" + described);
            Expect("flash", "lines", flashes.Count == 2 && described.Contains("ForestKeeperBirdLeft", StringComparison.Ordinal), described);

            NIntent first = ContainerIntents(birdNode).First();
            AccessTools.Method(typeof(NIntent), "OnHovered").Invoke(first, null);
            await WaitFrames(2);
            Expect("flash", "hover-hides", FlashOverlays(birdNode).Count == 0 && DescribeOverlay(bird) != "none",
                FlashOverlays(birdNode).Count + " | " + DescribeOverlay(bird));
            AccessTools.Method(typeof(NIntent), "OnUnhovered").Invoke(first, null);

            postfix.Invoke(null, [bird, true]);
            await WaitFrames(2);
            ulong start = Time.GetTicksMsec();
            int before = FlashOverlays(birdNode).Count;
            while (FlashOverlays(birdNode).Count > 0 && Time.GetTicksMsec() - start < 6000)
            {
                await WaitFrames(1);
            }

            Expect("flash", "fades", before == 2 && FlashOverlays(birdNode).Count == 0,
                "before=" + before + " after=" + FlashOverlays(birdNode).Count + " ms=" + (Time.GetTicksMsec() - start));

            LibraryOfRuinaSettings.IntentDisplayStyle = IntentDisplayStyle.Default;
            await WaitFrames(2);
            postfix.Invoke(null, [bird, true]);
            await WaitFrames(2);
            Expect("flash", "default-style", FlashOverlays(birdNode).Count == 0, FlashOverlays(birdNode).Count.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            LibraryOfRuinaSettings.IntentDisplayStyle = IntentDisplayStyle.Vanilla;
            monster.SetMoveImmediate(original, forceTransition: true);
            await WaitFrames(2);
        }
    }

    // ---------------------------------------------------------------- checks

    private static void ExpectTypes(string name, List<VSlot> slots, params string[] expected)
    {
        string actual = string.Join(",", slots.Select(static slot => slot.Type));
        Expect(name, "types", actual == string.Join(",", expected), actual);
    }

    private static void ExpectLabelFromSource(string name, VSlot slot, AbstractIntent source, IReadOnlyList<Creature> targets, Creature owner)
    {
        string expected = source.GetIntentLabel(targets, owner).GetFormattedText() ?? "";
        Expect(name, "label=source", slot.Label == expected, "shown=\"" + slot.Label + "\" source=\"" + expected + "\"");
    }

    /// <summary>原版外观：图标与粒子都取 intent_atlas，节点上没有效果行、角标、目标标记与卡牌，提示标题不是复合标题。</summary>
    private static void ExpectVanillaLook(string name, List<VSlot> slots)
    {
        foreach (VSlot slot in slots)
        {
            bool atlas = slot.Sprite.Contains("/intent_atlas.sprites/", StringComparison.Ordinal);
            bool nativeHolder = slot.HolderChildren.All(child => NativeHolderChildren.Contains(child));
            bool vanillaTip = !slot.Tip.Contains("COMBINED", StringComparison.Ordinal)
                              && slot.Tip.Contains("intent_atlas.sprites", StringComparison.Ordinal);
            Expect(name, "look" + slot.Index, atlas && nativeHolder && vanillaTip,
                slot.Sprite + " | " + string.Join(",", slot.HolderChildren) + " | " + slot.Tip);
        }
    }

    private static void ExpectTipFromSource(string name, List<VSlot> slots, AbstractIntent source, IReadOnlyList<Creature> targets, Creature owner)
    {
        string description = source.GetHoverTip(targets, owner).Description;
        foreach (VSlot slot in slots)
        {
            Expect(name, "tip-desc" + slot.Index, slot.Tip.Contains("desc=\"" + description + "\"", StringComparison.Ordinal), slot.Tip);
        }
    }

    private static void Expect(string name, string check, bool ok, string detail)
    {
        _vanillaChecks++;
        if (!ok)
        {
            VanillaFailures.Add(name + "." + check);
        }

        Log.Info(LogPrefix + "VCHECK|" + (ok ? "ok" : "FAIL") + "|" + name + "|" + check + "|" + detail);
    }

    private static void VRow(string name, string section, string data) =>
        Log.Info(LogPrefix + "VROW|" + name + "|" + section + "|" + data);
}
