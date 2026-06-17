using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class GeometricGlowEffect
{
    private float _rotationAngle;
    private float _scanLineY;
    private float _scanDir = 1f;
    private const float ScanSpeed = 120f;
    private const float RotationSpeed = 15f * MathF.PI / 180f;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        var h = max.Y - min.Y;

        _rotationAngle += RotationSpeed * dt;

        _scanLineY += _scanDir * ScanSpeed * dt;
        if (_scanLineY > h)
        {
            _scanLineY = h;
            _scanDir = -1f;
        }
        else if (_scanLineY < 0f)
        {
            _scanLineY = 0f;
            _scanDir = 1f;
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            var rot = _rotationAngle;
            var scanY = _scanLineY;
            _computeTask = Task.Run(() => ComputeFrameData(rot, scanY, min, max));
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var data = Volatile.Read(ref _front);
        if (data.Lines != null)
            foreach (var (a, b, col, thick) in data.Lines)
                dl.AddLine(a, b, col, thick);

        dl.PopClipRect();
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData(float rot, float scanY, Vector2 min, Vector2 max)
    {
        var accent = Theme.Colors.AccentBlue;
        var tertiary = Theme.Colors.TextTertiary;

        var lines = new List<(Vector2 A, Vector2 B, uint Col, float Thick)>();

        ComputeCornerLines(lines, min, max, accent);
        ComputePolygonLines(lines, min, max, accent, rot);
        ComputeScanLine(lines, min, max, accent, scanY);
        ComputeGridLines(lines, min, max, tertiary);

        var back = _back;
        back.Lines = lines.ToArray();
    }

    private static void ComputeCornerLines(List<(Vector2 A, Vector2 B, uint Col, float Thick)> lines, Vector2 min, Vector2 max, Vector4 accent)
    {
        var len = 28f;
        var corners = new (Vector2 origin, Vector2 dirH, Vector2 dirV)[]
        {
            (min, new Vector2(1, 0), new Vector2(0, 1)),
            (new Vector2(max.X, min.Y), new Vector2(-1, 0), new Vector2(0, 1)),
            (new Vector2(min.X, max.Y), new Vector2(1, 0), new Vector2(0, -1)),
            (max, new Vector2(-1, 0), new Vector2(0, -1)),
        };

        var glowCol = EffectUtils.PackColor(accent.X, accent.Y, accent.Z, 0.12f);
        var mainCol = EffectUtils.PackColor(accent.X, accent.Y, accent.Z, 0.5f);

        foreach (var (origin, dirH, dirV) in corners)
        {
            var hEnd = origin + dirH * len;
            var vEnd = origin + dirV * len;
            lines.Add((origin, hEnd, glowCol, 4f));
            lines.Add((origin, vEnd, glowCol, 4f));
            lines.Add((origin, hEnd, mainCol, 1.5f));
            lines.Add((origin, vEnd, mainCol, 1.5f));
        }
    }

    private static void ComputePolygonLines(List<(Vector2 A, Vector2 B, uint Col, float Thick)> lines, Vector2 min, Vector2 max, Vector4 accent, float rot)
    {
        var center = (min + max) * 0.5f;
        var radius = Math.Min(max.X - min.X, max.Y - min.Y) * 0.2f;
        if (radius < 10f) return;

        var sides = 6;
        var glowCol = EffectUtils.PackColor(accent.X, accent.Y, accent.Z, 0.08f);
        var mainCol = EffectUtils.PackColor(accent.X, accent.Y, accent.Z, 0.25f);
        var innerCol = EffectUtils.PackColor(accent.X, accent.Y, accent.Z, 0.15f);

        AddPolygon(lines, center, radius + 4f, sides, rot, glowCol, 3f);
        AddPolygon(lines, center, radius, sides, rot, mainCol, 1.5f);
        AddPolygon(lines, center, radius * 0.5f, sides, -rot * 0.7f, innerCol, 1f);
    }

    private static void AddPolygon(List<(Vector2 A, Vector2 B, uint Col, float Thick)> lines, Vector2 center, float radius, int sides, float angle, uint color, float thickness)
    {
        for (var i = 0; i < sides; i++)
        {
            var a1 = angle + i * MathF.Tau / sides;
            var a2 = angle + ((i + 1) % sides) * MathF.Tau / sides;
            lines.Add((center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius,
                        center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius,
                        color, thickness));
        }
    }

    private static void ComputeScanLine(List<(Vector2 A, Vector2 B, uint Col, float Thick)> lines, Vector2 min, Vector2 max, Vector4 accent, float scanY)
    {
        var y = min.Y + scanY;
        if (y < min.Y || y > max.Y) return;

        lines.Add((new Vector2(min.X, y), new Vector2(max.X, y),
            EffectUtils.PackColor(accent.X, accent.Y, accent.Z, 0.08f), 8f));
        lines.Add((new Vector2(min.X, y), new Vector2(max.X, y),
            EffectUtils.PackColor(accent.X, accent.Y, accent.Z, 0.3f), 1f));
    }

    private static void ComputeGridLines(List<(Vector2 A, Vector2 B, uint Col, float Thick)> lines, Vector2 min, Vector2 max, Vector4 tertiary)
    {
        var gridCol = EffectUtils.PackColor(tertiary.X, tertiary.Y, tertiary.Z, 0.04f);
        var spacing = 40f;

        for (var x = min.X + spacing; x < max.X; x += spacing)
            lines.Add((new Vector2(x, min.Y), new Vector2(x, max.Y), gridCol, 0.5f));
        for (var y = min.Y + spacing; y < max.Y; y += spacing)
            lines.Add((new Vector2(min.X, y), new Vector2(max.X, y), gridCol, 0.5f));
    }

    private sealed class FrameData
    {
        public (Vector2 A, Vector2 B, uint Col, float Thick)[]? Lines;
    }
}
