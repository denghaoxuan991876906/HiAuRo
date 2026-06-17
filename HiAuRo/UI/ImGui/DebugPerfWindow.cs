#if DEBUG
using System.Numerics;
using System.Diagnostics;
using Dalamud.Interface.Windowing;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// DEBUG 性能监控窗口 —— 显示各模块每帧耗时(μs)
/// </summary>
public sealed class DebugPerfWindow : Window
{
    /// <summary>全局单例，供 /hi debug 命令调用</summary>
    public static DebugPerfWindow? Instance { get; private set; }

    private long _perfDrawStart;
    // 复用快照字典，避免每帧 new 分配
    private readonly Dictionary<string, double> _snapshot = [];

    public DebugPerfWindow() : base("HiAuRo 性能监控##Debug")
    {
        Instance = this;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        IsOpen = false;
    }

    public override void PreDraw()
    {
        _perfDrawStart = Stopwatch.GetTimestamp();
        base.PreDraw();
    }

    public override void PostDraw()
    {
        base.PostDraw();
        PerfMonitor.Record("UI.PerfMon", _perfDrawStart);
    }

    public override void Draw()
    {
        using var _ = Theme.PushThemeScope();

        // 先快照上一帧数据（引用会被 BeginFrame 清掉，复用字典避免分配）
        _snapshot.Clear();
        foreach (var kv in PerfMonitor.LastFrame)
            _snapshot[kv.Key] = kv.Value;
        var total = PerfMonitor.TotalUs;
        var maxData = PerfMonitor.Max;
        PerfMonitor.BeginFrame();

        if (_snapshot.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "等待数据... (启动 ACR 后采集)");
            return;
        }

        var sorted = _snapshot.OrderByDescending(kv => kv.Value).ToList();

        if (ImGui.BeginTable("perfTable", 4,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("模块", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("耗时(μs)", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("占比", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("最大(μs)", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            foreach (var kv in sorted)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(kv.Key);
                ImGui.TableNextColumn();
                ImGui.Text($"{kv.Value:F1}");
                ImGui.TableNextColumn();
                var pct = total > 0 ? kv.Value / total * 100 : 0;
                ImGui.Text($"{pct:F1}%");
                ImGui.TableNextColumn();
                maxData.TryGetValue(kv.Key, out var maxVal);
                ImGui.Text($"{maxVal:F1}");
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Separator();
            ImGui.Text("总计");
            ImGui.TableNextColumn();
            ImGui.Text($"{total:F1}");
            ImGui.TableNextColumn();
            ImGui.Text("100%");
            ImGui.TableNextColumn();
            ImGui.Text("");

            ImGui.EndTable();
        }

        // 计算未计量开销（排除监控窗口自身）
        double perfSelf = _snapshot.GetValueOrDefault("UI.PerfMon", 0);
        double tickTotal = _snapshot.GetValueOrDefault("Tick.Total", 0);
        double uiTotal = _snapshot.GetValueOrDefault("UI.Total", 0);
        double accountedOther = 0;
        foreach (var kv in _snapshot)
        {
            if (kv.Key is "Tick.Total" or "UI.Total" or "UI.PerfMon") continue;
            accountedOther += kv.Value;
        }
        double unaccounted = tickTotal + uiTotal - accountedOther;

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1),
            $"监控自身: {perfSelf:F1}μs  |  框架缺口: {unaccounted:F1}μs");
        ImGui.Spacing();
        if (ImGui.Button("重置最大值"))
            PerfMonitor.ResetMax();
    }
}
#endif
