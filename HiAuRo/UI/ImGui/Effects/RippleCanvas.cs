using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class RippleCanvas
{
    private struct Ripple
    {
        public Vector2 Center;
        public float Radius;
        public float MaxRadius;
        public float Life;
        public float MaxLife;
        public float LineWidth;
    }

    private readonly Ripple[] _ripples;
    private readonly int _maxRipples;
    private readonly float _spawnInterval;
    private float _spawnTimer;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public RippleCanvas(int maxRipples = 6, float spawnInterval = 1.5f)
    {
        _maxRipples = maxRipples;
        _ripples = new Ripple[maxRipples];
        _spawnInterval = spawnInterval;
        _spawnTimer = Math.Max(0, _spawnInterval - 0.3f);
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _spawnTimer += dt;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0f;
            SpawnRipple(min, max);
        }

        for (var i = 0; i < _ripples.Length; i++)
        {
            ref var r = ref _ripples[i];
            if (r.Life <= 0f) continue;
            r.Life -= dt;
            var progress = 1f - r.Life / r.MaxLife;
            r.Radius = r.MaxRadius * progress;
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            _computeTask = Task.Run(ComputeFrameData);
        }
    }

    public void TriggerClick(Vector2 pos, float maxRadius = 40f)
    {
        SpawnRippleAt(pos, maxRadius, 0.5f, 1.5f);
    }

    public void Draw(ImDrawListPtr dl)
    {
        var data = Volatile.Read(ref _front);
        if (data.Ripples == null) return;

        foreach (var (center, radius, segs, glowCol, glowThick, mainCol, mainThick) in data.Ripples)
        {
            dl.PathArcTo(center, radius, 0f, MathF.PI * 2f, segs);
            dl.PathStroke(glowCol, 0, glowThick);
            dl.PathArcTo(center, radius, 0f, MathF.PI * 2f, segs);
            dl.PathStroke(mainCol, 0, mainThick);
        }
    }

    private void SpawnRipple(Vector2 min, Vector2 max)
    {
        var center = new Vector2(
            min.X + (max.X - min.X) * (0.3f + Random.Shared.NextSingle() * 0.4f),
            min.Y + (max.Y - min.Y) * (0.3f + Random.Shared.NextSingle() * 0.4f));
        SpawnRippleAt(center, 60f + Random.Shared.NextSingle() * 80f, 2f + Random.Shared.NextSingle(), 2.5f);
    }

    private void SpawnRippleAt(Vector2 center, float maxRadius, float maxLife, float lineWidth)
    {
        var oldestIdx = -1;
        var oldestLife = float.MaxValue;

        for (var i = 0; i < _ripples.Length; i++)
        {
            if (_ripples[i].Life <= 0f) { oldestIdx = i; break; }
            if (_ripples[i].Life < oldestLife) { oldestLife = _ripples[i].Life; oldestIdx = i; }
        }

        if (oldestIdx < 0) return;

        ref var r = ref _ripples[oldestIdx];
        r.Center = center;
        r.Radius = 3f;
        r.MaxRadius = maxRadius;
        r.MaxLife = maxLife;
        r.Life = maxLife;
        r.LineWidth = lineWidth;
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
        var ripples = new List<(Vector2 Center, float Radius, int Segs, uint GlowCol, float GlowThick, uint MainCol, float MainThick)>();

        for (var i = 0; i < _ripples.Length; i++)
        {
            ref var r = ref _ripples[i];
            if (r.Life <= 0f || r.Radius <= 0f) continue;

            var lifeRatio = Math.Max(0f, r.Life / r.MaxLife);
            var numSegments = Math.Max(32, (int)(MathF.Tau * r.Radius / 3f));
            var thickness = r.LineWidth * (0.3f + 0.7f * lifeRatio);

            ripples.Add((r.Center, r.Radius, numSegments,
                EffectUtils.PackColor(accent.X, accent.Y, accent.Z, lifeRatio * 0.18f), thickness + 4f,
                EffectUtils.PackColor(accent.X, accent.Y, accent.Z, lifeRatio * 0.7f), thickness));
        }

        _back.Ripples = ripples.ToArray();
    }

    private sealed class FrameData
    {
        public (Vector2 Center, float Radius, int Segs, uint GlowCol, float GlowThick, uint MainCol, float MainThick)[]? Ripples;
    }
}
