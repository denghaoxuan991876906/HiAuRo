using HiAuRo.Infrastructure;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 智能层——消费事实轴事件，释放对应的移动/站位需求。
/// 两种触发模式由 FactAxisFlags.MovementTriggerMode 控制：
///   FactNode — 事实节点匹配（原有逻辑）
///   CombatTime — 战斗时间（AddedOrder 作为目标战斗时间）
/// </summary>
public sealed class IntelligenceEngine
{
    /// <summary>智能层单例</summary>
    public static IntelligenceEngine Instance { get; } = new();

    /// <summary>当前激活的需求（已释放待执行）</summary>
    public List<MovementDemand> ActiveDemands { get; } = [];

    private readonly HashSet<string> _releasedFactNodeIds = [];

    private IntelligenceEngine() { }

    /// <summary>
    /// 每帧由 AIRunner 调用——根据 MovementTriggerMode 选择触发方式释放移动需求
    /// </summary>
    public void Update(FactAxis.FactTimeline timeline)
    {
        var state = timeline.State;
        var flags = PluginConfig.Instance.FactAxis;

        switch (flags.MovementTriggerMode)
        {
            case MovementTriggerMode.CombatTime:
                ReleaseByCombatTime(state);
                break;
            case MovementTriggerMode.FactNode:
            default:
                ReleaseByFactNode(state);
                break;
        }
    }

    private void ReleaseByCombatTime(FactAxis.FactState state)
    {
        foreach (var d in DemandBuffer.DrainByCombatTime())
        {
            DService.Instance().Log.Information(
                $"[Intelligence] 释放战斗时间需求: {d.Id} " +
                $"类型={d.Type} 目标时间={d.AddedOrder}");
            ActiveDemands.Add(d);
        }
    }

    private void ReleaseByFactNode(FactAxis.FactState state)
    {
        var ev = state.CurrentEvent;
        if (ev == null) return;
        if (_releasedFactNodeIds.Contains(ev.Id)) return;

        var grouped = DemandBuffer.GetGrouped();
        if (!grouped.Contains(ev.Id)) return;

        var demands = grouped[ev.Id].ToList();
        if (demands.Count == 0) return;

        foreach (var d in demands)
        {
            DService.Instance().Log.Information(
                $"[Intelligence] 释放事实节点需求: {d.Id} " +
                $"事实节点={d.FactNodeId} 类型={d.Type} " +
                $"来源={d.Source}");
            ActiveDemands.Add(d);
        }

        DemandBuffer.Remove(demands.Select(d => d.Id));
        _releasedFactNodeIds.Add(ev.Id);

        if (_releasedFactNodeIds.Count > 1000)
            _releasedFactNodeIds.Clear();
    }

    /// <summary>战斗重置时清空状态</summary>
    public void Reset()
    {
        ActiveDemands.Clear();
        _releasedFactNodeIds.Clear();
    }
}
