using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib.Effects;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 无边框 Overlay 窗口基类 — 半透明圆角背景 + 拖动 + 边缘缩放 + 位置持久化
/// 不依赖 ImGui WindowRounding，用 DrawList 手动绘制圆角背景铺满整个窗口
/// </summary>
public abstract class OverlayBase : Window
{
    /// <summary>插件配置</summary>
    protected readonly PluginConfig _config;
    private bool _isDragging;
    private bool _isResizing;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPos;
    private Vector2 _dragStartSize;
    private ResizeEdge _resizeEdge;
    private const float EdgeThickness = 6f;

    [Flags]
    private enum ResizeEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    /// <summary>是否允许缩放</summary>
    protected virtual bool AllowResize => true;
    /// <summary>内容边距</summary>
    protected virtual Vector2 ContentPadding => Theme.PaddingSM;

    /// <summary>Initializes a new instance of the <see cref="OverlayBase"/> class</summary>
    protected OverlayBase(string name, PluginConfig config) : base(name)
    {
        _config = config;
        Flags = ImGuiWindowFlags.NoTitleBar
              | ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoBackground; // 禁用 ImGui 默认背景绘制
        if (!AllowResize)
            Flags |= ImGuiWindowFlags.NoResize;
        IsOpen = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    /// <summary>绘制内容</summary>
    protected abstract void DrawContent();

    /// <summary>网格内容起始偏移（子类可覆写，默认 0）</summary>
    protected virtual Vector2 ContentOffset => Vector2.Zero;

    /// <summary>将光标设到 ContentOffset 位置</summary>
    protected void BeginContent() => ImGui.SetCursorPos(ContentOffset);

    /// <summary>列前进：未到末列调 SameLine，换行则重置 X 到 offset</summary>
    protected void SameLineOrWrap(ref int col, int cols)
    {
        col++;
        if (col < cols)
            ImGui.SameLine();
        else
        {
            col = 0;
            ImGui.SetCursorPosX(ContentOffset.X);
        }
    }

    /// <summary>预绘制</summary>
    public override void PreDraw()
    {
        // 子类在下一帧 Begin 前设定尺寸
        OnPreDraw();

        // 去掉默认边框（我们自己用 DrawList 画圆角边框）
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        // 关键：WindowPadding 用子类指定的 ContentPadding
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, ContentPadding);
    }

    /// <summary>后绘制</summary>
    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
    }

    /// <summary>在 PreDraw 中设置窗口尺寸</summary>
    protected virtual void OnPreDraw() { }

    /// <summary>绘制窗口</summary>
    public override void Draw()
    {
        using var themeScope = Theme.PushThemeScope();

        HandleInteraction();

        // 绘制铺满整个窗口的圆角半透明背景
        DrawWindowBackground();

        using var itemSp = new ImRaii.StyleDisposable();
        itemSp.Push(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4));

        DrawContent();

        // 右下角 resize 指示器（仅可缩放窗口）
        if (AllowResize && !_isDragging && !_isResizing)
        {
            var min = ImGui.GetWindowPos() + ImGui.GetWindowSize() - new Vector2(12, 12);
            ImGui.GetWindowDrawList().AddTriangleFilled(
                min + new Vector2(10, 10),
                min + new Vector2(10, 4),
                min + new Vector2(4, 10),
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.TextTertiary));
        }
    }

    /// <summary>
    /// 绘制窗口背景 — 铺满整个窗口，圆角6px
    /// </summary>
    private void DrawWindowBackground()
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        const float radius = Theme.RadiusMD;

        // ① 底层投影
        dl.AddRectFilled(min + new Vector2(0, 2), max + new Vector2(0, 2),
            ImGui.ColorConvertFloat4ToU32(Theme.Colors.GlassShadow), radius);

        // ② 主背景（半透明底色）
        dl.AddRectFilled(min, max,
            ImGui.ColorConvertFloat4ToU32(Theme.Colors.GlassBg), radius);

        // ②.5 微妙渐变叠加（顶部高光 → 底部暗部）
        GradientOverlay.DrawThemeGradient(dl, min, max, 16);

        // ③ 1px 细边框
        dl.AddRect(min, max,
            ImGui.ColorConvertFloat4ToU32(Theme.Colors.GlassBorder), radius, 0, 1.0f);
    }

    private void HandleInteraction()
    {
        var mousePos = ImGui.GetMousePos();
        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();

        if (!_isDragging && !_isResizing)
        {
            // 检测边缘 hover（仅可缩放窗口）
            if (AllowResize)
            {
                _resizeEdge = ResizeEdge.None;
                if (mousePos.X >= winPos.X && mousePos.X <= winPos.X + EdgeThickness)
                    _resizeEdge |= ResizeEdge.Left;
                if (mousePos.X >= winPos.X + winSize.X - EdgeThickness && mousePos.X <= winPos.X + winSize.X)
                    _resizeEdge |= ResizeEdge.Right;
                if (mousePos.Y >= winPos.Y && mousePos.Y <= winPos.Y + EdgeThickness)
                    _resizeEdge |= ResizeEdge.Top;
                if (mousePos.Y >= winPos.Y + winSize.Y - EdgeThickness && mousePos.Y <= winPos.Y + winSize.Y)
                    _resizeEdge |= ResizeEdge.Bottom;
            }

            // 鼠标不在窗口内 → 不处理
            var inside = mousePos.X >= winPos.X && mousePos.X <= winPos.X + winSize.X
                      && mousePos.Y >= winPos.Y && mousePos.Y <= winPos.Y + winSize.Y;
            if (!inside) return;

            // 用 IsMouseDragging 启动拖拽/缩放，避免 IsMouseClicked 与子控件按钮点击冲突
            // !IsAnyItemActive 防止与 Slider/拖拽控件 等已捕获鼠标的 ImGui 控件冲突
            if (_resizeEdge != ResizeEdge.None && ImGui.IsMouseDragging(ImGuiMouseButton.Left)
                && !ImGui.IsAnyItemActive())
            {
                _isResizing = true;
                _dragStartMouse = mousePos;
                _dragStartPos = winPos;
                _dragStartSize = winSize;
            }
            else if (_resizeEdge == ResizeEdge.None && ImGui.IsMouseDragging(ImGuiMouseButton.Left)
                && !ImGui.IsAnyItemActive())
            {
                _isDragging = true;
                _dragStartMouse = mousePos;
                _dragStartPos = winPos;
                _dragStartSize = winSize;
            }
        }

        if (_isResizing)
        {
            var delta = mousePos - _dragStartMouse;
            var newPos = _dragStartPos;
            var newSize = _dragStartSize;

            if (_resizeEdge.HasFlag(ResizeEdge.Left))
            {
                newPos.X = _dragStartPos.X + delta.X;
                newSize.X = _dragStartSize.X - delta.X;
            }
            if (_resizeEdge.HasFlag(ResizeEdge.Right))
                newSize.X = _dragStartSize.X + delta.X;
            if (_resizeEdge.HasFlag(ResizeEdge.Top))
            {
                newPos.Y = _dragStartPos.Y + delta.Y;
                newSize.Y = _dragStartSize.Y - delta.Y;
            }
            if (_resizeEdge.HasFlag(ResizeEdge.Bottom))
                newSize.Y = _dragStartSize.Y + delta.Y;

            // 使用 SizeConstraints 做最小尺寸限制（子类每帧更新它）
            float minW = SizeConstraints?.MinimumSize.X ?? 320;
            float minH = SizeConstraints?.MinimumSize.Y ?? 56;

            if (newSize.X < minW) { newSize.X = minW; if (_resizeEdge.HasFlag(ResizeEdge.Left)) newPos.X = _dragStartPos.X + _dragStartSize.X - minW; }
            if (newSize.Y < minH) { newSize.Y = minH; if (_resizeEdge.HasFlag(ResizeEdge.Top)) newPos.Y = _dragStartPos.Y + _dragStartSize.Y - minH; }

            ImGui.SetWindowPos(newPos);
            ImGui.SetWindowSize(newSize);

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _isResizing = false;
                SavePosition(ImGui.GetWindowPos());
            }
        }

        if (_isDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                ImGui.SetWindowPos(_dragStartPos + (mousePos - _dragStartMouse));
            else
            {
                _isDragging = false;
                SavePosition(ImGui.GetWindowPos());
            }
        }
    }

    /// <summary>保存窗口位置</summary>
    protected abstract void SavePosition(Vector2 pos);
}