using System.Reflection;
using System.Text.RegularExpressions;
using HiAuRo.Infrastructure;

namespace HiAuRo.Script;

/// <summary>脚本引擎 — [ScriptMethod] 事件路由 + [ScriptCheck] 轮询调度</summary>
public sealed class ScriptEngine
{
    public static ScriptEngine Instance { get; } = new();
    private bool _initialized;

    private readonly Dictionary<Type, List<MethodEntry>> _handlers = [];
    private readonly List<CheckEntry> _checks = [];

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;
        Execution.Events.GameEventHook.Instance.OnEventFired += OnGameEvent;
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;
        Execution.Events.GameEventHook.Instance.OnEventFired -= OnGameEvent;
        _handlers.Clear();
        _checks.Clear();
    }

    public void Register(ScriptRecord record)
    {
        if (record.Instance == null || record.Type == null) return;

        foreach (var method in record.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var attr in method.GetCustomAttributes(typeof(ScriptMethodAttribute), false))
            {
                var sma = (ScriptMethodAttribute)attr;
                var eventType = sma.EventType;
                if (!typeof(ACR.ITriggerCondParams).IsAssignableFrom(eventType)) continue;

                var key = sma.Name ?? $"{method.Name}({eventType.Name})";
                record.MethodEnabled.TryAdd(key, true);

                if (!_handlers.TryGetValue(eventType, out var list))
                {
                    list = [];
                    _handlers[eventType] = list;
                }

                list.Add(new MethodEntry
                {
                    Record = record,
                    Instance = record.Instance,
                    Key = key,
                    Method = method,
                    Condition = sma.Condition,
                    DelayMs = sma.DelayMs
                });

                if (sma.Params is { Length: > 0 })
                    record.MethodParamNames[key] = [.. sma.Params];
            }
        }

        foreach (var method in record.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var attr in method.GetCustomAttributes(typeof(ScriptCheckAttribute), false))
            {
                var sca = (ScriptCheckAttribute)attr;
                if (method.ReturnType != typeof(bool)) continue;
                if (method.GetParameters().Length != 0) continue;

                var key = sca.Name ?? method.Name;
                record.MethodEnabled.TryAdd(key, true);

                _checks.Add(new CheckEntry
                {
                    Record = record,
                    Instance = record.Instance,
                    Key = key,
                    Method = method,
                    IntervalMs = sca.IntervalMs,
                    NextCallAt = Environment.TickCount64 + sca.IntervalMs
                });

                if (sca.Params is { Length: > 0 })
                    record.MethodParamNames[key] = [.. sca.Params];
            }
        }

        // 如果脚本定义了 public void Init()，在注册后自动调用
        var initMethod = record.Type.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (initMethod != null)
            initMethod.Invoke(record.Instance, null);
    }

    public void UnregisterAll(ScriptRecord record)
    {
        foreach (var (_, list) in _handlers)
            list.RemoveAll(e => e.Record == record);

        _checks.RemoveAll(e => e.Record == record);
    }

    /// <summary>显式调用脚本的 Init() 方法（如果存在）</summary>
    public void InvokeInit(ScriptRecord record)
    {
        if (record.Instance == null || record.Type == null) return;
        try
        {
            var initMethod = record.Type.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (initMethod != null)
                initMethod.Invoke(record.Instance, null);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[ScriptEngine] InvokeInit 失败 {record.Name}: {ex.Message}");
        }
    }

    /// <summary>每帧：轮询 [ScriptCheck] 方法</summary>
    public void PollChecks()
    {
        if (_checks.Count == 0) return;
        if (ACR.MainControlHelper.IsPaused) return;
        var now = Environment.TickCount64;

        for (int i = _checks.Count - 1; i >= 0; i--)
        {
            var entry = _checks[i];
            if (!ScriptGlobal.Enabled || !entry.Record.IsEnabled) continue;
            if (!entry.Record.MethodEnabled.GetValueOrDefault(entry.Key, true)) continue;
            if (now < entry.NextCallAt) continue;

            try
            {
                var result = (bool)entry.Method.Invoke(entry.Instance, null)!;
                if (result)
                    _checks.RemoveAt(i);
                else
                    entry.NextCallAt = now + entry.IntervalMs;
            }
            catch (Exception ex)
            {
                Hi.Print($"[ScriptEngine] ScriptCheck异常 [{entry.Key}]: {ex.GetType().Name}: {ex.Message}");
                _checks.RemoveAt(i);
            }
        }
    }

    /// <summary>事件分发：按 EventType 路由到匹配的 [ScriptMethod] handler</summary>
    private void OnGameEvent(ACR.ITriggerCondParams eventParams)
    {
        if (!ScriptGlobal.Enabled) return;
        if (!Runtime.RuntimeCore.IsRunning || ACR.MainControlHelper.IsPaused) return;
        var type = eventParams.GetType();
        if (!_handlers.TryGetValue(type, out var list)) return;

        foreach (var entry in list)
        {
            if (!entry.Record.IsEnabled) continue;
            if (!entry.Record.MethodEnabled.GetValueOrDefault(entry.Key, true)) continue;
            try
            {
                if (!MatchConditions(eventParams, entry.Condition)) continue;

                if (entry.DelayMs > 0)
                {
                    var capturedParams = eventParams;
                    var capturedEntry = entry;
                    Hi.Delay(entry.DelayMs, () =>
                    {
                        try { capturedEntry.Method.Invoke(capturedEntry.Instance, [capturedParams]); }
                        catch (Exception ex)
                        {
                            Hi.Print($"[ScriptEngine] 延迟handler异常 [{capturedEntry.Key}]: {ex.GetType().Name}: {ex.Message}");
                        }
                    });
                }
                else
                {
                    entry.Method.Invoke(entry.Instance, [eventParams]);
                }
            }
            catch (Exception ex)
            {
                Hi.Print($"[ScriptEngine] handler异常 [{entry.Key}]: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static bool MatchConditions(ACR.ITriggerCondParams p, string[]? conditions)
    {
        if (conditions == null || conditions.Length == 0) return true;

        var type = p.GetType();
        foreach (var cond in conditions)
        {
            var parts = ParseCondition(cond);
            if (parts == null) continue;

            var (field, isRegex, value) = parts.Value;
            var prop = type.GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var fi = prop == null ? type.GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) : null;
            if (prop == null && fi == null) return false;

            var propValue = prop != null ? prop.GetValue(p) : fi!.GetValue(p);
            if (propValue == null) return false;

            if (isRegex)
            {
                if (!Regex.IsMatch(propValue.ToString()!, value))
                    return false;
            }
            else
            {
                if (!string.Equals(propValue.ToString(), value, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    private static (string field, bool isRegex, string value)? ParseCondition(string cond)
    {
        var firstColon = cond.IndexOf(':');
        if (firstColon < 0) return null;

        var field = cond[..firstColon];
        var rest = cond[(firstColon + 1)..];

        if (rest.StartsWith("regex:"))
            return (field, true, rest[6..]);

        return (field, false, rest);
    }

    private sealed class MethodEntry
    {
        public required ScriptRecord Record;
        public required object Instance;
        public required string Key;
        public required MethodInfo Method;
        public string[]? Condition;
        public int DelayMs;
    }

    private sealed class CheckEntry
    {
        public required ScriptRecord Record;
        public required object Instance;
        public required string Key;
        public required MethodInfo Method;
        public int IntervalMs;
        public long NextCallAt;
    }
}
