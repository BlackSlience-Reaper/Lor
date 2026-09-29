using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.encounters.PhilosophyFloorLiberation;
using LibraryOfRuina.encounters.RoadHome;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.encounters.WrathServant;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 遭遇自定义状态的存档格式护栏：8 个楼层解放遭遇与归途精英、愤怒仆从。
/// 对每个遭遇构造一组输入（空字典即旧档缺键、逐键逐值、布尔组合、固定种子的随机组合，包括越界、带空白、不能解析的值），
/// 新建遭遇执行 <c>LoadCustomState</c> → <c>SaveCustomState</c>，再把写出的字典交给另一个新建遭遇读一次、写一次，
/// 记录两次写出的字典（按枚举顺序，即存档 JSON 与联机包的写入顺序）和读档后遭遇自身的简单字段。
/// 社会层原版只在战斗结束后保存，此时已经生成怪物，所以它在保存前先 <c>GenerateMonstersWithSlots</c>。
/// <para>
/// 全部用例在当前区域性与 sv-SE（负号是 U+2212）下各跑一遍：按当前区域性读写的楼层在 sv-SE 下写出的负数会变，
/// 按不变区域性读写的楼层不变，两套写法混用会改变摘要。
/// </para>
/// 每个遭遇每一轮的用例汇总成一个摘要，与 <see cref="GoldenDigests"/> 比对；摘要取自重构前（main fb3ab28）的实现，
/// 所以通过即表示存读档与重构前逐字节一致。逐用例的摘要也写进日志，出现差异时可以和旧实现的日志逐行对照。
/// </summary>
internal static class LiberationEncounterStateVerificationPatch
{
    private const string VerifyArg = "lor-verify-liberation-encounter-state";
    private const string LogPrefix = "[LibraryOfRuina.LiberationEncounterState.Verify] ";
    private const int RandomCasesPerEncounter = 120;

    /// <summary>
    /// 键为“区域性轮次/遭遇”。历史层与技术层的键、钳制上限与缺省值都相同，摘要相同；
    /// 自然层的整数都钳制到非负、布尔不受区域性影响，哲学层、社会层按不变区域性读写，所以这几个两轮摘要相同。
    /// </summary>
    private static readonly Dictionary<string, string> GoldenDigests = new(StringComparer.Ordinal)
    {
        ["current/Art"] = "670AE0360BC7C3799893211280818C0C199B58420727E228067358656046AB7B",
        ["current/History"] = "46340C0530F57622B2A6CF7DDECE341DEAC03F7B9E51FBB20EA74A1B071D5C55",
        ["current/Technology"] = "46340C0530F57622B2A6CF7DDECE341DEAC03F7B9E51FBB20EA74A1B071D5C55",
        ["current/Language"] = "8B9BE7459330CD61AE5D8DA81A7AD0D462CE4D7C9E6B238868984106F51E420A",
        ["current/Literature"] = "B5C1F3D24D45DEC6B1ED326EBA1325BD73119A8D6BD90DD0575641252276F315",
        ["current/Natural"] = "0483BC6597B74449B45AAA7D5D03BD3BF8AA52E3FD93CA3332AEB51BD7F11F53",
        ["current/Philosophy"] = "D5C453199F70D5CEFA7C0AD8D45CA2E9006A87888755807D4C1F497943B16722",
        ["current/Social"] = "F643D4746B919619741CD1533BF42A4F791807A58A4800C0FFDBF30A0473D23E",
        ["current/RoadHomeElite"] = "3CFCEAA635BF8603C6C337BEF0193F1E27EA18E94AF669C9B89BFB35A15162D1",
        ["current/WrathServantStrong"] = "66B518244DEF88F3CBEB96D2C82BFF0130319D85AB6DE4F647C44FC03EB743C5",
        ["sv-SE/Art"] = "0AF901E9106774AB7D5098E78D2D34D768722EB6995DA1E21A5CA3F4527B6C39",
        ["sv-SE/History"] = "57EBC8EF057C82A2F651A9C1865BED85935420F4F61F25EDB7747C3D61238FCC",
        ["sv-SE/Technology"] = "57EBC8EF057C82A2F651A9C1865BED85935420F4F61F25EDB7747C3D61238FCC",
        ["sv-SE/Language"] = "91525390741E0D1BB370045B83A88788E08455DD56A066935ACD8E1796580473",
        ["sv-SE/Literature"] = "4AD7653C553AFD594BF4082D29F61B3A3D1238CFC6DB630D7DC5382372B4FC14",
        ["sv-SE/Natural"] = "0483BC6597B74449B45AAA7D5D03BD3BF8AA52E3FD93CA3332AEB51BD7F11F53",
        ["sv-SE/Philosophy"] = "D5C453199F70D5CEFA7C0AD8D45CA2E9006A87888755807D4C1F497943B16722",
        ["sv-SE/Social"] = "F643D4746B919619741CD1533BF42A4F791807A58A4800C0FFDBF30A0473D23E",
        ["sv-SE/RoadHomeElite"] = "3CFCEAA635BF8603C6C337BEF0193F1E27EA18E94AF669C9B89BFB35A15162D1",
        ["sv-SE/WrathServantStrong"] = "66B518244DEF88F3CBEB96D2C82BFF0130319D85AB6DE4F647C44FC03EB743C5",
    };

    /// <summary>设置环境变量 LOR_STATE_VERIFY_DUMP=1 时把每个用例的完整记录写进日志，便于和旧实现逐项对照。</summary>
    private static readonly bool DumpRecords =
        Environment.GetEnvironmentVariable("LOR_STATE_VERIFY_DUMP") == "1";

    private static bool _started;

    private enum ValueKind
    {
        Int,
        Bool,
        Enum,
        IntArray,
        Ulong,
        Text,
        Opaque
    }

    private sealed record StateKey(string Name, ValueKind Kind);

    private sealed record EncounterSpec(
        string Name,
        Func<EncounterModel> Create,
        bool GenerateBeforeSave,
        StateKey[] Keys);

    private static readonly string[] IntPool =
        ["0", "1", "2", "3", "4", "5", "6", "7", "-1", "-3", " 2 ", "+3", "abc", "", "2147483648", "−1"];

    private static readonly string[] BoolPool =
        ["True", "False", "true", "FALSE", " True ", "yes", "1", ""];

    private static readonly string[] EnumPool =
        ["0", "1", "2", "3", "4", "5", "6", "7", "8", "15", "16", "-1", "x", ""];

    private static readonly string[] IntArrayPool =
        ["1,2,3,4", "", "  ", "1,,2", " 1 , 2 ", "a,1", "5", "-1,−2", "1;2", "0,0,0,0"];

    private static readonly string[] UlongPool =
        ["", "0", "123", "76561198000000000", "18446744073709551615", "-1", "abc"];

    private static readonly string[] TextPool =
        ["", "crystal_1:10:0;face_2:5:1", "1:3;2:0", "garbage"];

    private static readonly string[] OpaquePool = ["5"];

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
            await RunCoreAsync();
            Log.Info(LogPrefix + "LIBERATION_ENCOUNTER_STATE_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LIBERATION_ENCOUNTER_STATE_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task RunCoreAsync()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        // 只需要一个 RunState 给社会层生成怪物（种子与楼层）；不等待开局流程走完：
        // ActLikeIt2 可能把第一个房间换成选幕房间并一直等待选择。
        _ = TaskHelper.RunSafely(game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LIBERATIONENCOUNTERSTATEVERIFY",
            GameMode.Standard,
            ascensionLevel: 0));
        IRunState? runState = null;
        for (int frame = 0; frame < 900 && runState == null; frame++)
        {
            SceneTree tree = game.GetTree();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            runState = RunManager.Instance.DebugOnlyGetState();
        }
        if (runState == null)
        {
            throw new TimeoutException("Run state was not created.");
        }

        var failures = new List<string>();
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach ((string passName, CultureInfo? culture) in CulturePasses())
            {
                if (culture == null)
                {
                    Log.Info(LogPrefix + "SKIP pass=" + passName + " (culture unavailable)");
                    continue;
                }

                CultureInfo.CurrentCulture = culture;
                Log.Info(LogPrefix + "PASS " + passName + " culture=" + culture.Name
                    + " negativeSign=" + JsonSerializer.Serialize(culture.NumberFormat.NegativeSign));
                foreach (EncounterSpec spec in Specs())
                {
                    string digest = RunSpec(spec, runState, passName);
                    string goldenKey = passName + "/" + spec.Name;
                    if (!GoldenDigests.TryGetValue(goldenKey, out string? golden))
                    {
                        failures.Add(goldenKey + " has no golden digest (actual " + digest + ")");
                    }
                    else if (!string.Equals(golden, digest, StringComparison.Ordinal))
                    {
                        failures.Add(goldenKey + " digest " + digest + " != golden " + golden);
                    }
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(failures.Count + " check(s) failed: " + string.Join("; ", failures));
        }
    }

    private static IEnumerable<(string Name, CultureInfo? Culture)> CulturePasses()
    {
        yield return ("current", CultureInfo.CurrentCulture);
        CultureInfo? swedish;
        try
        {
            swedish = CultureInfo.GetCultureInfo("sv-SE");
        }
        catch (CultureNotFoundException)
        {
            swedish = null;
        }

        yield return ("sv-SE", swedish);
    }

    private static IEnumerable<EncounterSpec> Specs()
    {
        StateKey I(string name) => new(name, ValueKind.Int);
        StateKey B(string name) => new(name, ValueKind.Bool);
        StateKey E(string name) => new(name, ValueKind.Enum);
        StateKey A(string name) => new(name, ValueKind.IntArray);
        StateKey U(string name) => new(name, ValueKind.Ulong);
        StateKey T(string name) => new(name, ValueKind.Text);
        StateKey O(string name) => new(name, ValueKind.Opaque);

        yield return new EncounterSpec(
            "Art",
            Create<ArtFloorLiberationEncounter>,
            false,
            [I("CurrentPhase"), I("KilledBossCount"), B("TransitionPending"), B("SettlementTriggered"),
                B("EndedByPlaceholder"), B("EndedByLethalDamage")]);
        yield return new EncounterSpec(
            "History",
            Create<HistoryFloorLiberationEncounter>,
            false,
            [I("CurrentPhase"), I("KilledBossCount"), B("TransitionPending"), B("SettlementTriggered"),
                B("EndedByLethalDamage")]);
        yield return new EncounterSpec(
            "Technology",
            Create<TechnologyFloorLiberationEncounter>,
            false,
            [I("CurrentPhase"), I("KilledBossCount"), B("TransitionPending"), B("SettlementTriggered"),
                B("EndedByLethalDamage")]);
        yield return new EncounterSpec(
            "Language",
            Create<LanguageFloorLiberationEncounter>,
            false,
            [I("CurrentPhase"), I("KilledBossCount"), B("PhaseComplete"), B("TransitionPending"),
                B("SettlementTriggered"), B("EndedByLethalDamage")]);
        yield return new EncounterSpec(
            "Literature",
            Create<LiteratureFloorLiberationEncounter>,
            false,
            [I("CurrentPhase"), I("KilledBossCount"), I("NormalMovesUntilSuperGift"), I("LeftFriendSpawnRound"),
                I("RightFriendSpawnRound"), I("BlackSwanRoundStarts"), I("NextBlackSwanBrother"),
                B("PhaseComplete"), B("TransitionPending"), B("SettlementTriggered"), B("EndedByLethalDamage"),
                B("SuperGiftPending"), B("LeftFriendWeakened"), B("RightFriendWeakened")]);
        yield return new EncounterSpec(
            "Natural",
            Create<NaturalFloorLiberationEncounter>,
            false,
            [I("CurrentPhase"), I("KilledBossCount"), B("TransitionPending"), B("SettlementTriggered"),
                B("PhaseTwoRageDefeated"), B("PhaseTwoHermitDefeated"), B("EntryBranchChosen"),
                B("NihilCompleted"), O("BossHp"), O("PhaseTwoSaved"), O("Staff1.Hp"), O("Hermit.Hp"),
                O("Rage.Hp"), O("Sword0.Hp"), O("TearEdge.Hp"), O("GoldRush.Hp"), O("Happiness1.Hp")]);
        yield return new EncounterSpec(
            "Philosophy",
            Create<PhilosophyFloorLiberationEncounter>,
            false,
            [I("TwilightStateVersion"), I("TwilightAliveEggMask"), E("TwilightActiveEgg"), E("TwilightPlannedMode"),
                B("TwilightHasPlannedMode"), I("TwilightModeCycleStep"), I("TwilightJudgmentBranchEntries"),
                I("TwilightSinTraceBranchEntries"), I("TwilightPunishmentBranchEntries"),
                B("TwilightNextEndFallbackIsOne"), E("TwilightPlannedBranchCounter"),
                B("TwilightPlannedUsesEndFallback"), E("TwilightPlannedOtherFirstAction"),
                E("TwilightPlannedOtherSecondAction"), I("TwilightLastEggScheduleRound"),
                B("TwilightBrokenEggRecoveryPending"), B("TwilightIntroCgPlayed"),
                A("TwilightPlannedTargetCombatIds"), I("TwilightSmallBeakProcessedRound"),
                A("TwilightSmallBeakProcessedPlayerCombatIds")]);
        yield return new EncounterSpec(
            "Social",
            Create<SocialFloorLiberationEncounter>,
            true,
            [I("SocialFloorStateVersion"), E("SocialFloorTrial"), I("SocialFloorTrialRound"),
                B("SocialFloorSetupComplete"), B("SocialFloorPlayersHealed"), I("SocialFloorDestroyedCrystalMask"),
                I("SocialFloorDestroyedFaceMask"), B("SocialFloorHasPendingTrial"), E("SocialFloorPendingTrial"),
                U("SocialFloorScaredyCatPlayerNetId"), U("SocialFloorOzmaPlayerNetId"), B("SocialFloorCowardApplied"),
                B("SocialFloorOzmaReplacementPending"), I("SocialFloorPowderCost"), B("SocialFloorTransformed"),
                B("SocialFloorFinalStrikeTriggered"), E("SocialFloorPlannedMove"), I("SocialFloorParticipantCount"),
                I("SocialFloorBossHp"), I("SocialFloorBossChao"), T("SocialFloorSummonVitals"),
                T("SocialFloorWisdomStacks"), T("SocialFloorWisdomCards"), I("SocialFloorLionCardsSubmitted"),
                B("SocialFloorCouragePlayed"), B("SocialFloorCouragePowerPresent"),
                I("SocialFloorCouragePendingActivations"), B("SocialFloorCourageEnergyActive"),
                B("SocialFloorCourageRemoveAtTurnEnd")]);
        yield return new EncounterSpec(
            "RoadHomeElite",
            Create<RoadHomeElite>,
            false,
            [B("endedByHouseDeath"), B("completingSuccessfulCleanup")]);
        yield return new EncounterSpec(
            "WrathServantStrong",
            Create<WrathServantStrong>,
            false,
            [B("endedByServantDeath")]);
    }

    private static EncounterModel Create<T>() where T : EncounterModel =>
        ModelDb.Encounter<T>().ToMutable();

    private static string RunSpec(EncounterSpec spec, IRunState runState, string passName)
    {
        var digestInput = new StringBuilder();
        int index = 0;
        foreach (Dictionary<string, string>? input in Cases(spec))
        {
            string record = RunCase(spec, runState, input);
            string caseDigest = Digest(record);
            Log.Info(LogPrefix + "CASE " + passName + "/" + spec.Name + "#" + index + " " + caseDigest[..16]);
            if (DumpRecords)
            {
                Log.Info(LogPrefix + "RECORD " + passName + "/" + spec.Name + "#" + index + " "
                    + JsonSerializer.Serialize(record));
            }
            digestInput.Append(caseDigest).Append('\n');
            index++;
        }

        string digest = Digest(digestInput.ToString());
        Log.Info(LogPrefix + "DIGEST " + passName + "/" + spec.Name + " cases=" + index + " " + digest);
        return digest;
    }

    /// <summary>null 表示不调用 LoadCustomState，直接保存刚创建的遭遇。</summary>
    private static IEnumerable<Dictionary<string, string>?> Cases(EncounterSpec spec)
    {
        yield return null;
        yield return new Dictionary<string, string>();

        foreach (StateKey key in spec.Keys)
        {
            foreach (string value in Pool(key.Kind))
            {
                yield return new Dictionary<string, string> { [key.Name] = value };
            }
        }

        StateKey[] ints = spec.Keys.Where(static key => key.Kind == ValueKind.Int).Take(2).ToArray();
        StateKey[] bools = spec.Keys.Where(static key => key.Kind == ValueKind.Bool).Take(6).ToArray();
        string[] phases = ints.Length > 0 ? ["1", "2", "3", "5"] : [""];
        foreach (string phase in phases)
        {
            for (int mask = 0; mask < 1 << bools.Length; mask++)
            {
                var state = new Dictionary<string, string>();
                if (ints.Length > 0)
                {
                    state[ints[0].Name] = phase;
                }
                if (ints.Length > 1 && (mask & 1) == 0)
                {
                    state[ints[1].Name] = "2";
                }
                for (int bit = 0; bit < bools.Length; bit++)
                {
                    state[bools[bit].Name] = (mask & (1 << bit)) != 0 ? "True" : "False";
                }
                yield return state;
            }
        }

        // 第二个整数取负数：缺省值依赖第一个整数（例如击杀数按阶段推算）时，
        // U+2212 负号能否解析取决于区域性，结果会走不同分支。
        if (ints.Length > 1)
        {
            foreach (string negative in new[] { "-1", "−1" })
            {
                yield return new Dictionary<string, string>
                {
                    [ints[0].Name] = "3",
                    [ints[1].Name] = negative
                };
            }
        }

        var random = new Random(20260929);
        for (int count = 0; count < RandomCasesPerEncounter; count++)
        {
            var state = new Dictionary<string, string>();
            foreach (StateKey key in spec.Keys)
            {
                if (random.Next(4) == 0)
                {
                    continue;
                }

                string[] pool = Pool(key.Kind);
                state[key.Name] = pool[random.Next(pool.Length)];
            }
            yield return state;
        }
    }

    private static string[] Pool(ValueKind kind) => kind switch
    {
        ValueKind.Int => IntPool,
        ValueKind.Bool => BoolPool,
        ValueKind.Enum => EnumPool,
        ValueKind.IntArray => IntArrayPool,
        ValueKind.Ulong => UlongPool,
        ValueKind.Text => TextPool,
        _ => OpaquePool
    };

    private static string RunCase(
        EncounterSpec spec,
        IRunState runState,
        Dictionary<string, string>? input)
    {
        var record = new StringBuilder();
        record.Append("in=").Append(input == null ? "<fresh>" : Serialize(input)).Append('\n');
        Dictionary<string, string>? first = LoadAndSave(spec, runState, input, record);
        if (first != null)
        {
            LoadAndSave(spec, runState, first, record);
        }

        return record.ToString();
    }

    private static Dictionary<string, string>? LoadAndSave(
        EncounterSpec spec,
        IRunState runState,
        Dictionary<string, string>? input,
        StringBuilder record)
    {
        EncounterModel encounter = spec.Create();
        try
        {
            if (input != null)
            {
                encounter.LoadCustomState(new Dictionary<string, string>(input));
            }
        }
        catch (Exception ex)
        {
            record.Append("load threw ").Append(ex.GetType().FullName).Append('\n');
            return null;
        }

        record.Append("fields=").Append(SnapshotFields(encounter)).Append('\n');
        try
        {
            if (spec.GenerateBeforeSave)
            {
                encounter.GenerateMonstersWithSlots(runState);
            }

            Dictionary<string, string> saved = encounter.SaveCustomState();
            record.Append("out=").Append(Serialize(saved)).Append('\n');
            return saved;
        }
        catch (Exception ex)
        {
            record.Append("save threw ").Append(ex.GetType().FullName).Append('\n');
            return null;
        }
    }

    /// <summary>遭遇类自己声明的实例字段中，简单类型与字符串字典的值（按字段名排序）。</summary>
    private static string SnapshotFields(EncounterModel encounter)
    {
        Type type = encounter.GetType();
        var parts = new List<string>();
        foreach (FieldInfo field in type
                     .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                         | BindingFlags.DeclaredOnly)
                     .OrderBy(static field => field.Name, StringComparer.Ordinal))
        {
            object? value = field.GetValue(encounter);
            string? text = value switch
            {
                null when IsSimple(field.FieldType) => "null",
                string s => JsonSerializer.Serialize(s),
                Enum e => e.GetType().Name + "." + Convert.ToInt64(e, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture),
                IFormattable f when IsSimple(field.FieldType) => f.ToString(null, CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                Dictionary<string, string> d => Serialize(d),
                _ => null
            };
            if (text != null)
            {
                parts.Add(field.Name + "=" + text);
            }
        }

        return string.Join(";", parts);
    }

    private static bool IsSimple(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string)
            || underlying == typeof(decimal);
    }

    /// <summary>按字典的枚举顺序写出，顺序本身也是存档格式的一部分。</summary>
    private static string Serialize(Dictionary<string, string> state) =>
        "{" + string.Join(
            ",",
            state.Select(static entry =>
                JsonSerializer.Serialize(entry.Key) + ":" + JsonSerializer.Serialize(entry.Value))) + "}";

    private static string Digest(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
