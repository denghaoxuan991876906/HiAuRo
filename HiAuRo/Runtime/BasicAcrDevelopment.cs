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
    private static Task? helperInitializationTask;
    private static bool pendingInitialLoad;

    internal static BasicAcrDevelopmentState State { get; private set; } = BasicAcrDevelopmentState.Disabled;
    internal static IReadOnlyList<BasicAcrDiagnostic> Diagnostics { get; private set; } = [];
    internal static string? LastError { get; private set; }
    internal static string? ScriptTypeName => current?.ScriptTypeName;
    internal static Jobs? TargetJob => current?.TargetJob;
    internal static DateTimeOffset? LoadedAt { get; private set; }

    internal static void Init(
        PluginConfig pluginConfig,
        string pluginConfigDir,
        string pluginHostDllDir,
        Task initializationTask)
    {
        config = pluginConfig;
        configDir = pluginConfigDir;
        hostDllDir = pluginHostDllDir;
        helperInitializationTask = initializationTask;
        pendingInitialLoad = false;
        Diagnostics = [];
        LastError = null;
        LoadedAt = null;

        if (!pluginConfig.BasicAcrScriptEnabled)
        {
            State = BasicAcrDevelopmentState.Disabled;
            return;
        }

        try
        {
            ACRLifecycle.SetDevelopmentOverride(true, null);
        }
        catch (Exception ex)
        {
            FailClosed($"基础 ACR 开发模式初始化失败: {ex.GetBaseException().Message}");
            return;
        }

        State = BasicAcrDevelopmentState.NotLoaded;
        pendingInitialLoad = true;
    }

    internal static void Update()
    {
        if (!ShouldRunPendingInitialLoad(
                pendingInitialLoad,
                helperInitializationTask?.IsCompleted == true))
            return;

        pendingInitialLoad = false;
        ReloadNow();
    }

    internal static bool SetEnabled(bool enabled)
    {
        var pluginConfig = config
            ?? throw new InvalidOperationException("基础 ACR 开发管理器尚未初始化");

        pendingInitialLoad = false;
        Diagnostics = [];
        LastError = null;
        LoadedAt = null;

        if (enabled)
        {
            pluginConfig.BasicAcrScriptEnabled = true;
            try
            {
                ACRLifecycle.SetDevelopmentOverride(true, null);
            }
            catch (Exception ex)
            {
                FailClosed($"启用基础 ACR 开发模式失败: {ex.GetBaseException().Message}");
                return true;
            }

            var old = current;
            current = null;
            DisposeCompilation(old, "重置旧脚本");
            State = BasicAcrDevelopmentState.NotLoaded;
            return true;
        }

        string? switchError = null;
        try
        {
            ACRLifecycle.SetDevelopmentOverride(false, null);
        }
        catch (Exception ex)
        {
            switchError = $"禁用基础 ACR 开发模式失败: {ex.GetBaseException().Message}";
            LogWarning(switchError);
        }
        finally
        {
            var old = current;
            current = null;
            DisposeCompilation(old, "禁用开发模式");
            pluginConfig.BasicAcrScriptEnabled = false;
            State = BasicAcrDevelopmentState.Disabled;
            LastError = switchError;
        }

        return true;
    }

    internal static bool Reload()
    {
        var pluginConfig = config;
        if (pluginConfig?.BasicAcrScriptEnabled != true)
            return false;

        pendingInitialLoad = false;
        return ReloadNow();
    }

    private static bool ReloadNow()
    {
        var runner = ACRLifecycle.Runner;
        runner.StopSlotGeneration();

        try
        {
            var success = ReloadCore();
            if (success)
                runner.ResumeSlotGeneration();
            return success;
        }
        catch (Exception ex)
        {
            return FailClosed($"基础 ACR 重载失败: {ex.GetBaseException().Message}");
        }
    }

    private static bool ReloadCore()
    {
        var pluginConfig = config;
        if (pluginConfig?.BasicAcrScriptEnabled != true)
            return false;

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
        {
            var message = result.ErrorMessage ?? "基础 ACR 编译失败";
            if (Diagnostics.Any(diagnostic =>
                    string.Equals(diagnostic.ToString(), message, StringComparison.Ordinal)))
                message = "基础 ACR 编译失败";
            return FailClosed(message);
        }

        BasicAcrCompilation? candidate = result.TakeCompilation();
        if (candidate is null)
            return FailClosed("基础 ACR 编译结果为空");

        var old = current;
        var loadedJob = Jobs.None;
        try
        {
            loadedJob = candidate.TargetJob;
            var registry = new ACRLifecycle.AcrRegistryEntry(
                InstallKey,
                $"Basic ACR: {Path.GetFileNameWithoutExtension(scriptPath)}",
                "",
                "",
                "dev",
                Path.GetDirectoryName(scriptPath)!,
                Path.Combine(configDir, "setting", "ACR", InstallKey),
                candidate.Entry);

            ACRLifecycle.SetDevelopmentOverride(true, registry);

            current = TransferCandidateOwnership(ref candidate);
            DisposeCompilation(old, "卸载旧脚本");
            State = BasicAcrDevelopmentState.Ready;
            LastError = null;
            LoadedAt = DateTimeOffset.Now;
        }
        catch (Exception ex)
        {
            return FailClosed($"应用基础 ACR 失败: {ex.GetBaseException().Message}");
        }
        finally
        {
            DisposeCompilation(candidate, "释放未应用的候选脚本");
        }

        PrintSuccess(scriptPath, loadedJob);
        return true;
    }

    internal static bool ShouldRunPendingInitialLoad(
        bool isPending,
        bool isHelperInitializationCompleted) =>
        isPending
        && isHelperInitializationCompleted;

    internal static BasicAcrCompilation TransferCandidateOwnership(
        ref BasicAcrCompilation? candidate)
    {
        var transferred = candidate
            ?? throw new InvalidOperationException("基础 ACR 候选对象为空");
        candidate = null;
        return transferred;
    }

    internal static bool FailClosed(string message)
    {
        pendingInitialLoad = false;
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

        LogError(message);
        foreach (var diagnostic in Diagnostics)
        {
            var diagnosticText = diagnostic.ToString();
            if (!string.Equals(diagnosticText, message, StringComparison.Ordinal))
                LogError(diagnosticText);
        }
        PrintError($"基础 ACR 已停止: {message}");
        return false;
    }

    internal static void Shutdown()
    {
        pendingInitialLoad = false;
        helperInitializationTask = null;
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

        var quiescence = ACRLifecycle.Runner.SlotQuiescence;
        if (!quiescence.IsCompleted)
        {
            _ = quiescence.ContinueWith(
                static (_, state) =>
                {
                    try { ((BasicAcrCompilation)state!).Dispose(); }
                    catch { }
                },
                compilation,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            return;
        }

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

    private static void LogError(string message)
    {
        try
        {
            if (DService.IsInitialized)
                DService.Instance().Log.Error($"[BasicAcrDevelopment] {message}");
        }
        catch { }
    }

    private static void PrintError(string message)
    {
        try
        {
            Hi.PrintError(message);
        }
        catch (Exception ex)
        {
            LogWarning($"输出错误提示失败: {ex.GetBaseException().Message}");
        }
    }

    private static void PrintSuccess(string scriptPath, Jobs targetJob)
    {
        try
        {
            Hi.Print($"基础 ACR 已加载: {Path.GetFileName(scriptPath)} ({targetJob})");
        }
        catch (Exception ex)
        {
            LogWarning($"输出成功提示失败: {ex.GetBaseException().Message}");
        }
    }
}
