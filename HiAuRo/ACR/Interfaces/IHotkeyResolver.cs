namespace HiAuRo.ACR;

/// <summary>
/// 热键解析器接口 —— QT 面板技能热键（点击释放技能）
/// </summary>
public interface IHotkeyResolver
{
    string Id { get; }
    string Label { get; }
    string DefaultKey { get; }

    /// <summary>检查是否可用。&gt;=0 可用，&lt;0 不可用。默认 0 = 总是可用</summary>
    int Check() => 0;

    /// <summary>技能图标 ID（0 = 无图标，前端用 emoji 兜底）</summary>
    uint IconId => 0;

    void Execute();
}
