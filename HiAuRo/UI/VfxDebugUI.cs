using System.Numerics;
using HiAuRo.Rendering;
using HiAuRo.Vfx;
using OmenTools.OmenService;

namespace HiAuRo.UI;

public sealed class VfxDebugUI
{
    // ── 形状测试参数 ──
    int _shapeType;
    Vector3 _shapePos;
    float _circleRadius = 5f;
    float _rectWidth = 4f, _rectLength = 8f, _rectRotation;
    float _fanRadius = 5f, _fanFacingDeg, _fanArcDeg = 60f;
    float _ringInnerR = 3f, _ringOuterR = 6f;
    float _crossLen = 6f, _crossWidth = 2f, _crossRotation;
    float _rfInnerR = 3f, _rfOuterR = 6f, _rfFacingDeg, _rfArcDeg = 60f;
    Vector3 _lineStart, _lineEnd;
    float _lineWidth = 2f;
    float _shapeDuration = 5f;

    // ── 扇形精确测试参数 ──
    float _ftRadius = 25f;
    float _ftRotDeg = 157.5f;
    float _ftRadius2 = 25f;
    float _ftRotDeg2 = -157.5f;
    float _ftDuration = 8f;
    Vector4 _ftColor = new(1f, 1f, 1f, 0.35f);
    string _ftLog = "";
    bool _ftUseTargetRot = true;

    // ── 连续扇形角度遍览 ──
    float _fanSweepRadius = 6f;
    float _fanSweepRotDeg;
    int _fanSweepCount = 8;
    float _fanSweepMaxDeg = 360f; // 0-100

    // ── 圈环测试参数 ──
    float _ringScaleX = 2.0f;
    float _ringScaleY = 0.15f;
    float _ringScaleZ = 2.0f;
    float _ringScaleX2 = 3.0f;
    float _ringScaleY2 = 0.15f;
    float _ringScaleZ2 = 3.0f;
    float _ringTestDuration = 15f;
    Vector4 _ringColor1 = new(0.3f, 1f, 0.3f, 0.4f);
    Vector4 _ringColor2 = new(1f, 0.8f, 0.2f, 0.3f);

    static readonly string[] ShapeNames = ["圆形", "矩形", "扇形", "环形", "十字", "扇环", "直线"];

    public VfxDebugUI()
    {
        var player = DService.Instance().ObjectTable.LocalPlayer;
        if (player != null)
        {
            _shapePos = player.Position;
            _lineStart = player.Position;
            _lineEnd = player.Position with { X = player.Position.X + 10f };
        }
    }

    public void Draw()
    {
        if (ImGui.BeginTabBar("##VfxDebugTabs"))
        {
            if (ImGui.BeginTabItem("形状测试"))
            {
                DrawShapeTestTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("扇形角度测试"))
            {
                DrawFanTestTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("连续扇面"))
            {
                DrawFanSweepTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("圈环测试"))
            {
                DrawRingTestTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    void DrawShapeTestTab()
    {
        var player = DService.Instance().ObjectTable.LocalPlayer;

        ImGui.Combo("形状类型", ref _shapeType, ShapeNames, ShapeNames.Length);

        ImGui.Spacing();
        DrawVector3("位置", ref _shapePos, 100f);
        ImGui.SameLine();
        if (ImGui.Button("玩家位置##shape"))
        {
            if (player != null) _shapePos = player.Position;
        }

        ImGui.Separator();
        ImGui.SliderFloat("持续时间(秒)", ref _shapeDuration, 0.5f, 60f, "%.1f");

        ImGui.Separator();
        switch (_shapeType)
        {
            case 0:
                ImGui.SliderFloat("半径##circle", ref _circleRadius, 0.5f, 40f, "%.1f");
                break;
            case 1:
                ImGui.SliderFloat("宽度##rect_w", ref _rectWidth, 0.5f, 40f, "%.1f");
                ImGui.SliderFloat("长度##rect_l", ref _rectLength, 0.5f, 40f, "%.1f");
                ImGui.SliderFloat("旋转角度##rect_r", ref _rectRotation, 0f, 360f, "%.0f°");
                break;
            case 2:
                ImGui.SliderFloat("半径##fan_r", ref _fanRadius, 0.5f, 40f, "%.1f");
                ImGui.SliderFloat("朝向角度##fan_f", ref _fanFacingDeg, 0f, 360f, "%.0f°");
                ImGui.SliderFloat("角度范围##fan_a", ref _fanArcDeg, 1f, 360f, "%.0f°");
                break;
            case 3:
                ImGui.SliderFloat("内半径##ring_ir", ref _ringInnerR, 0f, 39f, "%.1f");
                ImGui.SliderFloat("外半径##ring_or", ref _ringOuterR, 1f, 40f, "%.1f");
                if (_ringInnerR >= _ringOuterR) _ringInnerR = _ringOuterR - 1f;
                break;
            case 4:
                ImGui.SliderFloat("臂长##cross_l", ref _crossLen, 0.5f, 40f, "%.1f");
                ImGui.SliderFloat("臂宽##cross_w", ref _crossWidth, 0.5f, 10f, "%.1f");
                ImGui.SliderFloat("旋转角度##cross_r", ref _crossRotation, 0f, 360f, "%.0f°");
                break;
            case 5:
                ImGui.SliderFloat("内半径##rf_ir", ref _rfInnerR, 0f, 39f, "%.1f");
                ImGui.SliderFloat("外半径##rf_or", ref _rfOuterR, 1f, 40f, "%.1f");
                if (_rfInnerR >= _rfOuterR) _rfInnerR = _rfOuterR - 1f;
                ImGui.SliderFloat("朝向角度##rf_f", ref _rfFacingDeg, 0f, 360f, "%.0f°");
                ImGui.SliderFloat("角度范围##rf_a", ref _rfArcDeg, 1f, 360f, "%.0f°");
                break;
            case 6:
                DrawVector3("起点##line_s", ref _lineStart, 100f);
                DrawVector3("终点##line_e", ref _lineEnd, 100f);
                ImGui.SliderFloat("宽度##line_w", ref _lineWidth, 0.5f, 20f, "%.1f");
                break;
        }

        ImGui.Spacing();
        if (ImGui.Button("绘制", new Vector2(80f, 0f)))
            DrawShape();
        ImGui.SameLine();
        if (ImGui.Button("清除全部 VFX", new Vector2(120f, 0f)))
            VfxRenderer.Instance?.Clear();
    }

    void DrawFanTestTab()
    {
        var target = TargetManager.Target;
        var player = DService.Instance().ObjectTable.LocalPlayer;

        ImGui.Text("45° 扇形精确调节 (Fan45 VFX 经确认角度准确)");
        ImGui.Separator();

        // 快捷角度按钮
        ImGui.Text("可用角度:");
        TestFanBtn("20°", VfxPath.Fan20, 20f); ImGui.SameLine();
        TestFanBtn("30°", VfxPath.Fan30, 30f); ImGui.SameLine();
        TestFanBtn("45°", VfxPath.Fan45, 45f); ImGui.SameLine();
        TestFanBtn("60°", VfxPath.Fan60, 60f); ImGui.SameLine();
        TestFanBtn("90°", VfxPath.Fan90, 90f); ImGui.SameLine();
        TestFanBtn("100°", VfxPath.Fan100, 100f); ImGui.SameLine();
        TestFanBtn("120°", VfxPath.Fan120, 120f); ImGui.SameLine();
        TestFanBtn("150°", VfxPath.Fan150, 150f); ImGui.SameLine();
        TestFanBtn("180°", VfxPath.Fan180, 180f); ImGui.SameLine();
        TestFanBtn("270°", VfxPath.Fan270, 270f);

        ImGui.Separator();

        DrawVector3("中心位置", ref _shapePos, 100f);
        ImGui.SameLine();
        if (ImGui.Button("目标位置##ftpos_target"))
        {
            if (target != null) { _shapePos = target.Position; _ftUseTargetRot = true; }
        }
        ImGui.SameLine();
        if (ImGui.Button("玩家位置##ftpos_player"))
        {
            if (player != null) { _shapePos = player.Position; _ftUseTargetRot = false; }
        }

        var hasTarget = target != null;
        var drawPos = hasTarget
            ? new Vector3(target.Position.X, _shapePos.Y, target.Position.Z)
            : _shapePos;
        var baseRot = (_ftUseTargetRot && hasTarget) ? target.Rotation : 0f;
        var baseRotDeg = baseRot * 180f / MathF.PI;

        ImGui.Separator();
        ImGui.TextDisabled($"绘制中心: {(hasTarget ? $"目标" : "手动")} ({drawPos.X:F1}, {drawPos.Y:F2}, {drawPos.Z:F1})");
        ImGui.TextDisabled($"基准朝向: {(_ftUseTargetRot && hasTarget ? "目标面向" : "正北")} ({baseRotDeg:F1}° / {baseRot:F3}rad)");
        ImGui.Separator();

        ImGui.Columns(2, "##fan_cols", false);
        ImGui.Text("扇形 1"); ImGui.NextColumn();
        ImGui.Text("扇形 2 (对比)"); ImGui.NextColumn();
        ImGui.Separator();

        ImGui.SetNextItemWidth(140f);
        ImGui.SliderFloat("半径##f1_r", ref _ftRadius, 1f, 40f, "%.1f"); ImGui.NextColumn();
        ImGui.SetNextItemWidth(140f);
        ImGui.SliderFloat("半径##f2_r", ref _ftRadius2, 1f, 40f, "%.1f"); ImGui.NextColumn();
        ImGui.SetNextItemWidth(140f);
        ImGui.InputFloat("偏移角度##f1_rot", ref _ftRotDeg, 1f, 5f, "%.1f°"); ImGui.NextColumn();
        ImGui.SetNextItemWidth(140f);
        ImGui.InputFloat("偏移角度##f2_rot", ref _ftRotDeg2, 1f, 5f, "%.1f°"); ImGui.NextColumn();

        ImGui.SetNextItemWidth(140f);
        ImGui.SliderFloat("持续时间(秒)##ft_dur", ref _ftDuration, 1f, 60f, "%.0f"); ImGui.NextColumn();

        ImGui.ColorEdit4("颜色/透明度##ft_color", ref _ftColor,
            ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar); ImGui.NextColumn();
        ImGui.NewLine(); ImGui.NextColumn();

        ImGui.Columns(1);

        ImGui.Spacing();
        ImGui.TextDisabled($"当前参数 | 基准: {(_ftUseTargetRot ? $"目标面向 {baseRotDeg:F1}°" : "正北 0°")}");
        ImGui.Text($"  扇1: r={_ftRadius:F1}  offset={_ftRotDeg:F1}°  |  扇2: r={_ftRadius2:F1}  offset={_ftRotDeg2:F1}°  |  持续={_ftDuration:F0}s");
        if (!string.IsNullOrEmpty(_ftLog))
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), _ftLog);
        }

        ImGui.Spacing();
        if (ImGui.Button("绘制扇1", new Vector2(80f, 0f)))
        {
            var rot = baseRot + _ftRotDeg * MathF.PI / 180f;
            VfxRenderer.Instance?.RemoveByTag("vfx_fantest1");
            VfxRenderer.Instance?.Show(VfxPath.Fan90, drawPos,
                new Vector3(_ftRadius, 1f, _ftRadius), rot,
                duration: _ftDuration, tag: "vfx_fantest1", color: _ftColor);
            _ftLog = $"→ 扇1[Fan90]: r={_ftRadius:F1} offset={_ftRotDeg:F1}° abs={rot*180/MathF.PI:F1}°";
        }

        ImGui.SameLine();
        if (ImGui.Button("绘制扇2", new Vector2(80f, 0f)))
        {
            var rot = baseRot + _ftRotDeg2 * MathF.PI / 180f;
            VfxRenderer.Instance?.RemoveByTag("vfx_fantest2");
            VfxRenderer.Instance?.Show(VfxPath.Fan90, drawPos,
                new Vector3(_ftRadius2, 1f, _ftRadius2), rot,
                duration: _ftDuration, tag: "vfx_fantest2", color: _ftColor);
            _ftLog = $"→ 扇2[Fan90]: r={_ftRadius2:F1} offset={_ftRotDeg2:F1}° abs={rot*180/MathF.PI:F1}°";
        }

        ImGui.SameLine();
        if (ImGui.Button("同时绘制", new Vector2(80f, 0f)))
        {
            var r1 = baseRot + _ftRotDeg * MathF.PI / 180f;
            var r2 = baseRot + _ftRotDeg2 * MathF.PI / 180f;
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_fantest.*");
            VfxRenderer.Instance?.Show(VfxPath.Fan90, drawPos,
                new Vector3(_ftRadius, 1f, _ftRadius), r1,
                duration: _ftDuration, tag: "vfx_fantest1", color: _ftColor);
            VfxRenderer.Instance?.Show(VfxPath.Fan90, drawPos,
                new Vector3(_ftRadius2, 1f, _ftRadius2), r2,
                duration: _ftDuration, tag: "vfx_fantest2", color: _ftColor);
            _ftLog = $"→ 双扇[Fan90]: r1={_ftRadius:F1} off1={_ftRotDeg:F1}° | r2={_ftRadius2:F1} off2={_ftRotDeg2:F1}°";
        }

        ImGui.SameLine();
        if (ImGui.Button("清除扇形", new Vector2(80f, 0f)))
        {
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_fantest.*");
            _ftLog = "";
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("身位绘制验证 (以目标为中心, 用确认的 VFX 参数)");
        ImGui.Separator();

        if (ImGui.Button("绘制背身位 (Fan90 偏移180°)", new Vector2(240f, 0f)))
            DrawPositionalPreview(PositionalDir.Behind, _ftColor);
        ImGui.SameLine();
        if (ImGui.Button("绘制侧身位 (Fan120 偏移±90°)", new Vector2(240f, 0f)))
            DrawPositionalPreview(PositionalDir.Flank, _ftColor);

        ImGui.Spacing();
        ImGui.Text("正确/错误颜色对比:");
        if (ImGui.Button("背身位 正确(绿)", new Vector2(130f, 0f)))
            DrawPositionalPreview(PositionalDir.Behind, new Vector4(0, 1, 0, 0.35f));
        ImGui.SameLine();
        if (ImGui.Button("背身位 错误(红)", new Vector2(130f, 0f)))
            DrawPositionalPreview(PositionalDir.Behind, new Vector4(1, 0, 0, 0.35f));
        ImGui.SameLine();
        if (ImGui.Button("侧身位 正确(绿)", new Vector2(130f, 0f)))
            DrawPositionalPreview(PositionalDir.Flank, new Vector4(0, 1, 0, 0.35f));
        ImGui.SameLine();
        if (ImGui.Button("侧身位 错误(红)", new Vector2(130f, 0f)))
            DrawPositionalPreview(PositionalDir.Flank, new Vector4(1, 0, 0, 0.35f));

        ImGui.SameLine();
        if (ImGui.Button("清除身位预览", new Vector2(100f, 0f)))
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_posprev.*");
    }

    void DrawPositionalPreview(PositionalDir dir, Vector4 color)
    {
        var target = TargetManager.Target;
        if (target == null || target is not IBattleChara bc) return;

        var renderer = VfxRenderer.Instance;
        if (renderer == null) return;

        renderer.RemoveByTagRegex("vfx_posprev.*");

        var pos = bc.Position;
        var facing = bc.Rotation;
        var radius = bc.HitboxRadius + 3f;

        switch (dir)
        {
            case PositionalDir.Behind:
                renderer.Show(VfxPath.Fan90, pos,
                    new Vector3(radius, 1f, radius), facing + MathF.PI,
                    duration: 15f, tag: "vfx_posprev", color: color);
                break;
            case PositionalDir.Flank:
                renderer.Show(VfxPath.Fan120, pos,
                    new Vector3(radius, 1f, radius), facing + MathF.PI / 2f,
                    duration: 15f, tag: "vfx_posprev", color: color);
                renderer.Show(VfxPath.Fan120, pos,
                    new Vector3(radius, 1f, radius), facing - MathF.PI / 2f,
                    duration: 15f, tag: "vfx_posprev", color: color);
                break;
        }
    }

    void TestFanBtn(string label, string vfxPath, float degHint)
    {
        if (ImGui.Button($"{label}##fanbtn_{label}", new Vector2(40f, 0f)))
        {
            var target = TargetManager.Target;
            var pos = target?.Position ?? _shapePos;
            var rot = target?.Rotation ?? 0f;
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_quickfan.*");
            VfxRenderer.Instance?.Show(vfxPath, pos,
                new Vector3(8f, 1f, 8f), rot,
                duration: 8f, tag: $"vfx_quickfan_{label}", color: _ftColor);
        }
    }

    void DrawRingTestTab()
    {
        var target = TargetManager.Target;
        var hasTarget = target != null;
        var drawPos = hasTarget ? target.Position : _shapePos;

        ImGui.Text("圈环测试 (Scale X/Y/Z 精确控制)");
        ImGui.Separator();

        DrawVector3("中心位置", ref _shapePos, 120f);
        ImGui.SameLine();
        if (ImGui.Button("目标位置##ringpos"))
        {
            if (target != null) _shapePos = target.Position;
        }
        ImGui.TextDisabled($"绘制中心: {(hasTarget ? $"目标" : "手动")} ({drawPos.X:F1}, {drawPos.Y:F2}, {drawPos.Z:F1})");

        ImGui.Spacing();

        // ── 圈1 ──
        if (ImGui.CollapsingHeader("圈1", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent(12f);
            DrawRingParams("##r1", ref _ringScaleX, ref _ringScaleY, ref _ringScaleZ,
                ref _ringColor1, ref _ringScaleX, ref _ringScaleZ, ref _ringScaleY,
                hasTarget, target as IBattleChara,
                () => { VfxRenderer.Instance?.RemoveByTag("vfx_ring1");
                    VfxRenderer.Instance?.Show(VfxPath.Ring, drawPos,
                        new Vector3(_ringScaleX, _ringScaleY, _ringScaleZ), 0f,
                        duration: _ringTestDuration, tag: "vfx_ring1", color: _ringColor1); },
                "vfx_ring1");
            ImGui.Unindent(12f);
        }

        // ── 圈2 ──
        if (ImGui.CollapsingHeader("圈2", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent(12f);
            DrawRingParams("##r2", ref _ringScaleX2, ref _ringScaleY2, ref _ringScaleZ2,
                ref _ringColor2, ref _ringScaleX2, ref _ringScaleZ2, ref _ringScaleY2,
                hasTarget, target as IBattleChara,
                () => { VfxRenderer.Instance?.RemoveByTag("vfx_ring2");
                    VfxRenderer.Instance?.Show(VfxPath.Ring, drawPos,
                        new Vector3(_ringScaleX2, _ringScaleY2, _ringScaleZ2), 0f,
                        duration: _ringTestDuration, tag: "vfx_ring2", color: _ringColor2); },
                "vfx_ring2");
            ImGui.Unindent(12f);
        }

        ImGui.Spacing();
        if (ImGui.Button("同时绘制", new Vector2(100f, 0f)))
        {
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_ring.*");
            VfxRenderer.Instance?.Show(VfxPath.Ring, drawPos,
                new Vector3(_ringScaleX, _ringScaleY, _ringScaleZ), 0f,
                duration: _ringTestDuration, tag: "vfx_ring1", color: _ringColor1);
            VfxRenderer.Instance?.Show(VfxPath.Ring, drawPos,
                new Vector3(_ringScaleX2, _ringScaleY2, _ringScaleZ2), 0f,
                duration: _ringTestDuration, tag: "vfx_ring2", color: _ringColor2);
        }
        ImGui.SameLine();
        if (ImGui.Button("清除圈", new Vector2(80f, 0f)))
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_ring.*");
    }

    static void DrawRingParams(string id, ref float sx, ref float sy, ref float sz,
        ref Vector4 color, ref float refSx, ref float refSz, ref float refSy,
        bool hasTarget, IBattleChara? bc, Action draw, string tag)
    {
        ImGui.SetNextItemWidth(300f);
        ImGui.InputFloat($"Scale X{id}_sx", ref sx, 0.1f, 1f, "%.3f");
        ImGui.SetNextItemWidth(300f);
        ImGui.InputFloat($"Scale Y {id}_sy", ref sy, 0.01f, 0.5f, "%.3f");
        ImGui.SetNextItemWidth(300f);
        ImGui.InputFloat($"Scale Z{id}_sz", ref sz, 0.1f, 1f, "%.3f");

        ImGui.Spacing();
        ImGui.SetNextItemWidth(300f);
        ImGui.ColorEdit4($"颜色{id}", ref color,
            ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoInputs);

        ImGui.Spacing();
        if (ImGui.Button($"绘制{id}", new Vector2(80f, 0f))) draw();
        ImGui.SameLine();
        if (ImGui.Button($"清除{id}", new Vector2(80f, 0f)))
            VfxRenderer.Instance?.RemoveByTag(tag);

        if (hasTarget && bc != null)
        {
            ImGui.SameLine();
            var hr = bc.HitboxRadius;
            if (ImGui.Button($"HR={hr:F2}", new Vector2(80f, 0f)))
            {
                refSx = hr; refSz = hr; refSy = 0.15f;
            }
        }
    }

    void DrawFanSweepTab()
    {
        var player = DService.Instance().ObjectTable.LocalPlayer;

        ImGui.Text("连续扇形角度遍览（多角度的扇形同时展示，便于对比）");
        ImGui.Separator();

        DrawVector3("中心位置", ref _shapePos, 100f);
        ImGui.SameLine();
        if (ImGui.Button("玩家位置##sweep"))
        {
            if (player != null) _shapePos = player.Position;
        }

        ImGui.SliderFloat("半径##sw_r", ref _fanSweepRadius, 1f, 30f, "%.1f");
        ImGui.SliderFloat("起始角度##sw_rot", ref _fanSweepRotDeg, 0f, 360f, "%.0f°");
        ImGui.SliderInt("扇形数量##sw_cnt", ref _fanSweepCount, 2, 20);
        ImGui.SliderFloat("最大角度##sw_max", ref _fanSweepMaxDeg, 30f, 360f, "%.0f°");

        var stepDeg = _fanSweepMaxDeg / Math.Max(1, _fanSweepCount - 1);

        ImGui.Spacing();
        if (ImGui.Button("绘制遍览", new Vector2(100f, 0f)))
        {
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_sweep.*");
            for (int i = 0; i < _fanSweepCount; i++)
            {
                var deg = (i + 1) * stepDeg;
                var r = _fanSweepRadius + i * 0.5f; // 递增半径视觉分离
                VfxRenderer.Instance?.ShowFan(_shapePos, r,
                    deg / 2f * MathF.PI / 180f,
                    _fanSweepRotDeg * MathF.PI / 180f,
                    duration: 15f, tag: $"vfx_sweep_{deg:F0}");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("清除遍览", new Vector2(100f, 0f)))
            VfxRenderer.Instance?.RemoveByTagRegex("vfx_sweep.*");
    }

    static string GetFanAssetName(float totalDeg)
    {
        return totalDeg switch
        {
            <= 45f  => "Fan45  (gl_fan045_1bf)",
            <= 100f => "Fan100 (er_gl_fan100_o1v)",
            <= 180f => "Fan180 (gl_fan180_6014g2)",
            _       => "Fan270 (gl_fan270_1005af)",
        };
    }

    void DrawShape()
    {
        var renderer = VfxRenderer.Instance;
        if (renderer == null) return;

        renderer.RemoveByTagRegex("vfx_test");

        switch (_shapeType)
        {
            case 0:
                renderer.ShowCircle(_shapePos, _circleRadius, duration: _shapeDuration, tag: "vfx_test");
                break;
            case 1:
                renderer.ShowRect(_shapePos, _rectWidth, _rectLength,
                    _rectRotation * MathF.PI / 180f, duration: _shapeDuration, tag: "vfx_test");
                break;
            case 2:
                renderer.ShowFan(_shapePos, _fanRadius, _fanArcDeg / 2f * MathF.PI / 180f,
                    _fanFacingDeg * MathF.PI / 180f, duration: _shapeDuration, tag: "vfx_test");
                break;
            case 3:
                renderer.ShowRing(_shapePos, _ringInnerR, _ringOuterR, duration: _shapeDuration, tag: "vfx_test");
                break;
            case 4:
                renderer.ShowCross(_shapePos, _crossLen, _crossWidth,
                    rotation: _crossRotation * MathF.PI / 180f, duration: _shapeDuration, tag: "vfx_test");
                break;
            case 5:
                renderer.ShowRingFan(_shapePos, _rfInnerR, _rfOuterR,
                    _rfArcDeg / 2f * MathF.PI / 180f, _rfFacingDeg * MathF.PI / 180f,
                    duration: _shapeDuration, tag: "vfx_test");
                break;
            case 6:
                renderer.ShowLine(_lineStart, _lineEnd, _lineWidth, duration: _shapeDuration, tag: "vfx_test");
                break;
        }
    }

    static void DrawVector3(string label, ref Vector3 v, float itemWidth)
    {
        ImGui.PushItemWidth(itemWidth);
        ImGui.InputFloat($"{label} X", ref v.X);
        ImGui.SameLine();
        ImGui.InputFloat("Y", ref v.Y);
        ImGui.SameLine();
        ImGui.InputFloat("Z", ref v.Z);
        ImGui.PopItemWidth();
    }
}
