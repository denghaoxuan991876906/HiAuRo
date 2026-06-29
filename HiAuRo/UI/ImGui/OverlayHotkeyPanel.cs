using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// Hotkey 热键图标面板 — 绘制游戏技能图标，点击执行，自适应内容大小
/// </summary>
public sealed class OverlayHotkeyPanel : OverlayBase
{
    private readonly Action _saveConfig;
    private int _visibleCount;

    /// <summary>不允许缩放</summary>
    protected override bool AllowResize => false;
    /// <summary>无边距</summary>
    protected override Vector2 ContentPadding => Vector2.Zero;
    /// <summary>内容起始偏移</summary>
    protected override Vector2 ContentOffset => new(6, 6);

    /// <summary>Initializes a new instance of the <see cref="OverlayHotkeyPanel"/> class</summary>
    public OverlayHotkeyPanel(PluginConfig config, Action saveConfig) : base("HiAuRoHotkeyPanel##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayHotkeyPanelX, config.OverlayHotkeyPanelY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(56, 38),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    /// <summary>预绘制时计算窗口尺寸</summary>
    protected override void OnPreDraw()
    {
        var hotkeys = ImGuiOverlayState.Hotkeys;
        var ui = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        _visibleCount = hotkeys.Count(h => ui?.HkVisible.GetValueOrDefault("hk_" + h.Label, true) ?? true);
        if (_visibleCount == 0) return;
        var cols = (ui?.HkCols ?? 0) > 0 ? ui!.HkCols : 5;
        var rows = (_visibleCount + cols - 1) / cols;
        var actualCols = Math.Min(_visibleCount, cols);
        var pad = ContentOffset.X;
        var gapX = 6f;
        var gapY = 4f;
        var btnW = (ui?.HkBtnSize ?? 0) > 0 ? (float)ui!.HkBtnSize : 50f;
        var btnH = btnW;
        var w = pad * 2 + btnW * actualCols + gapX * (actualCols - 1);
        var h = pad * 2 + btnH * rows + gapY * (rows - 1);
        ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
    }

    /// <summary>绘制热键面板内容</summary>
    protected override void DrawContent()
    {
#if DEBUG
        var _uiTick = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        var hotkeys = ImGuiOverlayState.Hotkeys;

        if (hotkeys.Count == 0)
        {
            BeginContent();
            ImGui.TextColored(Theme.Colors.TextTertiary, "无热键技能");
            return;
        }

        BeginContent();
        if (_visibleCount == 0) return;

        var ui = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
        if (ui == null) return;

        var cols = ui.HkCols;
        if (cols <= 0) cols = 5;
        var btnSize = ui.HkBtnSize > 0
            ? ui.HkBtnSize : 50;
        var col = 0;

        // 公共样式：圆角、内边距、颜色（所有按钮相同）
        using var outerV = new ImRaii.StyleDisposable();
        outerV.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        outerV.Push(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
        using var outerC = new ImRaii.ColorDisposable();
        outerC.Push(ImGuiCol.Button, Theme.Colors.BgContainer);
        outerC.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        outerC.Push(ImGuiCol.ButtonActive, Theme.Colors.FillPrimary);
        outerC.Push(ImGuiCol.Border, Theme.Colors.BorderSecondary);

        for (var i = 0; i < hotkeys.Count; i++)
        {
            var hk = hotkeys[i];
            var visible = ui.HkVisible.GetValueOrDefault("hk_" + hk.Label, true);
            if (!visible) continue;

            var hkId = "hk_" + hk.Label;
            ImGui.PushID(hkId);

            var available = hk.Check() >= 0;
            ImGui.BeginDisabled(!available);
            var binding = HiAuRo.ACR.HotkeyHelper.GetBinding(hkId) ?? hk.DefaultKey;

            var clicked = DrawIconButton(hk.IconId, new Vector2(btnSize));

            if (clicked && HiAuRo.Runtime.RuntimeCore.IsRunning)
                HiAuRo.ACR.HotkeyHelper.ExecuteById(hkId);

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                var tip = string.IsNullOrEmpty(binding) ? hk.Label : $"{hk.Label}   {binding}";
                ImGui.SetTooltip(tip);
            }

            ImGui.PopID();

            SameLineOrWrap(ref col, cols);
        }

#if DEBUG
        PerfMonitor.Record("UI.Hotkey", _uiTick);
#endif
    }

    /// <summary>AntdUI 风格图标按钮 — 游戏技能图标 + 圆角边框（样式由外层循环管理）</summary>
    private bool DrawIconButton(uint iconId, Vector2 btnSize)
    {
        var clicked = ImGui.Button($"##hkb_{iconId}_{btnSize.X}", btnSize);

        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        var pad = 4f;

        if (iconId != 0)
        {
            var handle = LoadIconTexture(iconId);
            if (handle != (nint)0)
                ImGui.GetWindowDrawList().AddImage(
                    handle, rectMin + new Vector2(pad), rectMax - new Vector2(pad));
        }

        return clicked;
    }

    /// <summary>图标共享纹理缓存（持有 ISharedImmediateTexture 以保持 GPU 资源存活）</summary>
    private static readonly Dictionary<uint, ISharedImmediateTexture> _iconTextureCache = new();
    private static readonly List<uint> _iconLruOrder = new();
    private const int MaxCachedIcons = 200;

    /// <summary>加载游戏技能图标纹理（LRU 缓存共享纹理引用，每帧获取新鲜句柄防野指针）</summary>
    private static ImTextureID LoadIconTexture(uint iconId)
    {
        if (!_iconTextureCache.TryGetValue(iconId, out var sharedTex))
        {
            sharedTex = DService.Instance().Texture.GetFromGameIcon(new GameIconLookup(iconId));
            _iconTextureCache[iconId] = sharedTex;
            _iconLruOrder.Add(iconId);
            while (_iconTextureCache.Count > MaxCachedIcons && _iconLruOrder.Count > 0)
            {
                var oldest = _iconLruOrder[0];
                _iconLruOrder.RemoveAt(0);
                _iconTextureCache.Remove(oldest);
            }
        }
        return sharedTex.GetWrapOrDefault()?.Handle ?? 0;
    }

    /// <summary>保存窗口位置</summary>
    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayHotkeyPanelX = pos.X;
        _config.OverlayHotkeyPanelY = pos.Y;
        _saveConfig();
    }
}