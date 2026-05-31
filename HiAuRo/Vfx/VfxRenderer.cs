using System.Numerics;
using System.Text.RegularExpressions;

namespace HiAuRo.Vfx;

public sealed class VfxRenderer
{
    public static VfxRenderer? Instance { get; private set; }

    readonly List<VfxZone> _activeZones = [];

    public VfxRenderer()
    {
        Instance = this;
        VfxNative.Initialize();
    }

    public IVfxZone ShowCircle(Vector3 pos, float radius,
        Vector4? color = null, float duration = -1f, string? tag = null)
        => Show(VfxPath.Circle, pos, new Vector3(radius, 1f, radius), 0f, duration, tag, color);

    public IVfxZone ShowRect(Vector3 pos, float width, float length,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string? tag = null)
        => Show(VfxPath.Rect, pos, new Vector3(width, 1f, length), rotation, duration, tag, color);

    public IVfxZone ShowFan(Vector3 pos, float radius, float halfAngle,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string? tag = null)
    {
        var deg = (int)(halfAngle * 2f * 180f / MathF.PI);
        var path = deg switch
        {
            <= 20  => VfxPath.Fan20,
            <= 30  => VfxPath.Fan30,
            <= 45  => VfxPath.Fan45,
            <= 60  => VfxPath.Fan60,
            <= 90  => VfxPath.Fan90,
            <= 100 => VfxPath.Fan100,
            <= 120 => VfxPath.Fan120,
            <= 150 => VfxPath.Fan150,
            <= 180 => VfxPath.Fan180,
            _      => VfxPath.Fan270,
        };
        return Show(path, pos, new Vector3(radius, 1f, radius), rotation, duration, tag, color);
    }

    public IVfxZone ShowRing(Vector3 pos, float innerR, float outerR,
        Vector4? color = null, float duration = -1f, string? tag = null)
    {
        var thickness = outerR - innerR;
        return Show(VfxPath.Ring, pos, new Vector3(outerR, thickness, outerR), 0f, duration, tag, color);
    }

    public unsafe IVfxZone ShowCross(Vector3 pos, float length, float width,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string? tag = null)
    {
        Show(VfxPath.Cross, pos, new Vector3(width, 1f, length), rotation, duration, tag, color);
        Show(VfxPath.Cross, pos, new Vector3(width, 1f, length), rotation + MathF.PI / 2f, duration, tag, color);
        return new VfxZone(null, pos, rotation, new Vector3(width, 1f, length), duration, tag ?? "");
    }

    public IVfxZone ShowRingFan(Vector3 pos, float innerR, float outerR, float halfAngle,
        float rotation = 0f, Vector4? color = null, float duration = -1f, string? tag = null)
    {
        var thickness = outerR - innerR;
        return Show(VfxPath.Donut, pos, new Vector3(outerR, thickness, outerR), rotation, duration, tag, color);
    }

    public IVfxZone ShowLine(Vector3 start, Vector3 end, float width,
        Vector4? color = null, float duration = -1f, string? tag = null)
    {
        var center = (start + end) * 0.5f;
        var dx = end.X - start.X;
        var dz = end.Z - start.Z;
        var length = MathF.Sqrt(dx * dx + dz * dz);
        var rotation = MathF.Atan2(dx, dz);

        return Show(VfxPath.Rect, center, new Vector3(width, 1f, length), rotation, duration, tag, color);
    }

    public unsafe IVfxZone Show(string vfxPath, Vector3 pos, Vector3 scale,
        float rotation = 0f, float duration = -1f, string? tag = null, Vector4? color = null)
    {
        var vfx = VfxNative.Create(vfxPath, pos, scale, rotation, color);
        var zone = new VfxZone(vfx, pos, rotation, scale, duration, tag ?? "");

        lock (_activeZones) _activeZones.Add(zone);
        return zone;
    }

    public void RemoveByTag(string tag)
    {
        lock (_activeZones)
        {
            for (var i = _activeZones.Count - 1; i >= 0; i--)
            {
                if (_activeZones[i].Tag == tag)
                {
                    _activeZones[i].Dispose();
                    _activeZones.RemoveAt(i);
                }
            }
        }
    }

    public void RemoveByTagRegex(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        lock (_activeZones)
        {
            for (var i = _activeZones.Count - 1; i >= 0; i--)
            {
                if (regex.IsMatch(_activeZones[i].Tag))
                {
                    _activeZones[i].Dispose();
                    _activeZones.RemoveAt(i);
                }
            }
        }
    }

    public void Update(float deltaSeconds)
    {
        lock (_activeZones)
        {
            for (var i = _activeZones.Count - 1; i >= 0; i--)
            {
                if (!_activeZones[i].Tick(deltaSeconds))
                {
                    _activeZones[i].Dispose();
                    _activeZones.RemoveAt(i);
                }
            }
        }
    }

    public void Clear()
    {
        lock (_activeZones)
        {
            foreach (var zone in _activeZones)
                zone.Dispose();
            _activeZones.Clear();
        }
    }

    public void Dispose()
    {
        Clear();
        Instance = null;
    }
}
