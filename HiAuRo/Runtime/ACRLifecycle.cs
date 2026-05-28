using System.Linq;
using System.Runtime.Loader;
using HiAuRo.ACR;
using HiAuRo.ACR.Internal;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;

namespace HiAuRo.Runtime;

/// <summary>
/// ACR 生命周期管理 —— 职业切换自动加载
/// </summary>
public static class ACRLifecycle
{
    /// <summary>AI 运行器</summary>
    public static AIRunner Runner { get; } = new();
    /// <summary>当前 ACR 入口</summary>
    public static IRotationEntry? CurrentEntry { get; private set; }
    /// <summary>当前 ACR 名称</summary>
    public static string CurrentAcrName => CurrentEntry?.AuthorName ?? "无ACR";
    /// <summary>当前 ACR 作者</summary>
    public static string CurrentAuthor => CurrentEntry?.AuthorName ?? "";
    /// <summary>当前职业 ID</summary>
    public static uint CurrentJobId { get; private set; }
    /// <summary>ISettingsProvider 缓存（供显式 save 遍历）</summary>
    private static readonly Dictionary<string, (IRotationEntry Entry, Type SettingsType)> _settingsProviders = [];
    private static readonly object _settingsLock = new();
    /// <summary>当 ACR 作者未实现 ISettingsProvider{T} 时的默认 AcrSettings 实例</summary>
    private static AcrSettings? _defaultSettings;
    /// <summary>是否正在加载 Rotation</summary>
    public static bool IsLoadingRotation { get; private set; }

    /// <summary>外部 ACR: JobId → (Factory, SettingDir)</summary>
    private static readonly Dictionary<uint, (Func<IRotationEntry> Factory, string SettingDir)> _acrRegistry = [];
    /// <summary>外部 ALC 引用（用于 Reload 卸载）</summary>
    private static readonly List<AssemblyLoadContext> _externalAlcs = [];

    private static uint _lastJob;
    private static bool _resetCalled;

    /// <summary>注册外部 ACR</summary>
    public static void RegisterExternal(uint jobId, Func<IRotationEntry> factory, string settingDir)
    {
        _acrRegistry[jobId] = (factory, settingDir);
    }

    /// <summary>注册外部 ALC</summary>
    public static void RegisterContext(AssemblyLoadContext alc)
    {
        _externalAlcs.Add(alc);
    }

    /// <summary>强制下一帧重新检查职业（用于加载后触发首次匹配）</summary>
    public static void ForceRecheck() { _lastJob = 0; }

    /// <summary>初始化</summary>
    public static void Init(string settingRoot) { }

    /// <summary>清除静态缓存（插件卸载时调用）</summary>
    public static void Shutdown()
    {
        UnloadRotation();
        _acrRegistry.Clear();
        foreach (var alc in _externalAlcs)
        {
            try { alc.Unload(); }
            catch { }
        }
        _externalAlcs.Clear();
        _lastJob = 0;
        _resetCalled = false;
    }

    /// <summary>每帧由 RuntimeCore 调用</summary>
    public static void Update()
    {
        CheckJobSwitch();

        var state = CombatContext.CurrentState;
        if (state == CombatContext.State.Idle || state == CombatContext.State.Zoning)
        {
            _resetCalled = false;
            Runner.Update(); // 非战斗也跑 Check（blockBuild 阻止执行）
            return;
        }

        if (state == CombatContext.State.OutOfCombat)
        {
            if (!_resetCalled) { Runner.Reset(); _resetCalled = true; }
            Runner.Update(); // 非战斗也跑 Check（blockBuild 阻止执行）
            return;
        }

        _resetCalled = false;
        Runner.Update();
    }

    /// <summary>每帧同步 QT/Hotkey/运行状态到 ImGuiOverlayState（ImGui 模式下）</summary>
    internal static void PushImGuiState()
    {
        if (Plugin.IsWebUI || CurrentEntry == null) return;
        ImGuiOverlayState.UpdateStatus(CurrentAcrName, RuntimeCore.IsRunning,
            ACR.MainControlHelper.IsPaused, ACR.HotkeyHelper.GetAll(), ACR.QTHelper.GetAll());
    }

    private static void CheckJobSwitch()
    {
        if (!HiAuRo.Data.IsReady) return;

        var currentJob = Data.Me.ClassJob;
        if (currentJob == _lastJob && currentJob != 0) return;
        _lastJob = currentJob;

        DService.Instance().Log.Information($"[ACR] 职业切换: {_lastJob} → {currentJob}");

        if (_acrRegistry.TryGetValue(currentJob, out var reg))
        {
            DService.Instance().Log.Information($"[ACR] 找到匹配ACR: {reg.SettingDir}");
            LoadRotation(reg.Factory(), reg.SettingDir);
        }
        else
        {
            DService.Instance().Log.Information($"[ACR] 无匹配ACR, 卸载");
            UnloadRotation();
        }

        if (Plugin.IsWebUI && Plugin.Instance._uiBridge != null)
        {
            _ = Plugin.Instance._uiBridge.SendAsync(new
            {
                type = "status",
                data = new
                {
                    job = CurrentAcrName,
                    enabled = RuntimeCore.IsRunning,
                    paused = ACR.MainControlHelper.IsPaused,
                    hotkeys = ACR.HotkeyHelper.GetAll().Select(r => new
                    {
                        id = r.Id,
                        label = r.Label,
                        iconId = r.IconId,
                        iconUrl = HiAuRo.UI.IconServer.GetIconUrl(r.IconId),
                        available = r.Check() >= 0,
                        binding = ACR.HotkeyHelper.GetBinding(r.Id)
                    }).ToList(),
                    qts = ACR.QTHelper.GetAll().Select(q => new
                    {
                        id = q.Id,
                        label = q.Label,
                        value = q.Value,
                        tooltip = q.Tooltip,
                        color = q.Color,
                        binding = q.HotkeyBinding
                    }).ToList()
                }
            });
        }
        else
        {
            ImGuiOverlayState.UpdateStatus(CurrentAcrName, RuntimeCore.IsRunning,
                ACR.MainControlHelper.IsPaused, ACR.HotkeyHelper.GetAll(), ACR.QTHelper.GetAll());
        }
    }

    /// <summary>热重载</summary>
    public static void Reload()
    {
        UnloadRotation();

        _acrRegistry.Clear();

        // 卸载所有外部 ALC
        foreach (var alc in _externalAlcs)
        {
            try { alc.Unload(); }
            catch (Exception ex) { DService.Instance().Log.Error($"[ACR] 卸载 ALC 失败: {ex.Message}"); }
        }
        _externalAlcs.Clear();

        // 重新扫描
        ACRLoader.UnloadAll();
        ACRLoader.LoadAll(Plugin.Instance.PluginInterface.ConfigDirectory.FullName);

        _lastJob = 0;
        CheckJobSwitch();
    }

    private static void LoadRotation(IRotationEntry entry, string settingFolder)
    {
        IsLoadingRotation = true;
        UnloadRotation();

        // 切换 ACR 时重置 GCD 能力技计数和上限
        Data.Combat.AbilityCountInGcd = 0;
        Data.Combat.LastAbilityUseTime = 0;
        Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;

        CurrentJobId = _lastJob;

        AcrSettings? loadedSettings = null;

        // 自动检测 ISettingsProvider<T> 接口 → 加载 settings 并注入
        var providerInterface = entry.GetType()
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISettingsProvider<>));
        if (providerInterface != null)
        {
            var tType = providerInterface.GetGenericArguments()[0];
            var loadMethod = typeof(HiAuRo.Setting.SettingMgr).GetMethod(nameof(HiAuRo.Setting.SettingMgr.GetAcrJobSetting));
            if (loadMethod == null)
            {
                DService.Instance().Log.Error($"[ACR] GetAcrJobSetting 方法未找到");
                // skip injection — fall through
            }
            else
            {
                loadMethod = loadMethod.MakeGenericMethod(tType);
                try
                {
                    var acrSettings = loadMethod.Invoke(null, [entry.AuthorName, CurrentJobId]);
                    providerInterface.GetProperty("Settings")?.SetValue(entry, acrSettings);
                    if (acrSettings is AcrSettings acr)
                    {
                        acr._author = entry.AuthorName;
                        acr._jobId = CurrentJobId;
                        loadedSettings = acr;
                    }
                    lock (_settingsLock)
                    {
                        _settingsProviders[GetProviderKey(entry.AuthorName, CurrentJobId)] = (entry, tType);
                    }
                    DService.Instance().Log.Information($"[ACR] ISettingsProvider<{tType.Name}> 已注入: author={entry.AuthorName} jobId={CurrentJobId}");
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Error($"[ACR] Settings 加载/注入失败: {ex.Message}");
                }
            }
        }

        DService.Instance().Log.Information($"[ACR] LoadRotation 开始: author={entry.AuthorName}, jobId={CurrentJobId}, settingFolder={settingFolder}");
        Runner.Load(entry, settingFolder);
        CurrentEntry = entry;
        DService.Instance().Log.Information($"[ACR] Runner.Load 完成, CurrentRotation={Runner.CurrentRotation != null}");

        // 注册 ACR 自定义触发类型
        if (Runner.CurrentRotation != null)
            HiAuRo.Execution.ExecutionJsonLoader.RegisterFromRotation(Runner.CurrentRotation);

        // 确保 UI 设置始终有 AcrSettings 实例
        if (loadedSettings == null)
        {
            loadedSettings = HiAuRo.Setting.SettingMgr.GetAcrJobSetting<DefaultAcrSettings>(entry.AuthorName, CurrentJobId);
            loadedSettings._author = entry.AuthorName;
            loadedSettings._jobId = CurrentJobId;
            _defaultSettings = loadedSettings;
        }

        // 恢复热键绑定（从 AcrSettings.HkBindings）
        foreach (var (id, key) in loadedSettings.HkBindings)
            ACR.HotkeyHelper.SetBinding(id, key);

        // QT 值变更自动保存（先注册回调，值恢复在 RegisterControls 之后）
        ACR.QTHelper.OnChanged += OnQtChanged;
        ACR.HotkeyHelper.OnExecuted += OnHkExecuted;

        // 收集 ACR 作者声明的 UI 控件 → 推送到 Web 前端动态渲染
        List<HiAuRo.UI.UiControlDef>? controls = null;
        var ui = entry.GetRotationUI();
        if (ui != null)
        {
            var builder = new HiAuRo.UI.UiBuilderImpl();
            ui.RegisterControls(builder);
            controls = builder.GetControls();
            var tabCount = controls.Count(c => c.Type == "tab");
            DService.Instance().Log.Information($"[ACR] UI控件收集: {controls.Count}个 (tabs={tabCount} hks={controls.Count(c=>c.Type=="qthotkey")} qts={controls.Count(c=>c.Type=="qttoggle")} mainCtrl={controls.Count(c=>c.Type=="maincontrol")})");

            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(controls,
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, WriteIndented = false });
                DService.Instance().Log.Information($"[ACR] controls JSON ({json.Length} chars): {json}");
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[ACR] controls 序列化异常: {ex.Message}"); }

            if (Plugin.IsWebUI && Plugin.Instance._uiBridge != null)
            {
                _ = Plugin.Instance._uiBridge.SendAsync(new
                {
                    type = "controls",
                    data = controls
                });
                Plugin.Instance._uiBridge.CacheControls(controls);
            }
            ImGuiOverlayState.UpdateControls(controls);

            // 从已加载的 AcrSettings 填充 UI 控件初始值
            HiAuRo.Setting.SettingMgr.SyncControlsFromSettings(
                loadedSettings, ImGuiOverlayState.ControlValues, controls);

            DService.Instance().Log.Information("[ACR] controls 消息已发送 + 已缓存");
        }
        else
        {
            DService.Instance().Log.Warning("[ACR] GetRotationUI() 返回 null, 无 UI 控件");
        }

        // 恢复 QT 值（必须在 RegisterControls 之后，否则 key 尚未注册）
        foreach (var (id, value) in loadedSettings.QtValues)
            ACR.QTHelper.SetValue(id, value);

        // 推送 UI 设置（从 AcrSettings 读取）
        if (Plugin.IsWebUI && Plugin.Instance._uiBridge != null)
        {
            _ = Plugin.Instance._uiBridge.SendAsync(new
            {
                type = "uiSettings",
                data = new
                {
                    qtCols = loadedSettings.QtCols,
                    qtBtnW = loadedSettings.QtBtnW,
                    qtVisible = loadedSettings.QtVisible,
                    hkCols = loadedSettings.HkCols,
                    hkBtnSize = loadedSettings.HkBtnSize,
                    hkVisible = loadedSettings.HkVisible,
                    hkBindings = loadedSettings.HkBindings
                }
            });
            Plugin.Instance._uiBridge.CacheUiSettings(new
            {
                qtCols = loadedSettings.QtCols,
                qtBtnW = loadedSettings.QtBtnW,
                qtVisible = loadedSettings.QtVisible,
                hkCols = loadedSettings.HkCols,
                hkBtnSize = loadedSettings.HkBtnSize,
                hkVisible = loadedSettings.HkVisible,
                hkBindings = loadedSettings.HkBindings
            });
        }
        DService.Instance().Log.Information($"[ACR] uiSettings 消息已发送 + 已缓存 (qtVisible={loadedSettings.QtVisible?.Count ?? 0} hkVisible={loadedSettings.HkVisible?.Count ?? 0})");

        // 推送完整状态（qt + hotkey 数据）
        var hotkeyList = ACR.HotkeyHelper.GetAll();
        var qtList = ACR.QTHelper.GetAll();
        if (Plugin.IsWebUI && Plugin.Instance._uiBridge != null)
        {
            _ = Plugin.Instance._uiBridge.SendAsync(new
            {
                type = "status",
                data = new
                {
                    job = CurrentAcrName,
                    enabled = RuntimeCore.IsRunning,
                    paused = ACR.MainControlHelper.IsPaused,
                    hotkeys = hotkeyList.Select(r => new
                    {
                        id = r.Id,
                        label = r.Label,
                        iconId = r.IconId,
                        iconUrl = HiAuRo.UI.IconServer.GetIconUrl(r.IconId),
                        available = r.Check() >= 0,
                        binding = ACR.HotkeyHelper.GetBinding(r.Id)
                    }).ToList(),
                    qts = qtList.Select(q => new
                    {
                        id = q.Id,
                        label = q.Label,
                        value = q.Value,
                        tooltip = q.Tooltip,
                        color = q.Color,
                        binding = q.HotkeyBinding
                    }).ToList()
                }
            });
        }
        else
        {
            ImGuiOverlayState.UpdateStatus(CurrentAcrName, RuntimeCore.IsRunning,
                ACR.MainControlHelper.IsPaused, hotkeyList, qtList);
        }
        DService.Instance().Log.Information($"[ACR] status 消息已发送 (hotkeys={hotkeyList.Count} qts={qtList.Count})");

        // 恢复上次持久化的 overlay 尺寸（由外部插件处理）
        // 注册 ACR 自定义 ImGui 窗口
        var customWindows = entry.CustomWindows;
        if (customWindows != null)
        {
            var uiMgr = Plugin.Instance._uiManager;
            if (uiMgr != null)
            {
                foreach (var cw in customWindows)
                    uiMgr.AddCustomWindow(cw);
                DService.Instance().Log.Information($"[ACR] 自定义窗口已加载: {customWindows.Count()}个");
            }
        }
        // 增量合并：QT / HK — 只补新增，不覆盖用户已保存的值
        var needSave = false;

        // 合并 QT 值和可见性（用注册时的 DefaultValue）
        var qtAll = ACR.QTHelper.GetAll();
        foreach (var qt in qtAll)
        {
            if (!loadedSettings.QtValues.ContainsKey(qt.Id))
            {
                loadedSettings.QtValues[qt.Id] = qt.DefaultValue;
                needSave = true;
            }
            if (!loadedSettings.QtVisible.ContainsKey(qt.Id))
            {
                loadedSettings.QtVisible[qt.Id] = true;
                needSave = true;
            }
        }

        // 合并 HK 可见性和绑定（用注册时的 DefaultKey）
        var hkAll = ACR.HotkeyHelper.GetAll();
        foreach (var hk in hkAll)
        {
            if (!loadedSettings.HkVisible.ContainsKey(hk.Id))
            {
                loadedSettings.HkVisible[hk.Id] = true;
                needSave = true;
            }
            if (!loadedSettings.HkBindings.ContainsKey(hk.Id))
            {
                loadedSettings.HkBindings[hk.Id] = hk.DefaultKey;
                needSave = true;
            }
        }

        if (needSave)
        {
            loadedSettings.Save();
            DService.Instance().Log.Information($"[ACR] 新增项已合并保存 (qtValues={loadedSettings.QtValues.Count} hkVisible={loadedSettings.HkVisible.Count})");
        }

        // 宿主订阅保存事件 —— 用户点击保存按钮时自动写回所有 settings
        ACR.MainControlHelper.OnSave += HostSaveAllSettings;

        IsLoadingRotation = false;
    }

    private static void UnloadRotation()
    {
        if (Plugin.Instance != null)
            Plugin.Instance._uiManager?.RemoveCustomWindows();
        DService.Instance().Log.Information($"[ACR] UnloadRotation: {CurrentAcrName}");
        ACR.QTHelper.OnChanged -= OnQtChanged;
        ACR.HotkeyHelper.OnExecuted -= OnHkExecuted;
        _defaultSettings = null;

        Runner.Unload();
        CurrentEntry = null;
        CurrentJobId = 0;
        ACR.HotkeyHelper.Clear();
        ACR.QTHelper.Clear();
        ACR.MainControlHelper.OnSave -= HostSaveAllSettings;
        lock (_settingsLock)
        {
            _settingsProviders.Clear();
        }
        ACR.MainControlHelper.Reset();
    }

    private static DateTime _lastQtSave = DateTime.MinValue;
    private const int QtSaveDebounceMs = 1000; // 最多每秒存一次

    private static void OnQtChanged(string id, bool value)
    {
        var author = CurrentAuthor;
        var jobId = CurrentJobId;
        if (string.IsNullOrEmpty(author) || jobId == 0) return;

        if ((DateTime.UtcNow - _lastQtSave).TotalMilliseconds < QtSaveDebounceMs)
            return;
        _lastQtSave = DateTime.UtcNow;

        Task.Run(() =>
        {
            try
            {
                var settings = GetCurrentSettings();
                if (settings != null)
                {
                    var qtAll = ACR.QTHelper.GetAll();
                    settings.QtValues = qtAll.ToDictionary(q => q.Id, q => q.Value);
                    settings.Save();
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[ACR] QtSettings 保存失败: {ex.Message}");
            }
        });
    }

    private static void OnHkExecuted(string id, string label) { } // 占位，绑定/可见性由前端 saveUiSettings 维护

    /// <summary>获取当前加载的 AcrSettings（供 ImGui 面板/Web UI 读取）</summary>
    public static AcrSettings? GetCurrentSettings()
    {
        if (CurrentEntry == null) return null;
        var providerInterface = CurrentEntry.GetType()
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISettingsProvider<>));
        if (providerInterface != null)
            return providerInterface.GetProperty("Settings")?.GetValue(CurrentEntry) as AcrSettings;
        return _defaultSettings;
    }

    /// <summary>宿主 save handler —— 保存当前 AcrSettings（包括 ISettingsProvider 和默认实例）</summary>
    private static void HostSaveAllSettings()
    {
        var settings = GetCurrentSettings();
        if (settings != null)
            settings.Save();
    }

    private static string GetProviderKey(string author, uint jobId) => $"{author}_{jobId}";
}
