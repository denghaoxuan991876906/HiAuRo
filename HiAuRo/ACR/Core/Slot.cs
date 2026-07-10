using System.Collections.Concurrent;
using HiAuRo.Runtime;

namespace HiAuRo.ACR;

/// <summary>
/// 技能执行单元 —— 包含连续多个 SlotAction，按顺序执行
/// </summary>
public sealed class Slot
{
    /// <summary>要按顺序执行的技能列表</summary>
    public List<SlotAction> Actions { get; } = [];

    /// <summary>整体失败尝试时间 (ms)，默认 600</summary>
    public int MaxDuration { get; set; } = 600;

    /// <summary>Slot 来源标识（调试用）</summary>
    public string Source { get; set; } = "";

    /// <summary>是否序列内（失败后 100ms 重试，不立即跳过）</summary>
    public bool InSequence { get; set; }

    /// <summary>Slot 完成后回调</summary>
    public Action? CompletedAction { get; set; }

    /// <summary>强制延后到下个 GCD</summary>
    public bool Wait2NextGcd { get; set; }

    /// <summary>Slot 执行过程中的超时时间戳（由 executor 设置）</summary>
    internal long breakTime;

    /// <summary>是否已占用本次能力技节流窗口</summary>
    internal bool AbilityThrottleReserved { get; set; }

    internal bool BeforeSpellHandled { get; private set; }

    internal bool BypassesRotationPause { get; set; }

    internal ConcurrentQueue<Slot>? HighPriorityQueueOwner { get; set; }

    internal bool TryMarkBeforeSpellHandled()
    {
        if (BeforeSpellHandled) return false;
        BeforeSpellHandled = true;
        return true;
    }

    /// <summary>Slot 快结束时追加的序列</summary>
    public ISlotSequence? AppendedSequence { get; private set; }

    /// <summary>包装后的追加序列（带回调）</summary>
    internal SequenceWrapper? AppendedSequenceData { get; set; }

    // === 构造函数 ===
    public Slot() { }

    public Slot(int maxDuration)
    {
        MaxDuration = maxDuration;
    }

    public Slot(Spell spell, int maxDuration = 600)
    {
        MaxDuration = maxDuration;
        Add(spell);
    }

    // === 添加方法（fluent 链式，返回 Slot） ===

    /// <summary>添加一个技能</summary>
    public Slot Add(Spell spell, bool waitServerAcq = false, int gcdGuardMs = 0)
    {
        if (waitServerAcq || gcdGuardMs > 0)
        {
            spell = spell.WithExecutionOptions(
                waitServerAcq: waitServerAcq ? true : null,
                gcdGuardMs: gcdGuardMs > 0 ? gcdGuardMs : null);
        }

        Actions.Add(new SlotAction { Spell = spell });
        return this;
    }

    /// <summary>添加一个已配置的 SlotAction</summary>
    public Slot Add(SlotAction action)
    {
        Actions.Add(action);
        return this;
    }

    /// <summary>在指定位置插入（默认队首）</summary>
    public Slot Insert(SlotAction action, int index = 0)
    {
        Actions.Insert(Math.Clamp(index, 0, Actions.Count), action);
        return this;
    }

    /// <summary>在第二个能力技窗口添加</summary>
    public Slot Add2NdWindowAbility(Spell spell)
    {
        Actions.Add(new SlotAction
        {
            Spell = spell,
            Wait = WaitType.WaitForSndHalfWindow
        });
        return this;
    }

    /// <summary>延迟后添加技能</summary>
    public Slot AddDelaySpell(int delayMs, Spell spell)
    {
        Actions.Add(new SlotAction
        {
            Spell = spell,
            Wait = WaitType.WaitInMs,
            TimeInMs = delayMs
        });
        return this;
    }

    /// <summary>追加序列（可指定是否强制等待下个 GCD）</summary>
    public void AppendSequence(ISlotSequence? sequence, bool wait2NextGcd = true)
    {
        AppendedSequence = sequence;
        if (wait2NextGcd) Wait2NextGcd = true;
    }

    // === 调试 ===
    public override string ToString()
    {
        var spells = string.Join(" → ", Actions.Select(a => a.Spell.Name));
        return $"Slot[{MaxDuration}ms]: {spells}";
    }
}
