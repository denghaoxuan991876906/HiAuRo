using System.Timers;

namespace HiAuRo.Runtime;

/// <summary>
/// 统一文件监控入口：监控 ACR/ 和 Plugins/ 目录的 DLL 变更，自动触发对应重载
/// </summary>
internal static class DynModuleWatcher
{
    private static FileSystemWatcher? _acrWatcher;
    private static FileSystemWatcher? _pluginWatcher;
    private static System.Timers.Timer? _debounceTimer;
    private const int DebounceMs = 500;

    public static void Start(string configDir)
    {
        _debounceTimer = new System.Timers.Timer(DebounceMs) { AutoReset = false };
        _debounceTimer.Elapsed += OnDebounceElapsed;

        var acrDir = Path.Combine(configDir, "ACR");
        if (Directory.Exists(acrDir))
        {
            _acrWatcher = CreateWatcher(acrDir, "*.dll", true);
            _acrWatcher.EnableRaisingEvents = true;
            DService.Instance().Log.Information($"[DynModuleWatcher] 监控 ACR: {acrDir}");
        }

        var pluginsDir = Path.Combine(configDir, "Plugins");
        if (Directory.Exists(pluginsDir))
        {
            _pluginWatcher = CreateWatcher(pluginsDir, "*.dll", false);
            _pluginWatcher.EnableRaisingEvents = true;
            DService.Instance().Log.Information($"[DynModuleWatcher] 监控 Plugins: {pluginsDir}");
        }
    }

    public static void Stop()
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        if (_acrWatcher != null)
        {
            _acrWatcher.EnableRaisingEvents = false;
            _acrWatcher.Dispose();
            _acrWatcher = null;
        }
        if (_pluginWatcher != null)
        {
            _pluginWatcher.EnableRaisingEvents = false;
            _pluginWatcher.Dispose();
            _pluginWatcher = null;
        }
        DService.Instance().Log.Information("[DynModuleWatcher] 已停止");
    }

    private static FileSystemWatcher CreateWatcher(string path, string filter, bool includeSubdirs)
    {
        var w = new FileSystemWatcher(path, filter)
        {
            IncludeSubdirectories = includeSubdirs,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        w.Created += OnFileChanged;
        w.Changed += OnFileChanged;
        w.Deleted += OnFileChanged;
        w.Renamed += OnFileChanged;
        return w;
    }

    private static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        DService.Instance().Log.Information($"[DynModuleWatcher] 检测到变更: {e.ChangeType} {e.Name}");
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private static void OnDebounceElapsed(object? sender, ElapsedEventArgs e)
    {
        DService.Instance().Log.Information("[DynModuleWatcher] 防抖结束，请求重载");
        ACRLifecycle.RequestReload();
        PluginLifecycle.RequestReload();
    }
}
