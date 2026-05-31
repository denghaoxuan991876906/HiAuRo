namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者悬浮窗 UI 接口
/// </summary>
public interface IRotationUI
{
    /// <summary>注册 UI 控件，builder 提供 ref 重载可直接绑定 settings 字段</summary>
    void RegisterControls(IAcrUiBuilder builder);
}

/// <summary>
/// ACR 作者可选实现，用于在 ImGui 状态栏追加自绘面板。
/// </summary>
public interface IRotationImGuiPanelProvider
{
    IEnumerable<RotationImGuiPanel> ImGuiPanels { get; }
}

/// <summary>ACR 自绘面板定义。</summary>
public sealed record RotationImGuiPanel(
    string Id,
    string Label,
    RotationImGuiPanelPlacement Placement,
    Action Draw);

/// <summary>ACR 自绘面板挂载位置。</summary>
public enum RotationImGuiPanelPlacement
{
    Tab,
    QtTab,
    HotkeyTab
}
