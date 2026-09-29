using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.acts;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.features.settings;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 本局设置载体（<see cref="LibraryRunSettingsModifier"/>）的行为检查。本地设置直接改后备字段（setter 在局内拒绝写入，
/// 这里模拟的是“另一台机器的本地设置不同”或“局外改过设置”）。
/// <list type="bullet">
/// <item><c>out-of-run</c>：没有局时读本地设置。</item>
/// <item><c>mid-run</c>：以 A 建局后把本地改成 B 再生成房间：房间与以 A 建局相同，事件遗物门控、抗性倍率、BGM 开关仍按 A；
/// 另建一局以 B 生成房间，确认这个布局下开关确实影响房间（否则前一项没有区分力）。</item>
/// <item><c>save-load</c>：存档 JSON 往返后以另一组本地设置读档，载体仍是 A；读档回调把抗性设回 A。</item>
/// <item><c>old-save</c>：去掉载体的存档按读档时的本地设置补建；房主规范化后的存档再换本地设置读，不再变。</item>
/// <item><c>mp-new</c>：房主按本地设置追加载体，修改器列表按联机报文往返后，本地设置不同的客户端取出的是房主的值；
/// 两端各自建两名玩家的局，房间序列相同。</item>
/// <item><c>mp-load</c>：房主规范化旧存档时补建，报文往返后本地设置不同的客户端读到同一个值。</item>
/// <item><c>daily-load</c>：联机每日挑战读档界面的修改器格子数量，以及带载体的存档经过该界面初始化后载体仍在。</item>
/// </list>
/// 另记两行 INFO：Neow 过滤是否隐藏载体、离开本局后抗性是否复位（两者依赖本阶段之外的接线）。
/// </summary>
internal static class RunSettingsVerificationPatch
{
    private const string VerifyArg = "lor-verify-run-settings";
    private const string LogPrefix = "[LibraryOfRuina.RunSettings.Verify] ";
    private const string Seed = "LORRUNSETA";
    private const string OtherSeed = "LORRUNSETB";

    private static readonly Func<ActModel>[] Layout =
        [ModelDb.Act<Malkuth>, ModelDb.Act<NetZech>, ModelDb.Act<Chesed>];

    private static bool _started;
    private static int _checks;

    private readonly record struct Settings(bool Extension, int Resistance)
    {
        public override string ToString() => "ext=" + Extension + ",res=" + Resistance;
    }

    private static readonly Settings A = new(true, 2);
    private static readonly Settings B = new(false, 1);

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
        bool previousExtension = LibraryOfRuinaSettings.MonsterExtensionEnabled;
        double previousResistance = LibraryOfRuinaSettings.ResistanceMode;
        try
        {
            Row("carrierId=" + ModelDb.Modifier<LibraryRunSettingsModifier>().Id);
            VerifyOutOfRun();
            string roomsOn = VerifyMidRun(out string json);
            VerifySaveLoad(json);
            VerifyOldSave(json);
            VerifyMultiplayerNewRun(roomsOn);
            VerifyMultiplayerLoad(json);
            await VerifyDailyLoadScreen();
            RecordExternalWiring();
            Log.Info(LogPrefix + "RUN_SETTINGS_OK checks=" + _checks);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "RUN_SETTINGS_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
        finally
        {
            CleanupRun();
            SetLocalRaw(previousExtension, previousResistance);
            LibraryRunSettings.OnRunCleaningUp();
        }
    }

    private static void VerifyOutOfRun()
    {
        CleanupRun();
        Require(!RunManager.Instance.IsInProgress, "out-of-run: a run is still in progress");
        SetLocal(A);
        Require(LibraryRunSettings.MonsterExtensionEnabled, "out-of-run: reader did not follow local A");
        SetLocal(B);
        Require(!LibraryRunSettings.MonsterExtensionEnabled, "out-of-run: reader did not follow local B");
        LibraryRunSettings.OnRunCleaningUp();
        Require(LibraryResistanceModeState.Current == LibraryResistanceMode.Ignore,
            "out-of-run: cleanup did not restore resistance from local B");
        Row("out-of-run ok");
    }

    private static string VerifyMidRun(out string json)
    {
        SetLocal(A);
        RunState reference = SetUp(CreateRun(Seed, 1));
        string roomsA = Rooms(reference);
        CleanupRun();

        SetLocal(B);
        RunState disabled = SetUp(CreateRun(Seed, 1));
        string roomsB = Rooms(disabled);
        CleanupRun();
        Require(roomsA != roomsB, "mid-run: the extension setting does not change rooms for this layout");

        SetLocal(A);
        RunState state = CreateRun(Seed, 1);
        SetLocal(B);
        SetUp(state);
        string roomsMid = Rooms(state);
        Row("mid-run rooms A=" + Hash(roomsA) + " B=" + Hash(roomsB) + " A-then-B=" + Hash(roomsMid));
        Require(roomsMid == roomsA, "mid-run: rooms followed the local setting instead of the run");
        RequireCarrier(LibraryRunSettings.Find(state), A, "mid-run carrier");
        Require(LibraryRunSettings.MonsterExtensionEnabled, "mid-run: current-run reader is not A");
        Require(LibraryOfRuinaSettings.RuntimeSideEffectsEnabled, "mid-run: BGM gate did not follow the run");
        Require(LibraryResistanceModeState.Current == LibraryResistanceMode.Weak, "mid-run: resistance is not A");
        Require(LibraryResistanceLevel.Resist.GetMultiplier() == 0.5m, "mid-run: resist multiplier is not the weak one");

        bool eventRelicsIncludeMod = ModelDb.RelicPool<EventRelicPool>()
            .GetUnlockedRelics(state.UnlockState)
            .Any(MonsterExtensionRuntimeGate.IsInjectedByThisMod);
        Require(eventRelicsIncludeMod, "mid-run: event relic gate followed local B");

        LibraryOfRuinaSettings.ResistanceMode = 3d;
        Require(LibraryOfRuinaSettings.ResistanceMode == 1d, "mid-run: setter accepted a change during the run");
        Require(LibraryResistanceModeState.Current == LibraryResistanceMode.Weak, "mid-run: setter changed the run's resistance");

        SerializableRun save = RunManager.Instance.ToSave(null);
        json = SaveManager.ToJson(save);
        int start = json.IndexOf("LIBRARY_RUN_SETTINGS", StringComparison.Ordinal);
        Row("mid-run save modifiers=" + string.Join(",", save.Modifiers.Select(static modifier => modifier.Id))
            + " json=" + (start < 0 ? "missing" : json.Substring(Math.Max(0, start - 20), Math.Min(260, json.Length - Math.Max(0, start - 20))).ReplaceLineEndings(" ")));
        CleanupRun();
        Row("mid-run ok");
        return roomsA;
    }

    private static void VerifySaveLoad(string json)
    {
        SetLocal(B);
        SerializableRun loaded = FromJson(json);
        Require(LibraryRunSettings.IsMonsterExtensionEnabled(loaded), "save-load: save reader did not read the carrier");
        RunState state = RunState.FromSerializable(loaded);
        LibraryRunSettingsModifier? carrier = LibraryRunSettings.Find(state);
        RequireCarrier(carrier, A, "save-load carrier");
        Require(state.Modifiers.OfType<LibraryRunSettingsModifier>().Count() == 1, "save-load: carrier duplicated");
        LibraryRunSettings.OnRunCleaningUp();
        carrier!.OnRunLoaded(state);
        Require(LibraryResistanceModeState.Current == LibraryResistanceMode.Weak, "save-load: load hook did not apply A");
        LibraryRunSettings.OnRunCleaningUp();
        Row("save-load ok");
    }

    private static void VerifyOldSave(string json)
    {
        SerializableRun old = WithoutCarrier(FromJson(json));
        SetLocal(B);
        RunState backfilled = RunState.FromSerializable(old);
        RequireCarrier(LibraryRunSettings.Find(backfilled), B, "old-save backfill");
        Require(old.Modifiers.All(static modifier => modifier.Id != ModelDb.Modifier<LibraryRunSettingsModifier>().Id),
            "old-save: backfill wrote into the caller's save object");

        SerializableRun canonical = RunManager.CanonicalizeSave(old, 1uL);
        RequireCarrier(LibraryRunSettings.Find(canonical), B, "old-save canonical");
        SetLocal(A);
        RunState reloaded = RunState.FromSerializable(FromJson(SaveManager.ToJson(canonical)));
        RequireCarrier(LibraryRunSettings.Find(reloaded), B, "old-save reload after backfill");
        Row("old-save ok");
    }

    private static void VerifyMultiplayerNewRun(string roomsOn)
    {
        SetLocal(A);
        List<ModifierModel> hostList = LibraryRunSettings.WithHostCarrier([]);
        List<ModifierModel> received = RoundTrip(hostList);

        SetLocal(B);
        List<ModifierModel> clientVisible = LibraryRunSettings.TakeLobbyCarrier(Seed, received);
        Require(!clientVisible.OfType<LibraryRunSettingsModifier>().Any(), "mp-new: carrier left in the list for vanilla screens");
        RunState client = CreateRun(Seed, 2, clientVisible);
        RequireCarrier(LibraryRunSettings.Find(client), A, "mp-new client");
        SetUp(client);
        string clientRooms = Rooms(client);
        foreach (Player player in client.Players)
        {
            Require(LibraryRunSettings.IsMonsterExtensionEnabled(player.RunState), "mp-new: player " + player.NetId + " reads another value");
        }

        CleanupRun();

        SetLocal(A);
        RunState host = CreateRun(Seed, 2, LibraryRunSettings.TakeLobbyCarrier(Seed, hostList));
        RequireCarrier(LibraryRunSettings.Find(host), A, "mp-new host");
        SetUp(host);
        string hostRooms = Rooms(host);
        CleanupRun();
        Row("mp-new rooms host=" + Hash(hostRooms) + " client=" + Hash(clientRooms) + " singleOn=" + Hash(roomsOn));
        Require(hostRooms == clientRooms, "mp-new: host and client generated different rooms");

        // 开局消息和建局的种子对不上时不接收暂存的载体，退回本地设置。
        SetLocal(B);
        LibraryRunSettings.TakeLobbyCarrier(Seed, RoundTrip(hostList));
        RequireCarrier(LibraryRunSettings.Find(CreateRun(OtherSeed, 2)), B, "mp-new seed mismatch");
        Row("mp-new ok");
    }

    private static void VerifyMultiplayerLoad(string json)
    {
        SerializableRun old = WithoutCarrier(FromJson(json));
        Settings hostLocal = new(true, 3);
        SetLocal(hostLocal);
        SerializableRun canonical = RunManager.CanonicalizeSave(old, 1uL);

        var writer = new PacketWriter();
        writer.Write(canonical);
        var reader = new PacketReader();
        reader.Reset(writer.Buffer);
        SerializableRun received = reader.Read<SerializableRun>();

        SetLocal(B);
        RequireCarrier(LibraryRunSettings.Find(RunState.FromSerializable(received)), hostLocal, "mp-load client");
        SetLocal(hostLocal);
        RequireCarrier(LibraryRunSettings.Find(RunState.FromSerializable(canonical)), hostLocal, "mp-load host");
        Row("mp-load ok");
    }

    private static async Task VerifyDailyLoadScreen()
    {
        SetLocal(A);
        List<ModifierModel> daily = ModifierModel.Pick2Good1Bad(new Rng(7uL), []).ToList();
        RunState state = CreateRun(Seed, 1, daily, GameMode.Daily);
        RunManager.Instance.SetUpNewSingleplayer(state, shouldSave: false, DateTimeOffset.UtcNow);
        SerializableRun save = RunManager.Instance.ToSave(null);
        CleanupRun();
        int modifierCount = save.Modifiers.Count;

        NDailyRunLoadScreen screen = NDailyRunLoadScreen.Create()
            ?? throw new InvalidOperationException("daily-load: screen scene unavailable");
        NGame.Instance!.AddChild(screen);
        await WaitFrames(2);
        try
        {
            int slots = screen.GetNode<Control>("%ModifiersContainer").GetChildren().OfType<NDailyRunScreenModifier>().Count();
            Row("daily-load slots=" + slots + " saveModifiers=" + modifierCount);
            Require(modifierCount > slots, "daily-load: the save does not exceed the slots, the patch is not exercised");

            var lobby = new LoadRunLobby(new NetSingleplayerGameService(), screen, save);
            typeof(NDailyRunLoadScreen)
                .GetField("_lobby", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(screen, lobby);
            typeof(NDailyRunLoadScreen)
                .GetMethod("InitializeDisplay", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(screen, null);
            Require(lobby.Run.Modifiers.Count == modifierCount, "daily-load: the save lost modifiers after display");
            Require(LibraryRunSettings.Find(lobby.Run) != null, "daily-load: carrier was not restored");
            lobby.CleanUp(disconnectSession: false);
        }
        finally
        {
            screen.QueueFree();
        }

        Row("daily-load ok");
    }

    private static void RecordExternalWiring()
    {
        LibraryRunSettingsModifier carrier = LibraryRunSettingsModifier.CreateFromLocalSettings();
        bool neowHides = LibrarySecondAscensionNeowModifierFilter.FilterForNeow([carrier]).Count == 0;
        Row("INFO neowFilterHidesCarrier=" + neowHides);

        SetLocal(A);
        RunState state = SetUp(CreateRun(Seed, 1));
        Require(LibraryResistanceModeState.Current == LibraryResistanceMode.Weak, "wiring: run did not apply A");
        SetLocalRaw(B.Extension, B.Resistance);
        CleanupRun();
        Row("INFO cleanupRestoresResistance=" + (LibraryResistanceModeState.Current == LibraryResistanceMode.Ignore)
            + " current=" + LibraryResistanceModeState.Current);
        _ = state;
    }

    private static RunState CreateRun(
        string seed,
        int playerCount,
        IReadOnlyList<ModifierModel>? modifiers = null,
        GameMode gameMode = GameMode.Standard)
    {
        Player[] players = Enumerable.Range(0, playerCount)
            .Select(index => Player.CreateForNewRun(
                index == 0 ? ModelDb.Character<Ironclad>() : ModelDb.Character<Silent>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                (ulong)(index + 1)))
            .ToArray();
        return RunState.CreateForNewRun(
            players,
            Layout.Select(static act => act().ToMutable()).ToList(),
            modifiers ?? Array.Empty<ModifierModel>(),
            gameMode,
            0,
            seed);
    }

    private static RunState SetUp(RunState state)
    {
        RunManager.Instance.SetUpNewSingleplayer(state, shouldSave: false);
        return state;
    }

    private static string Rooms(RunState state) =>
        string.Join(" | ", state.Acts.Select(static act =>
        {
            RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(act);
            return act.Id.Entry
                   + " boss=" + act.BossEncounter.Id.Entry
                   + " second=" + (act.SecondBossEncounter?.Id.Entry ?? "none")
                   + " normal=" + string.Join(",", rooms.normalEncounters.Select(static encounter => encounter.Id.Entry))
                   + " elite=" + string.Join(",", rooms.eliteEncounters.Select(static encounter => encounter.Id.Entry));
        }));

    private static string Hash(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..12];

    private static List<ModifierModel> RoundTrip(IReadOnlyList<ModifierModel> modifiers)
    {
        var writer = new PacketWriter();
        writer.WriteList(modifiers.Select(static modifier => modifier.ToSerializable()).ToList());
        var reader = new PacketReader();
        reader.Reset(writer.Buffer);
        return reader.ReadList<SerializableModifier>().Select(ModifierModel.FromSerializable).ToList();
    }

    private static SerializableRun FromJson(string json) =>
        SaveManager.FromJson<SerializableRun>(json).SaveData
        ?? throw new InvalidOperationException("Run JSON did not parse.");

    private static SerializableRun WithoutCarrier(SerializableRun save)
    {
        ModelId carrierId = ModelDb.Modifier<LibraryRunSettingsModifier>().Id;
        save.Modifiers = save.Modifiers.Where(modifier => modifier.Id != carrierId).ToList();
        return save;
    }

    private static void RequireCarrier(LibraryRunSettingsModifier? carrier, Settings expected, string label)
    {
        Require(carrier != null, label + ": no carrier");
        var actual = new Settings(carrier!.LibraryOfRuina_RunMonsterExtensionEnabled, carrier.LibraryOfRuina_RunResistanceMode);
        Row(label + " " + actual);
        Require(actual == expected, label + ": expected " + expected + " but was " + actual);
    }

    private static void SetLocal(Settings settings) => SetLocalRaw(settings.Extension, settings.Resistance);

    // setter 在局内拒绝写入并有 BGM 副作用，这里只改后备字段。
    private static void SetLocalRaw(bool extension, double resistance)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(LibraryOfRuinaSettings).GetField("_monsterExtensionEnabled", flags)!.SetValue(null, extension);
        typeof(LibraryOfRuinaSettings).GetField("_resistanceMode", flags)!.SetValue(null, resistance);
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

    private static void Row(string text) => Log.Info(LogPrefix + "ROW " + text);

    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
