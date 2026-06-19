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

    /// <summary>GCD 动态时长 (ms) —— 受技能速度/职业特性影响，默认用技能 9 的复唱组</summary>
    public static unsafe float GetGCDDuration(uint actionId = 9)
    {
        var am = ActionManager.Instance();
        if (am == null) return 2500f;

        var recastGroup = ResolveRecastGroupForDuration(
            id => am->GetRecastGroup((int)ActionType.Action, id),
            actionId);
        return am->GetRecastTimeForGroup(recastGroup) * 1000f;
    }

    internal static int ResolveRecastGroupForDuration(Func<uint, int> getRecastGroup, uint actionId = 9)
        => getRecastGroup(actionId);

    /// <summary>GCD 是否可预输入 (剩余时间 &lt;= ActionQueueInMs)，含咏唱中检查、初始状态</summary>
    public static bool CanUseGCD()
    {
        var cd = GetGCDCooldown();
        if (cd == 0 && !IsGCDActive())
            return true;

        // GCD 刚启动（cd 等于完整时长）→ 立即可用
        if (cd > 0 && IsGCDActive()
            && Math.Abs(cd - GetGCDDuration()) < 1)
            return true;

        // 咏唱中且剩余咏唱 > 预输入窗口 → 不可用
        if (Data.Combat.IsCasting)
        {
            var me = Data.Me.Object;
            if (me != null)
            {
                var remainCast = (me.TotalCastTime - me.CurrentCastTime) * 1000f;
                if (remainCast > PluginConfig.Instance.ActionQueueInMs)
                    return false;
            }
        }

        return cd <= PluginConfig.Instance.ActionQueueInMs;
    }

    /// <summary>是否进入二段能力技窗口 (GCD剩余 &lt;= 阈值, 默认1000ms)</summary>
    public static bool Is2ndAbilityTime(float timeInMs = 1000f) => GetGCDCooldown() <= timeInMs;

    /// <summary>动画锁释放后即可插入 oGCD</summary>
    public static unsafe bool CanUseOffGcd()
    {
        var am = ActionManager.Instance();
        if (am == null) return true;
        return am->AnimationLock <= 0f;
    }

    /// <summary>GCD 计时器是否已激活（RecastDetail.IsActive）</summary>
    public static unsafe bool IsGCDActive()
    {
        var am = ActionManager.Instance();
        if (am == null) return false;
        var recastGroup = am->GetRecastGroup((int)ActionType.Action, 9);
        var detail = am->GetRecastGroupDetail(recastGroup);
        return detail != null && detail->IsActive;
    }

    /// <summary>GCD 是否就绪</summary>
    public static bool IsGCDReady() => GetGCDCooldown() <= 0;
}
