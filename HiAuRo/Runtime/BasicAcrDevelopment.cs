using HiAuRo.ACR;

namespace HiAuRo.Runtime;

internal enum BasicAcrDevelopmentState
{
    Disabled,
    NotLoaded,
    Ready,
    Failed,
}

internal static class BasicAcrDevelopment
{
    private const string InstallKey = "__basic_acr_dev__";

    private static PluginConfig? config;
    private static string configDir = string.Empty;
    private static string hostDllDir = string.Empty;
    private static BasicAcrCompilation? current;

    internal static BasicAcrDevelopmentState State { get; private set; } = BasicAcrDevelopmentState.Disabled;
    internal static IReadOnlyList<BasicAcrDiagnostic> Diagnostics { get; private set; } = [];
    internal static string? LastError { get; private set; }
    internal static string? ScriptTypeName => current?.ScriptTypeName;
    internal static Jobs? TargetJob => current?.TargetJob;
    internal static DateTimeOffset? LoadedAt { get; private set; }

    internal static void Init(PluginConfig pluginConfig, string pluginConfigDir, string pluginHostDllDir)
    {
        config = pluginConfig;
        configDir = pluginConfigDir;
        hostDllDir = pluginHostDllDir;
        Diagnostics = [];
        LastError = null;
        LoadedAt = null;

        if (!pluginConfig.BasicAcrScriptEnabled)
        {
            State = BasicAcrDevelopmentState.Disabled;
            return;
        }

        State = BasicAcrDevelopmentState.NotLoaded;
        try
        {
            ACRLifecycle.SetDevelopmentOverride(true, null);
        }
        catch (Exception ex)
        {
            FailClosed($"基础 ACR 开发模式初始化失败: {ex.GetBaseException().Message}");
            return;
        }

        Reload(initialLoad: true);
    }

    internal static void SetEnabled(bool enabled)
    {
        var pluginConfig = config
            ?? throw new InvalidOperationException("基础 ACR 开发管理器尚未初始化");

        pluginConfig.BasicAcrScriptEnabled = enabled;
        Diagnostics = [];
        LastError = null;
        LoadedAt = null;

        ACRLifecycle.SetDevelopmentOverride(enabled, null);

        var old = current;
        current = null;
        DisposeCompilation(old, enabled ? "重置旧脚本" : "禁用开发模式");
        State = enabled
            ? BasicAcrDevelopmentState.NotLoaded
            : BasicAcrDevelopmentState.Disabled;
    }

    internal static bool Reload(bool initialLoad = false)
    {
        var pluginConfig = config;
        if (pluginConfig?.BasicAcrScriptEnabled != true)
            return false;

        if (!initialLoad && !IsReloadAllowed(
                CombatContext.CurrentState,
                DService.Instance().Condition.IsBetweenAreas,
                ACRLifecycle.IsLoadingRotation))
        {
            Hi.PrintError("基础 ACR 重载已阻止：请在脱战、非切图且 ACR 未加载时重试");
            return false;
        }

        Diagnostics = [];
        LastError = null;

        var scriptPath = pluginConfig.BasicAcrScriptPath;
        if (string.IsNullOrWhiteSpace(scriptPath) || !Path.IsPathFullyQualified(scriptPath))
            return FailClosed("基础 ACR 脚本路径必须是绝对路径");
        if (!string.Equals(Path.GetExtension(scriptPath), ".cs", StringComparison.OrdinalIgnoreCase))
            return FailClosed("基础 ACR 脚本必须是 .cs 文件");
        if (!File.Exists(scriptPath))
            return FailClosed($"基础 ACR 脚本不存在: {scriptPath}");

        string source;
        try
        {
            source = File.ReadAllText(scriptPath);
        }
        catch (IOException ex)
        {
            return FailClosed($"读取基础 ACR 脚本失败: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailClosed($"读取基础 ACR 脚本失败: {ex.Message}");
        }

        HelperUpdater.TryLoadLocalSync();
        using var result = BasicAcrCompiler.Compile(source, scriptPath, hostDllDir);
        Diagnostics = result.Diagnostics.ToArray();
        if (!result.Success)
            return FailClosed(result.ErrorMessage ?? "基础 ACR 编译失败");

        var candidate = result.TakeCompilation();
        if (candidate is null)
            return FailClosed("基础 ACR 编译结果为空");

        var registry = new ACRLifecycle.AcrRegistryEntry(
            InstallKey,
            $"Basic ACR: {Path.GetFileNameWithoutExtension(scriptPath)}",
            "",
            "",
            "dev",
            Path.GetDirectoryName(scriptPath)!,
            Path.Combine(configDir, "setting", "ACR", InstallKey),
            candidate.Entry);

        var old = current;
        try
        {
            ACRLifecycle.SetDevelopmentOverride(true, registry);
        }
        catch (Exception ex)
        {
            DisposeCompilation(candidate, "释放应用失败的候选脚本");
            return FailClosed($"应用基础 ACR 失败: {ex.GetBaseException().Message}");
        }

        current = candidate;
        DisposeCompilation(old, "卸载旧脚本");
        State = BasicAcrDevelopmentState.Ready;
        LastError = null;
        LoadedAt = DateTimeOffset.Now;
        Hi.Print($"基础 ACR 已加载: {Path.GetFileName(scriptPath)} ({candidate.TargetJob})");
        return true;
    }

    internal static bool IsReloadAllowed(
        CombatContext.State state,
        bool isBetweenAreas,
        bool isLoadingRotation) =>
        state is not CombatContext.State.InCombat and not CombatContext.State.Zoning
        && !isBetweenAreas
        && !isLoadingRotation;

    internal static bool FailClosed(string message)
    {
        try
        {
            ACRLifecycle.SetDevelopmentOverride(true, null);
        }
        catch (Exception ex)
        {
            LogWarning($"关闭生命周期失败: {ex.GetBaseException().Message}");
        }

        var old = current;
        current = null;
        DisposeCompilation(old, "停止失效脚本");

        State = BasicAcrDevelopmentState.Failed;
        LastError = message;
        LoadedAt = null;

        var details = Diagnostics.Count == 0
            ? message
            : $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, Diagnostics)}";
        DService.Instance().Log.Error($"[BasicAcrDevelopment] {details}");
        Hi.PrintError($"基础 ACR 已停止: {message}");
        return false;
    }

    internal static void Shutdown()
    {
        var old = current;
        current = null;
        DisposeCompilation(old, "关闭开发管理器");

        config = null;
        configDir = string.Empty;
        hostDllDir = string.Empty;
        Diagnostics = [];
        LastError = null;
        LoadedAt = null;
        State = BasicAcrDevelopmentState.Disabled;
    }

    private static void DisposeCompilation(BasicAcrCompilation? compilation, string operation)
    {
        if (compilation is null)
            return;

        try
        {
            compilation.Dispose();
        }
        catch (Exception ex)
        {
            LogWarning($"{operation}失败: {ex.GetBaseException().Message}");
        }
    }

    private static void LogWarning(string message)
    {
        try
        {
            if (DService.IsInitialized)
                DService.Instance().Log.Warning($"[BasicAcrDevelopment] {message}");
        }
        catch { }
    }
}
