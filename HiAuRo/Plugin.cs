using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using HiAuRo.Authoring;
using HiAuRo.Command;
using HiAuRo.Execution;
using HiAuRo.Script;
using HiAuRo.Execution.Events;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;
using HiAuRo.Setting;
using HiAuRo.UI;
using HiAuRo.Decision;
using HiAuRo.Recording;
using HiAuRo.Rendering;
using HiAuRo.Vfx;
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
    public static bool IsWebUI => Instance?._uiManager?.IsWebUI ?? false;
    private readonly MainWindow _mainWindow;
    private readonly WindowSystem _windowSystem;
    private readonly VfxRenderer? _vfxRenderer;
    private readonly Action _windowDrawAction;

    /// <summary>插件实例单例</summary>
    public static Plugin Instance { get; private set; } = null!;

    /// <summary>保存配置到磁盘</summary>
    public static void SaveConfig() => Instance?._config.Save();

    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class</summary>
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            Instance = this;
            _pluginInterface = pluginInterface;

            DService.Init(pluginInterface);
            SettingMgr.Init(_pluginInterface.ConfigDirectory.FullName);
            _config = LoadConfig();
            LogManager.Instance.Init(_pluginInterface.ConfigDirectory.FullName);
            Theme.Mode = _config.ImGuiThemeMode == ImGuiThemeMode.Dark ? Theme.ThemeMode.Dark : Theme.ThemeMode.Light;
            IconHelper.Init();
            SafeFire(HelperUpdater.CheckAndUpdateAsync().ContinueWith(t =>
            {
                DService.Instance()?.Log.Information($"[Lifecycle] HelperUpdater 完成, Loaded={HelperUpdater.Loaded}");
            }), "HelperUpdater");
            DService.Instance().Log.Information("[Lifecycle] HelperUpdater 异步更新已启动");

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

            CommandMgr.Init();
            CombatEnhancementManager.Instance.Init();
            EventSystem.Init();
            GameEventHook.Instance.Init();
            ScriptManager.SetHostDllPath(_pluginInterface.AssemblyLocation.Directory?.FullName ?? "");
            ScriptEngine.Instance.Init();
            EncounterRecorder.Instance.Init();
            CombatRecorder.Instance.Init();
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
            _vfxRenderer = new VfxRenderer();
            DService.Instance().Log.Information($"[VFX] VfxRenderer 初始化完成, IsAvailable={VfxNative.IsAvailable}");
            _uiManager = new UIManager(_config, _pluginInterface, _windowSystem,
                () => _config.Save(), webRoot);
            _uiManager.Init();
            _uiBridge = _uiManager.Bridge;
            if (_uiBridge != null)
            {
                RegisterUiHandlers(_uiBridge);
                AuthoringServer.Instance.Register(_uiBridge);
            }

            _mainWindow = new MainWindow(_config, () => _config.Save());
            _windowSystem.AddWindow(_mainWindow);
#if DEBUG
            _windowDrawAction = () =>
            {
                var _uiTotal = System.Diagnostics.Stopwatch.GetTimestamp();
                var dt = ImGui.GetIO().DeltaTime;
                _vfxRenderer?.Update(dt);
                PositionalVfx.Update(dt);
                _uiManager?.SyncPanelVisibility();
                _windowSystem.Draw();
                Rendering.DebugPoint.Draw();
                Infrastructure.PerfMonitor.Record("UI.Total", _uiTotal);
            };
#else
            _windowDrawAction = () =>
            {
                var dt = ImGui.GetIO().DeltaTime;
                _vfxRenderer?.Update(dt);
                PositionalVfx.Update(dt);
                _uiManager?.SyncPanelVisibility();
                _windowSystem.Draw();
                Rendering.DebugPoint.Draw();
            };
#endif
            _pluginInterface.UiBuilder.Draw += _windowDrawAction;
            _pluginInterface.UiBuilder.OpenMainUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;
            _pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;

            // 加载外部 ACR
            DService.Instance().Log.Information("[ACR] 开始扫描外部 ACR...");
            ACRLifecycle.Init(_pluginInterface.ConfigDirectory.FullName);
            ACRLoader.LoadAll(_pluginInterface.ConfigDirectory.FullName);
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
        CombatRecorder.Instance.Shutdown();
        EncounterRecorder.Instance.Shutdown();
        ScriptEngine.Instance.Shutdown();
        ScriptManager.Shutdown();
        GameEventHook.Instance.Shutdown();
        EventSystem.Shutdown();
        CombatEnhancementManager.Instance.Shutdown();
        CommandMgr.Shutdown();

        PluginLifecycle.Shutdown();

        // 先关 ACR（UnloadRotation 依赖 Plugin.Instance）
        ACRLifecycle.Shutdown();

        // 注销 IPC
        try { DService.Instance().PI.GetIpcProvider<string, object>("HiAuRo.AddMovementDemand").UnregisterAction(); } catch { }

        if (_windowSystem != null)
        {
            _pluginInterface.UiBuilder.Draw -= _windowDrawAction;
            _uiManager?.Dispose();
            _windowSystem.RemoveAllWindows();
        }
        _vfxRenderer?.Dispose();
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

    /// <summary>显示组件展示窗口</summary>
    public void ShowDemoWindow()
    {
        _uiManager?.ShowDemoWindow();
    }

    /// <summary>切换主窗口显隐</summary>
    public void ToggleMainWindow()
    {
        _mainWindow.IsOpen = !_mainWindow.IsOpen;
    }

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
