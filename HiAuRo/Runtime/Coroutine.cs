namespace HiAuRo.Runtime;

public sealed class TimerAction
{
    public long Id;
    public long Time;
    public Func<bool> Check { get; set; } = () => true;
    public TaskCompletionSource<bool> Tcs = null!;
}

public sealed class Coroutine
{
    public static Coroutine Instance { get; } = new();

    public Dictionary<long, TimerAction> TimerActions { get; } = new(1000);
    private readonly HashSet<long> _failedTimer = new();
    private readonly HashSet<long> _succeedTimer = new();
    private long _id;

    private Coroutine() { }

    public long GenerateId() => _id++;

    public void Update()
    {
        if (TimerActions.Count == 0) return;
        long now = Environment.TickCount64;
        foreach (var kv in TimerActions)
        {
            if (!kv.Value.Check())
                _failedTimer.Add(kv.Key);
            else if (kv.Value.Time <= now)
                _succeedTimer.Add(kv.Key);
        }
        foreach (var id in _failedTimer)
        {
            try
            {
                if (!TimerActions.Remove(id, out var action)) continue;
                var tcs = action.Tcs;
                action.Tcs = null!;
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(true);
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[Coroutine] failed: {ex}"); }
        }
        foreach (var id in _succeedTimer)
        {
            try
            {
                if (!TimerActions.Remove(id, out var action)) continue;
                var tcs = action.Tcs;
                action.Tcs = null!;
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(true);
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[Coroutine] succeed: {ex}"); }
        }
        _failedTimer.Clear();
        _succeedTimer.Clear();
    }

    public Task WaitAsync(long ms)
    {
        if (ms <= 0) return Task.CompletedTask;
        return WaitAsync(ms, () => true);
    }

    public Task WaitAsync(long ms, CancellationToken ct)
    {
        if (ms <= 0)
            return ct.IsCancellationRequested ? Task.FromCanceled(ct) : Task.CompletedTask;
        return WaitAsync(ms, () => true, ct);
    }

    public async Task<bool> WaitAsync(long ms, Func<bool> cond, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (ms <= 0) return true;
        var expire = Environment.TickCount64 + ms;
        var tcs = new TaskCompletionSource<bool>();
        var id = GenerateId();
        var action = new TimerAction
        {
            Time = expire,
            Tcs = tcs,
            Id = id,
            Check = cond
        };
        TimerActions.Add(id, action);
        var registration = ct.Register(() =>
        {
            TimerActions.Remove(id);
            tcs.TrySetCanceled(ct);
        });
        try
        {
            return await tcs.Task;
        }
        finally
        {
            registration.Dispose();
        }
    }

    public Task DelayAsync(double ms) => WaitAsync((long)ms);

    public void Clear()
    {
        var keys = TimerActions.Keys.ToArray();
        foreach (var key in keys)
        {
            if (TimerActions.Remove(key, out var action)
                && action.Tcs != null && !action.Tcs.Task.IsCompleted)
                action.Tcs.SetResult(false);
        }
        _failedTimer.Clear();
        _succeedTimer.Clear();
    }
}
