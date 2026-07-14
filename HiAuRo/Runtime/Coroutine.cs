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
    private readonly object _timerLock = new();
    private long _id;

    private Coroutine() { }

    public long GenerateId() => Interlocked.Increment(ref _id) - 1;

    public void Update()
    {
        TimerAction[] snapshot;
        lock (_timerLock)
        {
            if (TimerActions.Count == 0) return;
            snapshot = TimerActions.Values.ToArray();
        }

        long now = Environment.TickCount64;
        List<TimerAction> readyActions = [];
        foreach (var action in snapshot)
        {
            if (!action.Check() || action.Time <= now)
                readyActions.Add(action);
        }

        if (readyActions.Count == 0) return;

        List<TaskCompletionSource<bool>> completions = [];
        lock (_timerLock)
        {
            foreach (var action in readyActions)
            {
                if (!TimerActions.TryGetValue(action.Id, out var current)
                    || !ReferenceEquals(current, action)
                    || !TimerActions.Remove(action.Id))
                    continue;

                var tcs = action.Tcs;
                action.Tcs = null!;
                if (!tcs.Task.IsCompleted)
                    completions.Add(tcs);
            }
        }

        foreach (var tcs in completions)
        {
            try
            {
                tcs.TrySetResult(true);
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[Coroutine] complete: {ex}"); }
        }
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
        lock (_timerLock)
        {
            TimerActions.Add(id, action);
        }
        var registration = ct.Register(() =>
        {
            TaskCompletionSource<bool>? completion = null;
            lock (_timerLock)
            {
                if (TimerActions.TryGetValue(id, out var current)
                    && ReferenceEquals(current, action)
                    && TimerActions.Remove(id))
                {
                    completion = action.Tcs;
                    action.Tcs = null!;
                }
            }

            completion?.TrySetCanceled(ct);
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
        List<TaskCompletionSource<bool>> completions = [];
        lock (_timerLock)
        {
            var keys = TimerActions.Keys.ToArray();
            foreach (var key in keys)
            {
                if (!TimerActions.TryGetValue(key, out var action)
                    || !TimerActions.Remove(key))
                    continue;

                var tcs = action.Tcs;
                action.Tcs = null!;
                if (tcs != null && !tcs.Task.IsCompleted)
                    completions.Add(tcs);
            }
        }

        foreach (var tcs in completions)
            tcs.TrySetResult(false);
    }
}
