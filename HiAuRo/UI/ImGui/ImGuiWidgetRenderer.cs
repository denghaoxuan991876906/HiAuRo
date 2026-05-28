using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Interface.Textures;
using HiAuRo.Runtime;
using HiAuRo.Setting;
using HiAuRo.UI;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ImGui 控件渲染器 —— 通过 UiBuilderImpl 的字段绑定直接读写 settings 值
/// 不再使用 ControlValues 中间字典
/// </summary>
public static class ImGuiWidgetRenderer
{
    private static readonly Dictionary<uint, ISharedImmediateTexture> _iconCache = [];

    public static void Render(List<UiControlDef> controls, string activeTab, UiBuilderImpl? builder)
    {
        if (controls.Count == 0) return;

        var groups = controls.Where(c => c.Type == "group" && c.ParentId == activeTab).ToList();
        if (groups.Count == 0)
        {
            RenderItems(controls.Where(c =>
                (c.ParentId == activeTab || c.ParentId == null) &&
                c.Type is not ("tab" or "mainControl")), builder);
            return;
        }

        foreach (var group in groups)
        {
            using var defaultFont = ImRaii.PushFont(UiBuilder.DefaultFont);
            ImGui.TextColored(Theme.Colors.TextPrimary, group.Label);
            ImGui.Spacing();
            var items = controls.Where(c => c.ParentId == group.Id);
            RenderItems(items, builder);
            ImGui.Spacing();
            ComponentLibrary.Divider();
        }
    }

    private static void RenderItems(IEnumerable<UiControlDef> items, UiBuilderImpl? builder)
    {
        foreach (var item in items)
        {
            switch (item.Type)
            {
                case "checkbox": RenderCheckbox(item, builder); break;
                case "slider":   RenderSlider(item, builder); break;
                case "dropdown": RenderDropdown(item, builder); break;
                case "intInput": RenderIntInput(item, builder); break;
                case "label":    ComponentLibrary.Label(item.Label); break;
                case "separator":ComponentLibrary.Divider(); break;
                case "sameLine": ImGui.SameLine(); break;
                case "hotkeyRow":RenderHotkeyRow(item); break;
            }
        }
    }

    private static void RenderHotkeyRow(UiControlDef ctrl)
    {
        var ids = ctrl.Options switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.Array =>
                el.EnumerateArray().Select(e => e.GetString() ?? "").ToArray(),
            string[] arr => arr,
            _ => Array.Empty<string>()
        };
        if (ids.Length == 0) return;

        var allHotkeys = HiAuRo.ACR.HotkeyHelper.GetAll();
        for (int i = 0; i < ids.Length; i++)
        {
            var hk = allHotkeys.FirstOrDefault(h => h.Id == ids[i]);
            if (hk == null) continue;
            if (i > 0) ImGui.SameLine();

            var available = hk.Check() >= 0;
            var binding = HiAuRo.ACR.HotkeyHelper.GetBinding(hk.Id);
            var tex = hk.IconId > 0 ? LoadCachedIcon(hk.IconId) : default;

            if (tex != default)
            {
                using var hkVar = new ImRaii.StyleDisposable();
                hkVar.Push(ImGuiStyleVar.FrameRounding, 4);
                hkVar.Push(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
                var clicked = ImGui.Button($"##hkbtn-{hk.Id}", new Vector2(36, 36));
                var rectMin = ImGui.GetItemRectMin();
                var rectMax = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().AddImage(tex, rectMin + new Vector2(4), rectMax - new Vector2(4));
                if (clicked) HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.IsNullOrEmpty(binding) ? hk.Label : $"{hk.Label}   {binding}");
            }
            else
            {
                var hkColor = available ? Theme.Colors.AccentBlue : new Vector4(0.3f, 0.3f, 0.3f, 1);
                using var hkCol = new ImRaii.ColorDisposable();
                hkCol.Push(ImGuiCol.Button, hkColor);
                if (ImGui.Button($"{hk.Label}###hkbtn-{hk.Id}"))
                    HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(hk.Label);
                    if (!string.IsNullOrEmpty(binding)) { ImGui.SameLine(); ImGui.TextDisabled($"({binding})"); }
                    ImGui.EndTooltip();
                }
            }
        }
    }

    private static void RenderCheckbox(UiControlDef ctrl, UiBuilderImpl? builder)
    {
        var val = GetValue(ctrl, builder) is true;
        if (ComponentLibrary.Switch(ctrl.Id, ctrl.Label, ref val))
        {
            SetValue(ctrl, builder, val);
            ACRLifecycle.MarkSettingsDirty();
        }
    }

    private static void RenderSlider(UiControlDef ctrl, UiBuilderImpl? builder)
    {
        var val = GetValue(ctrl, builder) is float f ? f : 0f;
        float min = 0, max = 100;
        if (ctrl.Options is JsonElement opts)
        {
            min = opts.TryGetProperty("min", out var mn) ? mn.GetSingle() : 0;
            max = opts.TryGetProperty("max", out var mx) ? mx.GetSingle() : 100;
        }
        if (ComponentLibrary.Slider(ctrl.Id, ctrl.Label, ref val, min, max))
        {
            SetValue(ctrl, builder, val);
            ACRLifecycle.MarkSettingsDirty();
        }
    }

    private static void RenderDropdown(UiControlDef ctrl, UiBuilderImpl? builder)
    {
        var options = Array.Empty<string>();
        if (ctrl.Options is JsonElement opts)
            options = opts.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        var selectedIdx = GetValue(ctrl, builder) is int i ? i : 0;
        if (options.Length > 0 && selectedIdx >= options.Length) selectedIdx = 0;
        if (ComponentLibrary.Select(ctrl.Id, ctrl.Label, ref selectedIdx, options))
        {
            SetValue(ctrl, builder, selectedIdx);
            ACRLifecycle.MarkSettingsDirty();
        }
    }

    private static void RenderIntInput(UiControlDef ctrl, UiBuilderImpl? builder)
    {
        var val = GetValue(ctrl, builder) is int i ? i : 0;
        var step = 1;
        var stepFast = 10;
        if (ctrl.Meta is JsonElement meta)
        {
            step = meta.TryGetProperty("step", out var s) ? s.GetInt32() : 1;
            stepFast = meta.TryGetProperty("stepFast", out var sf) ? sf.GetInt32() : 10;
        }
        if (ComponentLibrary.InputNumber(ctrl.Id, ctrl.Label, ref val, step, stepFast))
        {
            SetValue(ctrl, builder, val);
            ACRLifecycle.MarkSettingsDirty();
        }
    }

    private static ImTextureID LoadCachedIcon(uint iconId)
    {
        if (!_iconCache.TryGetValue(iconId, out var sharedTex))
        {
            sharedTex = DService.Instance().Texture.GetFromGameIcon(new GameIconLookup(iconId));
            _iconCache[iconId] = sharedTex;
        }
        return sharedTex.GetWrapOrDefault()?.Handle ?? default;
    }

    /// <summary>从 settings 字段读取当前值（优先用 UiBuilderImpl 绑定）</summary>
    private static object? GetValue(UiControlDef ctrl, UiBuilderImpl? builder)
    {
        if (builder != null)
            return UiBuilderImpl.GetBoundValue(ctrl, builder);
        return ctrl.Value;
    }

    /// <summary>向 settings 字段写入值</summary>
    private static void SetValue(UiControlDef ctrl, UiBuilderImpl? builder, object val)
    {
        if (builder != null)
            UiBuilderImpl.SetBoundValue(ctrl, builder, val);
    }
}
