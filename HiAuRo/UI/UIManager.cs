using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using HiAuRo.Infrastructure;
using HiAuRo.ACR;
using HiAuRo.ImGuiLib;

namespace HiAuRo.UI;

/// <summary>
/// UI 模式管理器 —— 集中管理 WebUI (CEF) 和 ImGui 两种悬浮窗模式
/// </summary>
internal class UIManager : IDisposable
{
    private readonly PluginConfig _config;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly WindowSystem _windowSystem;
    private readonly Action _saveConfig;

    private WebUiServer? _uiServer;
    private WebUiBridge? _uiBridge;
    private BrowsingwayIpc? _browsingwayIpc;

    private OverlayStatusBar? _overlayStatusBar;
    private OverlayQtPanel? _overlayQtPanel;
    private OverlayHotkeyPanel? _overlayHotkeyPanel;
    private Rendering.PositionalOverlay? _positionalOverlay;
    private DemoWindow? _demoWindow;
    private LogWindow? _logWindow;
    private readonly List<Window> _customWindows = [];

    public bool IsWebUI => _config.UIMode == UIMode.WebUI;

    /// <summary>WebSocket 桥接</summary>
    public WebUiBridge? Bridge => _uiBridge;

    /// <summary>Web 服务器端口</summary>
    public int WebServerPort => _uiServer?.Port ?? 5678;

    /// <summary>Browsingway IPC 服务</summary>
    public BrowsingwayIpc? BrowsingwayIpc => _browsingwayIpc;

    public UIManager(PluginConfig config, IDalamudPluginInterface pluginInterface,
                     WindowSystem windowSystem, Action saveConfig)
    {
        _config = config;
        _pluginInterface = pluginInterface;
        _windowSystem = windowSystem;
        _saveConfig = saveConfig;
    }

    /// <summary>根据当前配置初始化 UI 资源</summary>
    public void Init()
    {
        DService.Instance().Log.Information("[UIManager] 开始初始化 UI...");

        // 强制使用 ImGui 模式（WebUI 暂不可用）
        if (_config.UIMode == UIMode.WebUI)
        {
            _config.UIMode = UIMode.ImGui;
            _saveConfig();
            DService.Instance().Log.Information("[UIManager] WebUI 暂不可用，已强制切换到 ImGui 模式");
        }

        try
        {
            _uiBridge = new WebUiBridge();
            _uiServer = new WebUiServer(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".", _uiBridge);
            _uiServer.Start();
            DService.Instance().Log.Information($"[UIManager] WebUiServer 启动完成 端口={_uiServer.Port}");

            // 启动 Browsingway IPC（直接注册 overlay，按当前模式决定显隐）
            _browsingwayIpc = new BrowsingwayIpc(_uiServer.Port, () => _config.UIMode);
            var overlays = _config.Overlays ?? [];
            _browsingwayIpc.Init(overlays);
            if (_config.UIMode != UIMode.WebUI)
                _browsingwayIpc.HideAll(overlays);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[UIManager] WebUiServer 初始化失败: {ex}");
        }

        if (_config.UIMode == UIMode.ImGui)
        {
            CreateImGuiOverlays();
            DService.Instance().Log.Information("[UIManager] ImGui Overlay 已创建");
        }
    }

    /// <summary>动态切换 UI 模式</summary>
    public void SwitchTo(UIMode mode)
    {
        if (mode == _config.UIMode) return;

        if (mode == UIMode.WebUI)
        {
            DService.Instance().Chat.Print("[HiAuRo] WebUI 暂不可用");
            return;
        }

        _config.UIMode = mode;
        _browsingwayIpc?.DisableAll(_config.Overlays ?? []);
        CreateImGuiOverlays();
        _saveConfig();
        DService.Instance().Chat.Print($"[HiAuRo] UI 模式已切换为: ImGui");
    }

    private void CreateImGuiOverlays()
    {
        if (_overlayStatusBar != null) return; // 防止重复创建

        _demoWindow = new DemoWindow();
        _logWindow = new LogWindow();
        _overlayStatusBar = new OverlayStatusBar(_config, _saveConfig);
        _overlayQtPanel = new OverlayQtPanel(_config, _saveConfig);
        _overlayHotkeyPanel = new OverlayHotkeyPanel(_config, _saveConfig);
        _positionalOverlay = new Rendering.PositionalOverlay();

        _windowSystem.AddWindow(_demoWindow);
        _windowSystem.AddWindow(_logWindow);
        _windowSystem.AddWindow(_overlayStatusBar);
        _windowSystem.AddWindow(_overlayQtPanel);
        _windowSystem.AddWindow(_overlayHotkeyPanel);
        _windowSystem.AddWindow(_positionalOverlay);
#if DEBUG
        _windowSystem.AddWindow(new DebugPerfWindow());
#endif
    }

    private void RemoveImGuiOverlays()
    {
        try { if (_demoWindow != null) { _windowSystem.RemoveWindow(_demoWindow); _demoWindow = null; } } catch { }
        try { if (_logWindow != null) { _windowSystem.RemoveWindow(_logWindow); _logWindow = null; } } catch { }
        try { if (_overlayStatusBar != null) { _windowSystem.RemoveWindow(_overlayStatusBar); _overlayStatusBar = null; } } catch { }
        try { if (_overlayQtPanel != null) { _windowSystem.RemoveWindow(_overlayQtPanel); _overlayQtPanel = null; } } catch { }
        try { if (_overlayHotkeyPanel != null) { _windowSystem.RemoveWindow(_overlayHotkeyPanel); _overlayHotkeyPanel = null; } } catch { }
        try { if (_positionalOverlay != null) { _windowSystem.RemoveWindow(_positionalOverlay); _positionalOverlay = null; } } catch { }
    }

    public void SetAllOverlaysVisible(bool visible)
    {
        if (_overlayStatusBar != null) _overlayStatusBar.IsOpen = visible;
        if (_overlayQtPanel != null) _overlayQtPanel.IsOpen = visible;
        if (_overlayHotkeyPanel != null) _overlayHotkeyPanel.IsOpen = visible;

        var overlays = _config.Overlays ?? [];
        if (visible)
            _browsingwayIpc?.ShowConfigured(overlays);
        else
            _browsingwayIpc?.HideAll(overlays);
    }

    public void SyncPanelVisibility()
    {
        if (_overlayStatusBar != null && _overlayStatusBar.IsOpen && !_config.AllOverlaysVisible)
            _overlayStatusBar.IsOpen = false;
        if (_overlayQtPanel != null)
            _overlayQtPanel.IsOpen = _config.AllOverlaysVisible && _config.QtPanelVisible;
        if (_overlayHotkeyPanel != null)
            _overlayHotkeyPanel.IsOpen = _config.AllOverlaysVisible && _config.HotkeyPanelVisible;
    }

    /// <summary>打开日志窗口</summary>
    public void OpenLogWindow()
    {
        if (_logWindow != null)
            _logWindow.IsOpen = true;
    }

    public void ShowDemoWindow()
    {
        if (_demoWindow != null)
        {
            _demoWindow.IsOpen = true;
        }
        else
        {
            _demoWindow = new DemoWindow();
            _windowSystem.AddWindow(_demoWindow);
            _demoWindow.IsOpen = true;
        }
    }

    /// <summary>注册 ACR 自定义窗口</summary>
    public void AddCustomWindow(ICustomWindow cw)
    {
        var window = new CustomWindowHost(cw);
        _customWindows.Add(window);
        _windowSystem.AddWindow(window);
        if (cw.IsOpenByDefault)
            window.IsOpen = true;
        DService.Instance().Log.Information($"[UIManager] 自定义窗口已添加: {cw.Name}");
    }

    /// <summary>移除所有 ACR 自定义窗口（ACR 卸载时调用）</summary>
    public void RemoveCustomWindows()
    {
        foreach (var w in _customWindows)
        {
            w.IsOpen = false;
            try { _windowSystem.RemoveWindow(w); } catch { }
        }
        _customWindows.Clear();
        DService.Instance().Log.Information($"[UIManager] 自定义窗口已全部移除");
    }

    public void Dispose()
    {
        _browsingwayIpc?.DisableAll(_config.Overlays ?? []);
        _browsingwayIpc?.Dispose();
        try { RemoveCustomWindows(); } catch { }
        try { RemoveImGuiOverlays(); } catch { }
        _uiServer?.Stop();
        _uiBridge?.Dispose();
        DService.Instance().Log.Information("[UIManager] UIManager 已释放");
    }

    /// <summary>
    /// ICustomWindow → Dalamud Window 适配器
    /// </summary>
    private sealed class CustomWindowHost : Window
    {
        private readonly ICustomWindow _cw;

        public CustomWindowHost(ICustomWindow cw) : base($"{cw.Name}##Custom")
        {
            _cw = cw;
            var sz = cw.DefaultSize;
            if (sz.HasValue)
                SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = sz.Value,
                    MaximumSize = sz.Value
                };
            else
                SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(100, 50),
                    MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
                };
            IsOpen = cw.IsOpenByDefault;
        }

        public override void Draw()
        {
            try
            {
                _cw.Draw();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), $"Draw error: {ex.Message}");
            }
        }
    }
}
