using HiAuRo.Execution;
using HiAuRo.FactAxis;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    /// <summary>决策层 InCombat：轴轮询 + AI Check + 事实轴</summary>
    internal PipelineDecision Decide()
    {
        var d = new PipelineDecision();

        if (CurrentRotation?.CanPauseACRCheck != null)
        {
            d.CanPauseAck = CurrentRotation.CanPauseACRCheck() > 0;
        }

        if (ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis)
            d.ExecAxis = ExecutionAxis.Instance.Update(_battleTimeMs);

        d.AssistAxis = AssistAxis.Instance.Update(_battleTimeMs);

        // 事实轴（暂保留 StartSlot 调用，迁移步骤 2 再分离到 FactSlots）
        UpdateFactAxis();

        AiLoop?.CheckAll();

        return d;
    }

    /// <summary>决策层非战斗态：倒计时 + PreCombat + AI Check</summary>
    internal PipelineDecision PreCombatDecide(CombatContext.State state)
    {
        var d = new PipelineDecision();

        if (state == CombatContext.State.OutOfCombat)
            CurrentRotation?.EventHandler?.OnPreCombat();

        UpdateCountDown();
        AiLoop?.CheckAll();

        return d;
    }
}
