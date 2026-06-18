namespace HiAuRo.Runtime;

/// <summary>
/// oGCD 全局节流门。
/// 任一来源的能力技一旦进入执行链，就先占用一段最小间隔；
/// 后续来源必须等到间隔结束后才能继续 build / 发送下一发能力技。
/// </summary>
internal static class AbilityThrottle
{
    private static long _reservedUntil;

    internal static bool CanStartNow(long now)
    {
        var lastSuccess = Data.Combat.LastAbilityUseTime;
        var interval = PluginConfig.Instance.AbilityIntervalMs;
        return now >= _reservedUntil && (lastSuccess == 0 || now - lastSuccess >= interval);
    }

    internal static bool TryReserveNow(long now)
    {
        if (!CanStartNow(now)) return false;

        _reservedUntil = now + PluginConfig.Instance.AbilityIntervalMs;
        return true;
    }

    internal static long GetRemainingMs(long now)
    {
        var interval = PluginConfig.Instance.AbilityIntervalMs;
        var lastSuccess = Data.Combat.LastAbilityUseTime;
        long byReserve = Math.Max(0, _reservedUntil - now);
        long bySuccess = lastSuccess == 0 ? 0 : Math.Max(0, lastSuccess + interval - now);
        return Math.Max(byReserve, bySuccess);
    }

    internal static void MarkSuccess(long now)
    {
        Data.Combat.LastAbilityUseTime = now;
        _reservedUntil = Math.Max(_reservedUntil, now + PluginConfig.Instance.AbilityIntervalMs);
    }

    internal static void Reset()
    {
        _reservedUntil = 0;
    }
}
