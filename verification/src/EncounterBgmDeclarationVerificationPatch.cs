using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina;
using LibraryOfRuina.audio;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 遭遇战 BGM 的数据级转储，用于重构前后对照（A/B）。每项输出一行 <c>ROW</c>，两次构建的 ROW 行应逐行相同：
/// <list type="bullet">
/// <item>A：ModelDb 里的全部遭遇（含原版与其他模组）：是否有配置、<c>HasBgmForEncounter</c>、精英 BGM 是否接管，以及配置内容。</item>
/// <item>B：按阶段选曲与按敌人选曲的解析结果，覆盖越界阶段。</item>
/// <item>C：接待层背景的选曲：各接待层、大小写与反斜杠变体、未登记的层、没有层、两层同时存在。</item>
/// <item>D：会话轨迹：登记、按死亡推进（含连续死亡与重复死亡）、按回合或阶段刷新、注销、强制换曲、停止；
/// 每步记录当前与目标曲目、淡入淡出状态、音量上限与播放器状态，等淡入淡出结束后再记一次。</item>
/// </list>
/// 配置与会话状态是私有的，重构前后所在的类不同，所以按名字在几个候选类型上反射读取（见 <see cref="BgmTypeNames"/>）；
/// 配置记录按属性名读取，不依赖它的类型名。套件只在配置数不是 50 或有配置的遭遇类型不是 sealed 时失败，结果对照在套件外做。
/// </summary>
internal static class EncounterBgmDeclarationVerificationPatch
{
    private const string VerifyArg = "lor-verify-encounter-bgm";
    private const string LogPrefix = "[LibraryOfRuina.EncounterBgm.Verify] ";
    private const int ExpectedConfiguredEncounters = 50;
    private const double SettleTimeoutSeconds = 8;

    private static readonly string[] BgmTypeNames =
    [
        "LibraryOfRuina.encounters.EncounterBgmController",
        "LibraryOfRuina.encounters.BgmRegistry",
        "LibraryOfRuina.encounters.BgmSession",
        "LibraryOfRuina.encounters.BgmCrossfader",
    ];

    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
            await RunCoreAsync();
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Digest.ToString())))[..16];
            Log.Info(LogPrefix + "ENCOUNTER_BGM_OK rows=" + _rows + " digest=" + digest);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "ENCOUNTER_BGM_FAILED: " + ex);
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

    private static async Task RunCoreAsync()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        // 只需要一个 RunState（种子、幕、地图坐标）和一个活着的 NRun 作为播放器宿主；不等待开局流程走完：
        // ActLikeIt2 可能把第一个房间换成选幕房间并一直等待选择。多等一段帧，让进入第一个房间的事件先过去，
        // 免得它在会话进行中触发 RoomExited 之类的停止。
        _ = TaskHelper.RunSafely(game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "ENCOUNTERBGMVERIFY",
            GameMode.Standard,
            ascensionLevel: 0));
        IRunState? runState = null;
        for (int frame = 0; frame < 900 && runState == null; frame++)
        {
            await NextFrame();
            runState = RunManager.Instance.DebugOnlyGetState();
        }

        if (runState == null)
        {
            throw new TimeoutException("Run state was not created.");
        }

        for (int frame = 0; frame < 240; frame++)
        {
            await NextFrame();
        }

        Row("ENV sideEffects=" + LibraryOfRuinaSettings.RuntimeSideEffectsEnabled
            + " actScope=" + ReverberationEnsembleBgmController.IsActScope
            + " act=" + runState.Act?.Id.Entry
            + " running=" + EncounterBgmController.IsRunning);

        List<EncounterModel> encounters = AllEncounterModels();
        List<EncounterModel> configured = RecordEncounterTable(encounters, runState);
        if (configured.Count != ExpectedConfiguredEncounters)
        {
            throw new InvalidOperationException(
                "Expected " + ExpectedConfiguredEncounters + " encounters with BGM, found " + configured.Count + ".");
        }

        string[] unsealed = configured
            .Where(static encounter => !encounter.GetType().IsSealed)
            .Select(static encounter => encounter.GetType().FullName ?? encounter.GetType().Name)
            .ToArray();
        if (unsealed.Length > 0)
        {
            throw new InvalidOperationException(
                "Encounters with BGM must be sealed (exact-type lookup): " + string.Join(", ", unsealed));
        }

        RecordDynamicResolvers(configured, runState);
        RecordBackgroundLayers(configured, encounters, runState);
        await RecordSessions(configured, runState);
    }

    // ---- A：全部遭遇 ----

    private static List<EncounterModel> RecordEncounterTable(List<EncounterModel> encounters, IRunState runState)
    {
        var configured = new List<EncounterModel>();
        foreach (EncounterModel canonical in encounters)
        {
            Type type = canonical.GetType();
            string prefix = "A enc=" + canonical.Id + " type=" + type.FullName;
            try
            {
                EncounterModel encounter = canonical.ToMutable();
                var combatState = new CombatState(encounter, runState);
                object? config = FindEncounterConfig(encounter);
                if (config != null)
                {
                    configured.Add(canonical);
                }

                Row(prefix
                    + " sealed=" + type.IsSealed
                    + " guest=" + GuestReceptionPoolRegistry.IsGuestEncounterType(type)
                    + " phaseSource=" + (encounter is ILiberationPhaseBgmSource)
                    + " has=" + EncounterBgmController.HasBgmForEncounter(combatState)
                    + " eliteBgm=" + Try(() => AbnormalityEliteBgmController.HasBgmForCombat(combatState).ToString())
                    + " cfg=" + Describe(config));
            }
            catch (Exception ex)
            {
                Row(prefix + " error=" + ex.GetType().Name);
            }
        }

        return configured;
    }

    // ---- B：动态选曲 ----

    private static void RecordDynamicResolvers(List<EncounterModel> configured, IRunState runState)
    {
        EncounterModel? nonPhaseEncounter = configured.FirstOrDefault(static e => e is not ILiberationPhaseBgmSource);
        foreach (EncounterModel canonical in configured)
        {
            EncounterModel encounter = canonical.ToMutable();
            object config = FindEncounterConfig(encounter)!;
            if (ReadConfig(config, "DynamicTrackResolver") is not Func<CombatStateLike, int> resolver)
            {
                continue;
            }

            string prefix = "B enc=" + canonical.Id;
            var combatState = new CombatState(encounter, runState);
            if (encounter is ILiberationPhaseBgmSource source)
            {
                for (int phase = -1; phase <= 8; phase++)
                {
                    SetPhase(encounter, phase);
                    Row(prefix + " phase=" + phase + " current=" + source.CurrentPhase
                        + " track=" + Try(() => resolver(combatState).ToString(CultureInfo.InvariantCulture)));
                }
            }
            else
            {
                Row(prefix + " enemies=0 track=" + Try(() => resolver(combatState).ToString(CultureInfo.InvariantCulture)));
                List<Creature> creatures = CreateCreatures(encounter, combatState, 2);
                Creature? sourceCreature = creatures.FirstOrDefault(static c => c.Monster is IEncounterDynamicBgmTrackSource);
                Row(prefix + " enemies=" + string.Join(",", creatures.Select(static c => c.Monster?.Id.Entry))
                    + " track=" + Try(() => resolver(combatState).ToString(CultureInfo.InvariantCulture)));
                if (sourceCreature != null)
                {
                    for (int mask = 7; mask >= 0; mask--)
                    {
                        SetMember(sourceCreature.Monster!, "AliveEggMask", mask);
                        var trackSource = (IEncounterDynamicBgmTrackSource)sourceCreature.Monster!;
                        Row(prefix + " eggMask=" + mask + " sourceTrack=" + trackSource.CurrentEncounterBgmTrackIndex
                            + " track=" + Try(() => resolver(combatState).ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }

            if (nonPhaseEncounter != null)
            {
                var foreignState = new CombatState(nonPhaseEncounter.ToMutable(), runState);
                Row(prefix + " foreignEncounter=" + nonPhaseEncounter.Id
                    + " track=" + Try(() => resolver(foreignState).ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    // ---- C：接待层背景 ----

    private static void RecordBackgroundLayers(
        List<EncounterModel> configured,
        List<EncounterModel> encounters,
        IRunState runState)
    {
        string general = GuestReceptionPoolRegistry.GeneralReceptionFloorLayerScenePath;
        var cases = new List<(string Name, string[] Layers)>
        {
            ("none", Array.Empty<string>()),
        };
        foreach (string layer in GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths)
        {
            cases.Add((LayerName(layer), [layer]));
        }

        cases.Add(("general-variant", ["  " + general.ToUpperInvariant().Replace('/', '\\') + " "]));
        cases.Add(("unregistered-layer",
            ["res://scenes/backgrounds/guest_reception_pool/layers/guest_reception_pool_bg_00_unknown_floor.tscn"]));
        cases.Add(("not-a-scene", [general.Replace(".tscn", ".png", StringComparison.Ordinal)]));
        cases.Add(("religion+general",
            [GuestReceptionPoolRegistry.ReligionReceptionFloorLayerScenePath, general]));

        MethodInfo resolve = FindStaticMethod("ResolveConfigForEncounter", 3);
        // 有配置的全部遭遇（非接待遭遇应当原样返回默认配置），外加一个没有配置的接待遭遇作对照。
        IEnumerable<EncounterModel> targets = configured.Concat(encounters
            .Where(e => !configured.Contains(e) && GuestReceptionPoolRegistry.IsGuestEncounterType(e.GetType()))
            .Take(1));
        foreach (EncounterModel canonical in targets)
        {
            EncounterModel encounter = canonical.ToMutable();
            string generated = Try(() =>
            {
                encounter.GenerateMonstersWithSlots(runState);
                return "ok";
            });
            var combatState = new CombatState(encounter, runState);
            object? defaultConfig = FindEncounterConfig(encounter);
            foreach ((string name, string[] layers) in cases)
            {
                string prefix = "C enc=" + canonical.Id + " gen=" + generated + " layers=" + name;
                try
                {
                    SetBackgroundLayers(encounter, layers);
                    object?[] args = [combatState, defaultConfig, null];
                    object? result = resolve.Invoke(null, args);
                    Row(prefix + " sameAsDefault=" + ReferenceEquals(result, defaultConfig)
                        + " matched=" + (args[2] as string ?? "null")
                        + " cfg=" + Describe(result));
                }
                catch (Exception ex)
                {
                    Row(prefix + " error=" + Unwrap(ex).GetType().Name);
                }
            }
        }
    }

    // ---- D：会话轨迹 ----

    private static async Task RecordSessions(List<EncounterModel> configured, IRunState runState)
    {
        string[] layers = GuestReceptionPoolRegistry.GuestReceptionFloorLayerScenePaths;
        for (int index = 0; index < configured.Count; index++)
        {
            EncounterModel canonical = configured[index];
            // 接待遭遇轮流使用 6 个接待层，第 7 个起不设层，走遭遇自己的配置。
            string[]? layer = GuestReceptionPoolRegistry.IsGuestEncounterType(canonical.GetType())
                ? index % 7 < layers.Length ? [layers[index % 7]] : []
                : null;
            await RecordSession(canonical, runState, layer, initialPhase: null);
        }

        EncounterModel? natural = configured.FirstOrDefault(static e =>
            e.GetType().Name == "NaturalFloorLiberationEncounter");
        if (natural != null)
        {
            // 读档回到自然层第 5 阶段时，会话直接从最后一首开始。
            await RecordSession(natural, runState, null, initialPhase: 5);
            await RecordSession(natural, runState, null, initialPhase: 4);
        }
    }

    private static async Task RecordSession(
        EncounterModel canonical,
        IRunState runState,
        string[]? layers,
        int? initialPhase)
    {
        EncounterBgmController.StopRuntimeSession();
        string id = canonical.Id + (initialPhase is { } p ? "@phase" + p : string.Empty);
        EncounterModel encounter = canonical.ToMutable();
        if (layers != null)
        {
            string generated = Try(() =>
            {
                encounter.GenerateMonstersWithSlots(runState);
                SetBackgroundLayers(encounter, layers);
                return "ok";
            });
            Row("D enc=" + id + " layers=" + (layers.Length == 0 ? "none" : LayerName(layers[0])) + " gen=" + generated);
        }

        if (initialPhase is { } phase)
        {
            SetPhase(encounter, phase);
        }

        var combatState = new CombatState(encounter, runState);
        List<Creature> creatures = CreateCreatures(encounter, combatState, 3);
        Row("D enc=" + id + " creatures=" + string.Join(",", creatures.Select(static c => c.Monster?.Id.Entry)));
        Snapshot(id, "start");

        for (int i = 0; i < creatures.Count; i++)
        {
            Creature creature = creatures[i];
            Step(id, "register" + i, () => EncounterBgmController.RegisterMonster(creature));
        }

        // 同一个生物再登记一次：不重复订阅死亡事件，也不重启会话。
        if (creatures.Count > 0)
        {
            Step(id, "register0-again", () => EncounterBgmController.RegisterMonster(creatures[0]));
        }

        await SettleAndSnapshot(id, "registered");

        string mode = ReadActiveConfig("ProgressionMode")?.ToString() ?? "none";
        if (mode == "OnMonsterDeath")
        {
            if (creatures.Count > 0)
            {
                Step(id, "die0", () => creatures[0].InvokeDiedEvent());
                await SettleAndSnapshot(id, "die0");
            }

            // 连续两次死亡：第二次推进发生在淡入淡出进行中，要等第一段结束后接着推进。
            if (creatures.Count > 2)
            {
                Step(id, "die1", () => creatures[1].InvokeDiedEvent());
                Step(id, "die2", () => creatures[2].InvokeDiedEvent());
                await SettleAndSnapshot(id, "die12");
            }

            if (creatures.Count > 0)
            {
                Step(id, "die0-again", () => creatures[0].InvokeDiedEvent());
                await SettleAndSnapshot(id, "die0-again");
            }
        }
        else
        {
            if (creatures.Count > 0)
            {
                Step(id, "die0-ignored", () => creatures[0].InvokeDiedEvent());
            }

            Creature? eggSource = creatures.FirstOrDefault(static c => c.Monster is IEncounterDynamicBgmTrackSource);
            int[] eggMasks = [7, 7, 3, 3, 1, 1, 0, 0];
            for (int step = 1; step <= 8; step++)
            {
                combatState.RoundNumber = step + 1;
                if (encounter is ILiberationPhaseBgmSource)
                {
                    SetPhase(encounter, step);
                }

                if (eggSource != null)
                {
                    SetMember(eggSource.Monster!, "AliveEggMask", eggMasks[step - 1]);
                }

                Step(id, "refresh" + step, EncounterBgmController.RefreshCurrentEncounterTrack);
                await SettleAndSnapshot(id, "refresh" + step);
            }
        }

        if (canonical.GetType().Name == "KaliSpecialGuestEncounter")
        {
            Step(id, "force-ego", () => EncounterBgmController.ForceCurrentEncounterTrack(
                LibraryOfRuina.specialguests.Kali.Kali.EgoBgmPath,
                "RedMistEgoBGM"));
            await SettleAndSnapshot(id, "force-ego");
        }

        if (creatures.Count > 0)
        {
            Step(id, "unregister0", () => EncounterBgmController.UnregisterMonster(creatures[0]));
            Step(id, "die0-after-unregister", () => creatures[0].InvokeDiedEvent());
        }

        Step(id, "stop", EncounterBgmController.StopRuntimeSession);
        // 停止后再死亡、再刷新都不应有效果。
        if (creatures.Count > 1)
        {
            Step(id, "die1-after-stop", () => creatures[1].InvokeDiedEvent());
        }

        Step(id, "refresh-after-stop", EncounterBgmController.RefreshCurrentEncounterTrack);
    }

    private static void Step(string id, string name, Action action)
    {
        string error = "none";
        try
        {
            action();
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
        }

        Snapshot(id, name + (error == "none" ? string.Empty : " error=" + error));
    }

    private static async Task SettleAndSnapshot(string id, string name)
    {
        var watch = Stopwatch.StartNew();
        while ((bool)ReadStatic("_isTransitioning")! && watch.Elapsed.TotalSeconds < SettleTimeoutSeconds)
        {
            await NextFrame();
        }

        bool timedOut = (bool)ReadStatic("_isTransitioning")!;
        Snapshot(id, name + ".settled" + (timedOut ? " TIMEOUT" : string.Empty), includeVolume: !timedOut);
    }

    private static void Snapshot(string id, string step, bool includeVolume = false)
    {
        var text = new StringBuilder("D enc=").Append(id).Append(" step=").Append(step);
        text.Append(" running=").Append(EncounterBgmController.IsRunning);
        text.Append(" trans=").Append(ReadStatic("_isTransitioning"));
        text.Append(" cur=").Append(ReadStatic("_currentTrackIndex"));
        text.Append(" tgt=").Append(ReadStatic("_targetTrackIndex"));
        text.Append(" tag=").Append(ReadActiveConfig("LogTag") ?? "null");
        text.Append(" tracks=").Append(ReadActiveConfig("TrackPaths") is string[] tracks ? tracks.Length : -1);
        text.Append(" maxDb=").Append(((float)ReadStatic("_activeMaxVolumeDb")!).ToString("R", CultureInfo.InvariantCulture));
        text.Append(" reg=").Append(((HashSet<Creature>)ReadStatic("RegisteredCreatures")!).Count);
        text.Append(" suspended=").Append(ReadStatic("_suspendedSession") != null);
        text.Append(" active=").Append(DescribePlayer(ReadStatic("_activePlayer") as AudioStreamPlayer, includeVolume));
        text.Append(" inactive=").Append(DescribePlayer(ReadStatic("_inactivePlayer") as AudioStreamPlayer, includeVolume));
        text.Append(" tween=").Append(ReadStatic("_fadeTween") != null);
        Row(text.ToString());
    }

    private static string DescribePlayer(AudioStreamPlayer? player, bool includeVolume)
    {
        if (player == null)
        {
            return "null";
        }

        if (!GodotObject.IsInstanceValid(player))
        {
            return "freed";
        }

        string stream = player.Stream == null
            ? "none"
            : player.Stream.GetLength().ToString("0.000", CultureInfo.InvariantCulture);
        string text = player.Name + "(stream=" + stream + ",playing=" + player.Playing;
        if (includeVolume)
        {
            text += ",vol=" + player.VolumeDb.ToString("0.00", CultureInfo.InvariantCulture);
        }

        return text + ")";
    }

    // ---- 辅助 ----

    private static object? FindEncounterConfig(EncounterModel encounter)
    {
        Type? registry = ModAssembly.GetType("LibraryOfRuina.encounters.BgmRegistry");
        MethodInfo? lookup = registry?.GetMethod("TryGetEncounterConfig", AnyStatic);
        if (lookup != null)
        {
            object?[] args = [encounter, null];
            return (bool)lookup.Invoke(null, args)! ? args[1] : null;
        }

        var table = (IDictionary)ReadStatic("ConfigByEncounterType")!;
        return table.Contains(encounter.GetType()) ? table[encounter.GetType()] : null;
    }

    private static object? ReadConfig(object config, string name) =>
        config.GetType().GetProperty(name, AnyInstance)!.GetValue(config);

    private static object? ReadActiveConfig(string name) =>
        ReadStatic("_activeConfig") is { } config ? ReadConfig(config, name) : null;

    private static string Describe(object? config)
    {
        if (config == null)
        {
            return "none";
        }

        var tracks = (string[])ReadConfig(config, "TrackPaths")!;
        var thresholds = (int[])ReadConfig(config, "RoundThresholds")!;
        return "tag=" + ReadConfig(config, "LogTag")
            + " mode=" + ReadConfig(config, "ProgressionMode")
            + " tracks=[" + string.Join(",", tracks) + "]"
            + " thresholds=[" + string.Join(",", thresholds) + "]"
            + " vol=" + ((float)ReadConfig(config, "VolumeScale")!).ToString("R", CultureInfo.InvariantCulture)
            + " dyn=" + (ReadConfig(config, "DynamicTrackResolver") != null);
    }

    private static IEnumerable<Type> BgmTypes() =>
        BgmTypeNames.Select(static name => ModAssembly.GetType(name)).OfType<Type>();

    private static object? ReadStatic(string name)
    {
        foreach (Type type in BgmTypes())
        {
            if (type.GetField(name, AnyStatic) is { } field)
            {
                return field.GetValue(null);
            }

            if (type.GetProperty(name, AnyStatic) is { } property)
            {
                return property.GetValue(null);
            }
        }

        throw new MissingMemberException("BGM static member not found: " + name);
    }

    private static MethodInfo FindStaticMethod(string name, int parameterCount)
    {
        foreach (Type type in BgmTypes())
        {
            MethodInfo? method = type.GetMethods(AnyStatic)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
            if (method != null)
            {
                return method;
            }
        }

        throw new MissingMethodException("BGM static method not found: " + name);
    }

    private static void SetPhase(EncounterModel encounter, int phase)
    {
        Type type = encounter.GetType();
        if (type.GetProperty("CurrentPhase", AnyInstance) is { SetMethod: not null } property)
        {
            property.SetValue(encounter, phase);
            return;
        }

        if (type.GetField("_currentPhase", AnyInstance) is { } field)
        {
            field.SetValue(encounter, phase);
            return;
        }

        if (type.GetProperty("Trial", AnyInstance) is { SetMethod: not null } trial)
        {
            trial.SetValue(encounter, Enum.ToObject(trial.PropertyType, phase));
            return;
        }

        throw new MissingMemberException(type.Name + " has no settable phase.");
    }

    private static void SetMember(object target, string name, object value)
    {
        Type type = target.GetType();
        if (type.GetProperty(name, AnyInstance) is { SetMethod: not null } property)
        {
            property.SetValue(target, value);
            return;
        }

        throw new MissingMemberException(type.Name + "." + name);
    }

    private static void SetBackgroundLayers(EncounterModel encounter, string[] layers)
    {
        var assets = new BackgroundAssets(GuestReceptionPoolRegistry.SharedBackgroundTitle, new Rng(7uL));
        assets.BgLayers.Clear();
        assets.BgLayers.AddRange(layers);
        FieldInfo field = typeof(EncounterModel).GetField("_backgroundAssets", AnyInstance)
            ?? throw new MissingFieldException(nameof(EncounterModel), "_backgroundAssets");
        field.SetValue(encounter, assets);
    }

    /// <summary>
    /// 不经过 <c>CombatState.CreateCreature</c>：它会调用遭遇的 <c>OnCreatureSpawned</c> 并消耗随机数。
    /// 这里只需要 <c>CombatState</c> 指向本场、按顺序出现在敌人列表里的生物。
    /// </summary>
    private static List<Creature> CreateCreatures(EncounterModel encounter, CombatState combatState, int count)
    {
        var creatures = new List<Creature>();
        List<MonsterModel> candidates = encounter.AllPossibleMonsters.ToList();
        for (int attempt = 0; attempt < candidates.Count * 2 && creatures.Count < count; attempt++)
        {
            try
            {
                MonsterModel monster = candidates[attempt % candidates.Count].ToMutable();
                var creature = new Creature(monster, CombatSide.Enemy, "slot" + creatures.Count)
                {
                    CombatState = combatState
                };
                combatState.AddCreature(creature);
                creatures.Add(creature);
            }
            catch (Exception)
            {
                // 个别怪物的生命值读取依赖战斗状态，换下一个候选。
            }
        }

        return creatures;
    }

    private static List<EncounterModel> AllEncounterModels()
    {
        var models = new List<EncounterModel>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in SafeTypes(assembly))
            {
                if (type.IsAbstract || !typeof(EncounterModel).IsAssignableFrom(type) || !ModelDb.Contains(type))
                {
                    continue;
                }

                EncounterModel? model = Try2(() => ModelDb.GetById<EncounterModel>(ModelDb.GetId(type)));
                if (model != null)
                {
                    models.Add(model);
                }
            }
        }

        return models
            .DistinctBy(static model => model.Id)
            .OrderBy(static model => model.Id.ToString(), StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
        catch (Exception)
        {
            return Array.Empty<Type>();
        }
    }

    private static string LayerName(string layer)
    {
        string file = layer.Replace('\\', '/').Trim();
        int slash = file.LastIndexOf('/');
        return slash >= 0 ? file[(slash + 1)..] : file;
    }

    private static string Try(Func<string> func)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            return "error:" + Unwrap(ex).GetType().Name;
        }
    }

    private static T? Try2<T>(Func<T> func) where T : class
    {
        try
        {
            return func();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Exception Unwrap(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    private static async Task NextFrame()
    {
        SceneTree tree = NGame.Instance!.GetTree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
