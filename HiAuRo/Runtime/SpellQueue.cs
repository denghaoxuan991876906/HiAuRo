using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// HiAuRo 内部 Slot 调度队列（非 FFXIV 客户端队列）
/// </summary>
public sealed class SpellQueue
{
    private const int TtlMs = 5000;
    private readonly Queue<(Slot Slot, long EnqueuedAt)> _queue = new();

    /// <summary>入队 Slot</summary>
    public void Enqueue(Slot slot) => _queue.Enqueue((slot, Environment.TickCount64));

    /// <summary>是否有待处理的 Slot</summary>
    public bool HasPending()
    {
        ExpireOld();
        return _queue.Count > 0;
    }

    /// <summary>队列大小</summary>
    public int QueueSize => _queue.Count;

    /// <summary>获取下一个待执行 Slot</summary>
    public Slot? GetNext()
    {
        ExpireOld();
        return _queue.Count > 0 ? _queue.Dequeue().Slot : null;
    }

    /// <summary>清空队列</summary>
    public void Clear() => _queue.Clear();

    private void ExpireOld()
    {
        var now = Environment.TickCount64;
        while (_queue.Count > 0 && now - _queue.Peek().EnqueuedAt > TtlMs)
            _queue.Dequeue();
    }
}
