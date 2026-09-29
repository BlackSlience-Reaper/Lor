using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ActLikeIt2;
using Godot;
using HarmonyLib;
using LibraryOfRuina;
using LibraryOfRuina.acts;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.encounters.PhilosophyFloorLiberation;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 遭遇权重与解放楼层 Boss 路由的逐行转储，用于重构前后对照（A/B）。
/// 静态部分：各幕的固定 Boss 与族别、登记开关组合下的查询、遭遇池分类、复制份数与抽取权重。
/// 状态矩阵：幕布局 × 登记与怪物扩展开关 × 进阶 × 人数 × 种子，每个状态真实建局（触发原版
/// <c>GenerateRooms</c> 与本模组的补丁），再依次重放运行期替换、循环规则（含历史里已出现的遭遇与已打过的 Boss）、
/// 读档、生成地图前、ActLikeIt2 岔路重建、整局 Boss 重排与恢复原版遭遇。
/// 每一步记录房间序列、Boss 与第二 Boss、共享 UpFront 流的计数，以及本模组按标签新建的局部随机源和它们各自的抽取次数。
/// 只经过重构前后都存在的入口：补丁类按名字反射调用，私有的份数与权重方法按名字反射。
/// </summary>
internal static class LiberationFloorRoutingVerificationPatch
{
    private const string VerifyArg = "lor-verify-liberation-floor-routing";
    private const string LogPrefix = "[LibraryOfRuina.LiberationFloorRouting.Verify] ";

    private static readonly string[] Seeds = ["LORFLOORA", "LORFLOORB"];
    private static readonly int[] AscensionLevels = [0, 10];
    private static readonly int[] PlayerCounts = [1, 2];

    private static readonly (string Name, Func<ActModel>[] Acts)[] Layouts =
    [
        ("L1", [ModelDb.Act<Malkuth>, ModelDb.Act<NetZech>, ModelDb.Act<Chesed>]),
        ("L2", [ModelDb.Act<Yesod>, ModelDb.Act<Gebura>, ModelDb.Act<Binah>]),
        ("L3", [ModelDb.Act<Hod>, ModelDb.Act<Tiphereth>, ModelDb.Act<Binah>]),
        ("L4", [ModelDb.Act<Underdocks>, ModelDb.Act<Hive>, ModelDb.Act<Glory>]),
        // 图书馆幕放在不属于它的幕序号上：不是解放 Boss 目标，走普通 Boss 重排。
        ("L5", [ModelDb.Act<Chesed>, ModelDb.Act<Malkuth>, ModelDb.Act<NetZech>]),
        // 四幕：原版 A10 第二 Boss 落在最后一幕，第三幕的固定双 Boss 由本模组给。
        ("L6", [ModelDb.Act<Malkuth>, ModelDb.Act<Tiphereth>, ModelDb.Act<Chesed>, ModelDb.Act<ReverberationEnsembleAct>])
    ];

    private enum RegistryConfig
    {
        AllOn,
        AllOff,
        OnlyNatural,
        PhilosophyOff,
        SocialOff
    }

    private static readonly (string Name, bool Extension, RegistryConfig Registry)[] RunConfigs =
    [
        ("C1", true, RegistryConfig.AllOn),
        ("C2", true, RegistryConfig.AllOff),
        ("C3", true, RegistryConfig.OnlyNatural),
        ("C4", true, RegistryConfig.PhilosophyOff),
        ("C5", false, RegistryConfig.AllOn)
    ];

    private static bool _started;
    private static int _rows;
    private static readonly StringBuilder Digest = new();

    private static bool _capturing;
    private static readonly List<(string Label, Rng Rng)> CapturedRngs = new();

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
        bool[] previousRegistry = ReadRegistry();
        bool previousExtension = LibraryOfRuinaSettings.MonsterExtensionEnabled;
        try
        {
            InstallRngCapture();
            RecordActs();
            RecordRegistry();
            RecordPools();
            await RecordRunMatrix();
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Digest.ToString())))[..16];
            Log.Info(LogPrefix + "LIBERATION_FLOOR_ROUTING_OK rows=" + _rows + " digest=" + digest);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LIBERATION_FLOOR_ROUTING_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
        finally
        {
            WriteRegistry(previousRegistry);
            SetMonsterExtension(previousExtension);
        }
    }

    private static void Row(string text)
    {
        _rows++;
        Digest.Append(text).Append('\n');
        Log.Info(LogPrefix + "ROW " + text);
    }

    // 本模组的局部随机源都用 Rng(seed, label) 新建；记下标签与最终计数就是逐源的抽取次数。
    private static void InstallRngCapture()
    {
        ConstructorInfo ctor = AccessTools.Constructor(typeof(Rng), [typeof(ulong), typeof(string)])
            ?? throw new InvalidOperationException("Rng(ulong, string) was unavailable.");
        new Harmony("LibraryOfRuina.Verification.LiberationFloorRouting").Patch(
            ctor,
            postfix: new HarmonyMethod(typeof(LiberationFloorRoutingVerificationPatch), nameof(RngCtorPostfix)));
    }

    private static void RngCtorPostfix(Rng __instance, string name)
    {
        if (_capturing
            && name != null
            && (name.StartsWith("library_encounter", StringComparison.Ordinal)
                || name.StartsWith("liberation_", StringComparison.Ordinal)))
        {
            CapturedRngs.Add((name, __instance));
        }
    }

    private static string Capture(Action action)
    {
        CapturedRngs.Clear();
        string error = "";
        _capturing = true;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            error = " error=" + ex.GetType().Name + ":" + ex.Message.Replace('\n', ' ');
        }
        finally
        {
            _capturing = false;
        }

        string rngs = string.Join(";", CapturedRngs.Select(static captured =>
            captured.Label + "#" + captured.Rng.ToSerializable().counter));
        CapturedRngs.Clear();
        return "rngs=[" + rngs + "]" + error;
    }

    private static int Counter(Rng rng) => rng.ToSerializable().counter;

    private static string Id(EncounterModel? encounter) => encounter?.Id.Entry ?? "none";

    private static string Ids(IEnumerable<EncounterModel> encounters) =>
        string.Join(",", encounters.Select(static encounter => encounter.Id.Entry));

    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            return "!" + ex.GetType().Name;
        }
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static IEnumerable<ActModel> CanonicalActs() =>
        LibraryActCatalog.All().Cast<ActModel>()
            .Append(ModelDb.Act<ReverberationEnsembleAct>())
            .Append(ModelDb.Act<Overgrowth>())
            .Append(ModelDb.Act<Underdocks>())
            .Append(ModelDb.Act<Hive>())
            .Append(ModelDb.Act<Glory>());

    private static void RecordActs()
    {
        foreach (ActModel act in CanonicalActs())
        {
            string expected = act is LibraryOfRuinaActModel library ? Id(library.ExpectedBoss) : "-";
            Row("act " + act.Id.Entry
                + " type=" + act.GetType().Name
                + " library=" + LibraryOfRuinaActModel.IsLibraryAct(act)
                + " families=" + LibraryOfRuinaActModel.IsFirstFamily(act)
                + "/" + LibraryOfRuinaActModel.IsSecondFamily(act)
                + "/" + LibraryOfRuinaActModel.IsThirdFamily(act)
                + " expectedBoss=" + expected
                + " bossPool=[" + Safe(() => Ids(act.AllBossEncounters)) + "]");
            for (int actIndex = 0; actIndex < 4; actIndex++)
            {
                int index = actIndex;
                Row("chooseLiberation " + act.Id.Entry + " index=" + index + " -> "
                    + Safe(() => Id(LibraryEncounterWeighting.ChooseLiberationEncounter(act, index))));
            }
        }

        foreach (int actIndex in new[] { 0, 1, 2, 3 })
        {
            foreach (int ascension in new[] { 0, 9, 10, 20 })
            {
                Row("doubleBossRule index=" + actIndex + " asc=" + ascension + " -> "
                    + LibraryEncounterWeighting.ShouldApplyThirdActDoubleBossRule(actIndex, ascension));
            }
        }
    }

    private static readonly Type[] LiberationTypes =
    [
        typeof(TechnologyFloorLiberationEncounter),
        typeof(HistoryFloorLiberationEncounter),
        typeof(LiteratureFloorLiberationEncounter),
        typeof(ArtFloorLiberationEncounter),
        typeof(LanguageFloorLiberationEncounter),
        typeof(PhilosophyFloorLiberationEncounter),
        typeof(SocialFloorLiberationEncounter),
        typeof(NaturalFloorLiberationEncounter)
    ];

    private static bool[] ReadRegistry() =>
    [
        LiberationBossRegistry.RegisterTechnologyFloorLiberation,
        LiberationBossRegistry.RegisterHistoryFloorLiberation,
        LiberationBossRegistry.RegisterLiteratureFloorLiberation,
        LiberationBossRegistry.RegisterArtFloorLiberation,
        LiberationBossRegistry.RegisterLanguageFloorLiberation,
        LiberationBossRegistry.RegisterPhilosophyFloorLiberation,
        LiberationBossRegistry.RegisterSocialFloorLiberation,
        LiberationBossRegistry.RegisterNaturalFloorLiberation
    ];

    private static void WriteRegistry(bool[] flags)
    {
        LiberationBossRegistry.RegisterTechnologyFloorLiberation = flags[0];
        LiberationBossRegistry.RegisterHistoryFloorLiberation = flags[1];
        LiberationBossRegistry.RegisterLiteratureFloorLiberation = flags[2];
        LiberationBossRegistry.RegisterArtFloorLiberation = flags[3];
        LiberationBossRegistry.RegisterLanguageFloorLiberation = flags[4];
        LiberationBossRegistry.RegisterPhilosophyFloorLiberation = flags[5];
        LiberationBossRegistry.RegisterSocialFloorLiberation = flags[6];
        LiberationBossRegistry.RegisterNaturalFloorLiberation = flags[7];
    }

    private static bool[] FlagsFor(RegistryConfig config)
    {
        bool[] flags = Enumerable.Repeat(config != RegistryConfig.AllOff && config != RegistryConfig.OnlyNatural, 8).ToArray();
        switch (config)
        {
            case RegistryConfig.OnlyNatural:
                flags[7] = true;
                break;
            case RegistryConfig.PhilosophyOff:
                flags[5] = false;
                break;
            case RegistryConfig.SocialOff:
                flags[6] = false;
                break;
        }

        return flags;
    }

    private static IEnumerable<(string Name, bool[] Flags)> RegistryMatrix()
    {
        foreach (RegistryConfig config in Enum.GetValues<RegistryConfig>())
        {
            yield return (config.ToString(), FlagsFor(config));
        }

        for (int i = 0; i < 8; i++)
        {
            bool[] single = new bool[8];
            single[i] = true;
            yield return ("Only" + LiberationTypes[i].Name, single);
        }
    }

    private static void RecordRegistry()
    {
        List<EncounterModel> encounters = ModelDb.AllEncounters
            .OrderBy(static encounter => encounter.Id.Entry, StringComparer.Ordinal)
            .ToList();
        foreach (EncounterModel encounter in encounters)
        {
            Row("isLiberation " + encounter.Id.Entry + " -> " + LiberationBossRegistry.IsLiberationEncounter(encounter));
        }

        List<EncounterModel> bosses = encounters
            .Where(static encounter => encounter.RoomType == RoomType.Boss)
            .ToList();
        foreach ((string name, bool[] flags) in RegistryMatrix())
        {
            WriteRegistry(flags);
            Row("registry " + name
                + " flags=" + string.Join("", flags.Select(static flag => flag ? '1' : '0'))
                + " any=" + LiberationBossRegistry.AnyLiberationRegistered
                + " generic=" + string.Join("",
                    new[]
                    {
                        LiberationBossRegistry.IsEncounterRegistered<TechnologyFloorLiberationEncounter>(),
                        LiberationBossRegistry.IsEncounterRegistered<HistoryFloorLiberationEncounter>(),
                        LiberationBossRegistry.IsEncounterRegistered<LiteratureFloorLiberationEncounter>(),
                        LiberationBossRegistry.IsEncounterRegistered<ArtFloorLiberationEncounter>(),
                        LiberationBossRegistry.IsEncounterRegistered<LanguageFloorLiberationEncounter>(),
                        LiberationBossRegistry.IsEncounterRegistered<PhilosophyFloorLiberationEncounter>(),
                        LiberationBossRegistry.IsEncounterRegistered<SocialFloorLiberationEncounter>(),
                        LiberationBossRegistry.IsEncounterRegistered<NaturalFloorLiberationEncounter>()
                    }.Select(static flag => flag ? '1' : '0'))
                + " registered=[" + string.Join(",", bosses
                    .Where(LiberationBossRegistry.IsEncounterRegistered)
                    .Select(static encounter => encounter.Id.Entry)) + "]");
            for (ulong seed = 0; seed < 8; seed++)
            {
                ulong current = seed;
                string captured = Capture(() => Row("thirdActPair " + name + " seed=" + current + " -> "
                    + Ids(LibraryEncounterWeighting.ChooseThirdActDoubleBossSelection(current, "verify_liberation_floor_pair"))));
                Row("thirdActPairRng " + name + " seed=" + current + " " + captured);
            }
        }
    }

    private static readonly MethodInfo PoolCopiesFor = RequireMethod(typeof(LibraryEncounterWeighting), "PoolCopiesFor");
    private static readonly MethodInfo SelectionWeightFor = RequireMethod(typeof(LibraryEncounterWeighting), "SelectionWeightFor");

    private static MethodInfo RequireMethod(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new InvalidOperationException(type.Name + "." + name + " was unavailable.");

    private static void RecordPools()
    {
        foreach (ActModel act in CanonicalActs())
        {
            foreach ((string pool, IEnumerable<EncounterModel> encounters) in new (string, IEnumerable<EncounterModel>)[]
                     {
                         ("weak", act.AllWeakEncounters),
                         ("regular", act.AllRegularEncounters),
                         ("elite", act.AllEliteEncounters),
                         ("boss", act.AllBossEncounters)
                     })
            {
                List<EncounterModel> list = encounters.ToList();
                foreach (IGrouping<string, EncounterModel> group in list
                             .GroupBy(static encounter => encounter.Id.Entry)
                             .OrderBy(static group => group.Key, StringComparer.Ordinal))
                {
                    EncounterModel encounter = group.First();
                    Row("pool " + act.Id.Entry + " " + pool + " " + group.Key
                        + " copies=" + group.Count()
                        + " copiesFor=" + PoolCopiesFor.Invoke(null, [encounter])
                        + " priority=" + LibraryEncounterWeighting.IsPriorityEncounter(encounter)
                        + " standard=" + LibraryEncounterWeighting.IsStandardModEncounter(encounter)
                        + " mod=" + LibraryEncounterWeighting.IsModEncounter(encounter)
                        + " normalCandidate=" + LibraryEncounterWeighting.IsNormalEncounterPoolCandidate(encounter)
                        + " weight=" + Safe(() => Num((double)SelectionWeightFor.Invoke(null, [encounter, act])!))
                        + " weightNoAct=" + Safe(() => Num((double)SelectionWeightFor.Invoke(null, [encounter, null])!)));
                }
            }
        }
    }

    private static void SetMonsterExtension(bool enabled)
    {
        // 设置的 setter 会触发 BGM 副作用并在局中拒绝修改；这里只改后备字段。
        FieldInfo field = typeof(LibraryOfRuinaSettings).GetField(
                "_monsterExtensionEnabled",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_monsterExtensionEnabled was unavailable.");
        field.SetValue(null, enabled);
    }

    private static async Task RecordRunMatrix()
    {
        foreach ((string layoutName, Func<ActModel>[] layout) in Layouts)
        {
            foreach ((string configName, bool extension, RegistryConfig registry) in RunConfigs)
            {
                foreach (int ascension in AscensionLevels)
                {
                    foreach (int playerCount in PlayerCounts)
                    {
                        foreach (string seed in Seeds)
                        {
                            string key = layoutName + " " + configName + " A" + ascension + " P" + playerCount + " " + seed;
                            WriteRegistry(FlagsFor(registry));
                            SetMonsterExtension(extension);
                            try
                            {
                                RecordRun(key, layout, ascension, playerCount, seed);
                            }
                            finally
                            {
                                RunManager.Instance.CleanUp(graceful: true);
                            }

                            await NextFrame();
                        }
                    }
                }
            }
        }
    }

    private static async Task NextFrame()
    {
        SceneTree tree = NGame.Instance?.GetTree()
            ?? throw new InvalidOperationException("SceneTree is unavailable.");
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void RecordRun(string key, Func<ActModel>[] layout, int ascension, int playerCount, string seed)
    {
        Player[] players = Enumerable.Range(0, playerCount)
            .Select(index => Player.CreateForNewRun(
                index == 0 ? ModelDb.Character<Ironclad>() : ModelDb.Character<Silent>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                (ulong)(index + 1)))
            .ToArray();
        RunState state = RunState.CreateForNewRun(
            players,
            layout.Select(static act => act().ToMutable()).ToList(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            ascension,
            seed);

        string setup = Capture(() => RunManager.Instance.SetUpNewSingleplayer(state, shouldSave: false));
        Row(key + " setup upFront=" + Counter(state.Rng.UpFront) + " " + setup);
        RecordState(key, "setup", state);

        RecordScheduledVanilla(key, state);
        RecordRuntimeReplacement(key, state);
        RecordModFirstReplacement(key, state);
        RecordEntries(key, state);
        RecordCycleRule(key, state);
        RecordEntries(key + " afterHistory", state);
        RecordForkRegeneration(key, state);

        string reweight = Capture(() => InvokePatch("LibraryEncounterBossWeightPatch", "Postfix", RunManager.Instance));
        Row(key + " runRooms upFront=" + Counter(state.Rng.UpFront) + " " + reweight);
        RecordState(key, "runRooms", state);

        string force = Capture(() => LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(state));
        Row(key + " forceAll upFront=" + Counter(state.Rng.UpFront) + " " + force);
        RecordState(key, "forceAll", state);

        string restore = Capture(() => LibraryEncounterWeighting.RestoreVanillaEncounters(state));
        Row(key + " restore upFront=" + Counter(state.Rng.UpFront) + " " + restore);
        RecordState(key, "restore", state);
    }

    private static void RecordState(string key, string step, RunState state)
    {
        for (int i = 0; i < state.Acts.Count; i++)
        {
            ActModel act = state.Acts[i];
            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
            Row(key + " " + step + " act" + i + "=" + act.Id.Entry
                + " boss=" + Safe(() => Id(act.BossEncounter))
                + " second=" + Id(act.SecondBossEncounter)
                + " normal=[" + Ids(rooms.normalEncounters) + "]"
                + " elite=[" + Ids(rooms.eliteEncounters) + "]"
                + " visited=" + rooms.normalEncountersVisited + "/" + rooms.eliteEncountersVisited);
        }
    }

    private static ActModel VanillaSource(ActModel act) =>
        act is TemplateActModel template ? template.TemplateAct : ModelDb.GetById<ActModel>(act.Id);

    private static EncounterModel? FirstVanilla(IEnumerable<EncounterModel> pool, int skip = 0) =>
        pool.Where(static encounter => !LibraryEncounterWeighting.IsModEncounter(encounter))
            .DistinctBy(static encounter => encounter.Id)
            .OrderBy(static encounter => encounter.Id.Entry, StringComparer.Ordinal)
            .Skip(skip)
            .FirstOrDefault();

    private static void RecordScheduledVanilla(string key, RunState state)
    {
        for (int i = 0; i < state.Acts.Count; i++)
        {
            ActModel act = state.Acts[i];
            foreach (RoomType roomType in new[] { RoomType.Monster, RoomType.Elite, RoomType.Boss })
            {
                string result = Safe(() =>
                {
                    bool found = LibraryEncounterWeighting.TryGetScheduledVanillaEncounter(act, roomType, out EncounterModel encounter);
                    return found + ":" + Id(encounter);
                });
                Row(key + " scheduledVanilla act" + i + " " + roomType + " -> " + result);
            }
        }
    }

    private static void RecordRuntimeReplacement(string key, RunState state)
    {
        for (int i = 0; i < state.Acts.Count; i++)
        {
            state.CurrentActIndex = i;
            ActModel act = state.Acts[i];
            ActModel source = VanillaSource(act);
            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
            var probes = new (RoomType RoomType, EncounterModel? Current)[]
            {
                (RoomType.Monster, FirstVanilla(source.AllWeakEncounters)),
                (RoomType.Monster, FirstVanilla(source.AllRegularEncounters)),
                (RoomType.Elite, FirstVanilla(source.AllEliteEncounters)),
                (RoomType.Boss, FirstVanilla(source.AllBossEncounters)),
                (RoomType.Monster, rooms.normalEncounters.FirstOrDefault())
            };
            foreach ((RoomType roomType, EncounterModel? current) in probes)
            {
                if (current == null)
                {
                    continue;
                }

                foreach (int visited in new[] { 0, 1, 3, 7 })
                {
                    rooms.normalEncountersVisited = visited;
                    rooms.eliteEncountersVisited = visited;
                    bool replaced = false;
                    EncounterModel replacement = current;
                    string rngs = Capture(() => replaced = LibraryEncounterWeighting.TrySelectRuntimeModReplacement(
                        act,
                        roomType,
                        current,
                        out replacement));
                    Row(key + " runtime act" + i + " " + roomType + " current=" + Id(current) + " visited=" + visited
                        + " -> " + replaced + ":" + Id(replacement) + " " + rngs);
                }
            }

            rooms.normalEncountersVisited = 0;
            rooms.eliteEncountersVisited = 0;
        }

        state.CurrentActIndex = 0;
    }

    private static void RecordModFirstReplacement(string key, RunState state)
    {
        for (int i = 0; i < state.Acts.Count; i++)
        {
            ActModel act = state.Acts[i];
            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
            EncounterModel? excluded = rooms.normalEncounters.FirstOrDefault();
            for (int index = 0; index < Math.Min(4, rooms.normalEncounters.Count); index++)
            {
                int replacementIndex = index;
                string result = Safe(() => Id(LibraryEncounterWeighting.SelectModFirstReplacement(
                    act.AllWeakEncounters.Concat(act.AllRegularEncounters).Concat(VanillaSource(act).AllWeakEncounters),
                    rooms.normalEncounters,
                    replacementIndex,
                    encounter => excluded != null && encounter.Id == excluded.Id)));
                Row(key + " modFirst act" + i + " index=" + replacementIndex + " -> " + result);
            }
        }
    }

    private static void RecordCycleRule(string key, RunState state)
    {
        for (int i = 0; i < state.Acts.Count; i++)
        {
            state.CurrentActIndex = i;
            ActModel act = state.Acts[i];
            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
            List<EncounterModel> normal = rooms.normalEncounters.ToList();
            List<EncounterModel> elite = rooms.eliteEncounters.ToList();
            ActModel source = VanillaSource(act);
            EncounterModel? vanillaWeak = FirstVanilla(source.AllWeakEncounters);

            // 第一次调用前该幕没有历史：规则直接放行，也要记录。
            for (int step = 0; step < Math.Min(8, normal.Count); step++)
            {
                if (step > 0)
                {
                    state.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, normal[step - 1].Id);
                }

                RecordCycle(key, i, step, act, RoomType.Monster, normal[step]);
                RecordCycle(key, i, step, act, RoomType.Monster, normal[0]);
                if (elite.Count > 0)
                {
                    RecordCycle(key, i, step, act, RoomType.Elite, elite[step % elite.Count]);
                    RecordCycle(key, i, step, act, RoomType.Elite, elite[0]);
                }

                if (vanillaWeak != null)
                {
                    RecordCycle(key, i, step, act, RoomType.Monster, vanillaWeak);
                }
            }

            if (elite.Count > 0)
            {
                state.AppendToMapPointHistory(MapPointType.Elite, RoomType.Elite, elite[0].Id);
                RecordCycle(key, i, 100, act, RoomType.Elite, elite[0]);
            }

            // 已经打过本幕 Boss（含第二 Boss）的历史：解放楼层“已出现”。
            state.AppendToMapPointHistory(MapPointType.Boss, RoomType.Boss, act.BossEncounter.Id);
            if (act.SecondBossEncounter != null)
            {
                state.AppendToMapPointHistory(MapPointType.Boss, RoomType.Boss, act.SecondBossEncounter.Id);
            }

            if (normal.Count > 0)
            {
                RecordCycle(key, i, 200, act, RoomType.Monster, normal[0]);
            }
        }

        state.CurrentActIndex = 0;
    }

    private static void RecordCycle(string key, int actIndex, int step, ActModel act, RoomType roomType, EncounterModel current)
    {
        EncounterModel result = current;
        string rngs = Capture(() => result = LibraryEncounterWeighting.ApplyEncounterCycleRule(act, roomType, current));
        Row(key + " cycle act" + actIndex + " step=" + step + " " + roomType + " current=" + Id(current)
            + " -> " + Id(result) + " " + rngs);
    }

    private static void Corrupt(ActModel act, int variant)
    {
        List<EncounterModel> vanillaBosses = ModelDb.Act<Glory>().AllBossEncounters
            .Where(static encounter => !LibraryEncounterWeighting.IsModEncounter(encounter))
            .DistinctBy(static encounter => encounter.Id)
            .OrderBy(static encounter => encounter.Id.Entry, StringComparer.Ordinal)
            .ToList();
        act.SetBossEncounter(vanillaBosses[variant % vanillaBosses.Count]);
        act.SetSecondBossEncounter(variant % 2 == 0 ? null : vanillaBosses[(variant + 1) % vanillaBosses.Count]);
    }

    private static void RecordEntries(string key, RunState state)
    {
        for (int i = 0; i < state.Acts.Count; i++)
        {
            ActModel act = state.Acts[i];
            int index = i;

            Corrupt(act, 0);
            string afterLoad = Capture(() => act.ValidateRoomsAfterLoad(state.Rng.UpFront));
            RecordBoss(key, "afterLoad", i, act, state, afterLoad);

            Corrupt(act, 1);
            state.CurrentActIndex = index;
            string beforeMap = Capture(() => InvokePatch("LibraryEncounterBossBeforeMapPatch", "Prefix", RunManager.Instance));
            RecordBoss(key, "beforeMap", i, act, state, beforeMap);

            Corrupt(act, 1);
            string afterLoadPatch = Capture(() => InvokePatch("LibraryEncounterBossAfterLoadPatch", "Postfix", act));
            RecordBoss(key, "afterLoadPatch", i, act, state, afterLoadPatch);

            Corrupt(act, 0);
            string generated = Capture(() => InvokePatch("LibraryEncounterGenerateRoomsPatch", "Postfix", act, state.Rng.UpFront));
            RecordBoss(key, "generatedPatch", i, act, state, generated);
            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
            Row(key + " generatedPatch act" + i + " normal=[" + Ids(rooms.normalEncounters) + "] elite=[" + Ids(rooms.eliteEncounters) + "]");

            Corrupt(act, 1);
            string direct = Capture(() => LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(act, index));
            RecordBoss(key, "direct", i, act, state, direct);

            // 同一幕放到其他幕序号上的判定。
            int otherIndex = (index + 1) % 3;
            Corrupt(act, 0);
            string misplaced = Capture(() => LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(act, otherIndex));
            RecordBoss(key, "direct@" + otherIndex, i, act, state, misplaced);

            string restored = Capture(() => LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(act, index));
            RecordBoss(key, "direct", i, act, state, restored);
        }

        state.CurrentActIndex = 0;
    }

    private static void RecordBoss(string key, string step, int actIndex, ActModel act, RunState state, string rngs)
    {
        Row(key + " " + step + " act" + actIndex + "=" + act.Id.Entry
            + " boss=" + Safe(() => Id(act.BossEncounter))
            + " second=" + Id(act.SecondBossEncounter)
            + " upFront=" + Counter(state.Rng.UpFront)
            + " " + rngs);
    }

    // ActLikeIt2 在岔路里换幕：新建可变幕替换到原位，再重新生成房间并按自己的规则给第三幕第二 Boss，之后进入地图生成。
    private static void RecordForkRegeneration(string key, RunState state)
    {
        Type util = typeof(ActRegistry).Assembly.GetType("ActLikeIt2.Runtime.RunStateActUtil")
            ?? throw new InvalidOperationException("ActLikeIt2 RunStateActUtil was unavailable.");
        MethodInfo replace = util.GetMethod("ReplaceActAt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RunStateActUtil.ReplaceActAt was unavailable.");
        MethodInfo regenerate = util.GetMethod("RegenerateActRooms", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RunStateActUtil.RegenerateActRooms was unavailable.");

        for (int i = 0; i < state.Acts.Count; i++)
        {
            ActModel fresh = ModelDb.GetById<ActModel>(state.Acts[i].Id).ToMutable();
            replace.Invoke(null, [state, i, fresh]);
            int index = i;
            string regenerated = Capture(() => regenerate.Invoke(null, [state, index]));
            RecordBoss(key, "forkRegenerate", i, state.Acts[i], state, regenerated);
            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(state.Acts[i]);
            Row(key + " forkRegenerate act" + i + " normal=[" + Ids(rooms.normalEncounters) + "] elite=[" + Ids(rooms.eliteEncounters) + "]");

            state.CurrentActIndex = index;
            string beforeMap = Capture(() => InvokePatch("LibraryEncounterBossBeforeMapPatch", "Prefix", RunManager.Instance));
            RecordBoss(key, "forkBeforeMap", i, state.Acts[i], state, beforeMap);
        }

        state.CurrentActIndex = 0;
    }

    private static void InvokePatch(string patchClass, string method, params object?[] args)
    {
        Type type = typeof(LibraryEncounterWeighting).Assembly.GetType("LibraryOfRuina.patches." + patchClass)
            ?? throw new InvalidOperationException(patchClass + " was unavailable.");
        MethodInfo info = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(patchClass + "." + method + " was unavailable.");
        try
        {
            info.Invoke(null, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}
