using System.Reflection;
using System.Runtime.Loader;

namespace HiAuRo.Runtime;

/// <summary>
/// 通用插件加载器 —— 扫描 Plugins/*.dll, ALC 加载, 发现 IPlugin 实现
/// </summary>
public static class PluginLoader
{
    private static readonly Dictionary<string, PluginRecord> _plugins = [];

    /// <summary>已加载的插件记录（名称 → 记录）</summary>
    public static IReadOnlyDictionary<string, PluginRecord> Plugins => _plugins;

    /// <summary>已加载插件的程序集列表（供 ScriptCompiler 注入引用）</summary>
    public static IReadOnlyList<Assembly> PluginAssemblies =>
        _plugins.Values.Select(r => r.Assembly).ToList().AsReadOnly();

    /// <summary>扫描 Plugins/ 目录并加载所有插件</summary>
    public static void LoadAll(string pluginDir, string configDir)
    {
        HelperUpdater.TryLoadLocalSync();

        var pluginsPath = Path.Combine(configDir, "Plugins");
        if (!Directory.Exists(pluginsPath))
        {
            Directory.CreateDirectory(pluginsPath);
            DService.Instance().Log.Information($"[PluginLoader] Plugins 目录已创建: {pluginsPath}");
            return;
        }

        foreach (var dllPath in Directory.GetFiles(pluginsPath, "*.dll"))
        {
            AssemblyLoadContext? candidateContext = null;
            var accepted = false;
            try
            {
                var bindings = SharedAssemblyResolver.CaptureHost();
                if (SharedAssemblyResolver.IsHostAssemblyFile(dllPath, bindings))
                {
                    DService.Instance().Log.Warning(
                        $"[PluginLoader] 跳过宿主程序集副本: {Path.GetFileName(dllPath)}");
                    continue;
                }

                var dllName = Path.GetFileNameWithoutExtension(dllPath);
                if (_plugins.ContainsKey(dllName)) continue;

                var dllBytes = File.ReadAllBytes(dllPath);
                candidateContext = new AssemblyLoadContext($"Plugin_{dllName}", isCollectible: true);
                candidateContext.Resolving += (ctx, name) =>
                    ResolveAssembly(ctx, name, pluginsPath, bindings);

                using var ms = new MemoryStream(dllBytes, writable: false);
                var asm = candidateContext.LoadFromStream(ms);

                // 立即预解析所有引用程序集，避免 JIT 惰性解析在战斗中触发
                PreResolveReferences(candidateContext, asm, pluginsPath, bindings);

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
                                Context = candidateContext,
                                DllBytes = dllBytes,
                                DllPath = dllPath
                            };
                            found = true;
                            accepted = true;
                            DService.Instance().Log.Information(
                                $"[PluginLoader] {dllName} → {type.Name} v{plugin.Version}");
                            break;
                        }
                    }
                }

                if (!found)
                {
                    DService.Instance().Log.Warning(
                        $"[PluginLoader] {dllName} 中未找到 IPlugin 实现，卸载 ALC");
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error(
                    $"[PluginLoader] 加载失败 {Path.GetFileName(dllPath)}: {ex.Message}");
            }
            finally
            {
                if (!accepted)
                    candidateContext?.Unload();
            }
        }

        DService.Instance().Log.Information(
            $"[PluginLoader] 扫描完成，已加载 {_plugins.Count} 个插件");
    }

    /// <summary>程序集解析 —— 同 ACRLoader 模式</summary>
    private static Assembly? ResolveAssembly(
        AssemblyLoadContext ctx,
        AssemblyName name,
        string pluginsDir,
        SharedAssemblyResolver.Snapshot bindings)
    {
        if (bindings.IsHost(name.Name))
            return bindings.Resolve(name);

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
        foreach (var name in _plugins.Keys.ToArray())
        {
            if (!_plugins.TryGetValue(name, out var record))
                continue;

            try { record.Plugin.Initialize(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLoader] {name} Initialize 失败: {ex.Message}");
                _plugins.Remove(name);
                try { record.Plugin.Dispose(); }
                catch { }
                try { record.Context.Unload(); }
                catch { }
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

    /// <summary>立即解析所有引用程序集，避免战斗中 JIT 惰性解析触发 I/O</summary>
    private static void PreResolveReferences(
        AssemblyLoadContext alc,
        Assembly asm,
        string pluginsDir,
        SharedAssemblyResolver.Snapshot bindings)
    {
        SharedAssemblyResolver.PreloadDependencies(
            alc,
            asm,
            bindings,
            name => ResolveAssembly(alc, name, pluginsDir, bindings),
            "插件");
    }

    /// <summary>插件记录</summary>
    public sealed class PluginRecord
    {
        public IPlugin Plugin { get; init; } = null!;
        public Assembly Assembly { get; init; } = null!;
        public AssemblyLoadContext Context { get; init; } = null!;
        public byte[] DllBytes { get; init; } = null!;
        public string DllPath { get; init; } = "";
    }
}
