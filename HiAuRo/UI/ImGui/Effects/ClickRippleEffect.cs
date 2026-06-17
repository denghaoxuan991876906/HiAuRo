using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class ClickRippleEffect
{
    private struct Ripple
    {
        public Vector2 Center;
        public float Progress;
        public float MaxRadius;
    }

    private readonly Ripple[] _ripples;
    private const float Duration = 0.5f;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public ClickRippleEffect(int maxRipples = 8)
    {
        _ripples = new Ripple[maxRipples];
    }

    public void Trigger(Vector2 pos, float maxRadius = 40f)
    {
        var idx = -1;
        var oldestProg = float.MaxValue;

        for (var i = 0; i < _ripples.Length; i++)
        {
            if (_ripples[i].Progress >= 1f) { idx = i; break; }
            if (_ripples[i].Progress < oldestProg) { oldestProg = _ripples[i].Progress; idx = i; }
        }

        if (idx < 0) return;

        _ripples[idx] = new Ripple { Center = pos, Progress = 0f, MaxRadius = maxRadius };
    }

    public void Update(float dt)
    {
        for (var i = 0; i < _ripples.Length; i++)
        {
            if (_ripples[i].Progress < 1f)
                _ripples[i].Progress += dt / Duration;
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            _computeTask = Task.Run(ComputeFrameData);
        }
    }

    public void Draw(ImDrawListPtr dl)
    {
        var data = Volatile.Read(ref _front);
        if (data.Ripples == null) return;

        foreach (var (center, radius, segs, fillCol, glowCol, glowThick, mainCol, mainThick) in data.Ripples)
        {
            dl.PathArcTo(center, radius, 0f, MathF.PI * 2f, segs);
            dl.PathFillConvex(fillCol);
            dl.PathArcTo(center, radius, 0f, MathF.PI * 2f, segs);
            dl.PathStroke(glowCol, 0, glowThick);
            dl.PathArcTo(center, radius, 0f, MathF.PI * 2f, segs);
            dl.PathStroke(mainCol, 0, mainThick);
        }
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData()
    {
        var accent = Theme.Colors.AccentBlue;
        var ripples = new List<(Vector2 Center, float Radius, int Segs, uint FillCol, uint GlowCol, float GlowThick, uint MainCol, float MainThick)>();

        for (var i = 0; i < _ripples.Length; i++)
        {
            ref var r = ref _ripples[i];
            if (r.Progress >= 1f) continue;

            var easedProgress = Easing.EaseOutCubic(r.Progress);
            var radius = r.MaxRadius * easedProgress;
            var fade = 1f - r.Progress;
            var numSegments = Math.Max(32, (int)(MathF.Tau * radius / 3f));

            ripples.Add((r.Center, radius, numSegments,
                EffectUtils.PackColor(accent.X, accent.Y, accent.Z, fade * 0.15f),
                EffectUtils.PackColor(accent.X, accent.Y, accent.Z, fade * 0.3f), 5f * fade,
                EffectUtils.PackColor(accent.X, accent.Y, accent.Z, fade * 0.85f), 3f * fade));
        }

        _back.Ripples = ripples.ToArray();
    }

    private sealed class FrameData
    {
        public (Vector2 Center, float Radius, int Segs, uint FillCol, uint GlowCol, float GlowThick, uint MainCol, float MainThick)[]? Ripples;
    }
}
