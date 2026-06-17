using System.Collections.Concurrent;
using System.Numerics;
using OmenTools.Interop.Game.Helpers;

namespace HiAuRo.Rendering;

public enum DebugPointColor
{
    Red,
    Green,
    Blue,
    Yellow,
    White,
}

public static class DebugPoint
{
    private static readonly ConcurrentDictionary<int, Entry> _points = new();
    private static int _nextId;

    private static readonly Dictionary<DebugPointColor, Vector4> Colors = new()
    {
        [DebugPointColor.Red] = new(1f, 0.3f, 0.3f, 1f),
        [DebugPointColor.Green] = new(0.3f, 1f, 0.3f, 1f),
        [DebugPointColor.Blue] = new(0.3f, 0.6f, 1f, 1f),
        [DebugPointColor.Yellow] = new(1f, 0.9f, 0.2f, 1f),
        [DebugPointColor.White] = new(1f, 1f, 1f, 1f),
    };

    public static int Add(Vector3 worldPos, string? label = null, DebugPointColor color = DebugPointColor.Red)
    {
        var id = Interlocked.Increment(ref _nextId);
        _points[id] = new Entry(worldPos, label, color);
        return id;
    }
    public static List<int> Add(List<Vector3> worldPos, string? label = null, DebugPointColor color = DebugPointColor.Red)
    {
        List<int> ids = new();
        foreach (var pos in worldPos)
        {
            var id = Interlocked.Increment(ref _nextId);
            _points[id] = new Entry(pos, label, color);
            ids.Add(id);
        }
        return ids;
    }

    public static bool Remove(int id) => _points.TryRemove(id, out _);

    public static void Clear()
    {
        _points.Clear();
        Interlocked.Exchange(ref _nextId, 0);
    }

    public static void Draw()
    {
        if (_points.IsEmpty) return;

        try
        {
            var dl = ImGui.GetForegroundDrawList();

            foreach (var (id, entry) in _points)
            {
                if (!GameViewHelper.WorldToScreen(entry.WorldPos, out var screen, out _)) continue;

                var col = Colors.GetValueOrDefault(entry.Color, Colors[DebugPointColor.Red]);
                var colU32 = ImGui.ColorConvertFloat4ToU32(col);

                dl.AddCircleFilled(screen, 5f, colU32, 16);
                dl.AddCircle(screen, 5f, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.6f)), 16, 1f);

                var text = entry.Label ?? $"#{id}";
                dl.AddText(screen + new Vector2(8, -4), colU32, text);
            }
        }
        catch
        {
        }
    }

    private readonly record struct Entry(Vector3 WorldPos, string? Label, DebugPointColor Color);
}
