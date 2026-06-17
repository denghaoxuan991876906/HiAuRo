using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 组件级交互特效工具 — 悬停过渡、状态脉冲、文字发光
/// </summary>
public static class EffectUtils
{
    /// <summary>
    /// 计算悬停过渡进度（0~1），需要在每帧调用中传入同一 key 维护状态
    /// </summary>
    public static float HoverTransition(string key, bool isHovered, float speed = 10f)
    {
        if (!_hoverStates.TryGetValue(key, out var progress))
            progress = 0f;

        var dt = ImGui.GetIO().DeltaTime;
        var target = isHovered ? 1f : 0f;
        progress += (target - progress) * Math.Min(1f, speed * dt);
        progress = Math.Clamp(progress, 0f, 1f);
        _hoverStates[key] = progress;
        return progress;
    }

    /// <summary>
    /// 计算状态脉冲亮度（用于呼吸发光效果）
    /// </summary>
    /// <param name="frequency">脉冲频率（Hz）</param>
    /// <returns>0~1 的亮度值</returns>
    public static float StatePulse(float frequency = 2f)
    {
        return (MathF.Sin((float)ImGui.GetTime() * frequency * MathF.PI * 2f) + 1f) * 0.5f;
    }

    /// <summary>
    /// 绘制发光文字 — 在文字周围偏移绘制多层半透明版本
    /// </summary>
    public static void DrawGlowText(ImDrawListPtr dl, Vector2 pos, string text,
        Vector4 color, Vector4? glowColor = null, float glowSize = 2f)
    {
        var gc = glowColor ?? new Vector4(color.X, color.Y, color.Z, color.W * 0.3f);
        var glowU32 = ImGui.ColorConvertFloat4ToU32(gc);
        var textU32 = ImGui.ColorConvertFloat4ToU32(color);

        // 8 方向偏移绘制发光层
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4f;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * glowSize;
            dl.AddText(pos + offset, glowU32, text);
        }

        // 主文字
        dl.AddText(pos, textU32, text);
    }

    public static uint PackColor(float r, float g, float b, float a)
    {
        return (uint)((byte)Math.Clamp(a * 255f, 0, 255) << 24
                      | (byte)Math.Clamp(b * 255f, 0, 255) << 16
                      | (byte)Math.Clamp(g * 255f, 0, 255) << 8
                      | (byte)Math.Clamp(r * 255f, 0, 255));
    }

    private static readonly Dictionary<string, float> _hoverStates = new();
}
