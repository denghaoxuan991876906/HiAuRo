using System.Numerics;
using HiAuRo.Vfx;
using OmenTools.OmenService;

namespace HiAuRo.Rendering;

/// <summary>
/// 身位 VFX 绘制 — 在目标周围绘制攻击范围圈和身位扇形指示器
/// </summary>
public static class PositionalVfx
{
    const string TagPositional = "pos_indicator";
    const string TagHitbox = "pos_hitbox";
    const string TagAutoAttack = "pos_aa";

    static float _lastTargetEntityId;
    static PositionalDir _lastDir = PositionalDir.None;
    static bool _lastCorrect;
    static bool _lastShowHitbox;
    static bool _lastShowAA;

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

        var showHitbox = cfg.ShowPositional && cfg.ShowTargetHitbox;
        var showAA = cfg.ShowPositional && cfg.ShowAutoAttackRange;
        var showIndicator = cfg.ShowPositional && PositionalState.ActiveDir != PositionalDir.None;

        // 命中圈
        if (showHitbox && target != null && target is IBattleChara bc)
        {
            var hitboxR = bc.HitboxRadius;
            if (ShouldRefreshTarget(targetId) || !_lastShowHitbox)
            {
                renderer.RemoveByTag(TagHitbox);
                var innerR = MathF.Max(0.1f, hitboxR - 0.15f);
                renderer.ShowRing(bc.Position, innerR, hitboxR, duration: -1f, tag: TagHitbox,
                    color: new Vector4(0.3f, 1.0f, 0.3f, 0.35f));
                _lastShowHitbox = true;
            }
        }
        else if (!showHitbox && _lastShowHitbox)
        {
            renderer.RemoveByTag(TagHitbox);
            _lastShowHitbox = false;
        }

        // 自动攻击圈
        if (showAA && target != null && target is IBattleChara bcAA)
        {
            if (ShouldRefreshTarget(targetId) || !_lastShowAA)
            {
                renderer.RemoveByTag(TagAutoAttack);
                var aaRange = 3f;
                renderer.ShowRing(bcAA.Position, aaRange - 0.15f, aaRange, duration: -1f, tag: TagAutoAttack,
                    color: new Vector4(1.0f, 0.8f, 0.2f, 0.3f));
                _lastShowAA = true;
            }
        }
        else if (!showAA && _lastShowAA)
        {
            renderer.RemoveByTag(TagAutoAttack);
            _lastShowAA = false;
        }

        // 身位指示器
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
                    {
                        var behindFacing = facing + MathF.PI;
                        RenderFan(bcInd.Position, radius, behindFacing, color, TagPositional, VfxPath.Fan90);
                        break;
                    }
                    case PositionalDir.Flank:
                    {
                        RenderFan(bcInd.Position, radius, facing + MathF.PI / 2f, color, TagPositional, VfxPath.Fan120);
                        RenderFan(bcInd.Position, radius, facing - MathF.PI / 2f, color, TagPositional, VfxPath.Fan120);
                        break;
                    }
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

    static void RenderFan(Vector3 pos, float radius, float rotation, Vector4 color, string tag, string vfxPath)
    {
        VfxRenderer.Instance?.Show(vfxPath, pos,
            new Vector3(radius, 1f, radius), rotation,
            duration: -1f, tag: tag, color: color);
    }

    /// <summary>清除所有身位 VFX</summary>
    public static void Clear()
    {
        var renderer = VfxRenderer.Instance;
        if (renderer == null) return;
        renderer.RemoveByTag(TagPositional);
        renderer.RemoveByTag(TagHitbox);
        renderer.RemoveByTag(TagAutoAttack);
        _lastDir = PositionalDir.None;
        _lastCorrect = false;
        _lastShowHitbox = false;
        _lastShowAA = false;
        _lastTargetEntityId = 0;
        PositionalState.Clear();
    }
}
