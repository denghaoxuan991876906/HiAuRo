using System.Reflection;
using System.Runtime.Loader;

namespace HiAuRo.Runtime;

/// <summary>
/// 通用插件加载器 —— 扫描 Plugins/*.dll, ALC 加载, 发现 IPlugin 实现
/// </summary>
public static class PluginLoader
{
    private static readonly Dictionary<string, PluginRecord> _plugins = [];
    private static readonly HashSet<string> _hostPrefixes =
        ["HiAuRo", "OmenTools", "Dalamud", "FFXIVClientStructs", "Lumina", "ImGuiNET", "TerraFX", "System.", "Microsoft."];

    /// <summary>已加载的插件记录（名称 → 记录）</summary>
    public static IReadOnlyDictionary<string, PluginRecord> Plugins => _plugins;

    /// <summary>已加载插件的程序集列表（供 ScriptCompiler 注入引用）</summary>
    public static IReadOnlyList<Assembly> PluginAssemblies =>
        _plugins.Values.Select(r => r.Assembly).ToList().AsReadOnly();

    /// <summary>扫描 Plugins/ 目录并加载所有插件</summary>
    public static void LoadAll(string pluginDir, string configDir)
    {
        var pluginsPath = Path.Combine(configDir, "Plugins");
        if (!Directory.Exists(pluginsPath))
        {
            Directory.CreateDirectory(pluginsPath);
            DService.Instance().Log.Information($"[PluginLoader] Plugins 目录已创建: {pluginsPath}");
            return;
        }

        foreach (var dllPath in Directory.GetFiles(pluginsPath, "*.dll"))
        {
            try
            {
                var dllName = Path.GetFileNameWithoutExtension(dllPath);
                if (_plugins.ContainsKey(dllName)) continue;

                var dllBytes = File.ReadAllBytes(dllPath);
                var alc = new AssemblyLoadContext($"Plugin_{dllName}", isCollectible: true);
                alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name, pluginsPath);

                using var ms = new MemoryStream(dllBytes, writable: false);
                var asm = alc.LoadFromStream(ms);

                var found = false;
                foreach (var type in asm.GetExportedTypes())
                {
                    if (type is { IsAbstract: false, IsInterface: false } &&
                        typeof(IPlugin).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                        {
                            _plugins[dllName] = new PluginRecord
                            {
                                Plugin = plugin,
                                Assembly = asm,
                                Context = alc,
                                DllBytes = dllBytes
                            };
                            found = true;
                            DService.Instance().Log.Information(
                                $"[PluginLoader] {dllName} → {type.Name} v{plugin.Version}");
                        }
                    }
                }

                if (!found)
                {
                    DService.Instance().Log.Warning(
                        $"[PluginLoader] {dllName} 中未找到 IPlugin 实现，卸载 ALC");
                    alc.Unload();
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error(
                    $"[PluginLoader] 加载失败 {Path.GetFileName(dllPath)}: {ex.Message}");
            }
        }

        DService.Instance().Log.Information(
            $"[PluginLoader] 扫描完成，已加载 {_plugins.Count} 个插件");
    }

    /// <summary>程序集解析 —— 同 ACRLoader 模式</summary>
    private static Assembly? ResolveAssembly(AssemblyLoadContext ctx, AssemblyName name, string pluginsDir)
    {
        if (_hostPrefixes.Any(p => name.Name?.StartsWith(p) == true))
        {
            if (name.Name == "HiAuRo")
                return typeof(PluginLoader).Assembly;

            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (asm.GetName().Name == name.Name)
                    return asm;
            }

            var hostAlc = AssemblyLoadContext.GetLoadContext(typeof(PluginLoader).Assembly);
            if (hostAlc != null)
            {
                foreach (var asm in hostAlc.Assemblies)
                {
                    if (asm.GetName().Name == name.Name)
                        return asm;
                }

                try
                {
                    return hostAlc.LoadFromAssemblyName(name);
                }
                catch { }
            }

            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
            }
            catch { }
        }

        var path = Path.Combine(pluginsDir, name.Name + ".dll");
        if (File.Exists(path))
        {
            var depBytes = File.ReadAllBytes(path);
            using var depMs = new MemoryStream(depBytes, writable: false);
            return ctx.LoadFromStream(depMs);
        }

        return null;
    }

    /// <summary>初始化所有已加载的插件</summary>
    public static void InitializeAll()
    {
        foreach (var (name, record) in _plugins)
        {
            try { record.Plugin.Initialize(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLoader] {name} Initialize 失败: {ex.Message}");
            }
        }
    }

    /// <summary>卸载所有插件和 ALC</summary>
    public static void UnloadAll()
    {
        foreach (var (name, record) in _plugins)
        {
            try { record.Plugin.Dispose(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLoader] {name} Dispose 失败: {ex.Message}");
            }
            try { record.Context.Unload(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLoader] {name} ALC Unload 失败: {ex.Message}");
            }
        }
        _plugins.Clear();
    }

    /// <summary>插件记录</summary>
    public sealed class PluginRecord
    {
        public IPlugin Plugin { get; init; } = null!;
        public Assembly Assembly { get; init; } = null!;
        public AssemblyLoadContext Context { get; init; } = null!;
        public byte[] DllBytes { get; init; } = null!;
    }
}
