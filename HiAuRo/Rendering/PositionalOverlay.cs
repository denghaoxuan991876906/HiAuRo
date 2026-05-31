using System.Numerics;
using Dalamud.Interface.Windowing;
using OmenTools.Interop.Game.Helpers;
using OmenTools.OmenService;

namespace HiAuRo.Rendering;

/// <summary>
/// ImGui 全视口叠加层 — 绘制目标攻击范围圈和自动攻击范围圈
/// VfxObject 环宽不可调，改用 ImGui 精确控制线条粗细
/// </summary>
public sealed class PositionalOverlay : Window
{
    public PositionalOverlay() : base("##HiAuRoPositional",
        ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration |
        ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoBringToFrontOnFocus |
        ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings,
        true)
    {
        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public override void Draw()
    {
        var cfg = PluginConfig.Instance;
        if (cfg == null || !cfg.ShowPositional) return;

        var target = TargetManager.Target;
        if (target == null) return;

        var dl = ImGui.GetForegroundDrawList();
        var hitboxR = target.HitboxRadius;
        var center = target.Position;

        if (cfg.ShowTargetHitbox)
        {
            var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0.298f, 0.686f, 0.314f, 0.5f));
            DrawWorldCircle(dl, center, hitboxR, color, 1f, 64);
        }

        if (cfg.ShowAutoAttackRange)
        {
            var color = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.8f, 0.2f, 0.35f));
            DrawWorldCircle(dl, center, 3f, color, 1f, 48);
        }
    }

    static void DrawWorldCircle(ImDrawListPtr dl, Vector3 center, float radius, uint color, float thickness, int segments)
    {
        var points = new Vector2[segments + 1];
        var validCount = 0;

        for (var i = 0; i <= segments; i++)
        {
            var angle = MathF.PI * 2f * i / segments;
            var worldPos = center + new Vector3(MathF.Cos(angle) * radius, 0, MathF.Sin(angle) * radius);
            if (GameViewHelper.WorldToScreen(worldPos, out var screenPos, out _))
            {
                points[validCount] = screenPos;
                validCount++;
            }
        }

        for (var i = 0; i < validCount - 1; i++)
            dl.AddLine(points[i], points[i + 1], color, thickness);
        if (validCount > 2)
            dl.AddLine(points[validCount - 1], points[0], color, thickness);
    }
}
