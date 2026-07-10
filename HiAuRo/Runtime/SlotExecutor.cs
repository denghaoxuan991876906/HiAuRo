using System.Collections.Concurrent;
using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Runtime;

public sealed class SlotExecutor
{
    private readonly AIRunner _runner;

    public SlotExecutor(AIRunner runner) { _runner = runner; }

    internal static SlotActionGateOutcome EvaluatePreActionGateForTests(bool isDead, bool isPaused)
    {
        if (isDead) return SlotActionGateOutcome.StopAndDropSlot;
        if (isPaused) return SlotActionGateOutcome.BlockKeepSlot;
        return SlotActionGateOutcome.Allow;
    }

    internal static SlotActionGateOutcome EvaluatePostActionGateForTests(bool isDead, bool isPaused, bool actionSucceeded)
    {
        if (isDead) return SlotActionGateOutcome.StopAndDropSlot;
        if (isPaused) return actionSucceeded ? SlotActionGateOutcome.BlockAfterConsume : SlotActionGateOutcome.BlockKeepSlot;
        return SlotActionGateOutcome.Allow;
    }

    internal static bool ShouldDiscardHighPrioritySlot(Func<Slot?, int>? check, Slot slot) => check != null && check(slot) < 0;

    internal static bool ShouldScheduleBeforeSpell(Slot? beforeSlot, Slot currentSlot) => beforeSlot != null && !ReferenceEquals(beforeSlot, currentSlot);

    internal static bool TryDequeueCompletedHighPrioritySlot(ConcurrentQueue<Slot> queue, Slot completedSlot)
    {
        if (!queue.TryPeek(out var head) || !ReferenceEquals(head, completedSlot)) return false;
        return queue.TryDequeue(out _);
    }

    public bool CheckNextSlot(BattleData bd)
    {
        if (bd.NextSlot == null) return false;
        bd.NextSlot.InSequence = true;
        bd.NextSlot.BypassesRotationPause = true;
        bd.SetCurrSlot(bd.NextSlot);
        bd.NextSlot = null;
        return true;
    }

    public async Task<bool> HandleSlot(BattleData bd, bool rotationPaused = false)
    {
        var handledWaitGcdSlot = false;
        if (bd.WaitGcdSlot is { } waitGcdSlot
            && AIRunner.ShouldHandleCurrentSlotDuringRotationPause(rotationPaused, waitGcdSlot))
        {
            if (!GCDHelper.CanUseGCD()) return true;

            if (waitGcdSlot.AppendedSequence != null)
            {
                var wrapper = new SequenceWrapper
                {
                    Sequence = waitGcdSlot.AppendedSequence,
                    CompeltedAction = waitGcdSlot.CompletedAction
                };
                bd.PushSequence(wrapper);
            }
            else
            {
                waitGcdSlot.CompletedAction?.Invoke();
            }
            bd.PopWaitGcdSlot();
            handledWaitGcdSlot = true;
        }

        if (bd.CurrSlot == null)
        {
            if (!bd.CurrSlotStack.TryPeek(out var stackedSlot)
                || !AIRunner.ShouldHandleCurrentSlotDuringRotationPause(rotationPaused, stackedSlot))
                return handledWaitGcdSlot;
            bd.PopCurrSlot();
        }

        if (bd.CurrSlot is { } currentSlot)
        {
            if (!AIRunner.ShouldHandleCurrentSlotDuringRotationPause(rotationPaused, currentSlot))
                return handledWaitGcdSlot;

            if (await RunSlot(bd, currentSlot, true))
            {
                if (currentSlot.HighPriorityQueueOwner is { } highPriorityQueueOwner)
                    TryDequeueCompletedHighPrioritySlot(highPriorityQueueOwner, currentSlot);
                bd.SetCurrSlot(null);
            }
            return true;
        }
        return handledWaitGcdSlot;
    }

    public async Task<bool> HandleSlotSequence(BattleData bd)
    {
        var rot = _runner.CurrentRotation;
        if (bd.CurrSequence == null)
        {
            if (bd.PopSequenceStack()) { Hi.AcrRuntimeDebug("[SlotExec] 恢复嵌套序列"); return true; }

            // 只在倒计时后才自动使用起手
            if (bd.CurrSequence == null && CountDownHandler.Instance.CanDoAction
                && await OpenerMgr.Instance.UseOpener(bd, rot))
            {
                Hi.AcrRuntimeDebug($"[SlotExec] 起手已推送: {bd.CurrSequence?.Sequence.GetType().Name}");
                return true;
            }

            if (bd.CurrSequence == null && rot?.SlotSequences is { Count: > 0 })
            {
                foreach (var seq in rot.SlotSequences)
                {
                    var sc = seq.StartCheck();
                    Hi.AcrRuntimeDebug($"[SlotExec] 检查序列: {seq.GetType().Name} StartCheck={sc}");
                    if (sc >= 0)
                    {
                        bd.PushSequence(new SequenceWrapper
                        {
                            Sequence = (ISlotSequence)Activator.CreateInstance(seq.GetType())!
                        });
                        Hi.AcrRuntimeDebug($"[SlotExec] 序列已启动: {seq.GetType().Name}");
                        return true;
                    }
                }
            }
            return false;
        }

        var cseq = bd.CurrSequence;
        var slist = cseq.Sequence.Sequence;
        if (bd.CurrSequenceIndex >= slist.Count
            || cseq.Sequence.StopCheck(bd.CurrSequenceIndex) >= 0)
        {
            Hi.AcrRuntimeDebug($"[SlotExec] 序列完成: {cseq.Sequence.GetType().Name} idx={bd.CurrSequenceIndex}/{slist.Count}");
            cseq.CompeltedAction?.Invoke();
            bd.ClearSequence();
            bd.CurrSequenceIndex = 0;
        }
        else
        {
            var slot = new Slot();
            slist[bd.CurrSequenceIndex]?.Invoke(slot);
            slot.InSequence = true;
            var stepName = slist[bd.CurrSequenceIndex]?.Method.Name ?? "?";
            bd.CurrSequenceIndex++;
            bd.SetCurrSlot(slot);
            Hi.AcrRuntimeDebug($"[SlotExec] 序列步进: {cseq.Sequence.GetType().Name}[{bd.CurrSequenceIndex - 1}] → {string.Join(",", slot.Actions.Select(a => a.Spell.Name))}");
        }
        return true;
    }

    public async Task ResolveSlots(BattleData bd, int mode, bool skipRotationResolvers = false)
    {
        var rot = _runner.CurrentRotation;
        if (mode == 1)
        {
            var highPrioritySlots = bd.HighPrioritySlots_GCD;
            if (highPrioritySlots.TryPeek(out var slot))
            {
                slot.InSequence = true;
                if (ShouldDiscardHighPrioritySlot(rot?.CanUseHighPrioritySlotCheck, slot))
                {
                    highPrioritySlots.TryDequeue(out _);
                    Hi.AcrRuntimeDebug($"[SlotExec] 丢弃高优 Slot: {slot.Source}");
                }
                else
                {
                    slot.BypassesRotationPause = true;
                    slot.HighPriorityQueueOwner = highPrioritySlots;
                    if (await RunSlot(bd, slot, false))
                        TryDequeueCompletedHighPrioritySlot(highPrioritySlots, slot);
                    return;
                }
            }
        }
        else
        {
            if (!AbilityThrottle.CanStartNow(Environment.TickCount64))
                return;

            var highPrioritySlots = bd.HighPrioritySlots_OffGCD;
            if (highPrioritySlots.TryPeek(out var slot))
            {
                slot.InSequence = true;
                if (ShouldDiscardHighPrioritySlot(rot?.CanUseHighPrioritySlotCheck, slot))
                {
                    highPrioritySlots.TryDequeue(out _);
                    Hi.AcrRuntimeDebug($"[SlotExec] 丢弃高优 Slot: {slot.Source}");
                }
                else
                {
                    slot.BypassesRotationPause = true;
                    slot.HighPriorityQueueOwner = highPrioritySlots;
                    if (await RunSlot(bd, slot, false))
                        TryDequeueCompletedHighPrioritySlot(highPrioritySlots, slot);
                    return;
                }
            }
        }

        // 轴请求停手时：只放行轴强制的 HighPrioritySlot（上方已处理），跳过 rotation resolver
        if (_runner.IsAxisPaused || skipRotationResolvers) return;

        if (rot?.SlotResolvers == null || rot.SlotResolvers.Count == 0)
            return;

        foreach (var item in rot.SlotResolvers)
        {
            bool match = mode switch
            {
                1 => item.Mode is SlotMode.Gcd or SlotMode.Always,
                2 => item.Mode is SlotMode.OffGcd or SlotMode.Always,
                _ => false
            };
            if (!match) continue;

            if (await CheckNext(bd, item.Resolver))
                return;
        }
    }

    private async Task<bool> CheckNext(BattleData bd, ISlotResolver resolver)
    {
        if (resolver.Check() < 0) return false;

        var slot = new Slot();
        resolver.Build(slot);

        if (TryScheduleBeforeSpell(bd, slot)) return true;

        return await RunSlot(bd, slot, false);
    }

    public async Task<bool> RunSlot(BattleData bd, Slot slot, bool isNextSlot = false)
    {
        if (TryScheduleBeforeSpell(bd, slot)) return false;

        if (slot.Actions.Count == 0)
        {
            CompleteSlot(bd, slot);
            return true;
        }

        if (slot.breakTime == 0L)
            slot.breakTime = Environment.TickCount64 + slot.MaxDuration;

        if (!TryReserveAbilityWindow(slot))
            return false;

        _runner.Debug.SlotSource = slot.Source;
        _runner.Debug.InSeq = slot.InSequence;
        Hi.AcrRuntimeDebug($"[SlotExec] 开始 [{slot.Source}] InSeq={slot.InSequence} Actions={string.Join(",", slot.Actions.Select(a => $"{a.Spell.Name}({a.Spell.Id})"))}");

        while (slot.Actions.Count > 0)
        {
            var gate = CanUseActionBeforeAction();
            if (gate == SlotActionGateOutcome.StopAndDropSlot) return true;
            if (gate == SlotActionGateOutcome.BlockKeepSlot) return false;

            if (!isNextSlot && CheckNextSlot(bd)) return false;

            var action = slot.Actions[0];
            _runner.Debug.LastActionName = action.Spell.Name;
            Hi.AcrRuntimeDebug($"[SlotExec] [{slot.Source}] 执行: {action.Spell.Name}({action.Spell.Id}) Wait={action.Wait}");
            _runner.EventHandler?.OnBeforeSpellCast(slot, action.Spell);

            if (slot.Actions.Count == 0) return true;

            action = slot.Actions[0];
            bool success = await ExecuteActionAsync(bd, action, slot);

            gate = CanUseActionAfterAction(success);
            if (gate == SlotActionGateOutcome.StopAndDropSlot) return true;
            if (gate == SlotActionGateOutcome.BlockKeepSlot) return false;

            if (slot.InSequence && !success
                && Environment.TickCount64 < slot.breakTime)
            {
                await Coroutine.Instance.WaitAsync(100);
                continue;
            }

            slot.Actions.RemoveAt(0);
            if (gate == SlotActionGateOutcome.BlockAfterConsume) return false;

            var remaining = slot.Actions.Count;
            slot.breakTime = Environment.TickCount64 + slot.MaxDuration;
            Hi.AcrRuntimeDebug($"[SlotExec] [{slot.Source}] 消耗, 剩余动作={remaining}");
            if (slot.Actions.Count == 0)
            {
                Hi.AcrRuntimeDebug($"[SlotExec] [{slot.Source}] 完成");
                // BHMRV7nsmf 后处理
                if (slot.Wait2NextGcd)
                {
                    Hi.AcrRuntimeDebug($"[SlotExec] [{slot.Source}] 延后到下个GCD");
                }
                else if (slot.AppendedSequence != null)
                {
                    Hi.AcrRuntimeDebug($"[SlotExec] [{slot.Source}] 追加序列");
                }
                CompleteSlot(bd, slot);
                return true;
            }
        }
        return true;
    }

    private bool TryScheduleBeforeSpell(BattleData bd, Slot slot)
    {
        if (!slot.TryMarkBeforeSpellHandled()) return false;

        var beforeSlot = _runner.EventHandler?.BeforeSpell(slot);
        if (!ShouldScheduleBeforeSpell(beforeSlot, slot)) return false;

        var scheduledSlot = beforeSlot!;
        if (slot.BypassesRotationPause)
            scheduledSlot.BypassesRotationPause = true;
        if (!ReferenceEquals(bd.CurrSlot, slot))
            bd.SetCurrSlot(slot);
        bd.SetCurrSlot(scheduledSlot);
        return true;
    }

    internal static void CompleteSlot(BattleData bd, Slot slot)
    {
        if (slot.Wait2NextGcd)
        {
            bd.PushWaitGcdSlot(slot);
            return;
        }

        if (slot.AppendedSequence != null)
        {
            bd.PushSequence(new SequenceWrapper
            {
                Sequence = slot.AppendedSequence,
                CompeltedAction = slot.CompletedAction
            });
            return;
        }

        slot.CompletedAction?.Invoke();
    }

    private async Task<bool> ExecuteActionAsync(BattleData bd, SlotAction action, Slot slot)
    {
        var spell = action.Spell;

        if (spell.Id == 0 || spell == Spell.Idle)
        {
            await Coroutine.Instance.WaitAsync(100);
            return true;
        }

        Hi.AcrRuntimeDebug($"[Action] 施法: {spell.Name}({spell.Id}) 类型={spell.Type} IsAbility={spell.IsAbility()}");

        if (!spell.IsAbility())
        {
            float gcdCD = GCDHelper.GetGCDCooldown();
            if (gcdCD > PluginConfig.Instance.ActionQueueInMs)
                await Coroutine.Instance.WaitAsync((long)(gcdCD - PluginConfig.Instance.ActionQueueInMs));
        }

        switch (action.Wait)
        {
            case WaitType.WaitInMs when action.TimeInMs > 0:
                await Coroutine.Instance.WaitAsync(action.TimeInMs);
                break;
            case WaitType.WaitForSndHalfWindow:
                while (GCDHelper.GetGCDCooldown() >= 1000)
                    await Coroutine.Instance.WaitAsync(1);
                break;
        }

        bool result = await SpellCast.ExecuteAsync(slot, spell, _runner);

        if (result)
        {
            Hi.AcrRuntimeDebug($"[Action] 成功: {spell.Name}({spell.Id}) Ability={spell.IsAbility()} bdAbil={bd.AbilityCount}");
            if (spell.IsAbility())
            {
                bd.AbilityCount++;
                Data.Combat.AbilityCountInGcd++;
                AbilityThrottle.MarkSuccess(Environment.TickCount64);
                if (spell.GcdGuardMs > 0)
                {
                    bd.GcdGuardUntil = Math.Max(bd.GcdGuardUntil, Environment.TickCount64 + spell.GcdGuardMs);
                    Hi.AcrRuntimeDebug($"[Action] GCD保护窗: {spell.Name}({spell.Id}) {spell.GcdGuardMs}ms");
                }
            }
            else
            {
                bd.AbilityCount = 0;
                bd.CurrGcdAbilityCount = PluginConfig.Instance.MaxAbilityTimesInGcd;
                Data.Combat.AbilityCountInGcd = 0;
                Data.Combat.LastGCDCompleted = Environment.TickCount64;
                bd.GcdGuardUntil = 0;
            }
            _runner.EventHandler?.AfterSpell(slot, spell);
        }
        else
        {
            Hi.AcrRuntimeDebug($"[Action] 失败: {spell.Name}({spell.Id})");
        }
        return result;
    }

    private static SlotActionGateOutcome CanUseActionBeforeAction()
    {
        return EvaluatePreActionGateForTests(
            isDead: Data.Me.Object is { IsDead: true },
            isPaused: MainControlHelper.IsPaused);
    }

    private static SlotActionGateOutcome CanUseActionAfterAction(bool actionSucceeded)
    {
        return EvaluatePostActionGateForTests(
            isDead: Data.Me.Object is { IsDead: true },
            isPaused: MainControlHelper.IsPaused,
            actionSucceeded: actionSucceeded);
    }

    private static bool TryReserveAbilityWindow(Slot slot)
    {
        if (slot.Actions.Count == 0 || !slot.Actions[0].Spell.IsAbility())
            return true;
        if (slot.AbilityThrottleReserved)
            return true;

        if (!AbilityThrottle.TryReserveNow(Environment.TickCount64, slot.Actions[0].Spell.Id))
            return false;

        slot.AbilityThrottleReserved = true;
        return true;
    }
}

internal enum SlotActionGateOutcome
{
    Allow,
    BlockKeepSlot,
    BlockAfterConsume,
    StopAndDropSlot
}
