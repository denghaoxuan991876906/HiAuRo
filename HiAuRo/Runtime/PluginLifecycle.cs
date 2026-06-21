namespace HiAuRo.Runtime;

/// <summary>
/// 插件生命周期管理 —— 挂载到 HiAuRo 启动/Tick/关闭流程
/// </summary>
public static class PluginLifecycle
{
    private static string _pluginDir = string.Empty;
    private static string _configDir = string.Empty;
    private static volatile bool _pendingReload;

    /// <summary>标记需要重载（由文件监控触发）</summary>
    internal static void RequestReload()
    {
        _pendingReload = true;
    }

    /// <summary>初始化：扫描并加载所有插件</summary>
    public static void Init(string pluginDir, string configDir)
    {
        _pluginDir = pluginDir;
        _configDir = configDir;

        DService.Instance().Log.Information("[PluginLifecycle] 开始加载插件...");
        PluginLoader.LoadAll(pluginDir, configDir);
        PluginLoader.InitializeAll();

        // 注入插件程序集到 ScriptCompiler
        try
        {
            Execution.ScriptCompiler.RegisterPluginReferences();
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[PluginLifecycle] ScriptCompiler 注入失败: {ex.Message}");
        }
    }

    /// <summary>每帧更新所有已加载插件</summary>
    public static void Update()
    {
        CheckPendingReload();

        foreach (var (name, record) in PluginLoader.Plugins)
        {
            try { record.Plugin.Update(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLifecycle] {name} Update 失败: {ex.Message}");
            }
        }
    }

    /// <summary>非战斗时自动执行待定重载</summary>
    private static void CheckPendingReload()
    {
        if (!_pendingReload) return;
        if (CombatContext.CurrentState == CombatContext.State.InCombat) return;
        if (Data.Combat.IsInInstanceArea) return;
        if (Data.Combat.IsBetweenAreas) return;

        _pendingReload = false;
        DService.Instance().Log.Information("[PluginLifecycle] DynModuleWatcher 触发自动重载");
        Reload();
    }

    /// <summary>热重载所有插件</summary>
    public static void Reload()
    {
        PluginLoader.UnloadAll();
        PluginLoader.LoadAll(_pluginDir, _configDir);
        PluginLoader.InitializeAll();

        try
        {
            Execution.ScriptCompiler.RegisterPluginReferences();
            Execution.TriggerCatalogExporter.RebuildRuntimeTypeRegistry();
            Execution.TriggerCatalogExporter.ExportToConfigDirectory(_configDir);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[PluginLifecycle] ScriptCompiler 注入失败: {ex.Message}");
        }
    }

    /// <summary>关闭：卸载所有插件</summary>
    public static void Shutdown()
    {
        DService.Instance().Log.Information("[PluginLifecycle] 关闭所有插件...");
        PluginLoader.UnloadAll();
    }
}
