using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.core.settings;

/// <summary>
/// 联机时两端是否注入了本模组内容必须一致。是否注入在进程启动时按本地设置决定（关闭“启用废墟图书馆内容”启动，
/// 或检测到不兼容模组），未注入的一端不装任何玩法补丁，本局设置载体救不了它，只能诊断并退出。
/// <para>
/// 判定依据是房主有没有发来 <see cref="LibraryRunSettingsModifier"/>：注入的房主开新局时一定追加载体，
/// 读档时一定在规范化存档时补建；未注入的房主不追加，并在规范化时把存档里残留的载体去掉
/// （局是注入时开的、之后关掉内容再读档），所以“收到载体”与“房主已注入”一一对应。
/// 不一致时客户端在原版建局入口里抛出 <see cref="LibraryInjectionMismatchException"/>：
/// 原版各开局、读档界面都在 try 里调用这些入口，catch 里断开大厅并回到主菜单，弹窗显示异常信息。
/// </para>
/// <para>
/// 值的开关与否不影响判定：房主注入但关闭内容时，初始化登记的卡池（例如加入无色卡池的卡）和玩法补丁仍在，
/// 与未注入的一端同样会分叉。
/// </para>
/// </summary>
internal static class LibraryRunInjectionGuard
{
    private const string LocTable = "settings_ui";
    private const string HostInjectedKey = "LIBRARYOFRUINA-INJECTION_MISMATCH.host_injected";
    private const string HostNotInjectedKey = "LIBRARYOFRUINA-INJECTION_MISMATCH.host_not_injected";

    // 客户端 BeginRunLocally 看到的开局消息里有没有载体，由随后的 SetUpNewMultiplayer 取走。
    private static bool? _lobbyHostInjected;

    /// <summary>未注入模式只装这几个类；注入模式下它们作为普通补丁由 LibraryPatcher 安装。</summary>
    internal static void PatchForNonInjectedProcess(Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(LibraryRunSettingsBeginRunLocallyPatch)).Patch();
        harmony.CreateClassProcessor(typeof(LibraryRunInjectionNewRunGuardPatch)).Patch();
        harmony.CreateClassProcessor(typeof(LibraryRunInjectionLoadGuardPatch)).Patch();
        harmony.CreateClassProcessor(typeof(LibraryRunInjectionCanonicalizePatch)).Patch();
    }

    internal static void RecordLobbyHost(bool hostSentCarrier) => _lobbyHostInjected = hostSentCarrier;

    internal static bool? TakeLobbyHost()
    {
        bool? recorded = _lobbyHostInjected;
        _lobbyHostInjected = null;
        return recorded;
    }

    internal static bool HasCarrier(SerializableRun save)
    {
        ModelId carrierId = ModelDb.Modifier<LibraryRunSettingsModifier>().Id;
        return save.Modifiers?.Any(modifier => modifier.Id == carrierId) == true;
    }

    /// <summary>两端一致时返回 null；否则返回要抛出的异常（文案按房主那一侧的状态选）。</summary>
    internal static LibraryInjectionMismatchException? Check(bool localInjected, bool hostInjected, string when)
    {
        if (localInjected == hostInjected)
        {
            return null;
        }

        string key = hostInjected ? HostInjectedKey : HostNotInjectedKey;
        Log.Error("[LibraryOfRuina.RunSettings] Content injection differs from the host (" + when + "): host="
                  + hostInjected + ", local=" + localInjected + ". Leaving the run.");
        return new LibraryInjectionMismatchException(new LocString(LocTable, key).GetFormattedText());
    }

    /// <summary>未注入的房主规范化联机存档时去掉载体，让注入的客户端能认出房主没有注入。</summary>
    internal static void StripCarrier(SerializableRun save)
    {
        if (!HasCarrier(save))
        {
            return;
        }

        ModelId carrierId = ModelDb.Modifier<LibraryRunSettingsModifier>().Id;
        save.Modifiers = save.Modifiers.Where(modifier => modifier.Id != carrierId).ToList();
        Log.Info("[LibraryOfRuina.RunSettings] Content is not injected; removed the run settings carrier from the multiplayer save sent to clients.");
    }
}

/// <summary>消息就是原版错误弹窗里显示的说明，已按当前语言取好。</summary>
public sealed class LibraryInjectionMismatchException(string message) : InvalidOperationException(message);
