using System;
using System.Linq;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.features.settings;

/// <summary>
/// 影响玩法的设置按局固定：局内读 <see cref="LibraryRunSettingsModifier"/>，局外（主菜单、图鉴）读本地设置。
/// <para>
/// 载体从哪里来（补丁见 <c>LibraryRunSettingsPatches</c>）：
/// </para>
/// <list type="bullet">
/// <item>新局：房主（单人即本机）在 <c>StartRunLobby.BeginRunForAllPlayers</c> 按本地设置追加载体，原版把这份列表
/// 写进发给客户端的 <c>LobbyBeginRunMessage</c>，再交给本机的 <c>BeginRunLocally</c>。各端在 <c>BeginRunLocally</c>
/// 把载体取出暂存（标准模式的选角界面遇到非空列表会报错并丢弃全部修改器），由同一种子的
/// <c>RunState.CreateForNewRun</c> 接回。不经过大厅直接建局的路径（验证套件、调试开局）用本地设置。</item>
/// <item>读档：存档里有载体就用存档的。没有（v0.21.2 及更早的存档）时在 <c>RunState.FromSerializable</c> 后按
/// 此刻的本地设置补建。联机读档时房主先用 <c>CanonicalizeSave</c> 规范化存档（其中调用 FromSerializable），
/// 补建的载体因此写进发给客户端的存档，客户端读到的是房主的值；之后的存档里都有它，只补建这一次。</item>
/// </list>
/// 进程启动时没有注入内容（设置关闭或检测到不兼容模组）的一端不装这些补丁，与注入的一端混联不受支持。
/// </summary>
internal static class LibraryRunSettings
{
    private const string LogPrefix = "[LibraryOfRuina.RunSettings] ";

    // BeginRunLocally 取出、CreateForNewRun 接走之间的载体。两者之间隔着选角界面的转场（await），
    // 按种子配对，开局失败留下的旧值会被下一次开局覆盖或在离开本局时清掉。
    private static PendingCarrier? _pendingLobbyCarrier;

    private sealed record PendingCarrier(string Seed, LibraryRunSettingsModifier Carrier);

    /// <summary>当前局是否启用废墟图书馆内容；没有局时为本地设置。</summary>
    internal static bool MonsterExtensionEnabled =>
        IsMonsterExtensionEnabled(RunManager.Instance.DebugOnlyGetState());

    internal static bool IsMonsterExtensionEnabled(IRunState? runState) =>
        Find(runState)?.MonsterExtensionEnabled ?? LibraryOfRuinaSettings.MonsterExtensionEnabled;

    /// <summary>
    /// 读档时 RunState 还没建出来的读取点用这个。存档里没有载体时返回本地设置，与随后补建的值相同，
    /// 所以与补建补丁谁先执行无关。
    /// </summary>
    internal static bool IsMonsterExtensionEnabled(SerializableRun save) =>
        Find(save)?.MonsterExtensionEnabled ?? LibraryOfRuinaSettings.MonsterExtensionEnabled;

    internal static LibraryRunSettingsModifier? Find(IRunState? runState) =>
        runState?.Modifiers.OfType<LibraryRunSettingsModifier>().FirstOrDefault();

    internal static LibraryRunSettingsModifier? Find(SerializableRun save)
    {
        ModelId carrierId = ModelDb.Modifier<LibraryRunSettingsModifier>().Id;
        SerializableModifier? entry = save.Modifiers?.FirstOrDefault(modifier => modifier.Id == carrierId);
        return entry == null ? null : ModifierModel.FromSerializable(entry) as LibraryRunSettingsModifier;
    }

    internal static List<ModifierModel> WithHostCarrier(IEnumerable<ModifierModel> modifiers) =>
        modifiers
            .Where(static modifier => modifier is not LibraryRunSettingsModifier)
            .Append(LibraryRunSettingsModifier.CreateFromLocalSettings())
            .ToList();

    /// <summary>
    /// 从开局消息的修改器列表里取出载体暂存，返回不含载体的列表交给原版界面。列表里没有载体
    /// （开局的一端没有注入内容）时不暂存，新局退回本地设置。
    /// </summary>
    internal static List<ModifierModel> TakeLobbyCarrier(string seed, List<ModifierModel> modifiers)
    {
        LibraryRunSettingsModifier? carrier = modifiers.OfType<LibraryRunSettingsModifier>().LastOrDefault();
        _pendingLobbyCarrier = carrier == null ? null : new PendingCarrier(seed, carrier);
        return carrier == null
            ? modifiers
            : modifiers.Where(static modifier => modifier is not LibraryRunSettingsModifier).ToList();
    }

    internal static void AttachToNewRun(RunState runState, string seed)
    {
        PendingCarrier? pending = _pendingLobbyCarrier;
        _pendingLobbyCarrier = null;
        if (Find(runState) != null)
        {
            return;
        }

        bool fromLobby = pending != null && string.Equals(pending.Seed, seed, StringComparison.Ordinal);
        LibraryRunSettingsModifier carrier = fromLobby
            ? pending!.Carrier
            : LibraryRunSettingsModifier.CreateFromLocalSettings();
        Attach(runState, carrier, fromLobby ? "new run, lobby" : "new run, local settings");
    }

    internal static void BackfillLoadedRun(RunState runState)
    {
        if (Find(runState) == null)
        {
            Attach(runState, LibraryRunSettingsModifier.CreateFromLocalSettings(), "loaded run without carrier, backfilled from local settings");
        }
    }

    internal static void ApplyToRun(LibraryRunSettingsModifier carrier)
    {
        LibraryResistanceModeState.Current = carrier.ResistanceMode;
    }

    /// <summary>离开本局时调用：抗性回到本地设置，丢掉没被接走的开局载体。</summary>
    internal static void OnRunCleaningUp()
    {
        _pendingLobbyCarrier = null;
        LibraryResistanceModeState.Current =
            LibraryOfRuinaSettings.ToLibraryResistanceMode(LibraryOfRuinaSettings.ResistanceSettingLevel);
    }

    private static void Attach(RunState runState, LibraryRunSettingsModifier carrier, string source)
    {
        if (!VanillaPrivate.RunStateModifiers.Set(runState, runState.Modifiers.Append(carrier).ToList()))
        {
            // 挂不上时各读取点退回本地设置；联机两端的本地设置不同就会分叉，所以记错误。
            Log.Error(LogPrefix + "RunState.Modifiers backing field is unavailable; run settings fall back to local settings.");
            return;
        }

        Log.Info(LogPrefix + "Run settings (" + source + "): monsterExtension="
                 + carrier.MonsterExtensionEnabled
                 + ", resistance="
                 + carrier.LibraryOfRuina_RunResistanceMode
                 + ".");
    }
}
