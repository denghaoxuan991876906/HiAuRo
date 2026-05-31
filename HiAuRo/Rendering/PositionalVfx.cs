using System.Numerics;
using HiAuRo.Vfx;
using OmenTools.OmenService;

namespace HiAuRo.Rendering;

/// <summary>
/// 身位 VFX 绘制 — 仅绘制身位扇形指示器 (Fan90/Fan120)
/// 命中圈和自动攻击圈已改用 ImGui 绘制 (PositionalOverlay)
/// </summary>
public static class PositionalVfx
{
    const string TagPositional = "pos_indicator";

    static float _lastTargetEntityId;
    static PositionalDir _lastDir = PositionalDir.None;
    static bool _lastCorrect;

    /// <summary>每帧更新 VFX</summary>
    public static void Update(float dt)
    {
        var cfg = PluginConfig.Instance;
        if (cfg == null) return;

        var renderer = VfxRenderer.Instance;
        if (renderer == null) return;

        PositionalState.Update(dt);

        var target = TargetManager.Target;
        var targetId = target?.EntityID ?? 0;
        var showIndicator = cfg.ShowPositional && PositionalState.ActiveDir != PositionalDir.None;

        if (showIndicator && target != null && target is IBattleChara bcInd)
        {
            var dirChanged = PositionalState.ActiveDir != _lastDir;
            var correctChanged = PositionalState.IsCorrectPosition != _lastCorrect;

            if (ShouldRefreshTarget(targetId) || dirChanged || correctChanged)
            {
                renderer.RemoveByTag(TagPositional);
                var facing = bcInd.Rotation;
                var radius = bcInd.HitboxRadius + 3f;
                var correct = PositionalState.IsCorrectPosition;
                var green = new Vector4(0.0f, 1.0f, 0.0f, 0.35f);
                var red = new Vector4(1.0f, 0.0f, 0.0f, 0.35f);
                var color = correct ? green : red;

                switch (PositionalState.ActiveDir)
                {
                    case PositionalDir.Behind:
                        renderer.Show(VfxPath.Fan90, bcInd.Position,
                            new Vector3(radius, 1f, radius), facing + MathF.PI,
                            duration: -1f, tag: TagPositional, color: color);
                        break;
                    case PositionalDir.Flank:
                        renderer.Show(VfxPath.Fan120, bcInd.Position,
                            new Vector3(radius, 1f, radius), facing + MathF.PI / 2f,
                            duration: -1f, tag: TagPositional, color: color);
                        renderer.Show(VfxPath.Fan120, bcInd.Position,
                            new Vector3(radius, 1f, radius), facing - MathF.PI / 2f,
                            duration: -1f, tag: TagPositional, color: color);
                        break;
                }

                _lastDir = PositionalState.ActiveDir;
                _lastCorrect = PositionalState.IsCorrectPosition;
            }
        }
        else if (!showIndicator && _lastDir != PositionalDir.None)
        {
            renderer.RemoveByTag(TagPositional);
            _lastDir = PositionalDir.None;
            _lastCorrect = false;
        }

        _lastTargetEntityId = targetId;
    }

    static bool ShouldRefreshTarget(uint currentId)
    {
        return currentId != _lastTargetEntityId || _lastTargetEntityId == 0;
    }

    /// <summary>清除所有身位 VFX</summary>
    public static void Clear()
    {
        var renderer = VfxRenderer.Instance;
        if (renderer == null) return;
        renderer.RemoveByTag(TagPositional);
        _lastDir = PositionalDir.None;
        _lastCorrect = false;
        _lastTargetEntityId = 0;
        PositionalState.Clear();
    }
}
