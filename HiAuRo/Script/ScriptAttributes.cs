namespace HiAuRo.Script;

/// <summary>标记类为可执行脚本</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ScriptTypeAttribute : Attribute
{
    public string? Name { get; set; }
    public uint[]? TerritoryIds { get; set; }
    public string? Guid { get; set; }
    public string? Author { get; set; }
}

/// <summary>事件驱动方法：匹配的游戏事件触发时调用，每次事件都调用</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ScriptMethodAttribute : Attribute
{
    public Type EventType { get; set; } = null!;
    public string[]? Condition { get; set; }
    public string? Name { get; set; }
    public string[]? Params { get; set; }
    /// <summary>延迟执行毫秒，事件触发后等待 DelayMs 再调用 handler</summary>
    public int DelayMs { get; set; }
}

/// <summary>轮询检查方法：每 IntervalMs 调用，返回 true 后停止</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ScriptCheckAttribute : Attribute
{
    public int IntervalMs { get; set; }
    public string? Name { get; set; }
    public string[]? Params { get; set; }
}

/// <summary>用户设置属性：自动生成 ImGui 控件</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UserSettingAttribute : Attribute
{
    public string Label { get; set; } = "";

    public UserSettingAttribute() { }
    public UserSettingAttribute(string label) { Label = label; }
}
