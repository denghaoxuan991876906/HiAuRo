namespace HiAuRo.Infrastructure;

/// <summary>HiAuRo 便捷日志/聊天工具类</summary>
public static class Hi
{
    /// <summary>打印聊天消息</summary>
    public static void Print(string msg) =>
        DService.Instance().Chat.Print($"[HiAuRo] {msg}");

    /// <summary>打印错误聊天消息（红色）</summary>
    public static void PrintError(string msg) =>
        DService.Instance().Chat.PrintError($"[HiAuRo] {msg}");

    /// <summary>输出调试日志</summary>
    public static void Debug(string msg) =>
        DService.Instance().Log.Debug($"[HiAuRo] {msg}");

    /// <summary>输出详细日志（Verbose）</summary>
    public static void Verbose(string msg) =>
        DService.Instance().Log.Verbose($"[HiAuRo] {msg}");

    /// <summary>输出信息日志</summary>
    public static void Info(string msg) =>
        DService.Instance().Log.Information($"[HiAuRo] {msg}");

    /// <summary>输出警告日志</summary>
    public static void Warn(string msg) =>
        DService.Instance().Log.Warning($"[HiAuRo] {msg}");

    /// <summary>输出错误日志</summary>
    public static void Error(string msg) =>
        DService.Instance().Log.Error($"[HiAuRo] {msg}");
}
