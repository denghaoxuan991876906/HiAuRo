namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者专用 UI 控件接口 —— 值控件使用 ref 参数，直接读写 settings 字段
/// </summary>
public interface IAcrUiBuilder : IUiBuilder
{
    bool AddCheckbox(string label, ref bool value,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? expr = null);
    bool AddSlider(string label, float min, float max, ref float value,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? expr = null);
    bool AddDropdown(string label, string[] options, ref string value,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? expr = null);
    bool AddIntInput(string label, ref int value, int step = 1, int stepFast = 10,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? expr = null);
    bool AddFloatInput(string label, ref float value,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? expr = null);
    bool AddTextInput(string label, ref string value,
        [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? expr = null);
}
