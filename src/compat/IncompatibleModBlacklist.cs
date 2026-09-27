using System;
using System.Collections.Generic;

namespace LibraryOfRuina.compat;

/// <summary>
/// 已知与本模组不兼容的第三方模组黑名单。命中任意一项时本次启动不注入任何内容，
/// 并在主菜单弹窗列出原因（见 <see cref="IncompatibleModGuard"/>）。
/// 新增条目：在 <see cref="Entries"/> 追加 (清单 id, 原因本地化键)，并在四语 settings_ui.json 补充该原因文本。
/// </summary>
internal static class IncompatibleModBlacklist
{
    /// <param name="ModId">对方 manifest 中的 id，忽略大小写。</param>
    /// <param name="ReasonLocKey">settings_ui 表中的原因文本键。</param>
    internal sealed record Entry(string ModId, string ReasonLocKey);

    internal static readonly IReadOnlyList<Entry> Entries =
    [
        // HextechRunes：NeurosurgeUpgradeRune.TypePatch 为 NeurosurgePower.Type 追加 Postfix 并无条件读取 Owner，
        // 规范模型上抛 CanonicalModelException，导致书页奖励与三选一界面卡死。
        new("HextechRunes", "LIBRARYOFRUINA-INCOMPATIBLE_MOD_REASON.HEXTECH_RUNES"),
    ];

    internal static Entry? Find(string? modId)
    {
        if (string.IsNullOrEmpty(modId))
        {
            return null;
        }

        foreach (Entry entry in Entries)
        {
            if (string.Equals(entry.ModId, modId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }
}
