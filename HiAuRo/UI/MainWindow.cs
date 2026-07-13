using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Windowing;
using HiAuRo.ACR;
using HiAuRo.ImGuiLib;
using HiAuRo.ImGuiLib.Effects;
using HiAuRo.Runtime;
using HiAuRo.UI.Tabs;
using OmenTools.Dalamud;

namespace HiAuRo.UI;

public sealed class MainWindow : Window
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;
    private readonly List<TabPageBase> _tabPages;

    public MainWindow(PluginConfig config, Action saveConfig) : base("HiAuRo##Main")
    {
        _config = config;
        _saveConfig = saveConfig;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 200), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        IsOpen = false;

        _particleSystem = new ParticleSystem(60, 6f);
        _rippleCanvas = new RippleCanvas(6, 2f);
        _clickRipple = new ClickRippleEffect(8);
        _matrixRain = new MatrixRainEffect(40);
        _geometricGlow = new GeometricGlowEffect();
        _firefly = new FireflyEffect();
        _constellation = new ConstellationEffect();
        _leyLines = new LeyLinesEffect();

        _tabPages =
        [
            new StatusTabPage(_config, _saveConfig),
            new SettingsTabPage(_config, _saveConfig),
            new CombatSettingsTabPage(_config, _saveConfig),
            new CommandHelpTabPage(),
            new DebugTabPage(),
            new AcrInstalledTabPage(),
            new AcrSourcesTabPage(),
            new AcrListTabPage(_config, _saveConfig),
            new AcrDebugTabPage(_config, _saveConfig),
            new TimelineTabPage(_config, _saveConfig),
            new CombatRecorderTabPage(_config, _saveConfig),
            new VfxTabPage(),
        ];
    }

#if DEBUG
    private long _mwDrawStart;

    public override void PreDraw()
    {
        _mwDrawStart = System.Diagnostics.Stopwatch.GetTimestamp();
        base.PreDraw();
    }

    public override void PostDraw()
    {
        base.PostDraw();
        PerfMonitor.Record("UI.MainWindow", _mwDrawStart);
    }
#endif

    // ── 背景特效 ──

    private readonly ParticleSystem _particleSystem;
    private readonly RippleCanvas _rippleCanvas;
    private readonly ClickRippleEffect _clickRipple;
    private readonly MatrixRainEffect _matrixRain;
    private readonly GeometricGlowEffect _geometricGlow;
    private readonly FireflyEffect _firefly;
    private readonly ConstellationEffect _constellation;
    private readonly LeyLinesEffect _leyLines;

    // ── 布局状态字段 ──

    private int _selectedCardIndex;
    private int _selectedTabIndex;
    private int _lastCardIndex = -1;
    private string? _selectedPluginName;

    private const string _version = "1.0.0";

    // ── 固定模块定义 ──

    private static IReadOnlyList<ModuleDefinition> Modules => MainWindowNavigation.Modules;

    // ── 绘制入口 ──

    public override void Draw()
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(620, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };

        using var _ = Theme.PushThemeScope();

        ComponentLibrary.GlassBackground(Theme.RadiusMD);

        var dt = ImGui.GetIO().DeltaTime;
        var winMin = ImGui.GetWindowPos();
        var winMax = winMin + ImGui.GetWindowSize();
        var dl = ImGui.GetWindowDrawList();

        switch (_config.BgEffect)
        {
            case BgEffectMode.None:
                break;

            case BgEffectMode.Nebula:
                GradientOverlay.DrawThemeGradient(dl, winMin, winMax, 24);
                _particleSystem.Update(dt, winMin, winMax);
                _rippleCanvas.Update(dt, winMin, winMax);
                _clickRipple.Update(dt);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    var mp = ImGui.GetMousePos();
                    if (mp.X >= winMin.X && mp.X <= winMax.X && mp.Y >= winMin.Y && mp.Y <= winMax.Y)
                        _clickRipple.Trigger(mp);
                }
                break;

            case BgEffectMode.MatrixRain:
                _matrixRain.Update(dt, winMin, winMax);
                break;

            case BgEffectMode.GeometricGlow:
                _geometricGlow.Update(dt, winMin, winMax);
                break;

            case BgEffectMode.Firefly:
                _firefly.Update(dt, winMin, winMax);
                break;

            case BgEffectMode.Constellation:
                _constellation.Update(dt, winMin, winMax);
                break;

            case BgEffectMode.LeyLines:
                _leyLines.Update(dt, winMin, winMax);
                break;
        }

        using var gc = new ImRaii.ColorDisposable();
        gc.Push(ImGuiCol.WindowBg, Theme.Colors.BgLayout);
        gc.Push(ImGuiCol.ChildBg, Theme.Colors.BgContainer);
        gc.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        gc.Push(ImGuiCol.TextDisabled, Theme.Colors.TextTertiary);
        gc.Push(ImGuiCol.FrameBg, Theme.Colors.FillSecondary);
        gc.Push(ImGuiCol.Border, Theme.Colors.BorderSecondary);
        gc.Push(ImGuiCol.Separator, Theme.Colors.BorderSecondary);
        gc.Push(ImGuiCol.Header, Theme.Colors.BgHover);
        gc.Push(ImGuiCol.HeaderHovered, Theme.Colors.FillSecondary);
        gc.Push(ImGuiCol.HeaderActive, Theme.Colors.FillPrimary);
        gc.Push(ImGuiCol.Tab, Theme.Colors.FillTertiary);
        gc.Push(ImGuiCol.TabHovered, Theme.Colors.BgHover);
        gc.Push(ImGuiCol.TabActive, Theme.Colors.SidebarActive);
        gc.Push(ImGuiCol.TabUnfocusedActive, Theme.Colors.SidebarActive);

        using var gv = new ImRaii.StyleDisposable();
        gv.Push(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
        gv.Push(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));

        var avail = ImGui.GetContentRegionAvail();
        var topBarHeight = 44f;
        var statusBarHeight = 24f;
        var sidebarWidth = 168f;

        ImGui.BeginChild("##TopBar", new Vector2(avail.X, topBarHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawTopBar();
        ImGui.EndChild();

        ImGui.Separator();

        var midHeight = Math.Max(100, ImGui.GetContentRegionAvail().Y - statusBarHeight - 8f);

        ImGui.BeginChild("##SidebarPanel", new Vector2(sidebarWidth, midHeight), false,
            ImGuiWindowFlags.NoScrollbar);
        using var sidebarSpacing = new ImRaii.StyleDisposable();
        sidebarSpacing.Push(ImGuiStyleVar.ItemSpacing, new Vector2(3, 3));
        DrawSidebar(sidebarWidth, midHeight);
        ImGui.EndChild();

        ImGui.SameLine(0, 4);

        using var contentRounding = new ImRaii.StyleDisposable();
        contentRounding.Push(ImGuiStyleVar.ChildRounding, Theme.RadiusMD);
        ImGui.BeginChild("##ContentPanel", new Vector2(-1, midHeight), false);
        DrawContent();
        ImGui.EndChild();

        ImGui.Separator();
        DrawStatusBar();
        var ver = typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
            .FirstOrDefault() as AssemblyInformationalVersionAttribute;
        var verText = ver?.InformationalVersion ?? "Dev";
        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(verText).X - 20);
        ImGui.TextColored(Theme.Colors.TextTertiary, verText);

        var fg = ImGui.GetForegroundDrawList();
        switch (_config.BgEffect)
        {
            case BgEffectMode.Nebula:
                _particleSystem.Draw(fg);
                _rippleCanvas.Draw(fg);
                _clickRipple.Draw(fg);
                break;
            case BgEffectMode.MatrixRain:
                _matrixRain.Draw(fg, winMin, winMax);
                break;
            case BgEffectMode.GeometricGlow:
                _geometricGlow.Draw(fg, winMin, winMax);
                break;
            case BgEffectMode.Firefly:
                _firefly.Draw(fg, winMin, winMax);
                break;
            case BgEffectMode.Constellation:
                _constellation.Draw(fg, winMin, winMax);
                break;
            case BgEffectMode.LeyLines:
                _leyLines.Draw(fg, winMin, winMax);
                break;
        }
    }

    // ── 顶部信息栏 ──

    private void DrawTopBar()
    {
        var region = ImGui.GetContentRegionAvail();
        var btnSize = new Vector2(28, 28);
        var controlWidth = btnSize.X * 2 + 8f;
        var textH = ImGui.GetTextLineHeight();

        // 整行垂直居中
        ImGui.SetCursorPosY((region.Y - textH) * 0.5f);

        // ── 左侧：logo 图标 + 名称 + 版本 ──
        var logoPos = ImGui.GetCursorScreenPos() + new Vector2(2, textH * 0.5f);
        IconHelper.DrawIcon(ImGui.GetWindowDrawList(), logoPos,
            IconHelper.Icons.Settings,
            ImGui.ColorConvertFloat4ToU32(Theme.Colors.AccentBlue), 18f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 26);
        ImGui.TextColored(Theme.Colors.TextPrimary, "HiAuRo");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextTertiary, $"v{_version}");

        // ── 右侧：overlay 显隐 + 主题切换 ──
        ImGui.SameLine(region.X - controlWidth);
        using var vT = new ImRaii.StyleDisposable();
        vT.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        vT.Push(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
        using var cT = new ImRaii.ColorDisposable();
        cT.Push(ImGuiCol.Button, Vector4.Zero);
        cT.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        cT.Push(ImGuiCol.ButtonActive, Theme.Colors.BgSpotlight);

        var overlaysVisible = _config.AllOverlaysVisible;
        var overlayIcon = overlaysVisible ? IconHelper.Icons.UiVisible : IconHelper.Icons.UiInvisible;
        if (ImGui.Button("##OverlayToggle", btnSize))
        {
            _config.AllOverlaysVisible = !overlaysVisible;
            Plugin.Instance._uiManager?.SetAllOverlaysVisible(_config.AllOverlaysVisible);
            _saveConfig();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(overlaysVisible ? "隐藏全部悬浮窗" : "显示全部悬浮窗");
        var overlayBtnCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        IconHelper.DrawIcon(ImGui.GetWindowDrawList(), overlayBtnCenter, overlayIcon,
            ImGui.ColorConvertFloat4ToU32(overlaysVisible ? Theme.Colors.AccentBlue : Theme.Colors.TextTertiary), 18f);

        ImGui.SameLine();
        var isDark = Theme.Mode == Theme.ThemeMode.Dark;
        var themeIcon = isDark ? IconHelper.Icons.DarkMode : IconHelper.Icons.LightMode;
        if (ImGui.Button("##ThemeToggle", btnSize))
        {
            Theme.Mode = isDark ? Theme.ThemeMode.Light : Theme.ThemeMode.Dark;
            _config.ImGuiThemeMode = isDark ? ImGuiThemeMode.Light : ImGuiThemeMode.Dark;
            _saveConfig();
        }
        var btnCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        IconHelper.DrawIcon(ImGui.GetWindowDrawList(), btnCenter, themeIcon,
            ImGui.ColorConvertFloat4ToU32(Theme.Colors.AccentBlue), 18f);
    }

    private void DrawSidebar(float width, float height)
    {
        ImGui.BeginChild("##Sidebar", new Vector2(width, height), false,
            ImGuiWindowFlags.NoScrollbar);
        using var sidebarSp = new ImRaii.StyleDisposable();
        sidebarSp.Push(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));

        var modules = Modules;
        for (var i = 0; i < modules.Count; i++)
        {
            var module = modules[i];
            var icon = ResolveModuleIcon(module.IconKey);
            var name = module.Name;
            if (SidebarCard(name, icon, i == _selectedCardIndex && _selectedPluginName == null))
            {
                _selectedCardIndex = i;
                _selectedPluginName = null;
            }
        }

        var plugins = PluginLoader.Plugins;
        if (plugins.Count > 0)
        {
            ComponentLibrary.Divider();

            foreach (var (pluginName, record) in plugins.OrderBy(kv => kv.Key))
            {
                var isSelected = _selectedPluginName == pluginName;
                var name = record.Plugin.Name;
                var version = record.Plugin.Version;
                if (SidebarCard(name, IconHelper.Icons.Puzzle, isSelected, version))
                {
                    _selectedCardIndex = modules.Count;
                    _selectedPluginName = pluginName;
                }
            }
        }

        ImGui.EndChild();
    }

    private static bool SidebarCard(string title, string icon, bool isSelected, string? subtitle = null)
    {
        var cardWidth = ImGui.GetContentRegionAvail().X;
        var cardHeight = subtitle != null ? 46f : 38f;
        var cursorStart = ImGui.GetCursorPos();

        var accentColor = isSelected ? Theme.Colors.SidebarActiveBorder : Theme.Colors.BorderSecondary;
        var bgColor = isSelected ? Theme.Colors.SidebarActive : Vector4.Zero;
        var textColor = isSelected ? Theme.Colors.TextPrimary : Theme.Colors.TextSecondary;

        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(cardWidth, cardHeight);

        dl.AddRectFilled(min, max,
            ImGui.ColorConvertFloat4ToU32(bgColor),
            Theme.RadiusMD);

        dl.AddRectFilled(min,
            min + new Vector2(3, cardHeight),
            ImGui.ColorConvertFloat4ToU32(accentColor),
            Theme.RadiusMD, ImDrawFlags.RoundCornersLeft);

        ImGui.SetCursorPos(cursorStart);
        using var cB = new ImRaii.ColorDisposable();
        cB.Push(ImGuiCol.Button, Vector4.Zero);
        cB.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        cB.Push(ImGuiCol.ButtonActive, Theme.Colors.BgSpotlight);
        using var vS = new ImRaii.StyleDisposable();
        vS.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        vS.Push(ImGuiStyleVar.FramePadding, new Vector2(12, 4));

        var clicked = ImGui.Button($"##card_{title}", new Vector2(cardWidth, cardHeight));

        var rectMin = ImGui.GetItemRectMin();
        var centerY = rectMin.Y + cardHeight * 0.5f;
        var lineHeight = ImGui.GetTextLineHeight();
        var textTop = centerY - lineHeight * 0.5f;
        var iconX = rectMin.X + 16;

        IconHelper.DrawIcon(dl, new Vector2(iconX + 8, centerY), icon, ImGui.ColorConvertFloat4ToU32(textColor), 16f);
        dl.AddText(new Vector2(iconX + 24, textTop), ImGui.ColorConvertFloat4ToU32(textColor), title);

        if (subtitle != null)
        {
            dl.AddText(new Vector2(iconX + 24, textTop + lineHeight),
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.TextTertiary), subtitle);
        }

        if (ImGui.IsItemHovered() && !isSelected)
        {
            dl.AddRectFilled(min, max,
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.BgHover),
                Theme.RadiusMD);
        }

        return clicked;
    }

    private void DrawContent()
    {
        // 切换模块时重置子tab
        if (_lastCardIndex != _selectedCardIndex)
        {
            _selectedTabIndex = 0;
            _lastCardIndex = _selectedCardIndex;
        }

        var isPluginSelected = _selectedPluginName != null;
        ImGui.Indent(12);
        ImGui.Dummy(new Vector2(0, 6));

        if (isPluginSelected)
        {
            DrawPluginContent();
        }
        else if (IsDebugModule() && !_config.ShowDeveloperTools)
        {
            DrawDeveloperGate();
        }
        else
        {
            // 子tab 仅在多 tab 模块显示；单 tab 模块（如副本）直接画内容，
            // 避免与 TabPage 内部 sub-tab（如 TimelineTabPage 的录制/事实轴/...）重叠成两层 tab
            var modules = Modules;
            var moduleTabs = modules[Math.Clamp(_selectedCardIndex, 0, modules.Count - 1)].Tabs;
            if (moduleTabs.Length > 1)
            {
                var displayNames = moduleTabs.Select(rk =>
                    _tabPages.FirstOrDefault(t => t.RouteKey == rk)?.Name ?? rk).ToArray();
                ComponentLibrary.Tabs($"module_{_selectedCardIndex}", ref _selectedTabIndex, displayNames);
                ImGui.Spacing();
            }

            if (IsDebugModule())
                DrawBgEffectSelector();

            var tab = GetCurrentTab();
            tab?.DrawContent();
        }

        ImGui.Unindent(12);
    }

    private bool IsDebugModule()
    {
        var modules = Modules;
        var moduleIndex = Math.Clamp(_selectedCardIndex, 0, modules.Count - 1);
        return modules[moduleIndex].Name == "调试";
    }

    /// <summary>开发者工具启用入口（未启用时显示）。</summary>
    private void DrawDeveloperGate()
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.Colors.TextTertiary, "开发者工具未启用。");
        ImGui.Spacing();
        if (ComponentLibrary.PrimaryButton("启用开发者工具"))
        {
            _config.ShowDeveloperTools = true;
            _saveConfig();
        }
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextTertiary, "含运行时调试、VFX 测试、背景特效");
    }

    /// <summary>背景特效选择 + 关闭开发者工具入口（仅调试模块可见）。</summary>
    private void DrawBgEffectSelector()
    {
        ComponentLibrary.SectionHeader("背景特效");
        var bgIdx = (int)_config.BgEffect;
        var bgNames = new[] { "无", "星云", "代码雨", "几何光效", "萤火虫", "星座", "黑魔纹" };
        if (ComponentLibrary.Select("dbg_bg", "背景特效", ref bgIdx, bgNames))
        {
            _config.BgEffect = (BgEffectMode)bgIdx;
            _saveConfig();
        }
        ImGui.SameLine();
        if (ComponentLibrary.DefaultButton("关闭开发者工具"))
        {
            _config.ShowDeveloperTools = false;
            _saveConfig();
        }
        ImGui.Spacing();
    }

    private TabPageBase? GetCurrentTab()
    {
        var modules = Modules;
        var moduleIndex = Math.Clamp(_selectedCardIndex, 0, modules.Count - 1);
        var tabIndex = Math.Clamp(_selectedTabIndex, 0, modules[moduleIndex].Tabs.Length - 1);
        var routeKey = modules[moduleIndex].Tabs[tabIndex];
        return _tabPages.FirstOrDefault(t => t.RouteKey == routeKey);
    }

    private static string ResolveModuleIcon(string iconKey) => iconKey switch
    {
        "bug" => IconHelper.Icons.Bug,
        "clock" => IconHelper.Icons.Clock,
        _ => IconHelper.Icons.Settings
    };

    private void DrawPluginContent()
    {
        if (_selectedPluginName == null) return;

        var plugins = PluginLoader.Plugins;
        if (!plugins.TryGetValue(_selectedPluginName, out var record)) return;

        var embeddedUI = record.Plugin.GetEmbeddedUI();
        if (embeddedUI != null)
        {
            var style = ImGui.GetStyle();
            var colors = new Vector4[style.Colors.Length];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = style.Colors[i];
            try
            {
                embeddedUI();
            }
            catch (Exception ex)
            {
                DLog.Error("Plugin embedded UI threw exception", ex);
            }
            for (var i = 0; i < colors.Length; i++)
                style.Colors[i] = colors[i];
        }
        else
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "此插件未提供嵌入 UI。");
            ImGui.Spacing();
            ImGui.TextColored(Theme.Colors.TextSecondary, "提示：右键侧边栏卡片可打开独立窗口");
        }
    }

    private static void DrawStatusBar()
    {
        ImGui.BeginChild("##StatusBar", new Vector2(-1, 20f), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        var running = RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;
        var runState = running ? (paused ? "已暂停" : "运行中") : "已停止";
        var baseColor = running ? (paused ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen) : Theme.Colors.AccentRed;

        var pulse = running && !paused ? EffectUtils.StatePulse(1.5f) : 0f;
        var stateColor = new Vector4(
            baseColor.X,
            baseColor.Y,
            baseColor.Z,
            baseColor.W * (0.7f + pulse * 0.3f));
        ImGui.SetCursorPosX(8f);
        ImGui.TextColored(Theme.Colors.TextTertiary, "HiAuRo");
        ImGui.SameLine();
        ImGui.TextColored(stateColor, $"[{runState}]");

        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextTertiary, $"v{_version}");

        ImGui.SameLine();
        ComponentLibrary.VSplit();

        ImGui.SameLine();
        var acrName = ACRLifecycle.CurrentAcrName ?? "未加载";
        ImGui.TextColored(Theme.Colors.TextSecondary, $"ACR: {acrName}");

        ImGui.SameLine();
        ComponentLibrary.VSplit();

        ImGui.SameLine();
        var pluginCount = PluginLoader.Plugins.Count;
        ImGui.TextColored(Theme.Colors.TextSecondary, $"Plugins: {pluginCount}");

        // 右侧日志入口
        ImGui.SameLine(ImGui.GetWindowWidth() - 36);
        using var lbS = new ImRaii.StyleDisposable();
        lbS.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        lbS.Push(ImGuiStyleVar.FramePadding, new Vector2(2, 0));
        using var lbC = new ImRaii.ColorDisposable();
        lbC.Push(ImGuiCol.Button, Vector4.Zero);
        lbC.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        lbC.Push(ImGuiCol.ButtonActive, Theme.Colors.BgSpotlight);
        if (ImGui.Button("##OpenLog", new Vector2(28, 18)))
            Plugin.Instance?._uiManager?.OpenLogWindow();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("打开日志窗口");
        var logCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
        IconHelper.DrawIcon(ImGui.GetWindowDrawList(), logCenter, IconHelper.Icons.Bug,
            ImGui.ColorConvertFloat4ToU32(Theme.Colors.TextSecondary), 14f);

        ImGui.EndChild();
    }

    private static string DisplayAcrType(AcrType type) => type switch
    {
        AcrType.PvE => "PvE",
        AcrType.PvP => "PvP",
        AcrType.通用 => "通用",
        _ => type.ToString()
    };

    public void OpenRoute(string routeKey)
    {
        _selectedPluginName = null;

        var modules = Modules;
        for (var i = 0; i < modules.Count; i++)
        {
            var tabIndex = Array.IndexOf(modules[i].Tabs, routeKey);
            if (tabIndex < 0) continue;

            _selectedCardIndex = i;
            _selectedTabIndex = tabIndex;
            _lastCardIndex = i;
            IsOpen = true;
            return;
        }

        IsOpen = true;
    }

    public static void OpenEditor()
    {
        var url = EditorLaunchHelper.BuildWebToolsUrl();
        try { System.Diagnostics.Process.Start(EditorLaunchHelper.Build(url)); } catch { }
    }
}
