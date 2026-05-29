using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// GCD 辅助 —— 剩余时间 / 动态时长 / oGCD 窗口判定
/// </summary>
public static class GCDHelper
{
    /// <summary>GCD 剩余毫秒数</summary>
    public static unsafe float GetGCDCooldown()
    {
        var am = ActionManager.Instance();
        if (am == null) return 0;

        var recastGroup = am->GetRecastGroup((int)ActionType.Action, 9);
        var detail = am->GetRecastGroupDetail(recastGroup);
        if (detail == null || !detail->IsActive) return 0;

        return Math.Max(0, detail->Total - detail->Elapsed) * 1000f;
    }

    /// <summary>GCD 动态时长 (ms) —— 受技能速度/职业特性影响</summary>
    public static unsafe float GetGCDDuration()
    {
        var am = ActionManager.Instance();
        if (am == null) return 2500f;

        var recastGroup = am->GetRecastGroup((int)ActionType.Action, 9);
        return am->GetRecastTimeForGroup(recastGroup) * 1000f;
    }

    /// <summary>GCD 是否可预输入 (剩余时间 &lt;= ActionQueueInMs)</summary>
    public static bool CanUseGCD() => GetGCDCooldown() <= PluginConfig.Instance.ActionQueueInMs;

    /// <summary>是否进入二段能力技窗口 (GCD剩余 &lt;= 阈值, 默认1000ms)</summary>
    public static bool Is2ndAbilityTime(float timeInMs = 1000f) => GetGCDCooldown() <= timeInMs;

    /// <summary>动画锁释放后即可插入 oGCD</summary>
    public static unsafe bool CanUseOffGcd()
    {
        var am = ActionManager.Instance();
        if (am == null) return true;
        return am->AnimationLock <= 0f;
    }

    /// <summary>GCD 是否就绪</summary>
    public static bool IsGCDReady() => GetGCDCooldown() <= 0;
}
