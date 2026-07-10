using System.Collections.Concurrent;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public sealed class BattleData
{
    public ConcurrentQueue<Slot> HighPrioritySlots_GCD { get; private set; } = new();
    public ConcurrentQueue<Slot> HighPrioritySlots_OffGCD { get; private set; } = new();
    public Slot? NextSlot { get; set; }
    public Slot? WaitGcdSlot { get; set; }
    public Slot? CurrSlot { get; private set; }
    public SequenceWrapper? CurrSequence { get; private set; }
    public int CurrSequenceIndex { get; set; }
    public Stack<SequenceWrapper> CurrSequenceStack { get; } = new();
    public Stack<int> CurrSequenceIndexStack { get; } = new();
    public Stack<Slot> CurrSlotStack { get; } = new();
    public Stack<Slot> WaitGcdSlotStack { get; } = new();
    public int AbilityCount { get; set; }
    public int CurrGcdAbilityCount { get; set; } = 2;
    public bool UsedOpener { get; set; }
    public bool SlotState { get; set; }
    public bool TargetCleared { get; set; }
    public bool IsDead { get; set; }
    public long GcdGuardUntil { get; set; }

    public void AddSpell2NextSlot(Spell spell)
    {
        NextSlot ??= new Slot();
        NextSlot.Add(spell);
    }

    public void SetCurrSlot(Slot? slot)
    {
        if (slot == null) { CurrSlot = null; return; }
        if (CurrSlot != null) CurrSlotStack.Push(CurrSlot);
        CurrSlot = slot;
    }

    public bool PopCurrSlot()
    {
        if (CurrSlotStack.Count == 0) return false;
        CurrSlot = CurrSlotStack.Pop();
        return true;
    }

    public void PushWaitGcdSlot(Slot slot)
    {
        if (WaitGcdSlot != null && !ReferenceEquals(WaitGcdSlot, slot))
            WaitGcdSlotStack.Push(WaitGcdSlot);
        WaitGcdSlot = slot;
    }

    public void PopWaitGcdSlot()
    {
        WaitGcdSlot = WaitGcdSlotStack.Count > 0 ? WaitGcdSlotStack.Pop() : null;
    }

    public void PushSequence(SequenceWrapper wrapper)
    {
        if (wrapper == null) { CurrSequence = null; return; }
        if (CurrSequence != null)
        {
            CurrSequenceStack.Push(CurrSequence);
            CurrSequenceIndexStack.Push(CurrSequenceIndex);
        }
        CurrSequence = wrapper;
        CurrSequenceIndex = 0;
    }

    public void PushSequence(ISlotSequence seq)
    {
        PushSequence(new SequenceWrapper { Sequence = seq });
    }

    public bool PopSequenceStack()
    {
        if (CurrSequenceStack.Count == 0) return false;
        CurrSequence = CurrSequenceStack.Pop();
        CurrSequenceIndex = CurrSequenceIndexStack.Pop();
        return true;
    }

    public void ClearSequence()
    {
        CurrSequence = null;
        CurrSequenceIndex = 0;
    }

    public void Reset()
    {
        SlotState = false;
        ClearSequence();
        HighPrioritySlots_GCD = new();
        HighPrioritySlots_OffGCD = new();
        NextSlot = null;
        WaitGcdSlot = null;
        WaitGcdSlotStack.Clear();
        CurrSlot = null;
        CurrSequenceIndex = 0;
        CurrSequenceStack.Clear();
        CurrSequenceIndexStack.Clear();
        CurrSlotStack.Clear();
        UsedOpener = false;
        AbilityCount = 0;
        IsDead = false;
        TargetCleared = false;
        GcdGuardUntil = 0;
    }
}

public sealed class SequenceWrapper
{
    public required ISlotSequence Sequence { get; init; }
    public Action? CompeltedAction { get; set; }
}
