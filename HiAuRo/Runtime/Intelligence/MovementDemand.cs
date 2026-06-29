using System.Numerics;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>移动/传送/站定 需求（本地数据，角色路由由外部分发插件负责）</summary>
public sealed class MovementDemand
{
    /// <summary>需求唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>关联的事实节点 ID</summary>
    public string FactNodeId { get; set; } = "";
    /// <summary>需求类型</summary>
    public DemandType Type { get; set; } = DemandType.MoveTo;
    /// <summary>目标位置</summary>
    public Vector3? TargetPos { get; set; }
    /// <summary>目标朝向</summary>
    public float? TargetHeading { get; set; }
    /// <summary>停留持续时间（秒）</summary>
    public float? Duration { get; set; }
    /// <summary>添加顺序</summary>
    public int AddedOrder { get; set; }
    /// <summary>来源描述</summary>
    public string Source { get; set; } = "";
    /// <summary>移动策略（默认 Mechanic 惰性策略）</summary>
    public MovementPolicy Policy { get; set; } = MovementPolicy.Mechanic;
}
