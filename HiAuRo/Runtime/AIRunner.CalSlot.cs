using HiAuRo.ACR;
using HiAuRo.Execution;
using HiAuRo.FactAxis;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    internal static bool IsRotationPauseRequested(Func<int>? check) => check != null && check() >= 1;

    internal async Task CalSlotAsync()
    {
        var bd = BattleData;
        if (bd.SlotState) return;

        bd.SlotState = true;
        try
        {
            PushCombatSources();
            await 执行核心决策(bd);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[CalSlot] {ex}");
        }
        finally
        {
            CaptureDebug(bd);
            bd.SlotState = false;
        }
    }

    private void CaptureDebug(BattleData bd)
    {
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
        CollectResolverDebug();
    }

    private void CollectResolverDebug()
    {
        if (CurrentRotation?.SlotResolvers == null) return;
        Debug.Resolvers.Clear();
        foreach (var rd in CurrentRotation.SlotResolvers)
        {
            int cr;
            try { cr = rd.Resolver.Check(); }
            catch { cr = -99; }
            Debug.Resolvers.Add(new ResolverInfo
            {
                Name = rd.Resolver.GetType().Name,
                Mode = rd.Mode.ToString(),
                CheckResult = cr,
                PassedWindow = cr >= 0
            });
        }
    }

    private async Task 执行核心决策(BattleData bd)
    {
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
                bd.SetCurrSlot(sqSlot);
                if (await _slotExecutor.HandleSlot(bd)) { Debug.Phase = "SpellQueue"; return; }
            }
        }

    AfterSpellQueue:
        if (_slotExecutor.CheckNextSlot(bd)) { Debug.Phase = "NextSlot"; return; }

        if (await _slotExecutor.HandleSlot(bd)) { Debug.Phase = "HandleSlot"; return; }

        var rotationPaused = IsRotationPauseRequested(CurrentRotation?.CanPauseACRCheck);

        if (!rotationPaused && !Data.Combat.InCombat && !CountDownHandler.Instance.Start && !CountDownHandler.Instance.CanDoAction
            && CurrentRotation?.EventHandler != null)
        {
            Debug.Phase = "OnPreCombat";
            CurrentRotation.EventHandler.OnPreCombat();
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
                CurrentRotation?.EventHandler?.OnNoTarget();
            }
            return;
        }

        if (!rotationPaused && await _slotExecutor.HandleSlotSequence(bd)) { Debug.Phase = "SlotSeq"; return; }

        if (GCDHelper.CanUseGCD())
        {
            if (Environment.TickCount64 < bd.GcdGuardUntil)
            {
                Debug.Phase = "GcdGuard";
                return;
            }

            Debug.Phase = "GCD";
            await _slotExecutor.ResolveSlots(bd, 1, rotationPaused);
        }
        else if (bd.AbilityCount < bd.CurrGcdAbilityCount)
        {
            Debug.Phase = "OGCD";
            await _slotExecutor.ResolveSlots(bd, 2, rotationPaused);
        }
        else
            Debug.Phase = "Wait";
    }

    private ExecutionOutput? _execOutput;
    private ExecutionOutput? _assistOutput;

    /// <summary>执行轴/辅助轴是否请求暂停 ACR 输出（每帧由轴 output 的 PauseAcr 设置；ResumeAcr 即停止设置）</summary>
    internal bool IsAxisPaused => _execOutput?.PauseAcr == true || _assistOutput?.PauseAcr == true;

    private void PushCombatSources()
    {
        if (!Data.Combat.InCombat) return;

        // _execOutput/_assistOutput 已在 Refresh() 中产出（脱离 ConditionFlag 门控）
        try
        {
            UpdateFactAxis();
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[PushCombatSources] {ex}");
        }

        PushAxisSpell(_execOutput?.ForceSpell, "ExecAxis");
        PushAxisSpell(_assistOutput?.ForceSpell, "AssistAxis");
    }

    private void PushAxisSpell(Spell? spell, string source)
    {
        if (spell == null) return;
        var slot = new Slot { Source = source };
        slot.Add(spell);
        if (CurrentRotation?.CanUseHighPrioritySlotCheck?.Invoke(slot) < 0) return;

        if (!spell.IsAbility())
            BattleData.HighPrioritySlots_GCD.Enqueue(slot);
        else
            BattleData.HighPrioritySlots_OffGCD.Enqueue(slot);
    }
}
