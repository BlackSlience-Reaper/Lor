using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.specialguests;
using LibraryOfRuina.specialguests.Iori;
using LibraryOfRuina.specialguests.Kali;
using LibraryOfRuina.specialguests.Rnfmabj;
using LibraryOfRuina.specialguests.Xiao;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 特邀嘉宾多意图计划的逐回合轨迹：伊织（一、二阶段，含撤离与读回快照）、Rnfmabj（本体与双手，含指令取消、
/// 假死、合体与三阶段）、萧（一阶段含米莉丝、眩晕重排与假死收尾；二阶段）、卡莉（E.G.O. 显现、解除与再显现）。
/// 用真实回合循环驱动（结束玩家回合、等敌方回合与下一个玩家回合），每个玩家回合开始与每个脚本动作后把
/// 计划槽位、意图（类型、标签、伤害）、状态机、嘉宾自身状态和战斗摘要记为 <c>TRACE|</c> 行；敌方回合里的
/// 行动切换（SetMoveImmediate）、行动执行、动画触发、格挡获得记为 <c>TRACE|…|exec|</c> 行。
/// 同一个验证程序集分别配改动前后的主模组构建各跑一次，逐行比较 TRACE 行。
/// <c>rnfmabj-duo</c> 是两名玩家的 Rnfmabj 指令：两人进度不同、一人打错牌归零，存下三只怪的 SavedProperty 后
/// 在同种子的新跑图里读回再续战，读回后的参与者与逐人进度要与存档时一致（不一致记为失败）。
/// 参数 <c>lor-verify-special-guest-plan-trace</c>；加 <c>-iori</c>、<c>-rnfmabj</c>、<c>-xiao</c>、<c>-kali</c>、
/// <c>-rnfmabj-duo</c> 只跑一个场景。
/// </summary>
internal static class SpecialGuestPlanTraceVerificationPatch
{
    private const string VerifyArg = "lor-verify-special-guest-plan-trace";
    private const string LogPrefix = "[LibraryOfRuina.SpecialGuestPlanTrace.Verify] ";
    private const int PlayerMaxHp = 9999;
    private const string IoriStoryId = "IORI_SPECIAL_GUEST_STORY";

    private static readonly (string Name, Func<Task> Run)[] Guests =
    [
        ("iori", RunIori),
        ("rnfmabj", RunRnfmabj),
        ("xiao", RunXiao),
        ("kali", RunKali),
        ("rnfmabj-duo", RunRnfmabjDuo),
    ];

    private static readonly JsonSerializerOptions SavedStateJson = new() { IncludeFields = true };
    private static readonly List<string> Failures = [];
    private static bool _started;
    private static bool _tracing;
    private static string _label = string.Empty;
    private static Harmony? _harmony;

    internal static void Start()
    {
        if (_started || SelectedGuests() is not { Length: > 0 })
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static string[] SelectedGuests()
    {
        if (HasArg(VerifyArg))
        {
            return Guests.Select(static guest => guest.Name).ToArray();
        }

        return Guests
            .Select(static guest => guest.Name)
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
            string[] selected = SelectedGuests();
            Log.Info(LogPrefix + "guests: " + string.Join(",", selected));
            foreach ((string name, Func<Task> run) in Guests)
            {
                if (!selected.Contains(name))
                {
                    continue;
                }

                // 一位嘉宾出错不影响后面的嘉宾；异常本身也写进 TRACE 参与对照。
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

            Log.Info(LogPrefix + "SPECIAL_GUEST_PLAN_TRACE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "SPECIAL_GUEST_PLAN_TRACE_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    // ---- scenarios ------------------------------------------------------------------------------------------

    private static async Task RunIori()
    {
        await StartRun("LORPLANTRACEIORI");
        RunState runState = RunManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Run state is null.");
        // 正式流程在进入嘉宾战之前挂好载体；这里直接进战斗，先补上。战前剧情要等玩家点击，headless 下标记为已读。
        SpecialGuestRunStateModifier.GetOrCreate(runState).MarkStoryCompleted(IoriStoryId);

        CombatState one = await EnterCombat(
            "iori1",
            RoomType.Monster,
            ModelDb.Encounter<IoriSpecialGuestStageOneEncounter>().ToMutable());
        await PlayRounds("iori1", one, 5, async (round, state) =>
        {
            if (round == 5 && Guest<IoriStageOne>(state) is { } iori)
            {
                await CreatureCmd.SetCurrentHp(iori.Creature, Math.Ceiling(iori.Creature.MaxHp * 0.5m));
                return "hp to escape threshold";
            }

            return null;
        });
        await EndTurnAndWait("iori1", one, 5);
        string? snapshot = SpecialGuestRunStateModifier.TryGet(runState)
            ?.GetValue(IoriSpecialGuestIds.SnapshotValueKey);
        TraceLine("iori1", "stored snapshot", snapshot ?? "<none>");
        await EndRunAndWait("iori stage one");

        // 二阶段在新的跑图里开战，把一阶段撤离时存下的快照原样放回载体，走二阶段的读回路径。
        await StartRun("LORPLANTRACEIORI2");
        RunState stageTwoRun = RunManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Run state is null.");
        SpecialGuestRunStateModifier stageTwoCarrier = SpecialGuestRunStateModifier.GetOrCreate(stageTwoRun);
        stageTwoCarrier.MarkStoryCompleted(IoriStoryId);
        stageTwoCarrier.SetValue(IoriSpecialGuestIds.SnapshotValueKey, snapshot);
        CombatState two = await EnterCombat(
            "iori2",
            RoomType.Elite,
            ModelDb.Encounter<IoriSpecialGuestStageTwoEncounter>().ToMutable());
        await PlayRounds("iori2", two, 6, static (_, _) => Task.FromResult<string?>(null));
    }

    private static async Task RunRnfmabj()
    {
        await StartRun("LORPLANTRACERNFMABJ");
        CombatState state = await EnterCombat(
            "rnfmabj",
            RoomType.Elite,
            ModelDb.Encounter<RnfmabjSpecialGuestEncounter>().ToMutable());
        await PlayRounds("rnfmabj", state, 8, async (round, combat) =>
        {
            switch (round)
            {
                case 2 when Guest<Rnfmabj>(combat) is { } body:
                {
                    MethodInfo cancel = typeof(Rnfmabj).GetMethod(
                            "CancelLeftmostDirectiveIntent",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException(typeof(Rnfmabj).FullName, "CancelLeftmostDirectiveIntent");
                    bool canceled = await (Task<bool>)cancel.Invoke(body, null)!;
                    return "cancel leftmost directive intent=" + canceled;
                }
                case 3:
                    foreach (RnfmabjHandBase hand in combat.Enemies
                                 .Select(static enemy => enemy.Monster)
                                 .OfType<RnfmabjHandBase>()
                                 .ToArray())
                    {
                        await CreatureCmd.SetCurrentHp(hand.Creature, Rnfmabj.HandDisabledThreshold);
                    }

                    return "hands to disabled threshold";
                case 5 when Guest<Rnfmabj>(combat) is { } body:
                    await CreatureCmd.SetCurrentHp(body.Creature, Rnfmabj.BodyPhaseThreshold);
                    return "body to phase-three threshold";
                default:
                    return null;
            }
        });
    }

    private static async Task RunXiao()
    {
        await StartRun("LORPLANTRACEXIAO");
        CombatState one = await EnterCombat(
            "xiao1",
            RoomType.Monster,
            ModelDb.Encounter<XiaoSpecialGuestStageOneEncounter>().ToMutable());
        await PlayRounds("xiao1", one, 5, async (round, state) =>
        {
            switch (round)
            {
                case 3 when Guest<XiaoStageOne>(state)?.Creature is LibraryCreature xiao:
                    await LibraryCreatureCmd.SetCurrentChaoValue(xiao, 0m);
                    return "xiao stunned";
                case 4 when Guest<Miris>(state) is { } miris:
                    await CreatureCmd.Kill(miris.Creature, force: true);
                    return "miris killed";
                case 5 when Guest<XiaoStageOne>(state) is { } xiao:
                    await CreatureCmd.SetCurrentHp(xiao.Creature, 100m);
                    return "xiao to fake-death threshold";
                default:
                    return null;
            }
        });
        await EndRunAndWait("xiao stage one");

        await StartRun("LORPLANTRACEXIAOEGO");
        CombatState two = await EnterCombat(
            "xiao2",
            RoomType.Elite,
            ModelDb.Encounter<XiaoSpecialGuestStageTwoEncounter>().ToMutable());
        await PlayRounds("xiao2", two, 7, static (_, _) => Task.FromResult<string?>(null));
    }

    private static async Task RunKali()
    {
        await StartRun("LORPLANTRACEKALI");
        CombatState state = await EnterCombat(
            "kali",
            RoomType.Elite,
            ModelDb.Encounter<KaliSpecialGuestEncounter>().ToMutable());
        await PlayRounds("kali", state, 8, async (round, combat) =>
        {
            switch (round)
            {
                case 2 when Guest<Kali>(combat) is { } kali:
                    await CreatureCmd.SetCurrentHp(kali.Creature, kali.ScaledEgoHpThreshold - 1m);
                    return "hp below ego threshold";
                case 4 when Guest<Kali>(combat)?.Creature is LibraryCreature kali:
                    await LibraryCreatureCmd.SetCurrentChaoValue(kali, 0m);
                    return "chao to zero";
                default:
                    return null;
            }
        });
    }

    private static async Task RunRnfmabjDuo()
    {
        const string seed = "LORPLANTRACERNFMABJDUO";
        const string label = "rnfmabj-duo";
        await StartRun(seed, playerCount: 2);
        CombatState first = await EnterCombat(
            label,
            RoomType.Elite,
            ModelDb.Encounter<RnfmabjSpecialGuestEncounter>().ToMutable());
        IReadOnlyList<Player> players = RunPlayers();
        Player one = players[0];
        Player two = players[1];

        // 第 1 回合：一号玩家按序打两张；二号玩家打对一张、打错一张（进度归零）、再打对一张。存档时两人进度不同。
        await PlayRounds(label, first, 1, async (_, state) =>
        {
            await PlayDirectiveCard(label, state, one, correct: true);
            await PlayDirectiveCard(label, state, one, correct: true);
            await PlayDirectiveCard(label, state, two, correct: true);
            await PlayDirectiveCard(label, state, two, correct: false);
            await PlayDirectiveCard(label, state, two, correct: true);
            return "directive plays";
        });

        Rnfmabj savedBody = Guest<Rnfmabj>(first)
            ?? throw new InvalidOperationException("Rnfmabj is missing before the save.");
        string expected = DescribeDirectivePlayers(savedBody);
        Check(expected.Contains("dirProgressBy=[P1:2,P2:1]", StringComparison.Ordinal),
            label + ": progress before the save was not P1:2,P2:1: " + expected);
        // 存档格式与本模组自存怪物状态时相同：SavedProperties 经 JSON 往返。按槽位存，读档时按槽位放回。
        Dictionary<string, string> saved = first.Enemies
            .Where(static enemy => enemy.Monster != null && enemy.SlotName != null)
            .ToDictionary(
                static enemy => enemy.SlotName!,
                static enemy => JsonSerializer.Serialize(
                    SavedProperties.From(enemy.Monster!),
                    SavedStateJson));
        TraceLine(label, "save", "slots=" + string.Join(",", saved.Keys.OrderBy(static key => key, StringComparer.Ordinal))
            + expected);
        await AbandonRun(label + " save");

        await StartRun(seed, playerCount: 2);
        RunState resumedRun = RunManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Run state is null.");
        EncounterModel encounter = ModelDb.Encounter<RnfmabjSpecialGuestEncounter>().ToMutable();
        encounter.GenerateMonstersWithSlots(resumedRun);
        foreach ((MonsterModel monster, string? slot) in encounter.MonstersWithSlots)
        {
            if (slot == null || !saved.TryGetValue(slot, out string? json))
            {
                throw new InvalidOperationException("No saved state for slot " + slot + ".");
            }

            FillSavedState(
                JsonSerializer.Deserialize<SavedProperties>(json, SavedStateJson)
                    ?? throw new InvalidOperationException("Saved state for slot " + slot + " did not deserialize."),
                monster,
                slot);
            if (monster is Rnfmabj loadedBody)
            {
                TraceLine(label + "-resume", "loaded", "slot=" + slot + DescribeDirectivePlayers(loadedBody));
            }
        }

        CombatState second = await EnterCombat(label + "-resume", RoomType.Elite, encounter);
        Rnfmabj resumedBody = Guest<Rnfmabj>(second)
            ?? throw new InvalidOperationException("Rnfmabj is missing after the load.");
        string restored = DescribeDirectivePlayers(resumedBody);
        Check(restored == expected,
            label + ": directive players/progress after the load differ: saved" + expected + " loaded" + restored);

        // 续战：一号玩家打完本任务后再打一张错牌（已完成者锁定，不归零）；二号玩家打完后两人都完成，取消最左边的意图。
        IReadOnlyList<Player> resumedPlayers = RunPlayers();
        await PlayRounds(label + "-resume", second, 3, async (round, state) =>
        {
            if (round != 1)
            {
                return null;
            }

            Player resumedOne = resumedPlayers[0];
            Player resumedTwo = resumedPlayers[1];
            await PlayDirectiveCard(label + "-resume", state, resumedOne, correct: true);
            await PlayDirectiveCard(label + "-resume", state, resumedOne, correct: true);
            await PlayDirectiveCard(label + "-resume", state, resumedOne, correct: false);
            await PlayDirectiveCard(label + "-resume", state, resumedTwo, correct: true);
            await PlayDirectiveCard(label + "-resume", state, resumedTwo, correct: true);
            await PlayDirectiveCard(label + "-resume", state, resumedTwo, correct: true);
            return "directive plays";
        });
    }

    /// <summary>
    /// 把存档写回怪物。原版 <c>SavedProperties.Fill</c> 用 <c>GetType().GetProperty</c> 找属性，基类里 <c>private set</c>
    /// 的属性在派生类型上拿不到设置器，会抛 “Property set method not found”（原版从不对怪物调用 Fill，所以平时碰不到）。
    /// 这里按声明类型逐级找设置器，其余与原版一致；用到回退的属性名写成普通日志（不进 TRACE：拆分前后声明位置可能不同）。
    /// </summary>
    private static void FillSavedState(SavedProperties saved, object model, string slot)
    {
        var viaDeclaringType = new List<string>();
        void Set(string name, Func<Type, object?> value)
        {
            PropertyInfo? property = model.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                return;
            }

            MethodInfo? setter = property.SetMethod;
            if (setter == null)
            {
                setter = property.DeclaringType?.GetProperty(
                        name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?.SetMethod;
                viaDeclaringType.Add(property.DeclaringType?.Name + "." + name);
            }

            if (setter == null)
            {
                throw new InvalidOperationException("No setter for saved property " + name + ".");
            }

            setter.Invoke(model, [value(property.PropertyType)]);
        }

        foreach (SavedProperties.SavedProperty<int> entry in saved.ints ?? [])
        {
            Set(entry.name, type => type.IsEnum ? Enum.ToObject(type, entry.value) : entry.value);
        }

        foreach (SavedProperties.SavedProperty<int[]> entry in saved.intArrays ?? [])
        {
            Set(entry.name, type =>
            {
                Type element = type.GetElementType()!;
                if (!element.IsEnum)
                {
                    return entry.value;
                }

                Array array = Array.CreateInstance(element, entry.value.Length);
                for (int index = 0; index < entry.value.Length; index++)
                {
                    array.SetValue(Enum.ToObject(element, entry.value[index]), index);
                }

                return array;
            });
        }

        foreach (SavedProperties.SavedProperty<bool> entry in saved.bools ?? [])
        {
            Set(entry.name, _ => entry.value);
        }

        foreach (SavedProperties.SavedProperty<string> entry in saved.strings ?? [])
        {
            Set(entry.name, _ => entry.value);
        }

        foreach (SavedProperties.SavedProperty<ModelId> entry in saved.modelIds ?? [])
        {
            Set(entry.name, _ => entry.value);
        }

        if (saved.cards is { Count: > 0 } || saved.cardArrays is { Count: > 0 })
        {
            throw new InvalidOperationException("Card saved properties are not expected on " + slot + ".");
        }

        if (viaDeclaringType.Count > 0)
        {
            Log.Info(LogPrefix + "fill " + slot + " via declaring type: " + string.Join(",", viaDeclaringType));
        }
    }

    /// <summary>
    /// 让 <paramref name="player"/> 打出一张牌：按 Rnfmabj 对该玩家当前要求的类型取一张（<paramref name="correct"/> 为假时取
    /// 另一种类型），经 <c>Hook.AfterCardPlayed</c> 派发给所有监听者。起始牌组没有能力牌，要求能力牌时就抛错。
    /// 不走出牌动作队列：牌不离开牌堆、不结算效果，轨迹里只有指令相关的变化。
    /// </summary>
    private static async Task PlayDirectiveCard(string label, CombatState state, Player player, bool correct)
    {
        Rnfmabj body = Guest<Rnfmabj>(state)
            ?? throw new InvalidOperationException("Rnfmabj is missing.");
        RnfmabjDirectiveSnapshot snapshot = body.GetDirectiveSnapshot(player.NetId);
        if (!snapshot.IsVisible || snapshot.Sequence.Length == 0)
        {
            throw new InvalidOperationException("No directive is visible for " + PlayerLabel(player.NetId) + ".");
        }

        CardType required = snapshot.Sequence[Math.Min(snapshot.Progress, snapshot.Sequence.Length - 1)];
        CardType type = correct
            ? required
            : required == CardType.Attack ? CardType.Skill : CardType.Attack;
        CardModel card = CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard)
                .FirstOrDefault(candidate => candidate.Type == type)
            ?? throw new InvalidOperationException(PlayerLabel(player.NetId) + " has no " + type + " card.");
        await Hook.AfterCardPlayed(
            state,
            new ThrowingPlayerChoiceContext(),
            new CardPlay
            {
                Card = card,
                Player = player,
                Target = null,
                ResultPile = PileType.Discard,
                Resources = new ResourceInfo
                {
                    EnergySpent = 0,
                    EnergyValue = 0,
                    StarsSpent = 0,
                    StarValue = 0,
                },
                IsAutoPlay = false,
                PlayIndex = 0,
                PlayCount = 1,
            });
        await WaitFrames(4);
        Trace(label, "r" + state.RoundNumber + " " + PlayerLabel(player.NetId) + " plays " + type
            + (correct ? string.Empty : " (wrong)"), state);
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
    }

    private static async Task EndTurnAndWait(string label, CombatState state, int round)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        int startRound = state.RoundNumber;
        TraceLine(label, "r" + round + " end turn", "round=" + startRound);
        foreach (Player player in state.Players)
        {
            PlayerCmd.EndTurn(player, canBackOut: false);
        }

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
            TraceLine(label, "r" + round + " combat ended", Summary(state));
        }
    }

    private static async Task StartRun(string seed, int playerCount = 1)
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
            // 与社会层、哲学层套件相同的假联机：几名本地玩家（网络 ID 1、2…）的 RunState 直接交给 NGame.StartRun。
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
            MethodInfo startRun = typeof(NGame).GetMethod("StartRun", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("NGame.StartRun was unavailable for fake multiplayer.");
            await (startRun.Invoke(game, [runState]) as Task
                ?? throw new InvalidOperationException("NGame.StartRun did not return a Task."));
        }

        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
    }

    private static async Task<CombatState> EnterCombat(string label, RoomType roomType, EncounterModel encounter)
    {
        _label = label;
        _tracing = true;
        await RunManager.Instance.EnterRoomDebug(
            roomType,
            MapPointType.Unassigned,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() is
                {
                    CurrentSide: CombatSide.Player
                } state
                && state.Players.All(static player =>
                    player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
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

        foreach (Creature enemy in state.Enemies)
        {
            if (enemy.Monster?.MoveStateMachine is { } machine)
            {
                TraceLine(label, "machine " + enemy.Monster.GetType().Name, DescribeMachine(machine));
            }
        }

        return state;
    }

    // ---- trace ----------------------------------------------------------------------------------------------

    private static void Trace(string label, string step, CombatState state)
    {
        TraceLine(label, step, Summary(state));
        foreach (Creature enemy in state.Enemies)
        {
            if (enemy.Monster is { } monster)
            {
                TraceLine(label, step + "|" + monster.GetType().Name + "@" + enemy.SlotName, DescribeMonster(monster, state));
            }
        }
    }

    private static void TraceLine(string label, string step, string payload) =>
        Log.Info(LogPrefix + "TRACE|" + label + "|" + step + "|" + payload);

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
            enemy.Monster?.GetType().Name + ":" + DescribeCreature(enemy))));
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

    private static string DescribeMonster(MonsterModel monster, CombatState state)
    {
        var line = new StringBuilder();
        MoveState next = monster.NextMove;
        line.Append("next=").Append(next.Id)
            .Append(" once=").Append(next.MustPerformOnceBeforeTransitioning)
            .Append(" canLeave=").Append(next.CanTransitionAway)
            .Append(" intents=[").Append(DescribeIntents(next.Intents, monster, state)).Append(']');
        if (monster is SpecialGuestMonsterBase guest)
        {
            line.Append(" slots=").Append(string.Join(",", guest.PlannedMoveOne, guest.PlannedMoveTwo,
                    guest.PlannedMoveThree, guest.PlannedMoveFour, guest.PlannedMoveFive))
                .Append(" cap=").Append(guest.IntentCapacity)
                .Append(" emo=").Append(guest.EmotionLevel).Append('/').Append(guest.EmotionUnits)
                .Append(" lv5=").Append(guest.LevelFiveRoundCounter)
                .Append(" dealt=").Append(guest.UnblockedDamageDealtThisRound)
                .Append(" pattern=").Append(guest.PatternIndex)
                .Append(" first=").Append(guest.HasCompletedFirstTurn);
        }

        switch (monster)
        {
            case IoriMonsterBase iori:
                line.Append(" stance=").Append(iori.CurrentStance)
                    .Append(" lastRound=").Append(iori.LastPlannedRound)
                    .Append(" lastMove=").Append(iori.LastRegularMove)
                    .Append(" nextStance=").Append(iori.PlannedNextStance)
                    .Append(" mask=").Append(iori.SelectedStanceMask)
                    .Append(" r2cap=").Append(iori.HasExpandedRoundTwoCapacity)
                    .Append(" offset=").Append(iori.ReceptionRoundOffset)
                    .Append(" escQ=").Append(iori.EscapeQueued)
                    .Append(" escDone=").Append(iori.EscapeCompleted)
                    .Append(" restored=").Append(iori.StageSnapshotRestored)
                    .Append(" chains=").Append(iori.ChainsContributionByPlayerNetId)
                    .Append(" lock=").Append(iori.IsHealthBarLockActive);
                break;
            case Rnfmabj body:
                AppendRnfmabjBase(line, body);
                RnfmabjDirectiveSnapshot directive = body.GetDirectiveSnapshot(null);
                line.Append(" phase=").Append(body.Phase)
                    .Append(" united=").Append(body.IsUnited)
                    .Append(" p3=").Append(body.PhaseThreeInitialized)
                    .Append(" blade=").Append(body.BladeCooldown)
                    .Append(" activated=").Append(body.LastActivatedPlanSerial)
                    .Append(" targets=").Append(string.Join(",", body.PlannedTargetOne, body.PlannedTargetTwo,
                        body.PlannedTargetThree, body.PlannedTargetFour, body.PlannedTargetFive))
                    .Append(" dirSerial=").Append(body.DirectivePlanSerial)
                    .Append(" dirLen=").Append(body.DirectiveSequenceLength)
                    .Append(" dirCodes=").Append(string.Join(",", body.DirectiveSequenceCodes))
                    .Append(" dirTask=").Append(body.CurrentDirectiveTaskIndex)
                    .Append(" dirDone=").Append(body.CurrentDirectiveCompleted)
                    .Append(" dirPlayers=").Append(body.DirectiveRequiredPlayerNetIds.Length > 0 ? "set" : "empty")
                    .Append(" dirProgress=").Append(body.DirectiveProgressByPlayerNetId.Length)
                    .Append(" snap={").Append(directive.IsVisible)
                    .Append(',').Append(string.Join("+", directive.Sequence))
                    .Append(',').Append(directive.Progress)
                    .Append(',').Append(directive.TaskNumber).Append('/').Append(directive.TaskCount)
                    .Append(',').Append(directive.CompletedPlayers).Append('/').Append(directive.RequiredPlayers)
                    .Append(',').Append(directive.LocalPlayerCompleted)
                    .Append(',').Append(directive.CurrentTaskCompleted).Append('}')
                    .Append(DescribeDirectivePlayers(body));
                break;
            case RnfmabjHandBase hand:
                AppendRnfmabjBase(line, hand);
                line.Append(" fake=").Append(hand.IsFakeDead)
                    .Append(" p3step=").Append(hand.PhaseThreePatternStep);
                break;
            case XiaoStageOne xiao:
                line.Append(" fake=").Append(xiao.IsFakeDead)
                    .Append(" trueDeath=").Append(xiao.ForceTrueDeath);
                break;
            case XiaoEgo ego:
                line.Append(" hadResult=").Append(ego.HadAnyAttackResultThisEnemyTurn)
                    .Append(" allBlocked=").Append(ego.AllAttackResultsFullyBlocked);
                break;
            case Kali kali:
                line.Append(" egoTrig=").Append(kali.EgoTriggered)
                    .Append(" ego=").Append(kali.EgoActive)
                    .Append(" pending=").Append(kali.EgoManifestationPending)
                    .Append(" return=").Append(kali.EgoReturnCountdown)
                    .Append(" mist=").Append(kali.PersistedBloodMistStacks)
                    .Append(" planNo=").Append(kali.PersistedEnemyCardPlanNumber)
                    .Append(" queued=").Append(kali.PersistedQueuedExtraCardIds.Replace('\n', '+'))
                    .Append(" lockUsed=").Append(kali.EgoThresholdTurnLockConsumed)
                    .Append(" lockAt=").Append(kali.EgoThresholdTurnLockRound).Append('/').Append(kali.EgoThresholdTurnLockSide)
                    .Append(" cards=").Append(kali.EnemyCards == null
                        ? "<none>"
                        : string.Join("+", kali.EnemyCards.CurrentPlan.Select(static spec => spec.Id))
                          + "@" + kali.EnemyCards.CurrentPlanIndex);
                break;
        }

        return line.ToString();
    }

    /// <summary>
    /// 指令的参与者、逐人进度与逐人快照，玩家都写成 RunState.Players 里的序号（P1、P2…）：单人局的网络 ID 跨进程不固定，
    /// 序号才能逐行对照。参与者与进度按存档字符串原样解码（顺序即编码顺序），认不出的玩家写 P?，解析不了的条目写 !。
    /// 快照对每名玩家各取一次 GetDirectiveSnapshot(网络 ID)。
    /// </summary>
    private static string DescribeDirectivePlayers(Rnfmabj body)
    {
        var line = new StringBuilder();
        line.Append(" dirParticipants=[").Append(string.Join(",", SplitEntries(body.DirectiveRequiredPlayerNetIds)
                .Select(static entry => ulong.TryParse(entry, NumberStyles.None, CultureInfo.InvariantCulture, out ulong netId)
                    ? PlayerLabel(netId)
                    : "!")))
            .Append("] dirProgressBy=[").Append(string.Join(",", SplitEntries(body.DirectiveProgressByPlayerNetId)
                .Select(static entry =>
                {
                    int separator = entry.IndexOf(':');
                    return separator > 0
                           && ulong.TryParse(entry[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out ulong netId)
                        ? PlayerLabel(netId) + ":" + entry[(separator + 1)..]
                        : "!";
                })))
            .Append("] snapBy=[");
        line.Append(string.Join(";", RunPlayers().Select(player =>
        {
            RnfmabjDirectiveSnapshot snap = body.GetDirectiveSnapshot(player.NetId);
            return PlayerLabel(player.NetId) + "{" + snap.IsVisible
                + "," + string.Join("+", snap.Sequence)
                + "," + snap.Progress
                + "," + snap.TaskNumber + "/" + snap.TaskCount
                + "," + snap.CompletedPlayers + "/" + snap.RequiredPlayers
                + "," + snap.LocalPlayerCompleted
                + "," + snap.CurrentTaskCompleted + "}";
        })));
        return line.Append(']').ToString();
    }

    private static string[] SplitEntries(string serialized) =>
        serialized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<Player> RunPlayers() =>
        RunManager.Instance.DebugOnlyGetState()?.Players ?? [];

    private static string PlayerLabel(ulong netId)
    {
        IReadOnlyList<Player> players = RunPlayers();
        for (int index = 0; index < players.Count; index++)
        {
            if (players[index].NetId == netId)
            {
                return "P" + (index + 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        return "P?";
    }

    private static void AppendRnfmabjBase(StringBuilder line, RnfmabjMonsterBase monster)
    {
        line.Append(" lastRound=").Append(monster.LastPlannedRound)
            .Append(" serial=").Append(monster.PlanSerial)
            .Append(" dmg=").Append(string.Join(",", monster.PlannedDamageValues));
    }

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

        _harmony = new Harmony("LibraryOfRuina.Verification.SpecialGuestPlanTrace");
        _harmony.Patch(
            AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.SetMoveImmediate)),
            postfix: new HarmonyMethod(typeof(SpecialGuestPlanTraceVerificationPatch), nameof(AfterSetMoveImmediate)));
        _harmony.Patch(
            AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.PerformMove)),
            prefix: new HarmonyMethod(typeof(SpecialGuestPlanTraceVerificationPatch), nameof(BeforePerformMove)));
        _harmony.Patch(
            AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim)),
            prefix: new HarmonyMethod(typeof(SpecialGuestPlanTraceVerificationPatch), nameof(BeforeTriggerAnim)));
        _harmony.Patch(
            AccessTools.Method(
                typeof(CreatureCmd),
                nameof(CreatureCmd.GainBlock),
                [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(SpecialGuestPlanTraceVerificationPatch), nameof(BeforeGainBlock)));
    }

    private static bool IsTraced(MonsterModel? monster) =>
        _tracing && monster is SpecialGuestMonsterBase;

    private static void AfterSetMoveImmediate(MonsterModel __instance, MoveState state, bool forceTransition)
    {
        if (IsTraced(__instance))
        {
            TraceLine(_label, "exec", "set " + __instance.GetType().Name + " " + state.Id
                + " force=" + forceTransition + " next=" + __instance.NextMove.Id);
        }
    }

    private static void BeforePerformMove(MonsterModel __instance)
    {
        if (IsTraced(__instance))
        {
            TraceLine(_label, "exec", "perform " + __instance.GetType().Name + " " + __instance.NextMove.Id
                + " slots=" + (__instance is SpecialGuestMonsterBase guest
                    ? string.Join(",", guest.PlannedMoveOne, guest.PlannedMoveTwo, guest.PlannedMoveThree,
                        guest.PlannedMoveFour, guest.PlannedMoveFive)
                    : string.Empty));
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

    private static T? Guest<T>(CombatState state) where T : MonsterModel =>
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

    /// <summary>
    /// 等战斗自然结束（撤离、假死收尾）；最多等 30 秒，结不结束都记一行再清理跑图，两次对照在同一处取样。
    /// </summary>
    private static async Task EndRunAndWait(string description)
    {
        bool ended = await TryWaitUntil(
            static () => !CombatManager.Instance.IsInProgress,
            TimeSpan.FromSeconds(30));
        TraceLine(_label, "run end", description + " combatEnded=" + ended);
        _tracing = false;
        CleanupRun();
        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() == null
                && RunManager.Instance.DebugOnlyGetState() == null,
            description + " cleanup");
    }

    /// <summary>战斗中途直接清掉跑图（相当于存档后退出），不等战斗结束。</summary>
    private static async Task AbandonRun(string description)
    {
        TraceLine(_label, "run abandoned", description);
        _tracing = false;
        CleanupRun();
        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() == null
                && RunManager.Instance.DebugOnlyGetState() == null,
            description + " cleanup");
    }
}
