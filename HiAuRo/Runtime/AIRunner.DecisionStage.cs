using HiAuRo.Execution;
using HiAuRo.FactAxis;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    /// <summary>决策层 InCombat：轴轮询 + AI Check + 事实轴。异常时返回空 PipelineDecision，Execution 仅推进已有 Slot。</summary>
    internal PipelineDecision Decide()
    {
        var d = new PipelineDecision();
        try
        {
            if (CurrentRotation?.CanPauseACRCheck != null)
            {
                d.CanPauseAck = CurrentRotation.CanPauseACRCheck() > 0;
            }

            if (ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis)
                d.ExecAxis = ExecutionAxis.Instance.Update(_battleTimeMs);

            d.AssistAxis = AssistAxis.Instance.Update(_battleTimeMs);

            // 事实轴：事件推进 + 决策计算（技能通过 PrioritySlotStack 提交，由 ExecutionStage 统一调度）
            UpdateFactAxis();

            AiLoop?.CheckAll();
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[AIRunner.DecisionStage] Decide 异常: {ex}");
        }

        return d;
    }

    /// <summary>决策层非战斗态：倒计时 + PreCombat + AI Check</summary>
    internal PipelineDecision PreCombatDecide(CombatContext.State state)
    {
        var d = new PipelineDecision();
        try
        {
            if (state == CombatContext.State.OutOfCombat)
                CurrentRotation?.EventHandler?.OnPreCombat();

            UpdateCountDown();
            AiLoop?.CheckAll();
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[AIRunner.DecisionStage] PreCombatDecide 异常: {ex}");
        }

        return d;
    }
}
