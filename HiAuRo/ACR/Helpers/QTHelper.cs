namespace HiAuRo.ACR;

/// <summary>
/// QT (Quick Toggle) 开关管理器 —— 管理 ACR 行为模式开关（智能模式、AOE、自动选敌等）
/// </summary>
public static class QTHelper
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, QtData> _data = [];
    private static readonly List<string> _order = [];
    private static readonly Dictionary<string, string> _labelToId = [];

    /// <summary>QT 值变更事件。参数: (id, newValue)</summary>
    public static event Action<string, bool>? OnChanged;

    /// <summary>注册 QT 开关（重复 id 忽略，仅供 UiBuilderImpl 内部使用）</summary>
    internal static void Register(string id, string label, bool defaultValue, string? tooltip = null, string? color = null)
    {
        lock (_lock)
        {
            if (_data.ContainsKey(id))
                return;

            var qt = new QtData
            {
                Id = id,
                Label = label,
                Value = defaultValue,
                DefaultValue = defaultValue,
                Tooltip = tooltip,
                Color = color
            };
            _data[id] = qt;
            _order.Add(id);
            _labelToId[label] = id;
        }
    }

    /// <summary>获取 QT 当前值（先按 id 匹配，失败则按 label 匹配）</summary>
    public static bool IsEnabled(string id)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(id, out var qt))
                return qt.Value;
            if (_labelToId.TryGetValue(id, out var realId))
                return _data[realId].Value;
            return false;
        }
    }

    /// <summary>获取内置 QT 当前值</summary>
    public static bool IsEnabled(BuiltinQt type) => IsEnabled(type.GetId());

    /// <summary>设置 QT 值</summary>
    public static void SetValue(string id, bool value)
    {
        bool changed = false;
        lock (_lock)
        {
            if (_data.TryGetValue(id, out var qt) && qt.Value != value)
            {
                qt.Value = value;
                changed = true;
            }
        }
        if (changed) OnChanged?.Invoke(id, value);
    }

    /// <summary>设置内置 QT 值</summary>
    public static void SetValue(BuiltinQt type, bool value) => SetValue(type.GetId(), value);

    /// <summary>翻转 QT 值</summary>
    public static void Toggle(string id)
    {
        bool newValue = false;
        bool changed = false;
        lock (_lock)
        {
            if (_data.TryGetValue(id, out var qt))
            {
                qt.Value = !qt.Value;
                newValue = qt.Value;
                changed = true;
            }
        }
        if (changed) OnChanged?.Invoke(id, newValue);
    }

    /// <summary>翻转内置 QT 值</summary>
    public static void Toggle(BuiltinQt type) => Toggle(type.GetId());

    /// <summary>获取 QtData 引用（返回副本，线程安全）</summary>
    public static QtData? Get(string id)
    {
        lock (_lock)
        {
            return _data.TryGetValue(id, out var qt) ? qt with { } : null;
        }
    }

    /// <summary>获取内置 QtData 引用</summary>
    public static QtData? Get(BuiltinQt type) => Get(type.GetId());

    /// <summary>获取所有 QT 开关（按注册顺序）</summary>
    public static List<QtData> GetAll()
    {
        lock (_lock)
        {
            return _order.Select(id => _data[id] with { }).ToList();
        }
    }

    /// <summary>清空所有 QT（ACR 卸载时调用）</summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _data.Clear();
            _order.Clear();
            _labelToId.Clear();
        }
    }
}
