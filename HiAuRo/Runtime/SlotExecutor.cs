using HiAuRo.ACR;
using OmenTools.OmenService;
using ActionTypeFF = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace HiAuRo.Runtime;

/// <summary>
/// Slot 执行引擎 —— 跨帧状态机，逐个 action 等待释放条件后执行
/// </summary>
public sealed class SlotExecutor
{
    private readonly AIRunner _runner;

    // === 状态机 ===
    private Slot? _currentSlot;
    private long _slotBreakTime;
    private long _gcdJustUsedTimestamp; // GCD 释放后等游戏确认 GCD 已生效

    /// <summary>等待恢复执行的 Slot 队列（BeforeSpell 插入时保存当前 Slot）</summary>
    private readonly Stack<Slot> _pendingSlots = new();

    /// <summary>当前是否有 Slot 正在执行</summary>
    public bool IsExecuting => _currentSlot != null && _currentSlot.Actions.Count > 0;

    /// <summary>等待 AfterSpell 确认的技能</summary>
    private (Slot slot, Spell spell)? _pendingAfterSpell;

    public SlotExecutor(AIRunner runner)
    {
        _runner = runner;
    }

    /// <summary>开始执行一个 Slot（跨帧模式）。所有 Slot 统一走这个路径。</summary>
    public void StartSlot(Slot slot, bool skipBeforeSpell = false)
    {
        if (!skipBeforeSpell)
        {
            var insertSlot = _runner.EventHandler?.BeforeSpell(slot);
            if (insertSlot != null)
            {
                _pendingSlots.Push(slot);
                DService.Instance().Log.Debug($"[SlotExec] BeforeSpell 插入 Slot, 原 Slot 入队");
                StartSlot(insertSlot);
                return;
            }
        }

        _currentSlot = slot;
        _slotBreakTime = Environment.TickCount64 + slot.MaxDuration;
    }

    /// <summary>每帧调用一次。返回 true 表示 Slot 完成。</summary>
    public bool ExecuteStep()
    {
        if (_currentSlot == null || _currentSlot.Actions.Count == 0)
        {
            _currentSlot = null;
            return true;
        }

        var spell = _currentSlot.Actions[0].Spell;

        // GCD 技能：等待 GCD 就绪
        if (!spell.IsAbility())
        {
            // 刚释放过 GCD，等待游戏确认 GCD 计时器已激活（IsActive 从 false 变 true）
            if (_gcdJustUsedTimestamp > 0)
            {
                if (GCDHelper.IsGCDActive())
                    _gcdJustUsedTimestamp = 0; // GCD 已生效，放行到正常就绪检查
                else if (Environment.TickCount64 - _gcdJustUsedTimestamp < 500)
                {
                    _slotBreakTime = Environment.TickCount64 + _currentSlot.MaxDuration;
                    return false;
                }
                _gcdJustUsedTimestamp = 0; // 超时保护，放行
            }

            if (!GCDHelper.IsGCDReady())
            {
                _slotBreakTime = Environment.TickCount64 + _currentSlot.MaxDuration;
                return false;
            }
        }

        // 能力技：等待间隔就绪 + 动画锁释放
        if (spell.IsAbility() && (!Data.Combat.AbilityIntervalElapsed || !GCDHelper.CanUseOffGcd()))
        {
            _slotBreakTime = Environment.TickCount64 + _currentSlot.MaxDuration;
            return false;
        }

        // 超时 → 跳过（仅在条件满足后 UseAction 反复失败时生效）
        if (Environment.TickCount64 >= _slotBreakTime)
        {
            DService.Instance().Log.Debug($"[SlotExec] 超时跳过: {spell.Name}");
            _currentSlot.Actions.RemoveAt(0);

            if (_currentSlot.Actions.Count == 0)
            {
                FinishSlot();
                return true;
            }

            _slotBreakTime = Environment.TickCount64 + _currentSlot.MaxDuration;
            return false;
        }

        // 释放条件满足，尝试 UseAction
        var targetId = ResolveTarget(spell);
        var targetName = GetTargetNameById(targetId);
        var actionType = SpellCategoryToActionType(spell.SpellCategory);
        var useLocation = spell.UsePos.HasValue || spell.TargetType == SpellTargetType.Location;

        DService.Instance().Log.Debug($"[SlotExec] UseAction: {spell.Name}({spell.Id}) TargetType={spell.TargetType} TargetId={targetId:X}({targetName}) ActionType={actionType}");

        // GCD 技能：提前挂起，确保 OnPostUseAction（同步触发）能匹配
        if (!spell.IsAbility())
            _pendingAfterSpell = (_currentSlot, spell);

        var useResult = useLocation && spell.UsePos.HasValue
            ? UseActionManager.Instance().UseActionLocation(actionType, spell.Id, location: spell.UsePos.Value)
            : UseActionManager.Instance().UseAction(actionType, spell.Id, targetId, 0, 0, 0);
        DService.Instance().Log.Debug($"[SlotExec] UseAction result={useResult}");

        if (useResult)
        {
            EventSystem.OnUseActionSuccess(spell.Id, spell.Type);

            if (spell.IsAbility())
            {
                _runner.EventHandler?.OnSpellCastSuccess(_currentSlot, spell);
                _runner.EventHandler?.AfterSpell(_currentSlot, spell);
                Data.Combat.LastAbilityUseTime = Environment.TickCount64;
            }
            else
            {
                _gcdJustUsedTimestamp = Environment.TickCount64;
            }

            // 移除已完成的 action
            _currentSlot.Actions.RemoveAt(0);

            if (_currentSlot.Actions.Count == 0)
            {
                FinishSlot();
                return true;
            }

            _slotBreakTime = Environment.TickCount64 + _currentSlot.MaxDuration;
            return false;
        }

        // UseAction 失败，清除挂起（下帧重试）
        _pendingAfterSpell = null;
        return false;
    }

    /// <summary>Slot 完成后处理追加序列</summary>
    private void FinishSlot()
    {
        var slot = _currentSlot;
        _currentSlot = null;

        if (slot?.AppendedSequence != null && slot.AppendedSequence.StartCheck() >= 0)
        {
            var seqSlot = new Slot();
            foreach (var action in slot.AppendedSequence.Sequence)
                action(seqSlot);
            if (seqSlot.Actions.Count > 0)
            {
                DService.Instance().Log.Debug($"[SlotExec] 追加序列, 入队 {seqSlot.Actions.Count}个技能");
                _runner.SpellQueue.Enqueue(seqSlot);
            }
        }

        // 恢复被 BeforeSpell 插入推迟的 Slot
        if (_pendingSlots.Count > 0)
        {
            var pendingSlot = _pendingSlots.Pop();
            DService.Instance().Log.Debug($"[SlotExec] 恢复被推迟的 Slot, Actions={pendingSlot.Actions.Count}");
            StartSlot(pendingSlot, skipBeforeSpell: true);
        }
    }

    /// <summary>由 OnPostUseAction 调用，确认 GCD 技能释放后触发 AfterSpell</summary>
    internal void NotifySpellUsed(uint actionId)
    {
        if (_pendingAfterSpell.HasValue && _pendingAfterSpell.Value.spell.Id == actionId)
        {
            var (slot, spell) = _pendingAfterSpell.Value;
            _runner.EventHandler?.OnSpellCastSuccess(slot, spell);
            _runner.EventHandler?.AfterSpell(slot, spell);
            _pendingAfterSpell = null;
        }
    }

    private static string GetTargetNameById(ulong id)
    {
        if (id == 0) return "无目标";
        if (Data.Me.Object?.GameObjectID == id) return "自己";
        if (Data.Target.Current?.GameObjectID == id) return Data.Target.Current.Name.ToString();
        foreach (var pm in Data.Party.All)
        {
            if (pm.Player?.GameObjectID == id)
                return pm.Player.Name.ToString();
        }
        return "?";
    }

    private static ulong ResolveTarget(Spell spell)
    {
        switch (spell.TargetType)
        {
            case SpellTargetType.Self:
                return Data.Me.Object?.GameObjectID ?? 0;
            case SpellTargetType.Target:
                return Data.Target.Current?.GameObjectID ?? 0;
            case SpellTargetType.TargetTarget:
                if (Data.Target.Current is IBattleChara bc)
                    return bc.TargetObjectID;
                return 0;
            case SpellTargetType.SpecifyTarget:
                if (spell.SpecifyTarget is IGameObject go)
                    return go.GameObjectID;
                return 0;
            case SpellTargetType.DynamicTarget:
                if (spell.GetDynamicTarget?.Invoke() is IGameObject dgo)
                    return dgo.GameObjectID;
                return 0;
            case SpellTargetType.Pm1: case SpellTargetType.Pm2:
            case SpellTargetType.Pm3: case SpellTargetType.Pm4:
            case SpellTargetType.Pm5: case SpellTargetType.Pm6:
            case SpellTargetType.Pm7: case SpellTargetType.Pm8:
                var idx = (int)spell.TargetType - (int)SpellTargetType.Pm1;
                if (idx >= 0 && idx < Data.Party.All.Count)
                    return Data.Party.All[idx].Player?.GameObjectID ?? 0;
                return 0;
            default:
                return Data.Target.Current?.GameObjectID ?? 0;
        }
    }

    private static ActionTypeFF SpellCategoryToActionType(SpellCategory cat)
    {
        return cat switch
        {
            SpellCategory.Sprint => ActionTypeFF.GeneralAction,
            SpellCategory.Potion or SpellCategory.Item => ActionTypeFF.Item,
            SpellCategory.LimitBreak => ActionTypeFF.GeneralAction,
            _ => ActionTypeFF.Action
        };
    }
}
