using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// QT 芯片面板 — 独立窗口，自适应内容大小
/// </summary>
public sealed class OverlayQtPanel : OverlayBase
{
    private readonly Action _saveConfig;
    private readonly Dictionary<string, Vector4> _colorCache = [];
    private int _visibleCount;

    /// <summary>不允许缩放</summary>
    protected override bool AllowResize => false;
    /// <summary>无边距</summary>
    protected override Vector2 ContentPadding => Vector2.Zero;
    /// <summary>内容起始偏移</summary>
    protected override Vector2 ContentOffset => new(6, 6);

    /// <summary>Initializes a new instance of the <see cref="OverlayQtPanel"/> class</summary>
    public OverlayQtPanel(PluginConfig config, Action saveConfig) : base("HiAuRoQtPanel##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayQtPanelX, config.OverlayQtPanelY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(56, 38),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    /// <summary>预绘制时计算窗口尺寸</summary>
    protected override void OnPreDraw()
    {
        var qts = ImGuiOverlayState.Qts;
        var ui = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        _visibleCount = qts.Count(q => ui?.QtVisible.GetValueOrDefault(q.Id, true) ?? true);
        if (_visibleCount == 0) return;
        var cols = (ui?.QtCols ?? 0) > 0 ? ui!.QtCols : 4;
        var rows = (_visibleCount + cols - 1) / cols;
        var actualCols = Math.Min(_visibleCount, cols);
        var pad = ContentOffset.X;
        var gapX = 6f;
        var gapY = 4f;
        var btnW = 67f;
        var btnH = 46f;
        var w = pad * 2 + btnW * actualCols + gapX * (actualCols - 1);
        var h = pad * 2 + btnH * rows + gapY * (rows - 1);
        ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
    }

    /// <summary>绘制 QT 面板内容</summary>
    protected override void DrawContent()
    {
#if DEBUG
        var _uiTick = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var qts = ImGuiOverlayState.Qts;
        if (qts.Count == 0)
        {
            BeginContent();
            ImGui.TextColored(Theme.Colors.TextTertiary, "无 QT 开关");
            return;
        }

        BeginContent();
        if (_visibleCount == 0) return;

        var ui = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        if (ui == null) return;

        var cols = ui.QtCols;
        if (cols <= 0) cols = 4;
        var col = 0;
        var btnSize = new Vector2(67, 46);

        // 公共样式：圆角、内边距
        using var outerV = new ImRaii.StyleDisposable();
        outerV.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        outerV.Push(ImGuiStyleVar.FramePadding, new Vector2(10, 4));
        // 默认（非激活）颜色
        using var outerC = new ImRaii.ColorDisposable();
        outerC.Push(ImGuiCol.Button, Theme.Colors.FillSecondary);
        outerC.Push(ImGuiCol.ButtonHovered, Theme.Colors.FillSecondary);
        outerC.Push(ImGuiCol.ButtonActive, Theme.Colors.FillSecondary);
        outerC.Push(ImGuiCol.Text, Theme.Colors.TextSecondary);

        foreach (var qt in qts)
        {
            var visible = ui.QtVisible.GetValueOrDefault(qt.Id, true);
            if (!visible) continue;

            ImGui.PushID(qt.Id);

            if (qt.Value)
            {
                var activeColor = ResolveActiveColor(qt.Color);
                using var innerC = new ImRaii.ColorDisposable();
                innerC.Push(ImGuiCol.Button, activeColor);
                innerC.Push(ImGuiCol.ButtonHovered, activeColor);
                innerC.Push(ImGuiCol.ButtonActive, activeColor);
                innerC.Push(ImGuiCol.Text, Theme.Colors.TagActiveText);

                var clicked = ImGui.Button(qt.Label + "##" + qt.Id, btnSize);

                if (clicked)
                    HiAuRo.ACR.QTHelper.Toggle(qt.Id);
            }
            else
            {
                var clicked = ImGui.Button(qt.Label + "##" + qt.Id, btnSize);

                if (clicked)
                    HiAuRo.ACR.QTHelper.Toggle(qt.Id);
            }

            if (!string.IsNullOrEmpty(qt.Tooltip) && ImGui.IsItemHovered())
                ImGui.SetTooltip(qt.Tooltip);

            ImGui.PopID();
            SameLineOrWrap(ref col, cols);
        }

#if DEBUG
        PerfMonitor.Record("UI.QtPanel", _uiTick);
#endif
    }

    /// <summary>解析激活颜色（带缓存）</summary>
    private Vector4 ResolveActiveColor(string? colorHex)
    {
        if (string.IsNullOrEmpty(colorHex))
            return Theme.Colors.AccentGreen;

        if (_colorCache.TryGetValue(colorHex, out var cached))
            return cached;

        try
        {
            var hex = colorHex.StartsWith('#') ? colorHex[1..] : colorHex;
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);
            var color = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
            _colorCache[colorHex] = color;
            return color;
        }
        catch
        {
            return Theme.Colors.AccentGreen;
        }
    }

    /// <summary>保存窗口位置</summary>
    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayQtPanelX = pos.X;
        _config.OverlayQtPanelY = pos.Y;
        _saveConfig();
    }
}