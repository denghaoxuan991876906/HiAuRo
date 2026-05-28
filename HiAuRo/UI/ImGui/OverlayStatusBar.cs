using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;
using HiAuRo.UI;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 紧凑状态栏 + ACR 控制面板
/// 按钮即状态指示: Play=已停, Stop/Pause=运行中, Stop/Play=已暂停
/// 最小尺寸: 280×56 (紧凑到只放按钮)
/// </summary>
public sealed class OverlayStatusBar : OverlayBase
{
    private readonly Action _saveConfig;
    private Vector2 _lastExpandedSize = new(400, 320);
    private string _activeTabId = string.Empty;
    private int _tabBarVersion;
    private bool _wasExpanded;
    private List<UiControlDef> _allTabs = [];
    private List<UiControlDef>? _cachedControlsRef;
    private int _cachedControlsCount;

    /// <summary>无边距</summary>
    protected override Vector2 ContentPadding => Vector2.Zero;
    /// <summary>内容起始偏移</summary>
    protected override Vector2 ContentOffset => new(12, 10);

    private static readonly Vector2 _minExpandedSize = new(320, 180);

    private static readonly UiControlDef[] _builtinTabs =
    [
        new("__qt_setup__", "tab", null, "QT设置", null),
        new("__hk_setup__", "tab", null, "热键设置", null),
    ];

    /// <summary>Initializes a new instance of the <see cref="OverlayStatusBar"/> class</summary>
    public OverlayStatusBar(PluginConfig config, Action saveConfig) : base("HiAuRoStatusBar##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayStatusBarX, config.OverlayStatusBarY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 56),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    /// <summary>预绘制时设置窗口尺寸</summary>
    protected override void OnPreDraw()
    {
        if (!_config.OverlayStatusBarExpanded)
        {
            _wasExpanded = false;
            ImGui.SetNextWindowSize(new Vector2(320, 56));
        }
        else if (!_wasExpanded)
        {
            // 折叠→展开转场：取 Max(上次保存, 最小) 设一次，之后帧不再设允许用户自由调整
            _wasExpanded = true;
            var size = new Vector2(
                Math.Max(_lastExpandedSize.X, _minExpandedSize.X),
                Math.Max(_lastExpandedSize.Y, _minExpandedSize.Y));
            ImGui.SetNextWindowSize(size);
        }
    }

    /// <summary>绘制状态栏内容</summary>
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
            MinimumSize = expanded ? _minExpandedSize : new Vector2(320, 56),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        if (!expanded)
        {
            DrawBar();
            return;
        }

        // 用户每次手动调整大小都保存，下次展开恢复
        _lastExpandedSize = currentSize;

        DrawBar();
        ImGui.Spacing();

        var controls = ImGuiOverlayState.Controls;
        if (controls.Count == 0)
        {
            ImGui.SetCursorPosX(ContentOffset.X);
            ImGui.TextColored(Theme.Colors.TextSecondary, "等待 ACR 加载...");
            return;
        }

        // 仅在控件列表变化时重建 tab 缓存
        if (!ReferenceEquals(controls, _cachedControlsRef) || controls.Count != _cachedControlsCount)
        {
            _cachedControlsRef = controls;
            _cachedControlsCount = controls.Count;
            _allTabs = [.. controls.Where(c => c.Type == "tab"), .. _builtinTabs];
        }

        var allTabs = _allTabs;
        var activeTab = ImGuiOverlayState.ActiveTab;

        if (activeTab != _activeTabId)
            _tabBarVersion++;

        ImGui.SetCursorPosX(ContentOffset.X);
        if (ImGui.BeginTabBar($"##statusTabs_{_tabBarVersion}"))
        {
            foreach (var tab in allTabs)
            {
                if (!ImGui.BeginTabItem(tab.Label))
                    continue;

                _activeTabId = tab.Id;
                if (activeTab != tab.Id)
                    ImGuiOverlayState.ActiveTab = tab.Id;

                using var framePad = new ImRaii.StyleDisposable();
                framePad.Push(ImGuiStyleVar.FramePadding, Theme.PaddingSM);
                ImGui.Indent(ContentOffset.X);

                // 内容区域：水平滚动+宽度约束，防止控件溢出窗口
                var childSize = new Vector2(-1, -1);
                if (ImGui.BeginChild($"##acrContent_{tab.Id}", childSize, false,
                        ImGuiWindowFlags.None))
                {
                    if (tab.Id == "__qt_setup__")
                        DrawQtSetup();
                    else if (tab.Id == "__hk_setup__")
                        DrawHotkeySetup();
                    else
                        ImGuiWidgetRenderer.Render(controls, tab.Id);
                }
                ImGui.EndChild();

                ImGui.Unindent(ContentOffset.X);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
#if DEBUG
        }
        finally
        {
            PerfMonitor.Record("UI.StatusBar", _uiTick);
        }
#endif
    }

    private static void DrawQtSetup()
    {
        var settings = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        if (settings == null) return;
        var cols = settings.QtCols;
        if (cols <= 0) cols = 4;
        if (ComponentLibrary.SliderInt("__qtcols", "每行列数", ref cols, 1, 10))
            settings.QtCols = cols;

        ImGui.Spacing();
        ComponentLibrary.Label("可见性");
        using var textColor = new ImRaii.ColorDisposable();
        textColor.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        foreach (var qt in ImGuiOverlayState.Qts)
        {
            var vis = settings.QtVisible.GetValueOrDefault(qt.Id, true);
            if (ImGui.Checkbox(qt.Label, ref vis))
                settings.QtVisible[qt.Id] = vis;
        }
    }

    private static void DrawHotkeySetup()
    {
        var settings = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        if (settings == null) return;
        var cols = settings.HkCols;
        if (cols <= 0) cols = 5;
        if (ComponentLibrary.SliderInt("__hkcols", "每行列数", ref cols, 1, 10))
            settings.HkCols = cols;

        var btnSize = settings.HkBtnSize > 0 ? settings.HkBtnSize : 50;
        var btnSz = (int)btnSize;
        if (ComponentLibrary.SliderInt("__hkbtnsz", "按钮大小(px)", ref btnSz, 28, 80))
            settings.HkBtnSize = btnSz;

        ImGui.Spacing();
        ComponentLibrary.Label("可见性");
        using var textColor2 = new ImRaii.ColorDisposable();
        textColor2.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        foreach (var hk in ImGuiOverlayState.Hotkeys)
        {
            var vis = settings.HkVisible.GetValueOrDefault(hk.Id, true);
            if (ImGui.Checkbox(hk.Label, ref vis))
                settings.HkVisible[hk.Id] = vis;
        }
    }

    private void DrawBar()
    {
        ImGui.SetCursorPos(ContentOffset);

        var isRunning = ImGuiOverlayState.IsRunning;
        var isPaused = ImGuiOverlayState.IsPaused;
        var acrName = ImGuiOverlayState.AcrName;

        var btnSize = new Vector2(56, 38);

        // ── 左侧: ACR 名称 ──
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextSecondary, acrName);
        ImGui.SameLine(0, 10);

        // ── 按钮组 ──
        if (isRunning && !isPaused)
        {
            // 运行中 → Stop(红) + Pause(橙)
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Stop,
                    Theme.Colors.AccentRed, btnSize,
                    ComponentLibrary.IconButtonStyle.Fill, iconSizePx: 24f))
                Runtime.RuntimeCore.Stop();

            ImGui.SameLine(0, 6);

            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Pause,
                    Theme.Colors.AccentOrange, btnSize,
                    ComponentLibrary.IconButtonStyle.Fill, iconSizePx: 24f))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else if (isPaused)
        {
            // 已暂停 → Stop(红) + Play(绿)
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Stop,
                    Theme.Colors.AccentRed, btnSize,
                    ComponentLibrary.IconButtonStyle.Fill, iconSizePx: 24f))
                Runtime.RuntimeCore.Stop();

            ImGui.SameLine(0, 6);

            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Play,
                    Theme.Colors.AccentGreen, btnSize,
                    ComponentLibrary.IconButtonStyle.Fill, iconSizePx: 24f))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else
        {
            // 已停止 → Play(绿) + Pause(占位/禁用)
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Play,
                    Theme.Colors.AccentGreen, btnSize,
                    ComponentLibrary.IconButtonStyle.Fill, iconSizePx: 24f))
                Runtime.RuntimeCore.Start();

            ImGui.SameLine(0, 6);

            ImGui.BeginDisabled();
            ComponentLibrary.IconButton(ComponentLibrary.IconType.Pause,
                Theme.Colors.TextTertiary, btnSize,
                ComponentLibrary.IconButtonStyle.Outline);
            ImGui.EndDisabled();
        }

        ImGui.SameLine(0, 6);

        // 保存 (边框样式)
        if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Save,
                Theme.Colors.TextSecondary, btnSize,
                    ComponentLibrary.IconButtonStyle.Outline, iconSizePx: 24f))
            HiAuRo.ACR.MainControlHelper.Save();

        ImGui.SameLine(0, 6);

        // 折叠/展开 (边框样式，毛玻璃上可见)
        var foldIcon = _config.OverlayStatusBarExpanded
            ? ComponentLibrary.IconType.ChevronUp
            : ComponentLibrary.IconType.ChevronDown;
        if (ComponentLibrary.IconButton(foldIcon,
                Theme.Colors.AccentOrange, new Vector2(56, 38),
                    ComponentLibrary.IconButtonStyle.Outline, iconSizePx: 24f))
        {
            _config.OverlayStatusBarExpanded = !_config.OverlayStatusBarExpanded;
            _saveConfig();
        }
    }

    /// <summary>保存窗口位置</summary>
    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayStatusBarX = pos.X;
        _config.OverlayStatusBarY = pos.Y;
        _saveConfig();
    }
}