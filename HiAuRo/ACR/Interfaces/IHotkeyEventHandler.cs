namespace HiAuRo.ACR;

/// <summary>
/// 热键事件处理器接口 —— Rotation 级别热键处理（如切换 AOE 模式、开关爆发等）
/// </summary>
public interface IHotkeyEventHandler
{
    /// <summary>热键触发时的处理逻辑，返回 true 表示已处理（阻止后续 handler 执行）</summary>
    bool Run(HotkeyConfig config);
}
