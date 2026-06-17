using System.Numerics;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// Ant Design 5 设计令牌 — 亮色/暗色双主题
/// 参考: https://ant.design/docs/spec/colors-cn
/// </summary>
public static class Theme
{
    private static Vector4 Hex(uint hex) => new(
        ((hex >> 16) & 0xFF) / 255f,
        ((hex >> 8) & 0xFF) / 255f,
        (hex & 0xFF) / 255f,
        ((hex >> 24) & 0xFF) / 255f
    );

    private static Vector4 Rgba(float r, float g, float b, float a) => new(r, g, b, a);

    // ── 主题切换 ──

    /// <summary>主题模式</summary>
    public enum ThemeMode
    {
        /// <summary>亮色主题</summary>
        Light = 0,
        /// <summary>暗色主题</summary>
        Dark = 1
    }

    private static ThemeMode _mode = ThemeMode.Light;
    /// <summary>当前主题模式</summary>
    public static ThemeMode Mode
    {
        get => _mode;
        set { _mode = value; Tokens = value == ThemeMode.Light ? Light : Dark; }
    }

    /// <summary>
    /// 在 HiAuRo 窗口开始时调用 — 将 ImGui 样式切换到当前主题，
    /// 返回一个 IDisposable，窗口结束时 Dispose 即恢复原始样式。
    /// </summary>
    public static IDisposable PushThemeScope()
    {
        var scope = new ThemeScope();
        scope.Push();
        return scope;
    }

    private sealed class ThemeScope : IDisposable
    {
        private Vector4[]? _savedColors;
        private bool _disposed;
        private float _savedWindowRounding;
        private float _savedTabRounding;
        private float _savedScrollbarRounding;
        private float _savedGrabRounding;
        private float _savedChildRounding;
        private float _savedFrameRounding;
        private float _savedWindowBorderSize;
        private float _savedFrameBorderSize;
        private Vector2 _savedWindowPadding;
        private Vector2 _savedFramePadding;
        private Vector2 _savedItemSpacing;
        private Vector2 _savedItemInnerSpacing;
        private float _savedScrollbarSize;
        private float _savedGrabMinSize;

        public void Push()
        {
            var style = ImGui.GetStyle();
            var count = style.Colors.Length;

            _savedColors = new Vector4[count];
            for (var i = 0; i < count; i++)
                _savedColors[i] = style.Colors[i];

            _savedWindowRounding = style.WindowRounding;
            _savedTabRounding = style.TabRounding;
            _savedScrollbarRounding = style.ScrollbarRounding;
            _savedGrabRounding = style.GrabRounding;
            _savedChildRounding = style.ChildRounding;
            _savedFrameRounding = style.FrameRounding;
            _savedWindowBorderSize = style.WindowBorderSize;
            _savedFrameBorderSize = style.FrameBorderSize;
            _savedWindowPadding = style.WindowPadding;
            _savedFramePadding = style.FramePadding;
            _savedItemSpacing = style.ItemSpacing;
            _savedItemInnerSpacing = style.ItemInnerSpacing;
            _savedScrollbarSize = style.ScrollbarSize;
            _savedGrabMinSize = style.GrabMinSize;

            if (_mode == ThemeMode.Light)
                ImGui.StyleColorsLight(style);
            else
                ImGui.StyleColorsDark(style);

            style.WindowRounding = 0;
            style.TabRounding = RadiusSM;
            style.ScrollbarRounding = RadiusSM;
            style.GrabRounding = RadiusSM;
            style.ChildRounding = RadiusMD;
            style.FrameRounding = RadiusSM;
            style.WindowBorderSize = 0;
            style.FrameBorderSize = 0;
            style.WindowPadding = PaddingMD;
            style.FramePadding = PaddingSM;
            style.ItemSpacing = ItemSpacing;
            style.ItemInnerSpacing = new Vector2(6, 4);
            style.ScrollbarSize = 12;
            style.GrabMinSize = 8;

            style.Colors[(int)ImGuiCol.FrameBg] = Colors.FillSecondary;
            style.Colors[(int)ImGuiCol.FrameBgHovered] = Colors.FillPrimary;
            style.Colors[(int)ImGuiCol.FrameBgActive] = Colors.FillPrimary;
            style.Colors[(int)ImGuiCol.CheckMark] = Colors.AccentBlue;
            style.Colors[(int)ImGuiCol.SliderGrab] = Colors.SliderTrack;
            style.Colors[(int)ImGuiCol.SliderGrabActive] = Colors.SliderTrack;
            style.Colors[(int)ImGuiCol.Header] = Colors.AccentBlue;
            style.Colors[(int)ImGuiCol.HeaderHovered] = Colors.BgHover;
            style.Colors[(int)ImGuiCol.HeaderActive] = Colors.AccentBlue;
            style.Colors[(int)ImGuiCol.PopupBg] = Colors.BgElevated;
            style.Colors[(int)ImGuiCol.Border] = Colors.Border;
            style.Colors[(int)ImGuiCol.Separator] = Colors.BorderSecondary;
            style.Colors[(int)ImGuiCol.Tab] = Colors.FillTertiary;
            style.Colors[(int)ImGuiCol.TabHovered] = Colors.BgHover;
            style.Colors[(int)ImGuiCol.TabActive] = Colors.AccentBlue;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var style = ImGui.GetStyle();
            style.WindowRounding = _savedWindowRounding;
            style.TabRounding = _savedTabRounding;
            style.ScrollbarRounding = _savedScrollbarRounding;
            style.GrabRounding = _savedGrabRounding;
            style.ChildRounding = _savedChildRounding;
            style.FrameRounding = _savedFrameRounding;
            style.WindowBorderSize = _savedWindowBorderSize;
            style.FrameBorderSize = _savedFrameBorderSize;
            style.WindowPadding = _savedWindowPadding;
            style.FramePadding = _savedFramePadding;
            style.ItemSpacing = _savedItemSpacing;
            style.ItemInnerSpacing = _savedItemInnerSpacing;
            style.ScrollbarSize = _savedScrollbarSize;
            style.GrabMinSize = _savedGrabMinSize;

            if (_savedColors != null)
            {
                for (var i = 0; i < _savedColors.Length; i++)
                    style.Colors[i] = _savedColors[i];
            }
        }
    }

    /// <summary>当前主题令牌</summary>
    public static ThemeTokens Tokens { get; private set; } = Light!;

    static Theme() { }

    // ── 亮色令牌 (Ant Design 5 默认) ──

    /// <summary>亮色主题令牌</summary>
    public static readonly ThemeTokens Light = new()
    {
        BgBase        = Hex(0xFFFFFFFF),
        BgLayout      = Hex(0xFFF0F0F0),
        BgContainer   = Hex(0xFFFFFFFF),
        BgElevated    = Hex(0xFFFFFFFF),
        BgHover       = Rgba(0, 0, 0, 0.06f),
        BgSpotlight   = Hex(0xFFFFFFFF),

        // 毛玻璃三层: 底色 + 高光 + 暗部
        GlassBg       = Rgba(1f, 1f, 1f, 0.72f),      // 半透明白底
        GlassHighlight = Rgba(1f, 1f, 1f, 0.25f),     // 顶部高光
        GlassShade     = Rgba(0, 0, 0, 0.03f),        // 底部暗部
        GlassBorder    = Rgba(0, 0, 0, 0.08f),        // 细边框
        GlassShadow    = Rgba(0, 0, 0, 0.10f),        // 投影

        TextPrimary   = Rgba(0, 0, 0, 0.88f),
        TextSecondary = Rgba(0, 0, 0, 0.65f),
        TextTertiary  = Rgba(0, 0, 0, 0.45f),

        AccentBlue    = Hex(0xFF1677FF),
        AccentGreen   = Hex(0xFF52C41A),
        AccentRed     = Hex(0xFFFF4D4F),
        AccentOrange  = Hex(0xFFFAAD14),

        Border        = Hex(0xFFD9D9D9),
        BorderSecondary = Hex(0xFFE8E8E8),

        FillPrimary   = Rgba(0, 0, 0, 0.18f),
        FillSecondary = Rgba(0, 0, 0, 0.10f),
        FillTertiary  = Rgba(0, 0, 0, 0.04f),

        SwitchTrackOn = Hex(0xFF1677FF),
        SwitchTrackOff = Hex(0xFFBFBFBF),
        SwitchKnob    = Hex(0xFFFFFFFF),

        SliderTrack   = Hex(0xFF1677FF),
        SliderRail    = Hex(0xFFF0F0F0),

        SidebarActive = Hex(0xFFE6F4FF),
        SidebarActiveBorder = Hex(0xFF1677FF),

        TagActiveText = Hex(0xFFFFFFFF),
    };

    // ── 暗色令牌 ──

    /// <summary>暗色主题令牌</summary>
    public static readonly ThemeTokens Dark = new()
    {
        BgBase        = Hex(0xFF000000),
        BgLayout      = Hex(0xFF000000),
        BgContainer   = Hex(0xFF1A1A1A),
        BgElevated    = Hex(0xFF1F1F1F),
        BgHover       = Rgba(1f, 1f, 1f, 0.06f),
        BgSpotlight   = Hex(0xFF424242),

        // 毛玻璃三层: 底色 + 高光 + 暗部
        GlassBg       = Rgba(0.10f, 0.10f, 0.12f, 0.72f),  // 半透明暗底(偏蓝)
        GlassHighlight = Rgba(1f, 1f, 1f, 0.06f),           // 顶部微高光
        GlassShade     = Rgba(0, 0, 0, 0.20f),              // 底部暗部
        GlassBorder    = Rgba(1f, 1f, 1f, 0.08f),           // 细边框
        GlassShadow    = Rgba(0, 0, 0, 0.30f),              // 投影

        TextPrimary   = Rgba(1f, 1f, 1f, 0.85f),
        TextSecondary = Rgba(1f, 1f, 1f, 0.70f),
        TextTertiary  = Rgba(1f, 1f, 1f, 0.50f),

        AccentBlue    = Hex(0xFF3B82F6),
        AccentGreen   = Hex(0xFF4ADE80),
        AccentRed     = Hex(0xFFF87171),
        AccentOrange  = Hex(0xFFFB923C),

        Border        = Hex(0xFF424242),
        BorderSecondary = Hex(0xFF3A3A3A),

        FillPrimary   = Rgba(1f, 1f, 1f, 0.15f),
        FillSecondary = Rgba(1f, 1f, 1f, 0.15f),
        FillTertiary  = Rgba(1f, 1f, 1f, 0.08f),

        SwitchTrackOn = Hex(0xFF1668DC),
        SwitchTrackOff = Hex(0xFF555555),
        SwitchKnob    = Hex(0xFFFFFFFF),

        SliderTrack   = Hex(0xFF1668DC),
        SliderRail    = Hex(0xFF303030),

        SidebarActive = Rgba(0.09f, 0.41f, 0.87f, 0.2f),
        SidebarActiveBorder = Hex(0xFF1668DC),

        TagActiveText = Hex(0xFFFFFFFF),
    };

    // ── 动态颜色代理 (Theme.Colors.XXX 自动跟随当前主题) ──

    /// <summary>动态颜色代理（自动跟随当前主题）</summary>
    public static class Colors
    {
        /// <summary>布局背景色</summary>
        public static Vector4 BgLayout        => Tokens.BgLayout;
        /// <summary>容器背景色</summary>
        public static Vector4 BgContainer     => Tokens.BgContainer;
        /// <summary>浮层背景色</summary>
        public static Vector4 BgElevated      => Tokens.BgElevated;
        /// <summary>悬停背景色</summary>
        public static Vector4 BgHover         => Tokens.BgHover;
        /// <summary>高亮背景色</summary>
        public static Vector4 BgSpotlight     => Tokens.BgSpotlight;

        /// <summary>毛玻璃底色</summary>
        public static Vector4 GlassBg         => Tokens.GlassBg;
        /// <summary>毛玻璃高光色</summary>
        public static Vector4 GlassHighlight  => Tokens.GlassHighlight;
        /// <summary>毛玻璃暗部色</summary>
        public static Vector4 GlassShade      => Tokens.GlassShade;
        /// <summary>毛玻璃边框色</summary>
        public static Vector4 GlassBorder     => Tokens.GlassBorder;
        /// <summary>毛玻璃投影色</summary>
        public static Vector4 GlassShadow     => Tokens.GlassShadow;

        /// <summary>主要文字色</summary>
        public static Vector4 TextPrimary     => Tokens.TextPrimary;
        /// <summary>次要文字色</summary>
        public static Vector4 TextSecondary   => Tokens.TextSecondary;
        /// <summary>第三级文字色</summary>
        public static Vector4 TextTertiary    => Tokens.TextTertiary;

        /// <summary>品牌蓝</summary>
        public static Vector4 AccentBlue      => Tokens.AccentBlue;
        /// <summary>品牌绿</summary>
        public static Vector4 AccentGreen     => Tokens.AccentGreen;
        /// <summary>品牌红</summary>
        public static Vector4 AccentRed       => Tokens.AccentRed;
        /// <summary>品牌橙</summary>
        public static Vector4 AccentOrange    => Tokens.AccentOrange;

        /// <summary>边框色</summary>
        public static Vector4 Border          => Tokens.Border;
        /// <summary>次要边框色</summary>
        public static Vector4 BorderSecondary => Tokens.BorderSecondary;
        /// <summary>激活边框色</summary>
        public static Vector4 BorderActive    => Tokens.AccentBlue;

        /// <summary>一级填充色</summary>
        public static Vector4 FillPrimary     => Tokens.FillPrimary;
        /// <summary>二级填充色</summary>
        public static Vector4 FillSecondary   => Tokens.FillSecondary;
        /// <summary>三级填充色</summary>
        public static Vector4 FillTertiary    => Tokens.FillTertiary;

        /// <summary>开关轨道开启色</summary>
        public static Vector4 SwitchTrackOn   => Tokens.SwitchTrackOn;
        /// <summary>开关轨道关闭色</summary>
        public static Vector4 SwitchTrackOff  => Tokens.SwitchTrackOff;
        /// <summary>开关旋钮色</summary>
        public static Vector4 SwitchKnob      => Tokens.SwitchKnob;

        /// <summary>滑块轨道色</summary>
        public static Vector4 SliderTrack     => Tokens.SliderTrack;
        /// <summary>滑块滑轨色</summary>
        public static Vector4 SliderRail      => Tokens.SliderRail;

        /// <summary>侧边栏激活色</summary>
        public static Vector4 SidebarActive      => Tokens.SidebarActive;
        /// <summary>侧边栏激活边框色</summary>
        public static Vector4 SidebarActiveBorder => Tokens.SidebarActiveBorder;

        /// <summary>标签激活文字色</summary>
        public static Vector4 TagActiveText => Tokens.TagActiveText;
    }

    // ── 圆角 (Ant Design 5) ──

    /// <summary>超小圆角</summary>
    public const float RadiusXS = 3f;
    /// <summary>小圆角</summary>
    public const float RadiusSM = 6f;
    /// <summary>中圆角</summary>
    public const float RadiusMD = 8f;
    /// <summary>大圆角</summary>
    public const float RadiusLG = 12f;

    // ── 间距 ──

    /// <summary>超小间距</summary>
    public static readonly Vector2 PaddingXS  = new(6, 3);
    /// <summary>小间距</summary>
    public static readonly Vector2 PaddingSM  = new(10, 5);
    /// <summary>中间距</summary>
    public static readonly Vector2 PaddingMD  = new(16, 10);
    /// <summary>项目间距</summary>
    public static readonly Vector2 ItemSpacing = new(10, 6);

    // ── 字号 ──

    /// <summary>小字号</summary>
    public const float FontSizeSM = 13f;
    /// <summary>中字号</summary>
    public const float FontSizeMD = 14.5f;
    /// <summary>大字号</summary>
    public const float FontSizeLG = 18f;

    // ── 动画 ──

    /// <summary>动画速度</summary>
    public const float AnimSpeed = 12f;

    // ── 阴影 ──

    /// <summary>阴影偏移</summary>
    public const float ShadowOffset = 2f;
    /// <summary>阴影模糊</summary>
    public const float ShadowBlur = 8f;
}

/// <summary>
/// 主题令牌集合 — Ant Design 5 设计体系
/// </summary>
/// <summary>主题令牌集合 — Ant Design 5 设计体系</summary>
public class ThemeTokens
{
    // ── 背景 ──
    /// <summary>基础背景色</summary>
    public Vector4 BgBase;
    /// <summary>布局背景色</summary>
    public Vector4 BgLayout;
    /// <summary>容器背景色</summary>
    public Vector4 BgContainer;
    /// <summary>浮层背景色</summary>
    public Vector4 BgElevated;
    /// <summary>悬停背景色</summary>
    public Vector4 BgHover;
    /// <summary>高亮背景色</summary>
    public Vector4 BgSpotlight;

    // ── 毛玻璃叠加 ──
    /// <summary>毛玻璃底色</summary>
    public Vector4 GlassBg;
    /// <summary>毛玻璃高光色</summary>
    public Vector4 GlassHighlight;
    /// <summary>毛玻璃暗部色</summary>
    public Vector4 GlassShade;
    /// <summary>毛玻璃边框色</summary>
    public Vector4 GlassBorder;
    /// <summary>毛玻璃投影色</summary>
    public Vector4 GlassShadow;

    // ── 文字 ──
    /// <summary>主要文字色</summary>
    public Vector4 TextPrimary;
    /// <summary>次要文字色</summary>
    public Vector4 TextSecondary;
    /// <summary>第三级文字色</summary>
    public Vector4 TextTertiary;

    // ── 品牌色 ──
    /// <summary>品牌蓝</summary>
    public Vector4 AccentBlue;
    /// <summary>品牌绿</summary>
    public Vector4 AccentGreen;
    /// <summary>品牌红</summary>
    public Vector4 AccentRed;
    /// <summary>品牌橙</summary>
    public Vector4 AccentOrange;

    // ── 边框 ──
    /// <summary>边框色</summary>
    public Vector4 Border;
    /// <summary>次要边框色</summary>
    public Vector4 BorderSecondary;

    // ── 填充层级 ──
    /// <summary>一级填充色</summary>
    public Vector4 FillPrimary;
    /// <summary>二级填充色</summary>
    public Vector4 FillSecondary;
    /// <summary>三级填充色</summary>
    public Vector4 FillTertiary;

    // ── 开关 ──
    /// <summary>开关轨道开启色</summary>
    public Vector4 SwitchTrackOn;
    /// <summary>开关轨道关闭色</summary>
    public Vector4 SwitchTrackOff;
    /// <summary>开关旋钮色</summary>
    public Vector4 SwitchKnob;

    // ── 滑块 ──
    /// <summary>滑块轨道色</summary>
    public Vector4 SliderTrack;
    /// <summary>滑块滑轨色</summary>
    public Vector4 SliderRail;

    // ── 侧边栏 ──
    /// <summary>侧边栏激活色</summary>
    public Vector4 SidebarActive;
    /// <summary>侧边栏激活边框色</summary>
    public Vector4 SidebarActiveBorder;

    // ── 标签 ──
    /// <summary>标签激活文字色</summary>
    public Vector4 TagActiveText;
}