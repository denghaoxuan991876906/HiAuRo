using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class ParticleSystem
{
    private struct Particle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Size;
        public float Life;
        public float MaxLife;
    }

    private readonly Particle[] _pool;
    private readonly int _maxParticles;
    private readonly float _spawnRate;
    private float _spawnAccum;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public ParticleSystem(int maxParticles = 60, float spawnRate = 8f)
    {
        _maxParticles = maxParticles;
        _pool = new Particle[maxParticles];
        _spawnRate = spawnRate;
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _spawnAccum += _spawnRate * dt;
        while (_spawnAccum >= 1f)
        {
            _spawnAccum -= 1f;
            SpawnParticle(min, max);
        }

        for (var i = 0; i < _pool.Length; i++)
        {
            ref var p = ref _pool[i];
            if (p.Life <= 0f) continue;

            p.Life -= dt;
            p.Pos += p.Vel * dt;

            if (p.Pos.X < min.X || p.Pos.X > max.X) p.Vel.X *= -1f;
            if (p.Pos.Y < min.Y || p.Pos.Y > max.Y) p.Vel.Y *= -1f;
            p.Pos = Vector2.Clamp(p.Pos, min, max);
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
        if (data.Dots == null) return;

        foreach (var (pos, radius, col) in data.Dots)
            dl.AddCircleFilled(pos, radius, col);
    }

    private void SpawnParticle(Vector2 min, Vector2 max)
    {
        var oldestIdx = -1;
        var oldestLife = float.MaxValue;

        for (var i = 0; i < _pool.Length; i++)
        {
            if (_pool[i].Life <= 0f) { oldestIdx = i; break; }
            if (_pool[i].Life < oldestLife) { oldestLife = _pool[i].Life; oldestIdx = i; }
        }

        if (oldestIdx < 0) return;

        ref var p = ref _pool[oldestIdx];
        p.Pos = new Vector2(min.X + Random.Shared.NextSingle() * (max.X - min.X), min.Y + Random.Shared.NextSingle() * (max.Y - min.Y));
        p.Vel = new Vector2((Random.Shared.NextSingle() - 0.5f) * 12f, (Random.Shared.NextSingle() - 0.5f) * 12f);
        p.Size = 2f + Random.Shared.NextSingle() * 4f;
        p.MaxLife = 2f + Random.Shared.NextSingle() * 2f;
        p.Life = p.MaxLife;
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
        var dots = new List<(Vector2 Pos, float Radius, uint Col)>();

        for (var i = 0; i < _pool.Length; i++)
        {
            ref var p = ref _pool[i];
            if (p.Life <= 0f) continue;

            var alpha = Math.Max(0f, p.Life / p.MaxLife);
            dots.Add((p.Pos, p.Size * (0.5f + alpha * 0.5f), EffectUtils.PackColor(accent.X, accent.Y, accent.Z, alpha * 0.35f)));
        }

        _back.Dots = dots.ToArray();
    }

    private sealed class FrameData
    {
        public (Vector2 Pos, float Radius, uint Col)[]? Dots;
    }
}
