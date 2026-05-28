using HiAuRo.ACR;
using System.Linq;
using System.Reflection;

namespace HiAuRo.UI;

/// <summary>
/// IUiBuilder / IAcrUiBuilder 实现
/// - ImGui 模式 (_isImGui=true): 值控件即时渲染 ImGui 控件，ref 直接读写字段
/// - Web 模式 (_isImGui=false): 收集控件定义，发送到前端
/// </summary>
public sealed class UiBuilderImpl : IAcrUiBuilder
{
    private readonly bool _isImGui;
    private readonly List<UiControlDef> _controls = [];
    private string _currentTab = string.Empty;
    private string _currentGroup = string.Empty;

    private string CurrentParent => string.IsNullOrEmpty(_currentGroup) ? _currentTab : _currentGroup;

    public UiBuilderImpl(bool isImGui) => _isImGui = isImGui;
    public UiBuilderImpl() : this(false) { } // 默认 Web 模式，向后兼容
    public List<UiControlDef> GetControls() => [.. _controls];

    /// <summary>清空控件列表（ImGui 模式下每帧调用前重置）</summary>
    public void Clear()
    {
        _controls.Clear();
        _currentTab = string.Empty;
        _currentGroup = string.Empty;
    }

    #region 结构

    public void AddTab(string title)
    {
        EndTab();
        _currentTab = "tab_" + title;
        _currentGroup = string.Empty;
        _controls.Add(new UiControlDef(_currentTab, "tab", null, title, null));
    }

    public void EndTab() { _currentTab = string.Empty; _currentGroup = string.Empty; }

    public void AddGroup(string title)
    {
        _currentGroup = "grp_" + title;
        _controls.Add(new UiControlDef(_currentGroup, "group", _currentTab, title, null));
    }

    public void AddSeparator() =>
        _controls.Add(new UiControlDef("__sep__", "separator", CurrentParent, string.Empty, null));

    public void AddSameLine() =>
        _controls.Add(new UiControlDef("__sameline__", "sameLine", CurrentParent, string.Empty, null));

    public void AddMainControl(bool showPause = true, bool showSave = true) =>
        _controls.Add(new UiControlDef("__main__", "mainControl", null, string.Empty, true,
            Meta: new { showPause, showSave }));

    public void AddLabel(string text) =>
        _controls.Add(new UiControlDef("lbl_" + text, "label", CurrentParent, text, null));

    public void AddTooltip(string targetId, string tooltip) =>
        _controls.Add(new UiControlDef($"__tip__{targetId}", "tooltip", CurrentParent, string.Empty, tooltip));

    #endregion

    #region 无 ref 值控件（IUiBuilder，Trigger 系统用）

    public bool AddCheckbox(string label, bool value) => AddCtrl(label, "checkbox", value);
    public bool AddSlider(string label, float min, float max, float value) => AddCtrl(label, "slider", value, Options: new { min, max });
    public bool AddDropdown(string label, string[] options, string value) => AddCtrl(label, "dropdown", value, Options: options);
    public bool AddIntInput(string label, int value, int step = 1, int stepFast = 10) => AddCtrl(label, "intInput", value, Meta: new { step, stepFast });
    public bool AddFloatInput(string label, float value) => AddCtrl(label, "floatInput", value);
    public bool AddTextInput(string label, string value) => AddCtrl(label, "textInput", value ?? "");

    #endregion

    #region ref 值控件（IAcrUiBuilder，ACR 作者用）

    public bool AddCheckbox(string label, ref bool value)
        => AddBoundCtrl(label, "checkbox", value);
    public bool AddSlider(string label, float min, float max, ref float value)
        => AddBoundCtrl(label, "slider", value, Options: new { min, max });
    public bool AddDropdown(string label, string[] options, ref string value)
        => AddBoundCtrl(label, "dropdown", value, Options: options);
    public bool AddIntInput(string label, ref int value, int step = 1, int stepFast = 10)
        => AddBoundCtrl(label, "intInput", value, Meta: new { step, stepFast });
    public bool AddFloatInput(string label, ref float value)
        => AddBoundCtrl(label, "floatInput", value);
    public bool AddTextInput(string label, ref string value)
        => AddBoundCtrl(label, "textInput", value ?? "");

    #endregion

    #region QT / 热键

    public bool AddQtToggle(string label, bool value, string? tooltip = null, string? color = null, bool defaultVisible = true)
    {
        var id = "qt_" + label;
        QTHelper.Register(id, label, value, tooltip, color);
        _controls.Add(new UiControlDef(id, "qttoggle", CurrentParent, label, value,
            Meta: new { tooltip, color, defaultVisible }));
        return false;
    }

    public void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true)
    {
        HotkeyHelper.Register(resolver);
        _controls.Add(new UiControlDef(resolver.Id, "qthotkey", CurrentParent, label, resolver.DefaultKey,
            Meta: new { defaultVisible }));
    }

    public void AddHotkeyRow(IHotkeyResolver[] hotkeyIds)
    {
        for (int i = 0; i < hotkeyIds.Length; i++)
        {
            var r = hotkeyIds[i];
            HotkeyHelper.Register(r);
            _controls.Add(new UiControlDef(r.Id, "qthotkey", CurrentParent, r.Label, r.DefaultKey,
                Meta: new { defaultVisible = true }));
            if (i < hotkeyIds.Length - 1)
                _controls.Add(new UiControlDef("__sameline__", "sameLine", CurrentParent, string.Empty, null));
        }
    }

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

    #endregion

    #region 内部

    /// <summary>字段绑定：控件ID → 字段所属对象</summary>
    internal Dictionary<string, object> Bindings { get; } = [];

    private bool AddCtrl(string label, string type, object? value, object? Options = null, object? Meta = null)
    {
        _controls.Add(new UiControlDef("ctrl_" + label, type, CurrentParent, label, value, Options, Meta));
        return false;
    }

    private bool AddBoundCtrl<T>(string label, string type, T value, object? Options = null, object? Meta = null)
    {
        AddCtrl(label, type, value, Options, Meta);
        return false;
    }

    /// <summary>从控件定义和绑定中读取值（供 ImGui 渲染器使用）</summary>
    internal static object GetBoundValue(UiControlDef ctrl, UiBuilderImpl builder)
    {
        // 从 acrsettings 中读取字段值
        var settings = Runtime.ACRLifecycle.GetCurrentSettings();
        if (settings == null) return ctrl.Value ?? 0;
        var t = settings.GetType();
        var f = t.GetField(ctrl.Label, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return f.GetValue(settings) ?? ctrl.Value ?? 0;
        return ctrl.Value ?? 0;
    }

    /// <summary>向字段写入值（供 ImGui 渲染器使用）</summary>
    internal static void SetBoundValue(UiControlDef ctrl, UiBuilderImpl builder, object val)
    {
        var settings = Runtime.ACRLifecycle.GetCurrentSettings();
        if (settings == null) return;
        var t = settings.GetType();
        var f = t.GetField(ctrl.Label, BindingFlags.Public | BindingFlags.Instance);
        if (f != null)
            f.SetValue(settings, Convert.ChangeType(val, f.FieldType));
    }

    #endregion
}
