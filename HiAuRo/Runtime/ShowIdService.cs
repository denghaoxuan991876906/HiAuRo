using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using HiAuRo.Infrastructure;
using Lumina.Text.ReadOnly;
using OmenTools;
using OmenTools.OmenService;

namespace HiAuRo.Runtime;

/// <summary>
///     在物品 / 技能悬浮提示上追加显示其 ID（参考 SimpleTweaksPlugin 的 Show ID）。
///     复用 OmenTools 的 TooltipManager 完成原生 tooltip 文本注入，无需自行 hook addon。
/// </summary>
public static class ShowIdService
{
    private static bool _initialized;

    public static unsafe void Init()
    {
        if (_initialized)
            return;

        var tm = DService.Instance().GetOmenService<TooltipManager>();
        if (tm is null)
            return;

        tm.RegItem((_, itemId, ref mods) =>
        {
            if (!PluginConfig.Instance.ShowItemIdEnabled)
                return;

            var cfg = PluginConfig.Instance;
            mods.Add(new TooltipItemModification
            {
                Target = TooltipItemType.UICategory,
                Type   = TooltipModificationType.Append,
                Text   = new ReadOnlySeString($" [{FormatId(itemId, cfg.ShowItemIdHex, cfg.ShowItemIdBoth)}]"),
            });
        });

        tm.RegAction((_, actionId, ref mods) =>
        {
            if (!PluginConfig.Instance.ShowItemIdEnabled)
                return;

            var cfg      = PluginConfig.Instance;
            var resolved = ActionManager.Instance()->GetAdjustedActionId(actionId);

            var text = " [";
            if (cfg.ShowItemIdResolvedActionId)
                text += FormatId(resolved, cfg.ShowItemIdHex, cfg.ShowItemIdBoth);

            if (cfg.ShowItemIdOriginalActionId && cfg.ShowItemIdResolvedActionId && resolved != actionId)
            {
                text += " (";
                text += FormatId(actionId, cfg.ShowItemIdHex, cfg.ShowItemIdBoth);
                text += ")";
            }

            text += "]";

            mods.Add(new TooltipActionModification
            {
                Target = TooltipActionType.Category,
                Type   = TooltipModificationType.Append,
                Text   = new ReadOnlySeString(text),
            });
        });

        _initialized = true;
    }

    private static string FormatId(uint id, bool hex, bool both)
    {
        if (!hex)
            return id.ToString();

        return both ? $"{id} - 0x{id:X}" : $"0x{id:X}";
    }
}
