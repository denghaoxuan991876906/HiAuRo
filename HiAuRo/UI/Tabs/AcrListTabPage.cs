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
        : base("ACR 列表", "acr_list", IconHelper.Icons.Settings)
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
            return;
        }

        var currentJob = Data.Me.ClassJob;
        if (currentJob == 0)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "非战斗职业，ACR 不可用");
            return;
        }

        var registered = ACRLifecycle.GetRegisteredAcrs(currentJob);
        if (registered.Count == 0)
        {
            ComponentLibrary.SectionHeader("当前职业");
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前职业无可用 ACR");
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

    }

    private static string DisplayAcrType(AcrType type) => type switch
    {
        AcrType.PvE => "PvE",
        AcrType.PvP => "PvP",
        AcrType.通用 => "通用",
        _ => type.ToString()
    };
}
