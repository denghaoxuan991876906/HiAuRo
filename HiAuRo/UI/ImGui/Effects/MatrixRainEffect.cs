using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class MatrixRainEffect
{
    private struct RainDrop
    {
        public float X;
        public float Y;
        public float Speed;
        public float Life;
        public float MaxLife;
        public StringBuilder Chars;
    }

    private readonly RainDrop[] _drops;
    private float _lastWidth;

    private const string CharSet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        "@#$%&*+=-~<>{}[]|/\\!?" +
        "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ";

    private bool _isLight;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public MatrixRainEffect(int maxDrops = 80)
    {
        _drops = new RainDrop[maxDrops];
        for (var i = 0; i < _drops.Length; i++)
            _drops[i].Chars = new StringBuilder(16);
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _isLight = Theme.Mode == Theme.ThemeMode.Light;

        var width = max.X - min.X;
        var colSpacing = 14f;
        var colCount = Math.Max(1, (int)(width / colSpacing));

        if (Math.Abs(width - _lastWidth) > colSpacing)
        {
            _lastWidth = width;
            for (var i = 0; i < _drops.Length; i++)
            {
                ref var d = ref _drops[i];
                if (d.Life <= 0f) ResetDrop(ref d, min, max, colCount, colSpacing);
            }
        }

        for (var i = 0; i < _drops.Length; i++)
        {
            ref var drop = ref _drops[i];

            if (drop.Life <= 0f) { ResetDrop(ref drop, min, max, colCount, colSpacing); continue; }

            drop.Life -= dt;
            drop.Y += drop.Speed * dt;

            if (drop.Chars.Length > 0 && Random.Shared.NextSingle() < 0.1f)
            {
                var idx = Random.Shared.Next(drop.Chars.Length);
                drop.Chars[idx] = CharSet[Random.Shared.Next(CharSet.Length)];
            }

            if (drop.Y > max.Y || drop.Life <= 0f) ResetDrop(ref drop, min, max, colCount, colSpacing);
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            var isLight = _isLight;
            _computeTask = Task.Run(() => ComputeFrameData(isLight));
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var data = Volatile.Read(ref _front);
        if (data.Chars != null)
            foreach (var (pos, col, ch) in data.Chars)
                dl.AddText(pos, col, ch.ToString());

        dl.PopClipRect();
    }

    private void ResetDrop(ref RainDrop drop, Vector2 min, Vector2 max, int colCount, float colSpacing)
    {
        var col = Random.Shared.Next(colCount);
        drop.X = min.X + col * colSpacing + Random.Shared.NextSingle() * 4f;
        drop.Y = min.Y + Random.Shared.NextSingle() * 20f;
        drop.Speed = 60f + Random.Shared.NextSingle() * 60f;
        drop.MaxLife = 3f + Random.Shared.NextSingle() * 5f;
        drop.Life = drop.MaxLife;

        var len = 4 + Random.Shared.Next(10);
        drop.Chars.Clear();
        for (var i = 0; i < len; i++)
            drop.Chars.Append(CharSet[Random.Shared.Next(CharSet.Length)]);
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData(bool isLight)
    {
        var chars = new List<(Vector2 Pos, uint Col, char Ch)>();

        for (var i = 0; i < _drops.Length; i++)
        {
            ref var drop = ref _drops[i];
            if (drop.Life <= 0f || drop.Chars.Length == 0) continue;

            var lifeRatio = Math.Max(0f, drop.Life / drop.MaxLife);
            var tailLen = drop.Chars.Length;

            for (var j = 0; j < tailLen; j++)
            {
                var charY = drop.Y - j * 14f;

                float alpha;
                if (j == 0)
                    alpha = 0.95f;
                else
                    alpha = Math.Max(0.08f, 0.6f * (1f - (float)j / tailLen));

                alpha *= Math.Min(1f, lifeRatio * 3f);

                uint col;
                if (j == 0)
                {
                    col = isLight
                        ? EffectUtils.PackColor(0.05f, 0.15f, 0.35f, alpha)
                        : EffectUtils.PackColor(0.85f, 0.95f, 1f, alpha);
                }
                else
                {
                    col = isLight
                        ? EffectUtils.PackColor(0.02f, 0.4f, 0.15f, alpha)
                        : EffectUtils.PackColor(0f, 1f, 0.4f, alpha);
                }

                chars.Add((new Vector2(drop.X, charY), col, drop.Chars[j]));
            }
        }

        _back.Chars = chars.ToArray();
    }

    private sealed class FrameData
    {
        public (Vector2 Pos, uint Col, char Ch)[]? Chars;
    }
}
