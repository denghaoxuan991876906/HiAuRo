using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 纯色背景叠加 — 替代原来的 32 段渐变
/// </summary>
public static class GradientOverlay
{
    /// <summary>
    /// 绘制主题纯色叠加（原渐变已去除）
    /// </summary>
    public static void DrawThemeGradient(ImDrawListPtr dl, Vector2 min, Vector2 max, int segments = 32)
    {
        dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(Theme.Colors.GlassHighlight));
    }
}
