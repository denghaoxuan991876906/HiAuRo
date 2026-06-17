using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private static readonly Vector2[] FCenters =
    [
        new(4f, 0f),
        new(2f, 3.464102f),
        new(-2f, 3.464102f),
        new(-4f, 0f),
        new(-2f, -3.464102f),
        new(2f, -3.464102f),
    ];

    private static readonly Vector2[] TriBase1 =
    [
        new(4.005988f, 2.970220f),
        new(-0.569292f, 4.954398f),
        new(-4.575280f, 1.984178f),
        new(-4.005988f, -2.970220f),
        new(0.569292f, -4.954398f),
        new(4.575280f, -1.984178f),
    ];

    private static readonly Vector2[] TriBase2 =
    [
        new(4.575280f, 1.984178f),
        new(0.569292f, 4.954398f),
        new(-4.005988f, 2.970220f),
        new(-4.575280f, -1.984178f),
        new(-0.569292f, -4.954398f),
        new(4.005988f, -2.970220f),
    ];

    private static readonly Vector2[] TriTips =
    [
        new(5.152851f, 2.975000f),
        new(0.000000f, 5.950000f),
        new(-5.152851f, 2.975000f),
        new(-5.152851f, -2.975000f),
        new(0.000000f, -5.950000f),
        new(5.152851f, -2.975000f),
    ];

    private static readonly Vector2[] TriCentroids =
    [
        new(4.577851f, 2.643024f),
        new(0.000000f, 5.286047f),
        new(-4.577851f, 2.643024f),
        new(-4.577851f, -2.643024f),
        new(0.000000f, -5.286047f),
        new(4.577851f, -2.643024f),
    ];

    private static readonly Vector2[] DiamondC =
    [
        new(4f, 0f), new(0f, 4f), new(-4f, 0f), new(0f, -4f),
    ];

    private static readonly Vector2[] SquareD =
    [
        new(2f, 2f), new(-2f, 2f), new(-2f, -2f), new(2f, -2f),
    ];

    private static readonly (int A, int B)[] DiamondEdges =
    [
        (0, 1), (1, 2), (2, 3), (3, 0),
    ];

    private static readonly (int A, int B)[] SquareEdges =
    [
        (0, 1), (1, 2), (2, 3), (3, 0),
    ];


    private float _rotX;
    private float _rotY;
    private float _flatRot;
    private float _time;
    private float _flickerAlpha = 0.2f;
    private float _flickerTimer = 2f;
    private bool _inFlicker;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _rotY += 0.3f * dt;
        _flatRot += 0.8f * dt;

        var mouse = ImGui.GetIO().MousePos;
        var center = (min + max) * 0.5f;
        var inWindow = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        if (inWindow)
        {
            var dx = (mouse.X - center.X) / Math.Max(1f, (max.X - min.X) * 0.5f);
            var dy = (mouse.Y - center.Y) / Math.Max(1f, (max.Y - min.Y) * 0.5f);
            _rotY += dx * dt * 1.5f;
            _rotX += dy * dt * 1.5f;
        }

        _flickerTimer -= dt;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = 2f + Random.Shared.NextSingle() * 1f;
            _inFlicker = true;
        }

        if (_inFlicker)
        {
            _flickerAlpha = Random.Shared.NextSingle() < 0.3f
                ? 0.05f
                : 0.15f + Random.Shared.NextSingle() * 0.15f;
            _flickerTimer -= dt;
            if (_flickerTimer <= 0f || Random.Shared.NextSingle() < 0.1f)
            {
                _inFlicker = false;
                _flickerAlpha = 0.2f;
                _flickerTimer = 2f + Random.Shared.NextSingle() * 1f;
            }
        }
        else
        {
            _flickerAlpha = 0.2f + MathF.Sin(_time * 0.5f) * 0.05f;
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();

            var rotX = _rotX;
            var rotY = _rotY;
            var flatRot = _flatRot;
            var flickerAlpha = _flickerAlpha;

            _computeTask = Task.Run(() => ComputeFrameData(rotX, rotY, flatRot, flickerAlpha, min, max));
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        DrawBaseGradient(dl, winMin, winMax);

        var data = Volatile.Read(ref _front);
        if (data.Lines == null)
        {
            dl.PopClipRect();
            return;
        }

        foreach (var (a, b, col, thickness) in data.Lines)
            dl.AddLine(a, b, col, thickness);
        foreach (var (center, radius, col) in data.Dots)
            dl.AddCircleFilled(center, radius, col);
        foreach (var (a, b, col, thickness) in data.ScanLines)
            dl.AddLine(a, b, col, thickness);

        dl.PopClipRect();
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData(float rotX, float rotY, float flatRot, float flickerAlpha, Vector2 min, Vector2 max)
    {
        var center = (min + max) * 0.5f;
        var scale = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.06f;

        var projDiamond = ProjectVerts(DiamondC, center, scale, rotX, rotY, flatRot);
        var projSquare = ProjectVerts(SquareD, center, scale, rotX, rotY, flatRot);
        var projFCenters = ProjectVerts(FCenters, center, scale, rotX, rotY, flatRot);

        var projTriVerts = new Vector2[6][];
        for (var i = 0; i < 6; i++)
        {
            projTriVerts[i] =
            [
                Project(new Vector3(TriBase1[i].X * scale, TriBase1[i].Y * scale, 0), center, rotX, rotY, flatRot),
                Project(new Vector3(TriBase2[i].X * scale, TriBase2[i].Y * scale, 0), center, rotX, rotY, flatRot),
                Project(new Vector3(TriTips[i].X * scale, TriTips[i].Y * scale, 0), center, rotX, rotY, flatRot),
            ];
        }

        var lines = new List<(Vector2 A, Vector2 B, uint Col, float Thickness)>();
        var dots = new List<(Vector2 Center, float Radius, uint Col)>();

        var cyanOff = new Vector2(-2f, 0);
        var magentaOff = new Vector2(2f, 0);

        var cyanCol = ColorU32(new Vector4(0, 1, 1, 1), flickerAlpha * 0.5f);
        ComputeWireframe(lines, cyanOff, cyanCol, 3f, projDiamond, DiamondEdges, projSquare, SquareEdges, projTriVerts);
        ComputeCircles(lines, cyanOff, cyanCol, 3f, center, rotX, rotY, flatRot, scale);

        var magCol = ColorU32(new Vector4(1, 0, 1, 1), flickerAlpha * 0.5f);
        ComputeWireframe(lines, magentaOff, magCol, 3f, projDiamond, DiamondEdges, projSquare, SquareEdges, projTriVerts);
        ComputeCircles(lines, magentaOff, magCol, 3f, center, rotX, rotY, flatRot, scale);

        var mainCol = ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), flickerAlpha);
        ComputeWireframe(lines, Vector2.Zero, mainCol, 4.5f, projDiamond, DiamondEdges, projSquare, SquareEdges, projTriVerts);
        ComputeCircles(lines, Vector2.Zero, mainCol, 4.5f, center, rotX, rotY, flatRot, scale);

        foreach (var p in projDiamond)
            dots.Add((p, 6f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), flickerAlpha)));
        foreach (var p in projSquare)
            dots.Add((p, 6f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), flickerAlpha * 0.8f)));
        foreach (var p in projFCenters)
            dots.Add((p, 4.5f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), flickerAlpha * 0.7f)));
        foreach (var tri in projTriVerts)
            foreach (var p in tri)
                dots.Add((p, 4.5f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), flickerAlpha * 0.5f)));

        var scanLines = ComputeScanNoise(min, max);

        var back = _back;
        back.Lines = lines.ToArray();
        back.Dots = dots.ToArray();
        back.ScanLines = scanLines;
    }

    private static void ComputeWireframe(
        List<(Vector2 A, Vector2 B, uint Col, float Thickness)> lines,
        Vector2 offset, uint col, float thickness,
        Vector2[] diamond, (int A, int B)[] diamondEdges,
        Vector2[] square, (int A, int B)[] squareEdges,
        Vector2[][] triVerts)
    {
        for (var i = 0; i < diamondEdges.Length; i++)
            lines.Add((diamond[diamondEdges[i].A] + offset, diamond[diamondEdges[i].B] + offset, col, thickness));
        for (var i = 0; i < squareEdges.Length; i++)
            lines.Add((square[squareEdges[i].A] + offset, square[squareEdges[i].B] + offset, col, thickness));
        for (var t = 0; t < triVerts.Length; t++)
        {
            var tri = triVerts[t];
            lines.Add((tri[0] + offset, tri[1] + offset, col, thickness));
            lines.Add((tri[1] + offset, tri[2] + offset, col, thickness));
            lines.Add((tri[2] + offset, tri[0] + offset, col, thickness));
        }
    }

    private static void ComputeCircles(
        List<(Vector2 A, Vector2 B, uint Col, float Thickness)> lines,
        Vector2 offset, uint col, float thickness,
        Vector2 screenCenter, float rotX, float rotY, float flatRot, float scale)
    {
        ComputeProjectedCircle(lines, Vector3.Zero, 6f * scale, screenCenter, rotX, rotY, flatRot, offset, col, thickness, 128);
        ComputeProjectedCircle(lines, Vector3.Zero, 4f * scale, screenCenter, rotX, rotY, flatRot, offset, col, thickness, 128);
        for (var i = 0; i < FCenters.Length; i++)
            ComputeProjectedCircle(lines, new Vector3(FCenters[i].X * scale, FCenters[i].Y * scale, 0), 2f * scale,
                screenCenter, rotX, rotY, flatRot, offset, col, thickness, 96);
        for (var i = 0; i < TriCentroids.Length; i++)
            ComputeProjectedCircle(lines, new Vector3(TriCentroids[i].X * scale, TriCentroids[i].Y * scale, 0), 0.23f * scale,
                screenCenter, rotX, rotY, flatRot, offset, col, thickness, 48);
    }

    private static void ComputeProjectedCircle(
        List<(Vector2 A, Vector2 B, uint Col, float Thickness)> lines,
        Vector3 center3D, float radius3D,
        Vector2 screenCenter, float rotX, float rotY, float flatRot,
        Vector2 offset, uint color, float thickness, int segs)
    {
        var screenPts = new Vector2[segs];
        for (var i = 0; i < segs; i++)
        {
            var angle = i * MathF.Tau / segs;
            var localPt = new Vector3(
                center3D.X + radius3D * MathF.Cos(angle),
                center3D.Y + radius3D * MathF.Sin(angle),
                center3D.Z);
            screenPts[i] = Project(localPt, screenCenter, rotX, rotY, flatRot) + offset;
        }
        for (var i = 0; i < segs; i++)
            lines.Add((screenPts[i], screenPts[(i + 1) % segs], color, thickness));
    }

    private static Vector2[] ProjectVerts(Vector2[] localVerts, Vector2 screenCenter, float scale,
        float rotX, float rotY, float flatRot)
    {
        var result = new Vector2[localVerts.Length];
        for (var i = 0; i < localVerts.Length; i++)
            result[i] = Project(new Vector3(localVerts[i].X * scale, localVerts[i].Y * scale, 0),
                screenCenter, rotX, rotY, flatRot);
        return result;
    }

    private static Vector2 Project(Vector3 v, Vector2 center, float rotX, float rotY, float flatRot)
    {
        var zCos = MathF.Cos(flatRot);
        var zSin = MathF.Sin(flatRot);
        var x0 = v.X * zCos - v.Y * zSin;
        var y0 = v.X * zSin + v.Y * zCos;

        var cosY = MathF.Cos(rotY);
        var sinY = MathF.Sin(rotY);
        var x1 = x0 * cosY - v.Z * sinY;
        var z1 = x0 * sinY + v.Z * cosY;

        var cosX = MathF.Cos(rotX);
        var sinX = MathF.Sin(rotX);
        var y1 = y0 * cosX - z1 * sinX;
        var z2 = y0 * sinX + z1 * cosX;

        var perspective = 400f;
        var s = perspective / (perspective + z2);
        return center + new Vector2(x1, y1) * s;
    }

    private static (Vector2 A, Vector2 B, uint Col, float Thickness)[] ComputeScanNoise(Vector2 min, Vector2 max)
    {
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        var count = 8 + (Random.Shared.NextSingle() < 0.3f ? 12 : 0);
        var result = new (Vector2 A, Vector2 B, uint Col, float Thickness)[count];
        for (var i = 0; i < count; i++)
        {
            var x = min.X + Random.Shared.NextSingle() * w;
            var y = min.Y + Random.Shared.NextSingle() * h;
            var len = 5f + Random.Shared.NextSingle() * 20f;
            var a = 0.05f + Random.Shared.NextSingle() * 0.15f;
            result[i] = (new Vector2(x, y), new Vector2(x + len, y),
                ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), a), 1f);
        }
        return result;
    }

    private static void DrawBaseGradient(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var h = max.Y - min.Y;
        var steps = 6;
        var stepH = h / steps;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float)steps;
            var alpha = 0.02f + t * 0.03f;
            var y1 = min.Y + i * stepH;
            var y2 = y1 + stepH;
            dl.AddRectFilled(new Vector2(min.X, y1), new Vector2(max.X, y2),
                ColorU32(new Vector4(0.1f, 0.2f, 0.4f, 1), alpha));
        }
    }

    private static uint ColorU32(Vector4 c, float a)
    {
        var r = (byte)Math.Clamp(c.X * 255f, 0, 255);
        var g = (byte)Math.Clamp(c.Y * 255f, 0, 255);
        var b = (byte)Math.Clamp(c.Z * 255f, 0, 255);
        var alpha = (byte)Math.Clamp(a * 255f, 0, 255);
        return (uint)((alpha << 24) | (b << 16) | (g << 8) | r);
    }

    private sealed class FrameData
    {
        public (Vector2 A, Vector2 B, uint Col, float Thickness)[]? Lines;
        public (Vector2 Center, float Radius, uint Col)[]? Dots;
        public (Vector2 A, Vector2 B, uint Col, float Thickness)[]? ScanLines;
    }
}
