using System.Numerics;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 缓动函数集合 — 标准 t∈[0,1] 输入输出
/// </summary>
public static class Easing
{
    public static float EaseInQuad(float t) => t * t;

    public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    public static float EaseInOutQuad(float t) =>
        t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;

    public static float EaseInCubic(float t) => t * t * t;

    public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

    public static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        var c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
    }
}
