namespace HiAuRo.Runtime;

/// <summary>
/// oGCD 全局节流门。
/// 任一来源的能力技一旦进入执行链，就先占用一段最小间隔；
/// 后续来源必须等到间隔结束后才能继续 build / 发送下一发能力技。
/// </summary>
internal static class AbilityThrottle
{
    private sealed record Reservation(uint ActionId, long Until);

    private static Reservation? _reservation;

    internal static bool CanStartNow(long now)
    {
        return GetActiveReservation(now) == null;
    }

    internal static bool TryReserveNow(long now, uint actionId)
    {
        var reservation = new Reservation(actionId, now + PluginConfig.Instance.AbilityIntervalMs);
        while (true)
        {
            var current = Volatile.Read(ref _reservation);
            if (current is { } && now < current.Until) return false;

            if (Interlocked.CompareExchange(ref _reservation, reservation, current) == current)
                return true;
        }
    }

    internal static long GetRemainingMs(long now)
    {
        return GetActiveReservation(now) is { } reservation ? reservation.Until - now : 0;
    }

    internal static void MarkSuccess(long now)
    {
        Data.Combat.LastAbilityUseTime = now;
    }

    internal static bool CanReleaseForActionEffect(uint reservedActionId, uint effectActionId, int abilityIntervalMs)
    {
        return reservedActionId == effectActionId && abilityIntervalMs <= 500;
    }

    internal static void ConfirmActionEffect(uint actionId)
    {
        var reservation = Volatile.Read(ref _reservation);
        if (reservation == null
            || !CanReleaseForActionEffect(reservation.ActionId, actionId, PluginConfig.Instance.AbilityIntervalMs))
            return;

        Interlocked.CompareExchange(ref _reservation, null, reservation);
    }

    internal static void Reset()
    {
        Interlocked.Exchange(ref _reservation, null);
    }

    private static Reservation? GetActiveReservation(long now)
    {
        while (true)
        {
            var reservation = Volatile.Read(ref _reservation);
            if (reservation == null || now < reservation.Until) return reservation;

            if (Interlocked.CompareExchange(ref _reservation, null, reservation) == reservation)
                return null;
        }
    }
}
