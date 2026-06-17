namespace HiAuRo.ACR;

/// <summary>
/// QT (Quick Toggle) 开关数据模型
/// </summary>
public sealed record QtData
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool Value { get; set; }
    public bool DefaultValue { get; init; }
    public string? Tooltip { get; init; }
    public string? Color { get; init; }
    public string? HotkeyBinding { get; set; }
}
