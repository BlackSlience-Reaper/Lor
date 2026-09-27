using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace LibraryOfRuina.compat;

/// <summary>
/// 按 <see cref="IncompatibleModBlacklist"/> 检测已加载的第三方模组。命中时不注入任何内容
/// （走与“启用废墟图书馆内容”关闭时相同的路径），并在进入主菜单后弹窗告知玩家。
/// </summary>
internal static class IncompatibleModGuard
{
    private const string NoticeTable = "settings_ui";

    private const string NoticeTitleKey = "LIBRARYOFRUINA-INCOMPATIBLE_MOD_NOTICE.title";

    private const string NoticeBodyKey = "LIBRARYOFRUINA-INCOMPATIBLE_MOD_NOTICE.body";

    private const string NoticeItemKey = "LIBRARYOFRUINA-INCOMPATIBLE_MOD_NOTICE.item";

    private sealed record BlockingMod(string Label, string ReasonLocKey);

    private static readonly List<BlockingMod> BlockingMods = [];

    private static bool noticeShown;

    internal static bool IsBlocked => BlockingMods.Count > 0;

    /// <summary>
    /// 在本模组初始化时调用。ModManager 此时已完成清单读取、禁用剔除和排序，
    /// 因此无论对方加载顺序在前还是在后都能检测到。
    /// </summary>
    internal static bool DetectBlockingMods()
    {
        BlockingMods.Clear();
        foreach (Mod mod in ModManager.Mods)
        {
            if (mod.state is ModLoadState.Disabled or ModLoadState.DisabledDuplicate or ModLoadState.Failed)
            {
                continue;
            }

            IncompatibleModBlacklist.Entry? entry = IncompatibleModBlacklist.Find(mod.manifest?.id);
            if (entry == null)
            {
                continue;
            }

            BlockingMods.Add(new BlockingMod(DescribeMod(mod, entry.ModId), entry.ReasonLocKey));
        }

        if (!IsBlocked)
        {
            return false;
        }

        Log.Error("[LibraryOfRuina] Incompatible mod(s) detected: "
            + string.Join("; ", BlockingMods.ConvertAll(blocking => blocking.Label))
            + ". Library content injection is skipped for this session; only the settings UI is registered.");
        return true;
    }

    private static string DescribeMod(Mod mod, string id)
    {
        string name = mod.manifest?.name ?? id;
        string version = mod.manifest?.version ?? mod.version?.ToString() ?? "?";
        return name == id
            ? $"{id} ({version})"
            : $"{name} [{id}] ({version})";
    }

    internal static void ShowNoticeOnce()
    {
        if (!IsBlocked || noticeShown || NModalContainer.Instance == null)
        {
            return;
        }

        string title = new LocString(NoticeTable, NoticeTitleKey).GetFormattedText();
        string itemTemplate = new LocString(NoticeTable, NoticeItemKey).GetFormattedText();
        var items = new StringBuilder();
        foreach (BlockingMod blocking in BlockingMods)
        {
            string reason = new LocString(NoticeTable, blocking.ReasonLocKey).GetFormattedText();
            items.Append(itemTemplate
                    .Replace("{mod}", blocking.Label, StringComparison.Ordinal)
                    .Replace("{reason}", reason, StringComparison.Ordinal))
                .Append('\n');
        }

        string body = new LocString(NoticeTable, NoticeBodyKey)
            .GetFormattedText()
            .Replace("{mods}", items.ToString().TrimEnd('\n'), StringComparison.Ordinal);
        NErrorPopup? popup = NErrorPopup.Create(title, body, showReportBugButton: false);
        if (popup == null)
        {
            return;
        }

        NModalContainer.Instance.Add(popup);
        noticeShown = true;
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuIncompatibleModNoticePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!IncompatibleModGuard.IsBlocked)
        {
            return;
        }

        Callable.From(IncompatibleModGuard.ShowNoticeOnce).CallDeferred();
    }
}
