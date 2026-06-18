namespace HiAuRo.ACR;

/// <summary>
/// 单个技能执行行为
/// </summary>
public sealed class SlotAction
{
    /// <summary>要使用的技能</summary>
    public required Spell Spell { get; init; }

    /// <summary>等待类型</summary>
    public WaitType Wait { get; init; } = WaitType.None;

    /// <summary>延迟毫秒数（WaitInMs 时有效）</summary>
    public int TimeInMs { get; init; }

    /// <summary>执行失败最大尝试时间 (ms)，默认 1000</summary>
    public int MaxDuration { get; init; } = 1000;
}
