namespace HiAuRo.UI;

/// <summary>
/// UI 控件定义数据模型
/// </summary>
public sealed record UiControlDef(
    string Id,
    string Type,
    string? ParentId,
    string Label,
    object? Value,
    object? Options = null,
    object? Meta = null
);
