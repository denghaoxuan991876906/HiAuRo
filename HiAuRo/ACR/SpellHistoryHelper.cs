namespace HiAuRo.ACR;

/// <summary>
/// 技能使用历史跟踪 —— 记录每个技能最近一次成功执行的时间
/// ACR 作者通过 Check() 中判断 RecentlyUsed() 避免重复释放
/// </summary>
public static class SpellHistoryHelper
{
    /// <summary>技能最后使用时间戳 (DateTime.Ticks)</summary>
    private static readonly Dictionary<uint, long> _history = [];

    /// <summary>记录技能使用</summary>
    public static void RecordSpell(uint spellId)
    {
        _history[spellId] = DateTime.Now.Ticks;
    }

    /// <summary>技能是否在指定毫秒内使用过</summary>
    public static bool RecentlyUsed(uint spellId, int withinMs = 500)
    {
        if (!_history.TryGetValue(spellId, out var lastTick))
            return false;
        return (DateTime.Now.Ticks - lastTick) < withinMs * TimeSpan.TicksPerMillisecond;
    }

    /// <summary>距上次使用该技能的毫秒数（-1 = 从未使用）</summary>
    public static long GetLastSpellTime(uint spellId)
    {
        if (!_history.TryGetValue(spellId, out var lastTick))
            return -1;
        return (DateTime.Now.Ticks - lastTick) / TimeSpan.TicksPerMillisecond;
    }

    /// <summary>GC 计数 —— 上次 GCD 技能使用后经过几个 GCD</summary>
    public static int GetGcdCountFromLastGcd()
    {
        if (!_history.TryGetValue(0, out var lastTick))
            return 99;
        var elapsed = (DateTime.Now.Ticks - lastTick) / TimeSpan.TicksPerMillisecond;
        var gcdDuration = GCDHelper.GetGCDDuration();
        if (gcdDuration <= 0) return 99;
        return (int)(elapsed / gcdDuration);
    }

    /// <summary>RecordGcd —— 标记 GCD 使用时间点（用于 GetGcdCountFromLastGcd）</summary>
    public static void RecordGcd()
    {
        _history[0] = DateTime.Now.Ticks;
    }

    /// <summary>重置所有历史</summary>
    public static void Reset()
    {
        _history.Clear();
    }
}
