using System.Reflection;
using System.Runtime.Loader;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// 动态 ACR 加载器 —— 扫描 ACR/ 目录, 独立 ALC 加载 dll, 发现 IRotationEntry
/// </summary>
public static class ACRLoader
{
    private static readonly Dictionary<string, AssemblyLoadContext> _authorContexts = [];
    private static readonly List<Assembly> _loadedAcrAssemblies = [];
    private static readonly HashSet<string> _hostPrefixes = ["HiAuRo", "OmenTools", "Dalamud", "FFXIVClientStructs", "Lumina", "ImGuiNET", "TerraFX", "System.", "Microsoft."];

    public static IReadOnlyList<Assembly> LoadedAcrAssemblies => _loadedAcrAssemblies.AsReadOnly();

    /// <summary>扫描并加载所有外部 ACR，注册到 ACRLifecycle</summary>
    public static void LoadAll(string pluginDir)
    {
        var acrDir = Path.Combine(pluginDir, "ACR");
        if (!Directory.Exists(acrDir)) return;

        foreach (var authorDir in Directory.GetDirectories(acrDir))
        {
            var authorName = Path.GetFileName(authorDir);
            if (authorName == null) continue;

            var alc = new AssemblyLoadContext($"ACR_{authorName}", isCollectible: true);
            alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name, authorDir);

            var found = false;
            foreach (var dllPath in Directory.GetFiles(authorDir, "*.dll"))
            {
                try
                {
                    var dllBytes = File.ReadAllBytes(dllPath);
                    using var ms = new MemoryStream(dllBytes, writable: false);
                    var asm = alc.LoadFromStream(ms);
                    _loadedAcrAssemblies.Add(asm);

                    // 立即预解析所有引用程序集，避免 JIT 惰性解析在战斗中触发
                    PreResolveReferences(alc, asm, authorDir);
                    foreach (var type in asm.GetExportedTypes())
                    {
                        if (type is { IsAbstract: false, IsInterface: false } &&
                            typeof(IRotationEntry).IsAssignableFrom(type))
                        {
                            if (Activator.CreateInstance(type) is IRotationEntry entry)
                            {
                                var settingDir = Setting.SettingMgr.GetAcrDir(authorName);
                                foreach (var job in entry.TargetJobs)
                                {
                                    ACRLifecycle.RegisterExternal((uint)job, () =>
                                    {
                                        // 每次工厂调用时创建新实例（支持 ALC 内多职业）
                                        return (IRotationEntry)Activator.CreateInstance(type)!;
                                    }, settingDir);
                                }

                                DService.Instance().Log.Information($"[ACRLoader] {authorName}/{Path.GetFileName(dllPath)} → {type.Name} [{string.Join(",", entry.TargetJobs)}]");
                                found = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Error($"[ACRLoader] 加载失败 {dllPath}: {ex.Message}");
                }
            }

            if (found)
            {
                _authorContexts[authorName] = alc;
                ACRLifecycle.RegisterContext(alc);
            }
            else
            {
                alc.Unload();
            }
        }
    }

    /// <summary>程序集解析</summary>
    private static Assembly? ResolveAssembly(AssemblyLoadContext ctx, AssemblyName name, string authorDir)
    {
        DService.Instance().Log.Debug($"[ACRLoader] 解析程序集: {name.Name} v{name.Version} (请求者:{ctx.Name})");

        if (_hostPrefixes.Any(p => name.Name?.StartsWith(p) == true))
        {
            // HiAuRo 自身一定已加载（我们正在执行的代码就是它）
            if (name.Name == "HiAuRo")
            {
                var selfAsm = typeof(ACRLoader).Assembly;
                DService.Instance().Log.Debug($"[ACRLoader]   → 返回自身 HiAuRo 程序集: {selfAsm.GetName().Name} v{selfAsm.GetName().Version}");
                return selfAsm;
            }

            // HiAuRo.Helper: 优先用 HelperUpdater 已初始化 _ctx 的程序集，避免 ALC 隔离导致 _ctx=null
            if (name.Name == "HiAuRo.Helper" && HelperUpdater.HelperAssembly != null)
            {
                DService.Instance().Log.Debug($"[ACRLoader]   → 共享 HelperUpdater 的 HiAuRo.Helper 程序集");
                return HelperUpdater.HelperAssembly;
            }

            // 其它宿主程序集（OmenTools, Dalamud 等）：
            // 1. 先在 Default ALC 中查找（如 System.*, Microsoft.*）
            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (asm.GetName().Name == name.Name)
                {
                    DService.Instance().Log.Debug($"[ACRLoader]   → 在 Default ALC 找到: {asm.GetName().Name} v{asm.GetName().Version}");
                    return asm;
                }
            }

            // 2. 再在 HiAuRo 自身 ALC 中查找（OmenTools 等宿主程序集在这里）
            var hostAlc = AssemblyLoadContext.GetLoadContext(typeof(ACRLoader).Assembly);
            if (hostAlc != null)
            {
                foreach (var asm in hostAlc.Assemblies)
                {
                    if (asm.GetName().Name == name.Name)
                    {
                        DService.Instance().Log.Debug($"[ACRLoader]   → 在宿主 ALC 找到: {asm.GetName().Name} v{asm.GetName().Version}");
                        return asm;
                    }
                }

                // 2.5 宿主 ALC 遍历不到时（如 Dalamud 惰性加载），让宿主 ALC 自身尝试解析
                try
                {
                    var fromHost = hostAlc.LoadFromAssemblyName(name);
                    if (fromHost != null)
                    {
                        DService.Instance().Log.Debug($"[ACRLoader]   → 宿主 ALC 解析: {fromHost.GetName().Name} v{fromHost.GetName().Version}");
                        return fromHost;
                    }
                }
                catch { }
            }

            // 3. Fallback: 让 Default ALC 自行解析
            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[ACRLoader] LoadFromAssemblyName 失败: {name.Name} — {ex.Message}");
            }
        }

        var path = Path.Combine(authorDir, name.Name + ".dll");
        if (File.Exists(path))
        {
            var depBytes = File.ReadAllBytes(path);
            using var depMs = new MemoryStream(depBytes, writable: false);
            return ctx.LoadFromStream(depMs);
        }

        return null;
    }

    /// <summary>立即解析所有引用程序集，避免战斗中 JIT 惰性解析触发 I/O</summary>
    private static void PreResolveReferences(AssemblyLoadContext alc, Assembly asm, string authorDir)
    {
        foreach (var refName in asm.GetReferencedAssemblies())
        {
            try
            {
                // 确保引用程序集已加载到该 ALC 中
                alc.LoadFromAssemblyName(refName);
            }
            catch
            {
                // 如果 ALC 自身解析不了，尝试 ResolveAssembly 回退（如 ACR 本地依赖）
                try
                {
                    ResolveAssembly(alc, refName, authorDir);
                }
                catch { }
            }
        }
    }

    /// <summary>卸载所有外部 ACR</summary>
    public static void UnloadAll()
    {
        foreach (var (name, alc) in _authorContexts)
        {
            try { alc.Unload(); }
            catch (Exception ex) { DService.Instance().Log.Error($"[ACRLoader] 卸载 ALC 失败: {name}: {ex.Message}"); }
        }
        _authorContexts.Clear();
    }
}
