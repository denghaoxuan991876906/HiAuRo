using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 独立日志窗口 — 从主界面 tab 移出，可浮动/拖动，状态栏图标打开。
/// </summary>
public sealed class LogWindow : Window
{
    private string _logFilter = "";

    public LogWindow() : base("HiAuRo 日志##LogWindow")
    {
        Size = new Vector2(600, 360);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        IsOpen = false;
    }

    public override void Draw()
    {
        using var _ = Theme.PushThemeScope();

        var entries = LogManager.Instance.GetEntries();
        var filtered = string.IsNullOrEmpty(_logFilter)
            ? entries
            : entries.Where(e => e.Type.Contains(_logFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##LogFilter", "筛选类型...", ref _logFilter, 64);
        ImGui.SameLine();
        ImGui.TextDisabled($"({filtered.Count}/{entries.Count})");
        ImGui.SameLine();

        if (ComponentLibrary.DangerButton("清除"))
            LogManager.Instance.Clear();

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.BeginTable("##LogTable", 3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY,
            new Vector2(-1, -1)))
        {
            ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("类型", ImGuiTableColumnFlags.WidthFixed, 220);
            ImGui.TableSetupColumn("内容", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            var count = filtered.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                var e = filtered[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.Text(e.Timestamp.ToString("HH:mm:ss.fff"));

                ImGui.TableSetColumnIndex(1);
                if (ImGui.Selectable(e.Type))
                    _logFilter = _logFilter == e.Type ? "" : e.Type;

                ImGui.TableSetColumnIndex(2);
                ImGui.TextWrapped(e.Content);
            }

            ImGui.EndTable();
        }
    }
}
