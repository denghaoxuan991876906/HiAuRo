using HiAuRo.Execution;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using OmenTools;

namespace HiAuRo.Runtime;

/// <summary>模式切换管理 —— 执行轴/事实轴/无模式</summary>
public static class ModeSwitch
{
    /// <summary>模式枚举</summary>
    public enum Mode
    {
        /// <summary>无模式</summary>
        None,
        /// <summary>执行轴模式</summary>
        ExecutionAxis,
        /// <summary>事实轴模式</summary>
        FactAxis
    }

    /// <summary>当前模式</summary>
    public static Mode CurrentMode { get; private set; } = Mode.None;

    /// <summary>设置模式</summary>
    public static void SetMode(Mode newMode)
    {
        if (CurrentMode == newMode) return;

        switch (CurrentMode)
        {
            case Mode.ExecutionAxis:
                ExecutionAxis.Instance.Shutdown();
                break;
            case Mode.FactAxis:
                FactTimeline.Instance.Shutdown();
                break;
        }

        CurrentMode = newMode;

        switch (newMode)
        {
            case Mode.None:
                break;
            case Mode.ExecutionAxis:
                ExecutionAxis.Instance.Init();
                ExecutionAxis.Instance.AutoLoadTimeline();
                if (Runtime.CombatContext.IsInCombat) ExecutionAxis.Instance.Start();
                break;
            case Mode.FactAxis:
                FactTimeline.Instance.Init();
                if (Runtime.CombatContext.IsInCombat) FactTimeline.Instance.Start();
                break;
        }
    }

    /// <summary>获取当前模式</summary>
    public static Mode GetMode() => CurrentMode;

    /// <summary>切换事实轴开关</summary>
    public static void ToggleFactAxis()
    {
        if (CurrentMode == Mode.FactAxis)
        {
            SetMode(Mode.None);
            DService.Instance().Chat.Print("[HiAuRo] 事实轴已关闭");
        }
        else
        {
            SetMode(Mode.FactAxis);
            DService.Instance().Chat.Print("[HiAuRo] 事实轴已启用");
        }
    }

    /// <summary>切图时按配置优先级自动切换模式</summary>
    public static void TryAutoSwitch()
    {
        if (CurrentMode != Mode.None) return;

        var territoryId = OmenTools.OmenService.GameState.TerritoryType;
        if (territoryId == 0) return;

        var configDir = DService.Instance().PI.ConfigDirectory.FullName;
        bool hasExec = System.IO.File.Exists(System.IO.Path.Combine(configDir, "ExecutionTimelines", $"{territoryId}.json"));
        bool hasFact = System.IO.File.Exists(System.IO.Path.Combine(configDir, "FactTimelines", $"{territoryId}.json"));

        var autoSwitch = PluginConfig.Instance.AutoSwitch;

        if (hasExec && hasFact)
        {
            SetMode(autoSwitch == AutoSwitchMode.Fact优先 ? Mode.FactAxis : Mode.ExecutionAxis);
            DService.Instance().Log.Information($"[ModeSwitch] 双 JSON 存在, 优先级={autoSwitch}, 切换={CurrentMode}");
        }
        else if (hasExec)
        {
            SetMode(Mode.ExecutionAxis);
            DService.Instance().Log.Information($"[ModeSwitch] 自动切换执行轴: {territoryId}");
        }
        else if (hasFact && PluginConfig.Instance.FactAxis.Observe)
        {
            SetMode(Mode.FactAxis);
            DService.Instance().Log.Information($"[ModeSwitch] 自动切换事实轴: {territoryId}");
        }
    }
}
