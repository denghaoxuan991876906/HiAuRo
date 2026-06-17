using System.Reflection;
using System.Numerics;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Script;

namespace HiAuRo.UI.Tabs;

public sealed class ScriptTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public ScriptTabPage(PluginConfig config, Action saveConfig)
        : base("脚本", "script_list", IconHelper.Icons.MediaTechnologyCode)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();
        var territory = OmenTools.OmenService.GameState.TerritoryType;

        ComponentLibrary.SectionHeader("脚本管理");

        ImGui.TextColored(Theme.Colors.AccentBlue, "当前副本:");
        ImGui.SameLine();
        if (territory == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "未进入副本");
        }
        else
        {
            ImGui.TextColored(Theme.Colors.TextPrimary, $"TerritoryId={territory}");

            ImGui.Spacing();

            var scripts = ScriptManager.ActiveScripts;
            if (scripts.Count == 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(Theme.Colors.TextTertiary, "当前副本无已加载的脚本。");
                ImGui.Spacing();
                var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "Scripts");
                ImGui.TextColored(Theme.Colors.TextTertiary, $"放 .cs 文件到: {dir}");

                var diag = ScriptManager.CompileDiagnostics;
                if (diag.Count > 0)
                {
                    ImGui.Separator();
                    ImGui.TextColored(Theme.Colors.AccentOrange, "编译诊断:");
                    foreach (var msg in diag)
                        ImGui.TextColored(Theme.Colors.TextTertiary, $"  {msg}");
                }
            }
            else
            {
                var globalEnabled = ScriptGlobal.Enabled;
                if (ComponentLibrary.Switch("scriptSys", "启用脚本系统", ref globalEnabled))
                    ScriptGlobal.Enabled = globalEnabled;

                ImGui.SameLine();
                if (ComponentLibrary.DefaultButton("刷新脚本"))
                    ScriptManager.LoadTerritory(territory);

                ImGui.SameLine();
                if (ComponentLibrary.WarningButton("重置脚本状态"))
                    ScriptManager.ResetAll();

                ImGui.SameLine();
                ImGui.TextColored(Theme.Colors.TextPrimary, $"已加载 {scripts.Count} 个脚本");

                ImGui.Spacing();

                foreach (var script in scripts)
                {
                    DrawScriptCard(script);
                    ImGui.Spacing();
                }
            }
        }
        
    }

    private static void DrawScriptCard(ScriptRecord script)
    {
        var hash = script.GetHashCode();

        var enabled = script.IsEnabled;
        if (ComponentLibrary.Switch($"enable_{hash}", "", ref enabled))
        {
            script.IsEnabled = enabled;
            ScriptManager.SaveEnableStates();
        }
        ImGui.SameLine();

        var headerLabel = $"{script.Name}{(script.Author != null ? $" ({script.Author})" : "")}##{hash}";
        var expanded = ImGui.CollapsingHeader(headerLabel);

        if (script.CompileError != null)
        {
            ImGui.TextColored(Theme.Colors.AccentRed, $"编译错误: {script.CompileError}");
            return;
        }

        if (!expanded)
        {
            return;
        }

        ImGui.Indent(16f);

        if (script.Type != null && script.Instance != null && script.HasSettings)
        {
            var changed = false;
            foreach (var prop in script.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<UserSettingAttribute>(false);
                if (attr == null) continue;
                if (!prop.CanRead || !prop.CanWrite) continue;

                var val = prop.GetValue(script.Instance);
                var newVal = DrawSettingControl(attr.Label, val, prop.PropertyType);

                if (newVal != null && !Equals(newVal, val))
                {
                    prop.SetValue(script.Instance, newVal);
                    changed = true;
                }
            }

            if (changed)
                ScriptManager.SaveSettings(script);
        }

        if (script.Type != null)
        {
            var methods = script.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var hasMethods = false;
            var isFirst = true;

            foreach (var method in methods)
            {
                foreach (var attr in method.GetCustomAttributes<ScriptMethodAttribute>(false))
                {
                    hasMethods = true;

                    if (!isFirst) ComponentLibrary.Divider();
                    isFirst = false;

                    var key = attr.Name ?? $"{method.Name}({attr.EventType.Name})";
                    var keyId = $"##sm_{hash}_{key}";

                    var me = script.MethodEnabled.GetValueOrDefault(key, true);
                    if (ImGui.Checkbox(keyId, ref me))
                    {
                        script.MethodEnabled[key] = me;
                        ScriptManager.SaveEnableStates();
                    }
                    ImGui.SameLine();
                    ImGui.TextColored(Theme.Colors.AccentGreen, $"[事件] {method.Name}");
                    ImGui.SameLine();
                    ImGui.TextColored(Theme.Colors.TextSecondary, attr.EventType.Name);
                    if (attr.Condition is { Length: > 0 })
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.Colors.TextTertiary, $"[{string.Join(", ", attr.Condition)}]");
                    }

                    if (script.MethodParamNames.TryGetValue(key, out var paramNames))
                        DrawMethodParamEditors(script, key, method, paramNames);
                }

                foreach (var attr in method.GetCustomAttributes<ScriptCheckAttribute>(false))
                {
                    hasMethods = true;

                    if (!isFirst) ComponentLibrary.Divider();
                    isFirst = false;

                    var key = attr.Name ?? method.Name;
                    var keyId = $"##sc_{hash}_{key}";

                    var me = script.MethodEnabled.GetValueOrDefault(key, true);
                    if (ImGui.Checkbox(keyId, ref me))
                    {
                        script.MethodEnabled[key] = me;
                        ScriptManager.SaveEnableStates();
                    }
                    ImGui.SameLine();
                    ImGui.TextColored(Theme.Colors.AccentOrange, $"[轮询] {method.Name}");

                    if (attr.IntervalMs > 0)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.Colors.TextTertiary, $"(间隔 {attr.IntervalMs}ms)");
                    }

                    if (script.MethodParamNames.TryGetValue(key, out var paramNames))
                        DrawMethodParamEditors(script, key, method, paramNames);
                }
            }

            if (!hasMethods)
                ImGui.TextColored(Theme.Colors.TextTertiary, "未注册任何方法。");
        }

        ImGui.Unindent(16f);
    }

    private static void DrawMethodParamEditors(ScriptRecord script, string methodKey, MethodInfo method, List<string> paramNames)
    {
        var changed = false;
        foreach (var fieldName in paramNames)
        {
            var field = script.Type?.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && script.Instance != null)
            {
                var val = field.GetValue(script.Instance);
                var newVal = DrawSettingControl(fieldName, val, field.FieldType);
                if (newVal != null && !Equals(newVal, val))
                {
                    field.SetValue(script.Instance, newVal);
                    changed = true;
                }
                continue;
            }

            var prop = script.Type?.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanRead && prop.CanWrite && script.Instance != null)
            {
                var val = prop.GetValue(script.Instance);
                var newVal = DrawSettingControl(fieldName, val, prop.PropertyType);
                if (newVal != null && !Equals(newVal, val))
                {
                    prop.SetValue(script.Instance, newVal);
                    changed = true;
                }
            }
        }
        if (changed) ScriptManager.SaveParamStates();
    }

    private static void SettingLabel(string label)
    {
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        ImGui.SameLine(160f);
    }

    private static object? DrawSettingControl(string label, object? value, Type type)
    {
        if (type == typeof(bool))
        {
            var b = (bool)(value ?? false);
            SettingLabel(label);
            if (ComponentLibrary.Switch(label, "", ref b)) return b;
        }
        else if (type == typeof(int))
        {
            var i = (int)(value ?? 0);
            SettingLabel(label);
            ImGui.SetNextItemWidth(120);
            if (ComponentLibrary.InputNumber(label, "", ref i)) return i;
        }
        else if (type == typeof(float))
        {
            var f = (float)(value ?? 0f);
            SettingLabel(label);
            using var c = new ImRaii.ColorDisposable();
            c.Push(ImGuiCol.FrameBg, Theme.Colors.FillSecondary);
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputFloat($"##{label}", ref f)) return f;
        }
        else if (type == typeof(string))
        {
            var s = (string?)value ?? "";
            SettingLabel(label);
            ImGui.SetNextItemWidth(200);
            if (ComponentLibrary.InputText(label, "", ref s, 256, 200)) return s;
        }
        else if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            var idx = value != null ? Array.IndexOf(names, value.ToString()) : 0;
            if (idx < 0) idx = 0;
            SettingLabel(label);
            ImGui.SetNextItemWidth(180);
            if (ComponentLibrary.Select(label, "", ref idx, names))
                return Enum.Parse(type, names[idx]);
        }
        else
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, $"{label}: {value}");
        }
        return null;
    }
}
