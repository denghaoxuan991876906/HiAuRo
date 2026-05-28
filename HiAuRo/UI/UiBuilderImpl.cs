using HiAuRo.ACR;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace HiAuRo.UI;

public sealed class UiBuilderImpl : IAcrUiBuilder
{
    private readonly bool _isImGui;
    private readonly List<UiControlDef> _controls = [];
    private string _currentTab = string.Empty;
    private string _currentGroup = string.Empty;

    private string CurrentParent => string.IsNullOrEmpty(_currentGroup) ? _currentTab : _currentGroup;

    public UiBuilderImpl(bool isImGui) => _isImGui = isImGui;
    public UiBuilderImpl() : this(false) { }
    public List<UiControlDef> GetControls() => [.. _controls];

    public void Clear()
    {
        _controls.Clear();
        _currentTab = string.Empty;
        _currentGroup = string.Empty;
    }

    #region 结构

    private int _tabBarId;

    public void AddTab(string title)
    {
        EndTab();
        _currentTab = "tab_" + title;
        _currentGroup = string.Empty;
        if (_isImGui)
        {
            if (_tabBarId == 0) { _tabBarId = 1; ImGui.BeginTabBar("##acr_tab_bar"); }
            ImGui.BeginTabItem(title);
        }
        else
        {
            _controls.Add(new UiControlDef(_currentTab, "tab", null, title, null));
        }
    }

    public void EndTab()
    {
        if (_isImGui && !string.IsNullOrEmpty(_currentTab))
            ImGui.EndTabItem();
        _currentTab = string.Empty;
        _currentGroup = string.Empty;
    }

    /// <summary>关闭 tab bar（ImGui 模式下在 RegisterControls 结束时调用）</summary>
    public void Finish()
    {
        if (_isImGui && _tabBarId > 0)
        {
            ImGui.EndTabBar();
            _tabBarId = 0;
        }
    }

    public void AddGroup(string title)
    {
        _currentGroup = "grp_" + title;
        if (_isImGui)
        {
            ImGuiLib.ComponentLibrary.Label(title);
            ImGui.Spacing();
        }
        else
        {
            _controls.Add(new UiControlDef(_currentGroup, "group", _currentTab, title, null));
        }
    }

    public void AddSeparator()
    {
        if (_isImGui) ImGui.Separator();
        else _controls.Add(new UiControlDef("__sep__", "separator", CurrentParent, string.Empty, null));
    }

    public void AddSameLine()
    {
        if (_isImGui) ImGui.SameLine();
        else _controls.Add(new UiControlDef("__sameline__", "sameLine", CurrentParent, string.Empty, null));
    }

    public void AddMainControl(bool showPause = true, bool showSave = true)
    {
        if (!_isImGui)
            _controls.Add(new UiControlDef("__main__", "mainControl", null, string.Empty, true,
                Meta: new { showPause, showSave }));
    }

    public void AddLabel(string text)
    {
        if (_isImGui) ImGuiLib.ComponentLibrary.Label(text);
        else _controls.Add(new UiControlDef("lbl_" + text, "label", CurrentParent, text, null));
    }

    public void AddTooltip(string targetId, string tooltip)
    {
        if (!_isImGui) _controls.Add(new UiControlDef($"__tip__{targetId}", "tooltip", CurrentParent, string.Empty, tooltip));
    }

    #endregion

    #region 无 ref 值控件（IUiBuilder，Trigger 系统用）

    public bool AddCheckbox(string label, bool value) => _isImGui ? false : AddCtrl(label, "checkbox", value);
    public bool AddSlider(string label, float min, float max, float value) => _isImGui ? false : AddCtrl(label, "slider", value, Options: new { min, max });
    public bool AddDropdown(string label, string[] options, string value) => _isImGui ? false : AddCtrl(label, "dropdown", value, Options: options);
    public bool AddIntInput(string label, int value, int step = 1, int stepFast = 10) => _isImGui ? false : AddCtrl(label, "intInput", value, Meta: new { step, stepFast });
    public bool AddFloatInput(string label, float value) => _isImGui ? false : AddCtrl(label, "floatInput", value);
    public bool AddTextInput(string label, string value) => _isImGui ? false : AddCtrl(label, "textInput", value ?? "");

    #endregion

    #region ref 值控件（IAcrUiBuilder，ACR 作者用）—— ImGui 即时渲染，ref 直接读写字段

    public bool AddCheckbox(string label, ref bool value)
    {
        if (_isImGui)
        {
            var changed = ImGui.Checkbox(label, ref value);
            if (changed) Runtime.ACRLifecycle.MarkSettingsDirty();
            return changed;
        }
        return AddCtrl(label, "checkbox", value);
    }

    public bool AddSlider(string label, float min, float max, ref float value)
    {
        if (_isImGui)
        {
            var changed = ImGui.SliderFloat(label, ref value, min, max);
            if (changed) Runtime.ACRLifecycle.MarkSettingsDirty();
            return changed;
        }
        return AddCtrl(label, "slider", value, Options: new { min, max });
    }

    public bool AddDropdown(string label, string[] options, ref string value)
    {
        if (_isImGui)
        {
            var idx = Array.IndexOf(options, value);
            if (idx < 0) idx = 0;
            var changed = ImGui.Combo(label, ref idx, options, options.Length);
            if (changed) { value = options[idx]; Runtime.ACRLifecycle.MarkSettingsDirty(); }
            return changed;
        }
        return AddCtrl(label, "dropdown", value, Options: options);
    }

    public bool AddIntInput(string label, ref int value, int step = 1, int stepFast = 10)
    {
        if (_isImGui)
        {
            var changed = ImGui.InputInt(label, ref value, step, stepFast);
            if (changed) Runtime.ACRLifecycle.MarkSettingsDirty();
            return changed;
        }
        return AddCtrl(label, "intInput", value, Meta: new { step, stepFast });
    }

    public bool AddFloatInput(string label, ref float value)
    {
        if (_isImGui)
        {
            var changed = ImGui.InputFloat(label, ref value);
            if (changed) Runtime.ACRLifecycle.MarkSettingsDirty();
            return changed;
        }
        return AddCtrl(label, "floatInput", value);
    }

    public bool AddTextInput(string label, ref string value)
    {
        if (_isImGui)
        {
            var changed = ImGui.InputText(label, ref value, 256);
            if (changed) Runtime.ACRLifecycle.MarkSettingsDirty();
            return changed;
        }
        return AddCtrl(label, "textInput", value ?? "");
    }

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

    private bool AddCtrl(string label, string type, object? value, object? Options = null, object? Meta = null)
    {
        _controls.Add(new UiControlDef("ctrl_" + label, type, CurrentParent, label, value, Options, Meta));
        return false;
    }

    #endregion
}
