using HiAuRo.ACR;
using HiAuRo.Execution;

namespace HiAuRo.Runtime;

/// <summary>
/// DecisionStage 产出的决策结果。栈分配 struct，零 GC。
/// </summary>
internal struct PipelineDecision
{
    public ExecutionOutput? ExecAxis;       // 执行轴产出
    public ExecutionOutput? AssistAxis;     // 辅助轴产出
    public bool CanPauseAck;                // ACR 暂停请求
    public List<Slot>? FactSlots;           // 事实轴待执行技能列表（复用已有 List 实例，不新增分配）
}
