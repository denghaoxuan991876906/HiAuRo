namespace HiAuRo.ACR;

/// <summary>
/// 基础 UI 控件接口（无 ref）—— Trigger 系统使用
/// ACR 作者请使用 IAcrUiBuilder
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
    void AddLabel(string text);

    // === 值控件（无 ref，注册阶段返回 false）===
    bool AddCheckbox(string label, bool value);
    bool AddSlider(string label, float min, float max, float value);
    bool AddDropdown(string label, string[] options, string value);
    bool AddIntInput(string label, int value, int step = 1, int stepFast = 10);
    bool AddFloatInput(string label, float value);
    bool AddTextInput(string label, string value);
    bool AddButton(string label);
    bool AddQtToggle(string label, bool value, string? tooltip = null, string? color = null, bool defaultVisible = true);

    // === QT / 热键 ===
    void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true);
    void AddHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = false, bool isSystem = false, bool canDelete = true, int order = 1000);
    void AddTooltip(string targetId, string tooltip);
    void AddHotkeyRow(IHotkeyResolver[] hotkeyIds);
    void AddBuiltinQt(BuiltinQt type, bool? value = null);
}
