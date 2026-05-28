namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者专用 UI 控件接口 —— 值控件使用 ref 参数，直接读写 settings 字段
/// ImGui 模式：即时渲染控件，直接读写字段值
/// Web 模式：收集控件定义 + 存储字段绑定，前端变更通过绑定写回
/// </summary>
public interface IAcrUiBuilder : IUiBuilder
{
    /// <summary>ref 重载：直接读写 settings 字段，返回 true 表示当帧有变化</summary>
    bool AddCheckbox(string label, ref bool value);
    /// <summary>ref 重载</summary>
    bool AddSlider(string label, float min, float max, ref float value);
    /// <summary>ref 重载</summary>
    bool AddDropdown(string label, string[] options, ref string value);
    /// <summary>ref 重载</summary>
    bool AddIntInput(string label, ref int value, int step = 1, int stepFast = 10);
    /// <summary>ref 重载</summary>
    bool AddFloatInput(string label, ref float value);
    /// <summary>ref 重载</summary>
    bool AddTextInput(string label, ref string value);
}
