using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.ACR;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.ImGuiLib.Effects;
using HiAuRo.Runtime;
using HiAuRo.Recording;

namespace HiAuRo.UI;

/// <summary>
/// HiAuRo ImGui 主界面 —— 状态 / 设置 / 窗口设置 / Debug
/// </summary>
public sealed class MainWindow : Window
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class</summary>
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
    }

#if DEBUG
    private long _mwDrawStart;

    /// <summary>DEBUG: 绘制前记录起始时间戳</summary>
    public override void PreDraw()
    {
        _mwDrawStart = System.Diagnostics.Stopwatch.GetTimestamp();
        base.PreDraw();
    }

    /// <summary>DEBUG: 绘制后记录耗时</summary>
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

    // ── 新布局状态字段 ──

    /// <summary>当前选中的主模块卡片索引 (0=主控, 1=调试, 2=时间轴, 3+=Plugin)</summary>
    private int _selectedCardIndex;

    /// <summary>当前模块内的子 Tab 索引</summary>
    private int _selectedTabIndex;

    /// <summary>上次选中卡片索引（用于检测切换时重置 Tab）</summary>
    private int _lastCardIndex = -1;

    /// <summary>Plugin 卡片选中时的 Plugin 记录名称</summary>
    private string? _selectedPluginName;

    /// <summary>Tips 轮播数据</summary>
    private static readonly string[] _tips = new[]
    {
        "提示：右键侧边栏 Plugin 卡片可弹出独立窗口",
        "快捷键 /hi 或点击 Dalamud 图标打开主界面",
        "在「设置」中调整技能队列窗口和 AOE 判定数",
        "时间轴模块支持录制副本并编辑执行/辅助/事实轴",
        "Debug 面板提供 Aura/Combo/Spell/SpellHistory/Target 实时测试工具",
    };

    /// <summary>Tips 轮播定时器（渲染帧累计）</summary>
    private float _tipsTimer;

    /// <summary>当前显示的 Tips 索引</summary>
    private int _tipsIndex;

    /// <summary>Tips 淡入动画进度 0~1</summary>
    private float _tipsFade;

    /// <summary>版本号（构建时通过 ci 更新）</summary>
    private const string _version = "1.0.0";

    // ── 固定模块定义 ──

    /// <summary>固定模块元数据</summary>
    private static readonly (string Name, string Icon, string[] Tabs)[] _modules = new[]
    {
        ("主控", IconHelper.Icons.Settings, new[] { "状态", "设置", "窗口设置" }),
        ("调试", IconHelper.Icons.Bug, new[] { "Debug", "ACR Debug", "日志" }),
        ("时间轴", IconHelper.Icons.Clock, new[] { "录制", "事实轴", "执行轴", "辅助轴" }),
    };

    /// <summary>绘制窗口</summary>
    public override void Draw()
    {
        // ── 窗口最小尺寸（确保布局不挤压）──
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(620, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };

        // ── 主题背景 ──
        ComponentLibrary.GlassBackground(Theme.RadiusMD);

        // ── 背景特效（根据模式选择）──
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

        // ── 全局 ImGui 样式色（跟随主题）──
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

        // ── 窗口内边距 ──
        using var gv = new ImRaii.StyleDisposable();
        gv.Push(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
        gv.Push(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));

        // ── 计算布局区域 ──
        var avail = ImGui.GetContentRegionAvail();
        var topBarHeight = 144f;
        var tabBarHeight = 28f;
        var statusBarHeight = 24f;
        var sidebarWidth = 168f;

        // ── 顶部信息栏 ──
        ImGui.BeginChild("##TopBar", new Vector2(avail.X, topBarHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawTopBar();
        ImGui.EndChild();

        // ── Tab 栏（全宽，独立行）──
        ImGui.BeginChild("##TabBar", new Vector2(avail.X, tabBarHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawTabBar();
        ImGui.EndChild();

        ImGui.Separator();

        // ── 左侧栏 + 右侧内容（填充剩余空间）──
        var midHeight = Math.Max(100, ImGui.GetContentRegionAvail().Y - statusBarHeight - 8f);

        // ── 左侧栏 ──
        ImGui.BeginChild("##SidebarPanel", new Vector2(sidebarWidth, midHeight), false,
            ImGuiWindowFlags.NoScrollbar);
        using var sidebarSpacing = new ImRaii.StyleDisposable();
        sidebarSpacing.Push(ImGuiStyleVar.ItemSpacing, new Vector2(3, 3));
        DrawSidebar(sidebarWidth, midHeight);
        ImGui.EndChild();

        ImGui.SameLine(0, 4);

        // ── 右侧内容区 ──
        using var contentRounding = new ImRaii.StyleDisposable();
        contentRounding.Push(ImGuiStyleVar.ChildRounding, Theme.RadiusMD);
        ImGui.BeginChild("##ContentPanel", new Vector2(-1, midHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawContent();
        ImGui.EndChild();

        // ── 底部状态栏 ──
        ImGui.Separator();
        DrawStatusBar();
        // 版本号右下角（编译时间，北京时间）
        var ver = typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;
        var verText = ver?.InformationalVersion ?? "Dev";
        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(verText).X - 20);
        ImGui.TextColored(Theme.Colors.TextTertiary, verText);

        // ── 前景特效（ForegroundDrawList — 渲染在最顶层，覆盖所有子窗口）──
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

    /// <summary>绘制顶部信息栏：Logo行(居中) + Tips行(全宽)</summary>
    private void DrawTopBar()
    {
        var region = ImGui.GetContentRegionAvail();
        var logoRowHeight = region.Y - Theme.FontSizeMD * 2f - 8f;
        var tipsRowHeight = Theme.FontSizeMD * 2f;
        var controlWidth = 36f;

        // ── 第一行：Logo 垂直居中 + 主题按钮 ──
        ImGui.BeginChild("##LogoRow", new Vector2(region.X, logoRowHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        var logoLines = new[]
        {
            "██╗  ██╗██╗ █████╗ ██╗   ██╗██████╗  ██████╗",
            "██║  ██║██║██╔══██╗██║   ██║██╔══██╗██╔═══██╗",
            "███████║██║███████║██║   ██║██████╔╝██║   ██║",
            "██╔══██║██║██╔══██║██║   ██║██╔══██╗██║   ██║",
            "██║  ██║██║██║  ██║╚██████╔╝██║  ██║╚██████╔╝",
            "╚═╝  ╚═╝╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═╝ ╚═════╝",
        };

        using var monoFont = ImRaii.PushFont(UiBuilder.MonoFont);
        var lineHeight = ImGui.GetTextLineHeight();
        var totalLogoH = lineHeight * logoLines.Length;
        var logoStartY = Math.Max(0, (logoRowHeight - totalLogoH) * 0.5f);

        // 水平居中：计算最宽行宽度
        var maxLineW = 0f;
        foreach (var line in logoLines)
            maxLineW = Math.Max(maxLineW, ImGui.CalcTextSize(line).X);
        var offsetX = Math.Max(0, (region.X - maxLineW) * 0.5f);

        ImGui.SetCursorPosY(logoStartY);
        foreach (var line in logoLines)
        {
            ImGui.SetCursorPosX(offsetX);
            ImGui.TextColored(Theme.Colors.AccentBlue, line);
        }

        ImGui.EndChild();

        // ── 第二行：Tips 轮播 + 主题按钮 ──
        ImGui.BeginChild("##TipsRow", new Vector2(region.X, tipsRowHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.SetCursorPosY(logoStartY);
        ImGui.SetCursorPosX(offsetX);
        DrawTips(region.X - controlWidth - 12f);
        ImGui.SameLine(region.X - controlWidth - 4f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
        var isDark = Theme.Mode == Theme.ThemeMode.Dark;
        var themeIcon = isDark ? IconHelper.Icons.DarkMode : IconHelper.Icons.LightMode;

        using var vT = new ImRaii.StyleDisposable();
        vT.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        vT.Push(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
        using var cT = new ImRaii.ColorDisposable();
        cT.Push(ImGuiCol.Button, Vector4.Zero);
        cT.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        cT.Push(ImGuiCol.ButtonActive, Theme.Colors.BgSpotlight);

        if (ImGui.Button("##ThemeToggle", new Vector2(28, 28)))
        {
            Theme.Mode = isDark ? Theme.ThemeMode.Light : Theme.ThemeMode.Dark;
            _config.ImGuiThemeMode = isDark ? ImGuiThemeMode.Light : ImGuiThemeMode.Dark;
            _saveConfig();
        }

        var btnCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        IconHelper.DrawIcon(ImGui.GetWindowDrawList(), btnCenter, themeIcon,
            ImGui.ColorConvertFloat4ToU32(Theme.Colors.AccentBlue), 18f);

        ImGui.EndChild();
    }

    /// <summary>绘制 Tips 轮播文字</summary>
    private void DrawTips(float maxWidth)
    {
        _tipsTimer += ImGui.GetIO().DeltaTime;
        _tipsFade = MathF.Min(1f, _tipsFade + ImGui.GetIO().DeltaTime * 2f);

        if (_tipsTimer > 4f)
        {
            _tipsTimer = 0f;
            _tipsIndex = (_tipsIndex + 1) % _tips.Length;
            _tipsFade = 0f;
        }

        var tip = _tips[_tipsIndex];
        var alpha = _tipsFade;
        var tipColor = new Vector4(
            Theme.Colors.TextSecondary.X,
            Theme.Colors.TextSecondary.Y,
            Theme.Colors.TextSecondary.Z,
            Theme.Colors.TextSecondary.W * alpha);

        ImGui.SetCursorPosY((ImGui.GetContentRegionAvail().Y - ImGui.GetTextLineHeight()) * 0.5f);
        // 灯泡图标
        var iconPos = ImGui.GetCursorScreenPos() + new Vector2(8, ImGui.GetTextLineHeight() * 0.5f);
        IconHelper.DrawIcon(ImGui.GetWindowDrawList(), iconPos,
            IconHelper.Icons.MediaTechnologyLight,
            ImGui.ColorConvertFloat4ToU32(tipColor), 16f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 24);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + maxWidth - 24);
        ImGui.TextColored(tipColor, tip);
        ImGui.PopTextWrapPos();
    }

    /// <summary>绘制左侧栏：固定模块卡片 + 分隔线 + Plugin 动态卡片</summary>
    private void DrawSidebar(float width, float height)
    {
        ImGui.BeginChild("##Sidebar", new Vector2(width, height), false,
            ImGuiWindowFlags.NoScrollbar);
        using var sidebarSp = new ImRaii.StyleDisposable();
        sidebarSp.Push(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));

        // ── 固定模块卡片 ──
        for (var i = 0; i < _modules.Length; i++)
        {
            var (name, icon, _) = _modules[i];
            if (SidebarCard(name, icon, i == _selectedCardIndex && _selectedPluginName == null))
            {
                _selectedCardIndex = i;
                _selectedPluginName = null;
            }
        }

        // ── 分隔线（仅当有 Plugin 时显示）─
        var plugins = PluginLoader.Plugins;
        if (plugins.Count > 0)
        {
            ComponentLibrary.Divider();

            // ── Plugin 动态卡片 ──
            foreach (var (pluginName, record) in plugins.OrderBy(kv => kv.Key))
            {
                var isSelected = _selectedPluginName == pluginName;
                var name = record.Plugin.Name;
                var version = record.Plugin.Version;
                if (SidebarCard(name, IconHelper.Icons.Puzzle, isSelected, version))
                {
                    _selectedCardIndex = _modules.Length;
                    _selectedPluginName = pluginName;
                }
            }
        }

        ImGui.EndChild();
    }

    /// <summary>绘制单张侧边栏卡片（极简风格，左侧 3px 竖条 + 图标 + 标题）</summary>
    /// <returns>是否被点击</returns>
    private static bool SidebarCard(string title, string icon, bool isSelected, string? subtitle = null)
    {
        var cardWidth = ImGui.GetContentRegionAvail().X;
        var cardHeight = subtitle != null ? 46f : 38f;
        var cursorStart = ImGui.GetCursorPos();

        // 左侧竖条颜色
        var accentColor = isSelected ? Theme.Colors.SidebarActiveBorder : Theme.Colors.BorderSecondary;
        var bgColor = isSelected ? Theme.Colors.SidebarActive : Vector4.Zero;
        var textColor = isSelected ? Theme.Colors.TextPrimary : Theme.Colors.TextSecondary;

        // ── 背景绘制 ──
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(cardWidth, cardHeight);

        // 圆角背景
        dl.AddRectFilled(min, max,
            ImGui.ColorConvertFloat4ToU32(bgColor),
            Theme.RadiusMD);

        // 左侧竖条
        dl.AddRectFilled(min,
            min + new Vector2(3, cardHeight),
            ImGui.ColorConvertFloat4ToU32(accentColor),
            Theme.RadiusMD, ImDrawFlags.RoundCornersLeft);

        // ── 可点击区域 ──
        ImGui.SetCursorPos(cursorStart);
        using var cB = new ImRaii.ColorDisposable();
        cB.Push(ImGuiCol.Button, Vector4.Zero);
        cB.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        cB.Push(ImGuiCol.ButtonActive, Theme.Colors.BgSpotlight);
        using var vS = new ImRaii.StyleDisposable();
        vS.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        vS.Push(ImGuiStyleVar.FramePadding, new Vector2(12, 4));

        var clicked = ImGui.Button($"##card_{title}", new Vector2(cardWidth, cardHeight));

        // ── 图标+文字叠加（垂直居中）──
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

        // 悬停时在卡片上叠加白色半透明
        if (ImGui.IsItemHovered() && !isSelected)
        {
            dl.AddRectFilled(min, max,
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.BgHover),
                Theme.RadiusMD);
        }

        return clicked;
    }

    /// <summary>绘制 Tab 栏（左侧模块名 + 右侧 Tab 按钮，与侧边栏/内容区对齐）</summary>
    private void DrawTabBar()
    {
        var sidebarWidth = 168f;
        var isPluginSelected = _selectedPluginName != null;

        // ── 检测卡片切换，重置 Tab 索引 ──
        if (_lastCardIndex != _selectedCardIndex)
        {
            _selectedTabIndex = 0;
            _lastCardIndex = _selectedCardIndex;
        }

        // ── 左侧：模块名称（对齐 Sidebar 宽度）──
        ImGui.BeginChild("##TabLabel", new Vector2(sidebarWidth, 0), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.SetCursorPosX(8f);
        ImGui.SetCursorPosY((ImGui.GetContentRegionAvail().Y - ImGui.GetTextLineHeight()) * 0.5f);
        if (!isPluginSelected)
        {
            var (moduleName, _, _) = _modules[Math.Clamp(_selectedCardIndex, 0, _modules.Length - 1)];
            ImGui.TextColored(Theme.Colors.TextSecondary, moduleName);
        }
        else
        {
            ImGui.TextColored(Theme.Colors.AccentBlue, $"插件: {_selectedPluginName}");
        }
        ImGui.EndChild();

        ImGui.SameLine(0, 4);

        // ── 右侧：Tab 按钮（对齐 Content 区域）──
        ImGui.BeginChild("##TabButtons", new Vector2(-1, 0), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!isPluginSelected)
        {
            var (_, _, tabs) = _modules[Math.Clamp(_selectedCardIndex, 0, _modules.Length - 1)];
            ImGui.SetCursorPosY((ImGui.GetContentRegionAvail().Y - ImGui.GetTextLineHeight()) * 0.5f);
            ComponentLibrary.Tabs($"module_{_selectedCardIndex}", ref _selectedTabIndex, tabs);
        }
        ImGui.EndChild();
    }

    /// <summary>绘制右侧内容区（Tab 下方的纯内容）</summary>
    private void DrawContent()
    {
        var isPluginSelected = _selectedPluginName != null;

        // 全局缩进，防止内容被圆角裁剪
        ImGui.Indent(12);
        ImGui.Dummy(new Vector2(0, 6));

        if (isPluginSelected)
        {
            DrawPluginContent();
        }
        else
        {
            DrawModuleContent();
        }

        ImGui.Unindent(12);
    }

    /// <summary>根据当前选中的模块卡片 + Tab 索引绘制内容</summary>
    private void DrawModuleContent()
    {
        var moduleIndex = Math.Clamp(_selectedCardIndex, 0, _modules.Length - 1);
        var (moduleName, _, tabs) = _modules[moduleIndex];
        var tabIndex = Math.Clamp(_selectedTabIndex, 0, tabs.Length - 1);
        var tabName = tabs[tabIndex];

        // 路由到对应的 DrawXxx 方法
        switch (tabName)
        {
            case "状态":       DrawStatus(); break;
            case "设置":       DrawSettings(); break;
            case "窗口设置":   DrawOverlaySettings(); break;
            case "Debug":      DrawDebug(); break;
            case "ACR Debug":  DrawAcrDebug(); break;
            case "日志":       DrawLog(); break;
            case "录制":       DrawRecording(); break;
            case "事实轴":     DrawFactAxisTab(); break;
            case "执行轴":     DrawExecutionAxisTab(); break;
            case "辅助轴":     DrawAssistAxisTab(); break;
        }
    }

    /// <summary>绘制当前选中 Plugin 的嵌入内容</summary>
    private void DrawPluginContent()
    {
        if (_selectedPluginName == null) return;

        var plugins = PluginLoader.Plugins;
        if (!plugins.TryGetValue(_selectedPluginName, out var record)) return;

        var embeddedUI = record.Plugin.GetEmbeddedUI();
        if (embeddedUI != null)
        {
            embeddedUI();
        }
        else
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "此插件未提供嵌入 UI。");
            ImGui.Spacing();
            ImGui.TextColored(Theme.Colors.TextSecondary, "提示：右键侧边栏卡片可打开独立窗口");
        }
    }

    /// <summary>绘制底部状态栏</summary>
    private static void DrawStatusBar()
    {
        ImGui.BeginChild("##StatusBar", new Vector2(-1, 20f), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        var running = RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;
        var runState = running ? (paused ? "已暂停" : "运行中") : "已停止";
        var baseColor = running ? (paused ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen) : Theme.Colors.AccentRed;

        // 运行中时加呼吸脉冲
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

        ImGui.EndChild();
    }

    private void DrawStatus()
    {
        // UI 渲染模式（WebUI 暂不可用，只显示 ImGui）
        ImGui.TextColored(Theme.Colors.AccentBlue, "UI 渲染模式:");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextPrimary, "ImGui");

        // ImGui 主题模式（始终显示）
        {
            ImGui.Spacing();
            var isLight = _config.ImGuiThemeMode == ImGuiThemeMode.Light;
            ImGui.TextColored(Theme.Colors.AccentBlue, "ImGui 主题:");
            ImGui.SameLine();
            if (ImGui.RadioButton("亮色", isLight))
            {
                _config.ImGuiThemeMode = ImGuiThemeMode.Light;
                Theme.Mode = Theme.ThemeMode.Light;
                _saveConfig();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("暗色", !isLight))
            {
                _config.ImGuiThemeMode = ImGuiThemeMode.Dark;
                Theme.Mode = Theme.ThemeMode.Dark;
                _saveConfig();
            }

            // 背景特效模式
            ImGui.Spacing();
            ImGui.TextColored(Theme.Colors.AccentBlue, "背景特效:");
            ImGui.SameLine();
            var bgModes = new[] { "无", "星云", "代码雨", "几何光效", "萤火虫", "星座", "黑魔纹" };
            var bgIdx = (int)_config.BgEffect;
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("##BgEffect", ref bgIdx, bgModes, bgModes.Length))
            {
                _config.BgEffect = (BgEffectMode)bgIdx;
                _saveConfig();
            }
        }

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("ACR 运行状态");

        var running = RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;

        string state = running ? (paused ? "⏸ 已暂停" : "● 运行中") : "○ 已停止";
        Vector4 color = running ? (paused ? new Vector4(1f, 0.65f, 0, 1f) : new Vector4(0, 1, 0, 1)) : new Vector4(1, 0.3f, 0.3f, 1);
        ImGui.TextColored(color, state);

        ImGui.SameLine();
        if (ImGui.Button(running ? "停止" : "启动"))
        {
            if (running) RuntimeCore.Stop();
            else RuntimeCore.Start();
        }
        if (running)
        {
            ImGui.SameLine();
            if (ImGui.Button(paused ? "继续" : "暂停"))
                ACR.MainControlHelper.TogglePause();
        }

        ImGui.Spacing();
        ImGui.Separator();

        if (!HiAuRo.Data.IsReady)
        {
            ImGui.Text("等待角色加载...");
            return;
        }

        ImGui.Text($"战斗状态: {CombatContext.CurrentState}");
        ImGui.Text($"当前职业: {Data.Me.ClassJob}");
        ImGui.Text($"当前 ACR: {ACRLifecycle.CurrentAcrName}");
        ImGui.Text($"GCD 剩余: {ACR.GCDHelper.GetGCDCooldown():F0}ms");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("游戏内 CEF 悬浮窗（浏览器打开测试）:");
        ImGui.BulletText("http://localhost:5678/main.html     — 主控制栏");
        ImGui.BulletText("http://localhost:5678/qt.html       — QT 面板");
        ImGui.BulletText("http://localhost:5678/hotkey.html   — 热键面板");
        ImGui.Spacing();
    }

    private void DrawSettings()
    {
        ImGui.Spacing();

        var changed = false;
        var aq = _config.ActionQueueInMs;
        var maxAb = _config.MaxAbilityTimesInGcd;
        var abInterval = _config.AbilityIntervalMs;
        var aoe = _config.AoeCount;
        var range = _config.AttackRange;
        var debug = _config.DebugEnabled;

        ImGui.Text("全局设置");
        ImGui.Separator();

        ImGui.TextColored(Theme.Colors.TextPrimary, "技能队列窗口 (ms)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.InputInt("##aq_window", ref aq, 50);

        ImGui.TextColored(Theme.Colors.TextPrimary, "GCD 内能力技上限");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.InputInt("##max_ab", ref maxAb, 1);

        ImGui.TextColored(Theme.Colors.TextPrimary, "能力技间隔 (ms)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.InputInt("##ab_interval", ref abInterval, 50);

        ImGui.TextColored(Theme.Colors.TextPrimary, "AOE 判定敌人数");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.InputInt("##aoe_count", ref aoe, 1);

        using (var sc = new ImRaii.ColorDisposable())
        {
            sc.Push(ImGuiCol.FrameBg, Theme.Colors.SliderRail);
            sc.Push(ImGuiCol.SliderGrab, Theme.Colors.SliderTrack);
            sc.Push(ImGuiCol.SliderGrabActive, Theme.Colors.SliderTrack);
            sc.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
            ImGui.PushItemWidth(160);
            changed |= ImGui.SliderFloat("##attack_range", ref range, 5f, 40f, "%.1f");
            ImGui.PopItemWidth();
        }
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, "攻击距离");

        ImGui.Separator();
        changed |= ImGui.Checkbox("Debug 日志", ref debug);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("触发器目录同步 (GitHub)");
        ImGui.Separator();

        var ghToken = _config.GitHubToken ?? "";
        var ghRepo = _config.CatalogRepo ?? "";
        var ghBranch = _config.CatalogBranch ?? "";

        ImGui.SetNextItemWidth(250);
        changed |= ImGui.InputTextWithHint("GitHub Token", "ghp_... (repo 权限)", ref ghToken, 128, ImGuiInputTextFlags.Password);

        ImGui.SetNextItemWidth(200);
        changed |= ImGui.InputText("仓库", ref ghRepo, 128);

        ImGui.SetNextItemWidth(150);
        changed |= ImGui.InputText("分支", ref ghBranch, 64);

        if (changed)
        {
            _config.ActionQueueInMs = aq;
            _config.MaxAbilityTimesInGcd = maxAb;
            _config.AbilityIntervalMs = abInterval;
            _config.AoeCount = aoe;
            _config.AttackRange = range;
            _config.DebugEnabled = debug;
            _config.GitHubToken = ghToken.Length > 0 ? ghToken : null;
            _config.CatalogRepo = ghRepo;
            _config.CatalogBranch = ghBranch;
            _saveConfig();
        }
    }

    private void DrawDebug()
    {
        ImGui.Spacing();
        ImGui.Text("运行时信息");

        if (ImGui.Button("重载 ACR"))
            ACRLifecycle.Runner.Reset();

        ImGui.SameLine();
        if (ImGui.Button("清空协程"))
            Coroutine.Instance.Clear();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text($"ACR: {ACRLifecycle.CurrentAcrName}");
        ImGui.Text($"SpellQueue: {ACRLifecycle.Runner.SpellQueue.QueueSize}");
        ImGui.Text($"OpenerMgr: {ACRLifecycle.Runner.OpenerMgr.CurrentState}");

        // ================================================================
        //  Helper 测试区域
        // ================================================================

        // --- AuraHelper ---
        if (ImGui.CollapsingHeader("AuraHelper"))
        {
            // HasAura
            ImGui.Text("HasAura  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraHasAura_BuffId", ref _dbgAuraHasAura_BuffId, 0);
            ImGui.SameLine(); ImGui.RadioButton("自己##AuraHasAura_T", ref _dbgAuraHasAura_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##AuraHasAura_T", ref _dbgAuraHasAura_Target, 1);
            ImGui.SameLine();
            if (ImGui.Button("测试##AuraHasAura"))
            {
                var t = DbgTgt(_dbgAuraHasAura_Target);
                _dbgAuraHasAura_Result = $"→ HasAura: {AuraHelper.HasAura(t, (uint)_dbgAuraHasAura_BuffId)}";
            }
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasAura_Result);

            // HasAnyAura
            ImGui.Text("HasAnyAura  buffIds(逗号分隔)");
            ImGui.SameLine(); ImGui.SetNextItemWidth(120); ImGui.InputText("##AuraHasAnyAura_Ids", ref _dbgAuraHasAnyAura_BuffIds, 128);
            ImGui.SameLine(); ImGui.RadioButton("自己##AuraHasAnyAura_T", ref _dbgAuraHasAnyAura_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##AuraHasAnyAura_T", ref _dbgAuraHasAnyAura_Target, 1);
            ImGui.SameLine();
            if (ImGui.Button("测试##AuraHasAnyAura"))
            {
                var t = DbgTgt(_dbgAuraHasAnyAura_Target);
                var ids = _dbgAuraHasAnyAura_BuffIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => uint.TryParse(s.Trim(), out var id) ? id : 0).ToArray();
                _dbgAuraHasAnyAura_Result = $"→ HasAnyAura: {AuraHelper.HasAnyAura(t, ids)}";
            }
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasAnyAura_Result);

            // GetAuraTimeLeft
            ImGui.Text("GetAuraTimeLeft  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraGetTimeLeft_BuffId", ref _dbgAuraGetTimeLeft_BuffId, 0);

            ImGui.SameLine(); ImGui.RadioButton("自己##AuraGetTimeLeft_T", ref _dbgAuraGetTimeLeft_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##AuraGetTimeLeft_T", ref _dbgAuraGetTimeLeft_Target, 1);
            ImGui.SameLine();
            if (ImGui.Button("测试##AuraGetTimeLeft"))
            {
                var t = DbgTgt(_dbgAuraGetTimeLeft_Target);
                _dbgAuraGetTimeLeft_Result = $"→ 剩余: {AuraHelper.GetAuraTimeLeft(t, (uint)_dbgAuraGetTimeLeft_BuffId):F0}ms";
            }
            ImGui.SameLine(); ImGui.Text(_dbgAuraGetTimeLeft_Result);

            // HasSelfAura
            ImGui.Text("HasSelfAura  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraHasSelfAura_BuffId", ref _dbgAuraHasSelfAura_BuffId, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##AuraHasSelfAura"))
                _dbgAuraHasSelfAura_Result = $"→ HasSelfAura: {AuraHelper.HasSelfAura((uint)_dbgAuraHasSelfAura_BuffId)}";
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasSelfAura_Result);

            // HasTargetAura
            ImGui.Text("HasTargetAura  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraHasTargetAura_BuffId", ref _dbgAuraHasTargetAura_BuffId, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##AuraHasTargetAura"))
                _dbgAuraHasTargetAura_Result = $"→ HasTargetAura: {AuraHelper.HasTargetAura((uint)_dbgAuraHasTargetAura_BuffId)}";
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasTargetAura_Result);
        }

        // --- ComboHelper ---
        if (ImGui.CollapsingHeader("ComboHelper"))
        {
            ImGui.TextDisabled($"LastComboSpellId: {ComboHelper.LastComboSpellId}  ComboTimer: {ComboHelper.ComboTimer:F1}s  LastSpellId: {ComboHelper.LastSpellId}");

            // WasLastCombo
            ImGui.Text("WasLastCombo  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboWasLast_SpellId", ref _dbgComboWasLastCombo_SpellId, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##ComboWasLast"))
                _dbgComboWasLastCombo_Result = $"→ WasLastCombo: {ComboHelper.WasLastCombo((uint)_dbgComboWasLastCombo_SpellId)}";
            ImGui.SameLine(); ImGui.Text(_dbgComboWasLastCombo_Result);

            // ComboInWindow
            ImGui.Text("ComboInWindow  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboInWindow_SpellId", ref _dbgComboComboInWindow_SpellId, 0);
            ImGui.SameLine(); ImGui.Text("windowMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboInWindow_WindowMs", ref _dbgComboComboInWindow_WindowMs, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##ComboInWindow"))
                _dbgComboComboInWindow_Result = $"→ ComboInWindow: {ComboHelper.ComboInWindow((uint)_dbgComboComboInWindow_SpellId, _dbgComboComboInWindow_WindowMs)}";
            ImGui.SameLine(); ImGui.Text(_dbgComboComboInWindow_Result);

            // ComboAboutToExpire
            ImGui.Text("ComboAboutToExpire  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboAboutToExpire_SpellId", ref _dbgComboComboAboutToExpire_SpellId, 0);
            ImGui.SameLine(); ImGui.Text("withinMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputInt("##ComboAboutToExpire_WithinMs", ref _dbgComboComboAboutToExpire_WithinMs, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##ComboAboutToExpire"))
                _dbgComboComboAboutToExpire_Result = $"→ ComboAboutToExpire: {ComboHelper.ComboAboutToExpire((uint)_dbgComboComboAboutToExpire_SpellId, _dbgComboComboAboutToExpire_WithinMs)}";
            ImGui.SameLine(); ImGui.Text(_dbgComboComboAboutToExpire_Result);
        }

        // --- SpellHelper ---
        if (ImGui.CollapsingHeader("SpellHelper"))
        {
            // CanUseSpell
            ImGui.Text("CanUseSpell  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellCanUse_Id", ref _dbgSpellCanUseSpell_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellCanUse"))
                _dbgSpellCanUseSpell_Result = $"→ CanUseSpell: {SpellHelper.CanUseSpell((uint)_dbgSpellCanUseSpell_Id)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellCanUseSpell_Result);

            // IsActionReady
            ImGui.Text("IsActionReady  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellIsActionReady_Id", ref _dbgSpellIsActionReady_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellIsActionReady"))
            {
                _dbgSpellIsActionReady_Result = $"→ IsActionReady: {SpellHelper.IsActionReady((uint)_dbgSpellIsActionReady_Id)}";
            }
            ImGui.SameLine(); ImGui.Text(_dbgSpellIsActionReady_Result);

            // GetCooldownRemaining
            ImGui.Text("GetCooldownRemaining  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetCd_Id", ref _dbgSpellGetCd_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellGetCd"))
                _dbgSpellGetCd_Result = $"→ 剩余: {SpellHelper.GetCooldownRemaining((uint)_dbgSpellGetCd_Id):F0}ms";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetCd_Result);

            // GetMaxCharges
            ImGui.Text("GetMaxCharges  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetMaxCharges_Id", ref _dbgSpellGetMaxCharges_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellGetMaxCharges"))
                _dbgSpellGetMaxCharges_Result = $"→ 最大层数: {SpellHelper.GetMaxCharges((uint)_dbgSpellGetMaxCharges_Id)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetMaxCharges_Result);

            // GetCharges
            ImGui.Text("GetCharges  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetCharges_Id", ref _dbgSpellGetCharges_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellGetCharges"))
                _dbgSpellGetCharges_Result = $"→ 当前层数: {SpellHelper.GetCharges((uint)_dbgSpellGetCharges_Id)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetCharges_Result);

            // GetChargeCooldown
            ImGui.Text("GetChargeCooldown  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetChargeCd_Id", ref _dbgSpellGetChargeCd_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellGetChargeCd"))
                _dbgSpellGetChargeCd_Result = $"→ 充能剩余: {SpellHelper.GetChargeCooldown((uint)_dbgSpellGetChargeCd_Id):F0}ms";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetChargeCd_Result);

            // IsInRange
            ImGui.Text("IsInRange  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellIsInRange_Id", ref _dbgSpellIsInRange_Id, 0);
            ImGui.SameLine(); ImGui.RadioButton("自己##SpellIsInRange_T", ref _dbgSpellIsInRange_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##SpellIsInRange_T", ref _dbgSpellIsInRange_Target, 1);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellIsInRange"))
                _dbgSpellIsInRange_Result = $"→ IsInRange: {SpellHelper.IsInRange((uint)_dbgSpellIsInRange_Id, DbgTgt(_dbgSpellIsInRange_Target))}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellIsInRange_Result);
        }

        // --- SpellHistoryHelper ---
        if (ImGui.CollapsingHeader("SpellHistoryHelper"))
        {
            // RecentlyUsed
            ImGui.Text("RecentlyUsed  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellHistRecentlyUsed_Id", ref _dbgSpellHistRecentlyUsed_Id, 0);
            ImGui.SameLine(); ImGui.Text("withinMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputInt("##SpellHistRecentlyUsed_WithinMs", ref _dbgSpellHistRecentlyUsed_WithinMs, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellHistRecentlyUsed"))
                _dbgSpellHistRecentlyUsed_Result = $"→ RecentlyUsed: {SpellHistoryHelper.RecentlyUsed((uint)_dbgSpellHistRecentlyUsed_Id, _dbgSpellHistRecentlyUsed_WithinMs)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellHistRecentlyUsed_Result);

            // GetLastSpellTime
            ImGui.Text("GetLastSpellTime  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellHistGetLastTime_Id", ref _dbgSpellHistGetLastTime_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellHistGetLastTime"))
            {
                var t = SpellHistoryHelper.GetLastSpellTime((uint)_dbgSpellHistGetLastTime_Id);
                _dbgSpellHistGetLastTime_Result = t >= 0 ? $"→ 距上次: {t}ms" : "→ 从未使用";
            }
            ImGui.SameLine(); ImGui.Text(_dbgSpellHistGetLastTime_Result);

            // GetGcdCountFromLastGcd
            ImGui.Text("GetGcdCountFromLastGcd");
            ImGui.SameLine();
            if (ImGui.Button("测试##SpellHistGcdCount"))
                _dbgSpellHistGcdCount_Result = $"→ GCD计数: {SpellHistoryHelper.GetGcdCountFromLastGcd()}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellHistGcdCount_Result);

            // RecordSpell / RecordGcd / Reset
            ImGui.Text("RecordSpell  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellHistRecord_Id", ref _dbgSpellHistRecord_Id, 0);
            ImGui.SameLine();
            if (ImGui.Button("记录##SpellHistRecord")) SpellHistoryHelper.RecordSpell((uint)_dbgSpellHistRecord_Id);
            ImGui.SameLine();
            if (ImGui.Button("记录GCD##SpellHistRecordGcd")) SpellHistoryHelper.RecordGcd();
            ImGui.SameLine();
            if (ImGui.Button("重置历史##SpellHistReset")) SpellHistoryHelper.Reset();
        }

        // --- TargetHelper ---
        if (ImGui.CollapsingHeader("TargetHelper"))
        {
            // GetNearbyEnemyCount
            ImGui.Text("GetNearbyEnemyCount");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtGetNearby_T", ref _dbgTargetGetNearby_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtGetNearby_T", ref _dbgTargetGetNearby_Target, 1);
            ImGui.SameLine(); ImGui.Text("range");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputFloat("##TgtGetNearby_Range", ref _dbgTargetGetNearby_Range);
            ImGui.SameLine();
            if (ImGui.Button("测试##TgtGetNearby"))
                _dbgTargetGetNearby_Result = $"→ 附近敌人数: {TargetHelper.GetNearbyEnemyCount(DbgTgt(_dbgTargetGetNearby_Target), _dbgTargetGetNearby_Range)}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetGetNearby_Result);

            // IsBehind
            ImGui.Text("IsBehind");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtIsBehind_T", ref _dbgTargetIsBehind_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtIsBehind_T", ref _dbgTargetIsBehind_Target, 1);
            ImGui.SameLine();
            if (ImGui.Button("测试##TgtIsBehind"))
                _dbgTargetIsBehind_Result = $"→ IsBehind: {TargetHelper.IsBehind(DbgTgt(_dbgTargetIsBehind_Target))}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetIsBehind_Result);

            // IsFlanking
            ImGui.Text("IsFlanking");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtIsFlanking_T", ref _dbgTargetIsFlanking_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtIsFlanking_T", ref _dbgTargetIsFlanking_Target, 1);
            ImGui.SameLine();
            if (ImGui.Button("测试##TgtIsFlanking"))
                _dbgTargetIsFlanking_Result = $"→ IsFlanking: {TargetHelper.IsFlanking(DbgTgt(_dbgTargetIsFlanking_Target))}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetIsFlanking_Result);

            // TargetCastingIsBossAOE
            ImGui.Text("TargetCastingIsBossAOE");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtCastingAOE_T", ref _dbgTargetCastingAOE_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtCastingAOE_T", ref _dbgTargetCastingAOE_Target, 1);
            ImGui.SameLine(); ImGui.Text("thresholdMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##TgtCastingAOE_Threshold", ref _dbgTargetCastingAOE_Threshold, 0);
            ImGui.SameLine();
            if (ImGui.Button("测试##TgtCastingAOE"))
                _dbgTargetCastingAOE_Result = $"→ IsBossAOE: {TargetHelper.TargetCastingIsBossAOE(DbgTgt(_dbgTargetCastingAOE_Target) as IBattleChara, _dbgTargetCastingAOE_Threshold)}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetCastingAOE_Result);

            // GetCastingSpellTiming
            ImGui.Text("GetCastingSpellTiming");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtCastingTime_T", ref _dbgTargetCastingTime_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtCastingTime_T", ref _dbgTargetCastingTime_Target, 1);
            ImGui.SameLine();
            if (ImGui.Button("测试##TgtCastingTime"))
                _dbgTargetCastingTime_Result = $"→ 读条剩余: {TargetHelper.GetCastingSpellTiming(DbgTgt(_dbgTargetCastingTime_Target) as IBattleChara):F0}ms";
            ImGui.SameLine(); ImGui.Text(_dbgTargetCastingTime_Result);

            // GetMostCanTargetObjects
            ImGui.Text("GetMostCanTargetObjects  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##TgtMostCanTarget_SpellId", ref _dbgTargetMostCanTarget_SpellId, 0);
            ImGui.SameLine(); ImGui.Text("min");
            ImGui.SameLine(); ImGui.SetNextItemWidth(40); ImGui.InputInt("##TgtMostCanTarget_MinCount", ref _dbgTargetMostCanTarget_MinCount, 0);
            ImGui.SameLine(); ImGui.Text("range");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputFloat("##TgtMostCanTarget_Range", ref _dbgTargetMostCanTarget_Range);
            ImGui.SameLine();
            if (ImGui.Button("测试##TgtMostCanTarget"))
            {
                var best = TargetHelper.GetMostCanTargetObjects((uint)_dbgTargetMostCanTarget_SpellId, _dbgTargetMostCanTarget_MinCount, _dbgTargetMostCanTarget_Range);
                _dbgTargetMostCanTarget_Result = best != null ? $"→ 最佳目标: {best.Name}" : "→ 未找到(不足最小敌人数)";
            }
            ImGui.SameLine(); ImGui.Text(_dbgTargetMostCanTarget_Result);
        }
    }

    private void DrawAcrDebug()
    {
        ImGui.Spacing();
        ImGui.Text("SlotResolver 实时状态");
        ImGui.Separator();

        var runner = ACRLifecycle.Runner;
        if (runner.AiLoop is not AILoop_Normal loop)
        {
            ImGui.Text("无活跃 ACR 或 IAILoop 类型不匹配");
            return;
        }

        var resolvers = loop.DebugResolvers;
        if (resolvers.Count == 0)
        {
            ImGui.Text("没有已注册的 SlotResolver");
            return;
        }

        // GCD 状态条
        float gcdRemain = ACR.GCDHelper.GetGCDCooldown();
        bool gcdReady = gcdRemain <= 0;
        bool ogcdWindow = ACR.GCDHelper.CanUseOffGcd();
        ImGui.TextColored(gcdReady ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0.6f, 0, 1),
            $"GCD: {(gcdReady ? "就绪" : $"{gcdRemain:F0}ms")} | oGCD窗口: {(ogcdWindow ? "开" : "关")}");

        ImGui.SameLine();
        ImGui.TextDisabled($"(共 {resolvers.Count} 个)");

        ImGui.Spacing();

        if (!ImGui.BeginTable("##AcrDebugTable", 5,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("Resolver", ImGuiTableColumnFlags.WidthFixed, 160);
        ImGui.TableSetupColumn("Mode", ImGuiTableColumnFlags.WidthFixed, 55);
        ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableSetupColumn("窗口", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("产出技能", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        foreach (var info in resolvers)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Text(info.Name);

            ImGui.TableNextColumn();
            ImGui.Text(info.Mode.ToString());

            ImGui.TableNextColumn();
            if (info.CheckThrew)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "ERR");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(info.CheckError);
            }
            else if (info.CheckResult >= 0)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), info.CheckResult.ToString());
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), info.CheckResult.ToString());
            }

            ImGui.TableNextColumn();
            if (info.BuiltSlot)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "●");
            }
            else if (info.PassedWindow)
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "○");
            }
            else if (info.CheckResult >= 0)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "✗");
            }
            else
            {
                ImGui.Text("-");
            }

            ImGui.TableNextColumn();
            if (info.BuiltSlot && info.BuiltSkills.Length > 0)
            {
                using var greenText = new ImRaii.ColorDisposable();
                greenText.Push(ImGuiCol.Text, new Vector4(0, 1, 0, 1));
                ImGui.TextWrapped(info.BuiltSkills);
            }
            else if (info.CheckResult >= 0 && !info.PassedWindow)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "等待窗口");
            }
        }

        ImGui.EndTable();
    }

    private void DrawOverlaySettings()
    {
        ImGui.Spacing();
        ImGui.Text("外部悬浮窗");
        ImGui.Separator();

        var overlays = _config.Overlays;
        if (overlays == null || overlays.Length == 0) return;

        var changed = false;

        for (int i = 0; i < overlays.Length; i++)
        {
            var ol = overlays[i];
            var overlayChanged = false;

            ImGui.PushID(i);
            var vis = ol.Visible;
            if (ImGui.Checkbox(ol.Name, ref vis))
            {
                ol.Visible = vis;
                overlayChanged = true;
            }

            ImGui.Indent(16);

            var url = ol.Url;
            ImGui.SetNextItemWidth(300);
            if (ImGui.InputText("URL", ref url, 256))
            {
                ol.Url = url;
                overlayChanged = true;
            }

            var w = ol.Width;
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("宽", ref w, 10))
            {
                ol.Width = w;
                overlayChanged = true;
            }
            ImGui.SameLine();
            var h = ol.Height;
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("高", ref h, 10))
            {
                ol.Height = h;
                overlayChanged = true;
            }

            var zoom = ol.Zoom;
            ImGui.SetNextItemWidth(150);
            if (ImGui.SliderFloat("缩放 %", ref zoom, 50f, 200f, "%.0f%%"))
            {
                ol.Zoom = zoom;
                overlayChanged = true;
            }

            var locked = ol.Locked;
            if (ImGui.Checkbox("锁定窗口", ref locked))
            {
                ol.Locked = locked;
                overlayChanged = true;
            }

            ImGui.Unindent(16);
            ImGui.Spacing();
            ImGui.PopID();

            if (overlayChanged)
            {
                changed = true;
                Plugin.Instance._uiManager?.BrowsingwayIpc?.CreateOrUpdateOverlay(ol);
            }
        }

        if (changed) _saveConfig();
    }

    private void DrawFactAxisTab()
    {
        var flags = PluginConfig.Instance.FactAxis;
        bool changed = false;

        ImGui.Text("观测");
        ImGui.Separator();
        { var v = flags.Observe; changed |= ImGui.Checkbox("时间线观测", ref v); flags.Observe = v; }

        ImGui.Text("QT 调控");
        ImGui.Separator();
        { var v = flags.QtControl; changed |= ImGui.Checkbox("QT 调控", ref v); flags.QtControl = v; }

        ImGui.Text("决策分配");
        ImGui.Separator();
        { var v = flags.TeamMitigation; changed |= ImGui.Checkbox("团队减伤分配", ref v); flags.TeamMitigation = v; }
        { var v = flags.PersonalMitigation; changed |= ImGui.Checkbox("单人减伤分配", ref v); flags.PersonalMitigation = v; }
        { var v = flags.TeamHealing; changed |= ImGui.Checkbox("团队治疗分配", ref v); flags.TeamHealing = v; }
        { var v = flags.ForceExecute; changed |= ImGui.Checkbox("技能强制释放", ref v); flags.ForceExecute = v; }

        ImGui.Text("移动");
        ImGui.Separator();
        { var v = flags.MoveTo; changed |= ImGui.Checkbox("MoveTo", ref v); flags.MoveTo = v; }
        { var v = flags.TP; changed |= ImGui.Checkbox("TP", ref v); flags.TP = v; }
        { var v = flags.Hold; changed |= ImGui.Checkbox("Hold", ref v); flags.Hold = v; }

        ImGui.Text("移动模式");
        ImGui.Separator();
        var modes = new[] { "NavMesh", "TP", "NavMesh + TP兜底" };
        int modeIdx = (int)flags.MovementMode;
        if (ImGui.Combo("移动模式", ref modeIdx, modes, modes.Length))
        {
            flags.MovementMode = (MovementMode)modeIdx;
            changed = true;
        }

        if (changed)
        {
            _saveConfig();
        }

        // === 轴信息 ===
        ImGui.Text("轴信息"); ImGui.Separator();
        var ft = FactTimeline.Instance;
        var fState = ft.State;
        if (fState.IsRunning)
        {
            ImGui.Text("● 运行中");
            var territory = OmenTools.OmenService.GameState.TerritoryType;
            ImGui.Text($"副本: {territory}");
        }
        else
        {
            ImGui.Text("○ 未启动");
        }

        ImGui.Spacing();

        // 显示当前事实轴状态
        ImGui.Text("运行时状态");
        ImGui.Separator();
        var state = FactTimeline.Instance.State;
        ImGui.Text($"状态: {(state.IsRunning ? "运行中" : "未启动")}");
        if (state.IsRunning)
        {
            ImGui.Text($"副本: {state.TimelineName}");
            ImGui.Text($"阶段: {state.PhaseName} | {state.Status}");
            ImGui.Text($"时间: 阶段{state.PhaseTime:F1}s / 总{state.TotalTime:F1}s");
            if (state.CurrentEvent != null)
                ImGui.Text($"当前事件: {state.CurrentEvent.Name}");

            var mode = ModeSwitch.CurrentMode;
            ImGui.Text($"模式: {mode}");
        }
    }

    private void DrawExecutionAxisTab()
    {
        var config = PluginConfig.Instance;
        var axis = Execution.ExecutionAxis.Instance;
        var port = Plugin.Instance._uiManager?.WebServerPort ?? 5678;

        // === 轴信息 ===
        if (axis.Initialized)
        {
            ImGui.Text("轴信息"); ImGui.Separator();
            ImGui.Text(axis.IsRunning ? "● 运行中" : "○ 未启动");
            if (!string.IsNullOrEmpty(axis.TimelineName))
                ImGui.Text($"名称: {axis.TimelineName}");

            var data = axis.TimelineData;
            if (data != null)
            {
                var territory = OmenTools.OmenService.GameState.TerritoryType;
                ImGui.Text($"副本: {territory} (TerritoryTypeId: {data.TerritoryId})");
                if (!string.IsNullOrEmpty(data.Author))
                    ImGui.Text($"作者: {data.Author}");
                if (!string.IsNullOrEmpty(data.Guid))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
                    ImGui.Text(data.Guid);
                    ImGui.PopStyleColor();
                }
            }

            if (data != null && data.ExposedVarNames is { Count: > 0 })
            {
                ImGui.Spacing();
                ImGui.Text("暴露变量"); ImGui.Separator();
                if (!string.IsNullOrEmpty(data.ExposedVarDesc))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
                    ImGui.TextWrapped(data.ExposedVarDesc);
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                }
                foreach (var varName in data.ExposedVarNames)
                {
                    var val = axis.Context.GetVariable(varName);
                    ImGui.Text(varName);
                    if (ImGui.RadioButton("开##" + varName, val != 0)) axis.Context.SetVariable(varName, 1);
                    ImGui.SameLine();
                    if (ImGui.RadioButton("关##" + varName, val == 0)) axis.Context.SetVariable(varName, 0);
                }
            }
        }
        else
        {
            ImGui.Text("轴信息"); ImGui.Separator();
            ImGui.TextDisabled("未加载执行轴");
        }

        ImGui.Spacing();

        // 自动加载
        ImGui.Text("加载");
        ImGui.Separator();
        var autoLoad = config.ExecutionAxisAutoLoad;
        if (ImGui.Checkbox("进副本自动加载", ref autoLoad))
        {
            config.ExecutionAxisAutoLoad = autoLoad;
            axis.AutoLoadEnabled = autoLoad;
            _saveConfig();
        }

        // 当前状态
        ImGui.Spacing();
        ImGui.TextColored(axis.IsRunning
            ? new Vector4(0, 1, 0, 1)
            : new Vector4(0.6f, 0.6f, 0.6f, 1),
            axis.IsRunning ? $"● 运行中 — {axis.TimelineName}" : "○ 未启动");

        // 轴文件选择
        ImGui.Spacing();
        ImGui.Text("轴文件");
        ImGui.Separator();

        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "ExecutionTimelines");
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.json").Select(f => Path.GetFileName(f)!).ToArray()
            : [];
        var fileNames = files.Length > 0 ? files : ["(无文件)"];

        int selectedIdx = 0;
        if (!string.IsNullOrEmpty(axis.TimelineName))
        {
            var match = fileNames.FirstOrDefault(f => f == $"{axis.TerritoryId}.json");
            if (match != null) selectedIdx = Array.IndexOf(fileNames, match);
        }
        if (selectedIdx < 0) selectedIdx = 0;

        if (ImGui.Combo("选择文件", ref selectedIdx, fileNames, fileNames.Length))
        {
            var chosen = fileNames[selectedIdx];
            if (chosen != "(无文件)")
            {
                var path = Path.Combine(dir!, chosen);
                axis.LoadFromFile(path);
            }
        }

        ImGui.Spacing();

        // 手动加载/卸载
        if (axis.IsRunning)
        {
            if (ImGui.Button("卸载"))
                axis.Shutdown();
        }
        else
        {
            if (ImGui.Button("加载当前副本"))
                axis.AutoLoadTimeline();
        }

        // 打开编辑器
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("编辑器");
        ImGui.Separator();
        if (ImGui.Button("打开 axflow 编辑器"))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"http://localhost:{port}/axflow-editor.html"); }
            catch { }
        }
        if (ImGui.Button("打开通用编辑器"))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"http://localhost:{port}/editor.html"); }
            catch { }
        }
    }

    private void DrawAssistAxisTab()
    {
        var config = PluginConfig.Instance;
        var axis = Execution.AssistAxis.Instance;
        var port = Plugin.Instance._uiManager?.WebServerPort ?? 5678;

        // === 轴信息 ===
        if (axis.Initialized)
        {
            ImGui.Text("轴信息"); ImGui.Separator();
            ImGui.Text(axis.IsRunning ? "● 运行中" : "○ 未启动");
            if (!string.IsNullOrEmpty(axis.TimelineName))
                ImGui.Text($"名称: {axis.TimelineName}");
        }
        else
        {
            ImGui.Text("轴信息"); ImGui.Separator();
            ImGui.TextDisabled("未加载辅助轴");
        }

        ImGui.Spacing();

        // 自动加载
        ImGui.Text("加载");
        ImGui.Separator();
        var autoLoad = config.AssistAxisAutoLoad;
        if (ImGui.Checkbox("进副本自动加载", ref autoLoad))
        {
            config.AssistAxisAutoLoad = autoLoad;
            if (autoLoad) axis.LoadAssistTimeline();
            else axis.UnloadAssistTimeline();
            _saveConfig();
        }

        // 当前状态
        ImGui.Spacing();
        ImGui.TextColored(axis.IsRunning
            ? new Vector4(0, 1, 0, 1)
            : new Vector4(0.6f, 0.6f, 0.6f, 1),
            axis.IsRunning ? $"● 运行中 — {axis.TimelineName}" : "○ 未启动");

        // 轴文件选择
        ImGui.Spacing();
        ImGui.Text("轴文件");
        ImGui.Separator();

        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "AssistTimelines");
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.txt").Select(f => Path.GetFileName(f)!).ToArray()
            : [];
        var fileNames = files.Length > 0 ? files : ["(无文件)"];

        int selectedIdx = 0;
        if (ImGui.Combo("选择文件", ref selectedIdx, fileNames, fileNames.Length))
        {
            var chosen = fileNames[selectedIdx];
            if (chosen != "(无文件)")
            {
                var path = Path.Combine(dir!, chosen);
                axis.LoadFromFile(path);
            }
        }

        ImGui.Spacing();

        // 手动操作
        if (axis.IsRunning)
        {
            if (ImGui.Button("卸载"))
                axis.UnloadAssistTimeline();
        }
        else
        {
            if (ImGui.Button("加载当前副本"))
                axis.LoadAssistTimeline();
        }

        // 打开编辑器
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("编辑器");
        ImGui.Separator();
        if (ImGui.Button("打开编辑器"))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"http://localhost:{port}/editor.html?axis=assist"); }
            catch { }
        }
    }

    private static void DrawRecording()
    {
        ImGui.Spacing();
        ImGui.Text("副本录制状态");

        var recorder = EncounterRecorder.Instance;
        var isRecording = recorder.IsRecording;

        if (isRecording)
        {
            var seconds = recorder.ElapsedSeconds;
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1),
                $"● 录制中 ({seconds / 60:D2}:{seconds % 60:D2})");
            ImGui.Text($"文件名: {recorder.CurrentFileName}");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "○ 就绪");
        }

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.Text("录制历史:");
        ImGui.Spacing();

        var files = recorder.GetRecordFiles();
        if (files.Length == 0)
        {
            ImGui.TextDisabled("暂无录制记录");
        }
        else
        {
            using var childBorder = new ImRaii.StyleDisposable();
            childBorder.Push(ImGuiStyleVar.ChildBorderSize, 1);
            ImGui.BeginChild("##RecordingList",
                new Vector2(-1, 80), true);

            foreach (var (name, path) in files.Take(20))
            {
                ImGui.Text(name);
                ImGui.SameLine();
                ImGui.TextDisabled($"({path})");
            }

            ImGui.EndChild();
        }

        ImGui.Spacing();
        if (ImGui.Button("打开录制目录"))
        {
            var dir = Path.Combine(
                DService.Instance().PI.ConfigDirectory.FullName, "Recordings");
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Debug($"[Recording] 无法打开目录: {ex.Message}");
            }
        }
    }

    private string _logFilter = "";

    // ----- Debug 测试字段 -----

    // AuraHelper
    private int _dbgAuraHasAura_Target;
    private int _dbgAuraHasAura_BuffId;
    private string _dbgAuraHasAura_Result = "";

    private int _dbgAuraHasAnyAura_Target;
    private string _dbgAuraHasAnyAura_BuffIds = "";
    private string _dbgAuraHasAnyAura_Result = "";

    private int _dbgAuraGetTimeLeft_Target;
    private int _dbgAuraGetTimeLeft_BuffId;
    private string _dbgAuraGetTimeLeft_Result = "";

    private int _dbgAuraHasSelfAura_BuffId;
    private string _dbgAuraHasSelfAura_Result = "";

    private int _dbgAuraHasTargetAura_BuffId;
    private string _dbgAuraHasTargetAura_Result = "";

    // ComboHelper
    private int _dbgComboWasLastCombo_SpellId;
    private string _dbgComboWasLastCombo_Result = "";

    private int _dbgComboComboInWindow_SpellId;
    private int _dbgComboComboInWindow_WindowMs = 15000;
    private string _dbgComboComboInWindow_Result = "";

    private int _dbgComboComboAboutToExpire_SpellId;
    private int _dbgComboComboAboutToExpire_WithinMs = 500;
    private string _dbgComboComboAboutToExpire_Result = "";

    // SpellHelper
    private int _dbgSpellCanUseSpell_Id;
    private string _dbgSpellCanUseSpell_Result = "";

    private int _dbgSpellIsActionReady_Id;
    private string _dbgSpellIsActionReady_TargetId = "E0000000";
    private string _dbgSpellIsActionReady_Result = "";

    private int _dbgSpellGetCd_Id;
    private string _dbgSpellGetCd_Result = "";

    private int _dbgSpellGetMaxCharges_Id;
    private string _dbgSpellGetMaxCharges_Result = "";

    private int _dbgSpellGetCharges_Id;
    private string _dbgSpellGetCharges_Result = "";

    private int _dbgSpellGetChargeCd_Id;
    private string _dbgSpellGetChargeCd_Result = "";

    private int _dbgSpellIsInRange_Id;
    private int _dbgSpellIsInRange_Target;
    private string _dbgSpellIsInRange_Result = "";

    // SpellHistoryHelper
    private int _dbgSpellHistRecentlyUsed_Id;
    private int _dbgSpellHistRecentlyUsed_WithinMs = 500;
    private string _dbgSpellHistRecentlyUsed_Result = "";

    private int _dbgSpellHistGetLastTime_Id;
    private string _dbgSpellHistGetLastTime_Result = "";

    private string _dbgSpellHistGcdCount_Result = "";

    private int _dbgSpellHistRecord_Id;

    // TargetHelper
    private int _dbgTargetGetNearby_Target;
    private float _dbgTargetGetNearby_Range = 5f;
    private string _dbgTargetGetNearby_Result = "";

    private int _dbgTargetIsBehind_Target;
    private string _dbgTargetIsBehind_Result = "";

    private int _dbgTargetIsFlanking_Target;
    private string _dbgTargetIsFlanking_Result = "";

    private int _dbgTargetCastingAOE_Target;
    private int _dbgTargetCastingAOE_Threshold = 3000;
    private string _dbgTargetCastingAOE_Result = "";

    private int _dbgTargetCastingTime_Target;
    private string _dbgTargetCastingTime_Result = "";

    private int _dbgTargetMostCanTarget_SpellId;
    private int _dbgTargetMostCanTarget_MinCount = 1;
    private float _dbgTargetMostCanTarget_Range = 5f;
    private string _dbgTargetMostCanTarget_Result = "";

    private static IGameObject? DbgTgt(int sel) => sel == 0 ? Data.Me.Object : Data.Target.Current;

    private void DrawLog()
    {
        var entries = LogManager.Instance.GetEntries();
        var filtered = string.IsNullOrEmpty(_logFilter)
            ? entries
            : entries.Where(e => e.Type.Contains(_logFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        // 工具栏
        ImGui.Spacing();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##LogFilter", "筛选类型...", ref _logFilter, 64);
        ImGui.SameLine();
        ImGui.TextDisabled($"({filtered.Count}/{entries.Count})");
        ImGui.SameLine();

        if (ImGui.Button("清除"))
        {
            LogManager.Instance.Clear();
        }

        ImGui.Spacing();
        ImGui.Separator();

        // 表格
        if (ImGui.BeginTable("##LogTable", 3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY,
            new Vector2(-1, -1)))
        {
            ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("类型", ImGuiTableColumnFlags.WidthFixed, 220);
            ImGui.TableSetupColumn("内容",
                ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            // 从最新到最旧显示
            var count = filtered.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                var e = filtered[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.Text(e.Timestamp.ToString("HH:mm:ss.fff"));

                ImGui.TableSetColumnIndex(1);
                if (ImGui.Selectable(e.Type))
                {
                    _logFilter = _logFilter == e.Type ? "" : e.Type;
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.TextWrapped(e.Content);
            }

            ImGui.EndTable();
        }
    }

}