using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public enum SpellActionType
{
    Request,
    Effect,
    CancelCast,
    ActionRejected
}

public sealed class SpellActionTracker
{
    public static SpellActionTracker Instance { get; } = new();

    public uint PendingSpellId { get; private set; }
    private volatile bool _request;
    private volatile bool _effect;
    private volatile bool _cancelCast;
    private volatile bool _actionRejected;
    private Slot? _lastSlot;

    private SpellActionTracker() { }

    public bool HasFlag(uint spellId, SpellActionType type)
    {
        if (spellId != PendingSpellId) return false;
        return type switch
        {
            SpellActionType.Request => _request,
            SpellActionType.Effect => _effect,
            SpellActionType.CancelCast => _cancelCast,
            SpellActionType.ActionRejected => _actionRejected,
            _ => false
        };
    }

    public bool BeginTrack(Slot slot, Spell spell)
    {
        if (slot == _lastSlot) return false;
        PendingSpellId = spell.Id;
        _request = false;
        _effect = false;
        _cancelCast = false;
        _actionRejected = false;
        return true;
    }

    public void Notify(SpellActionType type, uint spellId)
    {
        if (PendingSpellId != spellId) return;
        switch (type)
        {
            case SpellActionType.Request: _request = true; break;
            case SpellActionType.Effect: _effect = true; break;
            case SpellActionType.CancelCast: _cancelCast = true; break;
            case SpellActionType.ActionRejected: _actionRejected = true; break;
        }
    }

    public void Clear()
    {
        PendingSpellId = 0;
        _request = false;
        _effect = false;
        _cancelCast = false;
        _actionRejected = false;
    }
}
