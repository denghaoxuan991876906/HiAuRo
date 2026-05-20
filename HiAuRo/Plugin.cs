using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using HiAuRo.Authoring;
using HiAuRo.Command;
using HiAuRo.Execution;
using HiAuRo.Execution.Events;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;
using HiAuRo.Setting;
using HiAuRo.UI;
using HiAuRo.Decision;
using HiAuRo.Recording;
using OmenTools;
using HiAuRo.ImGuiLib;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo;

/// <summary>HiAuRo 插件入口</summary>
public partial class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly PluginConfig _config;
    internal WebUiBridge? _uiBridge;
    internal IDalamudPluginInterface PluginInterface => _pluginInterface;
    internal UIManager? _uiManager;

    /// <summary>当前是否处于 WebUI 模式（用于 ACRLifecycle 等判断状态推送通道）</summary>
    public static bool IsWebUI => Instance._uiManager?.IsWebUI ?? false;
    private readonly MainWindow _mainWindow;
    private readonly WindowSystem _windowSystem;

    /// <summary>插件实例单例</summary>
    public static Plugin Instance { get; private set; } = null!;

    /// <summary>保存配置到磁盘</summary>
    public static void SaveConfig() => Instance?._pluginInterface.SavePluginConfig(Instance._config);

    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class</summary>
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            Instance = this;
            _pluginInterface = pluginInterface;

            DService.Init(pluginInterface);
            _config = LoadConfig();
            LogManager.Instance.Init(_pluginInterface.ConfigDirectory.FullName);
            Theme.Mode = _config.ImGuiThemeMode == ImGuiThemeMode.Dark ? Theme.ThemeMode.Dark : Theme.ThemeMode.Light;
            HelperUpdater.CheckAndUpdateAsync().ContinueWith(
                    t => { if (t.Exception != null) DService.Instance().Log.Error($"[Lifecycle] HelperUpdater 失败: {t.Exception.InnerException?.Message}"); },
                    TaskContinuationOptions.OnlyOnFaulted);

            var webRoot = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "web");
            var sourceWebRoot = Path.Combine(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".", "UI", "web");

            // 始终覆盖更新 web 前端文件
            if (Directory.Exists(sourceWebRoot))
            {
                CopyDirectory(sourceWebRoot, webRoot);
                DService.Instance().Log.Information($"[UI] web文件已复制: {sourceWebRoot} → {webRoot}");
            }
            else
            {
                DService.Instance().Log.Error($"[UI] web源目录不存在: {sourceWebRoot}, 悬浮窗将无内容!");
            }

            SettingMgr.Init(_pluginInterface.ConfigDirectory.FullName);
            CommandMgr.Init();
            EventSystem.Init();
            GameEventHook.Instance.Init();
            EncounterRecorder.Instance.Init();
            ExecutionAxis.Instance.Init();

            // 注册 MovementDemand IPC（接收外部分发插件的推送）
            DService.Instance().PI.GetIpcProvider<string, object>("HiAuRo.AddMovementDemand")
                .RegisterAction(json =>
                {
                    try
                    {
                        var demand = System.Text.Json.JsonSerializer.Deserialize<MovementDemand>(json);
                        if (demand != null)
                            DemandBuffer.Add(demand);
                    }
                    catch (Exception ex)
                    {
                        DService.Instance().Log.Debug($"[IPC] AddMovementDemand 反序列化失败: {ex.Message}");
                    }
                });

            RuntimeCore.Start();
            DecisionEngine.Instance.Init();
            AssistAxis.Instance.Init();
            ModeSwitch.TryAutoSwitch();
            CombatContext.StateChanged += OnCombatStateChanged;

            ACR.HotkeyHelper.OnExecuted += OnHotkeyExecuted;
            ACR.QTHelper.OnChanged += OnQtChanged;

            _windowSystem = new WindowSystem("HiAuRo");
            _uiManager = new UIManager(_config, _pluginInterface, _windowSystem,
                () => _pluginInterface.SavePluginConfig(_config), webRoot);
            _uiManager.Init();
            _uiBridge = _uiManager.Bridge;
            if (_uiBridge != null)
            {
                RegisterUiHandlers(_uiBridge);
                AuthoringServer.Instance.Register(_uiBridge);
            }

            _mainWindow = new MainWindow(_config, () => _pluginInterface.SavePluginConfig(_config));
            _windowSystem.AddWindow(_mainWindow);
            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenMainUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;
            _pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;

            // 加载外部 ACR
            DService.Instance().Log.Information("[ACR] 开始扫描外部 ACR...");
            ACRLifecycle.Init(_pluginInterface.ConfigDirectory.FullName);
            ACRLoader.LoadAll(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".");
            DService.Instance().Log.Information("[ACR] 扫描完成, 等待职业切换触发加载");
            ACRLifecycle.ForceRecheck(); // RuntimeCore 可能先于 LoadAll 跑了第一帧，强制重检

            // 加载通用插件
            PluginLifecycle.Init(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".",
                _pluginInterface.ConfigDirectory.FullName);

            PluginWindowManager.Init(_windowSystem);

            try
            {
                var merged = new TriggerCatalog();
                TriggerCatalogBuilder.MergeInto(merged,
                    TriggerCatalogBuilder.BuildFromAssembly(typeof(Execution.Triggers.Cond.TriggerCond_敌人读条).Assembly, "builtin"));

                foreach (var acrAsm in ACRLoader.LoadedAcrAssemblies)
                {
                    var acrCatalog = TriggerCatalogBuilder.BuildFromAssembly(acrAsm, "acr");
                    TriggerCatalogBuilder.MergeInto(merged, acrCatalog);
                }

                var catalogPath = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "trigger-catalog.json");
                var catalogJson = System.Text.Json.JsonSerializer.Serialize(merged,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                File.WriteAllText(catalogPath, catalogJson);
                DService.Instance().Log.Information($"[TriggerCatalog] 已生成 ({merged.Conditions.Count}C {merged.Actions.Count}A {merged.Scripts.Count}S)");
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[TriggerCatalog] 生成失败: {ex.Message}");
            }

            var modeText = _config.UIMode == Infrastructure.UIMode.WebUI ? "WebUI" : "ImGui";
            DService.Instance().Chat.Print($"[HiAuRo] /hi on|off|toggle|status|panel|reload  UI模式: {modeText}");
            DService.Instance().Log.Information($"[Lifecycle] HiAuRo 初始化完成。版本: {_config.LastSeenPluginVersion}  模式: {modeText}");
            DService.Instance().Log.Information($"[Lifecycle] 状态: Mode={modeText} ACR={ACRLifecycle.CurrentAcrName}");
        }
        catch (Exception ex)
        {
            try
            {
                DService.Instance().Log.Error($"[Lifecycle] 插件构造函数异常，尝试释放已分配资源: {ex}");
                Dispose();
            }
            catch (Exception disposeEx)
            {
                DService.Instance().Log.Error($"[Lifecycle] 释放资源时再次异常: {disposeEx}");
            }
            throw;
        }
    }

    private static void OnCombatStateChanged(CombatContext.State oldState, CombatContext.State newState)
    {
        if (newState is CombatContext.State.OutOfCombat or CombatContext.State.InCombat)
            ModeSwitch.TryAutoSwitch();
    }

    internal async Task UploadCatalogAsync()
    {
        var config = _config;
        if (string.IsNullOrWhiteSpace(config.GitHubToken))
        {
            DService.Instance().Chat.Print("[HiAuRo] 请先在设置中配置 GitHubToken");
            return;
        }

        var catalogPath = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "trigger-catalog.json");
        if (!File.Exists(catalogPath))
        {
            DService.Instance().Chat.Print("[HiAuRo] 目录未生成");
            return;
        }

        var json = await File.ReadAllTextAsync(catalogPath);
        var result = await CatalogSync.UploadToGitHubAsync(
            json, config.CatalogRepo, config.CatalogBranch, config.GitHubToken, onlyCloudSync: true);

        DService.Instance().Chat.Print(result.Success
            ? $"[HiAuRo] 目录已上传 → {result.CommitUrl}"
            : $"[HiAuRo] 上传失败: {result.Message}");
    }

    /// <summary>释放插件资源</summary>
    public void Dispose()
    {
        CombatContext.StateChanged -= OnCombatStateChanged;
        ACR.HotkeyHelper.OnExecuted -= OnHotkeyExecuted;
        ACR.QTHelper.OnChanged -= OnQtChanged;

        RuntimeCore.Shutdown();
        CombatContext.Reset();
        ExecutionAxis.Instance.Shutdown();
        AssistAxis.Instance.Shutdown();
        EncounterRecorder.Instance.Shutdown();
        GameEventHook.Instance.Shutdown();
        EventSystem.Shutdown();
        CommandMgr.Shutdown();

        PluginLifecycle.Shutdown();

        // 先关 ACR（UnloadRotation 依赖 Plugin.Instance）
        ACRLifecycle.Shutdown();

        if (_windowSystem != null)
        {
            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
            _uiManager?.Dispose();
            _windowSystem.RemoveAllWindows();
        }
        Instance = null!;
        PluginConfig.Instance = null!;

        // 清除静态缓存（避免下次加载残留）
        ACRLoader.UnloadAll();
        Execution.ExecutionJsonLoader.Clear();
        Execution.ScriptCompiler.ClearCache();
        Decision.DecisionSkillRegistry.Clear();
        ACR.HotkeyPoller.Clear();
        ACR.SpellHistoryHelper.Reset();

        LogManager.Instance.Dispose();
        DService.Uninit();
    }

    private static void RegisterUiHandlers(WebUiBridge bridge)
    {
        bridge.On("toggleACR", _d =>
        {
            if (RuntimeCore.IsRunning) RuntimeCore.Stop();
            else RuntimeCore.Start();
            _ = SendStatusState();
        });

        bridge.On("pause", _d =>
        {
            HiAuRo.ACR.MainControlHelper.TogglePause();
            _ = SendPauseState();
        });

        bridge.On("saveACR", _ =>
        {
            HiAuRo.ACR.MainControlHelper.Save();
        });

        bridge.On("hotkey", data =>
        {
            if (data == null) { DService.Instance().Log.Warning("[UI] hotkey: data is null"); return; }
            var id = data.Value.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (id == null) { DService.Instance().Log.Warning("[UI] hotkey: id not found in data"); return; }
            if (!RuntimeCore.IsRunning) { DService.Instance().Log.Information($"[UI] hotkey: '{id}' ignored (ACR 未启动)"); return; }
            var all = HiAuRo.ACR.HotkeyHelper.GetAll();
            var match = all.FirstOrDefault(r => r.Id == id);
            if (match == null) { DService.Instance().Log.Warning($"[UI] hotkey: '{id}' not found in {all.Count} registered resolvers"); return; }
            var check = match.Check();
            if (check < 0) { DService.Instance().Log.Information($"[UI] hotkey: '{id}' blocked (Check={check})"); return; }
            DService.Instance().Log.Information($"[UI] hotkey: executing '{id}' ({match.Label}) Check={check}");
            // 技能执行必须走 Dalamud 主线程
            var hotkeyId = id;
            DService.Instance().Framework.RunOnFrameworkThread(() => HiAuRo.ACR.HotkeyHelper.ExecuteById(hotkeyId));
        });

        bridge.On("qttoggle", data =>
        {
            if (data == null) return;
            var id = data.Value.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (id != null) HiAuRo.ACR.QTHelper.Toggle(id);
        });

        bridge.On("setHkBinding", data =>
        {
            if (data == null) return;
            var id = data.Value.TryGetProperty("id", out var i1) ? i1.GetString() : null;
            var key = data.Value.TryGetProperty("key", out var i2) ? i2.GetString() : null;
            if (id != null && key != null) HiAuRo.ACR.HotkeyHelper.SetBinding(id, key);
        });

        bridge.On("saveUiSettings", data =>
        {
            if (data == null) { DService.Instance().Log.Debug("[UI] saveUiSettings: data is null"); return; }
            var json = data.Value.GetRawText();
            DService.Instance().Log.Debug($"[UI] saveUiSettings 收到: {json.Length} 字节");
            try
            {
                var s = System.Text.Json.JsonSerializer.Deserialize<HiAuRo.ACR.UiSettings>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (s != null)
                {
                    DService.Instance().Log.Debug($"[UI] 反序列化成功 qtCols={s.QtCols} hkCols={s.HkCols}");
                    HiAuRo.Setting.SettingMgr.SaveAcrUiSettings(
                        HiAuRo.Runtime.ACRLifecycle.CurrentAuthor,
                        HiAuRo.Runtime.ACRLifecycle.CurrentJobId, s);
                    _ = SendUiSettings(s);
                }
                else { DService.Instance().Log.Debug("[UI] 反序列化结果为 null"); }
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[UI] saveUiSettings 异常: {ex}"); }
        });

        // 接收前端调试日志
        bridge.On("log", data =>
        {
            if (data == null) return;
            var msg = data.Value.TryGetProperty("msg", out var m) ? m.GetString() : "";
            var level = data.Value.TryGetProperty("level", out var l) ? l.GetString() : "info";
            var src = data.Value.TryGetProperty("src", out var s) ? s.GetString() : "web";
            var text = $"[Web:{src}] {msg}";
            switch (level)
            {
                case "error": DService.Instance().Log.Error(text); break;
                case "warn": DService.Instance().Log.Warning(text); break;
                default: DService.Instance().Log.Information(text); break;
            }
        });

        // 内容尺寸自适应：JS 上报 overlay 内容实际尺寸 → Browsingway IPC 调整窗口
        bridge.On("contentResize", data =>
        {
            if (data is null) return;
            if (Runtime.ACRLifecycle.IsLoadingRotation) return;
            var overlay = data.Value.TryGetProperty("overlay", out var o) ? o.GetString() : null;
            var width = data.Value.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
            var height = data.Value.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            if (string.IsNullOrEmpty(overlay) || width <= 0 || height <= 0) return;

            // MainWindow 固定尺寸，Qt/Hotkey 自适应
            if (overlay == "MainWindow") return;

            // 更新 PluginConfig 中对应 overlay 的尺寸
            var ol = Instance._config.Overlays?.FirstOrDefault(x => x.Name == overlay);
            if (ol != null) { ol.Width = width; ol.Height = height; }

            // 通知 Browsingway（须在主线程执行）
            var ipc = Instance._uiManager?.BrowsingwayIpc;
            if (ipc != null)
            {
                var oName = overlay;
                var oW = width; var oH = height;
                DService.Instance().Framework.RunOnFrameworkThread(() => ipc.ResizeOverlay(oName, oW, oH));
            }
        });
    }

    private static async Task SendStatusState()
    {
        if (!IsWebUI) return;
        await Instance._uiBridge!.SendAsync(new
        {
            type = "acrState",
            data = new { enabled = RuntimeCore.IsRunning }
        });
    }

    private static async Task SendUiSettings(HiAuRo.ACR.UiSettings s)
    {
        if (!IsWebUI) return;
        await Instance._uiBridge!.SendAsync(new
        {
            type = "uiSettings",
            data = new
            {
                qtCols = s.QtCols,
                qtBtnW = s.QtBtnW,
                qtVisible = s.QtVisible,
                hkCols = s.HkCols,
                hkBtnSize = s.HkBtnSize,
                hkVisible = s.HkVisible,
                hkBindings = s.HkBindings
            }
        });
    }

    private static async Task SendPauseState()
    {
        if (!IsWebUI) return;
        await Instance._uiBridge!.SendAsync(new
        {
            type = "pauseChanged",
            data = new { paused = HiAuRo.ACR.MainControlHelper.IsPaused }
        });
    }

    private static void OnHotkeyExecuted(string id, string label)
    {
        if (!IsWebUI) return;
        _ = Instance._uiBridge!.SendAsync(new
        {
            type = "hotkeyExecuted",
            data = new { id, label }
        });
    }

    private static void OnQtChanged(string id, bool value)
    {
        if (!IsWebUI) return;
        _ = Instance._uiBridge!.SendAsync(new
        {
            type = "qtChanged",
            data = new { id, value }
        });
    }

    /// <summary>显示组件展示窗口</summary>
    public void ShowDemoWindow()
    {
        _uiManager?.ShowDemoWindow();
    }

    #region 配置

    private PluginConfig LoadConfig()
    {
        var config = _pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        PluginConfig.Instance = config;
        config.LoadCount++;

        var migrated = false;

        // 方向 2 迁移：ActionPanel 拆分为 QtWindow + HotkeyWindow
        if (config.Overlays?.Any(o => o.Name == "ActionPanel") == true)
        {
            config.Overlays = config.Overlays
                .Where(o => o.Name != "ActionPanel")
                .Append(new OverlayWindowSetting { Name = "QtWindow", Url = "http://localhost:5678/qt.html", Width = 320, Height = 80 })
                .Append(new OverlayWindowSetting { Name = "HotkeyWindow", Url = "http://localhost:5678/hotkey.html", Width = 320, Height = 100 })
                .ToArray();
            migrated = true;
        }

        // 修复之前 contentResize 错误写入 MainWindow 的尺寸（高度 < 100 视为异常值）
        var mw = config.Overlays?.FirstOrDefault(o => o.Name == "MainWindow");
        if (mw != null && mw.Height < 100)
        {
            mw.Width = 310;
            mw.Height = 480;
            migrated = true;
        }

        if (migrated)
            _pluginInterface.SavePluginConfig(config);

        DService.Instance().Log.Information(
            $"[Config] SchemaVersion={config.Version}, LoadCount={config.LoadCount}, DebugEnabled={config.DebugEnabled}");
        return config;
    }

    #endregion

    #region 日志

    /// <summary>输出调试日志（受 DebugEnabled 开关控制）</summary>
    public void LogDebug(string message)
    {
        if (_config.DebugEnabled)
            DService.Instance().Log.Debug($"[Debug] {message}");
    }

    /// <summary>输出格式化调试日志（受 DebugEnabled 开关控制）</summary>
    public void LogDebug(string messageTemplate, params object[] args)
    {
        if (_config.DebugEnabled)
            DService.Instance().Log.Debug($"[Debug] {string.Format(messageTemplate, args)}");
    }

    #endregion

    #region 工具

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
            var dest = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }

    #endregion
}
