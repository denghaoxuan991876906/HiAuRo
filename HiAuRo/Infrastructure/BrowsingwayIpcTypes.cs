#pragma warning disable CS0649 // 字段由 Dalamud IPC 反序列化赋值

namespace Browsingway;

/// <summary>
/// Browsingway IPC 参数类型 —— 与 Browsingway 插件的 IPC 定义完全一致
/// 字段名、类型、命名空间必须匹配，否则 Dalamud IPC 反序列化会失败
/// </summary>

internal struct CreateOrUpdateArgs
{
    public string Name;
    public string Url;
    public int Width;
    public int Height;
    public float Zoom;
    public bool Locked;
}

internal struct SetVisibilityArgs
{
    public string Name;
    public bool Visible;
}

internal struct SetPositionArgs
{
    public string Name;
    public int? X;
    public int? Y;
}

internal struct SetDisabledArgs
{
    public string Name;
    public bool Disabled;
}
