namespace HiAuRo.Infrastructure;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

public static class Hi
{
    public static void Print(string msg)
    {
        DService.Instance().Chat.Print(new XivChatEntry
        {
            Message = SeString.Parse(System.Text.Encoding.UTF8.GetBytes($"[HiAuRo] {msg}")),
            Type = XivChatType.Echo
        });
        LogManager.Instance.WriteLine("Info", msg);
    }

    public static void PrintError(string msg)
    {
        DService.Instance().Chat.Print(new XivChatEntry
        {
            Message = SeString.Parse(System.Text.Encoding.UTF8.GetBytes($"[HiAuRo] {msg}")),
            Type = XivChatType.Urgent
        });
        LogManager.Instance.WriteLine("Error", msg);
    }

    /// <summary>输出调试日志</summary>
    public static void Debug(string msg) =>
        DService.Instance().Log.Debug($"[HiAuRo] {msg}");

    /// <summary>输出 ACR 运行时细粒度日志（默认关闭，受宿主 PluginConfig 控制）</summary>
    public static void AcrRuntimeDebug(string msg)
    {
        if (PluginConfig.Instance is { AcrRuntimeDebugEnabled: true })
            DService.Instance().Log.Debug($"[HiAuRo] {msg}");
    }

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

    /// <summary>发送原始聊天消息或执行指令。以 '/' 开头视为宏指令，否则输出聊天消息（不加前缀）。</summary>
    public static void SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (message.StartsWith('/'))
            DService.Instance().Command.ProcessCommand(message);
        else
            DService.Instance().Chat.Print(message);
    }

    /// <summary>延迟执行（在主线程 Coroutine 上等待 ms 毫秒后调用 action）</summary>
    public static void Delay(double ms, Action action)
    {
        if (ms <= 0) { action(); return; }
        _ = DelayOnFrameworkAsync((int)Math.Max(1, Math.Round(ms)), action);
    }

    private static async Task DelayOnFrameworkAsync(int ms, Action action)
    {
        try
        {
            await Task.Delay(ms);
            await DService.Instance().Framework.RunOnFrameworkThread(action);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[Hi] Delay 异常: {ex.Message}");
        }
    }

    /// <summary>当前战斗已持续毫秒数（进战清零）</summary>
    public static int BattleTimeMs => Runtime.ACRLifecycle.Runner.BattleTimeMs;
}
