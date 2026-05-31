using static HiAuRo.Data;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    internal void Execute(in PipelineDecision decisions, CombatContext.State state)
    {
        try
        {
            var stack = PrioritySlotStack.Instance;
            stack.Clear(); // 帧初清空，防止跨帧残留

            var stopped = !RuntimeCore.IsRunning;
            var paused = ACR.MainControlHelper.IsPaused;
            var blockBuild = stopped || paused;

            // ── 预执行（立即生效，不 return）──
            if (decisions.ExecAxis?.ForceTarget != null)
                OmenTools.OmenService.TargetManager.Target = decisions.ExecAxis.ForceTarget;

            // ── Push 所有来源到调度栈（高优先级覆盖低优先级）──
            // FactAxis: 已在 DecisionStage.UpdateFactAxis() 中 Push 到 FactAxis 优先级

            // ExecutionAxis ForceSpell
            if (decisions.ExecAxis?.ForceSpell != null && !blockBuild)
            {
                if (CurrentRotation?.CanUseHighPrioritySlotCheck == null
                    || CurrentRotation.CanUseHighPrioritySlotCheck() >= 0)
                {
                    var slot = new Slot();
                    slot.Add(decisions.ExecAxis.ForceSpell);
                    stack.Push(PrioritySlotStack.Priority.ExecAxis, slot);
                }
            }

            // AssistAxis ForceSpell
            if (decisions.AssistAxis?.ForceSpell != null && !blockBuild)
            {
                var slot = new Slot();
                slot.Add(decisions.AssistAxis.ForceSpell);
                stack.Push(PrioritySlotStack.Priority.AssistAxis, slot);
            }

            // SpellQueue
            if (!blockBuild && SpellQueue.HasPending())
            {
                var queued = SpellQueue.GetNext();
                if (queued != null)
                    stack.Push(PrioritySlotStack.Priority.SpellQueue, queued);
            }

            // ── 推进（所有状态下都运行）──
            if (SlotExecutor.IsExecuting)
            {
                var completed = SlotExecutor.ExecuteStep();
                if (completed && OpenerMgr.CurrentState == OpenerMgr.State.Running)
                    AdvanceOpener();
            }

            ExecuteOpenerIfRunning();

            // 非战斗态 Pop（Opener/SpellQueue 等预门控 slot）
            var preGateSlot = stack.Pop();
            if (preGateSlot != null)
                SlotExecutor.StartSlot(preGateSlot);

            // ── 门控 ──
            if (state != CombatContext.State.InCombat) return;
            if (SlotExecutor.IsExecuting) return;
            if (decisions.CanPauseAck) return;

            if (decisions.ExecAxis?.PauseAcr == true) return;

            // AiLoop Build + 统一调度（仅在 SlotExecutor 空闲时）
            if (!blockBuild && AiLoop != null)
            {
                var aiSlot = AiLoop.Build(blockBuild: false);
                if (aiSlot != null)
                    stack.Push(PrioritySlotStack.Priority.AiLoop, aiSlot);
            }

            var picked = stack.Pop();
            if (picked != null)
                SlotExecutor.StartSlot(picked);

            // 注意：ForceTarget（无 ForceSpell 时）仅在无 Slot 执行时生效
            if (decisions.ExecAxis?.ForceTarget != null)
                OmenTools.OmenService.TargetManager.Target = decisions.ExecAxis.ForceTarget;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[AIRunner.ExecutionStage] Execute 异常: {ex}");
        }
    }
}
