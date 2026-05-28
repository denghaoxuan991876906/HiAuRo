using HiAuRo.ACR;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace HiAuRo.UI;

public sealed class UiBuilderImpl : IAcrUiBuilder
{
    private readonly bool _isImGui;
    private readonly string? _activeTab;
    private readonly List<UiControlDef> _controls = [];
    private string _currentTab = string.Empty;
    private string _currentGroup = string.Empty;

    internal readonly Dictionary<string, string> Bindings = [];

    private string CurrentParent => string.IsNullOrEmpty(_currentGroup) ? _currentTab : _currentGroup;

    public UiBuilderImpl() : this(false) { }
    public UiBuilderImpl(bool isImGui, string? activeTab = null) { _isImGui = isImGui; _activeTab = activeTab; }

    private bool InScope => !_isImGui || _activeTab == null || _currentTab == _activeTab;

    public List<UiControlDef> GetControls() => [.. _controls];

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
        if (_isImGui) { if (InScope) { ImGuiLib.ComponentLibrary.Label(title); ImGui.Spacing(); } }
        else _controls.Add(new UiControlDef(_currentGroup, "group", _currentTab, title, null));
    }

    public void AddSeparator()
    {
        if (_isImGui) { if (InScope) ImGui.Separator(); }
        else _controls.Add(new UiControlDef("__sep__", "separator", CurrentParent, string.Empty, null));
    }

    public void AddSameLine()
    {
        if (_isImGui) { if (InScope) ImGui.SameLine(); }
        else _controls.Add(new UiControlDef("__sameline__", "sameLine", CurrentParent, string.Empty, null));
    }

    public void AddMainControl(bool showPause = true, bool showSave = true)
    {
        if (!_isImGui) _controls.Add(new UiControlDef("__main__", "mainControl", null, string.Empty, true,
            Meta: new { showPause, showSave }));
    }

    public void AddLabel(string text)
    {
        if (_isImGui) { if (InScope) ImGuiLib.ComponentLibrary.Label(text); }
        else _controls.Add(new UiControlDef("lbl_" + text, "label", CurrentParent, text, null));
    }

    public void AddTooltip(string targetId, string tooltip)
    {
        if (!_isImGui) _controls.Add(new UiControlDef($"__tip__{targetId}", "tooltip", CurrentParent, string.Empty, tooltip));
    }

    #endregion

    #region 无 ref 值控件（IUiBuilder，Trigger 系统用）

    public bool AddCheckbox(string label, bool value) { WebOnly(() => AddCtrl(label, "checkbox", value)); return false; }
    public bool AddSlider(string label, float min, float max, float value) { WebOnly(() => AddCtrl(label, "slider", value, Options: new { min, max })); return false; }
    public bool AddDropdown(string label, string[] options, string value) { WebOnly(() => AddCtrl(label, "dropdown", value, Options: options)); return false; }
    public bool AddIntInput(string label, int value, int step = 1, int stepFast = 10) { WebOnly(() => AddCtrl(label, "intInput", value, Meta: new { step, stepFast })); return false; }
    public bool AddFloatInput(string label, float value) { WebOnly(() => AddCtrl(label, "floatInput", value)); return false; }
    public bool AddTextInput(string label, string value) { WebOnly(() => AddCtrl(label, "textInput", value ?? "")); return false; }
    public bool AddButton(string label)
    {
        if (_isImGui) return InScope && ImGui.Button(Uid(label));
        AddCtrl(label, "button", null);
        return false;
    }

    private void WebOnly(System.Action a) { if (!_isImGui) a(); }

    #endregion

    #region ref 值控件（IAcrUiBuilder，ACR 作者用）

    /// <summary>每帧重置的控件序号（实例字段，新 builder 自动从 0 开始）</summary>
    private int _uidSeq;
    /// <summary>生成 ImGui 唯一 ID：{label}##{tab}_{group}_{seq}</summary>
    private string Uid(string label) => label + "##" + _currentTab + "_" + _currentGroup + "_" + _uidSeq++;

    public bool AddCheckbox(string label, ref bool value, string? expr = null)
    {
        if (_isImGui) return InScope && RenderChanged(ImGui.Checkbox(Uid(label), ref value));
        StoreAndBind(label, "checkbox", value, expr);
        return false;
    }

    public bool AddSlider(string label, float min, float max, ref float value, string? expr = null)
    {
        if (_isImGui) return InScope && RenderChanged(ImGui.SliderFloat(Uid(label), ref value, min, max));
        StoreAndBind(label, "slider", value, expr, Options: new { min, max });
        return false;
    }

    public bool AddDropdown(string label, string[] options, ref string value, string? expr = null)
    {
        if (_isImGui)
        {
            if (!InScope) return false;
            var idx = System.Array.IndexOf(options, value); if (idx < 0) idx = 0;
            if (ImGui.Combo(Uid(label), ref idx, options, options.Length)) { value = options[idx]; Runtime.ACRLifecycle.MarkSettingsDirty(); return true; }
            return false;
        }
        StoreAndBind(label, "dropdown", value, expr, Options: options);
        return false;
    }

    public bool AddIntInput(string label, ref int value, int step = 1, int stepFast = 10, string? expr = null)
    {
        if (_isImGui) return InScope && RenderChanged(ImGui.InputInt(Uid(label), ref value, step, stepFast));
        StoreAndBind(label, "intInput", value, expr, Meta: new { step, stepFast });
        return false;
    }

    public bool AddFloatInput(string label, ref float value, string? expr = null)
    {
        if (_isImGui) return InScope && RenderChanged(ImGui.InputFloat(Uid(label), ref value));
        StoreAndBind(label, "floatInput", value, expr);
        return false;
    }

    public bool AddTextInput(string label, ref string value, string? expr = null)
    {
        if (_isImGui) return InScope && RenderChanged(ImGui.InputText(Uid(label), ref value, 256));
        StoreAndBind(label, "textInput", value ?? "", expr);
        return false;
    }

    private static bool RenderChanged(bool changed) { if (changed) Runtime.ACRLifecycle.MarkSettingsDirty(); return changed; }

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
        var stableId = "hk_" + label;
        HotkeyHelper.Register(stableId, resolver);
        _controls.Add(new UiControlDef(stableId, "qthotkey", CurrentParent, label, resolver.DefaultKey,
            Meta: new { defaultVisible }));
    }

    public void AddHotkeyRow(IHotkeyResolver[] hotkeyIds)
    {
        for (int i = 0; i < hotkeyIds.Length; i++)
        {
            var r = hotkeyIds[i];
            var stableId = "hk_" + r.Label;
            HotkeyHelper.Register(stableId, r);
            _controls.Add(new UiControlDef(stableId, "qthotkey", CurrentParent, r.Label, r.DefaultKey,
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

    private string AddCtrl(string label, string type, object? value, object? Options = null, object? Meta = null)
    {
        var id = "ctrl_" + label + System.Guid.NewGuid().ToString("N")[..8];
        _controls.Add(new UiControlDef(id, type, CurrentParent, label, value, Options, Meta));
        return id;
    }

    private void StoreAndBind(string label, string type, object? value, string? expr, object? Options = null, object? Meta = null)
    {
        var id = AddCtrl(label, type, value, Options, Meta);
        if (string.IsNullOrEmpty(expr)) return;
        var dot = expr.LastIndexOf('.');
        Bindings[id] = dot >= 0 ? expr[(dot + 1)..] : expr;
    }

    public static void WriteBack(UiBuilderImpl builder, string controlId, object rawValue)
    {
        if (!builder.Bindings.TryGetValue(controlId, out var fieldName)) return;
        var settings = Runtime.ACRLifecycle.GetCurrentSettings();
        if (settings == null) return;
        var field = settings.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null) return;
        try { field.SetValue(settings, System.Convert.ChangeType(rawValue, field.FieldType)); Runtime.ACRLifecycle.MarkSettingsDirty(); } catch { }
    }

    #endregion
}
