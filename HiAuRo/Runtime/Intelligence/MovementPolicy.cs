namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 移动策略 — 决定什么时候移动
/// </summary>
public enum MovementPolicy
{
    /// <summary>惰性策略 — 尽量晚走，卡着 deadline 或最大不打断读条的时间</summary>
    Mechanic,
    /// <summary>集合策略 — 尽快到达目标位置</summary>
    Gather,
}
