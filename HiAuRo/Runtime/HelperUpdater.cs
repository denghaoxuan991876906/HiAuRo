using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;

namespace HiAuRo.Runtime;

/// <summary>
/// Helper DLL 自动更新 — 从 GitHub Release 拉取最新 HiAuRo.Helper.dll
/// </summary>
public static class HelperUpdater
{
    private const string RepoOwner = "denghaoxuan991876906";
    private const string RepoName = "HiAuRo.Helper";
    private const string DllName = "HiAuRo.Helper.dll";
    private const string LocalDevMarkerName = "HiAuRo.Helper.localdev";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "HiAuRo" } }
    };

    private static string StoreDir => Path.Combine(
        DService.Instance().PI.ConfigDirectory.FullName, "Helper");

    private static string LocalDevMarkerPath => Path.Combine(StoreDir, LocalDevMarkerName);

    private static AssemblyLoadContext? _alc;
    private static Assembly? _helperAsm;
    private static byte[]? _cachedDllBytes;

    /// <summary>Helper DLL 是否已加载</summary>
    public static bool Loaded { get; private set; }

    /// <summary>HelperUpdater 已加载的 HiAuRo.Helper 程序集（供 ACRLoader 共享，避免 ALC 隔离）</summary>
    public static Assembly? HelperAssembly => _helperAsm;

    /// <summary>尝试从本地缓存同步加载 Helper DLL（供 ACRLoader 时序竞争回退）</summary>
    public static bool TryLoadLocalSync()
    {
        if (_helperAsm != null) return true;

        var localDll = Path.Combine(StoreDir, DllName);
        if (!File.Exists(localDll)) return false;

        try
        {
            LoadDll(localDll);
            DService.Instance().Log.Information($"[HelperUpdater] 从本地缓存同步加载: {localDll}");
            return true;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[HelperUpdater] 本地缓存加载失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>检查更新，下载并加载最新 DLL（同步，避免与 ACRLoader 时序竞争）</summary>
    public static async Task CheckAndUpdateAsync()
    {
        var localDll = Path.Combine(StoreDir, DllName);
        if (ShouldUseLocalDevHelper(localDll))
        {
            DService.Instance().Log.Information("[HelperUpdater] 检测到本地开发版 Helper，跳过在线更新");
            LoadDll(localDll);
            return;
        }

        try
        {
            // 直接下载 latest release DLL，无需调用 GitHub API（避免 403 rate limit）
            var downloaded = await DownloadLatestDll(localDll);
            if (downloaded)
            {
                DService.Instance().Log.Information($"[HelperUpdater] 已更新 {RepoName}");
                LoadDll(localDll);
                return;
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[HelperUpdater] 下载失败: {ex.Message}");
        }

        // 下载失败 → 尝试本地缓存
        if (File.Exists(localDll))
        {
            DService.Instance().Log.Information("[HelperUpdater] 从本地缓存加载");
            LoadDll(localDll);
        }
        else
        {
            DService.Instance().Log.Warning("[HelperUpdater] 无本地缓存，跳过 Helper 加载");
        }
    }

    /// <summary>判断当前缓存是否为本地构建的 Helper，避免开发调试时被在线版本覆盖。</summary>
    private static bool ShouldUseLocalDevHelper(string localDll)
    {
        if (!File.Exists(localDll) || !File.Exists(LocalDevMarkerPath))
            return false;

        var hostPath = typeof(HelperUpdater).Assembly.Location;
        if (string.IsNullOrWhiteSpace(hostPath) || !File.Exists(hostPath))
            return true;

        var helperTime = File.GetLastWriteTimeUtc(localDll);
        var hostTime = File.GetLastWriteTimeUtc(hostPath);
        return helperTime >= hostTime;
    }

    /// <summary>下载 latest release DLL（直接链接，不走 API，不受 rate limit 限制）。</summary>
    private static async Task<bool> DownloadLatestDll(string destPath)
    {
        // GitHub 支持 /releases/latest/download/ 直接重定向到最新 release 的 asset
        // 格式: https://github.com/{owner}/{repo}/releases/latest/download/{filename}
        var url = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest/download/{DllName}";
        using var req = new HttpRequestMessage(HttpMethod.Head, url); // HEAD 先探测
        using var headResp = await _http.SendAsync(req);
        if (!headResp.IsSuccessStatusCode) return false;

        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return false;

        Directory.CreateDirectory(StoreDir);
        await using var fs = File.Create(destPath);
        await resp.Content.CopyToAsync(fs);
        return true;
    }

        /// <summary>解析 Helper ALC 中宿主程序集依赖（Dalamud、OmenTools、HiAuRo 等）</summary>
        private static Assembly? ResolveHelperDependency(AssemblyLoadContext ctx, AssemblyName name)
        {
            var hostPrefixes = new[] { "HiAuRo", "OmenTools", "Dalamud", "FFXIVClientStructs",
                "Lumina", "ImGuiNET", "TerraFX", "System.", "Microsoft." };

            if (!hostPrefixes.Any(p => name.Name?.StartsWith(p) == true))
                return null;

            // 先在 Default ALC 中查找（System.*、Microsoft.*）
            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (asm.GetName().Name == name.Name)
                    return asm;
            }

            // 再在宿主 ALC（HiAuRo 所在）中查找
            var hostAlc = AssemblyLoadContext.GetLoadContext(typeof(HelperUpdater).Assembly);
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

            return null;
        }

        private static void LoadDll(string dllPath)
    {
        if (!File.Exists(dllPath)) return;

        // 读入内存后立即释放文件句柄，避免下次更新时 DLL 被占用
        _cachedDllBytes = File.ReadAllBytes(dllPath);

        _alc?.Unload();
        _alc = new AssemblyLoadContext("HiAuRo.Helper", isCollectible: true);
        _alc.Resolving += ResolveHelperDependency;

        using var ms = new MemoryStream(_cachedDllBytes, writable: false);
        _helperAsm = _alc.LoadFromStream(ms);
        Loaded = true;
    }
}
