using HiAuRo.ACR;
using HiAuRo.ImGuiLib;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;

namespace HiAuRo.UI.Tabs;

public sealed class AcrListTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public AcrListTabPage(PluginConfig config, Action saveConfig)
        : base("当前 ACR", "acr_list", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();

        if (!HiAuRo.Data.IsReady)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "等待角色加载...");
            DrawRuntimeDebugToggle();
            return;
        }

        var currentJob = Data.Me.ClassJob;
        if (currentJob == 0)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "非战斗职业，ACR 不可用");
            DrawRuntimeDebugToggle();
            return;
        }

        var registered = ACRLifecycle.GetRegisteredAcrs(currentJob);
        if (registered.Count == 0)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前职业无可用 ACR");
            DrawRuntimeDebugToggle();
            return;
        }

        // ── Card 1: 当前职业 ──
        ComponentLibrary.SectionHeader("当前职业");

        JobIconHelper.DrawJobIcon(currentJob, 32f);
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8);
        ImGui.Text(((Jobs)currentJob).ToString());
        ImGui.Spacing();

        var rotation = ACRLifecycle.Runner.CurrentRotation;
        var activeIndex = ACRLifecycle.GetActiveAcrIndex(currentJob);

        var items = registered
            .Where(r => r.Entry != null)
            .Select(r => $"{r.Entry.AuthorName} ({DisplayAcrType(r.Entry.AcrType)})")
            .ToArray();
        var previewValue = items[Math.Clamp(activeIndex, 0, items.Length - 1)];

        var acrIndex = activeIndex;
        if (ComponentLibrary.Select("acrSelector", "当前 ACR", ref acrIndex, items))
            ACRLifecycle.SetActiveAcr(currentJob, acrIndex);

        ImGui.Spacing();

        if (rotation != null)
        {
            ImGui.Text($"等级区间: Lv.{rotation.MinLevel} - Lv.{rotation.MaxLevel}");
            ImGui.Spacing();
            ImGui.Text($"互娱类型: {DisplayAcrType(ACRLifecycle.CurrentEntry?.AcrType ?? AcrType.PvE)}");
            ImGui.Spacing();
            if (!string.IsNullOrEmpty(rotation.Description))
            {
                ImGui.Text("简介:");
                ImGui.TextWrapped(rotation.Description);
            }
        }
        else
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "ACR 未加载");
        }

        DrawRuntimeDebugToggle();
    }

    private void DrawRuntimeDebugToggle()
    {
        ImGui.Spacing();
        ComponentLibrary.Divider();
        ImGui.Spacing();

        var acrRuntimeDebug = _config.AcrRuntimeDebugEnabled;
        if (ComponentLibrary.Switch("acr_runtime_debug", "输出 ACR 运行时调试日志", ref acrRuntimeDebug))
        {
            _config.AcrRuntimeDebugEnabled = acrRuntimeDebug;
            _saveConfig();
        }
        ImGui.TextColored(Theme.Colors.TextTertiary,
            "控制 [SpellCast] / [Action] / [SlotExec] / [EventSystem] 这组细粒度日志，默认关闭。");
    }

    private static string DisplayAcrType(AcrType type) => type switch
    {
        AcrType.PvE => "PvE",
        AcrType.PvP => "PvP",
        AcrType.通用 => "通用",
        _ => type.ToString()
    };
}
