using System.Collections.Concurrent;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 线程安全的移动需求缓冲区——脚本和 IPC 接收方写入，智能层读取
/// </summary>
public static class DemandBuffer
{
    private static readonly ConcurrentQueue<MovementDemand> _pending = new();
    private static readonly object _rebuildLock = new();
    private static int _orderCounter;

    /// <summary>添加需求（战斗时间模式下 AddedOrder 保留为目标时间，不覆盖）</summary>
    public static void Add(MovementDemand demand)
    {
        if (demand.Source != "战斗时间")
            demand.AddedOrder = Interlocked.Increment(ref _orderCounter);
        _pending.Enqueue(demand);
    }

    /// <summary>按 factNodeId 分组取出所有积压需求（空队列快速返回）</summary>
    public static ILookup<string, MovementDemand> GetGrouped()
    {
        if (_pending.IsEmpty)
            return Array.Empty<MovementDemand>().ToLookup(d => d.FactNodeId);
        return _pending.ToArray().ToLookup(d => d.FactNodeId);
    }

    /// <summary>移除已释放的需求（省去 Where().ToArray() 中间分配）</summary>
    public static void Remove(IEnumerable<string> demandIds)
    {
        lock (_rebuildLock)
        {
            var ids = new HashSet<string>(demandIds);
            var snapshot = _pending.ToArray();
            while (_pending.TryDequeue(out _)) { }
            foreach (var d in snapshot)
                if (!ids.Contains(d.Id))
                    _pending.Enqueue(d);
        }
    }

    /// <summary>取出所有已到目标战斗时间的需求</summary>
    public static List<MovementDemand> DrainCombatTimeReady(double totalTimeSec)
    {
        if (_pending.IsEmpty) return [];
        lock (_rebuildLock)
        {
            var snapshot = _pending.ToArray();
            var readyDemands = snapshot
                .Where(d => d.Source == "战斗时间" && d.AddedOrder <= totalTimeSec)
                .ToList();
            if (readyDemands.Count == 0) return [];

            var ids = new HashSet<string>(readyDemands.Select(d => d.Id));
            while (_pending.TryDequeue(out _)) { }
            foreach (var d in snapshot)
                if (!ids.Contains(d.Id))
                    _pending.Enqueue(d);
            return readyDemands;
        }
    }

    /// <summary>清空</summary>
    public static void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
    }
}
