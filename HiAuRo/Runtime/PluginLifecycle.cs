namespace HiAuRo.Runtime;

/// <summary>
/// 插件生命周期管理 —— 挂载到 HiAuRo 启动/Tick/关闭流程
/// </summary>
public static class PluginLifecycle
{
    /// <summary>初始化：扫描并加载所有插件</summary>
    public static void Init(string pluginDir, string configDir)
    {
        DService.Instance().Log.Information("[PluginLifecycle] 开始加载插件...");
        PluginLoader.LoadAll(pluginDir, configDir);
        PluginLoader.InitializeAll();
    }

    /// <summary>每帧更新所有已加载插件</summary>
    public static void Update()
    {
        foreach (var (name, record) in PluginLoader.Plugins)
        {
            try { record.Plugin.Update(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLifecycle] {name} Update 失败: {ex.Message}");
            }
        }
    }

    /// <summary>关闭：卸载所有插件</summary>
    public static void Shutdown()
    {
        DService.Instance().Log.Information("[PluginLifecycle] 关闭所有插件...");
        PluginLoader.UnloadAll();
    }
}
