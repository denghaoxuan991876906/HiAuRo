using System.Numerics;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 通用 Lerp / Easing 动画工具
/// </summary>
public static class AnimationHelper
{
    /// <summary>线性插值（float）</summary>
    public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);

    /// <summary>线性插值（Vector2）</summary>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) =>
        new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));

    /// <summary>线性插值（Vector4）</summary>
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t) =>
        new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Z, b.Z, t), Lerp(a.W, b.W, t));

    /// <summary>平滑跟随 — 每帧调用，current 逐步逼近 target</summary>
    public static float SmoothLerp(ref float current, float target, float speed)
    {
        var dt = ImGui.GetIO().DeltaTime;
        current = Lerp(current, target, 1f - MathF.Exp(-speed * dt));
        return current;
    }

    /// <summary>平滑跟随 — Vector4</summary>
    public static Vector4 SmoothLerp(ref Vector4 current, Vector4 target, float speed)
    {
        var dt = ImGui.GetIO().DeltaTime;
        current = Lerp(current, target, 1f - MathF.Exp(-speed * dt));
        return current;
    }
}
