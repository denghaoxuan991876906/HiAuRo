namespace HiAuRo.ACR;

/// <summary>
/// 热键管理 —— 绑定 / 解析 / 触发
/// 使用稳定 ID（"hk_" + label）作为 key
/// </summary>
public static class HotkeyHelper
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, HotkeyRegistration> _registrations = [];
    private static readonly List<IHotkeyEventHandler> _handlers = [];
    private static readonly Dictionary<string, string> _keyBindings = [];

    public static event Action<string, string>? OnExecuted;

    /// <summary>注册热键解析器，同 ID 不重复添加</summary>
    internal static void Register(string id, IHotkeyResolver resolver)
    {
        Register(id, resolver, new HotkeyRegistration(id, resolver));
    }

    internal static void Register(string id, IHotkeyResolver resolver, HotkeyRegistration registration)
    {
        lock (_lock)
        {
            _registrations[id] = registration with { Id = id, Resolver = resolver };
            _keyBindings.TryAdd(id, resolver.DefaultKey);
        }
    }

    public static void RegisterHotkey(string id, IHotkeyResolver resolver, bool defaultVisible = false, bool isSystem = false, bool canDelete = true, int order = 0)
    {
        Register(id, resolver, new HotkeyRegistration(id, resolver, defaultVisible, isSystem, canDelete, order));
    }

    public static void UnregisterHotkey(string id)
    {
        lock (_lock)
        {
            _registrations.Remove(id);
            _keyBindings.Remove(id);
        }
    }

    public static void RegisterHandler(IHotkeyEventHandler handler)
    {
        lock (_lock) { if (!_handlers.Contains(handler)) _handlers.Add(handler); }
    }

    public static void UnregisterHandler(IHotkeyEventHandler handler)
    {
        lock (_lock) { _handlers.Remove(handler); }
    }

    public static void Clear()
    {
        lock (_lock) { _registrations.Clear(); _handlers.Clear(); _keyBindings.Clear(); }
    }

    public static void SetBinding(string id, string key)
    {
        lock (_lock) _keyBindings[id] = key;
    }

    public static string GetBinding(string id)
    {
        lock (_lock) return _keyBindings.GetValueOrDefault(id, string.Empty);
    }

    internal static Dictionary<string, string> GetAllBindings()
    {
        lock (_lock) return new Dictionary<string, string>(_keyBindings);
    }

    public static void HandleKeyPress(string key)
    {
        if (!HiAuRo.Runtime.RuntimeCore.IsRunning) return;

        string? executedId = null;
        string? executedLabel = null;

        lock (_lock)
        {
            foreach (var (id, registration) in _registrations)
            {
                var resolver = registration.Resolver;
                if (_keyBindings.TryGetValue(id, out var boundKey) && boundKey == key)
                {
                    if (resolver.Check() >= 0)
                    {
                        resolver.Execute();
                        executedId = id;
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

    public static void ExecuteById(string id)
    {
        if (!HiAuRo.Runtime.RuntimeCore.IsRunning) return;

        string? executedLabel = null;
        lock (_lock)
        {
            if (_registrations.TryGetValue(id, out var registration))
            {
                var resolver = registration.Resolver;
                var c = resolver.Check();
                if (c >= 0)
                {
                    resolver.Execute();
                    executedLabel = resolver.Label;
                }
            }
        }

        if (executedLabel != null)
            OnExecuted?.Invoke(id, executedLabel);
    }

    public static List<IHotkeyResolver> GetAll()
    {
        lock (_lock) return _registrations.Values
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Label)
            .Select(x => x.Resolver)
            .ToList();
    }

    public static List<HotkeyRegistration> GetAllRegistrations()
    {
        lock (_lock) return _registrations.Values
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Label)
            .ToList();
    }

    public static HotkeyRegistration? GetRegistration(string id)
    {
        lock (_lock) return _registrations.GetValueOrDefault(id);
    }
}

public sealed record HotkeyRegistration(
    string Id,
    IHotkeyResolver Resolver,
    bool DefaultVisible = true,
    bool IsSystem = true,
    bool CanDelete = false,
    int Order = 0)
{
    public string Label => Resolver.Label;
    public string DefaultKey => Resolver.DefaultKey;
    public uint IconId
    {
        get
        {
            try
            {
                return Resolver.IconId;
            }
            catch
            {
                return 0;
            }
        }
    }
}
