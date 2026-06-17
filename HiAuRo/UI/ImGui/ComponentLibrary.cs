using System.Numerics;
using Dalamud.Interface;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ImGui 组件库 — 复刻 Ant Design 5 按钮/开关/卡片等组件
/// 参考: https://ant-design.antgroup.com/components/button-cn
/// </summary>
public static class ComponentLibrary
{
    // ═══════════════════════════════════════════════════
    // 颜色工具
    // ═══════════════════════════════════════════════════

    private static Vector4 Lighten(Vector4 c, float amount)
    {
        return new Vector4(
            Math.Min(c.X + amount, 1f),
            Math.Min(c.Y + amount, 1f),
            Math.Min(c.Z + amount, 1f),
            c.W);
    }

    private static Vector4 Darken(Vector4 c, float amount)
    {
        return new Vector4(
            Math.Max(c.X - amount, 0f),
            Math.Max(c.Y - amount, 0f),
            Math.Max(c.Z - amount, 0f),
            c.W);
    }

    private static uint ColorU32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    // ═══════════════════════════════════════════════════
    // 毛玻璃背景 + 阴影
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 半透明圆角背景 + 阴影 — 铺满整个窗口
    /// </summary>
    public static void GlassBackground(float cornerRadius)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();

        // 底层阴影
        dl.AddRectFilled(min + new Vector2(0, 2), max + new Vector2(0, 2),
            ColorU32(Theme.Colors.GlassShadow), cornerRadius);

        // 半透明底色 — 铺满整个窗口
        dl.AddRectFilled(min, max,
            ColorU32(Theme.Colors.GlassBg), cornerRadius);

        // 1px 细边框
        dl.AddRect(min, max,
            ColorU32(Theme.Colors.GlassBorder), cornerRadius, 0, 1.0f);
    }

    // ═══════════════════════════════════════════════════
    // 图标集 (DrawList 矢量绘制)
    // ═══════════════════════════════════════════════════

    /// <summary>图标类型</summary>
    public enum IconType
    {
        /// <summary>播放</summary>
        Play,
        /// <summary>停止</summary>
        Stop,
        /// <summary>暂停</summary>
        Pause,
        /// <summary>保存</summary>
        Save,
        /// <summary>向下箭头</summary>
        ChevronDown,
        /// <summary>向上箭头</summary>
        ChevronUp,
        /// <summary>关闭</summary>
        Close,
        /// <summary>眼睛/可见</summary>
        Eye
    }

    /// <summary>在指定中心绘制图标 (图标尺寸约 12~14px)</summary>
    private static void DrawIcon(ImDrawListPtr dl, Vector2 center, IconType icon, uint color, float sizePx = 18f)
    {
        var iconChar = icon switch
        {
            IconType.Play => IconHelper.Icons.Play,
            IconType.Stop => IconHelper.Icons.Stop,
            IconType.Pause => IconHelper.Icons.Pause,
            IconType.Save => IconHelper.Icons.Save,
            IconType.ChevronDown => IconHelper.Icons.ArrowDown,
            IconType.ChevronUp => IconHelper.Icons.ArrowUp,
            IconType.Close => IconHelper.Icons.Cross,
            IconType.Eye => IconHelper.Icons.UiVisible,
            _ => null
        };

        if (iconChar == null) return;

        IconHelper.DrawIcon(dl, center, iconChar, color, sizePx);
    }

    // ═══════════════════════════════════════════════════
    // 图标按钮 (AntdUI Icon Button 风格)
    // ═══════════════════════════════════════════════════

    /// <summary>图标按钮样式</summary>
    public enum IconButtonStyle
    {
        /// <summary>填充样式</summary>
        Fill,
        /// <summary>边框样式</summary>
        Outline,
        /// <summary>文字样式</summary>
        Text
    }

    /// <summary>
    /// AntdUI 风格图标按钮 — 大尺寸、大圆角、清晰图标
    /// size 最小建议 52×36 (战斗易点击)
    /// </summary>
    public static bool IconButton(IconType icon, Vector4 color, Vector2 size,
        IconButtonStyle style = IconButtonStyle.Fill, float iconSizePx = 18f)
    {
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(10, 8));

        Vector4 bg, bgHover, bgActive, borderCol, iconCol;

        if (style == IconButtonStyle.Fill)
        {
            bg = color;
            bgHover = Lighten(color, 0.12f);
            bgActive = Darken(color, 0.12f);
            borderCol = color;
            iconCol = new Vector4(1, 1, 1, 1);
        }
        else if (style == IconButtonStyle.Outline)
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = color;
            iconCol = color;
        }
        else // Text
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = Vector4.Zero;
            iconCol = color;
        }

        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Button, bg);
        c.Push(ImGuiCol.ButtonHovered, bgHover);
        c.Push(ImGuiCol.ButtonActive, bgActive);
        c.Push(ImGuiCol.Border, borderCol, style == IconButtonStyle.Outline);

        var clicked = ImGui.Button($"##icon_{icon}", size);

        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        var center = (rectMin + rectMax) / 2;
        DrawIcon(ImGui.GetWindowDrawList(), center, icon, ColorU32(iconCol), iconSizePx);

        return clicked;
    }

    // ═══════════════════════════════════════════════════
    // Ant Design 5 按钮变体
    // ═══════════════════════════════════════════════════

    /// <summary>主按钮（蓝色填充）</summary>
    public static bool PrimaryButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentBlue, size);

    /// <summary>危险按钮（红色填充）</summary>
    public static bool DangerButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentRed, size);

    /// <summary>成功按钮（绿色填充）</summary>
    public static bool SuccessButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentGreen, size);

    /// <summary>警告按钮（橙色填充）</summary>
    public static bool WarningButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentOrange, size);

    /// <summary>默认按钮（白底灰边）</summary>
    public static bool DefaultButton(string label, Vector2? size = null)
        => OutlineButton(label, Theme.Colors.Border, Theme.Colors.TextPrimary, size);

    /// <summary>文字按钮（透明背景）</summary>
    public static bool TextButton(string label, Vector2? size = null)
        => GhostButton(label, Theme.Colors.AccentBlue, size);

    /// <summary>链接按钮</summary>
    public static bool LinkButton(string label, Vector2? size = null)
    {
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Button, Vector4.Zero);
        c.Push(ImGuiCol.ButtonHovered, Vector4.Zero);
        c.Push(ImGuiCol.ButtonActive, Vector4.Zero);
        c.Push(ImGuiCol.Text, Theme.Colors.AccentBlue);

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        if (ImGui.IsItemHovered())
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            dl.AddLine(new Vector2(min.X, max.Y - 1), new Vector2(max.X, max.Y - 1),
                ColorU32(Theme.Colors.AccentBlue), 1);
        }

        return clicked;
    }

    // ── 内部: 填充按钮 ──
    private static bool FillButton(string label, Vector4 color, Vector2? size)
    {
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(16, 8));
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Button, color);
        c.Push(ImGuiCol.ButtonHovered, Lighten(color, 0.12f));
        c.Push(ImGuiCol.ButtonActive, Darken(color, 0.12f));
        c.Push(ImGuiCol.Text, new Vector4(1, 1, 1, 1));

        return ImGui.Button(label, size ?? Vector2.Zero);
    }

    // ── 内部: 边框按钮 ──
    private static bool OutlineButton(string label, Vector4 borderColor, Vector4 textColor, Vector2? size)
    {
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(16, 8));
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Button, Vector4.Zero);
        c.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        c.Push(ImGuiCol.ButtonActive, Theme.Colors.FillSecondary);
        c.Push(ImGuiCol.Border, borderColor);
        c.Push(ImGuiCol.Text, textColor);

        return ImGui.Button(label, size ?? Vector2.Zero);
    }

    // ── 内部: Ghost 幽灵按钮 ──
    private static bool GhostButton(string label, Vector4 textColor, Vector2? size)
    {
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(16, 8));
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Button, Vector4.Zero);
        c.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        c.Push(ImGuiCol.ButtonActive, Theme.Colors.FillPrimary);
        c.Push(ImGuiCol.Text, textColor);

        return ImGui.Button(label, size ?? Vector2.Zero);
    }

    /// <summary>旧版 Button（等同于 PrimaryButton）</summary>
    public static bool Button(string label, Vector2? size = null, bool disabled = false)
        => PrimaryButton(label, size);

    /// <summary>自定义颜色填充按钮</summary>
    public static bool AccentButton(string label, Vector4 color, Vector2? size = null)
        => FillButton(label, color, size);

    /// <summary>旧版 OutlineButton（等同于 DefaultButton）</summary>
    public static bool OutlineButton(string label, Vector2? size = null)
        => DefaultButton(label, size);

    // ═══════════════════════════════════════════════════
    // 图标 + 文字 组合按钮 (AntdUI 风格)
    // ═══════════════════════════════════════════════════

    /// <summary>图标 + 文字按钮 (例如 "▶ 启动")</summary>
    public static bool IconTextButton(IconType icon, string label, Vector4 color,
        Vector2? size = null, IconButtonStyle style = IconButtonStyle.Fill, float iconSizePx = 18f)
    {
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(14, 8));
        v.Push(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6, 0));

        Vector4 bg, bgHover, bgActive, borderCol, iconCol, textCol;

        if (style == IconButtonStyle.Fill)
        {
            bg = color;
            bgHover = Lighten(color, 0.12f);
            bgActive = Darken(color, 0.12f);
            borderCol = color;
            iconCol = new Vector4(1, 1, 1, 1);
            textCol = new Vector4(1, 1, 1, 1);
        }
        else if (style == IconButtonStyle.Outline)
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = color;
            iconCol = color;
            textCol = color;
        }
        else
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = Vector4.Zero;
            iconCol = color;
            textCol = color;
        }

        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Button, bg);
        c.Push(ImGuiCol.ButtonHovered, bgHover);
        c.Push(ImGuiCol.ButtonActive, bgActive);
        c.Push(ImGuiCol.Border, borderCol, style == IconButtonStyle.Outline);
        c.Push(ImGuiCol.Text, textCol);

        var clicked = ImGui.Button($"##it_{icon}_{label}", size ?? Vector2.Zero);

        // 在按钮区域内绘制图标 + 文字
        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        var center = (rectMin + rectMax) / 2;
        var textSize = ImGui.CalcTextSize(label);
        var totalW = 14 + 4 + textSize.X; // icon + gap + text
        var startX = center.X - totalW / 2 + 7;

        // 绘制图标
        DrawIcon(ImGui.GetWindowDrawList(), new Vector2(startX, center.Y), icon, ColorU32(iconCol), iconSizePx);

        // 绘制文字
        ImGui.GetWindowDrawList().AddText(
            new Vector2(startX + 10, center.Y - textSize.Y / 2),
            ColorU32(textCol),
            label);

        return clicked;
    }

    // ═══════════════════════════════════════════════════
    // 分割竖线
    // ═══════════════════════════════════════════════════

    /// <summary>分割竖线</summary>
    public static void VSplit()
    {
        ImGui.TextColored(Theme.Colors.BorderSecondary, "│");
    }

    // ═══════════════════════════════════════════════════
    // 开关 (AntdUI Switch 风格)
    // ═══════════════════════════════════════════════════

    /// <summary>开关控件</summary>
    public static bool Switch(string id, ref bool value)
    {
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.FrameBg, value ? Theme.Colors.SwitchTrackOn : Theme.Colors.SwitchTrackOff);
        c.Push(ImGuiCol.CheckMark, Theme.Colors.SwitchKnob);
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, 12f);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(4, 3));

        return ImGui.Checkbox($"##{id}", ref value);
    }

    /// <summary>带标签的开关控件（labelFirst=true 标签在左，false 标签在右）</summary>
    public static bool Switch(string id, string label, ref bool value, bool labelFirst = true)
    {
        if (labelFirst)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Theme.Colors.TextPrimary, label);
            ImGui.SameLine();
            return Switch(id, ref value);
        }
        else
        {
            var changed = Switch(id, ref value);
            ImGui.SameLine();
            ImGui.TextColored(Theme.Colors.TextPrimary, label);
            return changed;
        }
    }

    // ═══════════════════════════════════════════════════
    // 滑块
    // ═══════════════════════════════════════════════════

    /// <summary>滑块控件</summary>
    public static bool Slider(string id, string label, ref float value, float min, float max, string format = "%.1f")
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var labelWidth = ImGui.CalcTextSize(label).X + 4;
        var sliderWidth = Math.Clamp(avail - labelWidth - 8, 60, 260);

        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.FrameBg, Theme.Colors.SliderRail);
        c.Push(ImGuiCol.SliderGrab, Theme.Colors.SliderTrack);
        c.Push(ImGuiCol.SliderGrabActive, Theme.Colors.SliderTrack);
        c.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        ImGui.SetNextItemWidth(sliderWidth);
        var changed = ImGui.SliderFloat($"##{id}", ref value, min, max, format);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    /// <summary>整数滑块控件</summary>
    public static bool SliderInt(string id, string label, ref int value, int min, int max)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var labelWidth = ImGui.CalcTextSize(label).X + 4;
        var sliderWidth = Math.Clamp(avail - labelWidth - 8, 60, 260);

        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.FrameBg, Theme.Colors.SliderRail);
        c.Push(ImGuiCol.SliderGrab, Theme.Colors.SliderTrack);
        c.Push(ImGuiCol.SliderGrabActive, Theme.Colors.SliderTrack);
        c.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        ImGui.SetNextItemWidth(sliderWidth);
        var changed = ImGui.SliderInt($"##{id}", ref value, min, max);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    // ═══════════════════════════════════════════════════
    // 下拉选择器
    // ═══════════════════════════════════════════════════

    /// <summary>下拉选择器</summary>
    public static bool Select(string id, string label, ref int selectedIndex, string[] options)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var labelWidth = label.Length > 0 ? ImGui.CalcTextSize(label).X + 4 : 0;
        var longestOpt = options.Length > 0 ? options.Max(o => ImGui.CalcTextSize(o).X) + 40 : 100;
        var comboWidth = label.Length > 0
            ? Math.Clamp(avail - labelWidth - 8, 80, Math.Max(240, longestOpt))
            : Math.Clamp(longestOpt, 80, 300);

        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.FrameBg, Theme.Colors.FillSecondary);
        c.Push(ImGuiCol.FrameBgHovered, Theme.Colors.FillPrimary);
        c.Push(ImGuiCol.FrameBgActive, Theme.Colors.FillPrimary);
        c.Push(ImGuiCol.Header, Theme.Colors.AccentBlue);
        c.Push(ImGuiCol.HeaderHovered, Theme.Colors.BgHover);
        c.Push(ImGuiCol.HeaderActive, Theme.Colors.AccentBlue);
        c.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        c.Push(ImGuiCol.PopupBg, Theme.Colors.BgElevated);
        c.Push(ImGuiCol.Border, Theme.Colors.Border);
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.SetNextItemWidth(comboWidth);
        var changed = ImGui.Combo($"##{id}", ref selectedIndex, options, options.Length);
        if (label.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.Colors.TextSecondary, label);
        }
        return changed;
    }

    // ═══════════════════════════════════════════════════
    // 标签页
    // ═══════════════════════════════════════════════════

    /// <summary>标签页控件</summary>
    public static bool Tabs(string id, ref int activeTab, string[] tabNames)
    {
        var changed = false;
        if (ImGui.BeginTabBar($"##tabs_{id}", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            for (var i = 0; i < tabNames.Length; i++)
            {
                if (ImGui.BeginTabItem(tabNames[i]))
                {
                    if (i != activeTab) changed = true;
                    activeTab = i;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        return changed;
    }

    // ═══════════════════════════════════════════════════
    // 卡片 — 自适应高度，标题与卡片体一体化
    // ═══════════════════════════════════════════════════

    private static int _cardCounter;

    /// <summary>
    /// 自适应高度卡片 — 标题栏与卡片体共享同一圆角矩形容器，高度跟随内容。<br/>
    /// 用 BeginGroup/EndGroup 测量内容实际尺寸，draw channel 分层让 header 背景在文字下层渲染，
    /// 解决“标题被卡片分割线切割/在多卡间突兀”的问题。不依赖 child window。
    /// </summary>
    public static void Card(string title, Action content)
    {
        var width = Math.Max(ImGui.GetContentRegionAvail().X, 1f);
        var pos = ImGui.GetCursorScreenPos();   // 卡片左上
        var padX = 14f;
        var headerH = ImGui.GetTextLineHeight() + 16f;
        var dl = ImGui.GetWindowDrawList();

        dl.ChannelsSplit(2);
        try
        {
            dl.ChannelsSetCurrent(1);   // 内容层（合并时后渲染，盖在背景上）

            ImGui.BeginGroup();

            // ── 标题 ──
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
            using (ImRaii.PushFont(UiBuilder.DefaultFont))
                ImGui.TextColored(Theme.Colors.TextPrimary, title);

            // ── 内容 ──
            ImGui.Dummy(new Vector2(0, 8f));
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
            using (var contentSp = new ImRaii.StyleDisposable())
            {
                contentSp.Push(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
                ImGui.BeginGroup();
                try
                {
                    content();
                }
                catch (Exception ex)
                {
                    ImGui.TextColored(Theme.Colors.AccentRed, $"Card 渲染异常: {ex.Message}");
                }
                ImGui.EndGroup();
            }

            // 底部 padding + 撑满宽度（让 Group bbox 宽度 = width）
            ImGui.Dummy(new Vector2(width, 10f));

            ImGui.EndGroup();

            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var cardW = Math.Max(width, max.X - min.X);

            // ── 回填装饰到背景层 ──
            dl.ChannelsSetCurrent(0);
            var bgMin = pos;
            var bgMax = new Vector2(pos.X + cardW, max.Y);
            // 卡片底色
            dl.AddRectFilled(bgMin, bgMax,
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.BgContainer),
                Theme.RadiusMD);
            // header 背景（仅顶部圆角）
            dl.AddRectFilled(bgMin, new Vector2(bgMax.X, bgMin.Y + headerH),
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.FillTertiary),
                Theme.RadiusMD, ImDrawFlags.RoundCornersTop);
            // header 分隔线
            dl.AddLine(new Vector2(bgMin.X, bgMin.Y + headerH),
                       new Vector2(bgMax.X, bgMin.Y + headerH),
                       ImGui.ColorConvertFloat4ToU32(Theme.Colors.BorderSecondary), 1f);
            // 卡片边框
            dl.AddRect(bgMin, bgMax,
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.BorderSecondary),
                Theme.RadiusMD, 0, 1f);
        }
        finally
        {
            dl.ChannelsMerge();
        }

        ImGui.Spacing();
    }

    /// <summary>自适应高度卡片（无标题栏，仅圆角背景容器）。</summary>
    public static void Card(Action content)
    {
        var width = Math.Max(ImGui.GetContentRegionAvail().X, 1f);
        var pos = ImGui.GetCursorScreenPos();
        var padX = 14f;
        var padY = 12f;
        var dl = ImGui.GetWindowDrawList();

        dl.ChannelsSplit(2);
        try
        {
            dl.ChannelsSetCurrent(1);

            ImGui.BeginGroup();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + padY / 2f);
            using (var contentSp = new ImRaii.StyleDisposable())
            {
                contentSp.Push(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
                ImGui.BeginGroup();
                try
                {
                    content();
                }
                catch (Exception ex)
                {
                    ImGui.TextColored(Theme.Colors.AccentRed, $"Card 渲染异常: {ex.Message}");
                }
                ImGui.EndGroup();
            }

            ImGui.Dummy(new Vector2(width, padY));

            ImGui.EndGroup();

            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var cardW = Math.Max(width, max.X - min.X);

            dl.ChannelsSetCurrent(0);
            var bgMin = pos;
            var bgMax = new Vector2(pos.X + cardW, max.Y);
            dl.AddRectFilled(bgMin, bgMax,
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.BgContainer),
                Theme.RadiusMD);
            dl.AddRect(bgMin, bgMax,
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.BorderSecondary),
                Theme.RadiusMD, 0, 1f);
        }
        finally
        {
            dl.ChannelsMerge();
        }

        ImGui.Spacing();
    }

    // ═══════════════════════════════════════════════════
    // 标签芯片 (Tag)
    // ═══════════════════════════════════════════════════

    /// <summary>标签芯片</summary>
    public static void Tag(string label, bool active, Vector4? activeColor = null, Vector4? inactiveColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : (inactiveColor ?? Theme.Colors.FillSecondary);
        var textColor = active ? Theme.Colors.TagActiveText : Theme.Colors.TextSecondary;
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        v.Push(ImGuiStyleVar.FramePadding, new Vector2(12, 6));
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Button, color);
        c.Push(ImGuiCol.ButtonHovered, color);
        c.Push(ImGuiCol.ButtonActive, color);
        c.Push(ImGuiCol.Text, textColor);
        ImGui.Button(label, Vector2.Zero);
    }

    // ═══════════════════════════════════════════════════
    // 分割线
    // ═══════════════════════════════════════════════════

    /// <summary>章节标题 + 分割线</summary>
    public static void SectionHeader(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.Colors.AccentBlue, title);
        ImGui.Separator();
    }

    /// <summary>分割线</summary>
    public static void Divider()
    {
        ImGui.Spacing();
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.Separator, Theme.Colors.BorderSecondary);
        ImGui.Separator();
        ImGui.Spacing();
    }

    // ═══════════════════════════════════════════════════
    // Badge 徽标
    // ═══════════════════════════════════════════════════

    /// <summary>徽标</summary>
    public static void Badge(bool active, Vector4? activeColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : Theme.Colors.TextTertiary;
        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var centerY = cursor.Y + lineHeight / 2;
        dl.AddCircleFilled(new Vector2(cursor.X + 6, centerY), 4, ColorU32(color));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16);
    }

    /// <summary>状态圆点</summary>
    public static void StatusDot(bool active, string label, Vector4? activeColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : Theme.Colors.TextTertiary;
        var symbol = active ? "●" : "○";
        ImGui.TextColored(color, symbol);
        ImGui.SameLine();
        ImGui.TextColored(active ? Theme.Colors.TextPrimary : Theme.Colors.TextSecondary, label);
    }

    // ═══════════════════════════════════════════════════
    // 数字输入
    // ═══════════════════════════════════════════════════

    /// <summary>数字输入框（labelFirst=true 标签在左，false 标签在右）</summary>
    public static bool InputNumber(string id, string label, ref int value, int step = 1, int stepFast = 10, bool labelFirst = true)
    {
        var inputWidth = 120f;

        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.FrameBg, Theme.Colors.FillSecondary);
        c.Push(ImGuiCol.FrameBgHovered, Theme.Colors.FillPrimary);
        c.Push(ImGuiCol.FrameBgActive, Theme.Colors.FillPrimary);
        c.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        c.Push(ImGuiCol.SliderGrab, Theme.Colors.TextPrimary);
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);

        if (labelFirst)
        {
            ImGui.TextColored(Theme.Colors.TextPrimary, label);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(inputWidth);
            return ImGui.InputInt($"##{id}", ref value, step, stepFast);
        }
        else
        {
            ImGui.SetNextItemWidth(inputWidth);
            var changed = ImGui.InputInt($"##{id}", ref value, step, stepFast);
            ImGui.SameLine();
            ImGui.TextColored(Theme.Colors.TextPrimary, label);
            return changed;
        }
    }

    // ═══════════════════════════════════════════════════
    // 文本输入
    // ═══════════════════════════════════════════════════

    /// <summary>文本输入框</summary>
    public static bool InputText(string id, string label, ref string value, int maxLength, float width = 180f)
    {
        using var c = new ImRaii.ColorDisposable();
        c.Push(ImGuiCol.FrameBg, Theme.Colors.FillSecondary);
        c.Push(ImGuiCol.FrameBgHovered, Theme.Colors.FillPrimary);
        c.Push(ImGuiCol.FrameBgActive, Theme.Colors.FillPrimary);
        c.Push(ImGuiCol.Text, Theme.Colors.TextPrimary);
        using var v = new ImRaii.StyleDisposable();
        v.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);

        if (label.Length > 0)
        {
            ImGui.TextColored(Theme.Colors.TextPrimary, label);
            ImGui.SameLine();
        }
        ImGui.SetNextItemWidth(width);
        return ImGui.InputText($"##{id}", ref value, maxLength);
    }

    // ═══════════════════════════════════════════════════
    // 折叠区域
    // ═══════════════════════════════════════════════════

    /// <summary>折叠区域</summary>
    public static void Collapsible(string title, Action content)
    {
        if (!ImGui.CollapsingHeader(title)) return;
        ImGui.Indent(16f);
        content();
        ImGui.Unindent(16f);
    }

    // ═══════════════════════════════════════════════════
    // 表单行
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 两列表单行 — 标签在左（固定宽度），控件在右
    /// </summary>
    public static void FormRow(string label, Action drawControl, float labelWidth = 160f)
    {
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        ImGui.SameLine(labelWidth);
        drawControl();
    }

    // ═══════════════════════════════════════════════════
    // 文本标签
    // ═══════════════════════════════════════════════════

    /// <summary>文本标签</summary>
    public static void Label(string text)
    {
        ImGui.TextColored(Theme.Colors.TextPrimary, text);
    }
}
