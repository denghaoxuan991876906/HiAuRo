using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Runtime;

public sealed class SlotExecutor
{
    private readonly AIRunner _runner;

    public SlotExecutor(AIRunner runner) { _runner = runner; }

    public bool CheckNextSlot(BattleData bd)
    {
        if (bd.NextSlot == null) return false;
        bd.NextSlot.InSequence = true;
        bd.SetCurrSlot(bd.NextSlot);
        bd.NextSlot = null;
        return true;
    }

    public async Task<bool> HandleSlot(BattleData bd)
    {
        if (bd.WaitGcdSlot != null)
        {
            if (!GCDHelper.CanUseGCD()) return true;

            if (bd.WaitGcdSlot.AppendedSequence != null)
            {
                var wrapper = new SequenceWrapper
                {
                    Sequence = bd.WaitGcdSlot.AppendedSequence,
                    CompeltedAction = bd.WaitGcdSlot.CompletedAction
                };
                bd.PushSequence(wrapper);
            }
            else
            {
                bd.WaitGcdSlot.CompletedAction?.Invoke();
            }
            bd.WaitGcdSlot = null;
        }

        if (bd.CurrSlot == null && !bd.PopCurrSlot()) return false;

        if (bd.CurrSlot != null)
        {
            if (await RunSlot(bd, bd.CurrSlot, true))
                bd.SetCurrSlot(null);
            return true;
        }
        return false;
    }

    public async Task<bool> HandleSlotSequence(BattleData bd)
    {
        var rot = _runner.CurrentRotation;
        if (bd.CurrSequence == null)
        {
            if (bd.PopSequenceStack()) { DService.Instance().Log.Debug("[SlotExec] 恢复嵌套序列"); return true; }

            // 只在倒计时后才自动使用起手
            if (bd.CurrSequence == null && CountDownHandler.Instance.CanDoAction
                && await OpenerMgr.Instance.UseOpener(bd, rot))
            {
                DService.Instance().Log.Debug($"[SlotExec] 起手已推送: {bd.CurrSequence?.Sequence.GetType().Name}");
                return true;
            }

            if (bd.CurrSequence == null && rot?.SlotSequences is { Count: > 0 })
            {
                foreach (var seq in rot.SlotSequences)
                {
                    var sc = seq.StartCheck();
                    DService.Instance().Log.Debug($"[SlotExec] 检查序列: {seq.GetType().Name} StartCheck={sc}");
                    if (sc >= 0)
                    {
                        bd.PushSequence(new SequenceWrapper
                        {
                            Sequence = (ISlotSequence)Activator.CreateInstance(seq.GetType())!
                        });
                        DService.Instance().Log.Debug($"[SlotExec] 序列已启动: {seq.GetType().Name}");
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
            DService.Instance().Log.Debug($"[SlotExec] 序列完成: {cseq.Sequence.GetType().Name} idx={bd.CurrSequenceIndex}/{slist.Count}");
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
            DService.Instance().Log.Debug($"[SlotExec] 序列步进: {cseq.Sequence.GetType().Name}[{bd.CurrSequenceIndex - 1}] → {string.Join(",", slot.Actions.Select(a => a.Spell.Name))}");
        }
        return true;
    }

    public async Task ResolveSlots(BattleData bd, int mode)
    {
        var rot = _runner.CurrentRotation;
        if (mode == 1)
        {
            if (bd.HighPrioritySlots_GCD.Count > 0)
            {
                var slot = bd.HighPrioritySlots_GCD.Peek();
                slot.InSequence = true;
                if (rot?.CanUseHighPrioritySlotCheck == null
                    || rot.CanUseHighPrioritySlotCheck(slot) >= 0)
                {
                    if (await RunSlot(bd, slot, false))
                        bd.HighPrioritySlots_GCD.Dequeue();
                }
                return;
            }
        }
        else
        {
            if (!AbilityThrottle.CanStartNow(Environment.TickCount64))
                return;

            if (bd.HighPrioritySlots_OffGCD.Count > 0)
            {
                var slot = bd.HighPrioritySlots_OffGCD.Peek();
                slot.InSequence = true;
                if (rot?.CanUseHighPrioritySlotCheck == null
                    || rot.CanUseHighPrioritySlotCheck(slot) >= 0)
                {
                    if (await RunSlot(bd, slot, false))
                        bd.HighPrioritySlots_OffGCD.Dequeue();
                }
                return;
            }
        }

        // 轴请求停手时：只放行轴强制的 HighPrioritySlot（上方已处理），跳过 rotation resolver
        if (_runner.IsAxisPaused) return;

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

        if (slot.Actions.Count > 0
            && slot.Actions[0].Spell.IsAbility()
            && !slot.AbilityThrottleReserved)
        {
            if (!AbilityThrottle.TryReserveNow(Environment.TickCount64))
                return false;
            slot.AbilityThrottleReserved = true;
        }

        if (slot.Actions.Count > 0) return await RunSlot(bd, slot, false);

        if (slot.AppendedSequence != null)
        {
            var wrapper = new SequenceWrapper
            {
                Sequence = slot.AppendedSequence,
                CompeltedAction = slot.CompletedAction
            };
            bd.PushSequence(wrapper);
            return true;
        }
        return false;
    }

    public async Task<bool> RunSlot(BattleData bd, Slot slot, bool isNextSlot = false)
    {
        if (slot.breakTime == 0L)
            slot.breakTime = Environment.TickCount64 + slot.MaxDuration;

        if (!TryReserveAbilityWindow(slot))
            return false;

        _runner.Debug.SlotSource = slot.Source;
        _runner.Debug.InSeq = slot.InSequence;
        DService.Instance().Log.Debug($"[SlotExec] 开始 [{slot.Source}] InSeq={slot.InSequence} Actions={string.Join(",", slot.Actions.Select(a => $"{a.Spell.Name}({a.Spell.Id})"))}");

        while (slot.Actions.Count > 0)
        {
            if (!CanUseAction()) return true;

            if (!isNextSlot && CheckNextSlot(bd)) return false;

            var action = slot.Actions[0];
            _runner.Debug.LastActionName = action.Spell.Name;
            DService.Instance().Log.Debug($"[SlotExec] [{slot.Source}] 执行: {action.Spell.Name}({action.Spell.Id}) Wait={action.Wait}");
            _runner.EventHandler?.OnBeforeSpellCast(slot, action.Spell);

            if (slot.Actions.Count == 0) return true;

            action = slot.Actions[0];
            bool success = await ExecuteActionAsync(bd, action, slot);

            if (!CanUseAction()) return true;

            if (slot.InSequence && !success
                && Environment.TickCount64 < slot.breakTime)
            {
                await Coroutine.Instance.WaitAsync(100);
                continue;
            }

            slot.Actions.RemoveAt(0);
            var remaining = slot.Actions.Count;
            slot.breakTime = Environment.TickCount64 + slot.MaxDuration;
            DService.Instance().Log.Debug($"[SlotExec] [{slot.Source}] 消耗, 剩余动作={remaining}");
            if (slot.Actions.Count == 0)
            {
                DService.Instance().Log.Debug($"[SlotExec] [{slot.Source}] 完成");
                // BHMRV7nsmf 后处理
                if (slot.Wait2NextGcd)
                {
                    DService.Instance().Log.Debug($"[SlotExec] [{slot.Source}] 延后到下个GCD");
                    bd.WaitGcdSlot = slot;
                }
                else if (slot.AppendedSequence != null)
                {
                    var wrapper = new SequenceWrapper
                    {
                        Sequence = slot.AppendedSequence,
                        CompeltedAction = slot.CompletedAction
                    };
                    DService.Instance().Log.Debug($"[SlotExec] [{slot.Source}] 追加序列");
                    bd.PushSequence(wrapper);
                }
                else
                {
                    slot.CompletedAction?.Invoke();
                }
                return true;
            }
        }
        return true;
    }

    private async Task<bool> ExecuteActionAsync(BattleData bd, SlotAction action, Slot slot)
    {
        var spell = action.Spell;

        if (spell.Id == 0 || spell == Spell.Idle)
        {
            await Coroutine.Instance.WaitAsync(100);
            return true;
        }

        DService.Instance().Log.Debug($"[Action] 施法: {spell.Name}({spell.Id}) 类型={spell.Type} IsAbility={spell.IsAbility()}");

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
            DService.Instance().Log.Debug($"[Action] 成功: {spell.Name}({spell.Id}) Ability={spell.IsAbility()} bdAbil={bd.AbilityCount}");
            if (spell.IsAbility())
            {
                bd.AbilityCount++;
                Data.Combat.AbilityCountInGcd++;
                AbilityThrottle.MarkSuccess(Environment.TickCount64);
            }
            else
            {
                bd.AbilityCount = 0;
                bd.CurrGcdAbilityCount = PluginConfig.Instance.MaxAbilityTimesInGcd;
                Data.Combat.AbilityCountInGcd = 0;
                Data.Combat.LastGCDCompleted = Environment.TickCount64;
            }
            _runner.EventHandler?.AfterSpell(slot, spell);
        }
        else
        {
            DService.Instance().Log.Debug($"[Action] 失败: {spell.Name}({spell.Id})");
        }
        return result;
    }

    private static bool CanUseAction()
    {
        if (Data.Me.Object is { IsDead: true }) return false;
        if (MainControlHelper.IsPaused) return false;
        return true;
    }

    private static bool TryReserveAbilityWindow(Slot slot)
    {
        if (slot.Actions.Count == 0 || !slot.Actions[0].Spell.IsAbility())
            return true;
        if (slot.AbilityThrottleReserved)
            return true;

        if (!AbilityThrottle.TryReserveNow(Environment.TickCount64))
            return false;

        slot.AbilityThrottleReserved = true;
        return true;
    }
}
