using static HiAuRo.Data;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    internal void Execute(in PipelineDecision decisions, CombatContext.State state)
    {
        try
        {
            var stopped = !RuntimeCore.IsRunning;
            var paused = ACR.MainControlHelper.IsPaused;
            var blockBuild = stopped || paused;
            var stack = PrioritySlotStack.Instance;

            // ── 预执行（立即生效，不 return）──
            if (decisions.ExecAxis?.ForceTarget != null)
                OmenTools.OmenService.TargetManager.Target = decisions.ExecAxis.ForceTarget;

            if (!blockBuild && CurrentRotation?.Opener != null
                && OpenerMgr.CurrentState == OpenerMgr.State.NotStarted)
            {
                OpenerMgr.Start(CurrentRotation.Opener);
            }

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

            // Opener: peek current slot, push if available
            if (OpenerMgr.CurrentState == OpenerMgr.State.Running)
            {
                var openerSlot = OpenerMgr.PeekCurrentSlot();
                if (openerSlot != null)
                    stack.Push(PrioritySlotStack.Priority.Opener, openerSlot);
            }

            // SpellQueue
            if (!blockBuild && SpellQueue.HasPending())
            {
                var queued = SpellQueue.GetNext();
                if (queued != null)
                    stack.Push(PrioritySlotStack.Priority.SpellQueue, queued);
            }

            // AiLoop 正常循环
            if (!blockBuild && AiLoop != null)
            {
                var aiSlot = AiLoop.Build(blockBuild: false);
                if (aiSlot != null)
                    stack.Push(PrioritySlotStack.Priority.AiLoop, aiSlot);
            }

            // ── 统一调度：取本帧最高优先级 Slot 执行 ──
            var picked = stack.Pop();
            if (picked != null)
                SlotExecutor.StartSlot(picked);

            // ── 推进（所有状态下都运行）──
            if (SlotExecutor.IsExecuting)
            {
                var completed = SlotExecutor.ExecuteStep();
                if (completed && OpenerMgr.CurrentState == OpenerMgr.State.Running)
                    AdvanceOpener();
            }

            ExecuteOpenerIfRunning();

            // ── 门控 ──
            if (state != CombatContext.State.InCombat) return;
            if (SlotExecutor.IsExecuting) return;
            if (decisions.CanPauseAck) return;

            if (decisions.ExecAxis?.PauseAcr == true) return;

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
