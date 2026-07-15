using System.Reflection;
using System.Runtime.Loader;
using HiAuRo.ACR;
using HiAuRo.Runtime.AcrDistribution;

namespace HiAuRo.Runtime;

/// <summary>
/// 动态 ACR 加载器 —— 扫描 ACR/ 目录, 独立 ALC 加载 dll, 发现 IRotationEntry
/// </summary>
public static class ACRLoader
{
    private static readonly Dictionary<string, AssemblyLoadContext> _authorContexts = [];
    private static readonly List<Assembly> _loadedAcrAssemblies = [];
    private static readonly HashSet<IRotationEntry> _loadedEntries =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>已加载的 ACR 程序集列表</summary>
    public static IReadOnlyList<Assembly> LoadedAcrAssemblies => _loadedAcrAssemblies.AsReadOnly();

    /// <summary>扫描并加载所有外部 ACR，注册到 ACRLifecycle</summary>
    public static void LoadAll(string configDir)
    {
        HelperUpdater.TryLoadLocalSync();

        var acrDir = Path.Combine(configDir, "ACR");
        if (!Directory.Exists(acrDir)) return;

        LoadInstalledAcrs(acrDir);
        LoadLegacyAuthorAcrs(acrDir);
    }

    private static void LoadInstalledAcrs(string acrDir)
    {
        var installationStore = new AcrInstallationStore(acrDir);
        foreach (var installed in installationStore.LoadInstalled())
        {
            var installKey = installed.InstallKey;
            var installDir = installed.InstallDir;
            if (string.IsNullOrWhiteSpace(installKey) || string.IsNullOrWhiteSpace(installDir))
                continue;

            var dllPath = Path.Combine(installDir, installed.EntryDll);
            if (!File.Exists(dllPath))
            {
                DService.Instance().Log.Warning($"[ACRLoader] 跳过 {installKey}: 主 DLL 不存在 {dllPath}");
                continue;
            }
            var bindings = SharedAssemblyResolver.CaptureHost();
            if (SharedAssemblyResolver.IsHostAssemblyFile(dllPath, bindings))
            {
                DService.Instance().Log.Error($"[ACRLoader] 跳过 {installKey}: 主 DLL 不能是宿主程序集");
                continue;
            }

            var alc = new AssemblyLoadContext($"ACR_{installKey}", isCollectible: true);
            alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name, installDir, bindings);

            var found = false;
            Assembly? loadedAssembly = null;
            var registrations = new List<(uint JobId, ACRLifecycle.AcrRegistryEntry Registry)>();
            var createdEntries = new List<IRotationEntry>();
            try
            {
                var dllBytes = File.ReadAllBytes(dllPath);
                using var ms = new MemoryStream(dllBytes, writable: false);
                var asm = alc.LoadFromStream(ms);
                loadedAssembly = asm;

                PreResolveReferences(alc, asm, installDir, bindings);
                foreach (var type in asm.GetExportedTypes())
                {
                    if (type is { IsAbstract: false, IsInterface: false } &&
                        typeof(IRotationEntry).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type) is IRotationEntry entry)
                        {
                            createdEntries.Add(entry);
                            var jobs = entry.TargetJobs.ToArray();
                            if (jobs.Length == 0)
                            {
                                entry.Dispose();
                                createdEntries.Remove(entry);
                                continue;
                            }
                            var registry = new ACRLifecycle.AcrRegistryEntry(
                                installKey,
                                !string.IsNullOrWhiteSpace(installed.DisplayName) ? installed.DisplayName : entry.AuthorName,
                                installed.PublisherId,
                                installed.AcrId,
                                installed.Version,
                                installDir,
                                installed.SettingDir,
                                entry,
                                false);
                            foreach (var job in jobs)
                            {
                                registrations.Add(((uint)job, registry));
                            }

                            DService.Instance().Log.Information($"[ACRLoader] {installKey}/{Path.GetFileName(dllPath)} → {type.Name} [{string.Join(",", jobs)}]");
                            found = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[ACRLoader] 加载失败 {dllPath}: {ex.Message}");
                foreach (var entry in createdEntries)
                {
                    try { entry.Dispose(); }
                    catch { }
                }
                registrations.Clear();
                found = false;
            }

            if (found)
            {
                foreach (var (jobId, registry) in registrations)
                    ACRLifecycle.RegisterExternal(jobId, registry);
                foreach (var entry in createdEntries)
                    _loadedEntries.Add(entry);
                _loadedAcrAssemblies.Add(loadedAssembly!);
                _authorContexts[installKey] = alc;
                ACRLifecycle.RegisterContext(alc);
            }
            else
            {
                alc.Unload();
            }
        }
    }

    private static void LoadLegacyAuthorAcrs(string acrDir)
    {
        foreach (var authorDir in Directory.GetDirectories(acrDir))
        {
            var authorName = Path.GetFileName(authorDir);
            if (string.IsNullOrWhiteSpace(authorName))
                continue;

            // 新安装模型目录：有同名 metadata json；这里跳过，避免重复加载
            var installedMetadataPath = Path.Combine(authorDir, authorName + ".json");
            if (File.Exists(installedMetadataPath))
                continue;

            var bindings = SharedAssemblyResolver.CaptureHost();
            var dllCandidates = Directory.GetFiles(authorDir, "*.dll")
                .Where(path => !path.EndsWith(".deps.dll", StringComparison.OrdinalIgnoreCase))
                .Where(path => !SharedAssemblyResolver.IsHostAssemblyFile(path, bindings))
                .ToArray();
            if (dllCandidates.Length == 0)
                continue;

            var alc = new AssemblyLoadContext($"ACR_{authorName}", isCollectible: true);
            alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name, authorDir, bindings);

            var found = false;
            foreach (var dllPath in dllCandidates)
            {
                var registrations = new List<(uint JobId, ACRLifecycle.AcrRegistryEntry Registry)>();
                var createdEntries = new List<IRotationEntry>();
                try
                {
                    var dllBytes = File.ReadAllBytes(dllPath);
                    using var ms = new MemoryStream(dllBytes, writable: false);
                    var asm = alc.LoadFromStream(ms);

                    PreResolveReferences(alc, asm, authorDir, bindings);
                    foreach (var type in asm.GetExportedTypes())
                    {
                        if (type is { IsAbstract: false, IsInterface: false } &&
                            typeof(IRotationEntry).IsAssignableFrom(type) &&
                            Activator.CreateInstance(type) is IRotationEntry entry)
                        {
                            createdEntries.Add(entry);
                            var installKey = authorName;
                            var settingDir = Setting.SettingMgr.GetAcrDir(authorName);
                            var jobs = entry.TargetJobs.ToArray();
                            if (jobs.Length == 0)
                            {
                                entry.Dispose();
                                createdEntries.Remove(entry);
                                continue;
                            }
                            var registry = new ACRLifecycle.AcrRegistryEntry(
                                installKey,
                                entry.AuthorName,
                                "",
                                "",
                                "",
                                authorDir,
                                settingDir,
                                entry,
                                true);

                            foreach (var job in jobs)
                                registrations.Add(((uint)job, registry));

                            DService.Instance().Log.Information($"[ACRLoader] legacy {authorName}/{Path.GetFileName(dllPath)} → {type.Name} [{string.Join(",", jobs)}]");
                        }
                    }

                    if (registrations.Count > 0)
                    {
                        foreach (var (jobId, registry) in registrations)
                            ACRLifecycle.RegisterExternal(jobId, registry);
                        foreach (var entry in createdEntries)
                            _loadedEntries.Add(entry);
                        _loadedAcrAssemblies.Add(asm);
                        found = true;
                    }
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Error($"[ACRLoader] legacy 加载失败 {dllPath}: {ex.Message}");
                    foreach (var entry in createdEntries)
                    {
                        try { entry.Dispose(); }
                        catch { }
                    }
                }
            }

            if (found)
            {
                _authorContexts[$"legacy:{authorName}"] = alc;
                ACRLifecycle.RegisterContext(alc);
            }
            else
            {
                alc.Unload();
            }
        }
    }

    /// <summary>程序集解析</summary>
    private static Assembly? ResolveAssembly(
        AssemblyLoadContext ctx,
        AssemblyName name,
        string authorDir,
        SharedAssemblyResolver.Snapshot bindings)
    {
        DService.Instance().Log.Debug($"[ACRLoader] 解析程序集: {name.Name} v{name.Version} (请求者:{ctx.Name})");

        if (bindings.IsHost(name.Name))
        {
            var shared = bindings.Resolve(name);
            if (shared is null)
                DService.Instance().Log.Error($"[ACRLoader] 宿主程序集未加载: {name.Name}");
            return shared;
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
    private static void PreResolveReferences(
        AssemblyLoadContext alc,
        Assembly asm,
        string authorDir,
        SharedAssemblyResolver.Snapshot bindings)
    {
        SharedAssemblyResolver.PreloadDependencies(
            alc,
            asm,
            bindings,
            name => ResolveAssembly(alc, name, authorDir, bindings),
            "ACR");
    }

    /// <summary>卸载所有外部 ACR</summary>
    public static void UnloadAll()
    {
        foreach (var entry in _loadedEntries.ToArray())
        {
            try { entry.Dispose(); }
            catch (Exception ex) { DService.Instance().Log.Error($"[ACRLoader] 释放入口失败: {ex.Message}"); }
        }
        _loadedEntries.Clear();

        foreach (var (name, alc) in _authorContexts)
        {
            try { alc.Unload(); }
            catch (Exception ex) { DService.Instance().Log.Error($"[ACRLoader] 卸载 ALC 失败: {name}: {ex.Message}"); }
        }
        _authorContexts.Clear();
        _loadedAcrAssemblies.Clear();
    }

    internal static bool TryDisposeEntry(IRotationEntry entry)
    {
        if (!_loadedEntries.Remove(entry))
            return false;

        entry.Dispose();
        return true;
    }
}
