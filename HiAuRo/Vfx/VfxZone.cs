using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace HiAuRo.Vfx;

public interface IVfxZone : IDisposable
{
    Vector3 Position { get; }
    float Rotation { get; }
    Vector3 Scale { get; }
    float Duration { get; }
    string Tag { get; }
    bool IsValid { get; }
}

public sealed unsafe class VfxZone : IVfxZone
{
    VfxObject* _vfx;
    readonly float _duration;
    float _elapsed;
    bool _disposed;

    public Vector3 Position { get; }
    public float Rotation { get; }
    public Vector3 Scale { get; }
    public float Duration => _duration;
    public string Tag { get; }
    public bool IsValid => !_disposed && _vfx != null;

    internal VfxZone(VfxObject* vfx, Vector3 pos, float rotation, Vector3 scale, float duration, string tag)
    {
        _vfx = vfx;
        Position = pos;
        Rotation = rotation;
        Scale = scale;
        _duration = duration;
        Tag = tag;
        _elapsed = 0f;
    }

    internal bool Tick(float dt)
    {
        if (_disposed) return false;
        if (_vfx == null) { _disposed = true; return false; }
        if (_duration < 0f) return true;

        _elapsed += dt;
        if (_elapsed >= _duration)
        {
            Dispose();
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_vfx != null)
        {
            VfxNative.Destroy(_vfx);
            _vfx = null;
        }
    }
}
