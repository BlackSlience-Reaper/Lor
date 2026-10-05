using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Models;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.intents.rendering;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 意图显示的逐行记录，用于意图渲染重构前后的对照（A/B）。
/// <para>
/// 只经过重构前后都存在的入口：原版 <c>NIntent.UpdateIntent</c>、<c>_Process</c>、<c>OnHovered</c>/<c>OnUnhovered</c>、
/// <c>AbstractIntent.GetHoverTip</c> 与 <c>NCreature.UpdateIntent</c>。每种意图（Combined、Badged、Detailed、反击、敌方卡牌、
/// 狐狸、指定目标、原版）挂到一个单独的意图节点上，记录节点树（名字、类型、位置、尺寸、缩放、层级、颜色、纹理路径、文字、
/// 字体与鼠标过滤）、两个固定动画帧的图标、悬停提示与指示线；另在四场战斗里给怪物换上指定的意图组合，记录意图容器。
/// 每条记录输出一行 <c>ROW|</c>，两次构建的 ROW 行应逐行相同，末尾打印全部 ROW 行的摘要。
/// </para>
/// <para>
/// 带流水线的构建另外输出 <c>PIPE|</c> 行（各入口依次调用了哪些装饰器、结果是什么、是否短路），不计入摘要。
/// 与时间有关的量不记录：意图的上下浮动、自动推进的动画帧、容器淡入；视觉哈希元数据是按进程随机化的 HashCode，只记键名。
/// </para>
/// </summary>
internal static partial class IntentRenderVerificationPatch
{
    private const string VerifyArg = "lor-verify-intent-render";
    private const string LogPrefix = "[LibraryOfRuina.IntentRender.Verify] ";
    private const string OverlayNodeName = "LibraryOfRuinaTargetedIntentLines";
    private const string GroupAttackIconPath = "atlases/intent_atlas.sprites/intent_stun.tres";

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly StringBuilder Digest = new();
    // 逐帧动画的帧号：意图贴图是 frames/ 下的 png，原版 buff、defend 等动画是 intent_atlas.sprites/ 下的图集 tres。
    private static readonly System.Text.RegularExpressions.Regex FrameNumber =
        new(@"(?<=(?:/frames/|/intent_atlas\.sprites/).*)_\d{2}(?=\.(?:png|tres)$)");
    private static readonly List<string> CapturedHoverTips = [];
    private static readonly List<string> Failures = [];
    private static bool _started;
    private static bool _capturing;
    private static int _rows;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        var harmony = new Harmony("LibraryOfRuina.Verification.IntentRender");
        harmony.Patch(
            AccessTools.Method(typeof(NCreature), nameof(NCreature.ShowHoverTips)),
            postfix: new HarmonyMethod(typeof(IntentRenderVerificationPatch), nameof(CaptureHoverTips)));
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

    private static void CaptureHoverTips(IEnumerable<IHoverTip> hoverTips)
    {
        if (!_capturing)
        {
            return;
        }

        foreach (IHoverTip tip in hoverTips)
        {
            CapturedHoverTips.Add(DescribeTip(tip));
        }
    }

    private static async Task RunAsync()
    {
        try
        {
            Log.Info(LogPrefix + "pipeline=" + PipelineAvailable());
            await RunFight("bird", PunishingBirdCases);
            await RunFight("counter", CounterOwnerCases);
            await RunFight("kali", KaliCases);
            await RunFight("chord", ChordCases);

            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Digest.ToString())))[..16];
            Log.Info(LogPrefix + "DIGEST|" + digest + "|rows=" + _rows);
            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(Failures.Count + " fight(s) failed: " + string.Join("; ", Failures));
            }

            // 原版风格画法另起一组 VROW/VCHECK 行，在默认画法的摘要打印之后运行，不进摘要。
            if (HasVanillaArg())
            {
                await RunVanillaAsync();
            }

            Log.Info(LogPrefix + "INTENT_RENDER_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "INTENT_RENDER_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunFight(string label, Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            // 一场战斗出错不影响后面的战斗；异常本身也作为 ROW 参与对照。
            Row(label, "fight-exception", ex.GetType().Name + ": " + ex.Message);
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

    // ---------------------------------------------------------------- fights

    private static async Task PunishingBirdCases()
    {
        CombatState state = await StartFight(
            ModelDb.Encounter<PunishingBirdStrong>().ToMutable(),
            RoomType.Monster,
            MapPointType.Monster,
            "LORINTENTRENDER_BIRD");
        Creature bird = state.Enemies.Single(static enemy => enemy.Monster is PunishingBird);
        NCreature birdNode = RequireNode(bird);
        IReadOnlyList<Creature> players = state.PlayerCreatures.ToArray();
        IReadOnlyList<Creature> enemies = state.Enemies.ToArray();
        Row("bird", "setup", "enemies=" + string.Join(",", enemies.Select(DescribeCreature))
                             + "|players=" + string.Join(",", players.Select(DescribeCreature)));

        var host = new Control { Name = "LorIntentRenderHost" };
        NCombatRoom.Instance!.AddChild(host);
        try
        {
            foreach ((string name, Func<AbstractIntent> create) in IntentCases(players, enemies))
            {
                RunIntentCase(name, create, bird, players, host);
                await WaitFrames(1);
            }

            foreach ((string name, Func<AbstractIntent> create) in ScopeCases())
            {
                RunIntentCase(name + "@enemies", create, bird, enemies, host);
                await WaitFrames(1);
            }
        }
        finally
        {
            host.QueueFree();
        }

        foreach ((string name, Func<AbstractIntent[]> create) in CreatureCases(players))
        {
            RunCreatureCase("bird." + name, birdNode, create(), players);
            await WaitFrames(2);
        }

        // 肃穆哀悼的封印：前 N 个意图节点变暗。
        await PowerCmdCompat.Apply<SolemnMourningSealOnEnemyPower>(
            new ThrowingPlayerChoiceContext(), bird, 1m, applier: bird, cardSource: null, silent: true);
        RunCreatureCase(
            "bird.solemn-seal-1",
            birdNode,
            [new SingleAttackIntent(6), new DefendIntent(), new BuffIntent()],
            players);
        await WaitFrames(2);
    }

    private static async Task CounterOwnerCases()
    {
        CombatState state = await StartFight(
            ModelDb.Encounter<ScorchedGirl>().ToMutable(),
            RoomType.Elite,
            MapPointType.Elite,
            "LORINTENTRENDER_COUNTER");
        Creature girl = state.Enemies.First(static enemy => enemy.Monster is ScorchedGirlMonster);
        NCreature node = RequireNode(girl);
        IReadOnlyList<Creature> players = state.PlayerCreatures.ToArray();
        var owner = (ICounterIntentQueueOwner)girl.Monster!;

        DumpContainer("counter.native", node);

        AbstractIntent[] queued =
        [
            new SingleAttackIntent(5),
            new DefendIntent(),
            new CounterAttackIntent(3),
            new CounterDefendIntent(2),
            new CounterBuffIntent()
        ];
        RunCreatureCase("counter.not-queued", node, queued, players);
        await WaitFrames(2);

        owner.CounterIntentQueue.Clear();
        foreach (ICounterIntent counter in queued.OfType<ICounterIntent>())
        {
            owner.CounterIntentQueue.Enqueue(counter);
        }

        RunCreatureCase("counter.queued", node, queued, players);
        await WaitFrames(2);

        AbstractIntent[] pairOnly = [new CounterAttackIntent(4), new CounterDebuffIntent()];
        owner.CounterIntentQueue.Clear();
        foreach (ICounterIntent counter in pairOnly.OfType<ICounterIntent>())
        {
            owner.CounterIntentQueue.Enqueue(counter);
        }

        RunCreatureCase("counter.queued-pair", node, pairOnly, players);
        owner.CounterIntentQueue.Clear();
        await WaitFrames(2);
    }

    private static async Task KaliCases()
    {
        CombatState state = await StartFight(
            ModelDb.Encounter<RedMistElite>().ToMutable(),
            RoomType.Elite,
            MapPointType.Elite,
            "LORINTENTRENDER_KALI");
        Creature kali = state.Enemies.Single(static enemy => enemy.Monster is Kali);
        NCreature node = RequireNode(kali);
        IReadOnlyList<Creature> players = state.PlayerCreatures.ToArray();

        await node.UpdateIntent(players);
        DumpContainer("kali.native", node);
        RunCreatureCase("kali.stun-replacement", node, [new StunIntent()], players);
        await WaitFrames(2);
    }

    private static async Task ChordCases()
    {
        // 直接读档进第 3 阶段时遭遇场景里还没有和弦的站位，节点建不出来；从第 2 阶段击杀 Mk4 转阶段，走遭遇自己的生成流程。
        EncounterModel encounter = ModelDb.Encounter<TechnologyFloorLiberationEncounter>().ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "2",
            ["KilledBossCount"] = "1"
        });
        CombatState state = await StartFight(encounter, RoomType.Boss, MapPointType.Boss, "LORINTENTRENDER_CHORD");
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
            // 原版 NIntent 换动画后，精灵贴图要到下一帧 _Process 才换成新动画的帧；UpdateIntent 与 GainBlock 之间
            // 是否恰好跑过一帧取决于时序。每次转储前固定等两帧（ProcessFrame 信号在节点 _Process 之前发出，等一帧不保证
            // _Process 已跑），读到的总是当前意图的贴图（帧号在转储里已归一）。
            await node.UpdateIntent(players);
            await WaitFrames(2);
            DumpContainer("chord.ego-no-block", node);

            foreach (Creature player in players)
            {
                await CreatureCmd.GainBlock(player, 999m, ValueProp.Unpowered, null, fast: true);
            }

            await node.UpdateIntent(players);
            await WaitFrames(2);
            DumpContainer("chord.ego-full-block", node);
        }
        finally
        {
            monster.SetMoveImmediate(original, forceTransition: true);
        }

        await WaitFrames(2);
    }

    // ---------------------------------------------------------------- cases

    private static IEnumerable<(string Name, Func<AbstractIntent> Create)> IntentCases(
        IReadOnlyList<Creature> players,
        IReadOnlyList<Creature> enemies)
    {
        Creature firstPlayer = players[0];
        IntentBadge Stun() => IntentBadge.Custom(GroupAttackIconPath);
        IntentBadge Group() => IntentBadge.Custom(GroupAttackIconPath, 0, null, null, IntentEffectSemantics.GroupAttack);
        EnemyCardSpec Spec(string id) => new(
            id,
            static () => EnemyCardSpec.CreateDisplayCardModel<RegretEgoCard>(),
            cost: 1,
            priority: 0,
            static (_, _) => Task.CompletedTask,
            static spec => new EnemyCardIntent(spec));

        // 原版意图：对照其他装饰器在不相关的意图上什么都不做。
        yield return ("vanilla.single", static () => new SingleAttackIntent(6));
        yield return ("vanilla.multi", static () => new MultiAttackIntent(3, 4));
        yield return ("vanilla.defend", static () => new DefendIntent());
        yield return ("vanilla.buff", static () => new BuffIntent());
        yield return ("vanilla.debuff", static () => new DebuffIntent());
        yield return ("vanilla.status", static () => new StatusIntent(2));

        // Combined：CombinedIntents.cs 的每个具体类，含伤害档位、多段、群体、指定目标与原版玩家目标。
        yield return ("combined.attack-debuff", static () => new CombinedAttackDebuffIntent(8, 1, null, IntentBadge.Weak(2)));
        yield return ("combined.attack-debuff.multi-tier5", static () => new CombinedAttackDebuffIntent(15, 3));
        yield return ("combined.attack-debuff.group", () => new CombinedAttackDebuffIntent(4, 1, null, IntentBadge.Weak(1), Group()));
        yield return ("combined.attack-carddebuff", static () => new CombinedAttackCardDebuffIntent(6, 1, null, IntentBadge.StatusCard<Dazed>(2)));
        yield return ("combined.attack-buff", static () => new CombinedAttackBuffIntent(12, 1, null, IntentBadge.Strength(2)));
        yield return ("combined.attack-buff.tier1", static () => new CombinedAttackBuffIntent(2));
        yield return ("combined.attack-defend", static () => new CombinedAttackDefendIntent(4, 2, null, 6));
        yield return ("combined.targeted-attack-debuff", static () =>
            new CombinedTargetedAttackDebuffIntent(7, 1, null, null, false, IntentBadge.Vulnerable(1)));
        yield return ("combined.targeted-attack-debuff.vanilla-player", static () =>
            new CombinedTargetedAttackDebuffIntent(7, 1, null, null, true, IntentBadge.Vulnerable(1)));
        yield return ("combined.targeted-attack-buff", static () =>
            new CombinedTargetedAttackBuffIntent(() => 9m, () => 2, null, null, false, IntentBadge.Strength(1)));
        yield return ("combined.targeted-attack-defend", static () =>
            new CombinedTargetedAttackDefendIntent(10, 1, null, null, false, 5));
        yield return ("combined.counter-attack-debuff", static () => new CombinedCounterAttackDebuffIntent(() => 5m));
        yield return ("combined.counter-attack-buff", static () =>
            new CombinedCounterAttackBuffIntent(() => 25m, () => 1, null, null, IntentBadge.Strength(3)));
        yield return ("combined.counter-attack-defend", static () => new CombinedCounterAttackDefendIntent(() => 5m, blockAmount: 4));
        yield return ("combined.defend-buff", static () => new CombinedDefendBuffIntent(5, null, IntentBadge.Strength(2)));
        yield return ("combined.defend-debuff", static () => new CombinedDefendDebuffIntent(5, null, IntentBadge.Weak(1)));
        yield return ("combined.defend-debuff.badge-amount", static () => new CombinedDefendDebuffIntent(5, null, 3));
        yield return ("combined.counter-defend-buff", static () => new CombinedCounterDefendBuffIntent(3));
        yield return ("combined.counter-defend-debuff", static () => new CombinedCounterDefendDebuffIntent(3, null, IntentBadge.Frail(1)));
        yield return ("combined.magic", static () => new CombinedMagicIntent());
        yield return ("combined.magic.large-overlay", () => new CombinedMagicIntent(null, true, Stun()));
        yield return ("combined.targeted-magic", () => new CombinedTargetedMagicIntent(null, _ => firstPlayer, false, IntentBadge.Weak(1)));

        // 敌方卡牌意图（EnemyCardIntent.cs）：单个意图节点上只看复合图标与提示，卡牌预览在怪物层的用例里。
        // 攻击类型的 EnemyCardIntent 在原版 UpdateVisuals 里找不到 "attack" 动画（只在运行时计划里配合卡牌显示），单节点用例取强化类型。
        yield return ("enemycard.intent", () => new EnemyCardIntent(
            Spec("LOR_VERIFY_CARD_A"), null, null, IntentType.Buff, "BUFF", "atlases/intent_atlas.sprites/intent_buff.tres"));
        yield return ("enemycard.attack", () => new EnemyCardAttackIntent(Spec("LOR_VERIFY_CARD_B"), () => 6m, () => 2));
        yield return ("enemycard.combined-attack-debuff", () => new EnemyCardCombinedAttackDebuffIntent(Spec("LOR_VERIFY_CARD_C"), () => 7m));
        yield return ("enemycard.combined-attack-buff", () => new EnemyCardCombinedAttackBuffIntent(Spec("LOR_VERIFY_CARD_D"), () => 30m));
        yield return ("enemycard.combined-attack-defend", () => new EnemyCardCombinedAttackDefendIntent(Spec("LOR_VERIFY_CARD_E"), () => 3m, () => 2));
        yield return ("enemycard.combined-defend-buff", () => new EnemyCardCombinedDefendBuffIntent(Spec("LOR_VERIFY_CARD_F")));
        yield return ("enemycard.combined-defend-debuff", () => new EnemyCardCombinedDefendDebuffIntent(Spec("LOR_VERIFY_CARD_G")));
        yield return ("enemycard.combined-magic", () => new EnemyCardCombinedMagicIntent(Spec("LOR_VERIFY_CARD_H"), null, true));
        yield return ("enemycard.play-card-attack", static () => new PlayCardAttackIntent<RegretEgoCard>("LOR_VERIFY_PLAY", () => 6m));

        // Badged：五个类，徽记种类覆盖能力（角标数字）、状态牌（卡牌小图）、治疗、召唤、自定义（主图标叠加）、
        // 卡牌负面、带左右文字、动态数值、不可见、超过两个换行。
        yield return ("badged.attack.weak", static () => new BadgedAttackIntent(8, IntentBadge.Weak(2)));
        yield return ("badged.attack.multi-badges", () => new BadgedAttackIntent(
            5, 2, null,
            IntentBadge.Strength(2), IntentBadge.Bleed(3), IntentBadge.Heal(4), IntentBadge.Summon(1), Stun()));
        yield return ("badged.attack.overlays", () => new BadgedAttackIntent(5, null, Stun(), Stun(), Stun()));
        yield return ("badged.attack.flaw-turns", static () => new BadgedAttackIntent(6, IntentBadge.Flaw(2, 3)));
        yield return ("badged.attack.status-card", static () => new BadgedAttackIntent(6, IntentBadge.StatusCard<Dazed>(2)));
        yield return ("badged.attack.card-debuff", static () => new BadgedAttackIntent(6, IntentBadge.CardDebuff<Dazed>(1)));
        yield return ("badged.attack.invisible", static () => new BadgedAttackIntent(6, IntentBadge.Weak(1).WithoutVisual()));
        yield return ("badged.attack.dynamic", static () => new BadgedAttackIntent(
            () => 7m, null, null, IntentBadge.FromPower<StrengthPower>(() => 3)));
        yield return ("badged.attack.group", () => new BadgedAttackIntent(5, null, Group(), IntentBadge.Weak(1)));
        yield return ("badged.targeted-attack", static () => new BadgedTargetedAttackIntent(9, 1, IntentBadge.Vulnerable(2)));
        yield return ("badged.targeted-attack.vanilla-player", static () => new BadgedTargetedAttackIntent(
            () => 9, () => 1, null, null, true, IntentBadge.Vulnerable(2)));
        yield return ("badged.defend", static () => new BadgedDefendIntent(IntentBadge.Guard(2, 1), 5));
        yield return ("badged.buff", static () => new BadgedBuffIntent(IntentBadge.Strength(2), 2));
        yield return ("badged.buff.overlay", () => new BadgedBuffIntent(Stun()));
        yield return ("badged.debuff", static () => new BadgedDebuffIntent(IntentBadge.Weak(2), 2));
        yield return ("badged.debuff.status", static () => new BadgedDebuffIntent(
            new[] { IntentBadge.StatusCards(2), IntentBadge.Frail(1) }));

        // Detailed：能力、负面、卡牌负面（浮动卡牌）、效果组、状态牌（各牌堆、指定目标、动态、分堆）。
        yield return ("detailed.buff.self", static () => new DetailedBuffIntent<StrengthPower>(2));
        yield return ("detailed.buff.all-enemies", static () => new DetailedBuffIntent<StrengthPower>(2, DetailedBuffTargetScope.AllEnemies));
        yield return ("detailed.buff.target", () => new DetailedBuffIntent<StrengthPower>(
            2, DetailedBuffTargetScope.Target, _ => [firstPlayer]));
        yield return ("detailed.debuff", static () => new DetailedDebuffIntent<WeakPower>(2));
        yield return ("detailed.debuff.scope", static () => new DetailedDebuffIntent<VulnerablePower>(1, DetailedIntentScopeText.AllEnemies));
        yield return ("detailed.card-debuff", static () => new DetailedCardDebuffIntent<Dazed>());
        yield return ("detailed.buff-group", static () => new DetailedBuffGroupIntent(
            DetailedIntentVisualEffect.StatusCard<Dazed>(1, PileType.Draw),
            DetailedIntentVisualEffect.FromBadge(IntentBadge.Strength(2))));
        yield return ("detailed.status-card.draw", static () => new DetailedStatusCardIntent<Dazed>(2, PileType.Draw));
        yield return ("detailed.status-card.discard-scope", static () =>
            new DetailedStatusCardIntent<Burn>(1, PileType.Discard, DetailedIntentScopeText.Target));
        yield return ("detailed.status-card.hand-no-marker", static () =>
            new DetailedStatusCardIntent<Dazed>(3, PileType.Hand, null, showSingleTargetMarker: false));
        yield return ("detailed.status-card.targeted", () => new TargetedDetailedStatusCardIntent<Wound>(
            2, PileType.Discard, "FALSE_THRONE_MANNERS.description", _ => players));
        yield return ("detailed.status-card.dynamic", static () => new DynamicDetailedStatusCardIntent<Dazed>(() => 3, PileType.Discard));
        yield return ("detailed.status-card.split", static () => new DetailedSplitStatusCardIntent<Dazed>(
            new[] { (1, PileType.Draw), (2, PileType.Discard) }));

        // 反击意图与闪避。
        yield return ("counter.attack", static () => new CounterAttackIntent(5));
        yield return ("counter.attack.multi", static () => new CounterAttackIntent(4, 3));
        yield return ("counter.attack.tier4", static () => new CounterAttackIntent(25));
        yield return ("counter.defend", static () => new CounterDefendIntent(4));
        yield return ("counter.buff", static () => new CounterBuffIntent());
        yield return ("counter.debuff", static () => new CounterDebuffIntent());
        yield return ("counter.card-debuff", static () => new CounterCardDebuffIntent());
        yield return ("counter.dodge", static () => new CounterDodgeIntent(2));
        yield return ("counter.summon", static () => new CounterSummonIntent());
        yield return ("counter.status", static () => new CounterStatusIntent(2));
        yield return ("counter.dodge-plain", static () => new DodgeIntent(3));

        // 指定目标、无差别攻击、狐狸数值与动态攻击。
        yield return ("targeted.monster-attack", static () => new TargetedMonsterAttackIntent(7, 1, "WOLF_FEROCIOUS_FANGS.description"));
        yield return ("indiscriminate", static () => new IndiscriminateAttackIntent(5, 1, null));
        yield return ("indiscriminate.badged", static () => new IndiscriminateAttackIntent(5, 2, null, IntentBadge.Weak(1)));
        yield return ("dynamic-attack", static () => new DynamicAttackIntent(4, () => 2));
    }

    /// <summary>目标全是怪物时，徽记与状态牌的作用范围文字（“所有敌人”“其他敌人”）才会出现。</summary>
    private static IEnumerable<(string Name, Func<AbstractIntent> Create)> ScopeCases()
    {
        yield return ("badged.debuff", static () => new BadgedDebuffIntent(IntentBadge.Weak(2), 2));
        yield return ("badged.buff", static () => new BadgedBuffIntent(IntentBadge.Strength(2), 2));
        yield return ("badged.attack.status-card", static () => new BadgedAttackIntent(6, IntentBadge.StatusCard<Dazed>(2)));
        yield return ("combined.defend-debuff", static () => new CombinedDefendDebuffIntent(5, null, IntentBadge.Weak(1)));
        yield return ("detailed.status-card.draw", static () => new DetailedStatusCardIntent<Dazed>(2, PileType.Draw));
        yield return ("detailed.status-card.split", static () => new DetailedSplitStatusCardIntent<Dazed>(
            new[] { (1, PileType.Draw), (2, PileType.Discard) }));
        yield return ("detailed.buff.all-enemies", static () => new DetailedBuffIntent<StrengthPower>(2, DetailedBuffTargetScope.AllEnemies));
        yield return ("targeted.monster-attack", static () => new TargetedMonsterAttackIntent(7, 1, "WOLF_FEROCIOUS_FANGS.description"));
    }

    /// <summary>怪物层：复合显示的成对简化、隐藏占位、反击尾巴、徽记攻击替换、敌方卡牌挂载。</summary>
    private static IEnumerable<(string Name, Func<AbstractIntent[]> Create)> CreatureCases(IReadOnlyList<Creature> players)
    {
        EnemyCardSpec Spec(string id) => new(
            id,
            static () => EnemyCardSpec.CreateDisplayCardModel<RegretEgoCard>(),
            cost: 1,
            priority: 0,
            static (_, _) => Task.CompletedTask,
            static spec => new EnemyCardIntent(spec));

        yield return ("single", static () => [new SingleAttackIntent(4)]);
        yield return ("pair.single-defend", static () => [new SingleAttackIntent(6), new DefendIntent()]);
        yield return ("pair.defend-single", static () => [new DefendIntent(), new SingleAttackIntent(6)]);
        yield return ("pair.multi-buff", static () => [new MultiAttackIntent(3, 2), new BuffIntent()]);
        yield return ("pair.single-debuff", static () => [new SingleAttackIntent(12), new DebuffIntent()]);
        yield return ("pair.dynamic-debuff", static () => [new DynamicAttackIntent(4, () => 2), new DebuffIntent()]);
        yield return ("pair.defend-buff", static () => [new DefendIntent(), new BuffIntent()]);
        yield return ("pair.defend-debuff", static () => [new DefendIntent(), new DebuffIntent()]);
        yield return ("pair.buff-debuff", static () => [new BuffIntent(), new DebuffIntent()]);
        yield return ("pair.counterattack-counterdefend", static () => [new CounterAttackIntent(5), new CounterDefendIntent(3)]);
        yield return ("pair.counterattack-counterbuff", static () => [new CounterAttackIntent(5), new CounterBuffIntent()]);
        yield return ("pair.counterattack-counterdebuff", static () => [new CounterDebuffIntent(), new CounterAttackIntent(5)]);
        yield return ("pair.counterattack-countercarddebuff", static () => [new CounterAttackIntent(5), new CounterCardDebuffIntent()]);
        yield return ("pair.counterdefend-counterbuff", static () => [new CounterDefendIntent(3), new CounterBuffIntent()]);
        yield return ("pair.counterdefend-counterdebuff", static () => [new CounterDebuffIntent(), new CounterDefendIntent(3)]);
        yield return ("pair.plus-counter-tail", static () => [new SingleAttackIntent(6), new DefendIntent(), new CounterAttackIntent(5)]);
        yield return ("triple.no-simplify", static () => [new SingleAttackIntent(6), new DefendIntent(), new BuffIntent()]);
        yield return ("hidden.then-pair", static () => [new HiddenIntent(), new SingleAttackIntent(6), new DebuffIntent()]);
        yield return ("hidden.only", static () => [new HiddenIntent(), new HiddenIntent()]);
        yield return ("badged-attack.debuff", static () => [new BadgedAttackIntent(6, IntentBadge.Weak(1))]);
        yield return ("badged-attack.buff", static () => [new BadgedAttackIntent(6, IntentBadge.Strength(1))]);
        yield return ("badged-attack.neutral", static () => [new BadgedAttackIntent(6, IntentBadge.Custom(GroupAttackIconPath))]);
        yield return ("badged-targeted.debuff", static () => [new BadgedTargetedAttackIntent(6, 1, IntentBadge.Weak(1))]);
        yield return ("combined-present.no-pair", static () => [new CombinedAttackDebuffIntent(5), new DefendIntent()]);
        yield return ("enemycard.move", () => [new EnemyCardIntent(Spec("LOR_VERIFY_MOVE_A"), () => 6m)]);
        yield return ("enemycard.mixed", () => [new EnemyCardIntent(Spec("LOR_VERIFY_MOVE_B"), () => 6m), new SingleAttackIntent(3)]);
        yield return ("enemycard.combined", () => [new EnemyCardCombinedDefendBuffIntent(Spec("LOR_VERIFY_MOVE_C"))]);
    }

    // ---------------------------------------------------------------- runners

    private static void RunIntentCase(
        string name,
        Func<AbstractIntent> create,
        Creature owner,
        IReadOnlyList<Creature> targets,
        Control host)
    {
        AbstractIntent intent;
        try
        {
            intent = create();
        }
        catch (Exception ex)
        {
            Row(name, "create", "exception " + ex.GetType().Name + ": " + ex.Message);
            return;
        }

        NIntent node = NIntent.Create(0f);
        host.AddChild(node);
        try
        {
            node.UpdateIntent(intent, targets, owner);
            Row(name, "intent", intent.GetType().Name + "|type=" + intent.IntentType);
            DumpTree(name, "tree", node);

            // 动画帧由累计时间推出，这里固定两个时间点，只看后缀换上的图标。
            foreach (float accumulator in new[] { 0f, 0.4f })
            {
                SetField(node, "_timeAccumulator", accumulator);
                node._Process(0.0);
                Row(name, "frame@" + accumulator.ToString("0.0", CultureInfo.InvariantCulture),
                    TexturePath(node.GetNode<Sprite2D>("%Intent").Texture));
            }

            // 第二次刷新：视觉哈希不变时不应重建节点。
            node.UpdateIntent(intent, targets, owner);
            Row(name, "again", ChildSummary(node.GetNode<Control>("%IntentHolder")));

            Row(name, "hovertip", Safe(() => DescribeTip(intent.GetHoverTip(targets, owner))));

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

            Row(name, "hover-shown", CapturedHoverTips.Count + ":" + string.Join(" || ", CapturedHoverTips));
            Row(name, "hover-lines", DescribeOverlay(owner));
            AccessTools.Method(typeof(NIntent), "OnUnhovered").Invoke(node, null);
            Row(name, "unhover-lines", DescribeOverlay(owner));

            if (PipelineAvailable())
            {
                DumpPipeline(name, intent, targets, owner, host);
            }
        }
        catch (Exception ex)
        {
            Row(name, "exception", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            host.RemoveChild(node);
            node.QueueFree();
        }
    }

    private static void RunCreatureCase(string name, NCreature creatureNode, AbstractIntent[] intents, IReadOnlyList<Creature> targets)
    {
        MonsterModel monster = creatureNode.Entity.Monster!;
        MoveState original = monster.NextMove;
        var move = new MoveState("LOR_VERIFY_" + name.ToUpperInvariant(), static _ => Task.CompletedTask, intents);
        try
        {
            monster.SetMoveImmediate(move, forceTransition: true);
            creatureNode.UpdateIntent(targets);
            Row(name, "move", string.Join(",", intents.Select(static intent => intent.GetType().Name)));
            DumpContainer(name, creatureNode);
            if (PipelineAvailable())
            {
                DumpCreaturePipeline(name, creatureNode, targets);
            }
        }
        catch (Exception ex)
        {
            Row(name, "exception", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            monster.SetMoveImmediate(original, forceTransition: true);
        }
    }

    private static void DumpContainer(string name, NCreature creatureNode)
    {
        Control container = creatureNode.IntentContainer;
        Row(name, "container", "children=" + container.GetChildCount()
                                           + "|size=" + Vec(container.Size)
                                           + "|min=" + Vec(container.CustomMinimumSize)
                                           + "|layout=" + DescribeContainerLayout(container));
        int index = 0;
        foreach (Node child in container.GetChildren())
        {
            if (child is NIntent intentNode)
            {
                AbstractIntent? intent = GetField<AbstractIntent>(intentNode, "_intent");
                Row(name, "slot" + index, (intent?.GetType().Name ?? "null") + "|frozen=" + GetField<bool>(intentNode, "_isFrozen"));
                if (intent is ICounterIntent || intent is ICombinedIntentVisual)
                {
                    IEnumerable<Creature>? slotTargets = GetField<IEnumerable<Creature>>(intentNode, "_targets");
                    Row(name, "slot" + index + ".targets", string.Join(",", slotTargets?.Select(DescribeCreature) ?? []));
                }
            }

            DumpTree(name, "slot" + index, child);
            index++;
        }
    }

    private static string DescribeContainerLayout(Control container)
    {
        if (container is not HBoxContainer box)
        {
            return container.GetType().Name;
        }

        string separation = box.HasThemeConstantOverride("separation")
            ? box.GetThemeConstant("separation").ToString(CultureInfo.InvariantCulture)
            : "-";
        return "hbox:sep=" + separation + ":align=" + box.Alignment;
    }

    // ---------------------------------------------------------------- pipeline (branch only)

    /// <summary>重构前的构建里没有这些类型；只在类型存在时调用，这些方法在调用前不会被 JIT。</summary>
    private static bool PipelineAvailable() =>
        typeof(IntentBadge).Assembly.GetType("LibraryOfRuina.framework.intents.rendering.IntentRenderPipeline", throwOnError: false) != null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DumpPipeline(string name, AbstractIntent intent, IReadOnlyList<Creature> targets, Creature owner, Control host)
    {
        if (intent is ICompositeIntent composite)
        {
            Pipe(name, "composite", composite.Composite.ToString());
        }

        NIntent node = NIntent.Create(0f);
        host.AddChild(node);
        try
        {
            var trace = new List<IntentRenderTraceEntry>();
            var context = new IntentRenderContext { IntentNode = node, Intent = intent, Targets = targets, Owner = owner };
            IntentRenderPipeline.Run(IntentRenderStage.IntentVisuals, ref context, trace);
            IntentRenderPipeline.Run(IntentRenderStage.IntentVisuals, ref context, trace);
            context.AnimationFrame = 3;
            IntentRenderPipeline.Run(IntentRenderStage.IntentFrame, ref context, trace);
            IntentRenderPipeline.Run(IntentRenderStage.IntentFrame, ref context, trace);
            context.HoverTip = new HoverTip(new MegaCrit.Sts2.Core.Localization.LocString("intents", "ATTACK.title"), "base");
            IntentRenderPipeline.Run(IntentRenderStage.HoverTip, ref context, trace);
            bool runOriginal = IntentRenderPipeline.Run(IntentRenderStage.IntentHovered, ref context, trace);
            Pipe(name, "trace", string.Join(" ", trace.Select(static entry => entry.Stage + ":" + entry.Decorator + "=" + entry.Outcome))
                                + " | hovered.runOriginal=" + runOriginal);
            NCombatRoom.Instance?.GetCreatureNode(owner)?.HideHoverTips();
        }
        catch (Exception ex)
        {
            Pipe(name, "exception", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            host.RemoveChild(node);
            node.QueueFree();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DumpCreaturePipeline(string name, NCreature creatureNode, IReadOnlyList<Creature> targets)
    {
        var trace = new List<IntentRenderTraceEntry>();
        var context = new IntentRenderContext { CreatureNode = creatureNode, Targets = targets };
        IntentRenderPipeline.Run(IntentRenderStage.CreatureLayout, ref context, trace);
        IntentRenderPipeline.Run(IntentRenderStage.CreatureDecorate, ref context, trace);
        Pipe(name, "trace", string.Join(" ", trace.Select(static entry => entry.Stage + ":" + entry.Decorator + "=" + entry.Outcome)));
        foreach (IntentRenderStage stage in Enum.GetValues<IntentRenderStage>())
        {
            if (name == "bird.single")
            {
                Pipe("order", stage.ToString(), string.Join(",", IntentRenderPipeline.DecoratorNames(stage)));
            }
        }
    }

    // ---------------------------------------------------------------- dump helpers

    private static void DumpTree(string name, string section, Node root)
    {
        Walk(root, string.Empty, 0);

        void Walk(Node node, string parentPath, int depth)
        {
            string path = parentPath.Length == 0 ? NodeName(node) : parentPath + "/" + NodeName(node);
            Row(name, section, path + "|" + DescribeNode(node));
            if (node is NCard || depth >= 10)
            {
                return;
            }

            foreach (Node child in node.GetChildren())
            {
                Walk(child, path, depth + 1);
            }
        }
    }

    private static string ChildSummary(Node node) =>
        node.GetChildCount() + ":" + string.Join(",", node.GetChildren().Select(NodeName));

    private static string NodeName(Node node)
    {
        string name = node.Name.ToString();
        // 未命名节点由 Godot 按全局计数器起名（@Control@123），计数与之前创建过多少节点有关，只保留类型。
        return name.StartsWith('@') ? "@" + node.GetType().Name : name;
    }

    private static string DescribeNode(Node node)
    {
        var text = new StringBuilder(node.GetType().Name);
        if (node is CanvasItem item)
        {
            text.Append(" vis=").Append(item.Visible);
            text.Append(" mod=").Append(Col(item.Modulate));
            if (item.SelfModulate != Colors.White)
            {
                text.Append(" self=").Append(Col(item.SelfModulate));
            }

            text.Append(" z=").Append(item.ZIndex).Append(item.ZAsRelative ? "" : "abs");
            if (item.Material != null)
            {
                text.Append(" mat=").Append(DescribeMaterial(item.Material));
            }
        }

        if (node is Control control)
        {
            // 意图容器随时间上下浮动，位置不记录。
            if (node.Name.ToString() != "IntentHolder")
            {
                text.Append(" pos=").Append(Vec(control.Position));
            }

            text.Append(" size=").Append(Vec(control.Size));
            if (control.CustomMinimumSize != Vector2.Zero)
            {
                text.Append(" min=").Append(Vec(control.CustomMinimumSize));
            }

            if (control.Scale != Vector2.One)
            {
                text.Append(" scale=").Append(Vec(control.Scale));
            }

            if (control.PivotOffset != Vector2.Zero)
            {
                text.Append(" pivot=").Append(Vec(control.PivotOffset));
            }

            text.Append(" mouse=").Append(control.MouseFilter);
            if (control.ClipContents)
            {
                text.Append(" clip");
            }

            int entered = control.GetSignalConnectionList(Control.SignalName.MouseEntered).Count;
            if (entered > 0)
            {
                text.Append(" onEnter=").Append(entered);
            }
        }
        else if (node is Node2D node2D)
        {
            text.Append(" pos=").Append(Vec(node2D.Position));
            text.Append(" scale=").Append(Vec(node2D.Scale));
            if (node2D.Rotation != 0f)
            {
                text.Append(" rot=").Append(F(node2D.Rotation));
            }
        }

        switch (node)
        {
            case Sprite2D sprite:
                // 节点树里的意图精灵可能已被引擎按真实时间推进过动画，帧号不记录；固定帧的对照在 frame@ 行。
                text.Append(" tex=").Append(FrameNumber.Replace(TexturePath(sprite.Texture), "_##"));
                break;
            case TextureRect rect:
                text.Append(" tex=").Append(TexturePath(rect.Texture))
                    .Append(" expand=").Append(rect.ExpandMode)
                    .Append(" stretch=").Append(rect.StretchMode);
                break;
            case Label label:
                text.Append(" text=\"").Append(label.Text).Append('"')
                    .Append(" h=").Append(label.HorizontalAlignment)
                    .Append(" v=").Append(label.VerticalAlignment)
                    .Append(" overrun=").Append(label.TextOverrunBehavior);
                if (label.LabelSettings is { } settings)
                {
                    text.Append(" font=").Append(settings.Font?.ResourcePath ?? "null")
                        .Append(" fs=").Append(settings.FontSize)
                        .Append(" fc=").Append(Col(settings.FontColor))
                        .Append(" ol=").Append(settings.OutlineSize).Append(':').Append(Col(settings.OutlineColor))
                        .Append(" sh=").Append(settings.ShadowSize).Append(':').Append(Col(settings.ShadowColor));
                }

                break;
            case RichTextLabel rich:
                text.Append(" text=\"").Append(rich.Text).Append('"');
                break;
            case ColorRect colorRect:
                text.Append(" color=").Append(Col(colorRect.Color));
                break;
            case CpuParticles2D particles:
                text.Append(" tex=").Append(TexturePath(particles.Texture));
                break;
            case NCard card:
                text.Append(" card=").Append(card.Model?.Id.Entry ?? "null");
                break;
        }

        Godot.Collections.Array<StringName> metas = node.GetMetaList();
        if (metas.Count > 0)
        {
            text.Append(" meta=").Append(string.Join(",", metas.Select(static meta => meta.ToString()).OrderBy(static meta => meta, StringComparer.Ordinal)));
        }

        return text.ToString();
    }

    private static string DescribeMaterial(Material material)
    {
        if (material is ShaderMaterial { Shader: { } shader })
        {
            return "shader#" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shader.Code)))[..8];
        }

        return string.IsNullOrEmpty(material.ResourcePath) ? material.GetType().Name : material.ResourcePath;
    }

    private static string DescribeOverlay(Creature owner)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        Node? overlay = ownerNode?.GetNodeOrNull(OverlayNodeName);
        if (overlay == null)
        {
            return "none";
        }

        IReadOnlyList<Creature>? targets = GetField<IReadOnlyList<Creature>>(overlay, "_targets");
        return "targets=" + string.Join(",", targets?.Select(DescribeCreature) ?? [])
                          + "|ally=" + GetField<bool>(overlay, "_isAlly")
                          + "|lines=" + overlay.GetChildCount();
    }

    private static string DescribeTip(IHoverTip tip)
    {
        var text = new StringBuilder(tip.GetType().Name);
        text.Append(" id=").Append(tip.Id);
        if (tip is HoverTip hoverTip)
        {
            text.Append(" title=\"").Append(hoverTip.Title).Append('"')
                .Append(" desc=\"").Append(hoverTip.Description).Append('"')
                .Append(" icon=").Append(TexturePath(hoverTip.Icon))
                .Append(" smart=").Append(hoverTip.IsSmart)
                .Append(" debuff=").Append(hoverTip.IsDebuff);
        }

        return text.ToString();
    }

    private static string DescribeCreature(Creature creature) =>
        (creature.Monster?.GetType().Name ?? (creature.IsPlayer ? "player" : "creature"))
        + "@" + (creature.SlotName ?? "-")
        + (creature.IsAlive ? "" : ":dead");

    private static string TexturePath(Texture2D? texture)
    {
        if (texture == null)
        {
            return "null";
        }

        if (!string.IsNullOrEmpty(texture.ResourcePath))
        {
            return texture.ResourcePath;
        }

        return texture switch
        {
            AtlasTexture atlas => "atlas(" + (atlas.Atlas?.ResourcePath ?? "null") + "@" + atlas.Region + ")",
            _ => texture.GetType().Name + "(" + texture.GetWidth() + "x" + texture.GetHeight() + ")"
        };
    }

    // ---------------------------------------------------------------- plumbing

    private static void Row(string name, string section, string data)
    {
        string text = "ROW|" + name + "|" + section + "|" + data;
        _rows++;
        Digest.Append(text).Append('\n');
        Log.Info(LogPrefix + text);
    }

    private static void Pipe(string name, string section, string data) =>
        Log.Info(LogPrefix + "PIPE|" + name + "|" + section + "|" + data);

    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            return "exception " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Vec(Vector2 value) => "(" + F(value.X) + "," + F(value.Y) + ")";

    private static string Col(Color color) =>
        "(" + F(color.R) + "," + F(color.G) + "," + F(color.B) + "," + F(color.A) + ")";

    private static T? GetField<T>(object instance, string field)
    {
        FieldInfo? info = instance.GetType().GetField(field, PrivateInstance)
                          ?? instance.GetType().BaseType?.GetField(field, PrivateInstance);
        return info == null ? default : (T?)info.GetValue(instance);
    }

    private static void SetField(object instance, string field, object value)
    {
        FieldInfo info = instance.GetType().GetField(field, PrivateInstance)
                         ?? throw new MissingFieldException(instance.GetType().Name, field);
        info.SetValue(instance, value);
    }

    private static NCreature RequireNode(Creature creature) =>
        NCombatRoom.Instance?.GetCreatureNode(creature)
        ?? throw new InvalidOperationException("No creature node for " + DescribeCreature(creature));

    private static async Task<CombatState> StartFight(
        EncounterModel encounter,
        RoomType roomType,
        MapPointType mapPointType,
        string seed)
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
        await RunManager.Instance.EnterRoomDebug(roomType, mapPointType, encounter, showTransition: false);
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
            .Select(static assembly => assembly.GetType("ActLikeIt2.Runtime.ActSelectionGate", throwOnError: false))
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
