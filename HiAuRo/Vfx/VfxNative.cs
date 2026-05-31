using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace HiAuRo.Vfx;

public static unsafe class VfxNative
{
    const string CreateSig = "E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08";
    const string RunSig = "E8 ?? ?? ?? ?? B0 02 EB 02";
    const string RemoveSig = "40 53 48 83 EC 20 48 8B D9 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 28 33 D2 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9";

    delegate VfxObject* CreateDelegate(byte* path, byte* pool);
    delegate IntPtr RunDelegate(VfxObject* vfx, float a1, uint a2);
    delegate IntPtr RemoveDelegate(VfxObject* vfx);

    static CreateDelegate? _create;
    static RunDelegate? _run;
    static RemoveDelegate? _remove;
    static bool _initialized;

    const string PoolName = "Client.System.Scheduler.Instance.VfxObject";
    const int PositionOffset = 0x50;
    const int RotationOffset = 0x60;
    const int ScaleOffset = 0x70;
    const int ColorOffset = 0x260;

    public static bool IsAvailable => _initialized && _create != null && _run != null;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            var sig = DService.Instance().SigScanner;
            if (sig == null) return;

            var crAddr = sig.ScanText(CreateSig);
            var runAddr = sig.ScanText(RunSig);
            var rmAddr = sig.ScanText(RemoveSig);

            if (crAddr == IntPtr.Zero || runAddr == IntPtr.Zero || rmAddr == IntPtr.Zero) return;

            _create = Marshal.GetDelegateForFunctionPointer<CreateDelegate>(crAddr);
            _run = Marshal.GetDelegateForFunctionPointer<RunDelegate>(runAddr);
            _remove = Marshal.GetDelegateForFunctionPointer<RemoveDelegate>(rmAddr);
        }
        catch
        {
            _create = null; _run = null; _remove = null;
        }
    }

    public static VfxObject* Create(string path, Vector3 pos, Vector3 scale, float rotation, Vector4? color = null)
    {
        if (_create == null || _run == null) return null;

        var pathBytes = Encoding.UTF8.GetBytes(path + "\0");
        var poolBytes = Encoding.UTF8.GetBytes(PoolName + "\0");

        fixed (byte* pathPtr = pathBytes)
        fixed (byte* poolPtr = poolBytes)
        {
            try
            {
                var vfx = _create(pathPtr, poolPtr);
                if (vfx == null) return null;

                SetPosition(vfx, pos);
                SetScale(vfx, scale);
                SetRotation(vfx, rotation);
                if (color.HasValue)
                    SetColor(vfx, color.Value);

                _run(vfx, 0f, 0xFFFFFFFF);
                return vfx;
            }
            catch
            {
                return null;
            }
        }
    }

    static void SetPosition(VfxObject* vfx, Vector3 pos)
    {
        *(Vector3*)((byte*)vfx + PositionOffset) = pos;
    }

    static void SetScale(VfxObject* vfx, Vector3 scale)
    {
        *(Vector3*)((byte*)vfx + ScaleOffset) = scale;
    }

    static void SetRotation(VfxObject* vfx, float yaw)
    {
        var q = Quaternion.CreateFromYawPitchRoll(yaw, 0f, 0f);
        *(Quaternion*)((byte*)vfx + RotationOffset) = q;
    }

    static void SetColor(VfxObject* vfx, Vector4 color)
    {
        *(Vector4*)((byte*)vfx + ColorOffset) = color;
    }

    public static void Destroy(VfxObject* vfx)
    {
        if (_remove == null || vfx == null) return;
        try { _remove(vfx); } catch { }
    }
}
