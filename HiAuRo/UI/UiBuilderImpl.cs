using HiAuRo.ACR;
using System.Linq;

namespace HiAuRo.UI;

/// <summary>
/// IUiBuilder 实现 —— 收集控件定义为 UiControlDef 列表
/// 注册阶段返回 false；值控件通过 ImGui 渲染器自动检测变更并持久化
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
        EndTab();
        string shortId = "tab_" + title;
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
        string shortId = "grp_" + title;
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
    public bool AddCheckbox(string label, bool value) =>
        AddValueControl(label, "checkbox", value);

    /// <summary>添加滑块</summary>
    public bool AddSlider(string label, float min, float max, float value) =>
        AddValueControl(label, "slider", value, Options: new { min, max });

    /// <summary>添加下拉框</summary>
    public bool AddDropdown(string label, string[] options, string value) =>
        AddValueControl(label, "dropdown", value, Options: options);

    /// <summary>添加整数输入</summary>
    public bool AddIntInput(string label, int value, int step = 1, int stepFast = 10) =>
        AddValueControl(label, "intInput", value, Meta: new { step, stepFast });

    /// <summary>添加浮点数输入</summary>
    public bool AddFloatInput(string label, float value) =>
        AddValueControl(label, "floatInput", value);

    /// <summary>添加文本输入</summary>
    public bool AddTextInput(string label, string value) =>
        AddValueControl(label, "textInput", value ?? "");

    /// <summary>添加 QT 开关（ID 使用稳定标识，确保跨会话持久化 key 一致）</summary>
    public bool AddQtToggle(string label, bool value, string? tooltip = null, string? color = null, bool defaultVisible = true)
    {
        var id = "qt_" + label;
        QTHelper.Register(id, label, value, tooltip, color);
        _controls.Add(new UiControlDef(id, "qttoggle", CurrentParent, label, value,
            Meta: new { tooltip, color, defaultVisible }));
        return false;
    }

    /// <summary>添加文本标签</summary>
    public void AddLabel(string text) =>
        _controls.Add(new UiControlDef("lbl_" + text, "label", CurrentParent, text, null));

    /// <summary>添加工具提示</summary>
    public void AddTooltip(string targetId, string tooltip) =>
        _controls.Add(new UiControlDef($"__tip__{targetId}", "tooltip", CurrentParent, string.Empty, tooltip));

    /// <summary>添加 QT 热键</summary>
    public void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true)
    {
        HotkeyHelper.Register(resolver);
        _controls.Add(new UiControlDef(resolver.Id, "qthotkey", CurrentParent, label, resolver.DefaultKey,
            Meta: new { defaultVisible }));
    }

    /// <summary>添加热键行</summary>
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
    public void AddBuiltinQt(BuiltinQt type, bool? value = null)
    {
        var id = type.GetId();
        var label = type.GetLabel();
        var val = value ?? type.GetDefault();
        if (_controls.Any(c => c.Id == id)) return;
        QTHelper.Register(id, label, val);
        _controls.Add(new UiControlDef(id, "qttoggle", CurrentParent, label, val,
            Meta: new { defaultVisible = true }));
    }

    /// <summary>添加主控制区</summary>
    public void AddMainControl(bool showPause = true, bool showSave = true) =>
        _controls.Add(new UiControlDef("__main__", "mainControl", null, string.Empty, true,
            Meta: new { showPause, showSave }));

    /// <summary>通用值控件注册（注册阶段返回 false，变更由渲染器检测）</summary>
    private bool AddValueControl(string label, string type, object? value, object? Options = null, object? Meta = null)
    {
        var id = "ctrl_" + label;
        _controls.Add(new UiControlDef(id, type, CurrentParent, label, value, Options, Meta));
        return false;
    }
}
