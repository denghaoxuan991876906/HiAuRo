using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class FireflyEffect
{
    private struct Firefly
    {
        public Vector2 Pos;
        public Vector2 TargetPos;
        public float Size;
        public float BrightnessPhase;
        public float BrightnessFreq;
        public float Speed;
        public float RetargetTimer;
        public float RetargetInterval;
        public Vector3 Color;
    }

    private readonly Firefly[] _fireflies;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public FireflyEffect(int count = 28)
    {
        _fireflies = new Firefly[count];
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        var pad = 30f;
        var innerMinX = min.X + pad;
        var innerMinY = min.Y + pad;
        var innerMaxX = max.X - pad;
        var innerMaxY = max.Y - pad;
        var rangeX = innerMaxX - innerMinX;
        var rangeY = innerMaxY - innerMinY;

        for (var i = 0; i < _fireflies.Length; i++)
        {
            ref var f = ref _fireflies[i];

            if (f.RetargetInterval <= 0f)
            {
                f.Pos = new Vector2(innerMinX + Random.Shared.NextSingle() * rangeX, innerMinY + Random.Shared.NextSingle() * rangeY);
                f.TargetPos = f.Pos;
                f.Size = 3f + Random.Shared.NextSingle() * 5f;
                f.BrightnessPhase = Random.Shared.NextSingle() * MathF.Tau;
                f.BrightnessFreq = 1f + Random.Shared.NextSingle() * 2.5f;
                f.Speed = 30f + Random.Shared.NextSingle() * 60f;
                f.RetargetInterval = 1f + Random.Shared.NextSingle() * 1.5f;
                f.RetargetTimer = f.RetargetInterval;

                var colorRand = Random.Shared.NextSingle();
                f.Color = colorRand < 0.7f ? new Vector3(0.7f, 1f, 0.2f) : new Vector3(1f, 0.9f, 0.3f);
                continue;
            }

            f.RetargetTimer -= dt;
            if (f.RetargetTimer <= 0f)
            {
                f.TargetPos = new Vector2(innerMinX + Random.Shared.NextSingle() * rangeX, innerMinY + Random.Shared.NextSingle() * rangeY);
                f.RetargetInterval = 1f + Random.Shared.NextSingle() * 1.5f;
                f.RetargetTimer = f.RetargetInterval;
            }

            var dx = f.TargetPos.X - f.Pos.X;
            var dy = f.TargetPos.Y - f.Pos.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > 0.5f)
            {
                var moveSpeed = f.Speed * dt;
                var ratio = Math.Min(1f, moveSpeed / dist);
                f.Pos.X += dx * ratio;
                f.Pos.Y += dy * ratio;
            }

            f.BrightnessPhase += f.BrightnessFreq * dt;
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            _computeTask = Task.Run(ComputeFrameData);
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var data = Volatile.Read(ref _front);
        if (data.Flies != null)
            foreach (var (pos, outerR, outerCol, midR, midCol, coreR, coreCol) in data.Flies)
            {
                dl.AddCircleFilled(pos, outerR, outerCol);
                dl.AddCircleFilled(pos, midR, midCol);
                dl.AddCircleFilled(pos, coreR, coreCol);
            }

        dl.PopClipRect();
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData()
    {
        var flies = new List<(Vector2 Pos, float OuterR, uint OuterCol, float MidR, uint MidCol, float CoreR, uint CoreCol)>();

        for (var i = 0; i < _fireflies.Length; i++)
        {
            ref var f = ref _fireflies[i];
            if (f.RetargetInterval <= 0f) continue;

            var brightness = (MathF.Sin(f.BrightnessPhase) + 1f) * 0.5f;
            brightness = 0.3f + brightness * 0.7f;

            var outerAlpha = brightness * 0.08f;
            var midAlpha = brightness * 0.2f;
            var coreAlpha = brightness * 0.8f;

            flies.Add((
                f.Pos,
                f.Size * 3f, EffectUtils.PackColor(f.Color.X, f.Color.Y, f.Color.Z, outerAlpha),
                f.Size * 1.5f, EffectUtils.PackColor(f.Color.X, f.Color.Y, f.Color.Z, midAlpha),
                f.Size, EffectUtils.PackColor(f.Color.X, f.Color.Y, f.Color.Z, coreAlpha)));
        }

        _back.Flies = flies.ToArray();
    }

    private sealed class FrameData
    {
        public (Vector2 Pos, float OuterR, uint OuterCol, float MidR, uint MidCol, float CoreR, uint CoreCol)[]? Flies;
    }
}
