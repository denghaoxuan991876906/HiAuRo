namespace HiAuRo.ACR;

/// <summary>
/// 热键配置数据结构
/// </summary>
public sealed class HotkeyConfig
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>绑定的技能 ID（0 = 非技能型热键）</summary>
    public uint SpellId { get; init; }

    /// <summary>描述文本（悬浮提示用）</summary>
    public string Description { get; init; } = string.Empty;
}
