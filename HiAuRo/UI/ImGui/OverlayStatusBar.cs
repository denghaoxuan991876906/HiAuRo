using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;
using HiAuRo.UI;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.ImGuiLib;

public sealed class OverlayStatusBar : OverlayBase
{
    private readonly Action _saveConfig;
    private Vector2 _lastExpandedSize = new(400, 320);
    private bool _wasExpanded;
    private string? _selectedTabId;

    protected override Vector2 ContentPadding => Vector2.Zero;
    protected override Vector2 ContentOffset => new(10, 8);

    private static readonly Vector2 _minExpandedSize = new(320, 180);

    public OverlayStatusBar(PluginConfig config, Action saveConfig) : base("HiAuRoStatusBar##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayStatusBarX, config.OverlayStatusBarY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(_config.OverlayStatusBarCollapsedW, _config.OverlayStatusBarCollapsedH),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _lastExpandedSize = new Vector2(
            Math.Max(config.OverlayStatusBarW, _minExpandedSize.X),
            Math.Max(config.OverlayStatusBarH, _minExpandedSize.Y));
    }

    protected override void OnPreDraw()
    {
        if (!_config.OverlayStatusBarExpanded)
        {
            _wasExpanded = false;
            ImGui.SetNextWindowSize(new Vector2(_config.OverlayStatusBarCollapsedW, _config.OverlayStatusBarCollapsedH));
        }
        else if (!_wasExpanded)
        {
            _wasExpanded = true;
            var size = new Vector2(
                Math.Max(_lastExpandedSize.X, _minExpandedSize.X),
                Math.Max(_lastExpandedSize.Y, _minExpandedSize.Y));
            ImGui.SetNextWindowSize(size);
        }
    }

    protected override void DrawContent()
    {
#if DEBUG
        var _uiTick = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
#endif
        var expanded = _config.OverlayStatusBarExpanded;
        var currentSize = ImGui.GetWindowSize();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = expanded ? _minExpandedSize : new Vector2(_config.OverlayStatusBarCollapsedW, _config.OverlayStatusBarCollapsedH),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        DrawBar();

        if (!expanded) return;

        _lastExpandedSize = currentSize;
        ImGui.Spacing();

        if (HiAuRo.Runtime.ACRLifecycle.CurrentEntry == null)
        {
            ImGui.SetCursorPosX(ContentOffset.X);
            ImGui.TextColored(Theme.Colors.TextSecondary, "等待 ACR 加载...");
            return;
        }

        DrawTabArea();
#if DEBUG
        }
        finally
        {
            PerfMonitor.Record("UI.StatusBar", _uiTick);
        }
#endif
    }

    private void DrawTabArea()
    {
        var acrTabs = ImGuiOverlayState.Controls.Where(c => c.Type == "tab").ToList();
        var entries = acrTabs.Select(t => (Id: t.Id, Label: t.Label))
            .Concat([(Id: "__qt", Label: "QT"), (Id: "__hk", Label: "热键")])
            .ToList();
        if (entries.Count == 0) return;

        // 用稳定 string id 跟踪选中，避免 ACR 切换职业时数组下标错位
        if (_selectedTabId == null || entries.All(e => e.Id != _selectedTabId))
            _selectedTabId = entries[0].Id;

        ImGui.SetCursorPosX(ContentOffset.X);
        var labels = entries.Select(e => e.Label).ToArray();
        var idx = entries.FindIndex(e => e.Id == _selectedTabId);
        CL.Tabs("##statusTabs", ref idx, labels);
        if (idx >= 0 && idx < entries.Count)
            _selectedTabId = entries[idx].Id;

        ImGui.Spacing();

        switch (_selectedTabId)
        {
            case "__qt":
                DrawQtTab();
                break;
            case "__hk":
                DrawHotkeyTab();
                break;
            default:
            {
                var imBuilder = new UiBuilderImpl(isImGui: true, activeTab: _selectedTabId);
                HiAuRo.Runtime.ACRLifecycle.CurrentEntry?.GetRotationUI()?.RegisterControls(imBuilder);
                break;
            }
        }
    }

    private static void DrawQtTab()
    {
        var settings = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        if (settings == null) return;

        CL.SectionHeader("显示控制");
        foreach (var qt in ImGuiOverlayState.Qts)
        {
            var vis = settings.QtVisible.GetValueOrDefault(qt.Id, true);
            if (CL.Switch($"qtvis_{qt.Id}", qt.Label, ref vis))
            {
                settings.QtVisible[qt.Id] = vis;
                HiAuRo.Runtime.ACRLifecycle.MarkSettingsDirty();
            }
        }
    }

    private static void DrawHotkeyTab()
    {
        var settings = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        if (settings == null) return;

        CL.SectionHeader("显示控制");
        foreach (var hk in ImGuiOverlayState.Hotkeys)
        {
            var hkId = "hk_" + hk.Label;
            var vis = settings.HkVisible.GetValueOrDefault(hkId, true);
            if (CL.Switch($"hkvis_{hkId}", hk.Label, ref vis))
            {
                settings.HkVisible[hkId] = vis;
                HiAuRo.Runtime.ACRLifecycle.MarkSettingsDirty();
            }
        }
    }

    private void DrawBar()
    {
        ImGui.SetCursorPos(ContentOffset);

        var isRunning = ImGuiOverlayState.IsRunning;
        var isPaused = ImGuiOverlayState.IsPaused;
        var acrName = ImGuiOverlayState.AcrName;

        var statusColor = (isRunning, isPaused) switch
        {
            (true, false) => Theme.Colors.AccentGreen,
            (true, true) => Theme.Colors.AccentOrange,
            _ => Theme.Colors.AccentRed,
        };
        var statusText = isRunning ? (isPaused ? "已暂停" : "运行中") : "已停止";

        var statusDot = isRunning ? "●" : "○";
        var label = $"{statusDot}  {acrName} · {statusText}";
        var labelW = ImGui.CalcTextSize(label).X;
        var center = ImGui.GetContentRegionAvail().X * 0.5f;
        ImGui.SetCursorPosX(Math.Max(ContentOffset.X, center - labelW * 0.5f));
        ImGui.TextColored(statusColor, label);

        ImGui.SetCursorPosX(ContentOffset.X);

        var btnSize = new Vector2(58, 36);

        if (isRunning && !isPaused)
        {
            if (CL.IconButton(CL.IconType.Stop, Theme.Colors.AccentRed, btnSize,
                    CL.IconButtonStyle.Fill, iconSizePx: 24f))
                Runtime.RuntimeCore.Stop();
            ImGui.SameLine(0, 4);
            if (CL.IconButton(CL.IconType.Pause, Theme.Colors.AccentOrange, btnSize,
                    CL.IconButtonStyle.Fill, iconSizePx: 24f))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else if (isPaused)
        {
            if (CL.IconButton(CL.IconType.Stop, Theme.Colors.AccentRed, btnSize,
                    CL.IconButtonStyle.Fill, iconSizePx: 24f))
                Runtime.RuntimeCore.Stop();
            ImGui.SameLine(0, 4);
            if (CL.IconButton(CL.IconType.Play, Theme.Colors.AccentBlue, btnSize,
                    CL.IconButtonStyle.Fill, iconSizePx: 24f))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else
        {
            if (CL.IconButton(CL.IconType.Play, Theme.Colors.AccentGreen, btnSize,
                    CL.IconButtonStyle.Fill, iconSizePx: 24f))
                Runtime.RuntimeCore.Start();
        }

        ImGui.SameLine(0, 4);

        if (CL.IconButton(CL.IconType.Save, Theme.Colors.TextSecondary, new Vector2(58, 36),
                CL.IconButtonStyle.Outline, iconSizePx: 24f))
            HiAuRo.ACR.MainControlHelper.Save();

        ImGui.SameLine(0, 4);

        var foldLabel = _config.OverlayStatusBarExpanded ? "▲" : "▼";
        if (CL.TextButton(foldLabel, new Vector2(38, 36)))
        {
            _config.OverlayStatusBarExpanded = !_config.OverlayStatusBarExpanded;
            _saveConfig();
        }

        ImGui.SameLine(0, 4);

        var panelsVisible = _config.QtPanelVisible && _config.HotkeyPanelVisible;
        var eyeColor = panelsVisible ? Theme.Colors.AccentBlue : Theme.Colors.TextTertiary;
        if (CL.IconButton(CL.IconType.Eye, eyeColor, new Vector2(38, 36),
                CL.IconButtonStyle.Outline, iconSizePx: 19f))
        {
            var newVal = !panelsVisible;
            _config.QtPanelVisible = newVal;
            _config.HotkeyPanelVisible = newVal;
            _saveConfig();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(panelsVisible ? "隐藏悬浮窗" : "显示悬浮窗");
    }

    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayStatusBarX = pos.X;
        _config.OverlayStatusBarY = pos.Y;
        if (_config.OverlayStatusBarExpanded)
        {
            var size = ImGui.GetWindowSize();
            _config.OverlayStatusBarW = size.X;
            _config.OverlayStatusBarH = size.Y;
        }
        _saveConfig();
    }
}
