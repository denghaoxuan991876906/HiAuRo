namespace HiAuRo.ACR;

/// <summary>
/// 热键管理 —— 绑定 / 解析 / 触发
/// </summary>
public static class HotkeyHelper
{
    private static readonly object _lock = new();
    private static readonly List<IHotkeyResolver> _resolvers = [];
    private static readonly List<IHotkeyEventHandler> _handlers = [];
    private static readonly Dictionary<string, string> _keyBindings = [];

    /// <summary>热键执行事件。参数: (resolverId, label)</summary>
    public static event Action<string, string>? OnExecuted;

    /// <summary>注册热键解析器（仅供 UiBuilderImpl 内部使用），同 ID 不重复添加</summary>
    internal static void Register(IHotkeyResolver resolver)
    {
        lock (_lock)
        {
            if (_resolvers.Any(r => r.Id == resolver.Id))
                return;
            _resolvers.Add(resolver);
            _keyBindings.TryAdd(resolver.Id, resolver.DefaultKey);
        }
    }

    /// <summary>注册 Rotation 级热键处理器</summary>
    public static void RegisterHandler(IHotkeyEventHandler handler)
    {
        lock (_lock)
        {
            if (!_handlers.Contains(handler))
                _handlers.Add(handler);
        }
    }

    /// <summary>注销 Rotation 级热键处理器</summary>
    public static void UnregisterHandler(IHotkeyEventHandler handler)
    {
        lock (_lock)
        {
            _handlers.Remove(handler);
        }
    }

    /// <summary>清空所有热键注册（ACR 卸载时调用）</summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _resolvers.Clear();
            _handlers.Clear();
            _keyBindings.Clear();
        }
    }

    /// <summary>设置热键绑定</summary>
    public static void SetBinding(string id, string key)
    {
        lock (_lock) _keyBindings[id] = key;
    }

    /// <summary>获取热键绑定</summary>
    public static string GetBinding(string id)
    {
        lock (_lock) return _keyBindings.GetValueOrDefault(id, string.Empty);
    }

    /// <summary>获取所有绑定映射（供 HotkeyPoller 使用）</summary>
    internal static Dictionary<string, string> GetAllBindings()
    {
        lock (_lock) return new Dictionary<string, string>(_keyBindings);
    }

    /// <summary>处理按键输入，依次执行匹配的 Resolver 和 Handler</summary>
    public static void HandleKeyPress(string key)
    {
        if (!HiAuRo.Runtime.RuntimeCore.IsRunning) return; // 停止时阻断所有热键

        string? executedId = null;
        string? executedLabel = null;

        lock (_lock)
        {
            foreach (var resolver in _resolvers)
            {
                if (_keyBindings.TryGetValue(resolver.Id, out var boundKey) && boundKey == key)
                {
                    if (resolver.Check() >= 0)
                    {
                        resolver.Execute();
                        executedId = resolver.Id;
                        executedLabel = resolver.Label;
                    }
                    break;
                }
            }
        }

        if (executedId != null)
            OnExecuted?.Invoke(executedId, executedLabel ?? string.Empty);

        IHotkeyEventHandler[] handlersSnapshot;
        lock (_lock) { handlersSnapshot = _handlers.ToArray(); }
        foreach (var handler in handlersSnapshot)
        {
            if (handler.Run(new HotkeyConfig { Key = key }))
                return;
        }
    }

    /// <summary>通过 ID 直接执行热键（Web 前端按钮点击）</summary>
    public static void ExecuteById(string id)
    {
        if (!HiAuRo.Runtime.RuntimeCore.IsRunning) { DService.Instance().Log.Information($"[HotkeyHelper] ExecuteById: '{id}' blocked (ACR stopped)"); return; }

        string? executedLabel = null;
        lock (_lock)
        {
            foreach (var resolver in _resolvers)
            {
                if (resolver.Id == id)
                {
                    var c = resolver.Check();
                    if (c >= 0)
                    {
                        DService.Instance().Log.Information($"[HotkeyHelper] ExecuteById: '{id}' ({resolver.Label}) Check={c}, calling Execute");
                        resolver.Execute();
                        executedLabel = resolver.Label;
                    }
                    else
                    {
                        DService.Instance().Log.Information($"[HotkeyHelper] ExecuteById: '{id}' found but Check={c}");
                    }
                    break;
                }
            }
        }

        if (executedLabel != null)
            OnExecuted?.Invoke(id, executedLabel);
        else
            DService.Instance().Log.Warning($"[HotkeyHelper] ExecuteById: '{id}' no resolver executed (label=null)");
    }

    /// <summary>获取所有已注册热键</summary>
    public static List<IHotkeyResolver> GetAll()
    {
        lock (_lock) return [.. _resolvers];
    }
}
