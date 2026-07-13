using HiAuRo.ACR;
using HiAuRo.Execution;
using HiAuRo.FactAxis;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    internal static bool IsRotationPauseRequested(Func<int>? check) => check != null && check() >= 1;
    internal static bool ShouldHandleCurrentSlotDuringRotationPause(bool rotationPaused, Slot? slot) => !rotationPaused || slot?.BypassesRotationPause == true;

    internal static bool ShouldFinalizeSlotGeneration(
        long taskGeneration,
        long currentGeneration,
        bool cancellationRequested) =>
        taskGeneration == currentGeneration && !cancellationRequested;

    private async Task RunCalSlotAsync(long generation, CancellationToken ct, Task startSignal)
    {
        await startSignal;
        var bd = BattleData;
        bd.SlotState = true;
        try
        {
            ct.ThrowIfCancellationRequested();
            PushCombatSources(ct);
            await 执行核心决策(bd, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[CalSlot] {ex}");
        }
        finally
        {
            if (ShouldFinalizeSlotGeneration(generation, _slotGeneration, ct.IsCancellationRequested))
            {
                try
                {
                    CaptureDebug(bd, ct);
                }
                catch (OperationCanceledException)
                {
                }

                if (ShouldFinalizeSlotGeneration(generation, _slotGeneration, ct.IsCancellationRequested))
                    bd.SlotState = false;
            }
        }
    }

    private void CaptureDebug(BattleData bd, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Debug.SlotState = bd.SlotState;
        Debug.HighPriGcd = bd.HighPrioritySlots_GCD.Count;
        Debug.HighPriOgcd = bd.HighPrioritySlots_OffGCD.Count;
        Debug.AbilityCount = bd.AbilityCount;
        Debug.CurrGcdAbilityCount = bd.CurrGcdAbilityCount;
        Debug.GcdRemain = GCDHelper.GetGCDCooldown();
        Debug.CanGcd = GCDHelper.CanUseGCD();
        Debug.CanOgcd = GCDHelper.CanUseOffGcd();
        Debug.HasNextSlot = bd.NextSlot != null;
        Debug.HasWaitGcdSlot = bd.WaitGcdSlot != null;
        Debug.HasCurrSlot = bd.CurrSlot != null;
        Debug.CurrSeqName = bd.CurrSequence?.Sequence.GetType().Name;
        CollectResolverDebug(ct);
    }

    private void CollectResolverDebug(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rotation = CurrentRotation;
        if (rotation?.SlotResolvers == null) return;
        Debug.Resolvers.Clear();
        foreach (var rd in rotation.SlotResolvers)
        {
            ct.ThrowIfCancellationRequested();
            int cr;
            try { cr = rd.Resolver.Check(); }
            catch { cr = -99; }
            ct.ThrowIfCancellationRequested();
            Debug.Resolvers.Add(new ResolverInfo
            {
                Name = rd.Resolver.GetType().Name,
                Mode = rd.Mode.ToString(),
                CheckResult = cr,
                PassedWindow = cr >= 0
            });
        }
    }

    private async Task 执行核心决策(BattleData bd, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rotationPaused = IsRotationPauseRequested(CurrentRotation?.CanPauseACRCheck);
        ct.ThrowIfCancellationRequested();

        if (SpellQueue.HasPending())
        {
            var peekSlot = SpellQueue.PeekNext();
            if (peekSlot != null)
            {
                if (peekSlot.Actions.Count > 0
                    && peekSlot.Actions[0].Spell.IsAbility()
                    && !AbilityThrottle.CanStartNow(Environment.TickCount64))
                    goto AfterSpellQueue;

                var sqSlot = SpellQueue.DequeueNext();
                if (sqSlot == null) goto AfterSpellQueue;
                sqSlot.Source = "SpellQueue";
                sqSlot.BypassesRotationPause = true;
                bd.SetCurrSlot(sqSlot);
                if (await _slotExecutor.HandleSlot(bd, rotationPaused, ct)) { Debug.Phase = "SpellQueue"; return; }
            }
        }

    AfterSpellQueue:
        if (_slotExecutor.CheckNextSlot(bd, ct)) { Debug.Phase = "NextSlot"; return; }

        Slot? currentSlot = bd.CurrSlot;
        if (currentSlot == null && bd.CurrSlotStack.TryPeek(out var stackedSlot))
            currentSlot = stackedSlot;
        if ((ShouldHandleCurrentSlotDuringRotationPause(rotationPaused, currentSlot)
             || ShouldHandleCurrentSlotDuringRotationPause(rotationPaused, bd.WaitGcdSlot))
            && await _slotExecutor.HandleSlot(bd, rotationPaused, ct))
        {
            Debug.Phase = "HandleSlot";
            return;
        }

        if (!rotationPaused && !Data.Combat.InCombat && !CountDownHandler.Instance.Start && !CountDownHandler.Instance.CanDoAction
            && CurrentRotation?.EventHandler != null)
        {
            Debug.Phase = "OnPreCombat";
            ct.ThrowIfCancellationRequested();
            CurrentRotation.EventHandler.OnPreCombat();
            ct.ThrowIfCancellationRequested();
        }

        bool inFight = Data.Combat.InCombat;
        if (!inFight && Data.Target.Current is IBattleChara t && t.StatusFlags.HasFlag(Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat))
            inFight = true;
        if (CountDownHandler.Instance.Start && !CountDownHandler.Instance.CanDoAction) inFight = false;
        if (!inFight && CountDownHandler.Instance.CanDoAction) inFight = true;
        if (!inFight) { Debug.Phase = "Gate"; return; }

        var target = Data.Target.Current;
        if (target is not IBattleChara bc || !bc.IsTargetable)
        {
            if (Data.Combat.InCombat)
            {
                Debug.Phase = "NoTarget";
                ct.ThrowIfCancellationRequested();
                CurrentRotation?.EventHandler?.OnNoTarget();
                ct.ThrowIfCancellationRequested();
            }
            return;
        }

        if (!rotationPaused && await _slotExecutor.HandleSlotSequence(bd, ct)) { Debug.Phase = "SlotSeq"; return; }

        if (GCDHelper.CanUseGCD())
        {
            if (Environment.TickCount64 < bd.GcdGuardUntil)
            {
                Debug.Phase = "GcdGuard";
                return;
            }

            Debug.Phase = "GCD";
            await _slotExecutor.ResolveSlots(bd, 1, rotationPaused, ct);
        }
        else if (bd.AbilityCount < bd.CurrGcdAbilityCount)
        {
            Debug.Phase = "OGCD";
            await _slotExecutor.ResolveSlots(bd, 2, rotationPaused, ct);
        }
        else
            Debug.Phase = "Wait";
    }

    private ExecutionOutput? _execOutput;
    private ExecutionOutput? _assistOutput;

    /// <summary>执行轴/辅助轴是否请求暂停 ACR 输出（每帧由轴 output 的 PauseAcr 设置；ResumeAcr 即停止设置）</summary>
    internal bool IsAxisPaused => _execOutput?.PauseAcr == true || _assistOutput?.PauseAcr == true;

    private void PushCombatSources(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Data.Combat.InCombat) return;

        // _execOutput/_assistOutput 已在 Refresh() 中产出（脱离 ConditionFlag 门控）
        try
        {
            UpdateFactAxis();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[PushCombatSources] {ex}");
        }

        ct.ThrowIfCancellationRequested();
        PushAxisSpell(_execOutput?.ForceSpell, "ExecAxis", ct);
        PushAxisSpell(_assistOutput?.ForceSpell, "AssistAxis", ct);
    }

    private void PushAxisSpell(Spell? spell, string source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (spell == null) return;
        var slot = new Slot { Source = source };
        slot.Add(spell);
        var canUseHighPrioritySlotCheck = CurrentRotation?.CanUseHighPrioritySlotCheck;
        if (canUseHighPrioritySlotCheck != null)
        {
            var check = canUseHighPrioritySlotCheck(slot);
            ct.ThrowIfCancellationRequested();
            if (check < 0) return;
        }

        if (!spell.IsAbility())
            BattleData.HighPrioritySlots_GCD.Enqueue(slot);
        else
            BattleData.HighPrioritySlots_OffGCD.Enqueue(slot);
    }
}
