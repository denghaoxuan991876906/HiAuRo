namespace HiAuRo.ACR;

/// <summary>
/// 描述性 UI 控件注册接口 —— C# 描述 UI，HiAuRo 转为 UiControlDef 供 Web / ImGui 双模式渲染
/// 值控件返回 true 表示当帧值有变化，ACR 作者可据此触发保存
/// </summary>
public interface IUiBuilder
{
    // === 结构 ===
    void AddTab(string title);
    void EndTab();
    void AddGroup(string title);
    void AddSeparator();
    void AddSameLine();
    void AddMainControl(bool showPause = true, bool showSave = true);

    // === 值控件：返回 true 表示值有变化 ===
    bool AddCheckbox(string label, bool value);
    bool AddSlider(string label, float min, float max, float value);
    bool AddDropdown(string label, string[] options, string value);
    bool AddIntInput(string label, int value, int step = 1, int stepFast = 10);
    bool AddFloatInput(string label, float value);
    bool AddTextInput(string label, string value);
    bool AddQtToggle(string label, bool value, string? tooltip = null, string? color = null, bool defaultVisible = true);
    void AddLabel(string text);

    // === QT / 热键 ===
    void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true);
    void AddTooltip(string targetId, string tooltip);
    void AddHotkeyRow(IHotkeyResolver[] hotkeyIds);
    void AddBuiltinQt(BuiltinQt type, bool? value = null);
}
