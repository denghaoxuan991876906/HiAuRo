namespace HiAuRo.Recording;

/// <summary>最近时间窗帧缓存，只保留内存里的轻量上下文。</summary>
public sealed class CombatFrameWindowBuffer
{
    private readonly long _windowMs;
    private readonly object _lock = new();
    private readonly Queue<CombatFrameCacheSample> _items = new();

    public CombatFrameWindowBuffer(long windowMs)
    {
        if (windowMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowMs));
        _windowMs = windowMs;
    }

    public int Count
    {
        get { lock (_lock) return _items.Count; }
    }

    public void Push(CombatFrameCacheSample sample)
    {
        lock (_lock)
        {
            _items.Enqueue(sample);
            Trim(sample.Tick);
        }
    }

    public IReadOnlyList<CombatFrameCacheSample> GetWindowSnapshot()
    {
        lock (_lock)
        {
            return _items.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    private void Trim(long nowTick)
    {
        while (_items.Count > 0 && nowTick - _items.Peek().Tick > _windowMs)
            _items.Dequeue();
    }
}
