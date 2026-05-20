namespace HiAuRo.Runtime;

/// <summary>
/// 主 Tick 循环入口
/// </summary>
public static class RuntimeCore
{
    /// <summary>是否正在运行</summary>
    public static bool IsRunning { get; private set; }

    /// <summary>启动 Tick 循环</summary>
    public static void Start()
    {
        if (IsRunning) return;
        ACR.MainControlHelper.Unpause();
        OmenTools.OmenService.FrameworkManager.Instance().Reg(OnTick);
        IsRunning = true;
    }

    /// <summary>停止 Tick 循环</summary>
    public static void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
    }

    /// <summary>完全关闭，注销 tick（插件 Dispose 时调用）</summary>
    public static void Shutdown()
    {
        if (IsRunning) Stop();
        OmenTools.OmenService.FrameworkManager.Instance().Unreg(OnTick);
    }

    private static void OnTick(Dalamud.Plugin.Services.IFramework _)
    {
        if (!IsRunning) return;
        try
        {
            if (!HiAuRo.Data.IsReady)
            {
                CombatContext.Reset();
                return;
            }

            Coroutine.Instance.Update();
            CombatContext.Check();
            EventSystem.CheckTargetChanged();
            ACR.HotkeyPoller.Update();
            ACRLifecycle.Update();
            PluginLifecycle.Update();
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[RuntimeCore] OnTick 异常: {ex}");
        }
    }
}
