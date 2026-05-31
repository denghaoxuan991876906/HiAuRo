using static HiAuRo.Data;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    internal void Execute(in PipelineDecision decisions, CombatContext.State state)
    {
        var stopped = !RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;
        var blockBuild = stopped || paused;

        // 预执行: ForceTarget for current slot, OpenerMgr.Start, ProcessSpellQueue
        if (decisions.ExecAxis?.ForceTarget != null)
            OmenTools.OmenService.TargetManager.Target = decisions.ExecAxis.ForceTarget;

        if (!blockBuild && CurrentRotation?.Opener != null
            && OpenerMgr.CurrentState == OpenerMgr.State.NotStarted)
        {
            OpenerMgr.Start(CurrentRotation.Opener);
        }

        if (!blockBuild && SpellQueue.HasPending())
        {
            var queued = SpellQueue.GetNext();
            if (queued != null)
            {
                SlotExecutor.StartSlot(queued);
            }
        }

        // 推进: Slot step + Opener step (both run regardless of combat state)
        if (SlotExecutor.IsExecuting)
        {
            var completed = SlotExecutor.ExecuteStep();
            if (completed && OpenerMgr.CurrentState == OpenerMgr.State.Running)
                AdvanceOpener();
        }

        ExecuteOpenerIfRunning();

        // 非战斗态门控
        if (state != CombatContext.State.InCombat) return;

        // Slot 构建门控
        if (SlotExecutor.IsExecuting) return;
        if (decisions.CanPauseAck) return;

        // 轴输出应用: ExecutionAxis
        if (decisions.ExecAxis != null)
        {
            if (decisions.ExecAxis.PauseAcr) return;

            if (decisions.ExecAxis.ForceTarget != null)
                OmenTools.OmenService.TargetManager.Target = decisions.ExecAxis.ForceTarget;

            if (decisions.ExecAxis.ForceSpell != null && !blockBuild)
            {
                if (CurrentRotation?.CanUseHighPrioritySlotCheck != null)
                {
                    if (CurrentRotation.CanUseHighPrioritySlotCheck() < 0) return;
                }
                var slot = new Slot();
                slot.Add(decisions.ExecAxis.ForceSpell);
                SlotExecutor.StartSlot(slot);
                return;
            }
        }

        // 辅助轴输出
        if (decisions.AssistAxis?.ForceSpell != null && !blockBuild)
        {
            var slot = new Slot();
            slot.Add(decisions.AssistAxis.ForceSpell);
            SlotExecutor.StartSlot(slot);
            return;
        }

        // 事实轴待执行技能
        if (decisions.FactSlots != null)
        {
            foreach (var factSlot in decisions.FactSlots)
            {
                SlotExecutor.StartSlot(factSlot);
                return;
            }
        }

        // 构建: AI loop build (SpellQueue already handled in pre-execution)
        if (AiLoop == null) return;
        var nextSlot = AiLoop.Build(blockBuild);
        if (nextSlot != null)
            SlotExecutor.StartSlot(nextSlot);
    }
}
