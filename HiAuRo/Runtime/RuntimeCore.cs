using HiAuRo.FactAxis;
using HiAuRo.Runtime.Movement;
using HiAuRo.Script;

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
#if DEBUG
        Infrastructure.PerfMonitor.Register(
            // RuntimeCore 层级
            "Tick.Total", "ImGuiState", "CombatContext", "EventSystem", "HotkeyPoller", "ACRLifecycle", "PluginLifecycle",
            // AIRunner 层级
            "Objects.Refresh", "Party.Refresh", "BattleUpdate",
            "ExecutionAxis", "FactAxis", "AssistAxis",
            "AILoop", "SlotExec",
            // UI 层级
            "UI.Total", "UI.PerfMon", "UI.Hotkey", "UI.QtPanel", "UI.StatusBar", "UI.MainWindow"
        );
#endif
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
#if DEBUG
        long _totalStart = System.Diagnostics.Stopwatch.GetTimestamp();
        long _pt0 = _totalStart;
#endif
        ACRLifecycle.PushImGuiState(); // 无论是否运行，每帧同步 ImGui 悬浮窗状态
#if DEBUG
        PerfMonitor.Record("ImGuiState", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif

        // 数据层 + 脚本生命周期 + 战斗状态检测不依赖 ACR 运行状态；游戏就绪即运行
        if (HiAuRo.Data.IsReady)
        {
            HiAuRo.Data.Objects.Refresh();
            HiAuRo.Data.Party.Refresh();
            ScriptManager.Update();
            CombatContext.Check();
            if (CombatContext.CurrentState == CombatContext.State.InCombat)
                ACRLifecycle.Runner.BattleTimeMs += (int)(HiAuRo.Data.Combat.DeltaTime * 1000);
            MovementService.Instance.Update(FactTimeline.Instance.State);
            ScriptEngine.Instance.PollChecks();
            Recording.CombatRecorder.Instance.UpdateFrameCache();
        }

        BasicAcrDevelopment.Update();
        if (!IsRunning) return;
        try
        {
            if (!HiAuRo.Data.IsReady)
            {
                CombatContext.Reset();
                return;
            }

            Coroutine.Instance.Update();
            EventSystem.CheckTargetChanged();
#if DEBUG
            PerfMonitor.Record("EventSystem", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ACR.HotkeyPoller.Update();
#if DEBUG
            PerfMonitor.Record("HotkeyPoller", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ACRLifecycle.Update();
#if DEBUG
            PerfMonitor.Record("ACRLifecycle", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            PluginLifecycle.Update();
#if DEBUG
            PerfMonitor.Record("PluginLifecycle", _pt0);
            PerfMonitor.Record("Tick.Total", _totalStart);
#endif
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[RuntimeCore] OnTick 异常: {ex}");
        }
    }
}
