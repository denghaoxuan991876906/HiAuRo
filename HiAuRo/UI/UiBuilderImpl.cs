using HiAuRo.ACR;
using System.Linq;

namespace HiAuRo.UI;

/// <summary>
/// IUiBuilder 实现 —— 收集控件定义为 UiControlDef 列表
/// </summary>
public sealed class UiBuilderImpl : HiAuRo.ACR.IUiBuilder
{
    private readonly List<UiControlDef> _controls = [];
    private string _currentTab = string.Empty;
    private string _currentGroup = string.Empty;

    private string CurrentParent => string.IsNullOrEmpty(_currentGroup) ? _currentTab : _currentGroup;

    /// <summary>获取收集到的所有控件定义</summary>
    public List<UiControlDef> GetControls() => [.. _controls];

    /// <summary>添加标签页</summary>
    public void AddTab(string title)
    {
        // 如果当前已有 tab，先结束上一个
        EndTab();
        // 生成 8 位唯一短ID
        string shortId = title + Guid.NewGuid().ToString("N")[0..8];
        _currentTab = shortId;
        _currentGroup = string.Empty;
        _controls.Add(new UiControlDef(shortId, "tab", null, title, null));
    }

    /// <summary>结束当前标签页</summary>
    public void EndTab()
    {
        _currentTab = string.Empty;
        _currentGroup = string.Empty;
    }

    /// <summary>添加分组</summary>
    public void AddGroup(string title)
    {
        string shortId = title + Guid.NewGuid().ToString("N")[0..8];
        _currentGroup = shortId;
        _controls.Add(new UiControlDef(shortId, "group", _currentTab, title, null));
    }

    /// <summary>添加分隔线</summary>
    public void AddSeparator() =>
        _controls.Add(new UiControlDef("__sep__", "separator", CurrentParent, string.Empty, null));

    /// <summary>添加同行标记</summary>
    public void AddSameLine() =>
        _controls.Add(new UiControlDef("__sameline__", "sameLine", CurrentParent, string.Empty, null));

    /// <summary>添加复选框</summary>
    public void AddCheckbox(string label, bool defaultValue) =>
        _controls.Add(new UiControlDef(label + Guid.NewGuid().ToString("N")[0..8], "checkbox", CurrentParent, label, defaultValue));

    /// <summary>添加滑块</summary>
    public void AddSlider(string label, float min, float max, float defaultValue) =>
        _controls.Add(new UiControlDef(label + Guid.NewGuid().ToString("N")[0..8], "slider", CurrentParent, label, defaultValue,
            Options: new { min, max }));

    /// <summary>添加下拉框</summary>
    public void AddDropdown(string label, string[] options, string defaultValue) =>
        _controls.Add(new UiControlDef(label + Guid.NewGuid().ToString("N")[0..8], "dropdown", CurrentParent, label, defaultValue,
            Options: options));

    /// <summary>添加 QT 热键</summary>
    public void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true)
    {
        HotkeyHelper.Register(resolver);
        _controls.Add(new UiControlDef(resolver.Id, "qthotkey", CurrentParent, label, resolver.DefaultKey,
            Meta: new { defaultVisible }));
    }

    /// <summary>添加 QT 开关（ID 使用 label，确保跨会话稳定，作为 QtValues/QtVisible 的持久化 key）</summary>
    public void AddQtToggle(string label, bool defaultValue, string? tooltip = null, string? color = null, bool defaultVisible = true)
    {
        var id = "qt_" + label;
        QTHelper.Register(id, label, defaultValue, tooltip, color);
        _controls.Add(new UiControlDef(id, "qttoggle", CurrentParent, label, defaultValue,
            Meta: new { tooltip, color, defaultVisible }));
    }

    /// <summary>添加主控制区</summary>
    public void AddMainControl(bool showPause = true, bool showSave = true)
    {
        _controls.Add(new UiControlDef("__main__", "mainControl", null, string.Empty, true,
            Meta: new { showPause, showSave }));
    }

    /// <summary>添加整数输入</summary>
    public void AddIntInput(string label, int defaultValue, int step = 1, int stepFast = 10) =>
        _controls.Add(new UiControlDef(label + Guid.NewGuid().ToString("N")[0..8], "intInput", CurrentParent, label, defaultValue,
            Meta: new { step, stepFast }));

    /// <summary>添加浮点数输入</summary>
    public void AddFloatInput(string label, float defaultValue) =>
        _controls.Add(new UiControlDef(label + Guid.NewGuid().ToString("N")[0..8], "floatInput", CurrentParent, label, defaultValue));

    /// <summary>添加文本输入</summary>
    public void AddTextInput(string label, string defaultValue) =>
        _controls.Add(new UiControlDef(label + Guid.NewGuid().ToString("N")[0..8], "textInput", CurrentParent, label, defaultValue ?? ""));

    /// <summary>添加文本标签</summary>
    public void AddLabel(string text) =>
        _controls.Add(new UiControlDef(text + Guid.NewGuid().ToString("N")[0..8], "label", CurrentParent, text, null));

    /// <summary>添加工具提示</summary>
    public void AddTooltip(string targetId, string tooltip) =>
        _controls.Add(new UiControlDef($"__tip__{targetId}", "tooltip", CurrentParent, string.Empty, tooltip));

    public void AddHotkeyRow(IHotkeyResolver[] hotkeyIds)
    {
        for (int i = 0; i < hotkeyIds.Length; i++)
        {
            var resolver = hotkeyIds[i];
            HotkeyHelper.Register(resolver);
            _controls.Add(new UiControlDef(resolver.Id, "qthotkey", CurrentParent, resolver.Label, resolver.DefaultKey,
                Meta: new { defaultVisible = true }));
            if (i < hotkeyIds.Length - 1)
                _controls.Add(new UiControlDef("__sameline__", "sameLine", CurrentParent, string.Empty, null));
        }
    }


    /// <summary>添加内置 QT 开关</summary>
    public void AddBuiltinQt(BuiltinQt type, bool? defaultValue = null)
    {
        var id = type.GetId();
        var label = type.GetLabel();
        var defaultVal = defaultValue ?? type.GetDefault();
        // 同一个内置 QT 不重复注册
        if (_controls.Any(c => c.Id == id)) return;
        QTHelper.Register(id, label, defaultVal);
        _controls.Add(new UiControlDef(id, "qttoggle", CurrentParent, label, defaultVal,
            Meta: new { defaultVisible = true }));
    }
}
