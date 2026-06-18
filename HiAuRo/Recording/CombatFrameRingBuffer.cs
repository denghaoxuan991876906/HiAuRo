namespace HiAuRo.Recording;

/// <summary>最近帧快照 ring buffer，只在内存保留少量上下文帧。</summary>
public sealed class CombatFrameRingBuffer
{
    private readonly CombatFrameSnapshot?[] _items;
    private readonly object _lock = new();
    private int _next;
    private int _count;

    public CombatFrameRingBuffer(int capacity)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be at least 2.");
        _items = new CombatFrameSnapshot[capacity];
    }

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    public void Push(CombatFrameSnapshot snapshot)
    {
        lock (_lock)
        {
            _items[_next] = snapshot;
            _next = (_next + 1) % _items.Length;
            if (_count < _items.Length)
                _count++;
        }
    }

    public CombatFrameSnapshot? GetLatest() => GetByOffset(1);

    public CombatFrameSnapshot? GetPrevious() => GetByOffset(2);

    private CombatFrameSnapshot? GetByOffset(int offset)
    {
        lock (_lock)
        {
            if (offset < 1 || offset > _count)
                return null;

            var index = _next - offset;
            if (index < 0)
                index += _items.Length;
            return _items[index];
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_items);
            _next = 0;
            _count = 0;
        }
    }
}
